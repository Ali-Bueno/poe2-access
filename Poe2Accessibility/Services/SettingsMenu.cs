namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
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
        private static readonly Item[] SettingItems =
        {
            Switch(nameof(AccessibilitySettings.AnnounceTarget)),
            Switch(nameof(AccessibilitySettings.AnnounceLowLife)),
            Switch(nameof(AccessibilitySettings.AnnounceLowMana)),
            Switch(nameof(AccessibilitySettings.AnnounceBossLife)),
            Switch(nameof(AccessibilitySettings.StatusAlerts)),
            Switch(nameof(AccessibilitySettings.AnnounceNearby)),
            Switch(nameof(AccessibilitySettings.AnnounceChat)),
            Switch(nameof(AccessibilitySettings.ReadTooltipsOnFocus)),
            Switch(nameof(AccessibilitySettings.AnnouncePickups)),
            Switch(nameof(AccessibilitySettings.AnnounceDiscoveries)),
            Switch(nameof(AccessibilitySettings.RoutePrompts)),
            Switch(nameof(AccessibilitySettings.AnnounceLevelUp)),
            Switch(nameof(AccessibilitySettings.Radar)),
            Switch(nameof(AccessibilitySettings.EnemySounds)),
            Switch(nameof(AccessibilitySettings.NpcSounds)),
            Switch(nameof(AccessibilitySettings.WallBump)),
            Switch(nameof(AccessibilitySettings.ObjectSounds)),
            Switch(nameof(AccessibilitySettings.DodgeWarnings)),
            Switch(nameof(AccessibilitySettings.CrosshairBeep)),
            Switch(nameof(AccessibilitySettings.LowLifeHeartbeat)),
            Number(nameof(AccessibilitySettings.LowLifeBelow), 5, 70, 5),
            Number(nameof(AccessibilitySettings.SoundVolume), 0, 200, 25),
            Number(nameof(AccessibilitySettings.RadarVolume), 0, 200, 25),
            Number(nameof(AccessibilitySettings.EnemyVolume), 0, 200, 25),
            Number(nameof(AccessibilitySettings.BeaconVolume), 0, 200, 25),
            Number(nameof(AccessibilitySettings.ObjectVolume), 0, 200, 25),
            Number(nameof(AccessibilitySettings.HeartbeatVolume), 0, 200, 25),
            Choice(nameof(AccessibilitySettings.Language), "auto", "es", "en"),
        };

        private readonly Item[] items;
        private readonly SoundGlossary? glossary;
        private int index;

        /// <param name="glossary">Its sounds become the list's last group: name and meaning, left or right plays it.</param>
        public SettingsMenu(SoundGlossary? glossary = null)
        {
            this.glossary = glossary;
            var all = new List<Item>(SettingItems);
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
                this.glossary?.Play(item.Name);
                return string.Empty;
            }

            var field = Field(item.Name);
            switch (item.Kind)
            {
                case Kind.Switch:
                    field.SetValue(settings, !(bool)field.GetValue(settings)!);
                    break;
                case Kind.Number:
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
            return item.Kind == Kind.Sound
                ? Announcements.SettingLine(Announcements.SoundName(item.Name), Announcements.SoundMeaning(item.Name), this.index + 1, this.items.Length)
                : Announcements.SettingLine(Announcements.SettingName(item.Name), this.Value(settings, item), this.index + 1, this.items.Length);
        }

        private string Value(AccessibilitySettings settings, Item item)
        {
            var value = Field(item.Name).GetValue(settings);
            return item.Kind switch
            {
                Kind.Switch => (bool)value! ? Announcements.On : Announcements.Off,
                Kind.Number => item.Max == 200 ? Announcements.Percent((int)value!) : $"{value}",
                _ => Announcements.LanguageName((string)value!),
            };
        }

        private static FieldInfo Field(string name) =>
            typeof(AccessibilitySettings).GetField(name) ?? throw new InvalidOperationException($"No setting {name}");

        private static Item Switch(string name) => new(name, Kind.Switch, 0, 0, 0, Array.Empty<string>());

        private static Item Number(string name, int min, int max, int step) => new(name, Kind.Number, min, max, step, Array.Empty<string>());

        private static Item Choice(string name, params string[] choices) => new(name, Kind.Choice, 0, 0, 0, choices);

        private enum Kind
        {
            Switch,
            Number,
            Choice,

            /// <summary>A glossary entry: plays a sound, changes nothing.</summary>
            Sound,
        }

        private sealed record Item(string Name, Kind Kind, int Min, int Max, int Step, string[] Choices);
    }
}
