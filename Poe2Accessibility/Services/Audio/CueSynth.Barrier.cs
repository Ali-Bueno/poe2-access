namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The closed barrier's cue of <see cref="CueSynth"/>: a way the story keeps shut (the Matlan Waterways' water, a beam,
    ///     a gate), heard where it stands so the player stops walking into it (the owner, 2026-11-19). Candidates for the
    ///     owner's ear; the pick stays, the rest go.
    /// </summary>
    public static partial class CueSynth
    {
        public const string BarrierSound = "closed_barrier";
        private const float BarrierLevel = 0.6f;

        /// <summary>Audition candidates 0-3: a sealed field's hum, iron bars, a stone slab, a heavy sealed thud.</summary>
        public static float[] BarrierCandidate(int which, int sampleRate) => which switch
        {
            0 => Normalized(Room(FieldHum(sampleRate), sampleRate, 0.2f), BarrierLevel),
            1 => Normalized(Room(Damped(Sum(Modal(196f, new[] { 1f, 2.76f, 5.4f, 8.9f }, new[] { 1f, 0.6f, 0.35f, 0.15f }, 0.18f, 0.5f, sampleRate),
                Delayed(Modal(185f, new[] { 1f, 2.76f, 5.4f, 8.9f }, new[] { 0.8f, 0.5f, 0.3f, 0.12f }, 0.18f, 0.5f, sampleRate), 0.16f, sampleRate)), 3000f, sampleRate), sampleRate, 0.3f), BarrierLevel),
            2 => Normalized(Room(Damped(Rasp(260f, 16f, 0.45f, sampleRate), 1400f, sampleRate), sampleRate, 0.25f), BarrierLevel),
            _ => Normalized(Room(Damped(Sum(Modal(70f, new[] { 1f, 1.5f, 2.3f }, new[] { 1f, 0.5f, 0.25f }, 0.12f, 0.45f, sampleRate),
                Delayed(Modal(62f, new[] { 1f, 1.5f, 2.3f }, new[] { 0.9f, 0.45f, 0.2f }, 0.12f, 0.45f, sampleRate), 0.22f, sampleRate)), 900f, sampleRate), sampleRate, 0.35f), BarrierLevel),
        };

        /// <summary>A low hum with a slow beat between two near pitches, swelling and fading: a field that holds.</summary>
        private static float[] FieldHum(int sampleRate)
        {
            const float seconds = 0.7f;
            var samples = new float[(int)(seconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var tone = MathF.Sin(2 * MathF.PI * 110f * t) + MathF.Sin(2 * MathF.PI * 113.5f * t) +
                           (0.35f * MathF.Sin(2 * MathF.PI * 330f * t)) + (0.15f * MathF.Sin(2 * MathF.PI * 551f * t));
                samples[i] = tone * Swell(t, seconds, 0.12f);
            }

            return samples;
        }

        /// <summary>The sound started <paramref name="seconds"/> late, its length grown to hold it.</summary>
        private static float[] Delayed(float[] sound, float seconds, int sampleRate)
        {
            var shift = (int)(seconds * sampleRate);
            var samples = new float[sound.Length + shift];
            Array.Copy(sound, 0, samples, shift, sound.Length);
            return samples;
        }
    }
}
