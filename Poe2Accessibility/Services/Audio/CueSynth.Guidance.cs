namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Guidance cues of <see cref="CueSynth"/>: the beacon ping, the dodge warning, the reach ping and the crosshair.</summary>
    public static partial class CueSynth
    {
        /// <summary>
        ///     Guide beacon (D2R beacon.md): a struck iron chime on 620 Hz, unlike every other cue — harmonic partials so
        ///     its pitch (up or down) stays clear, one inharmonic for the metal — then the room.
        /// </summary>
        private const float PingSeconds = 0.12f;
        private const float PingHz = 620f;
        private const float PingReleaseSeconds = 0.025f;
        private const float PingRoom = 0.3f;
        private const float PingLevel = 0.5f;
        private static readonly float[] PingRatios = { 1f, 2f, 3f, 4.16f };
        private static readonly float[] PingAmplitudes = { 1f, 0.45f, 0.25f, 0.15f };
        private static readonly float[] PingDecaySeconds = { 0.1f, 0.05f, 0.03f, 0.015f };

        /// <summary>
        ///     Dodge warning: a blade drawn over a low gong, diving on the "now" into a bass drop. Metal and bass, unlike the
        ///     radar's narrow breath of open floor, which an earlier noise sweep was taken for; the user chose it over alarm
        ///     beeps and a plain whoosh, and a rising tone sounded like a cartoon (2026-10-28).
        /// </summary>
        private const float DodgeLeadSeconds = 0.2f;
        private const float DodgeLeadLevel = 0.7f;
        private const float DodgeLevel = 0.85f;
        private const float DodgeRoom = 0.25f;

        /// <summary>The blade: noise through a struck plate's resonances, narrow (steel, not breath), rising 4% as it is drawn.</summary>
        private static readonly float[] BladeModesHz = { 1180f, 2830f, 4420f, 6150f };
        private static readonly float[] BladeModeLevels = { 1f, 0.8f, 0.55f, 0.35f };
        private const float BladeDamping = 0.03f;
        private const float BladeRise = 0.04f;
        private const float BladeFloor = 0.3f;
        private const float BladeAttackSeconds = 0.012f;
        private const float BladeReleaseSeconds = 0.03f;
        private const float BladeLevel = 0.8f;

        /// <summary>The gong under it: a tam-tam's inharmonic partials, the higher ones blooming later and dying sooner.</summary>
        private const float GongHz = 92f;
        private static readonly float[] GongRatios = { 1f, 1.52f, 2.31f, 3.17f, 4.43f, 5.9f };
        private static readonly float[] GongAmplitudes = { 1f, 0.8f, 0.6f, 0.45f, 0.3f, 0.2f };
        private const float GongDecaySeconds = 0.5f;
        private const float GongLevel = 0.6f;
        private const float GongBloomSeconds = 0.02f;
        private const float GongBloomStepSeconds = 0.03f;
        private const float GongPartialDamping = 0.5f;
        private const float GongAttackSeconds = 0.003f;
        private const float GongReleaseSeconds = 0.02f;

        /// <summary>The drop: a sine diving 110 → 35 Hz, softly saturated, over a low click on the hit.</summary>
        private const float DropSeconds = 0.4f;
        private const float DropStartHz = 110f;
        private const float DropEndHz = 35f;
        private const float DropGlideSeconds = 0.07f;
        private const float DropDecaySeconds = 0.22f;
        private const float DropDrive = 2.4f;
        private const float DropClickSeconds = 0.006f;
        private const float DropClickLevel = 0.8f;
        private const float DropClickSmoothing = 0.25f;
        private const float DropAttackSeconds = 0.002f;
        private const float DropReleaseSeconds = 0.04f;

        /// <summary>Crosshair (D2R crosshair.md): two flat 24 ms blips at 1175 Hz, 40 ms apart, odd harmonics.</summary>
        private const float ReachLowHz = 1320f;
        private const float ReachHighHz = 1760f;
        private const float ReachNoteSeconds = 0.048f;
        private const float ReachLevel = 0.3f;
        private const float BlipHz = 1175f;
        private const float BlipSeconds = 0.024f;
        private const float BlipGapSeconds = 0.04f;
        private const float BlipLevel = 0.22f;

        public static float[] Ping(int sampleRate)
        {
            var samples = new float[(int)(PingSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var tone = 0f;
                for (var p = 0; p < PingRatios.Length; p++)
                {
                    tone += PingAmplitudes[p] * MathF.Exp(-t / PingDecaySeconds[p]) * MathF.Sin(2 * MathF.PI * PingHz * PingRatios[p] * t);
                }

                samples[i] = tone * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (PingSeconds - t) / PingReleaseSeconds);
            }

            return Normalized(Room(samples, sampleRate, PingRoom), PingLevel);
        }

        public static float[] Dodge(int sampleRate)
        {
            var lead = (int)(DodgeLeadSeconds * sampleRate);
            var blade = Normalized(Blade(lead, sampleRate), 1f);
            var gong = Normalized(BloomingGong(lead, sampleRate), 1f);
            var mixed = new float[lead];
            for (var i = 0; i < lead; i++)
            {
                mixed[i] = (BladeLevel * blade[i]) + (GongLevel * gong[i]);
            }

            var drop = Normalized(BassDrop(sampleRate), 1f);
            var samples = new float[lead + drop.Length];
            Array.Copy(Normalized(mixed, DodgeLeadLevel), samples, lead);
            Array.Copy(drop, 0, samples, lead, drop.Length);
            return Normalized(Room(samples, sampleRate, DodgeRoom), DodgeLevel);
        }

        private static float[] Blade(int length, int sampleRate)
        {
            var samples = new float[length];
            var seconds = (float)length / sampleRate;
            var low = new float[BladeModesHz.Length];
            var band = new float[BladeModesHz.Length];
            var random = new Random(11);
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / sampleRate;
                var noise = (float)((random.NextDouble() * 2) - 1);
                var value = 0f;
                for (var m = 0; m < BladeModesHz.Length; m++)
                {
                    var f = 2f * MathF.Sin(MathF.PI * BladeModesHz[m] * (1f + (BladeRise * t / seconds)) / sampleRate);
                    low[m] += f * band[m];
                    band[m] += f * (noise - low[m] - (BladeDamping * band[m]));
                    value += BladeModeLevels[m] * band[m];
                }

                var swell = BladeFloor + ((1f - BladeFloor) * t / seconds);
                samples[i] = value * swell * MathF.Min(1f, t / BladeAttackSeconds) * MathF.Min(1f, (seconds - t) / BladeReleaseSeconds);
            }

            return samples;
        }

        private static float[] BloomingGong(int length, int sampleRate)
        {
            var samples = new float[length];
            var seconds = (float)length / sampleRate;
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / sampleRate;
                var value = 0f;
                for (var p = 0; p < GongRatios.Length; p++)
                {
                    var bloom = MathF.Min(1f, t / (GongBloomSeconds + (GongBloomStepSeconds * p)));
                    value += GongAmplitudes[p] * bloom * MathF.Sin(2 * MathF.PI * GongHz * GongRatios[p] * t) * MathF.Exp(-t * (1 + (GongPartialDamping * p)) / GongDecaySeconds);
                }

                samples[i] = value * MathF.Min(1f, t / GongAttackSeconds) * MathF.Min(1f, (seconds - t) / GongReleaseSeconds);
            }

            return samples;
        }

        private static float[] BassDrop(int sampleRate)
        {
            var samples = new float[(int)(DropSeconds * sampleRate)];
            var phase = 0.0;
            var random = new Random(5);
            var click = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                phase += (DropEndHz + ((DropStartHz - DropEndHz) * MathF.Exp(-t / DropGlideSeconds))) / sampleRate;
                var body = MathF.Tanh(DropDrive * MathF.Sin((float)(2 * Math.PI * phase))) * MathF.Exp(-t / DropDecaySeconds);
                click += DropClickSmoothing * ((float)((random.NextDouble() * 2) - 1) - click);
                var hit = DropClickLevel * click * MathF.Exp(-t / DropClickSeconds);
                samples[i] = (body + hit) * MathF.Min(1f, t / DropAttackSeconds) * MathF.Min(1f, (DropSeconds - t) / DropReleaseSeconds);
            }

            return samples;
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
    }
}
