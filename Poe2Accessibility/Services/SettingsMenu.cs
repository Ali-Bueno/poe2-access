namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    ///     The mod's settings, spoken (D2R in-game settings list): opened by holding L3 for a second and a half in gameplay.
    ///     The top level lists the groups (D2R submenus); left or right opens one, a tap on L3 goes back to the groups,
    ///     and a tap there or a double tap anywhere closes it and saves to config/settings.txt. Within a group the right
    ///     stick moves (up/down) and changes the value (left/right) with no need to hold L3; switches toggle,
    ///     percentages move in quarters, every list wraps. Fields are reached by name, so the groups are the only place
    ///     to touch when a setting is added.
    /// </summary>
    public sealed class SettingsMenu
    {
        /// <summary>The list opens on the controls help (D2R onboarding): one topic per entry, read in full.</summary>
        private static readonly string[] HelpTopics = { "help_l3", "help_scanner", "help_guide", "help_menus", "help_settings" };

        private static readonly Group[] SettingGroups =
        {
            new("character", new[]
            {
                Switch(nameof(AccessibilitySettings.AnnounceLowLife)),
                Number(nameof(AccessibilitySettings.LowLifeBelow), 5, 70, 5),
                Switch(nameof(AccessibilitySettings.AnnounceLowMana)),
                Switch(nameof(AccessibilitySettings.AnnounceEnergyShield)),
                Switch(nameof(AccessibilitySettings.AnnounceFlasks)),
                Switch(nameof(AccessibilitySettings.StatusAlerts)),
                Switch(nameof(AccessibilitySettings.ChargeSpeech)),
                Switch(nameof(AccessibilitySettings.ChargeSounds)),
                Switch(nameof(AccessibilitySettings.ComboSpeech)),
                Switch(nameof(AccessibilitySettings.ComboSounds)),
                Switch(nameof(AccessibilitySettings.DeployedCues)),
                Switch(nameof(AccessibilitySettings.AnnounceLevelUp)),
            }),
            new("enemies", new[]
            {
                Switch(nameof(AccessibilitySettings.AnnounceEnemies)),
                Switch(nameof(AccessibilitySettings.EnemyDirections)),
                Switch(nameof(AccessibilitySettings.AnnounceTarget)),
                Switch(nameof(AccessibilitySettings.AnnounceMonsterMods)),
                Switch(nameof(AccessibilitySettings.AnnounceBossLife)),
                Switch(nameof(AccessibilitySettings.EnemySounds)),
                Switch(nameof(AccessibilitySettings.CrosshairBeep)),
                Switch(nameof(AccessibilitySettings.MeleeReach)),
                Number(nameof(AccessibilitySettings.MeleeReachRange), 0, 200, 10),
                Switch(nameof(AccessibilitySettings.EnemyStateCues)),
                Switch(nameof(AccessibilitySettings.PrimedStunCues)),
                Choice(nameof(AccessibilitySettings.CullCue), CullThreshold.Modes),
                Switch(nameof(AccessibilitySettings.BossWeakPoint)),
            }),
            new("defence", new[]
            {
                Switch(nameof(AccessibilitySettings.DodgeWarnings)),
                Switch(nameof(AccessibilitySettings.UnblockableCues)),
                Switch(nameof(AccessibilitySettings.ParryCues)),
                Number(nameof(AccessibilitySettings.ParryLeadMs), 0, 600, 50),
                Switch(nameof(AccessibilitySettings.ShieldSounds)),
                Switch(nameof(AccessibilitySettings.AnnounceStunBar)),
                Switch(nameof(AccessibilitySettings.HazardAlarm)),
            }),
            new("navigation", new[]
            {
                Switch(nameof(AccessibilitySettings.Radar)),
                Switch(nameof(AccessibilitySettings.SmallObstacles)),
                Switch(nameof(AccessibilitySettings.UnexploredMark)),
                Switch(nameof(AccessibilitySettings.UnexploredLoop)),
                Switch(nameof(AccessibilitySettings.WallTones)),
                Number(nameof(AccessibilitySettings.WallToneRange), 200, 1600, 100),
                Switch(nameof(AccessibilitySettings.WallBump)),
                Switch(nameof(AccessibilitySettings.RoutePrompts)),
                Switch(nameof(AccessibilitySettings.AnnouncePlaces)),
                Switch(nameof(AccessibilitySettings.AnnounceDiscoveries)),
            }),
            new("surroundings", new[]
            {
                Switch(nameof(AccessibilitySettings.AnnounceNearby)),
                Switch(nameof(AccessibilitySettings.AnnounceInteractTarget)),
                Switch(nameof(AccessibilitySettings.ObjectSounds)),
                Switch(nameof(AccessibilitySettings.NpcSounds)),
                Switch(nameof(AccessibilitySettings.PlayerSounds)),
            }),
            new("items", new[]
            {
                Choice(nameof(AccessibilitySettings.LootSounds), LootFilter.Levels),
                Choice(nameof(AccessibilitySettings.QuietItemsInFights), FightState.Modes),
                Switch(nameof(AccessibilitySettings.AnnouncePickups)),
                Switch(nameof(AccessibilitySettings.AnnounceHand)),
                Switch(nameof(AccessibilitySettings.AnnounceMoves)),
                Switch(nameof(AccessibilitySettings.ReadTooltipsOnFocus)),
            }),
            new("chat", new[]
            {
                Switch(nameof(AccessibilitySettings.AnnounceChat)),
                Switch(nameof(AccessibilitySettings.AnnouncePlayerChat)),
            }),
            new("speech", new[]
            {
                Choice(nameof(AccessibilitySettings.SpeechStatusBegins), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechStatusEnds), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechLifeAndMana), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechCharges), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechCombo), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechBossLife), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechEnemyNames), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechQuest), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechPlaces), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechGroundItems), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechArrivals), SpeechPriority.Levels),
            }),
            new(VolumesGroup, new[]
            {
                Volume(nameof(AccessibilitySettings.SoundVolume)),
                Volume(nameof(AccessibilitySettings.RadarVolume)),
                Volume(nameof(AccessibilitySettings.EnemyVolume)),
                Volume(nameof(AccessibilitySettings.DodgeVolume)),
                Volume(nameof(AccessibilitySettings.BeaconVolume)),
                Volume(nameof(AccessibilitySettings.ObjectVolume)),
            }),
            new("general", new[]
            {
                Choice(nameof(AccessibilitySettings.Language), new[] { "auto" }.Concat(Localization.Languages).ToArray()),
                Switch(nameof(AccessibilitySettings.ReadPlayer1)),
                Switch(nameof(AccessibilitySettings.ReadPlayer2)),
                Switch(nameof(AccessibilitySettings.CheckForUpdates)),
            }),
        };

        /// <summary>Highest volume, percent; above 100 a cue is driven into a soft limit rather than only clipped (AudioCueService).</summary>
        public const int MaxVolume = 300;

        /// <summary>A sound's own volume when the player has not changed it, percent.</summary>
        private const int DefaultLevel = 100;

        private const int VolumeStep = 25;

        private const string VolumesGroup = "volumes";
        private const string SoundLevelsGroup = "sound_levels";

        private readonly Group[] groups;
        private readonly SoundGlossary? glossary;
        private readonly SettingsCursor cursor;

        /// <param name="glossary">
        ///     Its sounds become the last group (name and meaning, left or right plays it) and, after the volumes, each
        ///     sound's own volume (left or right changes it and plays it).
        /// </param>
        public SettingsMenu(SoundGlossary? glossary = null)
        {
            this.glossary = glossary;
            var all = new List<Group> { new("help", HelpTopics.Select(topic => new Item(topic, Kind.Help, 0, 0, 0, Array.Empty<string>())).ToArray()) };
            all.AddRange(SettingGroups);
            if (glossary?.Keys is { Count: > 0 } keys)
            {
                var levels = keys.Select(key => new Item(key, Kind.SoundLevel, 0, MaxVolume, VolumeStep, Array.Empty<string>())).ToArray();
                all.Insert(all.FindIndex(group => group.Name == VolumesGroup) + 1, new(SoundLevelsGroup, levels));
                all.Add(new("glossary", keys.Select(key => new Item(key, Kind.Sound, 0, 0, 0, Array.Empty<string>())).ToArray()));
            }

            this.groups = all.ToArray();
            this.cursor = new SettingsCursor(this.groups.Select(group => group.Items.Length).ToArray());
        }

        public bool IsOpen { get; private set; }

        /// <summary>Inside a group; at the top level the list holds only the groups.</summary>
        public bool InGroup => this.cursor.InGroup;

        private Group Stood => this.groups[this.cursor.Group];

        private Item Current => this.Stood.Items[this.cursor.Entry];

        /// <summary>Opens where it was left, naming the group when inside one; returns what to say.</summary>
        public string Open(AccessibilitySettings settings)
        {
            this.IsOpen = true;
            var line = this.InGroup ? Announcements.InContext(GroupName(this.Stood), this.Describe(settings)) : this.Describe(settings);
            return Announcements.InContext(Announcements.SettingsMenuTitle, line);
        }

        public string Close()
        {
            this.IsOpen = false;
            return Announcements.SettingsMenuClosed;
        }

        /// <summary>Next / previous group, or entry within a group (wraps).</summary>
        public string Move(AccessibilitySettings settings, int step)
        {
            this.cursor.Move(step);
            return this.Describe(settings);
        }

        /// <summary>Opens the group stood on, at the entry it was left on.</summary>
        public string Enter(AccessibilitySettings settings)
        {
            this.cursor.Enter();
            return this.Describe(settings);
        }

        /// <summary>Back to the groups, on the one just left.</summary>
        public string Back(AccessibilitySettings settings)
        {
            this.cursor.Back();
            return this.Describe(settings);
        }

        /// <summary>Toggles a switch or steps a value; returns the new value only. Within a group only.</summary>
        public string Change(AccessibilitySettings settings, int step)
        {
            var item = this.Current;
            if (item.Kind == Kind.Sound)
            {
                return string.Empty; // played by PlaySound
            }

            if (item.Kind == Kind.Help)
            {
                return Announcements.HelpText(item.Name); // left or right reads the topic again
            }

            if (item.Kind == Kind.SoundLevel)
            {
                var level = Math.Clamp(Level(settings, item.Name) + (step * item.Step), item.Min, item.Max);
                if (level == DefaultLevel)
                {
                    settings.SoundLevels.Remove(item.Name);
                }
                else
                {
                    settings.SoundLevels[item.Name] = level;
                }

                return this.Value(settings, item);
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
                    field.SetValue(settings, item.Choices[SettingsCursor.Wrap(current + step, item.Choices.Length)]);
                    break;
            }

            return this.Value(settings, item);
        }

        /// <summary>
        ///     Plays the sound the entry stands on (glossary or its own volume) as loud as the game now would: its group
        ///     volume, its own, the master. Called after a change is applied, so a new level is heard at once.
        /// </summary>
        public void PlaySound(AccessibilitySettings settings)
        {
            if (this.IsOpen && this.InGroup && this.Current.Kind is Kind.Sound or Kind.SoundLevel)
            {
                var name = this.Current.Name;
                this.glossary?.Play(name, SoundGlossary.VolumeSetting(name) is { } volume ? (int)Field(volume).GetValue(settings)! / 100f : 1f);
            }
        }

        private static string GroupName(Group group) => Announcements.SettingsGroupName(group.Name);

        private static int Level(AccessibilitySettings settings, string sound) =>
            (settings.SoundLevels ??= new()).TryGetValue(sound, out var level) ? level : DefaultLevel;

        private string Describe(AccessibilitySettings settings)
        {
            var (position, count) = this.cursor.Place;
            if (!this.InGroup)
            {
                return Announcements.SettingLine(GroupName(this.Stood), Announcements.Submenu, position, count);
            }

            var item = this.Current;
            return item.Kind == Kind.Help
                ? Announcements.SettingLine(Announcements.HelpTitle(item.Name), Announcements.HelpText(item.Name), position, count)
                : item.Kind == Kind.Sound
                ? Announcements.SettingLine(Announcements.SoundName(item.Name), SoundLine(item.Name), position, count)
                : item.Kind == Kind.SoundLevel
                ? Announcements.SettingLine(Announcements.SoundName(item.Name), this.Value(settings, item), position, count)
                : Announcements.SettingLine(Announcements.SettingName(item.Name), this.Value(settings, item), position, count);
        }

        /// <summary>A sound's meaning and the volume that sets it ("…, Volumen de enemigos"), so the player knows which to raise.</summary>
        private static string SoundLine(string key) => SoundGlossary.VolumeSetting(key) is { } volume
            ? $"{Announcements.SoundMeaning(key)}, {Announcements.SettingName(volume)}"
            : Announcements.SoundMeaning(key);

        private string Value(AccessibilitySettings settings, Item item)
        {
            if (item.Kind == Kind.SoundLevel)
            {
                return Announcements.Percent(Level(settings, item.Name));
            }

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

            /// <summary>One sound's own volume (settings SoundLevels by glossary key), a percentage of its group volume.</summary>
            SoundLevel,

            /// <summary>A controls help topic: read, changes nothing.</summary>
            Help,
        }

        private sealed record Item(string Name, Kind Kind, int Min, int Max, int Step, string[] Choices);

        /// <summary>A submenu: its key names it (<see cref="Announcements.SettingsGroupName"/>).</summary>
        private sealed record Group(string Name, Item[] Items);
    }
}
