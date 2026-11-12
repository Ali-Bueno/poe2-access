namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>Skill and buff cues of <see cref="CueSynth"/>: culling gong, deployed bell, charges, weak-point heartbeat, shield and perfect timing.</summary>
    public static partial class CueSynth
    {
        /// <summary>
        ///     Culling Strike in range: a gong, the player's pick of four rounds of samples (H5, 2026-10-29). A small temple
        ///     gong struck at 400 Hz whose pitch sags 3.5 % in its first tenth of a second, eleven inharmonic modes each a
        ///     pair beating slowly, a hum an octave under it and a 90 Hz mallet thump; its level trembles at 7 Hz as a big
        ///     gong's does, under a cymbal sizzle above 4 kHz; 0.75 s. Unlike the heavy-stun gong (a short low bell) and the
        ///     dodge warning's blade over a low gong.
        /// </summary>
        private const float CullSeconds = 0.75f;
        private const float CullHz = 400f;
        private const float CullSag = 0.965f;
        private const float CullSagSeconds = 0.12f;
        private const float CullBeatHz = 1.6f;
        private const float CullModeAttackSeconds = 0.002f;
        private const float CullBloomSeconds = 0.08f;
        private const float CullStrikeLevel = 0.45f;
        private const float CullStrikeSeconds = 0.025f;
        private const float CullStrikeDecay = 0.008f;
        private const float CullStrikeSmoothing = 0.35f;
        private const int CullStrikeSeed = 11;
        private const float CullFadeSeconds = 0.04f;
        private const float CullThumpLevel = 0.65f;
        private const float CullThumpHz = 90f;
        private const float CullThumpSeconds = 0.25f;
        private const float CullThumpDecay = 0.07f;
        private const float CullTremoloHz = 7f;
        private const float CullTremoloDepth = 0.55f;
        private const float CullTremoloOnsetSeconds = 0.06f;
        private const float CullSizzleLevel = 0.5f;
        private const float CullSizzleSeconds = 0.6f;
        private const float CullSizzleDecay = 0.35f;
        private const float CullSizzleAttackSeconds = 0.004f;
        private const int CullSizzleSeed = 3;

        /// <summary>
        ///     Something a skill placed and left standing (DeployedWatch: Tempest Bell, an ice crystal): a small bell, one
        ///     short high strike, apart from the culling gong, the glass charge ticks and the NPC ring.
        /// </summary>
        private const float DeployedHz = 2093f;
        private const float DeployedSeconds = 0.2f;
        private const float DeployedDecay = 0.06f;
        private const float DeployedLevel = 0.26f;

        /// <summary>The gong's own mix level, before the thump and the sizzle are laid over it at their levels.</summary>
        private const float CullGongLevel = 0.32f;
        private const float CullMixLevel = 0.42f;

        /// <summary>The sample's level raised 7 dB (0.42 → 0.94): it sounded too quiet in play, at +5 dB still a little (user, 2026-10-29).</summary>
        private const float CullLevel = 0.94f;

        /// <summary>The gong's modes: ratio to the strike tone, level, decay seconds; those above the second bloom after the strike.</summary>
        private static readonly (float Ratio, float Level, float Decay)[] CullModes =
        {
            (0.5f, 0.7f, 0.42f), (1f, 1f, 0.32f), (1.42f, 0.65f, 0.28f), (1.97f, 0.45f, 0.24f), (2.47f, 0.4f, 0.2f), (2.76f, 0.3f, 0.17f),
            (3.05f, 0.28f, 0.15f), (3.42f, 0.22f, 0.13f), (3.9f, 0.2f, 0.11f), (4.6f, 0.15f, 0.09f), (5.2f, 0.12f, 0.08f), (6.1f, 0.08f, 0.06f),
        };

        /// <summary>
        ///     Charges (ChargeWatch): one glass tick per charge now held, so the count is heard without words, all on one
        ///     pitch; a charge spent or expired is one muted tick a fifth lower. Power ticks highest, Frenzy in the
        ///     middle, Endurance lowest; Spirit charges between Power and Frenzy; infusions, surges and the rest share one
        ///     pitch, Rage the lowest.
        /// </summary>
        private const float ChargeTickSeconds = 0.06f;
        private const float ChargeTickSpacingSeconds = 0.09f;
        private const float ChargeTickDecay = 0.025f;
        private const float ChargeLevel = 0.26f;
        private const float ChargeLostLevel = 0.16f;
        private const float ChargeLostRatio = 2f / 3f;
        private const int MaxChargeTicks = 10;
        private const float PowerChargeHz = 1568f;
        private const float FrenzyChargeHz = 1175f;
        private const float EnduranceChargeHz = 784f;
        private const float SpiritChargeHz = 1319f;
        private const float OtherChargeHz = 1047f;
        private const float RageHz = 523f;

        /// <summary>
        ///     Boss weak point: a heartbeat, two soft thumps 150 ms apart whose pitch falls 160 → 90 Hz (the second a little
        ///     higher and quieter), with two overtones so small speakers carry it; no other cue is a double low thump.
        /// </summary>
        private const float BeatSeconds = 0.11f;
        private const float BeatGapSeconds = 0.15f;
        private const float BeatStartHz = 160f;
        private const float BeatEndHz = 90f;
        private const float BeatDecaySeconds = 0.035f;
        private const float BeatSecondRatio = 1.15f;
        private const float BeatSecondLevel = 0.7f;
        private const float BeatLevel = 0.6f;
        private static readonly float[] BeatHarmonics = { 1f, 0.5f, 0.3f };

        /// <summary>
        ///     Raise Shield: a short clank of a struck plate (inharmonic partials) whose pitch glides a fifth, up as the
        ///     shield rises (260 → 390 Hz), down and quieter as it falls.
        /// </summary>
        private const float ShieldSeconds = 0.13f;
        private const float ShieldGlideSeconds = 0.07f;
        private const float ShieldLowHz = 260f;
        private const float ShieldHighHz = 390f;
        private const float ShieldUpLevel = 0.45f;
        private const float ShieldDownLevel = 0.35f;
        private static readonly float[] ShieldRatios = { 1f, 2.4f, 3.9f };
        private static readonly float[] ShieldAmplitudes = { 1f, 0.45f, 0.2f };
        private static readonly float[] ShieldDecaySeconds = { 0.06f, 0.035f, 0.02f };

        public static float[] Heartbeat(int sampleRate)
        {
            var beat = (int)(BeatSeconds * sampleRate);
            var gap = (int)(BeatGapSeconds * sampleRate);
            var samples = new float[gap + beat];
            AddBeat(samples, 0, beat, 1f, 1f, sampleRate);
            AddBeat(samples, gap, beat, BeatSecondLevel, BeatSecondRatio, sampleRate);
            return Normalized(samples, BeatLevel);
        }

        public static float[] Cull(int sampleRate)
        {
            var n = (int)(CullSeconds * sampleRate);
            var samples = new float[n];
            foreach (var (ratio, level, decay) in CullModes)
            {
                double phase = 0, twin = 0;
                for (var i = 0; i < n; i++)
                {
                    var t = (float)i / sampleRate;
                    var hz = CullHz * ratio * (1f + ((CullSag - 1f) * (1f - MathF.Exp(-t / CullSagSeconds))));
                    phase += hz / sampleRate;
                    twin += (hz + (CullBeatHz * ratio)) / sampleRate;
                    var envelope = MathF.Exp(-t / decay) * MathF.Min(1f, t / CullModeAttackSeconds);
                    if (ratio > 2f)
                    {
                        envelope *= MathF.Min(1f, 0.3f + (t / CullBloomSeconds));
                    }

                    samples[i] += level * envelope * 0.5f * (float)(Math.Sin(2 * Math.PI * phase) + Math.Sin(2 * Math.PI * twin));
                }
            }

            // The mallet: a burst of smoothed noise.
            var random = new Random(CullStrikeSeed);
            var low = 0f;
            for (var i = 0; i < Math.Min(n, (int)(CullStrikeSeconds * sampleRate)); i++)
            {
                low += CullStrikeSmoothing * (((float)random.NextDouble() * 2f) - 1f - low);
                samples[i] += CullStrikeLevel * 3f * low * MathF.Exp(-(float)i / sampleRate / CullStrikeDecay);
            }

            var fade = (int)(CullFadeSeconds * sampleRate);
            for (var i = 0; i < fade; i++)
            {
                samples[n - 1 - i] *= (float)i / fade;
            }

            Normalized(samples, CullGongLevel);
            for (var i = 0; i < Math.Min(n, (int)(CullThumpSeconds * sampleRate)); i++)
            {
                var t = (float)i / sampleRate;
                samples[i] += CullThumpLevel * CullGongLevel * MathF.Sin(2 * MathF.PI * CullThumpHz * t) * MathF.Exp(-t / CullThumpDecay) *
                              MathF.Min(1f, t / CullModeAttackSeconds);
            }

            Normalized(samples, CullMixLevel);
            for (var i = 0; i < n; i++)
            {
                var t = (float)i / sampleRate;
                var onset = MathF.Min(1f, t / CullTremoloOnsetSeconds);
                samples[i] *= 1f - (CullTremoloDepth * onset * 0.5f * (1f - MathF.Cos(2 * MathF.PI * CullTremoloHz * t)));
            }

            Normalized(samples, CullMixLevel);

            // The sizzle: white noise less its smoothed part, a touch of the previous high sample taken off again.
            random = new Random(CullSizzleSeed);
            float smooth = 0f, previous = 0f;
            for (var i = 0; i < Math.Min(n, (int)(CullSizzleSeconds * sampleRate)); i++)
            {
                var t = (float)i / sampleRate;
                var white = ((float)random.NextDouble() * 2f) - 1f;
                smooth += 0.55f * (white - smooth);
                var high = white - smooth;
                samples[i] += CullSizzleLevel * CullGongLevel * (high - (0.3f * previous)) * MathF.Exp(-t / CullSizzleDecay) *
                              MathF.Min(1f, t / CullSizzleAttackSeconds);
                previous = high;
            }

            return Normalized(samples, CullLevel);
        }

        public static float[] Deployed(int sampleRate) =>
            Struck(new[] { new StateNote(0f, DeployedSeconds, DeployedHz, DeployedHz, Bell, DeployedDecay) }, DeployedLevel, sampleRate);

        /// <summary>A charge gained: one tick for each of the <paramref name="held"/> charges now held.</summary>
        public static float[] ChargeGained(ChargeKind kind, int held, int sampleRate)
        {
            var hz = ChargeHz(kind);
            var ticks = Enumerable.Range(0, Math.Clamp(held, 1, MaxChargeTicks))
                .Select(tick => new StateNote(tick * ChargeTickSpacingSeconds, ChargeTickSeconds, hz, hz, Glass, ChargeTickDecay))
                .ToArray();
            return Struck(ticks, ChargeLevel, sampleRate);
        }

        public static float[] ChargeLost(ChargeKind kind, int sampleRate)
        {
            var hz = ChargeHz(kind) * ChargeLostRatio;
            return Struck(new[] { new StateNote(0f, ChargeTickSeconds, hz, hz, Pure, ChargeTickDecay) }, ChargeLostLevel, sampleRate);
        }

        /// <summary>
        ///     Perfect Timing: one hammer blow on an anvil as the window opens. A burst of noise for the strike, then a
        ///     free bar's modes (1 : 2.76 : 5.40 : 8.93) ringing briefly on a steady pitch: the shield's clank glides, the
        ///     radar's exit and its pips ring higher (900 and 1320 Hz).
        /// </summary>
        public const float PerfectTimingHz = 587f;
        private const float PerfectTimingSeconds = 0.16f;
        private const float PerfectTimingLevel = 0.5f;
        private const float PerfectTimingStrikeSeconds = 0.004f;
        private const float PerfectTimingStrikeLevel = 0.5f;
        private const int PerfectTimingSeed = 23;
        private static readonly float[] PerfectTimingRatios = { 1f, 2.76f, 5.4f, 8.93f };
        private static readonly float[] PerfectTimingAmplitudes = { 1f, 0.6f, 0.35f, 0.2f };
        private static readonly float[] PerfectTimingDecaySeconds = { 0.07f, 0.045f, 0.03f, 0.02f };

        /// <param name="hz">The bar's lowest mode; the candidates the user picks from differ by it.</param>
        public static float[] PerfectTiming(int sampleRate, float hz = PerfectTimingHz)
        {
            var samples = new float[(int)(PerfectTimingSeconds * sampleRate)];
            var random = new Random(PerfectTimingSeed);
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var value = t < PerfectTimingStrikeSeconds ? PerfectTimingStrikeLevel * (((float)random.NextDouble() * 2f) - 1f) : 0f;
                for (var p = 0; p < PerfectTimingRatios.Length; p++)
                {
                    value += PerfectTimingAmplitudes[p] * MathF.Exp(-t / PerfectTimingDecaySeconds[p]) *
                             MathF.Sin(2f * MathF.PI * hz * PerfectTimingRatios[p] * t);
                }

                samples[i] = value * MathF.Min(1f, (PerfectTimingSeconds - t) / TapReleaseSeconds);
            }

            return Normalized(samples, PerfectTimingLevel);
        }

        public static float[] ShieldUp(int sampleRate) => Shield(sampleRate, ShieldLowHz, ShieldHighHz, ShieldUpLevel);

        public static float[] ShieldDown(int sampleRate) => Shield(sampleRate, ShieldHighHz, ShieldLowHz, ShieldDownLevel);

        /// <summary>One thump of <see cref="Heartbeat"/> mixed in at <paramref name="start"/>, its pitch scaled by <paramref name="ratio"/>.</summary>
        private static void AddBeat(float[] samples, int start, int length, float level, float ratio, int sampleRate)
        {
            var phase = 0.0;
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / sampleRate;
                phase += ratio * (BeatStartHz + ((BeatEndHz - BeatStartHz) * t / BeatSeconds)) / sampleRate;
                var tone = 0f;
                for (var h = 0; h < BeatHarmonics.Length; h++)
                {
                    tone += BeatHarmonics[h] * MathF.Sin((float)(2 * Math.PI * (h + 1) * phase));
                }

                samples[start + i] += level * tone * MathF.Exp(-t / BeatDecaySeconds) * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (BeatSeconds - t) / TapReleaseSeconds);
            }
        }

        private static float ChargeHz(ChargeKind kind) => kind switch
        {
            ChargeKind.Power => PowerChargeHz,
            ChargeKind.Frenzy => FrenzyChargeHz,
            ChargeKind.Endurance => EnduranceChargeHz,
            ChargeKind.Spirit => SpiritChargeHz,
            ChargeKind.Rage => RageHz,
            _ => OtherChargeHz,
        };

        private static float[] Shield(int sampleRate, float fromHz, float toHz, float level)
        {
            var samples = new float[(int)(ShieldSeconds * sampleRate)];
            var phases = new double[ShieldRatios.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var hz = fromHz + ((toHz - fromHz) * MathF.Min(1f, t / ShieldGlideSeconds));
                var value = 0f;
                for (var p = 0; p < ShieldRatios.Length; p++)
                {
                    phases[p] += hz * ShieldRatios[p] / sampleRate;
                    value += ShieldAmplitudes[p] * MathF.Exp(-t / ShieldDecaySeconds[p]) * MathF.Sin((float)(2 * Math.PI * phases[p]));
                }

                samples[i] = value * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (ShieldSeconds - t) / TapReleaseSeconds);
            }

            return Normalized(samples, level);
        }
    }
}
