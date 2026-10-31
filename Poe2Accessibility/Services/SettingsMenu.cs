namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    ///     The mod's settings, spoken (D2R in-game settings list): opened by holding L3 for a second and a half in gameplay.
    ///     The top level lists the main submenus; left or right opens the one stood on (Combat and Sound hold submenus of
    ///     their own), a tap on L3 goes back one level, and a tap at the top or a double tap anywhere closes it and saves to
    ///     config/settings.txt. The right stick moves (up/down) and changes a value (left/right) with no need to hold L3;
    ///     switches toggle, percentages move in quarters, every list wraps. Fields are reached by name, so the tree below is
    ///     the only place to touch when a setting is added.
    /// </summary>
    public sealed class SettingsMenu
    {
        /// <summary>The list opens on the controls help (D2R onboarding): one topic per entry, read in full.</summary>
        private static readonly string[] HelpTopics = { "help_l3", "help_scanner", "help_guide", "help_menus", "help_settings" };

        /// <summary>The submenus between the help and Sound; Sound is built from the sounds the glossary holds.</summary>
        private static readonly Entry[] SettingMenus =
        {
            Menu("character",
                Switch(nameof(AccessibilitySettings.AnnounceLowLife)),
                Number(nameof(AccessibilitySettings.LowLifeBelow), 5, 70, 5),
                Switch(nameof(AccessibilitySettings.AnnounceLowMana)),
                Switch(nameof(AccessibilitySettings.AnnounceEnergyShield)),
                Switch(nameof(AccessibilitySettings.AnnounceFlasks)),
                Switch(nameof(AccessibilitySettings.StatusAlerts)),
                Switch(nameof(AccessibilitySettings.AnnounceLevelUp))),
            Menu("combat",
                Menu("enemies",
                    Switch(nameof(AccessibilitySettings.AnnounceEnemies)),
                    Switch(nameof(AccessibilitySettings.EnemyDirections)),
                    Switch(nameof(AccessibilitySettings.AnnounceTarget)),
                    Switch(nameof(AccessibilitySettings.AnnounceMonsterMods)),
                    Switch(nameof(AccessibilitySettings.EnemySounds)),
                    Switch(nameof(AccessibilitySettings.MeleeReach)),
                    Number(nameof(AccessibilitySettings.MeleeReachRange), 0, 200, 10),
                    Switch(nameof(AccessibilitySettings.CrosshairBeep))),
                Menu("bosses",
                    Switch(nameof(AccessibilitySettings.AnnounceBossLife)),
                    Switch(nameof(AccessibilitySettings.BossWeakPoint)),
                    Switch(nameof(AccessibilitySettings.EnemyStateCues)),
                    Switch(nameof(AccessibilitySettings.PrimedStunCues)),
                    Choice(nameof(AccessibilitySettings.CullCue), CullThreshold.Modes)),
                Menu("defence",
                    Switch(nameof(AccessibilitySettings.DodgeWarnings)),
                    Switch(nameof(AccessibilitySettings.UnblockableCues)),
                    Switch(nameof(AccessibilitySettings.ParryCues)),
                    Number(nameof(AccessibilitySettings.ParryLeadMs), 0, 600, 50),
                    Switch(nameof(AccessibilitySettings.ShieldSounds)),
                    Switch(nameof(AccessibilitySettings.AnnounceStunBar)),
                    Switch(nameof(AccessibilitySettings.HazardAlarm))),
                Menu("skills",
                    Switch(nameof(AccessibilitySettings.ChargeSpeech)),
                    Switch(nameof(AccessibilitySettings.ChargeSounds)),
                    Switch(nameof(AccessibilitySettings.ComboSpeech)),
                    Switch(nameof(AccessibilitySettings.ComboSounds)),
                    Switch(nameof(AccessibilitySettings.DeployedCues))),
                Menu("fight_quiet",
                    Choice(nameof(AccessibilitySettings.QuietItemsInFights), FightState.Modes),
                    Choice(nameof(AccessibilitySettings.QuietChatInFights), FightState.Modes))),
            Menu("navigation",
                Switch(nameof(AccessibilitySettings.Radar)),
                Switch(nameof(AccessibilitySettings.SmallObstacles)),
                Switch(nameof(AccessibilitySettings.UnexploredMark)),
                Switch(nameof(AccessibilitySettings.UnexploredLoop)),
                Switch(nameof(AccessibilitySettings.WallTones)),
                Number(nameof(AccessibilitySettings.WallToneRange), 200, 1600, 100),
                Switch(nameof(AccessibilitySettings.WallBump)),
                Switch(nameof(AccessibilitySettings.RoutePrompts)),
                Switch(nameof(AccessibilitySettings.AnnouncePlaces)),
                Switch(nameof(AccessibilitySettings.AnnounceDiscoveries))),
            Menu("surroundings",
                Switch(nameof(AccessibilitySettings.AnnounceNearby)),
                Switch(nameof(AccessibilitySettings.AnnounceInteractTarget)),
                Switch(nameof(AccessibilitySettings.ObjectSounds)),
                Switch(nameof(AccessibilitySettings.NpcSounds)),
                Switch(nameof(AccessibilitySettings.PlayerSounds))),
            Menu("items",
                Choice(nameof(AccessibilitySettings.LootSounds), LootFilter.Levels),
                Switch(nameof(AccessibilitySettings.AnnouncePickups)),
                Switch(nameof(AccessibilitySettings.AnnounceHand)),
                Switch(nameof(AccessibilitySettings.AnnounceMoves)),
                Switch(nameof(AccessibilitySettings.ReadTooltipsOnFocus))),
            Menu("chat",
                Switch(nameof(AccessibilitySettings.AnnounceChat)),
                Switch(nameof(AccessibilitySettings.AnnouncePlayerChat))),
            Menu("speech",
                Choice(nameof(AccessibilitySettings.SpeechLifeAndMana), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechStatusBegins), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechStatusEnds), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechCharges), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechCombo), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechEnemyNames), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechBossLife), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechPlaces), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechArrivals), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechQuest), SpeechPriority.Levels),
                Choice(nameof(AccessibilitySettings.SpeechGroundItems), SpeechPriority.Levels)),
        };

        private static readonly Entry GeneralMenu = Menu("general",
            Choice(nameof(AccessibilitySettings.Language), new[] { "auto" }.Concat(Localization.Languages).ToArray()),
            Switch(nameof(AccessibilitySettings.ReadPlayer1)),
            Switch(nameof(AccessibilitySettings.ReadPlayer2)),
            Switch(nameof(AccessibilitySettings.CheckForUpdates)));

        /// <summary>
        ///     Sound's submenus, one per volume a sound plays under (<see cref="SoundGlossary.VolumeSetting"/>): that volume
        ///     first, then each of its sounds' own. Null holds the sounds only the master volume sets.
        /// </summary>
        private static readonly (string Menu, string? Volume)[] SoundKinds =
        {
            ("sound_enemies", nameof(AccessibilitySettings.EnemyVolume)),
            ("sound_dodge", nameof(AccessibilitySettings.DodgeVolume)),
            ("sound_radar", nameof(AccessibilitySettings.RadarVolume)),
            ("sound_beacon", nameof(AccessibilitySettings.BeaconVolume)),
            ("sound_objects", nameof(AccessibilitySettings.ObjectVolume)),
            ("sound_charges", null),
        };

        /// <summary>Highest volume, percent; above 100 a cue is driven into a soft limit rather than only clipped (AudioCueService).</summary>
        public const int MaxVolume = 300;

        /// <summary>A sound's own volume when the player has not changed it, percent.</summary>
        private const int DefaultLevel = 100;

        private const int VolumeStep = 25;

        private readonly Entry root;
        private readonly SoundGlossary? glossary;
        private readonly SettingsCursor cursor;

        /// <param name="glossary">
        ///     Its sounds fill Sound: a submenu per volume with each sound's own volume (left or right changes it and plays
        ///     it), and the glossary by the same submenus (name and meaning, left or right plays it).
        /// </param>
        public SettingsMenu(SoundGlossary? glossary = null)
        {
            this.glossary = glossary;
            var top = new List<Entry> { Menu("help", HelpTopics.Select(topic => new Entry(topic, new Item(topic, Kind.Help, 0, 0, 0, Array.Empty<string>()))).ToArray()) };
            top.AddRange(SettingMenus);
            top.Add(SoundMenu(glossary?.Keys ?? Array.Empty<string>()));
            top.Add(GeneralMenu);
            this.root = Menu("root", top.ToArray());
            this.cursor = new SettingsCursor(NodeOf(this.root));
        }

        public bool IsOpen { get; private set; }

        /// <summary>Inside a submenu; at the top level the list holds only the main submenus.</summary>
        public bool InGroup => this.cursor.InGroup;

        /// <summary>The entry stood on opens a submenu (left or right enters it).</summary>
        public bool OnSubmenu => this.cursor.OnSubmenu;

        private Entry Current => this.Along().Last();

        /// <summary>Opens where it was left, naming the submenus it stands in; returns what to say.</summary>
        public string Open(AccessibilitySettings settings)
        {
            this.IsOpen = true;
            var line = this.Describe(settings);
            foreach (var menu in this.Along().SkipLast(1).Reverse())
            {
                line = Announcements.InContext(GroupName(menu), line);
            }

            return Announcements.InContext(Announcements.SettingsMenuTitle, line);
        }

        public string Close()
        {
            this.IsOpen = false;
            return Announcements.SettingsMenuClosed;
        }

        /// <summary>Next / previous entry of the list stood in (wraps).</summary>
        public string Move(AccessibilitySettings settings, int step)
        {
            this.cursor.Move(step);
            return this.Describe(settings);
        }

        /// <summary>Opens the submenu stood on, at the entry it was left on.</summary>
        public string Enter(AccessibilitySettings settings)
        {
            this.cursor.Enter();
            return this.Describe(settings);
        }

        /// <summary>Back one level, on the submenu just left.</summary>
        public string Back(AccessibilitySettings settings)
        {
            this.cursor.Back();
            return this.Describe(settings);
        }

        /// <summary>Toggles a switch or steps a value; returns the new value only. On a setting only.</summary>
        public string Change(AccessibilitySettings settings, int step)
        {
            if (this.Current.Setting is not { } item || item.Kind == Kind.Sound)
            {
                return string.Empty; // a sound is played by PlaySound
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
            if (this.IsOpen && this.Current.Setting is { Kind: Kind.Sound or Kind.SoundLevel } item)
            {
                this.glossary?.Play(item.Name, SoundGlossary.VolumeSetting(item.Name) is { } volume ? (int)Field(volume).GetValue(settings)! / 100f : 1f);
            }
        }

        /// <summary>
        ///     Sound: the master volume, a submenu per volume (that volume, then each of its sounds' own) and the glossary
        ///     split the same way; a kind with no sound in the glossary has no submenu.
        /// </summary>
        private static Entry SoundMenu(IReadOnlyList<string> keys)
        {
            var entries = new List<Entry> { Volume(nameof(AccessibilitySettings.SoundVolume)) };
            var glossary = new List<Entry>();
            foreach (var (menu, volume) in SoundKinds)
            {
                var sounds = keys.Where(key => SoundGlossary.VolumeSetting(key) == volume).ToList();
                var levels = sounds.Select(key => new Entry(key, new Item(key, Kind.SoundLevel, 0, MaxVolume, VolumeStep, Array.Empty<string>())));
                var own = volume == null ? levels : levels.Prepend(Volume(volume));
                if (own.Any())
                {
                    entries.Add(Menu(menu, own.ToArray()));
                }

                if (sounds.Count > 0)
                {
                    glossary.Add(Menu(menu, sounds.Select(key => new Entry(key, new Item(key, Kind.Sound, 0, 0, 0, Array.Empty<string>()))).ToArray()));
                }
            }

            if (glossary.Count > 0)
            {
                entries.Add(Menu("glossary", glossary.ToArray()));
            }

            return Menu("sound", entries.ToArray());
        }

        private static SettingsCursor.Node NodeOf(Entry entry) =>
            entry.Entries.Length == 0 ? SettingsCursor.Node.Setting : new SettingsCursor.Node(entry.Entries.Select(NodeOf).ToArray());

        /// <summary>The entries from the top-level one down to the one stood on.</summary>
        private IEnumerable<Entry> Along()
        {
            var list = this.root;
            foreach (var index in this.cursor.Path)
            {
                list = list.Entries[index];
                yield return list;
            }
        }

        private static string GroupName(Entry menu) => Announcements.SettingsGroupName(menu.Name);

        private static int Level(AccessibilitySettings settings, string sound) =>
            (settings.SoundLevels ??= new()).TryGetValue(sound, out var level) ? level : DefaultLevel;

        private string Describe(AccessibilitySettings settings)
        {
            var (position, count) = this.cursor.Place;
            var entry = this.Current;
            if (entry.Setting is not { } item)
            {
                return Announcements.SettingLine(GroupName(entry), Announcements.Submenu, position, count);
            }

            return item.Kind switch
            {
                Kind.Help => Announcements.SettingLine(Announcements.HelpTitle(item.Name), Announcements.HelpText(item.Name), position, count),
                Kind.Sound => Announcements.SettingLine(Announcements.SoundName(item.Name), Announcements.SoundMeaning(item.Name), position, count),
                Kind.SoundLevel => Announcements.SettingLine(Announcements.SoundName(item.Name), this.Value(settings, item), position, count),
                _ => Announcements.SettingLine(Announcements.SettingName(item.Name), this.Value(settings, item), position, count),
            };
        }

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

        private static Entry Menu(string name, params Entry[] entries) => new(name, null, entries);

        private static Entry Switch(string name) => new(name, new Item(name, Kind.Switch, 0, 0, 0, Array.Empty<string>()));

        private static Entry Number(string name, int min, int max, int step) => new(name, new Item(name, Kind.Number, min, max, step, Array.Empty<string>()));

        private static Entry Volume(string name) => new(name, new Item(name, Kind.Volume, 0, MaxVolume, VolumeStep, Array.Empty<string>()));

        private static Entry Choice(string name, params string[] choices) => new(name, new Item(name, Kind.Choice, 0, 0, 0, choices));

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

        /// <summary>A menu entry: a setting, or a submenu named by its key (<see cref="Announcements.SettingsGroupName"/>).</summary>
        private sealed record Entry(string Name, Item? Setting, params Entry[] Entries);
    }
}
