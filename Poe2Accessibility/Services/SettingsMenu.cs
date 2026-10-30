namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    ///     The mod's settings, spoken (D2R in-game settings list): opened by holding L3 for a second and a half in gameplay;
    ///     the right stick then moves through the list (up/down) and changes the value (left/right), with no need to
    ///     hold L3; a tap on L3 closes it and saves to config/settings.txt. Switches toggle, percentages move in
    ///     quarters, the list wraps. Fields are reached by name, so the list is the only place to touch when a
    ///     setting is added.
    /// </summary>
    public sealed class SettingsMenu
    {
        /// <summary>The list opens on the controls help (D2R onboarding): one topic per entry, read in full.</summary>
        private static readonly string[] HelpTopics = { "help_l3", "help_scanner", "help_guide", "help_menus", "help_settings" };

        private static readonly Item[] SettingItems =
        {
            Switch(nameof(AccessibilitySettings.AnnounceTarget)),
            Switch(nameof(AccessibilitySettings.AnnounceMonsterMods)),
            Switch(nameof(AccessibilitySettings.AnnounceLowLife)),
            Switch(nameof(AccessibilitySettings.AnnounceLowMana)),
            Switch(nameof(AccessibilitySettings.AnnounceEnergyShield)),
            Switch(nameof(AccessibilitySettings.AnnounceFlasks)),
            Switch(nameof(AccessibilitySettings.AnnounceBossLife)),
            Switch(nameof(AccessibilitySettings.StatusAlerts)),
            Switch(nameof(AccessibilitySettings.ChargeAlerts)),
            Switch(nameof(AccessibilitySettings.AnnounceNearby)),
            Switch(nameof(AccessibilitySettings.AnnouncePlaces)),
            Switch(nameof(AccessibilitySettings.AnnounceInteractTarget)),
            Switch(nameof(AccessibilitySettings.AnnounceChat)),
            Switch(nameof(AccessibilitySettings.AnnouncePlayerChat)),
            Switch(nameof(AccessibilitySettings.ReadTooltipsOnFocus)),
            Switch(nameof(AccessibilitySettings.AnnouncePickups)),
            Switch(nameof(AccessibilitySettings.AnnounceDiscoveries)),
            Switch(nameof(AccessibilitySettings.AnnounceEnemies)),
            Switch(nameof(AccessibilitySettings.RoutePrompts)),
            Switch(nameof(AccessibilitySettings.AnnounceHand)),
            Switch(nameof(AccessibilitySettings.AnnounceMoves)),
            Switch(nameof(AccessibilitySettings.AnnounceLevelUp)),
            Switch(nameof(AccessibilitySettings.Radar)),
            Switch(nameof(AccessibilitySettings.SmallObstacles)),
            Switch(nameof(AccessibilitySettings.WallTones)),
            Switch(nameof(AccessibilitySettings.UnexploredLoop)),
            Switch(nameof(AccessibilitySettings.UnexploredMark)),
            Switch(nameof(AccessibilitySettings.EnemySounds)),
            Switch(nameof(AccessibilitySettings.NpcSounds)),
            Switch(nameof(AccessibilitySettings.PlayerSounds)),
            Switch(nameof(AccessibilitySettings.WallBump)),
            Switch(nameof(AccessibilitySettings.ObjectSounds)),
            Choice(nameof(AccessibilitySettings.LootSounds), LootFilter.Levels),
            Switch(nameof(AccessibilitySettings.DodgeWarnings)),
            Switch(nameof(AccessibilitySettings.UnblockableCues)),
            Switch(nameof(AccessibilitySettings.ParryCues)),
            Number(nameof(AccessibilitySettings.ParryLeadMs), 0, 600, 50),
            Switch(nameof(AccessibilitySettings.HazardAlarm)),
            Switch(nameof(AccessibilitySettings.BossWeakPoint)),
            Switch(nameof(AccessibilitySettings.ShieldSounds)),
            Switch(nameof(AccessibilitySettings.AnnounceStunBar)),
            Choice(nameof(AccessibilitySettings.CullCue), CullThreshold.Modes),
            Switch(nameof(AccessibilitySettings.EnemyStateCues)),
            Switch(nameof(AccessibilitySettings.PrimedStunCues)),
            Switch(nameof(AccessibilitySettings.CrosshairBeep)),
            Number(nameof(AccessibilitySettings.LowLifeBelow), 5, 70, 5),
            Volume(nameof(AccessibilitySettings.SoundVolume)),
            Volume(nameof(AccessibilitySettings.RadarVolume)),
            Number(nameof(AccessibilitySettings.WallToneRange), 200, 1600, 100),
            Switch(nameof(AccessibilitySettings.MeleeReach)),
            Number(nameof(AccessibilitySettings.MeleeReachRange), 0, 200, 10),
            Volume(nameof(AccessibilitySettings.EnemyVolume)),
            Volume(nameof(AccessibilitySettings.DodgeVolume)),
            Volume(nameof(AccessibilitySettings.BeaconVolume)),
            Volume(nameof(AccessibilitySettings.ObjectVolume)),
            Choice(nameof(AccessibilitySettings.Language), new[] { "auto" }.Concat(Localization.Languages).ToArray()),
            Choice(nameof(AccessibilitySettings.SpeechStatusBegins), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechStatusEnds), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechLifeAndMana), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechBossLife), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechEnemyNames), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechQuest), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechPlaces), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechGroundItems), SpeechPriority.Levels),
            Choice(nameof(AccessibilitySettings.SpeechArrivals), SpeechPriority.Levels),
            Switch(nameof(AccessibilitySettings.ReadPlayer1)),
            Switch(nameof(AccessibilitySettings.ReadPlayer2)),
            Switch(nameof(AccessibilitySettings.CheckForUpdates)),
        };

        /// <summary>Highest volume, percent; above 100 a cue is driven into a soft limit rather than only clipped (AudioCueService).</summary>
        public const int MaxVolume = 300;

        private const int VolumeStep = 25;

        private readonly Item[] items;
        private readonly SoundGlossary? glossary;
        private int index;

        /// <param name="glossary">Its sounds become the list's last group: name and meaning, left or right plays it.</param>
        public SettingsMenu(SoundGlossary? glossary = null)
        {
            this.glossary = glossary;
            var all = new List<Item>(HelpTopics.Select(topic => new Item(topic, Kind.Help, 0, 0, 0, Array.Empty<string>())));
            all.AddRange(SettingItems);
            foreach (var key in glossary?.Keys ?? Array.Empty<string>())
            {
                all.Add(new Item(key, Kind.Sound, 0, 0, 0, Array.Empty<string>()));
            }

            this.items = all.ToArray();
        }

        public bool IsOpen { get; private set; }

        /// <summary>Opens where it was left; returns what to say.</summary>
        public string Open(AccessibilitySettings settings)
        {
            this.IsOpen = true;
            return Announcements.InContext(Announcements.SettingsMenuTitle, this.Describe(settings));
        }

        public string Close()
        {
            this.IsOpen = false;
            return Announcements.SettingsMenuClosed;
        }

        /// <summary>Next / previous setting (wraps).</summary>
        public string Move(AccessibilitySettings settings, int step)
        {
            this.index = (((this.index + step) % this.items.Length) + this.items.Length) % this.items.Length;
            return this.Describe(settings);
        }

        /// <summary>Toggles a switch or steps a value; returns the new value only.</summary>
        public string Change(AccessibilitySettings settings, int step)
        {
            var item = this.items[this.index];
            if (item.Kind == Kind.Sound)
            {
                this.glossary?.Play(item.Name, SoundGlossary.VolumeSetting(item.Name) is { } volume ? (int)Field(volume).GetValue(settings)! / 100f : 1f);
                return string.Empty;
            }

            if (item.Kind == Kind.Help)
            {
                return Announcements.HelpText(item.Name); // left or right reads the topic again
            }

            var field = Field(item.Name);
            switch (item.Kind)
            {
                case Kind.Switch:
                    field.SetValue(settings, !(bool)field.GetValue(settings)!);
                    break;
                case Kind.Number or Kind.Volume:
                    field.SetValue(settings, Math.Clamp((int)field.GetValue(settings)! + (step * item.Step), item.Min, item.Max));
                    break;
                default:
                    var current = Array.IndexOf(item.Choices, (string)field.GetValue(settings)!);
                    field.SetValue(settings, item.Choices[(((current + step) % item.Choices.Length) + item.Choices.Length) % item.Choices.Length]);
                    break;
            }

            return this.Value(settings, item);
        }

        private string Describe(AccessibilitySettings settings)
        {
            var item = this.items[this.index];
            return item.Kind == Kind.Help
                ? Announcements.SettingLine(Announcements.HelpTitle(item.Name), Announcements.HelpText(item.Name), this.index + 1, this.items.Length)
                : item.Kind == Kind.Sound
                ? Announcements.SettingLine(Announcements.SoundName(item.Name), SoundLine(item.Name), this.index + 1, this.items.Length)
                : Announcements.SettingLine(Announcements.SettingName(item.Name), this.Value(settings, item), this.index + 1, this.items.Length);
        }

        /// <summary>A sound's meaning and the volume that sets it ("…, Volumen de enemigos"), so the player knows which to raise.</summary>
        private static string SoundLine(string key) => SoundGlossary.VolumeSetting(key) is { } volume
            ? $"{Announcements.SoundMeaning(key)}, {Announcements.SettingName(volume)}"
            : Announcements.SoundMeaning(key);

        private string Value(AccessibilitySettings settings, Item item)
        {
            var value = Field(item.Name).GetValue(settings);
            return item.Kind switch
            {
                Kind.Switch => (bool)value! ? Announcements.On : Announcements.Off,
                Kind.Volume => Announcements.Percent((int)value!),
                Kind.Number => $"{value}",
                _ => Announcements.ChoiceName((string)value!),
            };
        }

        private static FieldInfo Field(string name) =>
            typeof(AccessibilitySettings).GetField(name) ?? throw new InvalidOperationException($"No setting {name}");

        private static Item Switch(string name) => new(name, Kind.Switch, 0, 0, 0, Array.Empty<string>());

        private static Item Number(string name, int min, int max, int step) => new(name, Kind.Number, min, max, step, Array.Empty<string>());

        private static Item Volume(string name) => new(name, Kind.Volume, 0, MaxVolume, VolumeStep, Array.Empty<string>());

        private static Item Choice(string name, params string[] choices) => new(name, Kind.Choice, 0, 0, 0, choices);

        private enum Kind
        {
            Switch,
            Number,

            /// <summary>A percentage of a sound's designed level, 0 to <see cref="MaxVolume"/>.</summary>
            Volume,
            Choice,

            /// <summary>A glossary entry: plays a sound, changes nothing.</summary>
            Sound,

            /// <summary>A controls help topic: read, changes nothing.</summary>
            Help,
        }

        private sealed record Item(string Name, Kind Kind, int Min, int Max, int Step, string[] Choices);
    }
}
