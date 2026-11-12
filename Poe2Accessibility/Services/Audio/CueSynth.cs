namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Synthesized enemy cues (mono, full scale ±1), after the D2R mod's design (its docs/entity-cues.md): every
    ///     enemy shares one low growl so pitch only means up or down, and its rarity adds a layer on top. Timbres are
    ///     iron, stone and breath rather than plain beeps, so the cues sit in the game's dark sound world; the beacon and
    ///     the dodge warning ring on in a short stone-room tail.
    /// </summary>
    public static partial class CueSynth
    {
        private const float AttackSeconds = 0.004f;

        private const float TapReleaseSeconds = 0.01f;

        /// <summary>
        ///     The stone room: parallel damped comb filters then all-passes (Schroeder), a tail this long that fades out;
        ///     the damping in the loop swallows the highs as stone does.
        /// </summary>
        private const float RoomTailSeconds = 0.2f;
        private const float RoomFeedback = 0.5f;
        private const float RoomDamping = 0.4f;
        private const float RoomAllPassGain = 0.5f;
        private const float RoomFadeSeconds = 0.06f;
        private static readonly float[] RoomCombMs = { 29.7f, 37.1f, 41.1f, 43.7f };
        private static readonly float[] RoomAllPassMs = { 5f, 1.7f };

        /// <summary>The cue followed by its stone-room tail, <paramref name="wet"/> of the cue's level; the tail fades to silence.</summary>
        internal static float[] Room(float[] dry, int sampleRate, float wet)
        {
            var length = dry.Length + (int)(RoomTailSeconds * sampleRate);
            var reverb = new float[length];
            foreach (var ms in RoomCombMs)
            {
                var line = new float[(int)(ms * sampleRate / 1000f)];
                var low = 0f;
                for (var i = 0; i < length; i++)
                {
                    var slot = i % line.Length;
                    var delayed = line[slot];
                    low += RoomDamping * (delayed - low);
                    line[slot] = (i < dry.Length ? dry[i] : 0f) + (RoomFeedback * low);
                    reverb[i] += delayed / RoomCombMs.Length;
                }
            }

            foreach (var ms in RoomAllPassMs)
            {
                var line = new float[(int)(ms * sampleRate / 1000f)];
                for (var i = 0; i < length; i++)
                {
                    var slot = i % line.Length;
                    var delayed = line[slot];
                    var input = reverb[i];
                    line[slot] = input + (RoomAllPassGain * delayed);
                    reverb[i] = delayed - (RoomAllPassGain * line[slot]);
                }
            }

            var fade = (int)(RoomFadeSeconds * sampleRate);
            var output = new float[length];
            for (var i = 0; i < length; i++)
            {
                output[i] = ((i < dry.Length ? dry[i] : 0f) + (wet * reverb[i])) * MathF.Min(1f, (float)(length - i) / fade);
            }

            return output;
        }

        /// <summary>Fast attack, then a smooth fall to silence at the end (no click).</summary>
        private static float Envelope(float t, float duration) =>
            MathF.Min(1f, t / AttackSeconds) * MathF.Pow(MathF.Max(0f, 1f - (t / duration)), 1.5f);

        private static float[] Normalized(float[] samples, float peak)
        {
            var max = 0f;
            foreach (var sample in samples)
            {
                max = MathF.Max(max, MathF.Abs(sample));
            }

            if (max > 0)
            {
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] *= peak / max;
                }
            }

            return samples;
        }

        private static float[] Sum(float[] a, float[] b)
        {
            var sum = new float[Math.Max(a.Length, b.Length)];
            for (var i = 0; i < sum.Length; i++)
            {
                sum[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            }

            return sum;
        }
    }
}
