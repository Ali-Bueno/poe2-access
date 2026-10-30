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
        /// <summary>The drop each item group is heard by here: its most common one in catalog/item-sounds.json.</summary>
        private static readonly (string Key, string Sample)[] ItemGroupSamples =
        {
            (ItemSoundGroup.Flask, "Flask_Drop_01"),
            (ItemSoundGroup.Equipment, "HelmetMetal_Drop_01"),
            (ItemSoundGroup.Gem, "SupportGem_Drop_01"),
        };

        private readonly AudioCueService audio;
        private readonly Dictionary<string, float[]> sounds = new();
        private readonly Dictionary<string, StereoSound> stereo = new();
        private readonly List<string> keys = new();

        /// <remarks>Grouped as the settings menu's submenus: your skills, enemies, combat, defence, navigation, nearby, items.</remarks>
        public SoundGlossary(AudioCueService audio, GameSounds gameSounds, GameSounds modSounds, ItemDropSounds drops)
        {
            this.audio = audio;
            var rate = AudioCueService.SampleRate;
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
            this.AddGame(gameSounds, rate, "tempest_bell", "ice_crystal");

            this.Add("enemy", CueSynth.Enemy(CueSynth.Layer.None, rate));
            this.Add("elite", CueSynth.Enemy(CueSynth.Layer.Elite, rate));
            this.Add("boss", CueSynth.Enemy(CueSynth.Layer.Boss, rate));
            this.Add("reach", CueSynth.ReachPing(rate));
            this.Add("crosshair", CueSynth.Crosshair(rate));

            this.Add("weak_point", CueSynth.Heartbeat(rate));
            foreach (var state in Enum.GetValues<EnemyState>())
            {
                this.Add($"state_{state}", CueSynth.StateCue(state, rate));
            }

            this.Add("cull", modSounds.Get("cull") ?? CueSynth.Cull(rate));

            this.Add("dodge", CueSynth.Dodge(rate));
            this.Add("unblockable", CueSynth.Unblockable(rate));
            this.Add("parry", CueSynth.Parry(rate));
            this.Add("shield_up", CueSynth.ShieldUp(rate));
            this.Add("shield_down", CueSynth.ShieldDown(rate));
            this.Add("hazard", CueSynth.HazardAlarm(rate));

            this.Add("radar_wall", RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Left, rate));
            this.Add("radar_open", RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate));
            this.Add("radar_exit", RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Left, rate));
            this.Add("radar_obstacle", RadarSynth.Obstacle(RadarSynth.Direction.Left, rate));
            this.Add("radar_unexplored", RadarSynth.WithUnexploredTail(RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate), RadarSynth.Direction.Left, rate));
            if (modSounds.GetLoop(UnexploredLoop.LoopName) is { } unexplored)
            {
                this.Add(UnexploredLoop.LoopName, Excerpt(unexplored, rate));
            }

            this.Add("wall_tone", Excerpt(RadarSynth.WallTone(RadarSynth.Direction.Up, rate), rate));
            this.Add("bump", RadarSynth.Bump(rate));
            this.Add("beacon", CueSynth.Ping(rate));
            if (gameSounds.Get("waypoint") is { } chime)
            {
                this.Add(DiscoveryNarrator.SoundKey, chime);
            }

            this.Add("radar2_wall", RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("radar2_open", RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("radar2_exit", RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));
            this.Add("radar2_obstacle", RadarSynth.Obstacle(RadarSynth.Direction.Left, rate, RadarSynth.Voice.Second));

            this.AddGame(gameSounds, rate, "exit", "portal", "waypoint", "checkpoint", "chest", "corpse", "breakable", "switch", "shrine", "ritual");
            this.Add("friend", CueSynth.Enemy(CueSynth.Layer.Friend, rate));
            this.Add("npc_quest", CueSynth.Ring(rate));
            this.Add("player", CueSynth.Enemy(CueSynth.Layer.Player, rate));
            this.Add("player_down", CueSynth.Enemy(CueSynth.Layer.PlayerDown, rate));

            foreach (var (key, sample) in ItemGroupSamples)
            {
                if (drops.Sample(sample) is { } samples)
                {
                    this.Add(key, samples);
                }
            }

            this.AddGame(gameSounds, rate, "item", "item_rare", "currency", "gold", "quest_item");
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
                this.audio.Play(sound, 1f, gain, key);
            }
            else if (this.sounds.TryGetValue(key, out var samples))
            {
                this.audio.Play(samples, 0f, 1f, gain, key);
            }
        }

        /// <summary>The game's own sounds that were extracted, a loop by a second of it.</summary>
        private void AddGame(GameSounds gameSounds, int rate, params string[] keys)
        {
            foreach (var key in keys)
            {
                if ((gameSounds.GetLoop(key) is { } loop ? Excerpt(loop, rate) : gameSounds.Get(key)) is { } samples)
                {
                    this.Add(key, samples);
                }
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
