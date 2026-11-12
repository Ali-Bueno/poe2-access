namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Enemy cues of <see cref="CueSynth"/>: the growl, the elite and boss layers, the NPC and player taps.</summary>
    public static partial class CueSynth
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

        /// <summary>NPCs: a light wooden tap, 84 ms — a damped 700 Hz knock with a quicker overtone.</summary>
        private const float TapSeconds = 0.084f;
        private const float TapHz = 700f;
        private const float TapOvertoneRatio = 2.5f;
        private const float TapOvertoneLevel = 0.4f;
        private const float TapDecaySeconds = 0.012f;
        private const float TapOvertoneDecaySeconds = 0.006f;

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
    }
}
