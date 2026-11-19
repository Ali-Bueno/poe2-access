namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The left-behind cue of <see cref="CueSynth"/>: unseen ground the player walks away from (docs/radar.md "Left behind").</summary>
    public static partial class CueSynth
    {
        private const float LeftBehindLevel = 0.5f;
        private const float LeftBehindAttackSeconds = 0.004f;
        private const float LeftBehindReleaseSeconds = 0.03f;

        /// <summary>Audition candidates, 0 to 3: a far bell, two falling glass notes, a hollow knock and its echoes, a deep gong.</summary>
        public static float[] LeftBehind(int sampleRate, int variant) => variant switch
        {
            0 => Normalized(Room(Damped(Modal(370f, new[] { 1f, 2f, 2.4f, 3f, 4.5f }, new[] { 1f, 0.5f, 0.35f, 0.2f, 0.1f }, 0.45f, 0.9f, sampleRate), 2200f, sampleRate), sampleRate, 0.55f), LeftBehindLevel),
            1 => Normalized(Room(Sum(Modal(587f, new[] { 1f, 2.76f, 5.4f }, new[] { 1f, 0.3f, 0.1f }, 0.18f, 0.35f, sampleRate), Delayed(Modal(494f, new[] { 1f, 2.76f, 5.4f }, new[] { 1f, 0.3f, 0.1f }, 0.22f, 0.45f, sampleRate), 0.16f, sampleRate)), sampleRate, 0.4f), LeftBehindLevel),
            2 => Normalized(Echoes(Damped(Modal(330f, new[] { 1f, 2.32f, 4.25f }, new[] { 1f, 0.45f, 0.15f }, 0.06f, 0.18f, sampleRate), 3000f, sampleRate), 0.19f, 0.45f, 2, sampleRate), LeftBehindLevel),
            _ => Normalized(Room(Damped(Modal(196f, new[] { 1f, 1.5f, 2.1f, 2.9f }, new[] { 1f, 0.4f, 0.3f, 0.12f }, 0.5f, 1.0f, sampleRate), 1400f, sampleRate), sampleRate, 0.5f), LeftBehindLevel),
        };

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

        private static float[] Delayed(float[] samples, float seconds, int sampleRate)
        {
            var offset = (int)(seconds * sampleRate);
            var delayed = new float[samples.Length + offset];
            Array.Copy(samples, 0, delayed, offset, samples.Length);
            return delayed;
        }

        /// <summary>The sound and <paramref name="count"/> echoes, each <paramref name="gain"/> of the last and duller.</summary>
        private static float[] Echoes(float[] dry, float gapSeconds, float gain, int count, int sampleRate)
        {
            var output = dry;
            var echo = (float[])dry.Clone();
            for (var n = 1; n <= count; n++)
            {
                echo = Damped((float[])echo.Clone(), 1500f / n, sampleRate);
                var scaled = new float[echo.Length];
                for (var i = 0; i < echo.Length; i++)
                {
                    scaled[i] = echo[i] * MathF.Pow(gain, n);
                }

                output = Sum(output, Delayed(scaled, gapSeconds * n, sampleRate));
            }

            return output;
        }
    }
}
