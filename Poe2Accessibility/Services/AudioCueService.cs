namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    ///     Plays short synthesized cues: mono sample arrays mixed into one stereo stream through winmm's waveOut
    ///     (part of Windows, no extra DLL), each at a pitch ratio with an equal-power pan, on a mixer thread.
    /// </summary>
    public sealed class AudioCueService : IDisposable
    {
        public const int SampleRate = 44100;

        private const int Channels = 2;
        private const int BytesPerSample = 2;

        /// <summary>About 23 ms per buffer and three in flight: cues start within ~70 ms.</summary>
        private const int BufferFrames = 1024;
        private const int BufferCount = 3;

        /// <summary>A cue lasts ~0.1 s; more voices than this means stale cues piled up.</summary>
        private const int MaxVoices = 16;

        private const int WaitMs = 50;
        private const uint WaveMapper = 0xFFFFFFFF;
        private const uint CallbackEvent = 0x00050000;
        private const uint HeaderDone = 0x1;
        private const ushort PcmFormat = 1;

        private static readonly int HeaderSize = Marshal.SizeOf<WaveHeader>();
        private static readonly int FlagsOffset = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));

        private readonly PluginLog log;
        private readonly List<Voice> voices = new();
        private readonly List<Bed> beds = new();
        private readonly AutoResetEvent bufferDone = new(false);
        private readonly IntPtr[] headers = new IntPtr[BufferCount];
        private readonly float[] mix = new float[BufferFrames * Channels];
        private readonly short[] pcm = new short[BufferFrames * Channels];
        private readonly Thread? mixer;
        private IntPtr device;
        private volatile bool stopping;

        public AudioCueService(PluginLog log)
        {
            this.log = log;
            var format = new WaveFormat
            {
                FormatTag = PcmFormat,
                Channels = Channels,
                SamplesPerSec = SampleRate,
                AvgBytesPerSec = SampleRate * Channels * BytesPerSample,
                BlockAlign = Channels * BytesPerSample,
                BitsPerSample = BytesPerSample * 8,
            };
            var result = waveOutOpen(out this.device, WaveMapper, ref format, this.bufferDone.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent);
            if (result != 0)
            {
                this.log.Warn($"Audio cues off: waveOutOpen failed with {result}");
                this.device = IntPtr.Zero;
                return;
            }

            for (var i = 0; i < BufferCount; i++)
            {
                var data = Marshal.AllocHGlobal(BufferFrames * Channels * BytesPerSample);
                this.headers[i] = Marshal.AllocHGlobal(HeaderSize);
                Marshal.StructureToPtr(new WaveHeader { Data = data, BufferLength = (uint)(BufferFrames * Channels * BytesPerSample) }, this.headers[i], false);
                waveOutPrepareHeader(this.device, this.headers[i], (uint)HeaderSize);
            }

            this.mixer = new Thread(this.Run) { IsBackground = true, Name = "Poe2Accessibility audio" };
            this.mixer.Start();
            this.log.Info("Audio cues ready (waveOut, 44.1 kHz stereo).");
        }

        public bool IsAvailable => this.device != IntPtr.Zero;

        /// <summary>Master gain applied to every cue (settings volume).</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        ///     Starts a cue. <paramref name="pan"/> -1 left … 1 right; <paramref name="pitch"/> 1 = as synthesized. A
        ///     <paramref name="gain"/> above 1 drives the cue alone into the soft limit, so its quiet body grows while its
        ///     peaks stay under full scale: louder, where a plain gain would only be clipped by the output stage.
        /// </summary>
        public void Play(float[] samples, float pan, float pitch, float gain)
        {
            if (!this.IsAvailable || samples.Length == 0 || pitch <= 0)
            {
                return;
            }

            var angle = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI / 4f;
            lock (this.voices)
            {
                if (this.voices.Count >= MaxVoices)
                {
                    this.voices.RemoveAt(0);
                }

                var level = MathF.Min(gain, 1f);
                this.voices.Add(new Voice(samples, pitch, MathF.Cos(angle) * level, MathF.Sin(angle) * level, MathF.Max(gain, 1f)));
            }
        }

        /// <summary>A looping sound mixed under the cues until the caller sets it silent (wall tones).</summary>
        public Bed AddBed(float[] loop)
        {
            var bed = new Bed(loop);
            lock (this.beds)
            {
                this.beds.Add(bed);
            }

            return bed;
        }

        /// <summary>Cuts every cue at once (a menu opened, the area changed).</summary>
        public void StopAll()
        {
            lock (this.voices)
            {
                this.voices.Clear();
            }
        }

        public void Dispose()
        {
            if (this.device == IntPtr.Zero)
            {
                return;
            }

            this.stopping = true;
            this.bufferDone.Set();
            if (this.mixer != null && !this.mixer.Join(500))
            {
                // A mixer stalled in the driver may still write into the buffers: they are left to the process, never freed under it.
                this.log.Warn("Audio cues: the mixer did not stop in time; its buffers are left allocated");
                this.device = IntPtr.Zero;
                return;
            }

            waveOutReset(this.device);
            foreach (var header in this.headers)
            {
                waveOutUnprepareHeader(this.device, header, (uint)HeaderSize);
                Marshal.FreeHGlobal(Marshal.PtrToStructure<WaveHeader>(header).Data);
                Marshal.FreeHGlobal(header);
            }

            waveOutClose(this.device);
            this.device = IntPtr.Zero;
        }

        private void Run()
        {
            try
            {
                foreach (var header in this.headers)
                {
                    this.Submit(header);
                }

                while (!this.stopping)
                {
                    this.bufferDone.WaitOne(WaitMs);
                    foreach (var header in this.headers)
                    {
                        if (!this.stopping && (Marshal.ReadInt32(header, FlagsOffset) & HeaderDone) != 0)
                        {
                            this.Submit(header);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.log.Error("Audio mixer stopped", ex);
            }
        }

        private void Submit(IntPtr header)
        {
            this.Mix();
            Marshal.Copy(this.pcm, 0, Marshal.PtrToStructure<WaveHeader>(header).Data, this.pcm.Length);
            waveOutWrite(this.device, header, (uint)HeaderSize);
        }

        private void Mix()
        {
            Array.Clear(this.mix);
            lock (this.voices)
            {
                for (var v = this.voices.Count - 1; v >= 0; v--)
                {
                    if (this.voices[v].MixInto(this.mix))
                    {
                        this.voices.RemoveAt(v);
                    }
                }
            }

            lock (this.beds)
            {
                foreach (var bed in this.beds)
                {
                    bed.MixInto(this.mix);
                }
            }

            var volume = this.Volume;
            for (var i = 0; i < this.mix.Length; i++)
            {
                this.pcm[i] = (short)(CueSynth.Limit(this.mix[i] * volume) * short.MaxValue);
            }
        }

        /// <summary>A loop whose level and side glide to what the game thread last set; skipped while silent.</summary>
        public sealed class Bed
        {
            /// <summary>Per-sample step toward the target level: ~11 ms, so changes never click.</summary>
            private const float Glide = 0.002f;

            private const float Silence = 1e-5f;

            private readonly float[] loop;
            private double position;
            private float left;
            private float right;
            private float pitch = 1f;
            private volatile float targetLeft;
            private volatile float targetRight;
            private volatile float targetPitch = 1f;

            internal Bed(float[] loop) => this.loop = loop;

            /// <summary>Equal-power pan as for cues; gain 0 fades it out; pitch 1 = as recorded.</summary>
            public void Set(float pan, float gain, float pitch = 1f)
            {
                var angle = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI / 4f;
                this.targetLeft = MathF.Cos(angle) * gain;
                this.targetRight = MathF.Sin(angle) * gain;
                this.targetPitch = pitch > 0 ? pitch : 1f;
            }

            internal void MixInto(float[] mix)
            {
                var toLeft = this.targetLeft;
                var toRight = this.targetRight;
                if (this.loop.Length == 0 || (toLeft == 0 && toRight == 0 && this.left < Silence && this.right < Silence))
                {
                    this.left = 0;
                    this.right = 0;
                    return;
                }

                var toPitch = this.targetPitch;
                for (var frame = 0; frame < mix.Length / Channels; frame++)
                {
                    this.left += (toLeft - this.left) * Glide;
                    this.right += (toRight - this.right) * Glide;
                    this.pitch += (toPitch - this.pitch) * Glide;
                    var index = (int)this.position;
                    var next = index + 1 < this.loop.Length ? index + 1 : 0;
                    var sample = this.loop[index] + ((this.loop[next] - this.loop[index]) * (float)(this.position - index));
                    this.position += this.pitch;
                    if (this.position >= this.loop.Length)
                    {
                        this.position -= this.loop.Length;
                    }
                    mix[frame * Channels] += sample * this.left;
                    mix[(frame * Channels) + 1] += sample * this.right;
                }
            }
        }

        /// <summary>One playing cue: read with linear interpolation at the pitch ratio.</summary>
        private sealed class Voice
        {
            private readonly float[] samples;
            private readonly double step;
            private readonly float left;
            private readonly float right;
            private readonly float drive;
            private double position;

            public Voice(float[] samples, double step, float left, float right, float drive)
            {
                this.samples = samples;
                this.step = step;
                this.left = left;
                this.right = right;
                this.drive = drive;
            }

            /// <summary>Adds the next buffer's worth; true once the cue has ended.</summary>
            public bool MixInto(float[] mix)
            {
                for (var frame = 0; frame < mix.Length / Channels; frame++)
                {
                    var index = (int)this.position;
                    if (index + 1 >= this.samples.Length)
                    {
                        return true;
                    }

                    var fraction = (float)(this.position - index);
                    var sample = this.samples[index] + ((this.samples[index + 1] - this.samples[index]) * fraction);
                    if (this.drive > 1f)
                    {
                        sample = CueSynth.Limit(sample * this.drive);
                    }

                    mix[frame * Channels] += sample * this.left;
                    mix[(frame * Channels) + 1] += sample * this.right;
                    this.position += this.step;
                }

                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public ushort FormatTag;
            public ushort Channels;
            public uint SamplesPerSec;
            public uint AvgBytesPerSec;
            public ushort BlockAlign;
            public ushort BitsPerSample;
            public ushort ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public uint BufferLength;
            public uint BytesRecorded;
            public IntPtr User;
            public uint Flags;
            public uint Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr device);
    }
}
