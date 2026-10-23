namespace Poe2Accessibility
{
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Plugin;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    ///     Persisted plugin settings (config/settings.txt, JSON). Hotkeys are stored by key name so a blind
    ///     user can edit the file directly; the ImGui settings window is not screen-reader accessible.
    ///     The file is re-read automatically when it changes on disk.
    ///     Defaults avoid GameHelper2's own keys (F9 disables all plugin rendering, F10 element finder, F12 main menu).
    /// </summary>
    public sealed class AccessibilitySettings : IPSettings
    {
        public bool SpeechEnabled = true;
        public bool AnnounceGameState = true;
        public bool AnnounceArea = true;
        public bool VerboseLogging = false;

        [JsonConverter(typeof(StringEnumConverter))]
        public VK DumpUiTreeHotKey = VK.F6;

        [JsonConverter(typeof(StringEnumConverter))]
        public VK RepeatLastHotKey = VK.F7;

        [JsonConverter(typeof(StringEnumConverter))]
        public VK SpeakStatusHotKey = VK.F8;

        /// <summary>Read texts that appear on screen after the focus moves (tooltips, descriptions).</summary>
        public bool AnnounceAppearingText = true;

        /// <summary>Read new chat lines from NPCs, your character, the system, whispers and party (not global or trade).</summary>
        public bool AnnounceChat = true;

        /// <summary>Read the whole tooltip when the focus lands on something (items, gems, stats); off = name only, tooltip on request.</summary>
        public bool ReadTooltipsOnFocus = true;

        /// <summary>L3 is the mod button: tap, double tap and hold read lines, details, hints and the screen.</summary>
        public bool ControllerCommands = true;

        /// <summary>Say when life falls below half and below a quarter.</summary>
        public bool AnnounceLowLife = true;

        /// <summary>Name what you walk up to or stand on: checkpoints, NPCs, the stash, chests, items, exits.</summary>
        public bool AnnounceNearby = true;

        /// <summary>A short growl for each enemy on screen: side by stereo, below you by a lower pitch, nearer by a faster beat.</summary>
        public bool EnemySounds = true;

        /// <summary>A light wooden tap for the nearest NPC or other player on screen, faster as they get closer.</summary>
        public bool NpcSounds = true;

        /// <summary>A thud when you push the stick into a wall and stop.</summary>
        public bool WallBump = true;

        /// <summary>Say "Level 3" when the character levels up.</summary>
        public bool AnnounceLevelUp = true;

        /// <summary>Navigation radar: a cue when the way up, left, right or down on screen changes (tin exit, breath open floor, tock wall).</summary>
        public bool Radar = true;

        /// <summary>Volume of all the mod's sounds, percent (0-200).</summary>
        public int SoundVolume = 100;

        /// <summary>Radar and enemy sound volumes, percent of their designed level (0-200), under SoundVolume.</summary>
        public int RadarVolume = 100;
        public int EnemyVolume = 100;

        /// <summary>Research: dump the UI tree automatically once per screen (game state) after it settles, and log every focused widget's surroundings.</summary>
        public bool ResearchMode = true;

        /// <summary>Only dump UI subtrees whose visibility flag is set (their ancestors' too, implicitly).</summary>
        public bool UiDumpVisibleOnly = false;

        /// <summary>Hex address of a UI element to dump from instead of auto-discovered roots (research aid; empty = auto).</summary>
        public string UiDumpRootAddressOverride = string.Empty;

        /// <summary>Safety caps for the research dumper so a corrupt pointer can never hang the overlay.</summary>
        public int UiDumpMaxDepth = 40;
        public int UiDumpMaxNodes = 20000;
    }
}
