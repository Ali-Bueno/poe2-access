namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     A look-ahead peak limiter, stereo-linked: the gain ramps down before a peak and recovers slowly after it, so a
    ///     loud sound stays under <see cref="Ceiling"/> with its waveform intact (a per-sample curve saturates it).
    /// </summary>
    public sealed class PeakLimiter
    {
        /// <summary>The loudest sample let through, just under full scale.</summary>
        public const float Ceiling = 0.95f;

        /// <summary>The gain reaches a peak's level over this many frames before it (1.5 ms at 44.1 kHz); also the delay.</summary>
        private const int Lookahead = 64;

        /// <summary>The gain's recovery after a peak; slower than a low tone's cycle, so it does not ripple it.</summary>
        private const float ReleaseSeconds = 0.15f;

        private readonly float release;
        private readonly float[] delayLeft = new float[Lookahead - 1];
        private readonly float[] delayRight = new float[Lookahead - 1];
        private readonly float[] window = new float[Lookahead];
        private readonly long[] minFrames = new long[Lookahead];
        private readonly float[] minNeeds = new float[Lookahead];
        private int minHead;
        private int minCount;
        private long frame;
        private int delayAt;
        private int windowAt;
        private double windowSum = Lookahead;
        private float gain = 1f;

        public PeakLimiter(int sampleRate)
        {
            this.release = 1f - MathF.Exp(-1f / (ReleaseSeconds * sampleRate));
            Array.Fill(this.window, 1f);
        }

        /// <summary>A copy of a cue made <paramref name="drive"/> times louder under the ceiling: its quiet body grows, its peaks are held.</summary>
        public static (float[] Left, float[] Right) Drive(float[] left, float[] right, float drive, int sampleRate)
        {
            var limiter = new PeakLimiter(sampleRate);
            var length = Math.Min(left.Length, right.Length);
            var outLeft = new float[length];
            var outRight = ReferenceEquals(left, right) ? outLeft : new float[length];
            for (var i = 0; i < length + Lookahead - 1; i++)
            {
                var l = i < length ? left[i] * drive : 0f;
                var r = i < length ? right[i] * drive : 0f;
                limiter.Process(ref l, ref r);
                if (i >= Lookahead - 1)
                {
                    outLeft[i - Lookahead + 1] = l;
                    outRight[i - Lookahead + 1] = r;
                }
            }

            return (outLeft, outRight);
        }

        /// <summary>Takes one frame in and gives back the frame <see cref="Lookahead"/> − 1 frames older, limited.</summary>
        public void Process(ref float left, ref float right)
        {
            var peak = MathF.Max(MathF.Abs(left), MathF.Abs(right));
            var least = this.WindowMinimum(peak > Ceiling ? Ceiling / peak : 1f);
            this.gain = MathF.Min(least, this.gain + ((1f - this.gain) * this.release));

            // The average of gains that each cover the delayed frame's need never exceeds it.
            this.windowSum += this.gain - this.window[this.windowAt];
            this.window[this.windowAt] = this.gain;
            this.windowAt = (this.windowAt + 1) % Lookahead;
            var smooth = (float)(this.windowSum / Lookahead);

            (left, this.delayLeft[this.delayAt]) = (this.delayLeft[this.delayAt] * smooth, left);
            (right, this.delayRight[this.delayAt]) = (this.delayRight[this.delayAt] * smooth, right);
            this.delayAt = (this.delayAt + 1) % this.delayLeft.Length;
        }

        /// <summary>The least need among the last <see cref="Lookahead"/> frames (a monotonic queue: constant time a frame).</summary>
        private float WindowMinimum(float need)
        {
            if (this.minCount > 0 && this.minFrames[this.minHead] <= this.frame - Lookahead)
            {
                this.minHead = (this.minHead + 1) % Lookahead;
                this.minCount--;
            }

            while (this.minCount > 0 && this.minNeeds[(this.minHead + this.minCount - 1) % Lookahead] >= need)
            {
                this.minCount--;
            }

            var tail = (this.minHead + this.minCount) % Lookahead;
            this.minFrames[tail] = this.frame;
            this.minNeeds[tail] = need;
            this.minCount++;
            this.frame++;
            return this.minNeeds[this.minHead];
        }
    }
}
