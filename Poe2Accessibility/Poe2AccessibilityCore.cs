namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteEnums;
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
        private AudioCueService? audio;

        private EnemyCues? enemyCues;
        private Radar? radar;
        private UnexploredLoop? unexploredLoop;
        private Radar? partnerRadar;
        private LocalPlayers? localPlayers;
        private ProximityNarrator? nearby;
        private PlaceNarrator? places;
        private WallBump? wallBump;
        private WallTones? wallTones;
        private GamepadReader.PadState padState;
        private readonly InputSeat inputSeat = new();
        private readonly CoopDowns coopDowns = new();
        private readonly HostWindowHider hostWindow = new();
        private IntPtr padManager;
        private readonly System.Collections.Generic.Dictionary<string, long> tickFaults = new();
        private HostMemoryReader? memoryReader;

        /// <summary>A failing sound tick is logged once a minute, not every frame.</summary>
        private const int SoundErrorReportMs = 60000;

        /// <summary>Right stick deflection that counts as aiming (the target is named only then).</summary>
        private const float AimDeflection = 0.3f;

        /// <summary>A stick pushed this far counts as a direction (our threshold: well past rest, before a full push).</summary>
        private const float DirectionDeflection = 0.5f;

        private Beacon? beacon;
        private TargetNarrator? targets;
        private BossWatch? bosses;
        private WeakPointGuide? weakPoint;
        private ShieldWatch? shield;
        private HazardWatch? hazards;
        private EncounterReader? encounters;
        private CullWatch? culls;
        private DeployedWatch? deployed;
        private EnemyStateWatch? enemyStates;
        private ObjectSounds? objectSounds;
        private PlayerAlerts? alerts;
        private PlayerAlerts? partnerAlerts;
        private InventoryWatch? pickups;
        private InventoryWatch? partnerPickups;
        private InventoryReader? inventoryReader;
        private GoldWatch? gold;
        private BarrierWatch? barriers;
        private DiscoveryNarrator? discoveries;
        private EnemyNarrator? enemyNews;
        private DeathWatch? deaths;
        private ExperienceTable? experience;
        private FlaskWatch? flasks;
        private FlaskWatch? partnerFlasks;
        private EntityFields? entityFields;
        private FlagBits? flagBits;
        private readonly PadButtonCheck padCheck = new();

        /// <summary>Game sounds are cut to this length: repeated proximity cues must stay short.</summary>
        private const float GameSoundSeconds = 0.9f;

        /// <summary>The focus of the previous frame, to notice a text field closing (pre-game focus goes to nothing).</summary>
        private AccessibleUiNode? previousFocus;

        /// <summary>When A (the confirm button) last went down, from the game's own pad state.</summary>
        private long lastConfirmAt = long.MinValue / 2;

        /// <summary>When a direction (d-pad or a stick) was last held, from the game's own pad state.</summary>
        private long lastDirectionAt = long.MinValue / 2;

        /// <summary>When the game went to "not loaded"; zero once announced or left (see <see cref="AnnounceLastingClose"/>).</summary>
        private long closedSince;

        /// <summary>A "not loaded" state shorter than this is a transition, not the game closing.</summary>
        private const int ClosedSettleMs = 2000;

        /// <summary>
        ///     A state the game may only pass through is said once it has lasted this long: entering the game goes
        ///     through character selection for 18 ms ("Cargando. Selección de personaje. Cargando área", 2026-10-24).
        ///     The others are said at once, before the area name or the menu they bring.
        /// </summary>
        private const int StateSettleMs = 150;

        /// <summary>No area goes past this level (endgame maps reach the 80s); a larger number is a broken read.</summary>
        private const int MaxAreaLevel = 100;

        private static readonly System.Collections.Generic.HashSet<GameStateTypes> PassingStates = new()
        {
            GameStateTypes.SelectCharacterState, GameStateTypes.WaitingState, GameStateTypes.LoadingState,
        };

        /// <summary>The state announcement waiting out <see cref="StateSettleMs"/>; Detected keeps "Game detected" when replaced.</summary>
        private (string Text, bool Detected, long At, bool Queued)? pendingState;

        /// <summary>The language setting last applied to <see cref="Localization"/>.</summary>
        private string? appliedLanguage;
        private bool confirmDown;

        /// <summary>When each kind of monster attack lands, learnt from the blows taken (BossWatch).</summary>
        private string DodgeTimingsPath => Path.Join(this.DllDirectory, "config", "dodge-timings.txt");

        /// <summary>Timings measured in development, shipped so the first fights are timed too.</summary>
        private string DodgeTimingsSeedPath => Path.Join(this.DllDirectory, "catalog", "dodge-timings.txt");

        /// <summary>The creation screen's name help was said for this visit (a refused name comes back to it).</summary>
        private bool creationHelpSaid;

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
        public override string GetDescription() =>
            "Screen-reader accessibility for blind players: speaks game state and UI through PRISM.";

        /// <inheritdoc />
        public override void OnEnable(bool isGameOpened)
        {
            try
            {
                this.log = new PluginLog(Path.Join(this.DllDirectory, "logs"), this.Settings.VerboseLogging);
                this.settingsStore = new SettingsStore<AccessibilitySettings>(Path.Join(this.DllDirectory, "config", "settings.txt"), this.log);
                this.ApplySettings(this.settingsStore.Load(this.Settings));
                this.log.Info($"Enabled. GameHelper {Core.GetVersion()}, game open: {isGameOpened}");
                Localization.PhraseDirectory = Path.Join(this.DllDirectory, "catalog", "lang");
                this.appliedLanguage = this.Settings.Language;
                Localization.Detect(this.Settings.Language, message => this.log.Info(message));

                IAccessibilityOutputService? prism = this.Settings.SpeechEnabled
                    ? PrismOutputService.TryCreate(this.DllDirectory, this.log)
                    : null;
                this.output = prism ?? new NullOutputService();
                this.WarnOnHotkeyCollisions();

                var memory = new HostMemoryReader(this.log);
                this.memoryReader = memory;
                var debugDirectory = Path.Join(this.DllDirectory, "debug");
                this.dumper = new UiTreeDumper(new UiTreeReader(memory), debugDirectory, this.log);
                this.devCommands = new DevCommandFile(debugDirectory, this.log);
                var elements = new UiElementReader(memory);
                this.elements = elements;
                var catalog = UiCatalog.Load(Path.Join(this.DllDirectory, "catalog", "ui-catalog.json"), this.log);
                DatColumns.Apply(Path.Join(this.DllDirectory, "catalog", "dat-columns.json"), this.log);
                this.experience = ExperienceTable.Load(Path.Join(this.DllDirectory, "catalog", "experience.json"), this.log);
                this.compatibility = new CompatibilityReport(this.log);
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
                this.semantics = new UiSemantics(memory, elements, catalog, comboBoxes, inventory);
                this.semantics.TreeFields.Report = this.compatibility;
                this.semantics.TreeFields.Log = message => this.log?.Info(message);
                this.semantics.Trace = message => this.log.Debug(message);
                this.semantics.Fields = this.locator.Fields;
                this.focus = new FocusTracker(memory, elements, this.locator, this.semantics, this.log);
                this.narrator = new ScreenNarrator(this.output, this.log, layer => this.semantics?.SelectedTab(layer) ?? string.Empty);
                this.narrator.FocusLayer = () => this.focus?.CurrentLayer ?? IntPtr.Zero;
                this.narrator.SinceConfirm = () => Environment.TickCount64 - this.lastConfirmAt;
                this.narrator.SinceDirection = () => Environment.TickCount64 - this.lastDirectionAt;
                this.focus.FocusChanged += this.narrator.OnFocusChanged;
                this.focus.FocusChanged += node =>
                {
                    if (this.gameState?.CurrentState == GameStateTypes.InGameState)
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
                // GameHelper's Radar table; a test package ships only this plugin, with a copy of it in its catalog.
                var landmarks = LandmarkTable.Load(
                    this.log,
                    Path.Join(this.DllDirectory, "..", "Radar", "important_tgt_files.txt"),
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
                this.alerts = new PlayerAlerts(() => Seats.Followed, this.audio, followedVoice, this.log);
                this.partnerAlerts = new PlayerAlerts(() => Seats.Partner, null, partnerVoice, this.log);
                this.pickups = new InventoryWatch(inventory, followedVoice, this.log) { Seat = followedSeat };
                this.partnerPickups = new InventoryWatch(inventory, partnerVoice, this.log) { Seat = () => 2 };
                this.flasks = new FlaskWatch(inventory, followedVoice) { Seat = followedSeat };
                this.partnerFlasks = new FlaskWatch(inventory, partnerVoice) { Seat = () => 2 };
                this.gold = new GoldWatch(memory, elements, this.output, this.log) { Fields = this.locator.Fields };
                this.pickups.Gold = () => this.gold?.ReadNow();
                this.focus.InGameHighlightRoot = this.ShopPage;
                this.pickups.SidePanel = () => this.semantics?.SidePanelTitle(
                    this.locator.Fields.Find(this.focus?.LayerContainer ?? IntPtr.Zero, InGameField.InventoryPanel)) ?? string.Empty;
                this.pickups.GroundItemIds = this.partnerPickups.GroundItemIds = () => this.entities?.GroundItemIds() ?? Array.Empty<uint>();
                this.pickups.GroundItemTitle = this.partnerPickups.GroundItemTitle = id => this.entities?.GroundItemName(id);
                this.barriers = new BarrierWatch(this.entities, this.output, this.log);
                var timings = DodgeTimings.Load(Path.Join(this.DllDirectory, "catalog", "hit-times.txt"), this.DodgeTimingsSeedPath, this.DodgeTimingsPath);
                this.log.Info($"Dodge timings: {timings.FileTimes} attacks timed by the game files");
                this.bosses = new BossWatch(this.audio, this.output, elements, this.log) { Timings = timings, TimingsPath = this.DodgeTimingsPath };
                this.weakPoint = new WeakPointGuide(this.audio, this.output, elements, memory, this.log);
                this.shield = new ShieldWatch(this.audio, this.output, memory, this.log);
                var skills = new PlayerSkills(memory, elements, this.log);
                this.bosses.CanParry = () => skills.Has(PlayerSkills.Parry);
                var hazardGround = HazardGround.Load(Path.Join(this.DllDirectory, "catalog", "hazards.txt"));
                this.log.Info($"Hazard ground: {hazardGround.Count} buffs from the game files");
                this.hazards = new HazardWatch(this.audio, hazardGround, this.log);
                this.encounters = new EncounterReader(elements);
                var cullSkills = CullSkills.Load(Path.Join(this.DllDirectory, "catalog", "cull-skills.txt"));
                this.log.Info($"Cull skills: {cullSkills.Count} skills from the game files");
                this.culls = new CullWatch(this.audio, this.output, elements) { HasCullingSkill = () => cullSkills.AnyCulls(skills.Ids()) };
                this.deployed = new DeployedWatch(this.audio, this.log);
                this.enemyStates = new EnemyStateWatch(this.audio, this.output, elements, memory);
                var gameSounds = GameSounds.Load(Path.Join(this.DllDirectory, "sounds"), GameSoundSeconds, this.log);
                this.deployed.GameSound = gameSounds.Get;
                var drops = ItemDropSounds.Load(
                    Path.Join(this.DllDirectory, "catalog", "item-sounds.json"), Path.Join(this.DllDirectory, "sounds", "items"), GameSoundSeconds, this.log);
                this.objectSounds = new ObjectSounds(this.audio, this.entities, gameSounds, drops);
                // The mod's own recorded sounds (assets/, shipped), loaded like the game's.
                var modSounds = GameSounds.Load(Path.Join(this.DllDirectory, "assets"), GameSoundSeconds, this.log);
                this.unexploredLoop = new UnexploredLoop(this.audio, this.entities, modSounds.GetLoop(UnexploredLoop.LoopName), this.log);
                this.radar.Unexplored = () => this.Settings.UnexploredMark ? this.entities.UnexploredCells() : Array.Empty<System.Numerics.Vector2>();
                this.discoveries = new DiscoveryNarrator(this.entities, this.output, this.audio, gameSounds, new KnownPlaces(Path.Join(this.DllDirectory, "config", "known-places.txt")));
                this.enemyNews = new EnemyNarrator(this.entities, this.output);
                this.deaths = new DeathWatch(this.locator, elements, this.output, this.log)
                {
                    Died = () => this.entities?.RememberDeath(),
                    Revivable = () => Seats.Other is { } other && IsAlive(other),
                };
                this.ApplySoundVolumes(this.Settings);
                this.commands = new ModCommands(this.output, this.focus, this.semantics, this.narrator, () => this.screen?.Current ?? ScreenSnapshot.Empty, this.entities, this.beacon, this.ReadVitals, this.log);
                this.screen = new ScreenWatcher(elements, this.log);
                this.entities.QuestTexts = this.QuestTexts;
                this.entities.AreaName = () => this.gameState?.CurrentAreaName ?? string.Empty;
                this.entities.Memory = new AreaMemory(Path.Join(this.DllDirectory, "config", "explored"), this.log);
                this.commands.UseGlossary(new SoundGlossary(this.audio, gameSounds, modSounds));
                this.commands.Settings = () => this.Settings;
                this.commands.SkillBar = this.ReadSkillBar;
                SpeechPriority.Chosen = this.SpeechLevel;
                this.narrator.InPanel = this.semantics.IsInPanel;
                this.narrator.InOtherControlGroup = this.semantics.InOtherControlGroup;
                this.narrator.InSiblingRow = this.semantics.InSiblingRow;
                this.narrator.InAnotherShopCard = this.semantics.InAnotherShopCard;
                this.narrator.SelectorPosition = this.semantics.SelectorPosition;
                this.narrator.SelectorChoice = this.semantics.SelectorChoice;
                this.narrator.IsSkillBarText = this.IsSkillBarText;
                this.narrator.IsQuestTrackerText = this.IsQuestTrackerText;
                this.focus.FocusChanged += this.OnCreationFocus;
                this.narrator.Refresh = node => this.semantics?.Describe(node.Address, node.IsHovered);
                this.semantics.PlayerPath = () => Seats.Followed.Path ?? string.Empty;
                this.commands.SettingsChanged = changed =>
                {
                    this.ApplySettings(changed);
                    this.settingsStore?.Save(changed);
                };
                this.chat = new ChatWatcher(elements, comboBoxes, line =>
                {
                    if (this.Settings.AnnounceChat)
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
                };
                this.mapOverlay = new MapOverlayWatch(elements, text => this.output.Speak(text, true));
                this.longText = new LongTextReader(elements, this.output);
                this.research = new ResearchSession(this.dumper, new FocusResearchLog(elements, debugDirectory, this.log), this.log)
                {
                    Enabled = this.Settings.ResearchMode,
                };
                this.focus.FocusChanged += node => this.research.OnFocusChanged(this.gameState?.CurrentState ?? GameStateTypes.GameNotLoaded, node, this.focus.CurrentPanelIndex);

                this.gameState = new GameStateService(this.log, memory, elements) { Report = this.compatibility };
                this.gameState.StateChanged += this.OnGameStateChanged;
                this.gameState.AreaChanged += this.OnAreaChanged;
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
                this.gameState.AttachedChanged -= this.OnAttachedChanged;
                this.gameState = null;
            }

            this.processProbe = null;
            this.screen?.Dispose();
            this.screen = null;
            this.enemyCues = null;
            this.radar = null;
            this.unexploredLoop = null;
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
            this.Isolated("host window", this.hostWindow.Tick);
            if (this.settingsStore != null)
            {
                switch (this.settingsStore.ReloadIfChanged(this.Settings, out var reloaded))
                {
                    case SettingsStore<AccessibilitySettings>.ReloadOutcome.Reloaded:
                        this.ApplySettings(reloaded);
                        this.output.Speak(Announcements.SettingsReloaded, false);
                        break;
                    case SettingsStore<AccessibilitySettings>.ReloadOutcome.Failed:
                        this.output.Speak(Announcements.SettingsInvalid, false);
                        break;
                }
            }

            if (this.gameState == null)
            {
                return;
            }

            this.gameState.Tick();
            this.AnnounceLastingClose();
            this.AnnounceSettledState();
            if (!this.gameState.IsAttached)
            {
                this.processProbe?.Tick();
            }
            else
            {
                var stateObject = GameStateService.CurrentStateObjectAddress();
                // The pad first: the game moves the focus in the frame it reads a press, and the narrator needs to
                // know that A, LB or RB caused the move it is about to hear about.
                // GameHelper only writes a plugin's exceptions to its hidden console: each part reports its own faults
                // to the log and one failing part does not silence the rest.
                var state = this.gameState.CurrentState;
                this.Isolated("pad", this.ReadModButton);
                this.Isolated("focus", () => this.focus?.Tick(state, stateObject));
                this.Isolated("text field", this.WatchTextFieldClosing);
                this.Isolated("player news", this.TickPlayerNews);
                this.Isolated("sounds", this.TickSounds);
                this.Isolated("screen", () => this.NarrateScreenChanges(stateObject));
                if (state == GameStateTypes.InGameState)
                {
                    this.Isolated("death", () => this.deaths?.Tick(stateObject));
                    this.Isolated("trace", () => this.actorTrace?.Tick());
                    this.Isolated("stun trace", () => this.stunTrace?.Tick());
                }

                this.Isolated("compatibility", this.SayCompatibility);
                this.Isolated("research", () =>
                {
                    this.research?.Tick(state, stateObject, this.Settings.UiDumpRootAddressOverride, this.Settings.UiDumpMaxDepth, this.Settings.UiDumpMaxNodes);
                    if (this.elements != null)
                    {
                        this.research?.WatchFocusedBytes(this.focus?.Current?.Address ?? IntPtr.Zero, this.elements);
                    }
                });
            }

            if (this.devCommands != null)
            {
                foreach (var command in this.devCommands.Poll())
                {
                    this.RunDevCommand(command);
                }
            }

            // With the game attached, hotkeys only count while its window is in front (never while typing
            // elsewhere). Without a game there is no window to gate on, and the user still needs feedback.
            if (this.gameState.IsAttached && !Core.Process.Foreground)
            {
                return;
            }

            if (this.hotkeys.Pressed(this.Settings.DumpUiTreeHotKey))
            {
                this.DumpUiTree(this.Settings.UiDumpVisibleOnly);
            }

            if (this.hotkeys.Pressed(this.Settings.RepeatLastHotKey))
            {
                this.RepeatLast();
            }

            if (this.hotkeys.Pressed(this.Settings.SpeakStatusHotKey))
            {
                this.SpeakStatus();
            }
        }

        private void NarrateScreenChanges(IntPtr stateObject)
        {
            if (this.screen == null || this.narrator == null || this.focus == null || this.locator == null || this.gameState == null)
            {
                return;
            }

            var inGame = this.gameState.CurrentState == GameStateTypes.InGameState;
            var container = inGame ? this.locator.InGameLayerContainer(stateObject) : this.focus.CurrentRoot;
            this.focus.LayerContainer = container;
            if (this.semantics != null)
            {
                this.semantics.LayerContainer = container;
            }

            this.screen.SetRoot(container);
            var chatLayer = inGame ? this.locator.InGameChatLayer(stateObject) : IntPtr.Zero;
            this.screen.SetIgnoredElement(chatLayer);
            this.chat?.Tick(chatLayer);
            this.mapOverlay?.Tick(inGame ? this.locator.InGameMapOverlay(stateObject) : IntPtr.Zero);
            this.longText?.Tick(this.screen.LongText);
            this.narrator.TutorialLayer = inGame ? this.locator.InGameTutorialLayer(stateObject) : IntPtr.Zero;
            this.screen.SetArrivalLayer(this.narrator.TutorialLayer);
            this.narrator.HudLayer = inGame ? this.locator.InGameHudLayer(stateObject) : IntPtr.Zero;
            this.skillBar = inGame ? this.locator.InGameSkillBar(stateObject) : IntPtr.Zero;
            this.narrator.ReadTooltips = this.Settings.ReadTooltipsOnFocus;
            this.narrator.Tick();
            // Another window in front (a notification, alt-tab): what the game redraws meanwhile is not news, and is
            // taken in silently so coming back does not read it (tooltips read out on a notification, 2026-10-26).
            var watching = !this.gameState.IsAttached || Core.Process.Foreground;
            while (this.screen.TryDequeue(out var change))
            {
                this.log?.Info($"Screen {change.Kind} layer {change.Layer.Index}: {string.Join(" | ", change.Texts.Select(text => text.Button.Length > 0 ? $"{text.Text} [{text.Button}]" : text.Text))}");
                this.deaths?.Saw(change.Layer.Address);
                if (watching && (change.Kind == ScreenChangeKind.TopLayerChanged || this.Settings.AnnounceAppearingText))
                {
                    this.narrator.OnScreenChange(change, this.screen.Current, this.focus.Current, this.focus.CurrentLayer);
                }
            }

            if (watching && !this.wasWatching)
            {
                this.narrator.CheckQuestPage(this.screen.Current); // an objective that changed while another window was in front
            }

            this.wasWatching = watching;

            if (this.gameState.CurrentState is GameStateTypes.LoginState or GameStateTypes.SelectCharacterState or
                    GameStateTypes.CreateCharacterState or GameStateTypes.DeleteCharacterState && this.Settings.AnnounceAppearingText && watching)
            {
                this.narrator.OnPreGameHints(this.screen.Current);
            }
        }

        /// <summary>Per frame: the mod button's gestures from the pad state the game received.</summary>
        private void ReadModButton()
        {
            this.padState = default;
            if (this.pad == null || this.focus == null)
            {
                return;
            }

            if (this.focus.Manager != this.padManager)
            {
                this.padManager = this.focus.Manager;
                this.inputSeat.Baseline();
            }

            GamepadReader.PadState? first = this.pad.TryRead(this.focus.Manager, out var one) ? one : null;
            GamepadReader.PadState? second = this.Settings.ReadPlayer2 && this.pad.TryRead(this.focus.Manager, out var two, 2) ? two : null;
            var seat = this.inputSeat.Update(this.Settings.ReadPlayer1, this.Settings.ReadPlayer2,
                first is { } a ? (a.Buttons, a.Deflection) : null, second is { } b ? (b.Buttons, b.Deflection) : null);
            if (seat != Seats.Input)
            {
                this.log?.Info($"Local co-op: reading player {seat}'s controller and cursor");
                Seats.Input = seat;
                this.focus.Seat = seat;
                this.gestures = new L3Gestures(); // a hold on the other controller is not this one's tap
            }

            if ((seat == 2 ? second : first) is not { } state)
            {
                return;
            }

            this.padState = state;
            this.CheckPadButtons(state);
            if (IsDirection(state))
            {
                this.lastDirectionAt = Environment.TickCount64;
            }

            // A opens screens and popups, LB/RB switch tabs: a focus that follows them waits for the new page.
            var confirm = state.IsDown(GameLayout.PadButtons.A) || state.IsDown(GameLayout.PadButtons.LeftShoulder) ||
                          state.IsDown(GameLayout.PadButtons.RightShoulder);
            if (confirm && !this.confirmDown)
            {
                this.lastConfirmAt = Environment.TickCount64;
                if (!state.IsDown(GameLayout.PadButtons.A))
                {
                    this.narrator?.OnTabKey();
                }
            }

            this.confirmDown = confirm;
            if (!this.Settings.ControllerCommands || this.commands == null)
            {
                return;
            }

            this.gestures.FlicksWithoutL3 = this.commands.InSettingsMenu;
            if (this.commands.InSettingsMenu && this.focus?.Current != null)
            {
                this.commands.CloseSettingsMenu();
            }

            foreach (var gesture in this.gestures.Update(Environment.TickCount64, state.IsDown(GameLayout.PadButtons.LeftStick), state.RightX, state.RightY, state.IsDown(GameLayout.PadButtons.RightStick)))
            {
                this.commands.OnGesture(gesture);
            }
        }

        /// <summary>A d-pad direction or a pushed stick: what moves the cursor in a panel.</summary>
        private static bool IsDirection(GamepadReader.PadState state) =>
            state.IsDown(GameLayout.PadButtons.DPadUp | GameLayout.PadButtons.DPadDown | GameLayout.PadButtons.DPadLeft | GameLayout.PadButtons.DPadRight) ||
            MathF.Max(MathF.Max(MathF.Abs(state.LeftX), MathF.Abs(state.LeftY)), MathF.Max(MathF.Abs(state.RightX), MathF.Abs(state.RightY))) >= DirectionDeflection;

        /// <summary>
        ///     A text field gave the focus up (pre-game the controller cursor then rests on nothing, D2R "B ends editing"):
        ///     say what it holds and that editing ended, since nothing else changes on screen for the player.
        /// </summary>
        private void WatchTextFieldClosing()
        {
            var current = this.focus?.Current;
            if (current == null && this.previousFocus is { Role: UiRole.TextField } field &&
                this.gameState?.CurrentState != GameStateTypes.InGameState)
            {
                this.output.Speak(Announcements.EditFinished(field.Value), true);
            }

            this.previousFocus = current;
        }

        private void RepeatLast()
        {
            if (this.focus?.Current != null)
            {
                this.output.Speak(Announcements.Focused(this.focus.Current, this.Settings.ReadTooltipsOnFocus), true);
            }
            else
            {
                this.output.Repeat();
            }
        }

        private void OnAttachedChanged(bool attached)
        {
            this.processProbe?.Reset();
            if (attached)
            {
                this.NoteGameBuild();

                // The game asks for a restart to change its language: a game started again under a running host speaks
                // the new one (a fresh install starts in English; Español applies on the next start, 2026-10-24).
                Localization.Detect(this.Settings.Language, message => this.log?.Info(message));
            }

            // With state announcements on, "Game detected" and the screen follow at once and would cut the attach line off.
            if (!attached || !this.Settings.AnnounceGameState)
            {
                this.output.Speak(attached ? Announcements.GameAttached : Announcements.GameNotAttached, false);
            }
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

        /// <summary>
        ///     The creation screen's name field takes typing at once and nothing on screen says how to finish: once per
        ///     visit, after the field, how to create the character (user, 2026-10-24).
        /// </summary>
        private void OnCreationFocus(AccessibleUiNode node)
        {
            if (!this.creationHelpSaid && node.Role == UiRole.TextField && this.gameState?.CurrentState == GameStateTypes.CreateCharacterState)
            {
                this.creationHelpSaid = true;
                this.narrator?.SayAfterFocus(Announcements.CreationNameHelp);
            }
        }

        private void OnGameStateChanged(GameStateTypes previous, GameStateTypes current)
        {
            this.research?.OnStateChanged(current);
            if (current == GameStateTypes.SelectCharacterState)
            {
                this.creationHelpSaid = false;
            }

            if (current == GameStateTypes.AreaLoadingState)
            {
                // A respawn reloads the same area without an area change; its barriers come back reading open for a
                // moment ("Se abrió el paso" after a death, 2026-10-24): learnt again without a word.
                this.barriers?.Reset();
            }

            this.screen?.Reset(false);
            this.narrator?.Reset(current == GameStateTypes.InGameState);
            if (this.narrator != null)
            {
                // The game menu's screen carries the pause banner and a loading tip: its focus alone opens it.
                this.narrator.ReadScreenMessages = current != GameStateTypes.EscapeState;
                this.narrator.SaidTitle = current == GameStateTypes.EscapeState ? Announcements.ForState(current) : string.Empty;
            }
            if (this.semantics != null)
            {
                this.semantics.InGame = current == GameStateTypes.InGameState;
            }

            if (previous == GameStateTypes.EscapeState && current == GameStateTypes.InGameState)
            {
                // Back to play from the game menu: its long pause banner stops (user 2026-10-28). Options opened from
                // the menu are read after this, as their page arrives.
                this.output.Stop();
            }

            if (!this.Settings.AnnounceGameState)
            {
                return;
            }

            if (current == GameStateTypes.GameNotLoaded)
            {
                // Leaving the game for character selection passes through "not loaded" for a moment: only a state
                // that lasts is the game closing.
                this.closedSince = Environment.TickCount64;
                return;
            }

            if (previous == GameStateTypes.EscapeState && current == GameStateTypes.InGameState)
            {
                return; // the game menu closed (D2R: closing is not news), or Options opened from it: its page speaks
            }

            var reallyClosed = previous == GameStateTypes.GameNotLoaded && this.closedSince == 0;
            this.closedSince = 0;
            if (previous == GameStateTypes.AreaLoadingState && current == GameStateTypes.InGameState && this.pendingState?.Detected != true)
            {
                // A new area's name follows at once and replaces it ("En juego" before every area name was noise,
                // 2026-10-25); a respawn in the same area keeps it as the end of the load.
                this.pendingState = (Announcements.ForState(current), false, Environment.TickCount64, false);
                return;
            }

            // Taking an exit, the game's own "Entrando a Sierraclara..." was cut off by "Cargando área" (2026-10-25).
            var queued = previous == GameStateTypes.InGameState && current == GameStateTypes.AreaLoadingState;
            this.pendingState = (Announcements.ForState(current), reallyClosed || this.pendingState?.Detected == true, Environment.TickCount64, queued);
            if (!PassingStates.Contains(current))
            {
                this.pendingState = this.pendingState.Value with { At = long.MinValue / 2 };
                this.AnnounceSettledState();
            }
        }

        /// <summary>Per frame: the state reached, once it has settled; a passing one is replaced unsaid.</summary>
        private void AnnounceSettledState()
        {
            if (this.pendingState is not { } pending || Environment.TickCount64 - pending.At < StateSettleMs)
            {
                return;
            }

            this.pendingState = null;
            // Held: the screen's first focus cut "Menú del juego." and "Selección de personaje." 300 ms in (2026-10-26).
            var text = pending.Detected ? $"{Announcements.GameDetected} {pending.Text}" : pending.Text;
            this.output.Keep(text, SpeechFloor.MessageHoldMs(text), !pending.Queued, yieldsToMoves: true);
        }

        /// <summary>Per frame: "Game closed" once the game has stayed unloaded for a while.</summary>
        private void AnnounceLastingClose()
        {
            if (this.closedSince != 0 && Environment.TickCount64 - this.closedSince >= ClosedSettleMs)
            {
                this.closedSince = 0;
                if (this.Settings.AnnounceGameState)
                {
                    this.output.Speak(Announcements.ForState(GameStateTypes.GameNotLoaded), true);
                }
            }
        }

        private void OnAreaChanged(string areaName, bool resumed)
        {
            this.beacon?.Clear(false);
            this.bosses?.Reset();
            this.weakPoint?.Reset();
            this.shield?.Reset();
            this.culls?.Reset();
            this.deployed?.Reset();
            this.enemyStates?.Reset();
            this.alerts?.Statuses.Reset();
            this.partnerAlerts?.Statuses.Reset();
            this.alerts?.Charges.Reset();
            this.partnerAlerts?.Charges.Reset();
            this.pickups?.Reset();
            this.partnerPickups?.Reset();
            this.gold?.Reset();
            this.flasks?.Reset();
            this.partnerFlasks?.Reset();
            this.coopDowns.Reset();
            this.barriers?.Reset();
            this.targets?.Reset();
            this.discoveries?.Reset(areaName, resumed);
            this.enemyNews?.Reset();
            this.entities?.Forget(Core.States.InGameStateObject.CurrentAreaInstance.AreaHash, areaName);
            this.unexploredLoop?.Reset();
            this.nearby?.Reset();
            this.places?.Reset();
            this.narrator?.NewArea(resumed);
            if (this.Settings.AnnounceArea)
            {
                if (this.pendingState is { Detected: false } loaded && loaded.Text == Announcements.ForState(GameStateTypes.InGameState))
                {
                    this.pendingState = null;
                }

                var level = AreaLevel();
                this.log?.Info($"Area level {level} (instance {Core.States.InGameStateObject.CurrentAreaInstance.AreaHash})");
                this.output.Say(SpeechKind.Places, Announcements.Area(areaName, level));
            }
        }

    }
}
