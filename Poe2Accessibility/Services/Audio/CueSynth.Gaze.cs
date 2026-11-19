namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The petrifying statue's gaze on the player (docs/research/trial-of-chaos.md, Petrification Statues): one pulse at a time
    ///     while the statue looks, from the statue's side, rising with the petrification built up. The owner's pick of seven
    ///     (2026-11-19, audition page https://claude.ai/artifact/4NAjeJWcn3rrGYWtRrQLtj): a file drawn over stone, "la lima"; a plain
    ///     grind was too like the radar.
    /// </summary>
    public static partial class CueSynth
    {
        public const string GazeSound = "statue_gaze";

        /// <summary>Raised from 0.55 at the owner's word (2026-11-19: "un poquitín más alto").</summary>
        private const float GazeLevel = 0.75f;
        private const float GazeSemitone = 1.0594631f;
        private const float GazeBandHz = 700f;
        private const float GazeStrokeHz = 55f;
        private const float GazeSeconds = 0.25f;
        private const int GazeMostStacks = 12;

        /// <summary>One gaze pulse at <paramref name="stacks"/> of stone built up (0 = the gaze just met): a whole tone higher each.</summary>
        public static float[] Gaze(int stacks, int sampleRate)
        {
            var step = Math.Clamp(stacks, 0, GazeMostStacks);
            var lift = MathF.Pow(GazeSemitone, step * 2);
            return Normalized(Room(Rasp(GazeBandHz * lift, GazeStrokeHz * MathF.Pow(GazeSemitone, step), GazeSeconds, sampleRate), sampleRate, 0.15f), GazeLevel);
        }

        /// <summary>A rasp: noise opened by a falling sawtooth at <paramref name="strokeHz"/>, through a wide band at <paramref name="hz"/>.</summary>
        private static float[] Rasp(float hz, float strokeHz, float seconds, int sampleRate)
        {
            var noise = NoiseBand(hz, seconds, sampleRate, 5);
            var wide = NoiseBand(hz * 2.2f, seconds, sampleRate, 9);
            var samples = new float[noise.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var saw = 1f - ((t * strokeHz) % 1f); // each stroke bites, then eases
                samples[i] = ((noise[i] + (0.6f * wide[i])) * saw * saw) + (0.15f * MathF.Sin(2 * MathF.PI * strokeHz * 2 * t) * Swell(t, seconds, 0.01f));
            }

            return samples;
        }

        /// <summary>White noise through a resonant band-pass at <paramref name="hz"/>, swelling and falling.</summary>
        private static float[] NoiseBand(float hz, float seconds, int sampleRate, int seed)
        {
            var samples = new float[(int)(seconds * sampleRate)];
            var random = new Random(seed);
            var f = 2f * MathF.Sin(MathF.PI * hz / sampleRate);
            const float q = 0.12f;
            float low = 0f, band = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var noise = ((float)random.NextDouble() * 2f) - 1f;
                low += f * band;
                var high = noise - low - (q * band);
                band += f * high;
                samples[i] = band * Swell(t, seconds, 0.015f);
            }

            return samples;
        }

        /// <summary>Rises over <paramref name="attack"/>, then falls smoothly to silence at the end.</summary>
        private static float Swell(float t, float seconds, float attack) =>
            MathF.Min(1f, t / attack) * MathF.Pow(MathF.Max(0f, 1f - (t / seconds)), 1.2f);
    }
}
