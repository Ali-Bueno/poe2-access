namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Research and test commands of <see cref="Poe2AccessibilityCore"/> (debug/command.txt, hotkeys) and the status and compatibility reports.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>How the camera draws the world now: the window, the player on it, and the world axes per window height.</summary>
        private const string SemanticFieldFile = "semantic-fields.txt";

        /// <summary>The code fields of this game process, being read off the frame thread.</summary>
        private System.Threading.Tasks.Task<CodeFieldReading>? codeFields;

        /// <summary>The hand-written fields for this game build (config/layout-overrides.txt), applied with the code fields.</summary>
        private System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> layoutOverrides = Array.Empty<(CodeFieldEntry, int?)>();

        private void LogCamera()
        {
            if (!Seats.Followed.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var player))
            {
                return;
            }

            var window = Core.Process.WindowArea;
            var here = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(player.WorldPosition);
            var (alongX, alongY) = ScreenProjection.Axes(player.WorldPosition);
            var height = Math.Max(window.Height, 1);
            this.log?.Info($"Camera: window {window.Width}x{window.Height} foreground {Core.Process.Foreground} world {player.WorldPosition.X:0},{player.WorldPosition.Y:0},{player.WorldPosition.Z:0} " +
                           $"screen {here.X:0.0},{here.Y:0.0} axes {alongX} {alongY} per height {alongX / height} {alongY / height} centre offset {(here.X - (window.Width / 2f)) / height:0.0000},{(here.Y - (window.Height / 2f)) / height:0.0000}");

            // The matrix read straight (no window) against GameHelper's mapping onto this window, both in heights from the
            // centre: they must agree to rounding at any window size. Then the 16:9 view against what this window draws.
            var camera = ScreenProjection.Camera();
            var playerDrawn = ScreenProjection.Draw(player.WorldPosition);
            this.log?.Info($"Camera matrix: readable {ScreenProjection.MatrixReadable}, usable {camera != null}, player drawn {playerDrawn?.ToString() ?? "none"} heights from the centre" +
                           (camera is { } m ? $", matrix aspect {CameraAspect(m):0.0000} window aspect {window.Width / (float)height:0.0000}" : string.Empty));
            var (agree, windowOnly, viewOnly, worst) = (0, 0, 0, 0f);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var render))
                {
                    continue;
                }

                var drawn = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(render.WorldPosition);
                if (ScreenProjection.Draw(render.WorldPosition) is { } straight)
                {
                    var mapped = new System.Numerics.Vector2(drawn.X - (window.Width / 2f), drawn.Y - (window.Height / 2f)) / height;
                    worst = Math.Max(worst, System.Numerics.Vector2.Distance(straight, mapped));
                }

                var onWindow = drawn.X >= 0 && drawn.Y >= 0 && drawn.X <= window.Width && drawn.Y <= window.Height;
                var inView = ScreenProjection.InView(render.WorldPosition);
                (agree, windowOnly, viewOnly) = onWindow == inView ? (agree + 1, windowOnly, viewOnly) : onWindow ? (agree, windowOnly + 1, viewOnly) : (agree, windowOnly, viewOnly + 1);
                if (onWindow != inView)
                {
                    this.log?.Info($"Camera view differs: {entity.Path} drawn at {drawn.X / height:0.000},{drawn.Y / height:0.000} heights, in view {inView}");
                }
            }

            this.log?.Info($"Camera view: {agree} agree, {windowOnly} only on the window, {viewOnly} only in the 16:9 view; matrix and window part by {worst:0.00000} heights at most");
        }

        /// <summary>The camera matrix's own width-to-height ratio: its up column's length over its across column's, view part removed.</summary>
        private static float CameraAspect(System.Numerics.Matrix4x4 m)
        {
            var view = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(m.M14, m.M24, m.M34));
            float Flat(System.Numerics.Vector3 column) => (column - (System.Numerics.Vector3.Dot(column, view) * view)).Length();
            return Flat(new System.Numerics.Vector3(m.M12, m.M22, m.M32)) / Flat(new System.Numerics.Vector3(m.M11, m.M21, m.M31));
        }

        /// <summary>Every stat the player's Stats component holds, key and value, to debug/player-stats-*.txt.</summary>
        private void DumpPlayerStats()
        {
            if (!Seats.Asking.TryGetComponent<GameReader.RemoteObjects.Components.Stats>(out var stats))
            {
                this.log?.Warn("playerstats: no Stats component");
                return;
            }

            var lines = stats.StatsChangedByItems.OrderBy(stat => (int)stat.Key).Select(stat => $"items {(int)stat.Key} {stat.Value}")
                .Concat(stats.StatsChangedByBuffAndActions.OrderBy(stat => (int)stat.Key).Select(stat => $"buffs {(int)stat.Key} {stat.Value}"));
            var file = Path.Join(this.DllDirectory, "debug", $"player-stats-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllLines(file, lines);
            this.log?.Info($"Player stats written: {file}");
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

            if (command.StartsWith("stuntrace ", StringComparison.Ordinal) && int.TryParse(command["stuntrace ".Length..], out var stunSeconds))
            {
                this.stunTrace?.Start(stunSeconds);
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

            if (command.StartsWith("guide at ", StringComparison.Ordinal) && command["guide at ".Length..].Split(' ') is [var gx, var gy] &&
                float.TryParse(gx, out var cellX) && float.TryParse(gy, out var cellY) && Seats.Followed.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var self))
            {
                // Test aid: lock the guide on a grid cell (a tile seen in "entities"), for scripted walking.
                var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
                this.beacon?.Lock(new ScanEntry(uint.MaxValue - 2, ScanCategory.Objects, $"cell {cellX:0},{cellY:0}", cellX * toWorld, cellY * toWorld, self.WorldPosition.Z));
                return;
            }

            if (command == "guide unexplored")
            {
                // Test aid: lock the guide on the nearest unexplored place, for scripted exploring.
                if (this.entities?.Entries().FirstOrDefault(entry => entry.Category == ScanCategory.Unexplored) is { } frontier)
                {
                    this.beacon?.Lock(frontier);
                }

                return;
            }

            if (command.StartsWith("guide ", StringComparison.Ordinal) && uint.TryParse(command["guide ".Length..], out var guideId))
            {
                // Test aid: lock the guide on any awake entity by id (from "entities"), seen or not, for scripted walking.
                foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
                {
                    if (entity.Id == guideId && entity.IsValid && entity.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var render))
                    {
                        var at = render.WorldPosition;
                        this.beacon?.Lock(new ScanEntry(entity.Id, ScanCategory.Objects, $"entity {entity.Id}", at.X, at.Y, at.Z));
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
                case var describe when describe.StartsWith("describe 0x", StringComparison.Ordinal):
                    // Research: what the reader makes of any element, focused or not (an icon's item, a row's parts).
                    var node = this.semantics?.Describe((IntPtr)Convert.ToInt64(describe["describe 0x".Length..], 16), true);
                    this.log?.Info($"Describe {describe[9..]}: {node?.Role} \"{node?.Text}\" value \"{node?.Value}\" tooltip \"{string.Join(" | ", node?.Tooltip ?? Array.Empty<string>())}\"");
                    break;
                case "passivemap":
                    // Research: from each class's start, what the nearest keystones and notables cost.
                    foreach (var line in this.semantics?.PassiveMapReport(this.focus?.Current?.Address ?? IntPtr.Zero) ?? Enumerable.Empty<string>())
                    {
                        this.log?.Info($"Passive map: {line}");
                    }

                    break;
                case var keys when keys.StartsWith("datkeys", StringComparison.Ordinal):
                    // Research: which game data rows the focused widget holds (a map pin's MapPins row).
                    var keyed = this.focus?.Current?.Address ?? IntPtr.Zero;
                    if (command.StartsWith("datkeys 0x", StringComparison.Ordinal))
                    {
                        keyed = (IntPtr)Convert.ToInt64(command["datkeys 0x".Length..], 16);
                    }

                    if (keyed == IntPtr.Zero && this.focus?.Manager is { } manager && manager != IntPtr.Zero)
                    {
                        // Nothing focused (a map's pins are only hovered): the element under the cursor.
                        this.memoryReader?.TryRead(manager + GameLayout.UiManager.HoveredOffset, out keyed);
                    }

                    foreach (var line in this.semantics?.DatKeys(keyed) ?? Enumerable.Empty<string>())
                    {
                        this.log?.Info($"Dat key: {line}");
                    }

                    break;
                case "mappin":
                    // Research: the world map's hovered pin as its data tells it, and whether the game built its popup.
                    this.log?.Info($"Map pin: {this.semantics?.PinReport(this.WorldScreen()) ?? "no semantics"}");
                    break;
                case "uilayout":
                    // Research: the focused element's parent chain, drawn rect against the window-free layout rect.
                    foreach (var line in this.semantics?.LayoutChain(this.focus?.Current?.Address ?? IntPtr.Zero) ?? Enumerable.Empty<string>())
                    {
                        this.log?.Info($"Layout: {line}");
                    }

                    break;
                case "passivenear":
                    // Research: the nodes around the hovered passive, to compare the stick's moves with the links said.
                    foreach (var line in this.semantics?.PassiveNeighbours(this.focus?.Current?.Address ?? IntPtr.Zero, 400) ?? Enumerable.Empty<string>())
                    {
                        this.log?.Info($"Passive near: {line}");
                    }

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
                    var cell = Seats.Followed.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var me)
                        ? FormattableString.Invariant($"{me.GridPosition.X:F0},{me.GridPosition.Y:F0}")
                        : "?";
                    this.log?.Info($"Beacon state: {this.beacon?.Diagnose()}; cell={cell}; playing={Core.Process.Foreground && this.WorldInFront() && IsPlayerAlive()} focus={this.focus?.Current?.Text}");
                    break;
                case "trace":
                    this.actorTrace?.Start();
                    break;
                case "stuntrace":
                    this.stunTrace?.Start();
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
                case "sleeping":
                    // Research: the game's sleeping entities (known beyond the awake bubble) with a minimap icon or a name.
                    var sleepers = new System.Collections.Concurrent.ConcurrentBag<string>();
                    var sleeping = Core.States.InGameStateObject.CurrentAreaInstance.ScanSleepingEntities(_ => true, (_, entity) =>
                    {
                        var hasIcon = entity.TryGetComponent<GameReader.RemoteObjects.Components.MinimapIcon>(out var mark) && !string.IsNullOrEmpty(mark.IconName);
                        var label = hasIcon && this.memoryReader != null && this.elements != null ? MinimapIconLabel.Of(this.memoryReader, this.elements, mark!) : string.Empty;
                        var at = entity.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var place) ? FormattableString.Invariant($"{place.GridPosition.X:0},{place.GridPosition.Y:0}") : "-";
                        if (hasIcon || entity.Path.Contains("MinimapIcon", StringComparison.OrdinalIgnoreCase) || entity.Path.Contains("Transition", StringComparison.Ordinal) || entity.Path.Contains("Glyph", StringComparison.Ordinal) || entity.Path.Contains("Chest", StringComparison.Ordinal))
                        {
                            sleepers.Add($"{entity.Id} {entity.Path} icon={mark?.IconName}|{label} at {at}");
                        }
                    });
                    this.log?.Info($"Sleeping entities: {sleeping}, with icons {sleepers.Count}");
                    foreach (var line in sleepers.OrderBy(line => line, StringComparer.Ordinal))
                    {
                        this.log?.Info($"Sleeping {line}");
                    }

                    break;
                case "unrendered":
                    // Research: awake entities without a Render component (icon-only objects), with their components.
                    foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
                    {
                        if (entity.IsValid && !entity.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out _))
                        {
                            var mark = entity.TryGetComponent<GameReader.RemoteObjects.Components.MinimapIcon>(out var icon) ? icon.IconName : "-";
                            this.log?.Info($"Unrendered {entity.Id} {entity.Path} icon={mark} positioned={entity.TryGetComponent<GameReader.RemoteObjects.Components.Positioned>(out _)}");
                        }
                    }

                    break;
                case "scan":
                    // Research: every scanner entry, as the categories would say them, then the area's named tiles seen or not.
                    foreach (var entry in this.entities?.Entries() ?? Array.Empty<ScanEntry>())
                    {
                        this.log?.Info($"Scan {entry.Category} id={entry.Id} kind={entry.Kind} sound={entry.Sound} \"{entry.Name}\" {this.entities!.DistanceTo(entry):F0}");
                    }

                    foreach (var entry in this.entities?.AreaLandmarks() ?? Array.Empty<ScanEntry>())
                    {
                        this.log?.Info($"Scan tile {entry.Category} id={entry.Id} kind={entry.Kind} \"{entry.Name}\" {this.entities!.DistanceTo(entry):F0}");
                    }

                    break;
                case "enemies":
                    this.log?.Info(this.enemyCues?.Diagnose() ?? "Enemy cues off");
                    break;
                case "fight":
                    this.log?.Info(this.fights?.Diagnose() ?? "Fight watch off");
                    break;
                case "paths":
                    // Research: what the client knows that is no monster, awake or sleeping, by path and cell (docs/research/map-hints.md).
                    var known = new System.Collections.Concurrent.ConcurrentBag<string>();
                    void Note(GameReader.RemoteObjects.States.InGameStateObjects.Entity entity)
                    {
                        var path = entity.Path ?? string.Empty;
                        if (path.Length > 0 && !path.StartsWith("Metadata/Monsters", StringComparison.Ordinal) && !path.StartsWith("Metadata/Characters", StringComparison.Ordinal))
                        {
                            var at = entity.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var place) ? FormattableString.Invariant($"{place.GridPosition.X:0},{place.GridPosition.Y:0}") : "-";
                            var model = entity.TryGetComponent<GameReader.RemoteObjects.Components.Animated>(out var animated) ? $"{animated.Path} {animated.ModelPath}" : string.Empty;
                            known.Add($"{path} at {at} id={entity.Id} model={model}");
                        }
                    }

                    foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
                    {
                        Note(entity);
                    }

                    Core.States.InGameStateObject.CurrentAreaInstance.ScanSleepingEntities(_ => true, (_, entity) => Note(entity));
                    foreach (var line in known.OrderBy(line => line, StringComparer.Ordinal))
                    {
                        this.log?.Info($"Path {line}");
                    }

                    break;
                case "maphints":
                    // Research: the map hints found here and what each would say from where the player stands.
                    foreach (var line in this.mapHints?.Diagnose() ?? Array.Empty<string>())
                    {
                        this.log?.Info(line);
                    }

                    break;
                case "tiles":
                    // Research: the area's tile grid with rotations and anchors, for docs/research/places.md.
                    this.places?.Dump(Path.Join(this.DllDirectory, "debug", $"tiles-{this.gameState?.CurrentAreaId}-{DateTime.Now:yyyyMMdd-HHmmss}.txt"));
                    break;
                case "layout":
                    this.LayoutSelfTest();
                    break;
                case "structs":
                    this.worldLayout?.Sweep();
                    this.log?.Info($"Animation search: {this.worldLayout?.AnimationSearchState}");
                    break;
                case "sounds":
                    this.log?.Info(this.audio?.Diagnose() ?? "Audio off");
                    break;
                case "costs":
                    this.log?.Info(this.tickCosts.Report(25));
                    break;
                case "flasks":
                    this.log?.Info(this.flasks?.Diagnose() ?? "Flasks off");
                    break;
                case "cooldowns":
                    this.log?.Info(this.alerts?.Cooldowns.Diagnose() ?? "Cooldowns off");
                    break;
                case "focusinfo":
                    this.log?.Info($"Focus info: {(this.focus?.Current is { } focused ? this.semantics?.RawInfo(focused.Address) : "no focus")}");
                    break;
                case "camera":
                    this.LogCamera();
                    break;
                case "playerstats":
                    this.DumpPlayerStats();
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

            var pad = !this.padCheck.Judged ? "pad buttons: not judged yet (press the left trigger alone)" : this.padCheck.Shift is { } shift ? $"pad buttons ok (shift {shift})" : "pad buttons FAIL";
            result += $"; {this.gameState.SelfTestAreaRow()}; {this.entityFields?.SelfTest()}; {this.flagBits?.SelfTest()}; {pad}; {this.semantics?.TreeFields.SelfTest()}; {this.places?.SelfTest()}; {this.inventoryReader?.SelfTest()}; {this.localPlayers?.SelfTest()}; controls: {this.controls?.SelfTest()}";

            this.log?.Info(result);
        }

        /// <summary>Per frame: whether the pad's button bits are where measured, judged on the left trigger (PadButtonCheck).</summary>
        private void CheckPadButtons(GamepadReader.PadState state)
        {
            if (!this.padCheck.Observe(state.Buttons, state.LeftTrigger, state.RightTrigger, GameLayout.PadButtons.MeasuredLeftTriggerBit))
            {
                return;
            }

            if (this.padCheck.Shift is not { } shift)
            {
                this.compatibility?.Missing("pad buttons");
                return;
            }

            GameLayout.PadButtons.Shift = shift;
            if (shift == 0)
            {
                this.compatibility?.Found("pad buttons");
            }
            else
            {
                this.compatibility?.Moved("pad buttons", GameLayout.PadButtons.MeasuredLeftTriggerBit, GameLayout.PadButtons.MeasuredLeftTriggerBit + shift);
            }
        }

        /// <summary>Per frame: the parts of the game's layout that could not be found, said once together.</summary>
        private void SayCompatibility()
        {
            this.TakeCodeFields();
            this.worldLayout?.Tick(this.gameState?.CurrentState == GameStateTypes.InGameState);
            if (this.compatibility?.TakeUnsaid() is { Count: > 0 } missing)
            {
                this.output.Speak(Announcements.CompatibilityMissing(missing), false);
            }

            if (this.worldLayout?.TakeSurvived() is { } survived)
            {
                this.output.Speak(survived, false);
            }

            if (this.gameNewsLine is { } news && this.gameState?.CurrentState == GameStateTypes.InGameState)
            {
                this.gameNewsLine = null;
                this.output.Speak(news, false);
                this.RememberGameKeys();
            }
        }

        private string GameKeysCatalog => Path.Join(this.DllDirectory, "catalog", "game-keys.txt");

        private string KnownGameKeys => Path.Join(this.DllDirectory, "config", "game-keys-known.txt");

        private void RememberGameKeys()
        {
            try
            {
                GameNews.Remember(this.GameKeysCatalog, this.KnownGameKeys);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                this.log?.Info($"Game news: could not keep the list: {error.Message}");
            }
        }

        /// <summary>Every kind the mod's rules name by id must still exist in the game's tables; a rename is said, not left silent.</summary>
        private void CheckNamedKinds(string catalog)
        {
            if (!File.Exists(catalog))
            {
                return;
            }

            var buffs = GameNews.Keys(File.ReadLines(catalog), BuffTable);
            if (buffs.Count == 0)
            {
                return; // the table was not read, which says nothing of what is in it
            }

            var animations = GameIds.Current.AnimationNames.ToList();
            var charges = Enum.GetValues<ChargeKind>().Where(kind => kind != ChargeKind.Rage); // a stat, not a buff
            var buffStates = Enum.GetValues<EnemyState>().Where(state => state is not (EnemyState.PrimedForStun or EnemyState.HeavyStunned));
            var animationStates = new[] { EnemyState.HeavyStunned, EnemyState.Electrocuted };
            var lost = GameNews.Unmatched(buffs, charges, PlayerCharges.FromBuffId).Select(kind => kind.ToString())
                .Concat(GameNews.Unmatched(buffs, buffStates, EnemyStates.FromBuffId).Select(state => state.ToString()))
                .Concat(GameNews.Unmatched(buffs, Enum.GetValues<StatusAilment>(), StatusAilments.FromBuffId).Select(ailment => ailment.ToString()))
                .Concat(animations.Count == 0 ? Array.Empty<string>() : GameNews.Unmatched(animations, animationStates, EnemyStates.FromAnimation).Select(state => $"{state} animation"))
                .ToList();
            if (lost.Count == 0)
            {
                this.compatibility?.Found(NamedKindsPart);
            }
            else
            {
                this.log?.Info($"Game news: no id in the game reads as {string.Join(", ", lost)}");
                this.compatibility?.Missing(NamedKindsPart);
            }
        }

        /// <summary>
        ///     Once per game process, after the report's reset: the kinds the mod names, the catalogs a patch left behind, and what an
        ///     update added, which is logged, listed and said once in play. Files in use must not stop anything: they only cost the news.
        /// </summary>
        private void CheckGameNews()
        {
            try
            {
                this.CheckNamedKinds(this.GameKeysCatalog);
                var failed = Path.Join(this.DllDirectory, "catalog", "game-data-failed.txt");
                if (File.Exists(failed))
                {
                    this.log?.Info($"Game news: not regenerated after the update: {string.Join(", ", File.ReadAllLines(failed))}");
                    this.compatibility?.Missing(GameDataPart);
                }

                var news = GameNews.Check(
                    this.GameKeysCatalog,
                    this.KnownGameKeys,
                    Path.Join(this.DllDirectory, "config", "game-news.txt"),
                    (table, key) => table == BuffTable ? StatusAilments.FromBuffId(key)?.ToString() ?? EnemyStates.FromBuffId(key)?.ToString()
                        : table == AnimationTable ? DodgeThreats.Of(1, _ => key).ToString() : null);
                foreach (var table in news)
                {
                    this.log?.Info($"Game news: {table.Table} {table.Added.Count} new, {table.Removed.Count} gone");
                }

                this.gameNewsLine = Announcements.GameNews(news.Select(table => (table.Table, table.Added.Count)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                this.log?.Info($"Game news: not checked: {error.Message}");
            }

            if (this.gameNewsLine == null)
            {
                this.RememberGameKeys();
            }
        }

        /// <summary>Logs which build of the game this is; a new game process is checked against the layout again.</summary>
        private void NoteGameBuild()
        {
            this.compatibility?.Reset();
            this.CheckGameNews();
            this.worldLayout?.Reset();
            this.layoutProbe?.Reset();
            this.locator?.Fields.Reset();
            this.controls?.Reset();
            this.entityFields?.Reset();
            this.flagBits?.Reset();
            this.padCheck.Reset();
            this.semantics?.TreeFields.Reset();
            this.places?.ResetLayout();
            this.inventoryReader?.BaseNames.Reset();
            GameLayout.PadButtons.Shift = 0;
            this.gameState?.ResetLayout();
            this.locator?.Reset();
            try
            {
                using var game = System.Diagnostics.Process.GetProcessById((int)Core.Process.Pid);
                if (game.MainModule?.FileName is { Length: > 0 } path)
                {
                    var file = new FileInfo(path);
                    if (this.worldLayout != null)
                    {
                        this.worldLayout.GameBuild = $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
                    }

                    this.StartCodeFields(path);
                    this.log?.Info($"Game build: {path}, {file.Length} bytes, written {file.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                this.log?.Debug($"Game build unreadable: {ex.Message}");
            }
        }

        /// <summary>The host reads WorldAreas rows by struct: its columns follow the table as the plugin's own do.</summary>
        private static void PlaceWorldAreaColumns(System.Collections.Generic.IReadOnlyDictionary<string, int>? columns)
        {
            var fields = new System.Collections.Generic.List<(string Field, int Expected, int Found)>();
            if (columns?.TryGetValue("WorldAreas.Name", out var name) == true)
            {
                fields.Add(("NamePtr", WorldAreaNameMeasured, name));
            }

            if (columns?.TryGetValue("WorldAreas.IsTown", out var town) == true)
            {
                fields.Add(("IsTown", WorldAreaIsTownMeasured, town));
            }

            if (fields.Count > 0)
            {
                GameReader.Utils.OffsetHealer.PlaceFromCode(WorldAreaStruct, fields);
            }
        }

        /// <summary>
        ///     Reads, off the frame thread, where the game's code holds the fields no live check judges: the byte signatures
        ///     and the semantic rules (docs/research/exe-layouts.md). <see cref="TakeCodeFields"/> follows what moved.
        /// </summary>
        private void StartCodeFields(string exe)
        {
            var catalog = Path.Join(this.DllDirectory, "catalog");
            this.layoutOverrides = this.ReadLayoutOverrides(new FileInfo(exe).Length);
            this.codeFields = System.Threading.Tasks.Task.Run(() => ReadCodeFields(exe, catalog));
        }

        /// <summary>The hand-written fields for this build (<see cref="LayoutOverrides"/>); none when there is no file.</summary>
        private System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> ReadLayoutOverrides(long exeSize)
        {
            var path = Path.Join(this.DllDirectory, "config", LayoutOverrides.FileName);
            try
            {
                if (!File.Exists(path))
                {
                    return Array.Empty<(CodeFieldEntry, int?)>();
                }

                var fields = LayoutOverrides.Parse(File.ReadLines(path), exeSize, out var otherBuilds, out var rejected);
                this.log?.Info($"Layout overrides: {fields.Count} for this build ({exeSize} bytes), {otherBuilds} for other builds, not used"
                    + (rejected.Count == 0 ? string.Empty : $"; unreadable: {string.Join(" | ", rejected)}"));
                return fields;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.log?.Warn($"Layout overrides unread: {ex.Message}");
                return Array.Empty<(CodeFieldEntry, int?)>();
            }
        }

        /// <summary>
        ///     Per frame: once the reading is done, follows what moved, as a patch that moved a field would otherwise read
        ///     wrong in silence. A field a live judgment already placed stays (<c>OffsetHealer.PlaceFromCode</c>).
        /// </summary>
        private void TakeCodeFields()
        {
            if (this.codeFields is not { IsCompleted: true } task)
            {
                return;
            }

            this.codeFields = null;
            var reading = task.IsCompletedSuccessfully ? task.Result
                : new CodeFieldReading(Array.Empty<(CodeFieldEntry, int?)>(), 0, Array.Empty<(CodeFieldEntry, int?)>(), 0, task.Exception?.GetBaseException().Message, null);
            if (reading.Error != null)
            {
                this.log?.Debug($"Code fields unread: {reading.Error}");
                if (this.layoutOverrides.Count == 0)
                {
                    return;
                }
            }

            var (settled, disagree) = CodeFieldCatalog.Settle(reading.Code, reading.Semantic);
            var read = LayoutOverrides.Apply(settled, this.layoutOverrides);
            var moved = new System.Collections.Generic.List<string>();
            var stuck = false;
            foreach (var group in read.Where(field => field.Found != null && field.Entry.Struct != null).GroupBy(field => field.Entry.Struct!))
            {
                var fields = group.Select(field => (field.Entry.Field, field.Entry.Measured, field.Found!.Value)).Distinct().ToList();
                if (GameReader.Utils.OffsetHealer.PlaceFromCode(group.Key, fields) is not { } outcomes)
                {
                    this.log?.Info($"Code fields: {group.Key} does not match the catalog, left where it is");
                    continue;
                }

                moved.AddRange(outcomes.Where(outcome => outcome.Health == GameReader.Utils.StructHealth.Moved)
                    .Select(outcome => $"{group.Key}.{outcome.Name} +0x{outcome.From:X} to +0x{outcome.To:X}"));
            }

            foreach (var (entry, found) in read)
            {
                if (found is not { } value || value == entry.Measured)
                {
                    continue;
                }

                if (entry.Layout is { } layout && LayoutFields.TryGetValue(layout, out var field))
                {
                    // Memory beats code: a live search that moved the field before the code was read keeps its value.
                    if (field.Get() != entry.Measured)
                    {
                        this.log?.Info($"Code fields: {entry.Name} reads +0x{value:X}, kept at +0x{field.Get():X} where a live search put it");
                        continue;
                    }

                    field.Set(value);
                    moved.Add($"{entry.Name} +0x{entry.Measured:X} to +0x{value:X}");
                }
                else if (entry.IsReport)
                {
                    stuck = true;
                    this.log?.Info($"Code fields: {entry.Name} moved from +0x{entry.Measured:X} to +0x{value:X}, and nothing can follow it");
                }
            }

            var semanticMoves = reading.Semantic.Where(field => field.Found is { } value && value != field.Entry.Measured)
                .Select(field => $"{field.Entry.Name} +0x{field.Entry.Measured:X} to +0x{field.Found:X}").Distinct().ToList();
            this.log?.Info(Tally("Code fields", reading.Code, reading.CodeMilliseconds, moved));
            this.log?.Info(reading.SemanticError is { } error ? $"Semantic fields unread: {error}"
                : Tally("Semantic fields", reading.Semantic, reading.SemanticMilliseconds, semanticMoves)
                    + (disagree.Count == 0 ? string.Empty : $"; disagree, left where they are: {string.Join(", ", disagree)}"));
            if (stuck)
            {
                this.compatibility?.Missing(CodeFieldsPart);
            }
        }

        /// <summary>"Code fields: 50 of 50 read in 600 ms; moved: none", and what read nothing.</summary>
        private static string Tally(
            string source, System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> read, long milliseconds, System.Collections.Generic.IReadOnlyList<string> moved)
        {
            var unsettled = read.Where(field => field.Found == null).Select(field => field.Entry.Name).Distinct().ToList();
            return $"{source}: {read.Count(field => field.Found != null)} of {read.Count} read in {milliseconds} ms; moved: {(moved.Count == 0 ? "none" : string.Join(", ", moved))}"
                + (unsettled.Count == 0 ? string.Empty : $"; not settled: {string.Join(", ", unsettled)}");
        }

        /// <summary>The semantic rules' reading, or why there is none: a reader that fails leaves the code fields standing.</summary>
        private static (System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Read, string? Error) ReadSemanticFields(byte[] file, string rules)
        {
            try
            {
                return (CodeFieldCatalog.ReadSemantic(file, CodeFieldCatalog.ParseSemantic(File.ReadLines(rules))), null);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or IndexOutOfRangeException or InvalidOperationException or OverflowException)
            {
                return (Array.Empty<(CodeFieldEntry, int?)>(), ex.Message);
            }
        }

        /// <summary>Both readings of the exe on disk; nothing touches the game. For a worker thread.</summary>
        private static CodeFieldReading ReadCodeFields(string exe, string catalog)
        {
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var entries = CodeFieldCatalog.Parse(File.ReadLines(Path.Join(catalog, CodeFieldFile)));
                var file = File.ReadAllBytes(exe);
                var (start, length) = GameReader.Utils.CodeFieldSearch.CodeOf(file);
                var code = CodeFieldCatalog.Read(file.AsSpan(start, length), entries);
                var codeTime = watch.ElapsedMilliseconds;
                var (semantic, semanticError) = ReadSemanticFields(file, Path.Join(catalog, SemanticFieldFile));
                return new CodeFieldReading(code, codeTime, semantic, watch.ElapsedMilliseconds - codeTime, null, semanticError);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new CodeFieldReading(Array.Empty<(CodeFieldEntry, int?)>(), 0, Array.Empty<(CodeFieldEntry, int?)>(), 0, ex.Message, null);
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

        /// <summary>What the exe on disk says, from the signatures and from the semantic rules, and how long each took.</summary>
        private sealed record CodeFieldReading(
            System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Code,
            long CodeMilliseconds,
            System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Semantic,
            long SemanticMilliseconds,
            string? Error,
            string? SemanticError);
    }
}
