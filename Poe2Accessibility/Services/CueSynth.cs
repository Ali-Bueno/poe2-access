namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Synthesized enemy cues (mono, full scale ±1), after the D2R mod's design (its docs/entity-cues.md): every
    ///     enemy shares one low growl so pitch only means up or down, and its rarity adds a layer on top.
    /// </summary>
    public static class CueSynth
    {
        /// <summary>A 90 ms band-limited buzz falling 175 → 125 Hz, its loudness fluttering at 36 Hz.</summary>
        private const float GrowlSeconds = 0.09f;
        private const float GrowlStartHz = 175f;
        private const float GrowlEndHz = 125f;
        private const float GrowlFlutterHz = 36f;
        private const float GrowlLevel = 0.45f;

        /// <summary>Harmonics stop below this, so the buzz is rough without hiss.</summary>
        private const float BandLimitHz = 2000f;

        /// <summary>Magic and rare: a bright 750 Hz buzz of six harmonics chattering at 62 Hz.</summary>
        private const float EliteHz = 750f;
        private const int EliteHarmonics = 6;
        private const float EliteChatterHz = 62f;
        private const float EliteLevel = 0.28f;

        /// <summary>Spread phases (rad × h²) keep the harmonics' crest low.</summary>
        private const float ElitePhaseSpread = 1.3f;

        /// <summary>Unique: a struck metal bar — a free bar's partial ratios, the higher ones dying faster.</summary>
        private const float RingSeconds = 0.14f;
        private const float RingHz = 520f;
        private const float RingLevel = 0.3f;
        private static readonly float[] RingRatios = { 1f, 2.756f, 5.404f, 8.933f };
        private static readonly float[] RingAmplitudes = { 1f, 0.5f, 0.25f, 0.12f };
        private static readonly float[] RingDecaySeconds = { 0.09f, 0.05f, 0.03f, 0.02f };

        private const float AttackSeconds = 0.004f;

        /// <summary>NPCs and other players: a light wooden tap, 84 ms — a damped 700 Hz knock with a quicker overtone.</summary>
        private const float TapSeconds = 0.084f;
        private const float TapHz = 700f;
        private const float TapOvertoneRatio = 2.5f;
        private const float TapOvertoneLevel = 0.4f;
        private const float TapDecaySeconds = 0.012f;
        private const float TapOvertoneDecaySeconds = 0.006f;
        private const float TapReleaseSeconds = 0.01f;
        private const float TapLevel = 0.4f;

        /// <summary>Guide beacon (D2R beacon.md): a plain 620 Hz note of 80 ms, unlike every other cue.</summary>
        private const float PingSeconds = 0.08f;
        private const float PingHz = 620f;
        private const float PingOvertoneLevel = 0.2f;
        private const float PingReleaseSeconds = 0.025f;
        private const float PingLevel = 0.35f;

        /// <summary>What sets an enemy apart besides where it is.</summary>
        public enum Layer
        {
            None,
            Elite,
            Boss,

            /// <summary>Not an enemy: an NPC or another player, a tap instead of the growl.</summary>
            Friend,
        }

        /// <summary>The growl with the layer for this enemy mixed in; a tap for a friend.</summary>
        public static float[] Enemy(Layer layer, int sampleRate)
        {
            var growl = Growl(sampleRate);
            return layer switch
            {
                Layer.Elite => Sum(growl, EliteBuzz(sampleRate)),
                Layer.Boss => Sum(growl, Ring(sampleRate)),
                Layer.Friend => Tap(sampleRate),
                _ => growl,
            };
        }

        public static float[] Growl(int sampleRate)
        {
            var samples = new float[(int)(GrowlSeconds * sampleRate)];
            var phase = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var frequency = GrowlStartHz + ((GrowlEndHz - GrowlStartHz) * t / GrowlSeconds);
                phase += frequency / sampleRate;
                var harmonics = (int)(BandLimitHz / frequency);
                var value = 0f;
                for (var h = 1; h <= harmonics; h++)
                {
                    value += MathF.Sin((float)(2 * Math.PI * h * phase)) / h;
                }

                var flutter = 0.65f + (0.35f * MathF.Sin(2 * MathF.PI * GrowlFlutterHz * t));
                samples[i] = value * flutter * Envelope(t, GrowlSeconds);
            }

            return Normalized(samples, GrowlLevel);
        }

        public static float[] EliteBuzz(int sampleRate)
        {
            var samples = new float[(int)(GrowlSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var value = 0f;
                for (var h = 1; h <= EliteHarmonics; h++)
                {
                    value += MathF.Sin((2 * MathF.PI * EliteHz * h * t) + (ElitePhaseSpread * h * h)) / h;
                }

                var chatter = 0.5f + (0.5f * MathF.Sin(2 * MathF.PI * EliteChatterHz * t));
                samples[i] = value * chatter * Envelope(t, GrowlSeconds);
            }

            return Normalized(samples, EliteLevel);
        }

        public static float[] Ring(int sampleRate)
        {
            var samples = new float[(int)(RingSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var value = 0f;
                for (var p = 0; p < RingRatios.Length; p++)
                {
                    value += RingAmplitudes[p] * MathF.Exp(-t / RingDecaySeconds[p]) * MathF.Sin(2 * MathF.PI * RingHz * RingRatios[p] * t);
                }

                samples[i] = value * MathF.Min(1f, t / AttackSeconds);
            }

            return Normalized(samples, RingLevel);
        }

        public static float[] Tap(int sampleRate)
        {
            var samples = new float[(int)(TapSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var knock = (MathF.Sin(2 * MathF.PI * TapHz * t) * MathF.Exp(-t / TapDecaySeconds)) +
                            (TapOvertoneLevel * MathF.Sin(2 * MathF.PI * TapHz * TapOvertoneRatio * t) * MathF.Exp(-t / TapOvertoneDecaySeconds));
                samples[i] = knock * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (TapSeconds - t) / TapReleaseSeconds);
            }

            return Normalized(samples, TapLevel);
        }

        public static float[] Ping(int sampleRate)
        {
            var samples = new float[(int)(PingSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var tone = MathF.Sin(2 * MathF.PI * PingHz * t) + (PingOvertoneLevel * MathF.Sin(2 * MathF.PI * PingHz * 2 * t));
                samples[i] = tone * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (PingSeconds - t) / PingReleaseSeconds);
            }

            return Normalized(samples, PingLevel);
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
