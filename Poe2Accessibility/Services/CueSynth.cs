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

        /// <summary>Dodge warning: a fast rising noise sweep (a whoosh), unlike every tonal cue, short enough to react to.</summary>
        private const float WhooshSeconds = 0.22f;
        private const float WhooshStartHz = 300f;
        private const float WhooshEndHz = 1400f;
        private const float WhooshLevel = 0.5f;

        /// <summary>Low-life heartbeat (D2R vitals.md): a lub-dub of two low thumps, one beat every 0.58 s (~103/min).</summary>
        public const int HeartbeatPeriodMs = 580;
        private const float HeartbeatLubHz = 62f;
        private const float HeartbeatDubHz = 52f;
        private const float HeartbeatGapSeconds = 0.16f;
        private const float HeartbeatThumpSeconds = 0.11f;
        private const float HeartbeatLevel = 0.6f;

        /// <summary>Crosshair (D2R crosshair.md): two flat 24 ms blips at 1175 Hz, 40 ms apart, odd harmonics.</summary>
        private const float ReachLowHz = 1320f;
        private const float ReachHighHz = 1760f;
        private const float ReachNoteSeconds = 0.048f;
        private const float ReachLevel = 0.3f;
        private const float BlipHz = 1175f;
        private const float BlipSeconds = 0.024f;
        private const float BlipGapSeconds = 0.04f;
        private const float BlipLevel = 0.22f;

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

        public static float[] Whoosh(int sampleRate)
        {
            // Band-passed noise whose centre rises: a resonant filter swept over white noise, louder toward the end.
            var samples = new float[(int)(WhooshSeconds * sampleRate)];
            var random = new Random(7);
            float low = 0f, band = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var progress = t / WhooshSeconds;
                var centre = WhooshStartHz * MathF.Pow(WhooshEndHz / WhooshStartHz, progress);
                var f = 2f * MathF.Sin(MathF.PI * centre / sampleRate);
                var noise = (float)((random.NextDouble() * 2) - 1);
                low += f * band;
                var high = noise - low - (0.25f * band);
                band += f * high;
                samples[i] = band * MathF.Min(1f, t / 0.02f) * MathF.Min(1f, (WhooshSeconds - t) / 0.03f) * (0.4f + (0.6f * progress));
            }

            return Normalized(samples, WhooshLevel);
        }

        public static float[] Heartbeat(int sampleRate)
        {
            var samples = new float[(int)((HeartbeatGapSeconds + HeartbeatThumpSeconds) * sampleRate)];
            void Thump(float start, float hz, float level)
            {
                var first = (int)(start * sampleRate);
                for (var i = 0; i < (int)(HeartbeatThumpSeconds * sampleRate) && first + i < samples.Length; i++)
                {
                    var t = (float)i / sampleRate;
                    // A falling sine with a soft attack: a felt thump, below every other cue.
                    samples[first + i] += level * MathF.Sin(2 * MathF.PI * hz * t * (1f - (0.3f * t / HeartbeatThumpSeconds))) *
                                          MathF.Min(1f, t / 0.008f) * MathF.Exp(-t / 0.035f);
                }
            }

            Thump(0f, HeartbeatLubHz, 1f);
            Thump(HeartbeatGapSeconds, HeartbeatDubHz, 0.75f);
            return Normalized(samples, HeartbeatLevel);
        }

        /// <summary>D2R reach ping: 96 ms, two rising sine notes (1320 then 1760 Hz), far above every other cue.</summary>
        public static float[] ReachPing(int sampleRate)
        {
            var note = (int)(ReachNoteSeconds * sampleRate);
            var samples = new float[2 * note];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)(i % note) / sampleRate;
                var hz = i < note ? ReachLowHz : ReachHighHz;
                var edge = MathF.Min(1f, MathF.Min(t, ReachNoteSeconds - t) / 0.004f);
                samples[i] = MathF.Sin(2 * MathF.PI * hz * t) * edge;
            }

            return Normalized(samples, ReachLevel);
        }

        public static float[] Crosshair(int sampleRate)
        {
            var blip = (int)(BlipSeconds * sampleRate);
            var gap = (int)(BlipGapSeconds * sampleRate);
            var samples = new float[(2 * blip) + gap];
            for (var i = 0; i < blip; i++)
            {
                var t = (float)i / sampleRate;
                var tone = MathF.Sin(2 * MathF.PI * BlipHz * t) + (MathF.Sin(2 * MathF.PI * BlipHz * 3 * t) / 3f) + (MathF.Sin(2 * MathF.PI * BlipHz * 5 * t) / 5f);
                var edge = MathF.Min(1f, MathF.Min(t, BlipSeconds - t) / 0.003f);
                samples[i] = tone * edge;
                samples[blip + gap + i] = tone * edge;
            }

            return Normalized(samples, BlipLevel);
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
