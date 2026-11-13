namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using System.Threading;

    /// <summary>The beds and voices of <see cref="AudioCueService"/>: the sounds the mixer plays.</summary>
    public sealed partial class AudioCueService
    {
        /// <summary>A loop whose level and side glide to what the game thread last set; skipped while silent.</summary>
        public sealed class Bed
        {
            /// <summary>Per-sample step toward the target level: ~11 ms, so changes never click.</summary>
            private const float Glide = 0.002f;

            /// <summary>The step once stopped: ~120 ms, a thing gone or a menu opened dies away instead of cutting.</summary>
            private const float Release = 1f / (0.12f * SampleRate);

            private const float Silence = 1e-5f;

            private readonly float[] loop;
            private readonly Func<float> level;
            private double position;
            private float left;
            private float right;
            private float pitch = 1f;
            private volatile float targetLeft;
            private volatile float targetRight;
            private volatile float targetPitch = 1f;
            private volatile bool stopped;

            internal Bed(float[] loop, string sound, Func<float> level, int start)
            {
                this.loop = loop;
                this.Sound = sound;
                this.level = level;
                this.position = start;
            }

            /// <summary>Its glossary key.</summary>
            public string Sound { get; }

            /// <summary>Whether it is sounding now.</summary>
            public bool IsHeard => this.left >= Silence || this.right >= Silence;

            /// <summary>Equal-power pan as for cues; gain 0 fades it out; pitch 1 = as recorded.</summary>
            public void Set(float pan, float gain, float pitch = 1f)
            {
                // What is no number is silence where it was (IsSound).
                (this.targetLeft, this.targetRight) = IsSound(pan, pitch, gain) ? Panned(pan, gain) : (0f, 0f);
                this.targetPitch = pitch > 0 && float.IsFinite(pitch) ? pitch : 1f;
            }

            /// <summary>Fades it out for good: the mixer lets it go once silent.</summary>
            public void Stop()
            {
                this.targetLeft = 0f;
                this.targetRight = 0f;
                this.stopped = true;
            }

            /// <summary>Adds the next buffer's worth; true once stopped and silent.</summary>
            internal bool MixInto(float[] mix)
            {
                var level = this.level();
                var toLeft = this.targetLeft * level;
                var toRight = this.targetRight * level;
                if (this.loop.Length == 0 || (toLeft == 0 && toRight == 0 && this.left < Silence && this.right < Silence))
                {
                    this.left = 0;
                    this.right = 0;
                    return this.stopped;
                }

                var toPitch = this.targetPitch;
                var glide = this.stopped ? Release : Glide;
                for (var frame = 0; frame < mix.Length / Channels; frame++)
                {
                    this.left += (toLeft - this.left) * glide;
                    this.right += (toRight - this.right) * glide;
                    this.pitch += (toPitch - this.pitch) * Glide;
                    var sample = LoopReader.At(this.loop, this.position);
                    this.position += this.pitch;
                    if (this.position >= this.loop.Length)
                    {
                        this.position -= this.loop.Length;
                    }
                    mix[frame * Channels] += sample * this.left;
                    mix[(frame * Channels) + 1] += sample * this.right;
                }

                return false;
            }
        }

        private void Add(Voice voice)
        {
            lock (this.voices)
            {
                if (this.voices.Count(playing => !playing.IsLetGo) >= MaxVoices)
                {
                    this.voices.First(playing => !playing.IsLetGo).LetGo();
                }

                this.voices.Add(voice);
            }
        }

        /// <summary>One playing cue, a channel per side (the same one for a mono cue): read with linear interpolation at the pitch ratio.</summary>
        private sealed class Voice
        {
            private readonly float[] samples;
            private readonly float[] rightSamples;
            private readonly double step;
            private readonly float left;
            private readonly float right;
            private double position;
            private int letGoFrames = -1;

            public Voice(float[] samples, float[] rightSamples, double step, float left, float right)
            {
                this.samples = samples;
                this.rightSamples = rightSamples;
                this.step = step;
                this.left = left;
                this.right = right;
            }

            public bool IsLetGo => this.letGoFrames >= 0;

            /// <summary>Fades it out now, wherever it is.</summary>
            public void LetGo()
            {
                if (!this.IsLetGo)
                {
                    this.letGoFrames = LetGoFrames;
                }
            }

            /// <summary>Adds the next buffer's worth; true once the cue has ended.</summary>
            public bool MixInto(float[] mix)
            {
                for (var frame = 0; frame < mix.Length / Channels; frame++)
                {
                    var index = (int)this.position;
                    if (index + 1 >= this.samples.Length || this.letGoFrames == 0)
                    {
                        return true;
                    }

                    var level = this.IsLetGo ? (float)this.letGoFrames-- / LetGoFrames : 1f;
                    var fraction = (float)(this.position - index);
                    mix[frame * Channels] += this.Read(this.samples, index, fraction) * this.left * level;
                    mix[(frame * Channels) + 1] += this.Read(this.rightSamples, index, fraction) * this.right * level;
                    this.position += this.step;
                }

                return false;
            }

            private float Read(float[] channel, int index, float fraction)
            {
                return channel[index] + ((channel[index + 1] - channel[index]) * fraction);
            }
        }
    }
}
