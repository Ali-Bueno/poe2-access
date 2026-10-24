namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using ClickableTransparentOverlay.Win32;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteEnums;
    using ImGuiNET;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>
    ///     GameHelper2 plugin entry point. Thin by design: it wires services together and forwards the
    ///     per-frame tick and hotkeys; all behaviour lives in the services.
    /// </summary>
    public sealed class Poe2AccessibilityCore : PCore<AccessibilitySettings>
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
        private EntityDump? entityDump;
        private TerrainDump? terrainDump;
        private ActorTrace? actorTrace;
        private EntityCatalog? entities;
        private GamepadReader? pad;
        private ModCommands? commands;
        private readonly L3Gestures gestures = new();
        private AudioCueService? audio;

        /// <summary>Sound volumes are percents; above double the synthesized level the cues clip.</summary>
        private const int MaxSoundVolume = 200;
        private EnemyCues? enemyCues;
        private Radar? radar;
        private ProximityNarrator? nearby;
        private WallBump? wallBump;
        private WallTones? wallTones;
        private GamepadReader.PadState padState;
        private (string Character, int Level) lastLevel;
        private readonly System.Collections.Generic.Dictionary<string, long> tickFaults = new();
        private HostMemoryReader? memoryReader;

        /// <summary>A failing sound tick is logged once a minute, not every frame.</summary>
        private const int SoundErrorReportMs = 60000;

        /// <summary>Right stick deflection that counts as aiming (the target is named only then).</summary>
        private const float AimDeflection = 0.3f;

        /// <summary>Half and a quarter; re-armed 10 points above.</summary>
        private readonly VitalAlarm lifeAlarm = new(10, 50, 25);

        /// <summary>
        ///     Half, a quarter and a tenth; re-armed 30 points above: mana is spent and refilled constantly, and at 10 the
        ///     half was said every 8 s through the Miller fight (2026-10-24).
        /// </summary>
        private readonly VitalAlarm manaAlarm = new(30, 50, 25, 10);
        private Beacon? beacon;
        private TargetNarrator? targets;
        private BossWatch? bosses;
        private ObjectSounds? objectSounds;
        private StatusWatch? statuses;
        private InventoryWatch? pickups;
        private GoldWatch? gold;
        private BarrierWatch? barriers;
        private DiscoveryNarrator? discoveries;
        private EnemyNarrator? enemyNews;
        private DeathWatch? deaths;

        /// <summary>Game sounds are cut to this length: repeated proximity cues must stay short.</summary>
        private const float GameSoundSeconds = 0.9f;

        /// <summary>The focus of the previous frame, to notice a text field closing (pre-game focus goes to nothing).</summary>
        private AccessibleUiNode? previousFocus;

        /// <summary>When A (the confirm button) last went down, from the game's own pad state.</summary>
        private long lastConfirmAt = long.MinValue / 2;

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

        private static readonly System.Collections.Generic.HashSet<GameStateTypes> PassingStates = new()
        {
            GameStateTypes.SelectCharacterState, GameStateTypes.WaitingState, GameStateTypes.LoadingState,
        };

        /// <summary>The state announcement waiting out <see cref="StateSettleMs"/>; Detected keeps "Game detected" when replaced.</summary>
        private (string Text, bool Detected, long At)? pendingState;
        private bool confirmDown;

        /// <summary>When each kind of monster attack lands, learnt from the blows taken (BossWatch).</summary>
        private string DodgeTimingsPath => Path.Join(this.DllDirectory, "config", "dodge-timings.txt");

        /// <summary>Timings measured in development, shipped so the first fights are timed too.</summary>
        private string DodgeTimingsSeedPath => Path.Join(this.DllDirectory, "catalog", "dodge-timings.txt");

        /// <summary>The creation screen's name help was said for this visit (a refused name comes back to it).</summary>
        private bool creationHelpSaid;

        /// <summary>The HUD's skill bar this frame (zero outside gameplay or when not found).</summary>
        private IntPtr skillBar;

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
                this.compatibility = new CompatibilityReport(this.log);
                this.layoutProbe = new LayoutProbe(memory, elements, this.compatibility);
                var probe = this.layoutProbe;
                this.controls = new ControlFields(this.compatibility, () => probe.TextShift);
                elements.Controls = this.controls;
                this.locator = new UiManagerLocator(memory, elements, this.log, this.layoutProbe, new InGameFields(memory, elements, this.compatibility));
                var inventory = new InventoryReader(memory, elements) { Controls = this.controls };
                this.semantics = new UiSemantics(memory, elements, catalog, new ComboBoxReader(elements, this.log), inventory);
                this.semantics.Trace = message => this.log.Debug(message);
                this.semantics.Fields = this.locator.Fields;
                this.focus = new FocusTracker(memory, elements, this.locator, this.semantics, this.log);
                this.narrator = new ScreenNarrator(this.output, this.log, layer => this.semantics?.SelectedTab(layer) ?? string.Empty);
                this.narrator.FocusLayer = () => this.focus?.CurrentLayer ?? IntPtr.Zero;
                this.narrator.SinceConfirm = () => Environment.TickCount64 - this.lastConfirmAt;
                this.focus.FocusChanged += this.narrator.OnFocusChanged;
                this.focus.ValueChanged += this.narrator.OnValueChanged;
                this.focus.TooltipChanged += this.narrator.OnTooltipChanged;
                this.pad = new GamepadReader(memory);
                this.entityDump = new EntityDump(elements, memory, this.log, debugDirectory);
                this.terrainDump = new TerrainDump(memory, this.log, debugDirectory);
                this.actorTrace = new ActorTrace(memory, this.log, debugDirectory);
                ItemNames.Load(this.log, Path.Join(this.DllDirectory, "catalog", "item-names.json"));
                // GameHelper's Radar table; a test package ships only this plugin, with a copy of it in its catalog.
                var landmarks = LandmarkTable.Load(
                    this.log,
                    Path.Join(this.DllDirectory, "..", "Radar", "important_tgt_files.txt"),
                    Path.Join(this.DllDirectory, "catalog", "important_tgt_files.txt"),
                    Path.Join(this.DllDirectory, "catalog", "landmarks.json"));
                this.entities = new EntityCatalog(memory, elements, inventory, landmarks, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log)
                {
                    Probe = this.layoutProbe,
                    Report = this.compatibility,
                };
                var checkpointPlaces = new CheckpointPlaces(this.entities);
                this.semantics.MarkerPlaces = checkpointPlaces.Describe;
                this.audio = new AudioCueService(this.log);
                this.enemyCues = new EnemyCues(this.audio, memory, this.log);
                this.radar = new Radar(this.audio, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log);
                this.nearby = new ProximityNarrator(this.entities, this.output);
                this.wallBump = new WallBump(this.audio) { HeldStill = () => this.narrator?.RepeatLesson() };
                this.wallTones = new WallTones(this.audio);
                this.beacon = new Beacon(this.audio, this.entities, this.output, this.log);
                this.targets = new TargetNarrator(this.audio, memory, elements, this.output, this.log) { HudLayer = () => this.narrator?.HudLayer ?? IntPtr.Zero };
                this.narrator.IgnoredHudText = () => this.targets?.NameLabel ?? IntPtr.Zero;
                this.narrator.GoldCounter = () => this.gold?.Counter ?? IntPtr.Zero;
                this.narrator.GroundItemName = texts => this.entities?.ItemOfTooltip(texts);
                this.statuses = new StatusWatch(this.output, this.log);
                this.pickups = new InventoryWatch(inventory, this.output, this.log);
                this.gold = new GoldWatch(memory, elements, this.output, this.log) { Fields = this.locator.Fields };
                this.barriers = new BarrierWatch(this.entities, this.output, this.log);
                this.bosses = new BossWatch(this.audio, this.output, elements, this.log) { Timings = DodgeTimings.Load(this.DodgeTimingsSeedPath, this.DodgeTimingsPath), TimingsPath = this.DodgeTimingsPath };
                var gameSounds = GameSounds.Load(Path.Join(this.DllDirectory, "sounds"), GameSoundSeconds, this.log);
                var drops = ItemDropSounds.Load(
                    Path.Join(this.DllDirectory, "catalog", "item-sounds.json"), Path.Join(this.DllDirectory, "sounds", "items"), GameSoundSeconds, this.log);
                this.objectSounds = new ObjectSounds(this.audio, this.entities, gameSounds, drops);
                this.discoveries = new DiscoveryNarrator(this.entities, this.output, this.audio, gameSounds, new KnownPlaces(Path.Join(this.DllDirectory, "config", "known-places.txt")));
                this.enemyNews = new EnemyNarrator(this.entities, this.output);
                this.deaths = new DeathWatch(this.locator, elements, this.output, this.log);
                this.ApplySoundVolumes(this.Settings);
                this.commands = new ModCommands(this.output, this.focus, this.semantics, this.narrator, () => this.screen?.Current ?? ScreenSnapshot.Empty, this.entities, this.beacon, this.ReadVitals, this.log);
                this.screen = new ScreenWatcher(elements, this.log);
                this.entities.QuestTexts = this.QuestTexts;
                this.entities.AreaName = () => this.gameState?.CurrentAreaName ?? string.Empty;
                this.commands.UseGlossary(new SoundGlossary(this.audio, gameSounds));
                this.commands.Settings = () => this.Settings;
                this.commands.SkillBar = this.ReadSkillBar;
                SpeechPriority.Chosen = this.SpeechLevel;
                this.narrator.InPanel = this.semantics.IsInPanel;
                this.narrator.InOtherControlGroup = this.semantics.InOtherControlGroup;
                this.narrator.SelectorPosition = this.semantics.SelectorPosition;
                this.narrator.SelectorChoice = this.semantics.SelectorChoice;
                this.narrator.IsSkillBarText = this.IsSkillBarText;
                this.focus.FocusChanged += this.OnCreationFocus;
                this.narrator.Refresh = node => this.semantics?.Describe(node.Address, node.IsHovered);
                this.semantics.PlayerPath = () => Core.States.InGameStateObject.CurrentAreaInstance.Player.Path ?? string.Empty;
                this.commands.SettingsChanged = changed =>
                {
                    this.ApplySettings(changed);
                    this.settingsStore?.Save(changed);
                };
                this.chat = new ChatWatcher(elements, line =>
                {
                    if (this.Settings.AnnounceChat)
                    {
                        this.output.Speak(line, false);
                    }
                })
                {
                    PanelChanged = text => this.output.Speak(text, true),
                };
                this.mapOverlay = new MapOverlayWatch(elements, text => this.output.Speak(text, true));
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
            this.nearby = null;
            this.wallBump = null;
            this.wallTones = null;
            this.audio?.Dispose();
            this.audio = null;

            this.output.Dispose();
            this.output = new NullOutputService();
            this.log?.Info("Disabled.");
        }

        /// <inheritdoc />
        public override void DrawUI()
        {
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
                this.Isolated("life", this.WatchLife);
                this.Isolated("sounds", this.TickSounds);
                this.Isolated("screen", () => this.NarrateScreenChanges(stateObject));
                if (state == GameStateTypes.InGameState)
                {
                    this.Isolated("death", () => this.deaths?.Tick(stateObject));
                    this.Isolated("trace", () => this.actorTrace?.Tick());
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
            this.screen.SetRoot(container);
            var chatLayer = inGame ? this.locator.InGameChatLayer(stateObject) : IntPtr.Zero;
            this.screen.SetIgnoredElement(chatLayer);
            this.chat?.Tick(chatLayer);
            this.mapOverlay?.Tick(inGame ? this.locator.InGameMapOverlay(stateObject) : IntPtr.Zero);
            this.narrator.TutorialLayer = inGame ? this.locator.InGameTutorialLayer(stateObject) : IntPtr.Zero;
            this.screen.SetArrivalLayer(this.narrator.TutorialLayer);
            this.narrator.HudLayer = inGame ? this.locator.InGameHudLayer(stateObject) : IntPtr.Zero;
            this.skillBar = inGame ? this.locator.InGameSkillBar(stateObject) : IntPtr.Zero;
            this.narrator.ReadTooltips = this.Settings.ReadTooltipsOnFocus;
            this.narrator.Tick();
            while (this.screen.TryDequeue(out var change))
            {
                this.log?.Info($"Screen {change.Kind} layer {change.Layer.Index}: {string.Join(" | ", change.Texts.Select(text => text.Button.Length > 0 ? $"{text.Text} [{text.Button}]" : text.Text))}");
                this.deaths?.Saw(change.Layer.Address);
                if (change.Kind == ScreenChangeKind.TopLayerChanged || this.Settings.AnnounceAppearingText)
                {
                    this.narrator.OnScreenChange(change, this.screen.Current, this.focus.Current, this.focus.CurrentLayer);
                }
            }

            if (this.gameState.CurrentState is GameStateTypes.LoginState or GameStateTypes.SelectCharacterState or
                    GameStateTypes.CreateCharacterState or GameStateTypes.DeleteCharacterState && this.Settings.AnnounceAppearingText)
            {
                this.narrator.OnPreGameHints(this.screen.Current);
            }
        }

        /// <summary>Per frame: the mod button's gestures from the pad state the game received.</summary>
        private void ReadModButton()
        {
            this.padState = default;
            if (this.pad == null || this.focus == null || !this.pad.TryRead(this.focus.Manager, out var state))
            {
                return;
            }

            this.padState = state;
            // A opens screens and popups, LB/RB switch tabs: a focus that follows them waits for the new page.
            var confirm = state.IsDown(GameLayout.PadButtons.A) || state.IsDown(GameLayout.PadButtons.LeftShoulder) ||
                          state.IsDown(GameLayout.PadButtons.RightShoulder);
            if (confirm && !this.confirmDown)
            {
                this.lastConfirmAt = Environment.TickCount64;
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


        /// <summary>
        ///     Per frame: radar, enemy sounds and what is nearby, only in gameplay. Each service runs on its own (D2R
        ///     crash containment): one that throws is logged (throttled per service) and the others still run.
        /// </summary>
        private void TickSounds()
        {
            bool playing;
            try
            {
                playing = Core.Process.Foreground && this.focus?.Current == null &&
                          this.gameState?.CurrentState == GameStateTypes.InGameState && IsPlayerAlive();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault("gameplay state", ex);
                return;
            }

            this.Isolated("enemy sounds", () =>
            {
                if (this.enemyCues != null)
                {
                    this.enemyCues.Enemies = this.Settings.EnemySounds;
                    this.enemyCues.Friends = this.Settings.NpcSounds;
                    this.enemyCues.Tick(playing && (this.Settings.EnemySounds || this.Settings.NpcSounds || this.Settings.MeleeReach));
                }
            });
            this.Isolated("radar", () => this.radar?.Tick(playing && this.Settings.Radar));
            this.Isolated("wall tones", () => this.wallTones?.Tick(playing && this.Settings.WallTones));
            this.Isolated("nearby", () => this.nearby?.Tick(playing, this.Settings.AnnounceNearby, this.Settings.AnnounceInteractTarget));
            this.Isolated("exploration", () =>
            {
                if (playing)
                {
                    this.entities?.Track();
                }
            });
            this.Isolated("beacon", () =>
            {
                if (this.beacon != null)
                {
                    this.beacon.TurnPrompts = this.Settings.RoutePrompts;
                    this.beacon.Tick(playing);
                }
            });
            this.Isolated("target", () =>
            {
                var aiming = !this.padState.IsDown(GameLayout.PadButtons.LeftStick) && !(this.commands?.InSettingsMenu ?? false) &&
                             MathF.Max(MathF.Abs(this.padState.RightX), MathF.Abs(this.padState.RightY)) >= AimDeflection;
                if (this.targets != null)
                {
                    this.targets.Names = this.Settings.AnnounceTarget;
                    this.targets.ModNames = this.Settings.AnnounceMonsterMods;
                    this.targets.Beep = this.Settings.CrosshairBeep;
                    this.targets.Tick(playing && (this.Settings.AnnounceTarget || this.Settings.CrosshairBeep), aiming);
                }
            });
            this.Isolated("states", () => this.statuses?.Tick(playing && this.Settings.StatusAlerts));
            if (this.gameState?.CurrentState == GameStateTypes.InGameState)
            {
                this.Isolated("pick-ups", () => this.pickups?.Tick(playing && this.Settings.AnnouncePickups));
                this.Isolated("hand", () => this.pickups?.TickHand(this.Settings.AnnounceHand));
                this.Isolated("moves", () => this.pickups?.TickMoves(playing, this.Settings.AnnounceMoves));
                this.Isolated("gold", () => this.gold?.Tick(this.focus?.LayerContainer ?? IntPtr.Zero, playing, this.Settings.AnnouncePickups));
                this.Isolated("barriers", () => this.barriers?.Tick(this.Settings.AnnounceDiscoveries));
            }

            this.Isolated("bosses", () =>
            {
                if (this.bosses != null)
                {
                    this.bosses.AnnounceLife = this.Settings.AnnounceBossLife;
                    this.bosses.DodgeWarnings = this.Settings.DodgeWarnings;
                    this.bosses.Tick(playing);
                }
            });
            this.Isolated("object sounds", () => this.objectSounds?.Tick(playing && this.Settings.ObjectSounds));
            this.Isolated("discoveries", () => this.discoveries?.Tick(playing && this.Settings.AnnounceDiscoveries));
            this.Isolated("enemy news", () => this.enemyNews?.Tick(playing && this.Settings.AnnounceEnemies));
            this.Isolated("wall bump", () =>
            {
                if (playing)
                {
                    this.wallBump?.Tick(this.padState.LeftX, this.padState.LeftY, this.Settings.WallBump);
                }
            });
            this.Isolated("level", this.WatchLevel);
        }

        /// <summary>Runs one gameplay service; an exception is logged (throttled per service) and the frame goes on.</summary>
        private void Isolated(string service, Action tick)
        {
            try
            {
                tick();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault(service, ex);
            }
        }

        private void ReportTickFault(string service, Exception ex)
        {
            var now = Environment.TickCount64;
            if (!this.tickFaults.TryGetValue(service, out var last) || now - last >= SoundErrorReportMs)
            {
                this.tickFaults[service] = now;
                this.log?.Error($"Tick failed: {service}", ex);
            }
        }

        /// <summary>"Level 3" when the character levels up; the first reading of a character is only remembered.</summary>
        private void WatchLevel()
        {
            var player = Core.States.InGameStateObject.CurrentAreaInstance.Player;
            if (!player.TryGetComponent<GameHelper.RemoteObjects.Components.Player>(out var character) || character.Level <= 0)
            {
                return;
            }

            if (this.lastLevel.Character == character.Name && character.Level > this.lastLevel.Level && this.Settings.AnnounceLevelUp)
            {
                this.output.Speak(Announcements.LevelUp(character.Level), false);
            }

            this.lastLevel = (character.Name, character.Level);
        }

        private static bool IsPlayerAlive() =>
            Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) && life.IsAlive;

        /// <summary>Per frame: a warning when the player's life falls below a threshold.</summary>
        private void WatchLife()
        {
            if (!this.Settings.AnnounceLowLife || this.gameState?.CurrentState != GameStateTypes.InGameState ||
                !Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) ||
                !life.IsAlive || life.Health.Total <= 0)
            {
                return;
            }

            if (this.lifeAlarm.Update(life.Health.CurrentInPercent()) is { } threshold)
            {
                this.output.Say(SpeechKind.LifeAndMana, Announcements.LowLife(threshold), threshold <= this.Settings.LowLifeBelow);
            }

            if (this.Settings.AnnounceLowMana && life.Mana.Total > 0 && this.manaAlarm.Update(life.Mana.CurrentInPercent()) is { } manaThreshold)
            {
                // Mana runs low in every fight: urgent only when it is nearly gone.
                this.output.Say(SpeechKind.LifeAndMana, Announcements.LowMana(manaThreshold), manaThreshold <= 10);
            }
        }

        /// <summary>"Life 80 percent, mana 40 percent" for the mod button; null outside gameplay.</summary>
        private string? ReadVitals()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState ||
                !Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) ||
                life.Health.Total <= 0)
            {
                return null;
            }

            var vitals = Announcements.Vitals(
                life.Health.CurrentInPercent(),
                life.Mana.Total > 0 ? life.Mana.CurrentInPercent() : 0,
                life.EnergyShield.Total > 0 ? life.EnergyShield.CurrentInPercent() : null);
            var states = this.statuses?.Active.Select(Announcements.AilmentName).ToList() ?? new System.Collections.Generic.List<string>();
            return Announcements.Sentences(new[] { vitals, string.Join(", ", states), this.bosses?.Status() ?? string.Empty }.Where(part => part.Length > 0));
        }

        /// <summary>The delivery level the player chose for a kind of news (speech priority).</summary>
        private string SpeechLevel(SpeechKind kind) => kind switch
        {
            SpeechKind.StatusBegins => this.Settings.SpeechStatusBegins,
            SpeechKind.StatusEnds => this.Settings.SpeechStatusEnds,
            SpeechKind.LifeAndMana => this.Settings.SpeechLifeAndMana,
            SpeechKind.BossLife => this.Settings.SpeechBossLife,
            SpeechKind.EnemyNames => this.Settings.SpeechEnemyNames,
            SpeechKind.Quest => this.Settings.SpeechQuest,
            SpeechKind.Places => this.Settings.SpeechPlaces,
            SpeechKind.GroundItems => this.Settings.SpeechGroundItems,
            _ => this.Settings.SpeechArrivals,
        };

        /// <summary>The skill bar's sets in screen order (D2R skill bar readout); null outside gameplay or when not found.</summary>
        private string? ReadSkillBar()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState || this.semantics == null || this.focus == null)
            {
                return null;
            }

            var sets = this.semantics.ReadSkillBar(this.focus.LayerContainer);
            return sets.Count == 0 ? null : Announcements.Sentences(sets.Select(set => Announcements.SkillSet(set.Title, set.Bindings)));
        }

        /// <summary>The HUD's texts (quest tracker among them) for the scanner's Quest category.</summary>
        private System.Collections.Generic.IReadOnlyList<string> QuestTexts()
        {
            var hud = this.narrator == null ? null : this.screen?.Current.Find(this.narrator.HudLayer);
            return hud?.Texts.Select(text => text.Text).ToList() ?? (System.Collections.Generic.IReadOnlyList<string>)Array.Empty<string>();
        }

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

        private void RunDevCommand(string command)
        {
            if (command.StartsWith("gesture ", StringComparison.Ordinal) &&
                Enum.TryParse<PadGesture>(command["gesture ".Length..], true, out var gesture))
            {
                // Test aid: a mod-button gesture without the pad (L3 + R3 cannot be chorded by the virtual pad).
                this.commands?.OnGesture(gesture);
                return;
            }

            if (command.StartsWith("trace ", StringComparison.Ordinal) && int.TryParse(command["trace ".Length..], out var seconds))
            {
                this.actorTrace?.Start(seconds);
                return;
            }

            if (command.StartsWith("hex ", StringComparison.Ordinal) && this.memoryReader != null)
            {
                // Research: "hex 0x46656B1D030 0x800" logs that memory, 32 bytes per line, to diff two game states.
                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 && long.TryParse(parts[1].Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out var at) &&
                    int.TryParse(parts[2].Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out var length))
                {
                    var bytes = this.memoryReader.ReadBytes(new IntPtr(at), Math.Clamp(length, 0, 0x4000));
                    for (var offset = 0; offset < bytes.Length; offset += 32)
                    {
                        this.log?.Info($"hex 0x{at:X}+{offset:X4}: {Convert.ToHexString(bytes, offset, Math.Min(32, bytes.Length - offset))}");
                    }
                }

                return;
            }

            if (command.StartsWith("dump 0x", StringComparison.Ordinal))
            {
                // Research: "dump 0x4DD5032C310" dumps the tree under that element, for popups no root holds (the
                // menu A opens on another player is reached only from the focus, 2026-10-24).
                this.DumpUiTree(false, command["dump ".Length..]);
                return;
            }

            switch (command)
            {
                case "dump":
                    this.DumpUiTree(false);
                    break;
                case "dump visible":
                    this.DumpUiTree(true);
                    break;
                case "repeat":
                    this.RepeatLast();
                    break;
                case "status":
                    this.SpeakStatus();
                    break;
                case "details":
                    // Re-read: an item's tooltip is built a moment after the cursor lands on it.
                    var current = this.focus?.Current;
                    var fresh = current == null ? null : this.semantics?.Describe(current.Address, current.IsHovered) ?? current;
                    this.output.Speak(fresh == null ? Announcements.NoDetails : Announcements.Details(fresh), true);
                    break;
                case "hints":
                    this.narrator?.SpeakHints(this.screen?.Current ?? ScreenSnapshot.Empty);
                    break;
                case "entities":
                    this.entityDump?.Write(80);
                    break;
                case "entities all":
                    // Research: every entity the client knows (the network bubble), e.g. far quest icons.
                    this.entityDump?.Write(int.MaxValue);
                    break;
                case "beacon":
                    // Test aid: where the guide's last ping pointed on screen (x right, y down), for scripted walking.
                    this.log?.Info(this.beacon?.LastAim is { } aim
                        ? FormattableString.Invariant($"Beacon aim {aim.Screen.X:F3},{aim.Screen.Y:F3} remaining {aim.RemainingWorld:F0}")
                        : "Beacon aim none");
                    var cell = Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Render>(out var me)
                        ? FormattableString.Invariant($"{me.GridPosition.X:F0},{me.GridPosition.Y:F0}")
                        : "?";
                    this.log?.Info($"Beacon state: {this.beacon?.Diagnose()}; cell={cell}; playing={Core.Process.Foreground && this.focus?.Current == null && IsPlayerAlive()} focus={this.focus?.Current?.Text}");
                    break;
                case "trace":
                    this.actorTrace?.Start();
                    break;
                case "terrain":
                    this.terrainDump?.Write();
                    if (this.entities != null && this.entities.TryLiveGrid(out var routed, out _))
                    {
                        // The grid routes are searched on (live samples combined), beside the raw layer just written.
                        var routedPath = Path.Join(this.DllDirectory, "debug", $"routed-{DateTime.Now:yyyyMMdd-HHmmss}.walkable");
                        File.WriteAllBytes(routedPath, routed);
                        this.log?.Info($"Routed grid written to {routedPath}");
                    }

                    break;
                case "enemies":
                    this.log?.Info(this.enemyCues?.Diagnose() ?? "Enemy cues off");
                    break;
                case "layout":
                    this.LayoutSelfTest();
                    break;
                case "screen":
                    this.narrator?.SpeakScreen(this.screen?.Current ?? ScreenSnapshot.Empty, this.focus?.CurrentLayer ?? IntPtr.Zero);
                    break;
                default:
                    this.log?.Warn($"Unknown dev command: {command}");
                    break;
            }
        }

        private void SpeakStatus()
        {
            if (this.gameState == null)
            {
                return;
            }

            var processReport = this.gameState.IsAttached ? string.Empty : this.processProbe?.Describe() ?? string.Empty;
            this.output.Speak(
                Announcements.Status(this.gameState.IsAttached, this.gameState.CurrentState, this.gameState.CurrentAreaName, this.output.BackendName, processReport),
                true);
        }

        /// <summary>Research (dev command "layout"): the layout searches started off target must find the layout in use.</summary>
        private void LayoutSelfTest()
        {
            if (this.gameState == null || this.locator == null || this.layoutProbe == null || this.memoryReader == null)
            {
                return;
            }

            var stateObject = GameStateService.CurrentStateObjectAddress();
            var manager = this.locator.Locate(this.gameState.CurrentState, stateObject);
            var root = this.memoryReader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var held) ? held : IntPtr.Zero;
            var result = $"Layout self-test ({this.gameState.CurrentState}): {this.layoutProbe.SelfTest(root, manager)}";
            if (this.gameState.CurrentState == GameStateTypes.InGameState)
            {
                result += $"; {this.locator.Fields.SelfTest(this.locator.InGameLayerContainer(stateObject))}";
            }

            result += $"; {this.gameState.SelfTestAreaRow()}; controls: {this.controls?.SelfTest()}";

            this.log?.Info(result);
        }

        /// <summary>Per frame: the parts of the game's layout that could not be found, said once together.</summary>
        private void SayCompatibility()
        {
            if (this.compatibility?.TakeUnsaid() is { Count: > 0 } missing)
            {
                this.output.Speak(Announcements.CompatibilityMissing(missing), false);
            }
        }

        /// <summary>Logs which build of the game this is; a new game process is checked against the layout again.</summary>
        private void NoteGameBuild()
        {
            this.compatibility?.Reset();
            this.layoutProbe?.Reset();
            this.locator?.Fields.Reset();
            this.controls?.Reset();
            this.gameState?.ResetLayout();
            this.locator?.Reset();
            try
            {
                using var game = System.Diagnostics.Process.GetProcessById((int)Core.Process.Pid);
                if (game.MainModule?.FileName is { Length: > 0 } path)
                {
                    var file = new FileInfo(path);
                    this.log?.Info($"Game build: {path}, {file.Length} bytes, written {file.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                this.log?.Debug($"Game build unreadable: {ex.Message}");
            }
        }

        private void OnAttachedChanged(bool attached)
        {
            this.processProbe?.Reset();
            if (attached)
            {
                this.NoteGameBuild();
            }

            this.output.Speak(attached ? Announcements.GameAttached : Announcements.GameNotAttached, false);
        }

        /// <inheritdoc />
        public override void DrawSettings()
        {
            ImGui.TextWrapped("Blind users: edit config/settings.txt directly (hotkeys by key name); it is reloaded automatically.");
            ImGui.Checkbox("Speech enabled (restart plugin to apply)", ref this.Settings.SpeechEnabled);
            ImGui.Checkbox("Announce game state changes", ref this.Settings.AnnounceGameState);
            ImGui.Checkbox("Announce area changes", ref this.Settings.AnnounceArea);
            ImGui.Checkbox("Read NPC, system, whisper and party chat", ref this.Settings.AnnounceChat);
            ImGui.Checkbox("Read whole tooltips when the focus lands", ref this.Settings.ReadTooltipsOnFocus);
            ImGui.Checkbox("L3 is the mod button (lines, details, hints, screen)", ref this.Settings.ControllerCommands);
            ImGui.Checkbox("Say when life falls below half and a quarter", ref this.Settings.AnnounceLowLife);
            if (ImGui.Checkbox("Verbose logging", ref this.Settings.VerboseLogging) && this.log != null)
            {
                this.log.Verbose = this.Settings.VerboseLogging;
            }

            ImGui.Checkbox("UI dump: visible elements only", ref this.Settings.UiDumpVisibleOnly);
            ImGui.Text($"Hotkeys: dump UI tree {this.Settings.DumpUiTreeHotKey}, repeat {this.Settings.RepeatLastHotKey}, status {this.Settings.SpeakStatusHotKey}");
            ImGui.Text($"Speech backend: {this.output.BackendName}");
        }

        /// <inheritdoc />
        public override void SaveSettings() => this.settingsStore?.Save(this.Settings);

        private void ApplySettings(AccessibilitySettings settings)
        {
            this.Settings = settings;
            if (this.log != null)
            {
                this.log.Verbose = settings.VerboseLogging;
            }

            if (this.research != null)
            {
                this.research.Enabled = settings.ResearchMode;
            }

            this.ApplySoundVolumes(settings);
        }

        private void ApplySoundVolumes(AccessibilitySettings settings)
        {
            static float Fraction(int percent) => Math.Clamp(percent, 0, MaxSoundVolume) / 100f;
            if (this.audio != null)
            {
                this.audio.Volume = Fraction(settings.SoundVolume);
            }

            if (this.radar != null)
            {
                this.radar.Gain = Fraction(settings.RadarVolume);
                this.radar.SmallObstacles = settings.SmallObstacles;
            }

            if (this.wallBump != null)
            {
                this.wallBump.Gain = Fraction(settings.RadarVolume);
            }

            if (this.wallTones != null)
            {
                this.wallTones.Gain = Fraction(settings.RadarVolume);
                this.wallTones.RangeWorld = Math.Clamp(settings.WallToneRange, 100, 3000);
            }

            if (this.enemyCues != null)
            {
                this.enemyCues.Gain = Fraction(settings.EnemyVolume);
                this.enemyCues.Reach = settings.MeleeReach;
                this.enemyCues.ReachWorld = Math.Clamp(settings.MeleeReachRange, 0, 500);
            }

            if (this.beacon != null)
            {
                this.beacon.Gain = Fraction(settings.BeaconVolume);
            }

            if (this.bosses != null)
            {
                this.bosses.Gain = Fraction(settings.EnemyVolume);
            }

            if (this.objectSounds != null)
            {
                this.objectSounds.Gain = Fraction(settings.ObjectVolume);
            }

            if (this.discoveries != null)
            {
                this.discoveries.Gain = Fraction(settings.ObjectVolume);
            }

            if (this.targets != null)
            {
                this.targets.Gain = Fraction(settings.EnemyVolume);
            }
        }

        /// <summary>The overlay library's key-timeout table is process-wide: a key the host consumes first never reaches us.</summary>
        private void WarnOnHotkeyCollisions()
        {
            var host = Core.GHSettings;
            var hostKeys = new (string Name, VK Key)[]
            {
                ("disable all rendering", host.DisableAllRenderingKey),
                ("element finder", host.ElementFinderHotKey),
                ("main menu", host.MainMenuHotKey),
            };
            var ours = new (string Name, VK Key)[]
            {
                ("dump UI tree", this.Settings.DumpUiTreeHotKey),
                ("repeat", this.Settings.RepeatLastHotKey),
                ("status", this.Settings.SpeakStatusHotKey),
            };
            foreach (var (ourName, ourKey) in ours)
            {
                foreach (var (hostName, hostKey) in hostKeys)
                {
                    if (ourKey == hostKey)
                    {
                        this.log?.Warn($"Hotkey collision: {ourKey} is used by GameHelper for '{hostName}'; our '{ourName}' will not fire. Change it in config/settings.txt.");
                        this.output.Speak(Announcements.HotkeyCollision(ourKey.ToString(), ourName), false);
                    }
                }
            }
        }

        /// <summary>A text element inside the HUD's skill bar (its alert line).</summary>
        private bool IsSkillBarText(IntPtr text) =>
            this.skillBar != IntPtr.Zero && this.elements != null && this.elements.TryReadElement(text, out var info) &&
            this.elements.ReadAncestors(info, SkillBarTextDepth).Any(ancestor => ancestor.Address == this.skillBar);

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
            }
            if (this.semantics != null)
            {
                this.semantics.InGame = current == GameStateTypes.InGameState;
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
            this.pendingState = (Announcements.ForState(current), reallyClosed || this.pendingState?.Detected == true, Environment.TickCount64);
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
            this.output.Speak(pending.Detected ? $"{Announcements.GameDetected} {pending.Text}" : pending.Text, true);
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
            this.statuses?.Reset();
            this.pickups?.Reset();
            this.gold?.Reset();
            this.barriers?.Reset();
            this.targets?.Reset();
            this.discoveries?.Reset(areaName, resumed);
            this.enemyNews?.Reset();
            this.entities?.Forget();
            this.nearby?.Reset();
            this.narrator?.NewArea(resumed);
            if (this.Settings.AnnounceArea)
            {
                this.output.Say(SpeechKind.Places, Announcements.Area(areaName));
            }
        }

        private void DumpUiTree(bool visibleOnly, string? root = null)
        {
            if (this.dumper == null || this.gameState == null)
            {
                return;
            }

            if (!this.gameState.IsAttached)
            {
                this.SpeakStatus();
                return;
            }

            var stateObject = GameStateService.CurrentStateObjectAddress();
            if (stateObject == IntPtr.Zero)
            {
                this.output.Speak(Announcements.UiDumpNoRoot, true);
                return;
            }

            // The host captures a full stack trace for every failed read while this toggle is on, and the
            // root scan issues thousands of reads that are expected to fail.
            if (Core.GHSettings.ShowMemoryDiagnostics)
            {
                this.log?.Warn("UI dump refused: disable 'Show memory diagnostics' in GameHelper settings first.");
                this.output.Speak(Announcements.UiDumpBlockedByDiagnostics, true);
                return;
            }

            this.output.Speak(Announcements.UiDumpStarted, true);
            try
            {
                var path = this.dumper.Dump(
                    this.gameState.CurrentState,
                    stateObject,
                    root ?? this.Settings.UiDumpRootAddressOverride,
                    visibleOnly,
                    this.Settings.UiDumpMaxDepth,
                    this.Settings.UiDumpMaxNodes);
                this.output.Speak(path == null ? Announcements.UiDumpNoRoot : Announcements.UiDumpDone, false);
            }
            catch (Exception ex)
            {
                this.log?.Error("UI dump failed", ex);
                this.output.Speak(Announcements.UiDumpFailed, false);
            }
        }
    }
}
