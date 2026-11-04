namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Reads a looped sound between its samples with a windowed sinc. A bed changes pitch with the side it is on, so it is
    ///     read off its samples all the time: linear reading left the bright Diablo IV recordings 24 dB under their own grit
    ///     at the top pitch, this leaves them 54 dB under it (docs/research/game-audio.md).
    /// </summary>
    public static class LoopReader
    {
        private const int Taps = 16;
        private const int Half = Taps / 2;
        private const int Phases = 512;

        /// <summary>A kernel per position between two samples, the last at the next sample.</summary>
        private static readonly float[] Kernels = BuildKernels();

        /// <summary>The sound at <paramref name="position"/>, a sample index with its fraction; past either end it reads on round the loop.</summary>
        public static float At(float[] loop, double position)
        {
            var index = (int)Math.Floor(position);
            var kernel = (int)(((position - index) * Phases) + 0.5) * Taps;
            var first = index - Half + 1;
            var sum = 0f;
            if (first >= 0 && first + Taps <= loop.Length)
            {
                for (var tap = 0; tap < Taps; tap++)
                {
                    sum += loop[first + tap] * Kernels[kernel + tap];
                }
            }
            else
            {
                for (var tap = 0; tap < Taps; tap++)
                {
                    sum += loop[(((first + tap) % loop.Length) + loop.Length) % loop.Length] * Kernels[kernel + tap];
                }
            }

            return sum;
        }

        private static float[] BuildKernels()
        {
            var kernels = new float[(Phases + 1) * Taps];
            var taps = new double[Taps];
            for (var phase = 0; phase <= Phases; phase++)
            {
                var total = 0.0;
                for (var tap = 0; tap < Taps; tap++)
                {
                    var distance = tap - Half + 1 - ((double)phase / Phases);
                    var sinc = distance == 0 ? 1.0 : Math.Sin(Math.PI * distance) / (Math.PI * distance);
                    var angle = Math.PI * distance / Half;
                    taps[tap] = sinc * (0.42 + (0.5 * Math.Cos(angle)) + (0.08 * Math.Cos(2 * angle)));
                    total += taps[tap];
                }

                for (var tap = 0; tap < Taps; tap++)
                {
                    kernels[(phase * Taps) + tap] = (float)(taps[tap] / total);
                }
            }

            return kernels;
        }
    }
}
