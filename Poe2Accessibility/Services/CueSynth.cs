namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>
    ///     Synthesized enemy cues (mono, full scale ±1), after the D2R mod's design (its docs/entity-cues.md): every
    ///     enemy shares one low growl so pitch only means up or down, and its rarity adds a layer on top. Timbres are
    ///     iron, stone and breath rather than plain beeps, so the cues sit in the game's dark sound world; the beacon and
    ///     the dodge warning ring on in a short stone-room tail.
    /// </summary>
    public static class CueSynth
    {
        /// <summary>A 90 ms band-limited buzz falling 175 → 125 Hz over its sub-octave, its loudness fluttering at 36 Hz.</summary>
        private const float GrowlSeconds = 0.09f;
        private const float GrowlStartHz = 175f;
        private const float GrowlEndHz = 125f;
        private const float GrowlFlutterHz = 36f;
        private const float GrowlSubLevel = 0.6f;
        private const float GrowlLevel = 0.58f;

        /// <summary>Harmonics stop below this, so the buzz is rough without hiss.</summary>
        private const float BandLimitHz = 2000f;

        /// <summary>Magic and rare: a bright 750 Hz buzz of six harmonics chattering at 62 Hz.</summary>
        private const float EliteHz = 750f;
        private const int EliteHarmonics = 6;
        private const float EliteChatterHz = 62f;
        private const float EliteLevel = 0.36f;

        /// <summary>Spread phases (rad × h²) keep the harmonics' crest low.</summary>
        private const float ElitePhaseSpread = 1.3f;

        /// <summary>Unique: a struck metal bar — a free bar's partial ratios, the higher ones dying faster.</summary>
        private const float RingSeconds = 0.14f;
        private const float RingHz = 520f;
        private const float RingReleaseSeconds = 0.01f;
        private const float RingLevel = 0.4f;
        private static readonly float[] RingRatios = { 1f, 2.756f, 5.404f, 8.933f };
        private static readonly float[] RingAmplitudes = { 1f, 0.5f, 0.25f, 0.12f };
        private static readonly float[] RingDecaySeconds = { 0.09f, 0.05f, 0.03f, 0.02f };

        private const float AttackSeconds = 0.004f;

        /// <summary>NPCs: a light wooden tap, 84 ms — a damped 700 Hz knock with a quicker overtone.</summary>
        private const float TapSeconds = 0.084f;
        private const float TapHz = 700f;
        private const float TapOvertoneRatio = 2.5f;
        private const float TapOvertoneLevel = 0.4f;
        private const float TapDecaySeconds = 0.012f;
        private const float TapOvertoneDecaySeconds = 0.006f;
        private const float TapReleaseSeconds = 0.01f;
        private const float TapLevel = 0.46f;

        /// <summary>Another player: two quicker, higher knocks, so it is not taken for an NPC (user 2026-10-26).</summary>
        private const float PlayerTapHz = 1100f;
        private const float PlayerTapGapSeconds = 0.075f;

        /// <summary>
        ///     Another player down, waiting to be revived: a soft ding-dong falling a minor third, quieter than the taps
        ///     (user 2026-10-27: rising sweeps sounded like the stun wind-up and drowned the game out).
        /// </summary>
        private const float DownHighHz = 659f;
        private const float DownLowHz = 554f;
        private const float DownNoteSeconds = 0.13f;
        private const float DownAttackSeconds = 0.012f;
        private const float DownDecaySeconds = 0.06f;
        private const float DownOvertoneLevel = 0.25f;
        private const float DownLevel = 0.3f;

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

        /// <summary>The mixer's output stays linear up to this level, then rounds off toward full scale.</summary>
        private const float LimitKnee = 0.8f;

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

        /// <summary>
        ///     Enemy states (EnemyStateWatch), one struck figure each: primed for stun a wind-up rising an octave and a half;
        ///     heavy stunned a low gong; frozen three
        ///     icy sparkles rising; electrocuted a crackling buzz; pinned two nail knocks; armour broken a gritty crack
        ///     falling; dazed a wobbling tone sinking; parried a single bright ting.
        /// </summary>
        private const float GritSeconds = 0.0025f;
        private const int GritSeed = 7;
        private static readonly float[] Bell = { 1f, 2.76f, 5.4f };
        private static readonly float[] Glass = { 1f, 1.012f, 2f };
        private static readonly float[] Buzz = { 1f, 2f, 3f, 4f, 5f, 6f, 7f };
        private static readonly float[] Wood = { 1f, 2.7f };
        private static readonly float[] Rubble = { 1f, 1.5f, 2.3f };
        private static readonly float[] Pure = { 1f };
        private static readonly float[] Organ = { 1f, 2f, 3f };
        private static readonly float[] Metal = { 1f, 2.92f };
        private static readonly float[] Plate = { 1f, 1.58f, 2.37f, 3.41f };

        /// <summary>
        ///     Parry now: a reverse hit — a struck buckler (a low plate and a ringing bell partial) in a stone room, played
        ///     backwards, so it swells in and stops dead on the moment to press Parry ("the attack is coming, parry it",
        ///     the user's ask, 2026-10-28). The dodge warning ends in a bass drop and a tail; this one ends in silence.
        /// </summary>
        private static readonly StateNote[] ParryHitNotes =
        {
            new(0f, 0.5f, 210f, 200f, Plate, 0.16f, Grit: 0.25f),
            new(0f, 0.35f, 840f, 830f, Bell, 0.09f, Grit: 0.15f),
        };

        private const float ParryRoom = 0.7f;
        private const float ParrySwellSeconds = 0.32f;
        private const float ParryFadeInSeconds = 0.03f;
        private const float ParryLevel = 0.8f;

        /// <summary>
        ///     An attack that cannot be blocked (the game's red glow): two low brass stabs a tritone apart, short and harsh, at
        ///     the wind-up, before the dodge warning itself.
        /// </summary>
        private static readonly StateNote[] UnblockableNotes =
        {
            new(0f, 0.09f, 147f, 147f, Buzz, 0.08f, Grit: 0.2f),
            new(0f, 0.09f, 208f, 208f, Buzz, 0.08f, Grit: 0.2f),
            new(0.15f, 0.09f, 147f, 147f, Buzz, 0.08f, Grit: 0.2f),
            new(0.15f, 0.09f, 208f, 208f, Buzz, 0.08f, Grit: 0.2f),
        };

        private const float UnblockableLevel = 0.7f;

        /// <summary>
        ///     A skill usable again (SkillCooldownWatch): one warm bell strike on G5 in a small stone room, a single note so
        ///     it never rises; lower and longer than the bell of what a skill places (C7).
        /// </summary>
        private static readonly StateNote[] SkillReadyNotes = { new(0f, 0.6f, 784f, 784f, Bell, 0.2f) };

        private const float SkillReadyRoom = 0.35f;
        private const float SkillReadyLevel = 0.3f;

        /// <summary>
        ///     Harmful ground under the player: a soft low tone (110 Hz and two overtones) swelling and fading twice a loop, so
        ///     it pulses like a quiet alarm while it lasts; a whole number of cycles per loop, silent at the seam.
        /// </summary>
        private const float HazardHz = 110f;
        private const float HazardPulseSeconds = 0.4f;
        private const int HazardPulses = 2;
        private const float HazardLevel = 0.3f;
        private static readonly float[] HazardHarmonics = { 1f, 0.35f, 0.15f };

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

            /// <summary>Not an enemy: an NPC, a tap instead of the growl.</summary>
            Friend,

            /// <summary>Another player, alive: a double tap.</summary>
            Player,

            /// <summary>Another player down: the ding-dong.</summary>
            PlayerDown,
        }

        /// <summary>A layer's glossary key, which sets its own volume.</summary>
        public static string LayerSound(Layer layer) => layer switch
        {
            Layer.Elite => "elite",
            Layer.Boss => "boss",
            Layer.Friend => "friend",
            Layer.Player => "player",
            Layer.PlayerDown => "player_down",
            _ => "enemy",
        };

        /// <summary>The growl with the layer for this enemy mixed in; a tap for an NPC, a double tap for a player.</summary>
        public static float[] Enemy(Layer layer, int sampleRate)
        {
            var growl = Growl(sampleRate);
            return layer switch
            {
                Layer.Elite => Sum(growl, EliteBuzz(sampleRate)),
                Layer.Boss => Sum(growl, Ring(sampleRate)),
                Layer.Friend => Tap(sampleRate),
                Layer.Player => PlayerTaps(sampleRate),
                Layer.PlayerDown => DownCall(sampleRate),
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
                var value = GrowlSubLevel * MathF.Sin((float)(Math.PI * phase));
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

                samples[i] = value * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (RingSeconds - t) / RingReleaseSeconds);
            }

            return Normalized(samples, RingLevel);
        }

        public static float[] Tap(int sampleRate) => Normalized(Knock(TapHz, sampleRate), TapLevel);

        public static float[] PlayerTaps(int sampleRate)
        {
            var knock = Knock(PlayerTapHz, sampleRate);
            var gap = (int)(PlayerTapGapSeconds * sampleRate);
            var samples = new float[gap + knock.Length];
            for (var i = 0; i < knock.Length; i++)
            {
                samples[i] += knock[i];
                samples[gap + i] += knock[i];
            }

            return Normalized(samples, TapLevel);
        }

        public static float[] DownCall(int sampleRate)
        {
            var note = (int)(DownNoteSeconds * sampleRate);
            var samples = new float[note * 2];
            for (var i = 0; i < note; i++)
            {
                var t = (float)i / sampleRate;
                var envelope = MathF.Min(1f, t / DownAttackSeconds) * MathF.Exp(-t / DownDecaySeconds) * MathF.Min(1f, (DownNoteSeconds - t) / TapReleaseSeconds);
                samples[i] = envelope * (MathF.Sin(2 * MathF.PI * DownHighHz * t) + (DownOvertoneLevel * MathF.Sin(4 * MathF.PI * DownHighHz * t)));
                samples[note + i] = envelope * (MathF.Sin(2 * MathF.PI * DownLowHz * t) + (DownOvertoneLevel * MathF.Sin(4 * MathF.PI * DownLowHz * t)));
            }

            return Normalized(samples, DownLevel);
        }

        /// <summary>A damped knock with a quicker overtone (the NPC tap, and the player's at a higher pitch).</summary>
        private static float[] Knock(float hz, int sampleRate)
        {
            var samples = new float[(int)(TapSeconds * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var knock = (MathF.Sin(2 * MathF.PI * hz * t) * MathF.Exp(-t / TapDecaySeconds)) +
                            (TapOvertoneLevel * MathF.Sin(2 * MathF.PI * hz * TapOvertoneRatio * t) * MathF.Exp(-t / TapOvertoneDecaySeconds));
                samples[i] = knock * MathF.Min(1f, t / AttackSeconds) * MathF.Min(1f, (TapSeconds - t) / TapReleaseSeconds);
            }

            return samples;
        }

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

        public static float[] StateCue(EnemyState state, int sampleRate)
        {
            var (notes, level) = state switch
            {
                EnemyState.PrimedForStun => (new[] { new StateNote(0f, 0.18f, 300f, 900f, Organ, 0.3f) }, 0.3f),
                EnemyState.HeavyStunned => (new[] { new StateNote(0f, 0.35f, 196f, 185f, Bell, 0.18f) }, 0.45f),
                EnemyState.Frozen => (new[]
                {
                    new StateNote(0f, 0.09f, 2349f, 2349f, Glass, 0.05f),
                    new StateNote(0.05f, 0.09f, 2794f, 2794f, Glass, 0.05f),
                    new StateNote(0.1f, 0.12f, 3520f, 3520f, Glass, 0.06f),
                }, 0.28f),
                EnemyState.Electrocuted => (new[] { new StateNote(0f, 0.2f, 110f, 90f, Buzz, 0.12f, Grit: 0.6f) }, 0.32f),
                EnemyState.Pinned => (new[]
                {
                    new StateNote(0f, 0.06f, 880f, 700f, Wood, 0.02f, Grit: 0.3f),
                    new StateNote(0.08f, 0.06f, 880f, 700f, Wood, 0.02f, Grit: 0.3f),
                }, 0.4f),
                EnemyState.ArmourBroken => (new[] { new StateNote(0f, 0.18f, 520f, 160f, Rubble, 0.07f, Grit: 0.8f) }, 0.4f),
                EnemyState.Dazed => (new[] { new StateNote(0f, 0.32f, 700f, 500f, Pure, 0.25f, VibratoHz: 7f, VibratoDepth: 0.12f) }, 0.28f),
                _ => (new[] { new StateNote(0f, 0.14f, 1568f, 1568f, Metal, 0.05f) }, 0.3f),
            };

            return Struck(notes, level, sampleRate);
        }

        /// <summary>Parry now (<see cref="ParryHitNotes"/>): the cue's last sample is the moment to press.</summary>
        public static float[] Parry(int sampleRate) => ReverseHit(Struck(ParryHitNotes, 1f, sampleRate), ParryRoom, sampleRate);

        /// <summary>An attack that cannot be blocked (<see cref="UnblockableNotes"/>).</summary>
        public static float[] Unblockable(int sampleRate) => Struck(UnblockableNotes, UnblockableLevel, sampleRate);

        /// <summary>A skill usable again (<see cref="SkillReadyNotes"/>).</summary>
        public static float[] SkillReady(int sampleRate) => Normalized(Room(Struck(SkillReadyNotes, 1f, sampleRate), sampleRate, SkillReadyRoom), SkillReadyLevel);

        /// <summary>The hazard alarm's loop (<see cref="HazardHz"/>): each pulse swells and fades within its first half.</summary>
        public static float[] HazardAlarm(int sampleRate)
        {
            var samples = new float[(int)(HazardPulseSeconds * HazardPulses * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var phase = (t % HazardPulseSeconds) / HazardPulseSeconds;
                var swell = phase < 0.5f ? MathF.Pow(MathF.Sin(MathF.PI * phase * 2f), 2f) : 0f;
                var tone = 0f;
                for (var h = 0; h < HazardHarmonics.Length; h++)
                {
                    tone += HazardHarmonics[h] * MathF.Sin(2 * MathF.PI * HazardHz * (h + 1) * t);
                }

                samples[i] = tone * swell;
            }

            return Normalized(samples, HazardLevel);
        }

        /// <summary>A hit and its room played backwards, the last <see cref="ParrySwellSeconds"/> of it: a swell into a dead stop.</summary>
        internal static float[] ReverseHit(float[] hit, float room, int sampleRate)
        {
            var reversed = Room(hit, sampleRate, room);
            Array.Reverse(reversed);
            var length = Math.Min(reversed.Length, (int)(ParrySwellSeconds * sampleRate));
            var skip = reversed.Length - length;
            var fade = (int)(ParryFadeInSeconds * sampleRate);
            var swell = new float[length];
            for (var i = 0; i < length; i++)
            {
                swell[i] = reversed[skip + i] * MathF.Min(1f, (float)i / fade);
            }

            return Normalized(swell, ParryLevel);
        }

        /// <summary>Struck notes summed and brought to <paramref name="level"/> (enemy states, parry).</summary>
        internal static float[] Struck(StateNote[] notes, float level, int sampleRate)
        {
            var samples = new float[(int)(notes.Max(note => note.Start + note.Seconds) * sampleRate) + 1];
            var random = new Random(GritSeed);
            var gritStep = Math.Max(1, (int)(GritSeconds * sampleRate));
            foreach (var note in notes)
            {
                var start = (int)(note.Start * sampleRate);
                var phases = new double[note.Ratios.Length];
                var grit = 1f;
                for (var i = 0; i < (int)(note.Seconds * sampleRate); i++)
                {
                    var t = (float)i / sampleRate;
                    var hz = (note.FromHz + ((note.ToHz - note.FromHz) * t / note.Seconds)) *
                             (1f + (note.VibratoDepth * MathF.Sin(2 * MathF.PI * note.VibratoHz * t)));
                    if (i % gritStep == 0)
                    {
                        grit = 1f - (note.Grit * (float)random.NextDouble());
                    }

                    var value = 0f;
                    for (var p = 0; p < note.Ratios.Length; p++)
                    {
                        phases[p] += hz * note.Ratios[p] / sampleRate;
                        value += MathF.Sin((float)(2 * Math.PI * phases[p])) / (p + 1);
                    }

                    samples[start + i] += value * grit * MathF.Exp(-t / note.Decay) * MathF.Min(1f, t / AttackSeconds) *
                                          MathF.Min(1f, (note.Seconds - t) / TapReleaseSeconds);
                }
            }

            return Normalized(samples, level);
        }

        public static float[] ShieldUp(int sampleRate) => Shield(sampleRate, ShieldLowHz, ShieldHighHz, ShieldUpLevel);

        public static float[] ShieldDown(int sampleRate) => Shield(sampleRate, ShieldHighHz, ShieldLowHz, ShieldDownLevel);

        /// <summary>The mixer's output stage: linear to the knee, then a soft curve that never passes full scale.</summary>
        public static float Limit(float sample)
        {
            var magnitude = MathF.Abs(sample);
            if (magnitude <= LimitKnee)
            {
                return sample;
            }

            var over = (magnitude - LimitKnee) / (1f - LimitKnee);
            return MathF.CopySign(LimitKnee + ((1f - LimitKnee) * MathF.Tanh(over)), sample);
        }

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

        /// <summary>One struck note of an enemy-state cue: onset, length, pitch glide, partials (falling 1/n), decay, wobble and crackle.</summary>
        internal readonly record struct StateNote(
            float Start, float Seconds, float FromHz, float ToHz, float[] Ratios, float Decay, float VibratoHz = 0f, float VibratoDepth = 0f, float Grit = 0f);

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
