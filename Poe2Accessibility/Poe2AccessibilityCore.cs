namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader;
    using GameReader.Plugin;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>
    ///     GameHelper2 plugin entry point. Thin by design: it wires services together and forwards the
    ///     per-frame tick and hotkeys; all behaviour lives in the services.
    /// </summary>
    public sealed partial class Poe2AccessibilityCore : PCore<AccessibilitySettings>
    {
        private readonly HotkeyService hotkeys = new();
        private PluginLog? log;
        private SettingsStore<AccessibilitySettings>? settingsStore;
        private IAccessibilityOutputService output = new NullOutputService();
        private GameStateService? gameState;
        private GameProcessProbe? processProbe;
        private UiTreeDumper? dumper;
        private FocusTracker? focus;
        private ScreenWatcher? screen;
        private ScreenNarrator? narrator;
        private ChatWatcher? chat;
        private MapOverlayWatch? mapOverlay;
        private LongTextReader? longText;
        private EntityDump? entityDump;
        private TerrainDump? terrainDump;
        private ActorTrace? actorTrace;
        private StunTrace? stunTrace;
        private EntityCatalog? entities;
        private GamepadReader? pad;
        private ModCommands? commands;
        private L3Gestures gestures = new();

        /// <summary>How often the guide looks for the next Death Crystal: a disabled one leaves the list at the scanner's pace.</summary>
        private const int TimedGuideMs = 400;

        /// <summary>
        ///     A room's crystals span about 5400 and the floor's other rooms stand 20,000 or more away, in the same instance: past the room's
        ///     last the guide went on to one of theirs (log 2026-11-07 12:07).
        /// </summary>
        private const float TimedRoomWorld = 10000f;

        /// <summary>Routes searched to choose the next crystal: the most a room was seen to hold is 7.</summary>
        private const int MaxTimedRoutes = 8;

        private readonly TimedGuide timedGuide = new();
        private long nextTimedGuide;

        /// <summary>The walks to the next crystal's candidates being searched, and what the guide was on when they were asked for.</summary>
        private (System.Threading.Tasks.Task<float[]> Walks, System.Collections.Generic.List<Services.ScanEntry> Candidates, uint? Guided)? timedRanking;
        private AudioCueService? audio;

        private WorldLayoutWatch? worldLayout;
        private TradeOfferWatch? tradeOffers;
        private SearchHighlightWatch? searchHighlights;

        private EnemyCues? enemyCues;
        private Radar? radar;
        private LostHint? lostHint;
        private MapHintNarrator? mapHints;
        private Radar? partnerRadar;
        private LocalPlayers? localPlayers;
        private ProximityNarrator? nearby;
        private PlaceNarrator? places;
        private WallBump? wallBump;
        private WallTones? wallTones;
        private GamepadReader.PadState padState;
        private readonly InputSeat inputSeat = new();
        private readonly CoopDowns coopDowns = new();
        private IntPtr padManager;
        private readonly System.Collections.Generic.Dictionary<string, long> tickFaults = new();
        private readonly TickCosts tickCosts = new();
        private HostMemoryReader? memoryReader;

        /// <summary>A failing sound tick is logged once a minute, not every frame.</summary>
        private const int SoundErrorReportMs = 60000;

        /// <summary>Right stick deflection that counts as aiming (the target is named only then).</summary>
        private const float AimDeflection = 0.3f;

        private Beacon? beacon;
        private TargetNarrator? targets;
        private BossWatch? bosses;
        private readonly TrialDodgeScope trialScope = new();
        private WeakPointGuide? weakPoint;
        private BossArenaLog? arenaLog;
        private ShieldWatch? shield;
        private PerfectTimingWatch? perfectTiming;
        private HazardWatch? hazards;
        private EncounterReader? encounters;
        private TrapWatch? traps;
        private TrialDoorWatch? trialDoors;
        private ChaosTrialWatch? chaosTrial;
        private MapPinNarrator? mapPins;
        private MinionReport? minions;
        private CullWatch? culls;
        private DeployedWatch? deployed;
        private EnemyStateWatch? enemyStates;
        private ObjectSounds? objectSounds;
        private FightWatch? fights;
        private PlayerAlerts? alerts;
        private PlayerAlerts? partnerAlerts;
        private SkillDetailsDump? skillDump;
        private InventoryWatch? pickups;
        private InventoryWatch? partnerPickups;
        private InventoryReader? inventoryReader;
        private GoldWatch? gold;
        private BarrierWatch? barriers;
        private DiscoveryNarrator? discoveries;
        private EnemyNarrator? enemyNews;
        private DeathWatch? deaths;
        private AscendancyChoiceWatch? ascendancy;
        private ExperienceTable? experience;
        private FlaskWatch? flasks;
        private FlaskWatch? partnerFlasks;
        private EntityFields? entityFields;
        private FlagBits? flagBits;
        private readonly PadButtonCheck padCheck = new();

        /// <summary>Game sounds are cut to this length: repeated proximity cues must stay short.</summary>
        private const float GameSoundSeconds = 0.9f;

        /// <summary>No area goes past this level (endgame maps reach the 80s); a larger number is a broken read.</summary>
        private const int MaxAreaLevel = 100;

        /// <summary>The language setting last applied to <see cref="Localization"/>.</summary>
        private string? appliedLanguage;

        /// <summary>When each kind of monster attack lands, learnt from the blows taken (BossWatch).</summary>
        private string DodgeTimingsPath => Path.Join(this.DllDirectory, "config", "dodge-timings.txt");

        /// <summary>Timings measured in development, shipped so the first fights are timed too.</summary>
        private string DodgeTimingsSeedPath => Path.Join(this.DllDirectory, "catalog", "dodge-timings.txt");

        /// <summary>The HUD's skill bar this frame (zero outside gameplay or when not found).</summary>
        private IntPtr skillBar;
        private bool wasWatching;

        /// <summary>The skill bar's alert line sits two levels under it (skill_bar → container → label, 2026-10-24).</summary>
        private const int SkillBarTextDepth = 4;
        private ResearchSession? research;
        private UiElementReader? elements;
        private DevCommandFile? devCommands;
        private UiManagerLocator? locator;
        private CompatibilityReport? compatibility;
        private LayoutProbe? layoutProbe;
        private ControlFields? controls;
        private UiSemantics? semantics;

        /// <inheritdoc />
        public override void OnEnable(bool isGameOpened)
        {
            try
            {
                this.log = new PluginLog(Path.Join(this.DllDirectory, "logs"), this.Settings.VerboseLogging);
                this.settingsStore = new SettingsStore<AccessibilitySettings>(Path.Join(this.DllDirectory, "config", "settings.txt"), this.log);
                this.ApplySettings(this.settingsStore.Load(this.Settings));
                this.log.Info($"Enabled. Host library {Core.GetVersion()}, game open: {isGameOpened}");
                Localization.PhraseDirectory = Path.Join(this.DllDirectory, "catalog", "lang");
                this.appliedLanguage = this.Settings.Language;
                Localization.Detect(this.Settings.Language, message => this.log.Info(message));

                IAccessibilityOutputService? prism = this.Settings.SpeechEnabled
                    ? PrismOutputService.TryCreate(this.DllDirectory, this.log)
                    : null;
                this.output = prism ?? new NullOutputService();

                var memory = new HostMemoryReader(this.log);
                this.memoryReader = memory;
                var debugDirectory = Path.Join(this.DllDirectory, "debug");
                this.dumper = new UiTreeDumper(new UiTreeReader(memory), debugDirectory, this.log);
                this.devCommands = new DevCommandFile(debugDirectory, this.log);
                var elements = new UiElementReader(memory);
                this.elements = elements;
                var catalog = UiCatalog.Load(Path.Join(this.DllDirectory, "catalog", "ui-catalog.json"), this.log);
                PlaceWorldAreaColumns(DatColumns.Apply(Path.Join(this.DllDirectory, "catalog", "dat-columns.json"), this.log));
                this.experience = ExperienceTable.Load(Path.Join(this.DllDirectory, "catalog", "experience.json"), this.log);
                // What a node, a gem or a skill is, from the game's files: the rows in memory back these up (docs/research/file-catalogs.md).
                PassiveTreeCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "passive-tree.json"));
                GemCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "gems.json"));
                ItemBaseCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "item-bases.json"));
                ModTextCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "mod-texts.json"));
                ModStatCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "mod-stats.json"));
                // The build's gear lines name bases and modifiers through these; empty catalogs leave the author's own words.
                BuildGear.Bases = ItemBaseCatalog.Shared;
                BuildGear.Texts = ModTextCatalog.Shared;
                this.compatibility = new CompatibilityReport(this.log);
                // The columns still read at a compiled offset nothing judged: known as soon as the catalogs are, whatever is read first.
                DatColumns.Report(this.compatibility);
                this.worldLayout = new WorldLayoutWatch(this.compatibility, this.log, Path.Join(this.DllDirectory, "config", "game-build.txt"));
                this.layoutProbe = new LayoutProbe(memory, elements, this.compatibility);
                var probe = this.layoutProbe;
                this.controls = new ControlFields(this.compatibility, () => probe.TextShift);
                elements.Controls = this.controls;
                this.locator = new UiManagerLocator(memory, elements, this.log, this.layoutProbe, new InGameFields(memory, elements, this.compatibility));
                this.flagBits = new FlagBits(elements, this.compatibility);
                this.locator.Flags = this.flagBits;
                var inventory = new InventoryReader(memory, elements) { Controls = this.controls, Report = this.compatibility, Locals = new LocalInventories(memory) };
                this.inventoryReader = inventory;
                var comboBoxes = new ComboBoxReader(elements, this.log);
                this.semantics = new UiSemantics(memory, elements, catalog, comboBoxes, inventory)
                {
                    TooltipsMoved = (from, to) => this.compatibility?.Moved("tooltips", from, to),
                    PlayerStat = name => PlayerStats.Of(Seats.Asking, name),
                };
                this.semantics.TreeFields.Report = this.compatibility;
                this.semantics.TreeFields.Log = message => this.log?.Info(message);
                this.semantics.Trace = message => this.log.Debug(message);
                this.semantics.ItemModsLog = message => this.log.Info(message);
                this.semantics.ArtLog = message => this.log.Info(message);
                this.semantics.BuildPlanLog = message => this.log.Info(message);
                this.semantics.Fields = this.locator.Fields;
                this.semantics.CharacterName = () => PlayerAlerts.Name(Seats.Followed);
                this.semantics.IsPlayerNamed = OtherPlayerNamed;
                this.semantics.PassiveResumeFile = Path.Join(this.DllDirectory, "config", "passive-resume.txt");
                this.ApplyPassiveTree(this.Settings);
                this.focus = new FocusTracker(memory, elements, this.locator, this.semantics, this.log);
                this.narrator = new ScreenNarrator(this.output, this.log, layer => this.semantics?.SelectedTab(layer) ?? string.Empty);
                this.narrator.FocusLayer = () => this.focus?.CurrentLayer ?? IntPtr.Zero;
                this.narrator.FocusPending = () => this.focus?.ItemPending == true;
                this.narrator.PanelOverGame = () => this.PanelOverGame() != null;
                this.narrator.SinceConfirm = () => Environment.TickCount64 - this.lastConfirmAt;
                this.narrator.SinceDirection = () => Environment.TickCount64 - this.lastDirectionAt;
                this.narrator.FocusBehindAnswer = this.FocusOfFinderPress;
                this.focus.HoverOverFocus = (focused, hovered) =>
                    this.PreviewTree is var tree && tree != IntPtr.Zero && this.semantics?.NodeUnderPlannerButton(tree, hovered, focused) == true;
                this.focus.SlotTrace = this.Settings.ResearchMode ? line => this.log?.Info(line) : null;
                this.focus.FocusChanged += this.narrator.OnFocusChanged;
                this.focus.FocusChanged += node =>
                {
                    if (!node.IsPassiveNode)
                    {
                        this.semantics?.PassiveFocusLeft();
                    }
                    else
                    {
                        this.lastTreeNode = node.Address;
                    }

                    // Only what the manager's own focus slot holds teaches the navigable bit. The tracker also reports as the
                    // focus what is merely hovered (a passive tree's node, an atlas node), a shop's highlighted choice and a
                    // list's row with no focus, and those carry other bits: twelve tree nodes moved the bit to 0x12 on a
                    // healthy game (2026-11-09 and 2026-11-10, sessions that began in the tree).
                    if (this.InGame && !node.IsHovered && !node.IsPassiveNode && this.InFocusSlot(node.Address))
                    {
                        this.flagBits?.SampleFocus(node.Address, node.Role);
                    }
                };
                this.focus.ValueChanged += this.narrator.OnValueChanged;
                this.focus.TooltipChanged += this.narrator.OnTooltipChanged;
                this.pad = new GamepadReader(memory);
                this.entityDump = new EntityDump(elements, memory, this.log, debugDirectory);
                this.terrainDump = new TerrainDump(memory, this.log, debugDirectory);
                this.actorTrace = new ActorTrace(memory, this.log, debugDirectory);
                this.stunTrace = new StunTrace(memory, this.log, debugDirectory);
                ItemNames.Load(this.log, Path.Join(this.DllDirectory, "catalog", "item-names.json"));
                CurrencyIcons.Load(this.log, Path.Join(this.DllDirectory, "catalog", "currency-art.json"));
                EndgameCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "endgame.json"));
            ControllerGlyphs.Dropped = message => this.log.Info(message);
            InlineIcons.Load(this.log, Path.Join(this.DllDirectory, "catalog", "inline-icons.txt"));
                // Named tiles from GameHelper2's Radar table (kept in the catalog), then the mod's own additions.
                var landmarks = LandmarkTable.Load(
                    this.log,
                    Path.Join(this.DllDirectory, "catalog", "important_tgt_files.txt"),
                    Path.Join(this.DllDirectory, "catalog", "landmarks.json"));
                this.entityFields = new EntityFields(memory, elements) { Report = this.compatibility };
                this.entities = new EntityCatalog(memory, elements, inventory, landmarks, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log)
                {
                    Probe = this.layoutProbe,
                    Fields = this.entityFields,
                };
                var checkpointPlaces = new CheckpointPlaces(this.entities);
                this.semantics.MarkerPlaces = checkpointPlaces.Describe;
                this.audio = new AudioCueService(this.log);
                this.enemyCues = new EnemyCues(this.audio, memory, this.log);
                var radarTurn = new Radar.Turn();
                this.radar = new Radar(this.audio, () => Seats.Followed, RadarSynth.Voice.First, radarTurn, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log);
                this.localPlayers = new LocalPlayers(memory, this.log);
                Seats.Attach(this.localPlayers, () => this.Settings);
                this.partnerRadar = new Radar(this.audio, () => Seats.Partner, RadarSynth.Voice.Second, radarTurn, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log);
                this.nearby = new ProximityNarrator(this.entities, this.output);
                this.semantics.UsedObject = () => this.nearby?.UsedObject ?? string.Empty;
                this.places = new PlaceNarrator(PlaceCatalog.Load(Path.Join(this.DllDirectory, "catalog", "places.json"), this.log), memory, this.output, this.log)
                {
                    Report = this.compatibility,
                };
                this.entities.PlaceEntries = height => this.places?.Entries(height) ?? Array.Empty<ScanEntry>();
                this.wallBump = new WallBump(this.audio) { HeldStill = () => this.narrator?.RepeatLesson() };
                this.wallTones = new WallTones(this.audio);
                this.beacon = new Beacon(this.audio, this.entities, this.output, this.log);
                this.targets = new TargetNarrator(this.audio, memory, elements, this.output, this.log) { HudLayer = () => this.narrator?.HudLayer ?? IntPtr.Zero };
                this.narrator.IgnoredHudText = () => this.targets?.NameLabel ?? IntPtr.Zero;
                this.narrator.GoldCounter = () => this.gold?.Counter ?? IntPtr.Zero;
                this.narrator.GroundItemName = texts => this.entities?.ItemOfTooltip(texts);
                this.narrator.ItemTargetNamed = () => this.Settings.AnnounceInteractTarget && this.entities?.InteractTarget()?.Category == ScanCategory.Items;
                // Each local player's own news; while both are read, each line is named (Seats).
                var followedVoice = new SeatOutput(this.output, () => Seats.Partner == null ? string.Empty : PlayerAlerts.Name(Seats.Followed));
                var partnerVoice = new SeatOutput(this.output, () => PlayerAlerts.Name(Seats.Partner));
                Func<int> followedSeat = () => Seats.FollowsSecond ? 2 : 1;
                this.alerts = new PlayerAlerts(() => Seats.Followed, memory, elements, this.audio, followedVoice, this.log);
                this.alerts.Cooldowns.Report = this.compatibility;
                this.partnerAlerts = new PlayerAlerts(() => Seats.Partner, memory, elements, null, partnerVoice, this.log);
                var tradeOffers = new TradeOfferWatch(elements, inventory, line => this.output.Speak(line, false));
                this.tradeOffers = tradeOffers;
                this.searchHighlights = new SearchHighlightWatch(elements, inventory, line => this.output.Speak(line, false));
                this.pickups = new InventoryWatch(inventory, followedVoice, this.log) { Seat = followedSeat };
                this.partnerPickups = new InventoryWatch(inventory, partnerVoice, this.log) { Seat = () => 2 };
                this.flasks = new FlaskWatch(inventory, followedVoice) { Seat = followedSeat, Log = this.log };
                this.partnerFlasks = new FlaskWatch(inventory, partnerVoice) { Seat = () => 2, Log = this.log };
                this.gold = new GoldWatch(elements, this.output, this.log) { Fields = this.locator.Fields };
                this.pickups.Gold = () => this.gold?.ReadNow();
                this.focus.InGameHighlightRoot = this.ShopHighlightRoot;
                this.pickups.Engraving = () => this.narrator?.Engraving ?? (string.Empty, string.Empty);
                this.pickups.InTrade = () => tradeOffers.Open;
                this.pickups.SidePanel = () => this.semantics?.SidePanelTitle(
                    this.locator.Fields.Find(this.focus?.LayerContainer ?? IntPtr.Zero, InGameField.InventoryPanel)) ?? string.Empty;
                this.pickups.GroundItemIds = this.partnerPickups.GroundItemIds = () => this.entities?.GroundItemIds() ?? Array.Empty<uint>();
                this.pickups.GroundItemTitle = this.partnerPickups.GroundItemTitle = id => this.entities?.GroundItemName(id);
                this.barriers = new BarrierWatch(this.entities, this.output, this.log);
                this.entities.OpenedWayEntries = this.barriers.Ways.Update;
                this.barriers.Opened = way =>
                {
                    if (this.Settings.ObjectSounds)
                    {
                        this.objectSounds?.WayOpened(way);
                    }
                };
                GameIds.Load(Path.Join(this.DllDirectory, "catalog", "game-ids.txt"));
                this.HandDeadStat();
                var timings = DodgeTimings.Load(Path.Join(this.DllDirectory, "catalog", "hit-times.txt"), this.DodgeTimingsSeedPath, this.DodgeTimingsPath);
                this.log.Info($"Dodge timings: {timings.FileTimes} attacks timed by the game files");
                this.bosses = new BossWatch(this.audio, this.output, elements, this.log) { Timings = timings, TimingsPath = this.DodgeTimingsPath };
                this.bosses.Mechanics = this.BossMechanics(elements, this.log);
                this.bosses.Defeated = name => this.entities?.BossDefeated(name);
                this.weakPoint = new WeakPointGuide(this.audio, this.output, elements, memory, this.log) { Hiding = () => this.bosses?.Mechanics?.LeadTo };
                this.arenaLog = new BossArenaLog(this.log) { Fighting = () => this.bosses?.Mechanics?.Fighting };
                this.shield = new ShieldWatch(this.audio, this.output, memory, this.log);
                this.perfectTiming = new PerfectTimingWatch(this.audio, this.log) { Reader = () => this.bosses?.Reader };
                var skills = new PlayerSkills(memory, elements, this.log);
                this.skillDump = new SkillDetailsDump(memory, elements);
                this.bosses.CanParry = () => skills.Has(PlayerSkills.Parry);
                var hazardGround = HazardGround.Load(Path.Join(this.DllDirectory, "catalog", "hazards.txt"));
                this.log.Info($"Hazard ground: {hazardGround.Count} buffs from the game files");
                this.hazards = new HazardWatch(this.audio, hazardGround, this.log);
                var harmfulBuffs = HarmfulBuffs.Load(Path.Join(this.DllDirectory, "catalog", "buff-names.json"));
                this.log.Info($"Harmful buffs: {harmfulBuffs.Count} named by the game files");
                this.alerts.Statuses.Harmful = harmfulBuffs;
                this.bosses.Mechanics.NamedElsewhere = buff => harmfulBuffs.NameOf(buff, Localization.Language).Length > 0;
                var mechanics = ClassMechanicCatalog.Load(Path.Join(this.DllDirectory, "catalog", "class-mechanics.json"));
                this.log.Info($"Class mechanics: {mechanics.Resources.Count} resources, {mechanics.Limits.Count} limits, {mechanics.BuffCount} buffs, {mechanics.SkillCount} skills from the game files");
                this.alerts.Statuses.IsCovered = hazardGround.IsHazard;
                this.encounters = new EncounterReader(elements);
                this.minions = new MinionReport(elements);
                var cullSkills = CullSkills.Load(Path.Join(this.DllDirectory, "catalog", "cull-skills.txt"));
                this.log.Info($"Cull skills: {cullSkills.Count} skills from the game files");
                this.culls = new CullWatch(this.audio, this.output, elements)
                {
                    HasCullingSkill = () => cullSkills.AnyCulls(skills.Ids()),
                    Increases = () => (PlayerStats.Of(Seats.Followed, "culling_strike_threshold_+%") ?? 0,
                                       PlayerStats.Of(Seats.Followed, "culling_strike_threshold_+%_vs_rare_or_unique_monsters") ?? 0),
                };
                this.deployed = new DeployedWatch(this.audio, this.log);
                this.traps = new TrapWatch(this.audio, this.log);
                this.trialDoors = new TrialDoorWatch(this.entities, this.output, this.log);
                this.chaosTrial = new ChaosTrialWatch(this.output, this.log);
                this.enemyStates = new EnemyStateWatch(this.audio, this.output, elements, memory);
                var gameSounds = GameSounds.Load(Path.Join(this.DllDirectory, "sounds"), GameSoundSeconds, this.log);
                this.deployed.GameSound = gameSounds.Get;
                var drops = ItemDropSounds.Load(
                    Path.Join(this.DllDirectory, "catalog", "item-sounds.json"), Path.Join(this.DllDirectory, "sounds", "items"), GameSoundSeconds, this.log);
                var diabloSounds = GameSounds.LoadLoops(Path.Join(this.DllDirectory, "assets", ItemSoundSet.Folder), this.log);
                this.objectSounds = new ObjectSounds(this.audio, this.entities, gameSounds, drops, diabloSounds);
                this.fights = new FightWatch(memory);
                Func<bool> inFight = () => this.fights?.InFight(this.Settings.QuietItemsInFights) ?? false;
                this.objectSounds.QuietLoot = inFight;
                this.nearby.QuietLoot = inFight;
                this.narrator.QuietGroundItems = inFight;
                // The mod's own recorded sounds (assets/, shipped), loaded like the game's.
                var modSounds = GameSounds.Load(Path.Join(this.DllDirectory, "assets"), GameSoundSeconds, this.log);
                this.alerts.Charges.RecordedSound = key => modSounds.GetStereo(key) ?? gameSounds.GetStereo(key);
                this.culls.Chime = modSounds.Get("cull") ?? this.culls.Chime;
                this.lostHint = new LostHint(this.audio, this.entities, modSounds.GetLoop(LostHint.LoopName), this.log);
                this.lostHint.SayWay = cell => this.output?.Say(SpeechKind.Places, this.entities.DescribeWayOn(cell));
                this.mapHints = new MapHintNarrator(this.entities, this.output, MapHintTable.Load(this.log, Path.Join(this.DllDirectory, "catalog", "map-hints.json")),
                                                    () => this.gameState?.CurrentAreaId ?? string.Empty, this.log);
                this.radar.LeadsToUnseen = (from, axis) => this.Settings.UnexploredMark ? this.entities.UnseenToward(from, axis) : null;
                this.discoveries = new DiscoveryNarrator(this.entities, this.output, this.audio, gameSounds, new KnownPlaces(Path.Join(this.DllDirectory, "config", "known-places.txt")));
                this.enemyNews = new EnemyNarrator(this.entities, this.output);
                this.deaths = new DeathWatch(this.locator, elements, this.output, this.log)
                {
                    Died = () =>
                    {
                        this.entities?.RememberDeath();
                        this.bosses?.PlayerDied();
                    },
                    Revivable = () => Seats.Other is { } other && IsAlive(other),
                    EndsWithoutDeathScreen = () => TrialHud.IsRunArea(this.gameState?.CurrentAreaId ?? string.Empty),
                };
                this.ApplySoundVolumes(this.Settings);
                this.commands = new ModCommands(this.output, this.focus, this.semantics, this.narrator, () => this.screen?.Current ?? ScreenSnapshot.Empty, this.entities, this.beacon, this.ReadVitals, this.log);
                this.screen = new ScreenWatcher(elements, this.log);
                this.entities.QuestTexts = this.QuestTexts;
                this.entities.CrystalsCounted = () => this.narrator != null && this.screen != null && this.narrator.CrystalsCounted(this.screen.Current);
                this.entities.RoomsApart = () => this.trialScope.InTrial(Environment.TickCount64);
                this.entities.QuestGoals = QuestGoalCatalog.Load(this.log, Path.Join(this.DllDirectory, "catalog", "quest-goals.json"));
                this.narrator.HonourLabel = () => this.entities?.HonourLabel ?? string.Empty;
                this.narrator.WaterLabel = () => this.entities?.WaterLabel ?? string.Empty;
                this.narrator.TrialCountersBeside = this.semantics.TrialCounters;
                this.narrator.TrialCounterKind = this.semantics.TrialCounterKind;
                this.narrator.TrialEffectsBeside = this.semantics.TrialEffects;
                this.semantics.TrialFloorNote = note => this.log?.Info(note);
                this.narrator.Trials = this.semantics.Trials = TrialCatalog.Load(Path.Join(this.DllDirectory, "catalog", "trials.json"), this.log);
                this.ascendancy = new AscendancyChoiceWatch(elements, () => this.semantics?.Trials ?? TrialCatalog.Empty, this.output, this.log);
                this.commands.TreeAnchor = this.PreviewAnchor;
                this.semantics.UnallocatedLabel = () => this.entities?.UnallocatedLabel ?? string.Empty;
                this.semantics.PinLevelLabel = () => this.entities?.PinLevelLabel ?? string.Empty;
                this.semantics.PinTownLabel = () => this.entities?.PinTownLabel ?? string.Empty;
                this.mapPins = new MapPinNarrator(this.semantics, this.output, this.log);
                this.entities.AreaName = () => this.gameState?.CurrentAreaName ?? string.Empty;
                this.entities.Memory = new AreaMemory(Path.Join(this.DllDirectory, "config", "explored"), this.log);
                this.commands.UseGlossary(new SoundGlossary(this.audio, gameSounds, modSounds, drops, diabloSounds));
                this.commands.Settings = () => this.Settings;
                this.commands.SkillBar = this.ReadSkillBar;
                SpeechPriority.Chosen = this.SpeechLevel;
                this.narrator.InPanel = this.semantics.IsInPanel;
                this.narrator.InOtherControlGroup = this.semantics.InOtherControlGroup;
                this.narrator.SidePanel = () => this.pickups?.SidePanel?.Invoke() ?? string.Empty;
                this.narrator.InSiblingRow = this.semantics.InSiblingRow;
                this.narrator.InAnotherShopCard = this.semantics.InAnotherShopCard;
                this.narrator.ShopShown = () => this.ShopPage() != IntPtr.Zero;
                this.narrator.SelectorPosition = this.semantics.SelectorPosition;
                this.narrator.SelectorChoice = this.semantics.SelectorChoice;
                this.narrator.IsSkillBarText = this.IsSkillBarText;
                this.narrator.IsQuestTrackerText = this.IsQuestTrackerText;
                this.narrator.AreaLinePending = this.AreaLinePending;
                this.focus.FocusChanged += this.OnCreationFocus;
                this.focus.FocusChanged += this.OnPreGameFocus;
                this.narrator.Refresh = node => this.semantics?.Describe(node.Address, node.IsHovered);
                this.semantics.PlayerPath = () => Seats.Followed.Path ?? string.Empty;
                this.semantics.Overrides = PassiveOverrides.Load(Path.Join(this.DllDirectory, "catalog", "passive-overrides.txt"));
                this.log.Info($"Passive overrides: {this.semantics.Overrides.Count} class nodes from the game files");
                this.commands.SettingsChanged = changed =>
                {
                    this.ApplySettings(changed);
                    this.settingsStore?.Save(changed);
                };
                this.chat = new ChatWatcher(elements, comboBoxes, line =>
                {
                    if (!this.Settings.AnnounceChat)
                    {
                        return;
                    }

                    // A notice can arrive as the window it belongs to closes: closing must not cut "Intercambio aceptado". It cuts in
                    // and yields to moves: queued behind an item's text it held the inventory silent for 8 s (2026-11-06).
                    if (ChatDiff.IsNotice(line))
                    {
                        this.output.Keep(line, SpeechFloor.MessageHoldMs(line), true, yieldsToMoves: true);
                    }
                    else
                    {
                        this.output.Speak(line, false);
                    }
                })
                {
                    PanelChanged = text => this.output.Speak(text, true),
                    ChannelChanged = text => this.output.Urge(text),
                    ReadPlayerChannels = () => this.Settings.AnnouncePlayerChat,
                    OwnNames = () => new[] { PlayerAlerts.Name(Seats.Of(1)), PlayerAlerts.Name(Seats.Other) },
                    SpeakOwn = line => this.output.Speak(line, false),
                    QuietLocal = () => this.fights?.InFight(this.Settings.QuietChatInFights) ?? false,
                    KeepInFight = line => BossMechanicsCatalog.Shared.IsMechanicLine(line),
                };
                this.mapOverlay = new MapOverlayWatch(elements, text => this.output.Speak(text, true));
                this.longText = new LongTextReader(elements, this.output);
                this.longText.Closed += this.RepeatFocus;
                this.research = new ResearchSession(this.dumper, new FocusResearchLog(elements, debugDirectory, this.log), this.log)
                {
                    Enabled = this.Settings.ResearchMode,
                };
                this.focus.FocusChanged += node => this.research.OnFocusChanged(this.gameState?.CurrentState ?? GameStateTypes.GameNotLoaded, node, this.focus.CurrentPanelIndex);

                this.gameState = new GameStateService(this.log, memory, elements) { Report = this.compatibility };
                this.gameState.StateChanged += this.OnGameStateChanged;
                this.gameState.AreaChanged += this.OnAreaChanged;
                this.gameState.InstanceChanged += this.OnInstanceChanged;
                this.gameState.AttachedChanged += this.OnAttachedChanged;
                this.processProbe = new GameProcessProbe(this.log);
                this.processProbe.ReportChanged += report => this.output.Speak(report, false);

                this.output.Speak(Announcements.PluginReady, true);
            }
            catch (Exception ex)
            {
                // The host enables plugins without a try/catch; never take GameHelper down with us.
                Console.WriteLine($"[Poe2Accessibility] OnEnable failed: {ex}");
                this.log?.Error("OnEnable failed", ex);
            }
        }

        /// <inheritdoc />
        public override void OnDisable()
        {
            if (this.gameState != null)
            {
                this.gameState.StateChanged -= this.OnGameStateChanged;
                this.gameState.AreaChanged -= this.OnAreaChanged;
                this.gameState.InstanceChanged -= this.OnInstanceChanged;
                this.gameState.AttachedChanged -= this.OnAttachedChanged;
                this.gameState = null;
            }

            this.processProbe = null;
            this.screen?.Dispose();
            this.screen = null;
            this.enemyCues = null;
            this.radar = null;
            this.lostHint = null;
            this.mapHints = null;
            this.partnerRadar = null;
            Seats.Attach(null, null);
            this.localPlayers = null;
            this.nearby = null;
            this.places = null;
            this.inventoryReader = null;
            this.wallBump = null;
            this.wallTones = null;
            this.hazards = null;
            this.audio?.Dispose();
            this.audio = null;

            this.output.Dispose();
            this.output = new NullOutputService();
            this.log?.Info("Disabled.");
        }

        /// <inheritdoc />
        public override void DrawUI()
        {
            this.Isolated("settings file", static core =>
            {
                if (core.settingsStore == null)
                {
                    return;
                }

                switch (core.settingsStore.ReloadIfChanged(core.Settings, out var reloaded))
                {
                    case SettingsStore<AccessibilitySettings>.ReloadOutcome.Reloaded:
                        core.ApplySettings(reloaded);
                        core.output.Speak(Announcements.SettingsReloaded, false);
                        break;
                    case SettingsStore<AccessibilitySettings>.ReloadOutcome.Failed:
                        core.output.Speak(Announcements.SettingsInvalid, false);
                        break;
                }
            });

            if (this.gameState == null)
            {
                return;
            }

            // The host logs an exception that leaves DrawUI once and drops the rest of the frame, every frame it happens:
            // what runs before the parts below is isolated like them, so one of these failing cannot silence the whole mod.
            this.Isolated("game state", static core => core.gameState?.Tick());
            this.Isolated("state news", static core =>
            {
                core.AnnounceLastingClose();
                core.AnnounceSettledState();
            });
            this.Isolated("area news", static core => core.AnnounceLoadedArea());
            if (!this.gameState.IsAttached)
            {
                this.Isolated("process probe", static core => core.processProbe?.Tick());
            }
            else
            {
                var stateObject = GameStateService.CurrentStateObjectAddress();
                // The pad first: the game moves the focus in the frame it reads a press, and the narrator needs to
                // know that A, LB or RB caused the move it is about to hear about.
                // GameHelper only writes a plugin's exceptions to its hidden console: each part reports its own faults
                // to the log and one failing part does not silence the rest.
                // Static lambdas given the plugin and the frame's state (Isolated): a method group or a capturing lambda would allocate each frame.
                var state = this.gameState.CurrentState;
                var frame = (State: state, StateObject: stateObject);
                this.Isolated("pad", static core => core.ReadModButton());
                this.Isolated("focus", frame, static (core, frame) => core.focus?.Tick(frame.State, frame.StateObject));
                this.Isolated("trade offer", static core => core.tradeOffers?.Tick(core.focus?.Current?.Address ?? IntPtr.Zero));
                this.Isolated("search highlight", state, static (core, state) => core.searchHighlights?.Tick(state == GameStateTypes.InGameState ? core.focus?.Current : null));
                this.Isolated("text field", static core => core.WatchTextFieldClosing());
                this.Isolated("creation name", static core => core.TickCreationName());
                this.Isolated("player news", static core => core.TickPlayerNews());
                this.Isolated("sounds", static core => core.TickSounds());
                this.Isolated("screen", stateObject, static (core, stateObject) => core.NarrateScreenChanges(stateObject));
                if (state == GameStateTypes.InGameState)
                {
                    this.Isolated("death", stateObject, static (core, stateObject) => core.deaths?.Tick(stateObject));
                    this.Isolated("ascendancy", static core => core.ascendancy?.Tick(
                        core.PanelOverGame()?.Address ?? IntPtr.Zero, core.focus?.Current is { IsPassiveNode: false }, Environment.TickCount64 - core.lastConfirmAt));
                    this.Isolated("trace", static core => core.actorTrace?.Tick());
                    this.Isolated("stun trace", static core => core.stunTrace?.Tick());
                    this.Isolated("grid trace", static core => core.TickGridTrace());
                }

                this.Isolated("compatibility", static core => core.SayCompatibility());
                this.Isolated("research", frame, static (core, frame) =>
                {
                    core.research?.Tick(frame.State, frame.StateObject, core.Settings.UiDumpRootAddressOverride, core.Settings.UiDumpMaxDepth, core.Settings.UiDumpMaxNodes);
                    if (core.elements != null)
                    {
                        core.research?.WatchFocusedBytes(core.focus?.Current?.Address ?? IntPtr.Zero, core.elements);
                    }
                });
            }

            if (this.devCommands != null)
            {
                foreach (var command in this.devCommands.Poll())
                {
                    this.Isolated("dev command", command, static (core, command) => core.RunDevCommand(command));
                }
            }

            // With the game attached, hotkeys only count while its window is in front (never while typing
            // elsewhere). Without a game there is no window to gate on, and the user still needs feedback.
            if (this.gameState.IsAttached && !Core.Process.Foreground)
            {
                return;
            }

            this.Isolated("hotkeys", static core =>
            {
                if (core.hotkeys.Pressed(core.Settings.DumpUiTreeHotKey))
                {
                    core.DumpUiTree(core.Settings.UiDumpVisibleOnly);
                }

                if (core.hotkeys.Pressed(core.Settings.RepeatLastHotKey))
                {
                    core.RepeatLast();
                }

                if (core.hotkeys.Pressed(core.Settings.SpeakStatusHotKey))
                {
                    core.SpeakStatus();
                }
            });
        }

        private void NarrateScreenChanges(IntPtr stateObject)
        {
            if (this.screen == null || this.narrator == null || this.focus == null || this.locator == null || this.gameState == null)
            {
                return;
            }

            var inGame = this.InGame;
            var container = inGame ? this.locator.InGameLayerContainer(stateObject) : this.focus.CurrentRoot;
            this.focus.LayerContainer = container;
            if (this.semantics != null)
            {
                this.semantics.LayerContainer = container;
            }

            this.screen.SetRoot(container);
            // The layers hang off the container found above; the locator's InGame* wrappers would find it again for each.
            var fields = this.locator.Fields;
            var chatLayer = inGame ? fields.Find(container, InGameField.Chat) : IntPtr.Zero;
            this.screen.SetIgnoredElement(chatLayer);
            // Each reader beside the narrator on its own: the chat failing on every frame must not leave every menu unread.
            this.Isolated("chat", chatLayer, static (core, chatLayer) => core.chat?.Tick(chatLayer));
            this.Isolated("map overlay", inGame ? fields.Find(container, InGameField.MapOverlay) : IntPtr.Zero, static (core, overlay) => core.mapOverlay?.Tick(overlay));
            this.Isolated("long text", static core =>
            {
                if (core.screen != null)
                {
                    core.longText?.Tick(core.screen.LongText);
                }
            });
            this.narrator.TutorialLayer = inGame ? fields.Find(container, InGameField.Tutorial) : IntPtr.Zero;
            this.screen.SetArrivalLayer(this.narrator.TutorialLayer);
            this.narrator.HudLayer = inGame ? fields.Find(container, InGameField.Hud) : IntPtr.Zero;
            this.skillBar = inGame ? fields.Find(container, InGameField.SkillBar) : IntPtr.Zero;
            this.narrator.ReadTooltips = this.Settings.ReadTooltipsOnFocus;
            this.narrator.Tick();
            // Another window in front (a notification, alt-tab): what the game redraws meanwhile is not news, and is
            // taken in silently so coming back does not read it (tooltips read out on a notification, 2026-10-26).
            var watching = !this.gameState.IsAttached || Core.Process.Foreground;
            while (this.screen.TryDequeue(out var change))
            {
                if (change.Kind != ScreenChangeKind.NumbersChanged) // a running counter would fill the log
                {
                    this.log?.Info($"Screen {change.Kind} layer {change.Layer.Index}: {string.Join(" | ", change.Texts.Select(text => text.Button.Length > 0 ? $"{text.Text} [{text.Button}]" : text.Text))}");
                }

                this.deaths?.Saw(change.Layer.Address);
                if (watching && (change.Kind == ScreenChangeKind.TopLayerChanged || this.Settings.AnnounceAppearingText))
                {
                    this.narrator.OnScreenChange(change, this.screen.Current, this.focus.Current, this.focus.CurrentLayer);
                }
            }

            // What the narrator looks for on the screen, each on its own: a countdown that fails to parse must not take the
            // honour, the trial's lines and the map pins with it.
            var snapshot = this.screen.Current;
            if (watching && (!this.wasWatching || this.narrator.QuestPageDue))
            {
                // An objective that changed behind another window or a menu.
                this.Isolated("quest page", snapshot, static (core, snapshot) => core.narrator?.CheckQuestPage(snapshot));
            }

            this.wasWatching = watching;
            if (!watching)
            {
                return;
            }

            if (this.Settings.AnnounceTimers)
            {
                this.Isolated("countdown", snapshot, static (core, snapshot) => core.narrator?.CheckCountdown(snapshot));
            }

            this.Isolated("shop balance", snapshot, static (core, snapshot) => core.narrator?.CheckShopBalance(snapshot));

            if (this.Settings.AnnounceHonour)
            {
                this.Isolated("honour", snapshot, static (core, snapshot) => core.narrator?.CheckHonour(snapshot));
            }

            if (this.Settings.AnnounceTrialCounters)
            {
                this.Isolated("trial counters", snapshot, static (core, snapshot) => core.narrator?.CheckTrialCounters(snapshot));
                this.Isolated("trial effects", snapshot, static (core, snapshot) => core.narrator?.CheckTrialEffects(snapshot));
            }

            if (this.Settings.AnnounceTrialProgress)
            {
                this.Isolated("trial lines", snapshot, static (core, snapshot) => core.narrator?.CheckTrialLines(snapshot));
            }

            if (this.Settings.AnnounceAppearingText)
            {
                this.Isolated("map pins", static core => core.mapPins?.Tick(core.InGame ? core.WorldScreen() : IntPtr.Zero));
            }

            if (this.gameState.CurrentState is (GameStateTypes.LoginState or GameStateTypes.SelectCharacterState or
                    GameStateTypes.CreateCharacterState or GameStateTypes.DeleteCharacterState) && this.Settings.AnnounceAppearingText)
            {
                this.Isolated("pre-game hints", snapshot, static (core, snapshot) => core.narrator?.OnPreGameHints(snapshot));
                this.Isolated("controller prompt", snapshot, static (core, snapshot) => core.TickControllerPrompt(snapshot));
            }
        }

        /// <summary>Whether the element is the one the UI manager's controller-focus slot holds now, for the seat being read.</summary>
        private bool InFocusSlot(IntPtr element) =>
            this.focus is { Manager: var manager } tracker && manager != IntPtr.Zero && this.memoryReader != null &&
            this.memoryReader.TryRead<IntPtr>(manager + GameLayout.UiManager.CurrentOffset + ((tracker.Seat - 1) * GameLayout.UiManager.PlayerStride), out var held) &&
            held == element;

        /// <summary>
        ///     Whether another player in the area bears that name (the host's Player component): what tells the menu A
        ///     opens beside a player from any other button menu.
        /// </summary>
        private static bool OtherPlayerNamed(string name)
        {
            if (name.Length == 0 || Core.States.GameCurrentState != GameStateTypes.InGameState)
            {
                return false;
            }

            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.EntityType == GameReader.RemoteEnums.Entity.EntityTypes.Player &&
                    entity.TryGetComponent<GameReader.RemoteObjects.Components.Player>(out var player) &&
                    string.Equals(player.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A text element inside the HUD's skill bar (its alert line).</summary>
        private bool IsSkillBarText(IntPtr text) =>
            this.skillBar != IntPtr.Zero && this.elements != null && this.elements.TryReadElement(text, out var info) &&
            this.elements.ReadAncestors(info, SkillBarTextDepth).Any(ancestor => ancestor.Address == this.skillBar);

        /// <summary>A quest's title or objective line in the HUD's quest tracker.</summary>
        private bool IsQuestTrackerText(IntPtr text) =>
            this.elements != null && this.elements.TryReadElement(text, out var info) &&
            info.Name is GameLayout.InGameUi.QuestTitleName or GameLayout.InGameUi.QuestLineName &&
            this.elements.ReadAncestors(info, GameLayout.InGameUi.QuestLineDepth).Any(ancestor => ancestor.Name == GameLayout.InGameUi.QuestPagesName);
    }
}
