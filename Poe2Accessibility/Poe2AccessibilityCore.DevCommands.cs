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

            if (command.StartsWith("gridtrace ", StringComparison.Ordinal) && int.TryParse(command["gridtrace ".Length..], out var gridSeconds))
            {
                (this.gridTraceUntil, this.gridTraceLast) = (Environment.TickCount64 + (gridSeconds * 1000L), string.Empty);
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
                case "positioned":
                    // Research: the cell offset found in Positioned, and where it puts the entities the game does not draw.
                    if (this.bosses?.Mechanics?.Positions is { } positions && GameReader.Core.States.InGameStateObject.CurrentAreaInstance is { } placedArea)
                    {
                        var undrawn = placedArea.AwakeEntities.Values
                            .Where(entity => entity.IsValid && !entity.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out _)).Take(40).ToList();
                        foreach (var entity in undrawn)
                        {
                            var place = positions.Of(entity);
                            this.log?.Info($"Positioned {entity.Id} {entity.Path} {(place is { } at ? $"at {at.X:0},{at.Y:0}" : "no place")}");
                        }

                        this.log?.Info($"Positioned cell offset: {(positions.CellOffset is { } found ? $"+0x{found:X}" : "not found")}, {undrawn.Count} undrawn entities");
                        foreach (var line in positions.Evidence())
                        {
                            this.log?.Info($"Positioned evidence: {line}");
                        }
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
                case var info when info.StartsWith("focusinfo 0x", StringComparison.Ordinal):
                    // Research: the art and tooltip markup of any element (a glyph the screen did not pair with its label).
                    this.log?.Info($"Focus info {info[10..]}: {this.semantics?.RawInfo((IntPtr)Convert.ToInt64(info["focusinfo 0x".Length..], 16))}");
                    break;
                case "focusinfo":
                    this.log?.Info($"Focus info: {(this.focus?.Current is { } focused ? this.semantics?.RawInfo(focused.Address) : "no focus")}");
                    break;
                case "itemmods":
                    this.log?.Info($"Item mods: {(this.focus?.Current is { } onItem ? this.semantics?.FocusedItemMods(onItem) : "no focus")}");
                    break;
                case "itemmodscheck":
                    foreach (var line in this.semantics?.ItemModsCheck() ?? Array.Empty<string>())
                    {
                        this.log?.Info(line);
                    }

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
    }
}
