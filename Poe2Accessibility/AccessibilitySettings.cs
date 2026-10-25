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

        /// <summary>Language of the mod's own phrases: "auto" follows the game's text language, or a code of <see cref="Localization.Languages"/>.</summary>
        public string Language = "auto";

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

        /// <summary>Say when mana falls below half, a quarter and a tenth.</summary>
        public bool AnnounceLowMana = true;

        /// <summary>Say a boss's life as it falls past 75, 50, 40, 30, 20, 10 and 5 percent, and its defeat.</summary>
        public bool AnnounceBossLife = true;

        /// <summary>A rising whoosh on the side of a rare or unique monster that starts an attack: time to dodge.</summary>
        public bool DodgeWarnings = true;

        /// <summary>Name the monster, NPC or object the right stick aims at.</summary>
        public bool AnnounceTarget = true;

        /// <summary>Read a magic or rare monster's modifiers after its name when it is aimed at (D2R enchantments).</summary>
        public bool AnnounceMonsterMods = true;

        /// <summary>Name what you walk up to or stand on: checkpoints, NPCs, the stash, chests, items, exits.</summary>
        public bool AnnounceNearby = true;

        /// <summary>Name the places of the map you walk into and the props in reach: "bridge", "stairs", "cart", "well".</summary>
        public bool AnnouncePlaces = true;

        /// <summary>Say what A uses (the label the game marks with the button) whenever it changes: "Stash, A".</summary>
        public bool AnnounceInteractTarget = true;

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

        /// <summary>The radar's wall cue knocks hollow when the wall is a small obstacle the player can step round.</summary>
        public bool SmallObstacles = true;

        // Speech priority (D2R): per kind of news, "waits" (its turn), "urgent" (cuts in when urgent), "cuts" (cuts in),
        // or "holds" (cuts in and later news queues behind it for a moment).

        /// <summary>A harmful state begins: delivery level.</summary>
        public string SpeechStatusBegins = "holds";

        /// <summary>A harmful state ends: delivery level.</summary>
        public string SpeechStatusEnds = "cuts";

        /// <summary>Low life and mana: delivery level.</summary>
        public string SpeechLifeAndMana = "urgent";

        /// <summary>Boss life: delivery level.</summary>
        public string SpeechBossLife = "urgent";

        /// <summary>Names of what the aim lands on: delivery level.</summary>
        public string SpeechEnemyNames = "urgent";

        /// <summary>New objectives and tutorial lines: delivery level.</summary>
        public string SpeechQuest = "waits";

        /// <summary>Areas, discoveries and things nearby: delivery level.</summary>
        public string SpeechPlaces = "urgent";

        /// <summary>Picked-up items: delivery level.</summary>
        public string SpeechGroundItems = "waits";

        /// <summary>Arriving with the guide: delivery level.</summary>
        public string SpeechArrivals = "holds";

        /// <summary>Say what the cursor takes in hand in the inventory and when it lets go (D2R item in hand).</summary>
        public bool AnnounceHand = true;

        /// <summary>Say what is equipped or unequipped, and what arrives in or leaves the inventory in a panel (D2R arrivals).</summary>
        public bool AnnounceMoves = true;

        /// <summary>Reach ping (D2R): two rising notes when an enemy in sight comes within melee reach.</summary>
        public bool MeleeReach = true;

        /// <summary>Gap between your body and the enemy's at which the reach ping fires, world units (~400 per second of walking).</summary>
        public int MeleeReachRange = 40;

        /// <summary>Wall tones (D2R): a soft noise band per screen direction that swells as a wall closes in. Off by default.</summary>
        public bool WallTones = false;

        /// <summary>How far ahead a wall starts to sound for the wall tones, in world units (~400 per second of walking).</summary>
        public int WallToneRange = 600;

        /// <summary>The game's own sounds for the nearest item, chest or shrine, exit or portal, and waypoint or checkpoint on screen.</summary>
        public bool ObjectSounds = true;

        /// <summary>While the guide beacon leads, name each turn as you reach it: "Ahora arriba a la derecha, 12".</summary>
        public bool RoutePrompts = true;

        /// <summary>Name the first sighting of an exit, waypoint, checkpoint or boss in an area, and a barrier that opens.</summary>
        public bool AnnounceDiscoveries = true;

        /// <summary>Name the enemies coming into view, a pack in one line: "Ahogado, 3, derecha".</summary>
        public bool AnnounceEnemies = true;

        /// <summary>Say what arrives in the inventory while playing: "Recogido: Traje de ermitaño".</summary>
        public bool AnnouncePickups = true;

        /// <summary>Two short blips when the game's target lands on a living enemy (D2R crosshair).</summary>
        public bool CrosshairBeep = true;

        /// <summary>Life warnings at or under this percent cut in when urgent (the game plays its own low-life heartbeat).</summary>
        public int LowLifeBelow = 25;

        /// <summary>Say when a harmful state begins and ends: frozen, chilled, shocked, ignited, poisoned, bleeding, stunned, slowed, cursed.</summary>
        public bool StatusAlerts = true;

        /// <summary>Volume of all the mod's sounds, percent (0-200).</summary>
        public int SoundVolume = 100;

        /// <summary>Radar and enemy sound volumes, percent of their designed level (0-200), under SoundVolume.</summary>
        public int RadarVolume = 100;
        public int EnemyVolume = 100;

        /// <summary>Guide beacon volume (hold L3, press the right stick on a scanner choice), percent (0-200).</summary>
        public int BeaconVolume = 100;

        /// <summary>
        ///     Which items on the ground sound and are named on approach (D2R loot filter): "all", "magic" (magic or
        ///     better) or "valuable" (currency, gold, gems, quest items, rares and uniques). The scanner lists them all.
        /// </summary>
        public string LootSounds = Services.LootFilter.All;

        /// <summary>Volume of the object sounds (game sounds for loot, chests, exits, waypoints), percent (0-200).</summary>
        public int ObjectVolume = 70;

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
