namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Research and test commands of <see cref="Poe2AccessibilityCore"/> (debug/command.txt, hotkeys) and the status report.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>The area's offset that the game's own functions read the WorldAreas row at (field audit A4).</summary>
        private const int AreaInstanceAreaRowOffset = 0xA0;

        /// <summary>
        ///     Research (dev command "arearow", field audit A4): the WorldAreas row the WorldData chain reaches
        ///     (WorldAreaDetailsPtr +0x98, then the row) beside the one at [AreaInstance+0xA0], with each row's Id.
        /// </summary>
        private void LogAreaRows()
        {
            if (this.memoryReader is not { } memory)
            {
                return;
            }

            var game = Core.States.InGameStateObject;
            var chain = game.CurrentWorldInstance.AreaDetails.Address;
            var area = game.CurrentAreaInstance.Address;
            var direct = area != IntPtr.Zero && memory.TryRead<IntPtr>(area + AreaInstanceAreaRowOffset, out var row) ? row : IntPtr.Zero;
            string Id(IntPtr at) => at != IntPtr.Zero && memory.TryRead<IntPtr>(at, out var text) ? this.elements?.ReadUtf16BufferAt(text, 64) ?? "?" : "-";
            var world = game.CurrentWorldInstance.Address;
            var details = world != IntPtr.Zero && memory.TryRead<IntPtr>(world + 0x98, out var held) ? held : IntPtr.Zero;
            var detailsRow = details != IntPtr.Zero && memory.TryRead<IntPtr>(details + 0x98, out var rowHeld) ? rowHeld : IntPtr.Zero;
            this.log?.Info($"Area rows: WorldData chain 0x{chain.ToInt64():X} ({Id(chain)}), [AreaInstance+0x{AreaInstanceAreaRowOffset:X}] 0x{direct.ToInt64():X} ({Id(direct)}), " +
                           $"{(chain == direct && chain != IntPtr.Zero ? "the same row" : "DIFFERENT")}; WorldData 0x{world.ToInt64():X}, [+0x98] 0x{details.ToInt64():X}, " +
                           $"[[+0x98]+0x98] 0x{detailsRow.ToInt64():X} ({Id(detailsRow)})");
        }

        /// <summary>Research (dev command "inventories"): every server inventory of player 1 that holds items, each item by base id, name and cell.</summary>
        private void LogInventories()
        {
            if (this.memoryReader is not { } memory || this.inventoryReader is not { } inventory)
            {
                return;
            }

            foreach (var (name, address) in Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject.PlayerInventories.OrderBy(pair => (int)pair.Key))
            {
                var items = ServerInventory.Items(memory, address);
                if (items.Count > 0)
                {
                    this.log?.Info($"Inventory {(int)name} {name}: " + string.Join(" | ", items.OrderBy(item => item.Y).ThenBy(item => item.X)
                        .Select(item => $"{item.X},{item.Y} {inventory.ItemBaseId(item.Entity)} \"{inventory.DescribeItemEntity(item.Entity)}\"" +
                                        (inventory.IsGem(item.Entity) ? " [" + string.Join(" ", inventory.ItemComponents(item.Entity).Select(pair => $"{pair.Key}=0x{pair.Value.ToInt64():X}")) + "]" : string.Empty))));
                }
            }
        }

        /// <summary>How the camera draws the world now: the window, the player on it, and the world axes per window height.</summary>
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
                case "atlasnode":
                    // Research: the hovered element as a node of the Atlas or its trees (docs/research/endgame.md).
                    var atlasHovered = IntPtr.Zero;
                    this.memoryReader?.TryRead(this.focus?.Manager is { } atlasManager ? atlasManager + GameLayout.UiManager.HoveredOffset : IntPtr.Zero, out atlasHovered);
                    this.log?.Info($"Atlas node: {this.semantics?.EndgameNodeReport(atlasHovered) ?? "no semantics"}");
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
                    this.log?.Info(this.screen?.TakeSnapshotCosts() ?? "Screen snapshots: no watcher");
                    break;
                case "flasks":
                    this.log?.Info(this.flasks?.Diagnose() ?? "Flasks off");
                    break;
                case "cooldowns":
                    this.log?.Info(this.alerts?.Cooldowns.Diagnose() ?? "Cooldowns off");
                    break;
                case "skills":
                    this.log?.Info(this.skillDump?.Dump() ?? "Skill dump off");
                    break;
                case "arearow":
                    this.LogAreaRows();
                    break;
                case "inventories":
                    this.LogInventories();
                    break;
                case "focusinfo":
                    this.log?.Info($"Focus info: {(this.focus?.Current is { } focused ? this.semantics?.RawInfo(focused.Address) : "no focus")}");
                    this.log?.Info($"Focused grid: {(this.focus?.Current is { } gridCell && this.semantics?.ReadGrid(gridCell, out _) is { } wholeGrid ? wholeGrid : "none")} | bases: {(this.focus?.Current is { } baseCell ? this.semantics?.GridBaseIds(baseCell) : string.Empty)}");
                    break;
                case "itemmods":
                    this.log?.Info($"Item mods: {(this.focus?.Current is { } onItem ? this.semantics?.FocusedItemMods(onItem) : "no focus")}");
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
            if (this.InGame)
            {
                result += $"; {this.locator.Fields.SelfTest(this.locator.InGameLayerContainer(stateObject))}";
            }

            var pad = !this.padCheck.Judged ? "pad buttons: not judged yet (press the left trigger alone)" : this.padCheck.Shift is { } shift ? $"pad buttons ok (shift {shift})" : "pad buttons FAIL";
            result += $"; {this.gameState.SelfTestAreaRow()}; {this.entityFields?.SelfTest()}; {this.flagBits?.SelfTest()}; {pad}; {this.semantics?.TreeFields.SelfTest()}; {this.places?.SelfTest()}; {this.inventoryReader?.SelfTest()}; {this.localPlayers?.SelfTest()}; controls: {this.controls?.SelfTest()}";

            this.log?.Info(result);
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
    }
}
