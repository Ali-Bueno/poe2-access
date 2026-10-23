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
        private EntityDump? entityDump;
        private TerrainDump? terrainDump;
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
        private GamepadReader.PadState padState;
        private (string Character, int Level) lastLevel;
        private long lastSoundError = long.MinValue / 2;
        private long viewDownSince;

        /// <summary>The radial menu opens on a held View; a second of holding means it is open.</summary>
        private const int RadialDumpHoldMs = 1000;

        /// <summary>A failing sound tick is logged once a minute, not every frame.</summary>
        private const int SoundErrorReportMs = 60000;

        /// <summary>Half and a quarter; re-armed 10 points above.</summary>
        private readonly VitalAlarm lifeAlarm = new(10, 50, 25);
        private ResearchSession? research;
        private UiElementReader? elements;
        private DevCommandFile? devCommands;
        private UiManagerLocator? locator;
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

                IAccessibilityOutputService? prism = this.Settings.SpeechEnabled
                    ? PrismOutputService.TryCreate(this.DllDirectory, this.log)
                    : null;
                this.output = prism ?? new NullOutputService();
                this.WarnOnHotkeyCollisions();

                var memory = new HostMemoryReader(this.log);
                var debugDirectory = Path.Join(this.DllDirectory, "debug");
                this.dumper = new UiTreeDumper(new UiTreeReader(memory), debugDirectory, this.log);
                this.devCommands = new DevCommandFile(debugDirectory, this.log);
                var elements = new UiElementReader(memory);
                this.elements = elements;
                var catalog = UiCatalog.Load(Path.Join(this.DllDirectory, "catalog", "ui-catalog.json"), this.log);
                this.locator = new UiManagerLocator(memory, elements, this.log);
                var inventory = new InventoryReader(memory, elements);
                this.semantics = new UiSemantics(memory, elements, catalog, new ComboBoxReader(elements, this.log), inventory);
                this.focus = new FocusTracker(memory, elements, this.locator, this.semantics, this.log);
                this.narrator = new ScreenNarrator(this.output, this.log, layer => this.semantics?.SelectedTab(layer) ?? string.Empty);
                this.focus.FocusChanged += this.narrator.OnFocusChanged;
                this.focus.ValueChanged += this.narrator.OnValueChanged;
                this.focus.TooltipChanged += this.narrator.OnTooltipChanged;
                this.pad = new GamepadReader(memory);
                this.entityDump = new EntityDump(elements, memory, this.log, debugDirectory);
                this.terrainDump = new TerrainDump(memory, this.log, debugDirectory);
                var landmarks = LandmarkTable.Load(
                    this.log,
                    Path.Join(this.DllDirectory, "..", "Radar", "important_tgt_files.txt"),
                    Path.Join(this.DllDirectory, "catalog", "landmarks.json"));
                this.entities = new EntityCatalog(memory, elements, inventory, landmarks, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log);
                this.audio = new AudioCueService(this.log);
                this.enemyCues = new EnemyCues(this.audio, memory, this.log);
                this.radar = new Radar(this.audio, () => this.gameState?.CurrentAreaId ?? string.Empty, this.log);
                this.nearby = new ProximityNarrator(this.entities, this.output);
                this.wallBump = new WallBump(this.audio);
                this.ApplySoundVolumes(this.Settings);
                this.commands = new ModCommands(this.output, this.focus, this.semantics, this.narrator, () => this.screen?.Current ?? ScreenSnapshot.Empty, this.entities, this.log);
                this.screen = new ScreenWatcher(elements, this.log);
                this.chat = new ChatWatcher(elements, line =>
                {
                    if (this.Settings.AnnounceChat)
                    {
                        this.output.Speak(line, false);
                    }
                });
                this.research = new ResearchSession(this.dumper, new FocusResearchLog(elements, debugDirectory, this.log), this.log)
                {
                    Enabled = this.Settings.ResearchMode,
                };
                this.focus.FocusChanged += node => this.research.OnFocusChanged(this.gameState?.CurrentState ?? GameStateTypes.GameNotLoaded, node, this.focus.CurrentPanelIndex);

                this.gameState = new GameStateService(this.log, memory, elements);
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
            if (!this.gameState.IsAttached)
            {
                this.processProbe?.Tick();
            }
            else
            {
                var stateObject = GameStateService.CurrentStateObjectAddress();
                this.focus?.Tick(this.gameState.CurrentState, stateObject);
                this.ReadModButton();
                this.WatchLife();
                this.TickSounds();
                this.NarrateScreenChanges(stateObject);
                this.research?.Tick(this.gameState.CurrentState, stateObject, this.Settings.UiDumpRootAddressOverride, this.Settings.UiDumpMaxDepth, this.Settings.UiDumpMaxNodes);
                if (this.elements != null)
                {
                    this.research?.WatchFocusedBytes(this.focus?.Current?.Address ?? IntPtr.Zero, this.elements);
                }
            }

            if (this.Settings.ResearchMode && this.devCommands != null)
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
            this.narrator.TutorialLayer = inGame ? this.locator.InGameTutorialLayer(stateObject) : IntPtr.Zero;
            this.narrator.HudLayer = inGame ? this.locator.InGameHudLayer(stateObject) : IntPtr.Zero;
            this.narrator.ReadTooltips = this.Settings.ReadTooltipsOnFocus;
            this.narrator.Tick();
            while (this.screen.TryDequeue(out var change))
            {
                this.log?.Info($"Screen {change.Kind} layer {change.Layer.Index}: {string.Join(" | ", change.Texts.Select(text => text.Button.Length > 0 ? $"{text.Text} [{text.Button}]" : text.Text))}");
                if (change.Kind == ScreenChangeKind.TopLayerChanged || this.Settings.AnnounceAppearingText)
                {
                    this.narrator.OnScreenChange(change, this.screen.Current, this.focus.Current, this.focus.CurrentLayer);
                }
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
            if (!this.Settings.ControllerCommands || this.commands == null)
            {
                return;
            }

            foreach (var gesture in this.gestures.Update(Environment.TickCount64, state.IsDown(GameLayout.PadButtons.LeftStick), state.RightX, state.RightY))
            {
                this.commands.OnGesture(gesture);
            }

            this.DumpHeldRadialMenu(state.IsDown(GameLayout.PadButtons.Back));
        }

        /// <summary>Research: holding View opens the radial menu, which no layer event reports yet; dump it once per hold.</summary>
        private void DumpHeldRadialMenu(bool viewDown)
        {
            if (!viewDown)
            {
                this.viewDownSince = 0;
                return;
            }

            var now = Environment.TickCount64;
            if (this.viewDownSince == 0)
            {
                this.viewDownSince = now;
            }
            else if (this.viewDownSince > 0 && now - this.viewDownSince >= RadialDumpHoldMs && this.Settings.ResearchMode &&
                     this.gameState?.CurrentState == GameStateTypes.InGameState)
            {
                this.viewDownSince = -1; // once per hold
                this.log?.Info("View held: dumping the UI for the radial menu");
                this.DumpUiTree(false);
            }
        }

        /// <summary>Per frame: radar, enemy sounds and what is nearby, only in gameplay; a failure here never stops the speech after it.</summary>
        private void TickSounds()
        {
            try
            {
                var playing = Core.Process.Foreground && this.focus?.Current == null &&
                              this.gameState?.CurrentState == GameStateTypes.InGameState && IsPlayerAlive();
                if (this.enemyCues != null)
                {
                    this.enemyCues.Enemies = this.Settings.EnemySounds;
                    this.enemyCues.Friends = this.Settings.NpcSounds;
                    this.enemyCues.Tick(playing && (this.Settings.EnemySounds || this.Settings.NpcSounds));
                }

                this.radar?.Tick(playing && this.Settings.Radar);
                this.nearby?.Tick(playing && this.Settings.AnnounceNearby);
                if (playing && this.Settings.WallBump)
                {
                    this.wallBump?.Tick(this.padState.LeftX, this.padState.LeftY);
                }

                this.WatchLevel();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (Environment.TickCount64 - this.lastSoundError >= SoundErrorReportMs)
                {
                    this.lastSoundError = Environment.TickCount64;
                    this.log?.Error("Gameplay sound/proximity tick failed", ex);
                }
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
                this.output.Speak(Announcements.LowLife(threshold), true);
            }
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
                case "terrain":
                    this.terrainDump?.Write();
                    break;
                case "enemies":
                    this.log?.Info(this.enemyCues?.Diagnose() ?? "Enemy cues off");
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

        private void OnAttachedChanged(bool attached)
        {
            this.processProbe?.Reset();
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
            }

            if (this.wallBump != null)
            {
                this.wallBump.Gain = Fraction(settings.RadarVolume);
            }

            if (this.enemyCues != null)
            {
                this.enemyCues.Gain = Fraction(settings.EnemyVolume);
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

        private void OnGameStateChanged(GameStateTypes previous, GameStateTypes current)
        {
            this.research?.OnStateChanged(current);
            this.screen?.Reset(false);
            this.narrator?.Reset(current == GameStateTypes.InGameState);
            if (this.semantics != null)
            {
                this.semantics.InGame = current == GameStateTypes.InGameState;
            }
            if (!this.Settings.AnnounceGameState)
            {
                return;
            }

            var text = previous == GameStateTypes.GameNotLoaded && current != GameStateTypes.GameNotLoaded
                ? $"{Announcements.GameDetected} {Announcements.ForState(current)}"
                : Announcements.ForState(current);
            this.output.Speak(text, true);
        }

        private void OnAreaChanged(string areaName)
        {
            this.entities?.Forget();
            this.nearby?.Reset();
            if (this.Settings.AnnounceArea)
            {
                this.output.Speak(Announcements.Area(areaName), false);
            }
        }

        private void DumpUiTree(bool visibleOnly)
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
                    this.Settings.UiDumpRootAddressOverride,
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
