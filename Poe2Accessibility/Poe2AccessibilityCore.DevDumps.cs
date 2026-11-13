namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader;
    using Poe2Accessibility.Services;

    /// <summary>The developer dumps of <see cref="Poe2AccessibilityCore"/>: area rows, inventories, camera, player stats, the grid trace, the status line and the UI tree.</summary>
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
                                        (inventory.IsGem(item.Entity) || items.Count == 1 ? " ["+ string.Join(" ", inventory.ItemComponents(item.Entity).Select(pair => $"{pair.Key}=0x{pair.Value.ToInt64():X}")) + "]" : string.Empty))));
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

        private long gridTraceUntil;
        private string gridTraceLast = string.Empty;

        /// <summary>`gridtrace N`: for N seconds, the focused inventory grid's own state each time it changes (<see cref="UiSemantics.GridState"/>).</summary>
        private void TickGridTrace()
        {
            if (Environment.TickCount64 >= this.gridTraceUntil || this.focus?.Current is not { } focused || this.semantics == null)
            {
                return;
            }

            var state = $"0x{focused.Address.ToInt64():X} {this.semantics.GridState(focused.Address)}";
            if (state != this.gridTraceLast)
            {
                this.gridTraceLast = state;
                this.log?.Info($"Grid: {state}");
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
