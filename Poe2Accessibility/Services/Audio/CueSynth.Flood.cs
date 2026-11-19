namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The pulse of a boss arena's flood (Mektul's molten gold), from its nearest edge and faster as it nears: a hiss
    ///     of steam, apart from the ground alarm's grinding stabs, the shelter's bright chord and the heartbeat. The owner's
    ///     pick on the audition page (2026-11-19) over a lava bubble, a crackle and a thump of magma.
    /// </summary>
    public static partial class CueSynth
    {
        private const float FloodLevel = 0.8f;
        private const int FloodSeed = 4141;

        /// <summary>The flood's pulse as played.</summary>
        public static float[] Flood(int sampleRate) => FloodHiss(sampleRate);

        /// <summary>Steam off the gold, a hiss swelling in and dying over a low hum at 70 Hz, 0.3 s.</summary>
        public static float[] FloodHiss(int sampleRate)
        {
            var samples = new float[(int)(0.3f * sampleRate)];
            var random = new Random(FloodSeed);
            var (low, previous) = (0f, 0f);
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var noise = (float)((random.NextDouble() * 2) - 1);
                low += 0.35f * (noise - low);
                var band = low - previous;
                previous = low;
                var envelope = MathF.Min(1f, t / 0.03f) * MathF.Exp(-t / 0.09f);
                samples[i] = envelope * ((1.6f * band) + (0.4f * MathF.Sin(2f * MathF.PI * 70f * t)));
            }

            return Normalized(samples, FloodLevel);
        }
    }
}
