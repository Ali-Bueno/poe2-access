namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Every sound the mod makes, by key, for the settings menu's last group (D2R sound glossary): landing on a
    ///     sound says its name and meaning, left or right plays it. Synthesized cues are built here; the game's own
    ///     sounds come from <see cref="GameSounds"/> and are listed only when they were extracted.
    /// </summary>
    public sealed class SoundGlossary
    {
        private readonly AudioCueService audio;
        private readonly Dictionary<string, float[]> sounds = new();
        private readonly Dictionary<string, StereoSound> stereo = new();
        private readonly List<string> keys = new();

        public SoundGlossary(AudioCueService audio, GameSounds gameSounds, GameSounds modSounds)
        {
            this.audio = audio;
            var rate = AudioCueService.SampleRate;
            this.Add("beacon", CueSynth.Ping(rate));
            this.Add("dodge", CueSynth.Dodge(rate));
            this.Add("parry", CueSynth.Parry(rate));
            this.Add("unblockable", CueSynth.Unblockable(rate));
            this.Add("hazard", CueSynth.HazardAlarm(rate));
            this.Add("crosshair", CueSynth.Crosshair(rate));
            this.Add("weak_point", CueSynth.Heartbeat(rate));
            this.Add("shield_up", CueSynth.ShieldUp(rate));
            this.Add("shield_down", CueSynth.ShieldDown(rate));
            this.Add("cull", modSounds.Get("cull") ?? CueSynth.Cull(rate));
            this.Add("charge_gained", CueSynth.ChargeGained(ChargeKind.Frenzy, 3, rate));
            this.Add("charge_lost", CueSynth.ChargeLost(ChargeKind.Frenzy, rate));
            if (gameSounds.GetStereo("power_charge") is { } power)
            {
                this.AddStereo("power_charge", power);
            }

            if (modSounds.GetStereo("combo") is { } combo)
            {
                this.AddStereo("combo", combo);
            }
            this.Add("deployed", CueSynth.Deployed(rate));
            foreach (var state in Enum.GetValues<EnemyState>())
            {
                this.Add($"state_{state}", CueSynth.StateCue(state, rate));
            }

            this.Add("reach", CueSynth.ReachPing(rate));
            this.Add("enemy", CueSynth.Enemy(CueSynth.Layer.None, rate));
            this.Add("elite", CueSynth.Enemy(CueSynth.Layer.Elite, rate));
            this.Add("boss", CueSynth.Enemy(CueSynth.Layer.Boss, rate));
            this.Add("friend", CueSynth.Enemy(CueSynth.Layer.Friend, rate));
            this.Add("player", CueSynth.Enemy(CueSynth.Layer.Player, rate));
            this.Add("player_down", CueSynth.Enemy(CueSynth.Layer.PlayerDown, rate));
            this.Add("npc_quest", CueSynth.Ring(rate));
            this.Add("radar_wall", RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Left, rate));
            this.Add("radar_open", RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate));
            this.Add("radar_exit", RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Left, rate));
            this.Add("radar_obstacle", RadarSynth.Obstacle(RadarSynth.Direction.Left, rate));
            this.Add("radar_unexplored", RadarSynth.WithUnexploredTail(RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate), RadarSynth.Direction.Left, rate));
            if (modSounds.GetLoop(UnexploredLoop.LoopName) is { } unexplored)
            {
                this.Add(UnexploredLoop.LoopName, Excerpt(unexplored, rate));
            }

            this.Add("radar2_wall", RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("radar2_open", RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("radar2_exit", RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("radar2_obstacle", RadarSynth.Obstacle(RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("bump", RadarSynth.Bump(rate));
            this.Add("wall_tone", Excerpt(RadarSynth.WallTone(RadarSynth.Direction.Up, rate), rate));
            foreach (var game in new[] { "item", "item_rare", "currency", "gold", "quest_item", "chest", "corpse", "breakable", "switch", "shrine", "ritual", "exit", "portal", "waypoint", "checkpoint", "tempest_bell", "ice_crystal" })
            {
                if ((gameSounds.GetLoop(game) is { } loop ? Excerpt(loop, rate) : gameSounds.Get(game)) is { } samples)
                {
                    this.Add(game, samples);
                }
            }
        }

        /// <summary>Sound keys in menu order.</summary>
        public IReadOnlyList<string> Keys => this.keys;

        /// <summary>A second of a loop with soft edges: what a wall right ahead sounds like.</summary>
        private static float[] Excerpt(float[] loop, int rate)
        {
            var length = Math.Min(loop.Length, rate);
            var fade = rate / 20;
            var excerpt = new float[length];
            for (var i = 0; i < length; i++)
            {
                excerpt[i] = loop[i] * Math.Min(1f, Math.Min(i, length - 1 - i) / (float)fade);
            }

            return excerpt;
        }

        /// <summary>
        ///     The volume setting a sound plays under in the game (Poe2AccessibilityCore.ApplySoundVolumes), null for one
        ///     only the master volume sets: the glossary played every sound at 100 %, so a tester raising the enemy volume
        ///     to 300 % heard the harmful-ground alarm no louder there (2026-10-29).
        /// </summary>
        public static string? VolumeSetting(string key) => key switch
        {
            "beacon" => nameof(AccessibilitySettings.BeaconVolume),
            "dodge" or "parry" or "unblockable" => nameof(AccessibilitySettings.DodgeVolume),
            "hazard" or "cull" or "crosshair" or "weak_point" or "reach" or "enemy" or "elite" or "boss" or "friend" or "player" or "player_down" =>
                nameof(AccessibilitySettings.EnemyVolume),
            _ when key.StartsWith("state_", StringComparison.Ordinal) => nameof(AccessibilitySettings.EnemyVolume),
            "bump" or "wall_tone" => nameof(AccessibilitySettings.RadarVolume),
            _ when key.StartsWith("radar", StringComparison.Ordinal) => nameof(AccessibilitySettings.RadarVolume),
            "shield_up" or "shield_down" or "charge_gained" or "charge_lost" or "power_charge" or "combo" => null,
            _ => nameof(AccessibilitySettings.ObjectVolume),
        };

        /// <summary>Plays a sound as loud as the game would: <paramref name="gain"/> is its volume setting, 1 = 100 %.</summary>
        public void Play(string key, float gain)
        {
            if (this.stereo.TryGetValue(key, out var sound))
            {
                this.audio.Play(sound, 1f, gain);
            }
            else if (this.sounds.TryGetValue(key, out var samples))
            {
                this.audio.Play(samples, 0f, 1f, gain);
            }
        }

        private void Add(string key, float[] samples)
        {
            this.sounds[key] = samples;
            this.keys.Add(key);
        }

        /// <summary>A sound the player's own news plays unpanned, as recorded.</summary>
        private void AddStereo(string key, StereoSound sound)
        {
            this.stereo[key] = sound;
            this.keys.Add(key);
        }
    }
}
