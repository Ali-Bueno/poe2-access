namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The left-behind cue of <see cref="CueSynth"/>: unseen ground the player walks away from (docs/radar.md "Left behind").</summary>
    public static partial class CueSynth
    {
        private const float LeftBehindLevel = 0.5f;
        private const float LeftBehindAttackSeconds = 0.004f;
        private const float LeftBehindReleaseSeconds = 0.03f;

        /// <summary>A far bell: low partials, dulled as if heard from off down a passage, in a room (the owner's pick of four).</summary>
        public static float[] LeftBehind(int sampleRate) =>
            Normalized(Room(Damped(Modal(370f, new[] { 1f, 2f, 2.4f, 3f, 4.5f }, new[] { 1f, 0.5f, 0.35f, 0.2f, 0.1f }, 0.45f, 0.9f, sampleRate), 2200f, sampleRate), sampleRate, 0.55f), LeftBehindLevel);

        /// <summary>A struck body: sine partials at <paramref name="ratios"/> of <paramref name="hz"/>, each dying faster than the one below it.</summary>
        private static float[] Modal(float hz, float[] ratios, float[] amplitudes, float decaySeconds, float seconds, int sampleRate)
        {
            var samples = new float[(int)(seconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var value = 0f;
                for (var p = 0; p < ratios.Length; p++)
                {
                    value += amplitudes[p] * MathF.Sin(2 * MathF.PI * hz * ratios[p] * t) * MathF.Exp(-t * (1 + p) / decaySeconds);
                }

                samples[i] = value * MathF.Min(1f, t / LeftBehindAttackSeconds) * MathF.Min(1f, (seconds - t) / LeftBehindReleaseSeconds);
            }

            return samples;
        }

        /// <summary>A one-pole low-pass: the sound heard from farther off.</summary>
        private static float[] Damped(float[] samples, float cutoffHz, int sampleRate)
        {
            var alpha = 1f - MathF.Exp(-2 * MathF.PI * cutoffHz / sampleRate);
            var low = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                low += alpha * (samples[i] - low);
                samples[i] = low;
            }

            return samples;
        }
    }
}
