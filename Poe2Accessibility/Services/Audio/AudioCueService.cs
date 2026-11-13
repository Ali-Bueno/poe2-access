namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    ///     Plays short synthesized cues: mono sample arrays mixed into one stereo stream through winmm's waveOut
    ///     (part of Windows, no extra DLL), each at a pitch ratio with an equal-power pan, on a mixer thread; a recorded
    ///     stereo cue of the player's own news plays unpanned, as recorded.
    /// </summary>
    public sealed partial class AudioCueService : IDisposable
    {
        public const int SampleRate = 44100;

        private const int Channels = 2;
        private const int BytesPerSample = 2;

        /// <summary>About 23 ms per buffer and three in flight: cues start within ~70 ms.</summary>
        private const int BufferFrames = 1024;
        private const int BufferCount = 3;

        /// <summary>The game's cues last up to 0.9 s and a room's chests and urns all sound: past this the oldest fades out.</summary>
        private const int MaxVoices = 32;

        /// <summary>A cue let go before its end fades over this many frames (20 ms): cut dead it clicks.</summary>
        private const int LetGoFrames = SampleRate / 50;

        /// <summary>Drives within a twentieth of each other share one limited copy of a cue.</summary>
        private const float DriveStep = 0.05f;

        /// <summary>Limited copies kept; past this the cache starts over.</summary>
        private const int MaxDriven = 64;

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
        private readonly PeakLimiter output = new(SampleRate);
        private readonly Dictionary<string, int> started = new(StringComparer.Ordinal);
        private volatile float outputPeak;
        private readonly Dictionary<(float[] Left, float[] Right, int Drive), (float[] Left, float[] Right)> driven = new();
        private readonly Thread? mixer;
        private volatile IReadOnlyDictionary<string, float> levels = new Dictionary<string, float>();
        private IntPtr device;
        private volatile bool stopping;

        /// <summary>Whether the last write was refused (the output device unplugged or taken): logged once per loss, not every buffer.</summary>
        private bool writeFailed;

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

        /// <summary>Each sound's own volume by its glossary key, percent of its group's (settings SoundLevels); unset is 100.</summary>
        public void SetLevels(IReadOnlyDictionary<string, int>? percents)
        {
            var fractions = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var (sound, percent) in percents ?? new Dictionary<string, int>())
            {
                fractions[sound] = Fraction(percent);
            }

            this.levels = fractions;
        }

        /// <summary>A volume setting's percent as the factor the mixer takes.</summary>
        public static float Fraction(int percent) => Math.Clamp(percent, 0, SettingsMenu.MaxVolume) / 100f;

        /// <summary>Equal-power pan: the left and right gains of a sound at <paramref name="pan"/> (-1 left … 1 right).</summary>
        private static (float Left, float Right) Panned(float pan, float gain)
        {
            var angle = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI / 4f;
            return (MathF.Cos(angle) * gain, MathF.Sin(angle) * gain);
        }

        /// <summary>
        ///     Whether a side, a pitch and a gain are numbers: one that is not (a place read as garbage for a frame) mixed in
        ///     makes the whole output no number, and in a bed it stayed so: every sound silent until the mod started again.
        /// </summary>
        public static bool IsSound(float pan, float pitch, float gain) => float.IsFinite(pan) && float.IsFinite(pitch) && float.IsFinite(gain);

        /// <summary>A sound's own volume, 1 = as designed.</summary>
        public float Level(string sound) => this.levels.TryGetValue(sound, out var level) ? level : 1f;

        /// <summary>
        ///     Starts a cue. <paramref name="pan"/> -1 left … 1 right; <paramref name="pitch"/> 1 = as synthesized. A
        ///     <paramref name="gain"/> above 1 plays a limited copy (<see cref="PeakLimiter.Drive"/>): its quiet body grows while
        ///     its peaks are held under full scale, without the distortion of clipping them.
        ///     <paramref name="sound"/> is its glossary key, whose own volume scales the gain.
        /// </summary>
        public void Play(float[] samples, float pan, float pitch, float gain, string sound)
        {
            gain *= this.Level(sound);
            if (!this.IsAvailable || samples.Length == 0 || pitch <= 0 || gain <= 0 || !IsSound(pan, pitch, gain))
            {
                return;
            }

            this.Note(sound);
            var (leftGain, rightGain) = Panned(pan, MathF.Min(gain, 1f));
            var (left, right) = this.Driven(samples, samples, gain);
            this.Add(new Voice(left, right, pitch, leftGain, rightGain));
        }

        /// <summary>Starts a recorded stereo cue as recorded, unpanned (the player's own news); pitch and gain as for a mono cue.</summary>
        public void Play(StereoSound sound, float pitch, float gain, string key)
        {
            gain *= this.Level(key);
            if (!this.IsAvailable || sound.Left.Length == 0 || pitch <= 0 || gain <= 0 || !IsSound(0f, pitch, gain))
            {
                return;
            }

            this.Note(key);
            var level = MathF.Min(gain, 1f);
            var (left, right) = this.Driven(sound.Left, sound.Right, gain);
            this.Add(new Voice(left, right, pitch, level, level));
        }

        /// <summary>Research (dev command "sounds"): what started and how loud the output got since last asked, and the beds heard now.</summary>
        public string Diagnose()
        {
            string started;
            lock (this.started)
            {
                started = string.Join(", ", this.started.OrderByDescending(sound => sound.Value).Select(sound => $"{sound.Key} {sound.Value}"));
                this.started.Clear();
            }

            string beds;
            lock (this.beds)
            {
                beds = string.Join(", ", this.beds.Where(bed => bed.IsHeard).Select(bed => bed.Sound).Distinct());
            }

            var peak = this.outputPeak;
            this.outputPeak = 0f;
            return $"Sounds: output peak {peak:F3}; started: {(started.Length == 0 ? "none" : started)}; beds heard: {(beds.Length == 0 ? "none" : beds)}";
        }

        private void Note(string sound)
        {
            lock (this.started)
            {
                this.started[sound] = this.started.GetValueOrDefault(sound) + 1;
            }
        }

        /// <summary>The cue as is up to a gain of 1; above it a limited copy at that drive, made once and kept.</summary>
        private (float[] Left, float[] Right) Driven(float[] left, float[] right, float gain)
        {
            var step = (int)MathF.Round(gain / DriveStep);
            if (step * DriveStep <= 1f)
            {
                return (left, right);
            }

            lock (this.driven)
            {
                if (!this.driven.TryGetValue((left, right, step), out var copy))
                {
                    if (this.driven.Count >= MaxDriven)
                    {
                        this.driven.Clear();
                    }

                    copy = PeakLimiter.Drive(left, right, step * DriveStep, SampleRate);
                    this.driven[(left, right, step)] = copy;
                }

                return copy;
            }
        }

        /// <summary>
        ///     A looping sound mixed under the cues until the caller sets it silent (wall tones) or stops it for good; its own
        ///     volume by <paramref name="sound"/>, heard from sample <paramref name="start"/> on.
        /// </summary>
        public Bed AddBed(float[] loop, string sound, int start = 0)
        {
            var bed = new Bed(loop, sound, () => this.Level(sound), start);
            if (this.IsAvailable)
            {
                lock (this.beds)
                {
                    this.beds.Add(bed);
                }
            }

            return bed;
        }

        /// <summary>Lets every cue go at once (a menu opened, the area changed).</summary>
        public void StopAll()
        {
            lock (this.voices)
            {
                foreach (var voice in this.voices)
                {
                    voice.LetGo();
                }
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
            var result = waveOutWrite(this.device, header, (uint)HeaderSize);
            if (result != 0 && !this.writeFailed)
            {
                this.writeFailed = true;
                this.log.Warn($"Audio cues: the output device refused a buffer (waveOutWrite returned {result}); the cues are silent until it answers again");
            }
            else if (result == 0 && this.writeFailed)
            {
                this.writeFailed = false;
                this.log.Info("Audio cues: the output device answers again");
            }
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
                for (var b = this.beds.Count - 1; b >= 0; b--)
                {
                    if (this.beds[b].MixInto(this.mix))
                    {
                        this.beds.RemoveAt(b);
                    }
                }
            }

            var volume = this.Volume;
            for (var i = 0; i < this.mix.Length; i += Channels)
            {
                var left = this.mix[i] * volume;
                var right = this.mix[i + 1] * volume;
                this.output.Process(ref left, ref right);
                this.outputPeak = MathF.Max(this.outputPeak, MathF.Max(MathF.Abs(left), MathF.Abs(right)));
                this.pcm[i] = (short)(Math.Clamp(left, -1f, 1f) * short.MaxValue);
                this.pcm[i + 1] = (short)(Math.Clamp(right, -1f, 1f) * short.MaxValue);
            }
        }
    }
}
