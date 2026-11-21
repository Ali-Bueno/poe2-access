namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The countdown of a boss's long blast (Doryani's Triumph's self-destruct), from where the boss is: a clean timer beep
    ///     at 300 Hz with a soft octave, 140 ms, one steady pitch. Low but not deep, as the owner asked (2026-11-21), and apart from
    ///     the boss's growl (a buzz falling 175 → 125 Hz with a 520 Hz ring), the flood's hiss and the heartbeat.
    /// </summary>
    public static partial class CueSynth
    {
        public const string BlastSound = "boss_blast";

        private const float BlastHz = 300f;
        private const float BlastOctaveLevel = 0.35f;
        private const float BlastSeconds = 0.14f;
        private const float BlastLevel = 0.75f;

        public static float[] Blast(int sampleRate)
        {
            var samples = new float[(int)(BlastSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var envelope = MathF.Min(1f, t / 0.004f) * MathF.Min(1f, (BlastSeconds - t) / 0.03f) * MathF.Exp(-t / 0.12f);
                samples[i] = envelope * (MathF.Sin(2f * MathF.PI * BlastHz * t) + (BlastOctaveLevel * MathF.Sin(4f * MathF.PI * BlastHz * t)));
            }

            return Normalized(samples, BlastLevel);
        }
    }
}
