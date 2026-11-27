namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Inside Atziri's Temple: the room the player walks into, the room completed (the quest tracker's "Salas del templo
    ///     completadas (N/7)" rising) and the way to the next one, from the console's map laid on the ground (<see cref="TempleSlots"/>).
    /// </summary>
    public sealed class TempleWatch
    {
        /// <summary>The temple's area (WorldAreas 430 `IncursionTemple`); the Lost Temple of the present is built otherwise.</summary>
        public const string TempleArea = "IncursionTemple";

        private const int PollMs = 400;

        /// <summary>The scanner id the next room's way is described under (no entity has it: ids are 32-bit counters from 1).</summary>
        private const uint NextRoomId = uint.MaxValue - 7;

        /// <summary>The tracker's room counter ("Completed Temple Rooms ({0}/{1})", ClientStrings): the only count it shows in the temple.</summary>
        private static readonly Regex RoomCount = new(@"\((\d+)\s*/\s*(\d+)\)\s*$", RegexOptions.Compiled);

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private long lastPoll;
        private bool arriving;
        private string areaHash = string.Empty;
        private Task<TempleSlots.Ground>? measuring;
        private TempleSlots.Frame? frame;
        private object? matchedLayout;
        private (float X, float Y)? spawn;
        private (int Row, int Column)? told;
        private int? completed;

        public TempleWatch(IAccessibilityOutputService output, PluginLog log)
        {
            this.output = output;
            this.log = log;
        }

        /// <summary>Gets or sets the id of the area the player is in.</summary>
        public Func<string> AreaId { get; set; } = () => string.Empty;

        /// <summary>Gets or sets the quest tracker's lines as last read.</summary>
        public Func<IReadOnlyList<string>> QuestLines { get; set; } = () => Array.Empty<string>();

        /// <summary>Gets or sets how the scanner says the way to a point ("…, 40 pasos, arriba 5, luego derecha").</summary>
        public Func<ScanEntry, string> Describe { get; set; } = entry => entry.Name;

        /// <summary>An area change: a temple entered anew starts in its Vestibule.</summary>
        public void Reset(bool resumed) => this.arriving = !resumed;

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (this.AreaId() != TempleArea)
            {
                TempleRun.Here = null;
                this.arriving = false;
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (string.IsNullOrEmpty(area.AreaHash) || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var at = (player.GridPosition.X, player.GridPosition.Y);
            if (area.AreaHash != this.areaHash)
            {
                this.Enter(area, at);
            }

            if (this.Frame() is not { } laid)
            {
                return;
            }

            var cell = laid.CellOf(at.X, at.Y);
            TempleRun.Here = cell;
            var layout = TempleRun.Layout;
            if (cell != this.told)
            {
                this.told = cell;
                if (layout.TryGetValue(cell, out var room) && !room.Path)
                {
                    this.output.Say(SpeechKind.Quest, Announcements.TempleRoomEntered(room.Title, TempleRun.Done.Contains(cell)));
                }
            }

            this.Completions(laid, cell, layout, area.WorldToGridConvertor, player.WorldPosition.Z);
        }

        /// <summary>A temple instance first seen: its ground is cut into slots off the frame thread.</summary>
        private void Enter(GameReader.RemoteObjects.States.InGameStateObjects.AreaInstance area, (float X, float Y) at)
        {
            this.areaHash = area.AreaHash;
            TempleRun.Restart();
            (this.frame, this.matchedLayout, this.told, this.completed) = (null, null, null, null);
            this.spawn = this.arriving ? at : null;
            this.arriving = false;
            var (grid, row) = (area.GridWalkableData, area.TerrainMetadata.BytesPerRow);
            this.measuring = Task.Run(() => TempleSlots.Measure(grid, row));
        }

        /// <summary>The map laid on the ground, matched again whenever the map is read anew; null until both are known and fit.</summary>
        private TempleSlots.Frame? Frame()
        {
            var layout = TempleRun.Layout;
            if (ReferenceEquals(layout, this.matchedLayout) || this.measuring is not { IsCompleted: true } task)
            {
                return this.frame;
            }

            this.matchedLayout = layout;
            if (task.IsFaulted || layout.Count <= 2 * TempleSlots.MaxMismatches)
            {
                this.log.Info($"Temple: {(task.IsFaulted ? "the ground could not be cut into slots" : $"the console's map is not known ({layout.Count} cells read)")}");
                return this.frame = null;
            }

            var ground = task.Result;
            var spawnSlot = this.spawn is { } arrived ? ground.SlotOf(arrived.X, arrived.Y) : ((int X, int Y)?)null;
            this.frame = TempleSlots.Match(ground, layout.Keys.ToHashSet(), spawnSlot, out var wrong);
            this.log.Info($"Temple: slot lines at ({ground.BoundaryX},{ground.BoundaryY}), ground {ground.Picture()}; the map's {layout.Count} cells " +
                          (this.frame is { } laid
                              ? $"lie turned ({laid.RowX},{laid.ColumnX},{laid.RowY},{laid.ColumnY}) from slot ({laid.OffsetX},{laid.OffsetY}), {wrong} wrong"
                              : $"fit no single way (best {(wrong == int.MaxValue ? "none" : wrong.ToString())} wrong, spawn slot {spawnSlot?.ToString() ?? "unseen"})"));
            return this.frame;
        }

        /// <summary>The tracker's room count rising: the room the player stands in is done, and the next one is told with its way.</summary>
        private void Completions(TempleSlots.Frame laid, (int Row, int Column) cell, IReadOnlyDictionary<(int Row, int Column), (string Title, bool Path)> layout, float gridSize, float z)
        {
            var count = this.QuestLines().Select(line => RoomCount.Match(line)).FirstOrDefault(match => match.Success);
            if (count == null)
            {
                return;
            }

            var (done, total) = (int.Parse(count.Groups[1].Value), int.Parse(count.Groups[2].Value));
            var before = this.completed;
            this.completed = done;
            if (before is not { } had || done <= had)
            {
                return;
            }

            if (layout.TryGetValue(cell, out var room) && !room.Path)
            {
                TempleRun.Complete(cell);
            }

            var paths = layout.ToDictionary(each => each.Key, each => each.Value.Path);
            var next = done >= total && layout.ContainsKey(TempleGrid.AtziriChamber)
                ? TempleGrid.AtziriChamber
                : TempleGrid.NextRoom(cell, paths, TempleRun.Done);
            this.log.Info($"Temple: room {done}/{total} done at {cell}; next {next?.ToString() ?? "none"}");
            if (next is not { } goal || !layout.TryGetValue(goal, out var target))
            {
                return;
            }

            var (x, y) = laid.CentreOf(goal);
            var entry = new ScanEntry(NextRoomId, ScanCategory.PointsOfInterest, Announcements.TempleNextRoom(target.Title), x * gridSize, y * gridSize, z);
            this.output.Say(SpeechKind.Quest, this.Describe(entry));
        }
    }
}
