namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Research and test commands of <see cref="Poe2AccessibilityCore"/> (debug/command.txt, hotkeys) and the status and compatibility reports.</summary>
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
                float.TryParse(gx, out var cellX) && float.TryParse(gy, out var cellY) && Seats.Followed.TryGetComponent<GameHelper.RemoteObjects.Components.Render>(out var self))
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
                    if (entity.Id == guideId && entity.IsValid && entity.TryGetComponent<GameHelper.RemoteObjects.Components.Render>(out var render))
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
                case "entities all":
                    // Research: every entity the client knows (the network bubble), e.g. far quest icons.
                    this.entityDump?.Write(int.MaxValue);
                    break;
                case "beacon":
                    // Test aid: where the guide's last ping pointed on screen (x right, y down), for scripted walking.
                    this.log?.Info(this.beacon?.LastAim is { } aim
                        ? FormattableString.Invariant($"Beacon aim {aim.Screen.X:F3},{aim.Screen.Y:F3} remaining {aim.RemainingWorld:F0}")
                        : "Beacon aim none");
                    var cell = Seats.Followed.TryGetComponent<GameHelper.RemoteObjects.Components.Render>(out var me)
                        ? FormattableString.Invariant($"{me.GridPosition.X:F0},{me.GridPosition.Y:F0}")
                        : "?";
                    this.log?.Info($"Beacon state: {this.beacon?.Diagnose()}; cell={cell}; playing={Core.Process.Foreground && this.focus?.Current == null && IsPlayerAlive()} focus={this.focus?.Current?.Text}");
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
                        var hasIcon = entity.TryGetComponent<GameHelper.RemoteObjects.Components.MinimapIcon>(out var mark) && !string.IsNullOrEmpty(mark.IconName);
                        var label = hasIcon && this.memoryReader != null && this.elements != null ? MinimapIconLabel.Of(this.memoryReader, this.elements, mark!) : string.Empty;
                        var at = entity.TryGetComponent<GameHelper.RemoteObjects.Components.Render>(out var place) ? FormattableString.Invariant($"{place.GridPosition.X:0},{place.GridPosition.Y:0}") : "-";
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
                        if (entity.IsValid && !entity.TryGetComponent<GameHelper.RemoteObjects.Components.Render>(out _))
                        {
                            var mark = entity.TryGetComponent<GameHelper.RemoteObjects.Components.MinimapIcon>(out var icon) ? icon.IconName : "-";
                            this.log?.Info($"Unrendered {entity.Id} {entity.Path} icon={mark} positioned={entity.TryGetComponent<GameHelper.RemoteObjects.Components.Positioned>(out _)}");
                        }
                    }

                    break;
                case "scan":
                    // Research: every scanner entry, as the categories would say them, then the area's named tiles seen or not.
                    foreach (var entry in this.entities?.Entries() ?? Array.Empty<ScanEntry>())
                    {
                        this.log?.Info($"Scan {entry.Category} id={entry.Id} kind={entry.Kind} \"{entry.Name}\" {this.entities!.DistanceTo(entry):F0}");
                    }

                    foreach (var entry in this.entities?.AreaLandmarks() ?? Array.Empty<ScanEntry>())
                    {
                        this.log?.Info($"Scan tile {entry.Category} id={entry.Id} kind={entry.Kind} \"{entry.Name}\" {this.entities!.DistanceTo(entry):F0}");
                    }

                    break;
                case "enemies":
                    this.log?.Info(this.enemyCues?.Diagnose() ?? "Enemy cues off");
                    break;
                case "tiles":
                    // Research: the area's tile grid with rotations and anchors, for docs/research/places.md.
                    this.places?.Dump(Path.Join(this.DllDirectory, "debug", $"tiles-{this.gameState?.CurrentAreaId}-{DateTime.Now:yyyyMMdd-HHmmss}.txt"));
                    break;
                case "layout":
                    this.LayoutSelfTest();
                    break;
                case "flasks":
                    this.log?.Info(this.flasks?.Diagnose() ?? "Flasks off");
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
            result += $"; {this.gameState.SelfTestAreaRow()}; {this.entityFields?.SelfTest()}; {this.flagBits?.SelfTest()}; {pad}; {this.semantics?.TreeFields.SelfTest()}; {this.places?.SelfTest()}; {this.inventoryReader?.SelfTest()}; controls: {this.controls?.SelfTest()}";

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
                    this.log?.Info($"Game build: {path}, {file.Length} bytes, written {file.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                this.log?.Debug($"Game build unreadable: {ex.Message}");
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
