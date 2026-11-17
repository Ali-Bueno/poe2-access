namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The pulse that leads to a boss fight's shelter (Asala's barrier): a struck chord, bright and loud enough to cut through
    ///     a fight, steady in pitch, short enough to repeat faster as the player nears it. The user's pick on the audition page
    ///     (2026-11-17) over a temple bell, a double glass chime, a struck bowl and a bell over a thump, and before them the
    ///     game's own loops, which a fight's noise hid.
    /// </summary>
    public static partial class CueSynth
    {
        private const float RefugeLevel = 0.85f;

        /// <summary>A holy chord, C major (C, E, G and the octave) struck bright and short: 0.3 s.</summary>
        public static float[] Refuge(int sampleRate)
        {
            var samples = new float[(int)(0.3f * sampleRate)];
            foreach (var hz in new[] { 523.3f, 659.3f, 784f, 1046.5f })
            {
                for (var h = 1; h <= 3; h++)
                {
                    AddStrike(samples, 0, hz * h, 1f / (h * h), 0.003f, 0.12f / h, 2f, sampleRate);
                }
            }

            return Normalized(samples, RefugeLevel);
        }

        /// <summary>One struck partial at <paramref name="start"/>, with a twin <paramref name="beatHz"/> above it when that is not 0.</summary>
        private static void AddStrike(float[] samples, int start, float hz, float level, float attack, float decay, float beatHz, int sampleRate)
        {
            for (var i = start; i < samples.Length; i++)
            {
                var t = (float)(i - start) / sampleRate;
                var envelope = MathF.Min(1f, t / attack) * MathF.Exp(-t / decay) * (1f - ((float)i / samples.Length));
                var tone = MathF.Sin(2f * MathF.PI * hz * t);
                if (beatHz != 0f)
                {
                    tone = 0.5f * (tone + MathF.Sin(2f * MathF.PI * (hz + beatHz) * t));
                }

                samples[i] += level * envelope * tone;
            }
        }
    }
}
