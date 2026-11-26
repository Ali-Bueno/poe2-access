namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     How the unseen ground past a closed barrier (<see cref="ExplorationMap.BarrierGroundKind"/>) is chosen: what the
    ///     exploring guide may still take, and which barriers are a door's. Pure.
    ///     docs/superpowers/specs/2026-11-26-ground-behind-closed-barriers-design.md.
    /// </summary>
    public static class BarrierGround
    {
        /// <summary>A barrier this near a door the player opens is that door: its ground is the door's entry (cells).</summary>
        public const float DoorCells = 12f;

        /// <summary>
        ///     The entries the exploring guide may still go on to: ground not seen yet, and the ground behind a barrier not
        ///     found shut since something changed. Its place is the barrier's near side, seen ground, so seen says nothing of it.
        /// </summary>
        public static List<ScanEntry> StillToTry(IEnumerable<ScanEntry> found, Func<ScanEntry, bool> seen, TriedBarriers tried) =>
            found.Where(entry => entry.Kind == ExplorationMap.BarrierGroundKind ? !tried.Contains(entry.Id) : !seen(entry)).ToList();

        /// <summary>The places without the barriers that stand at a door (grid cells): a shut door has its own "Unopened door" entry.</summary>
        public static List<FrontierPlace> AwayFromDoors(IEnumerable<FrontierPlace> places, IReadOnlyCollection<Vector2> doorCells) =>
            places.Where(place => !place.BehindBarrier || !doorCells.Any(door => Vector2.Distance(door, place.Cell) <= DoorCells)).ToList();
    }

    /// <summary>
    ///     The barriers the guide found still shut, by entry id: the exploring guide does not lead to them again until
    ///     something changes, a boss's defeat or the quest's next step (<see cref="Retry"/>, <see cref="NoteQuest"/>). Pure.
    /// </summary>
    public sealed class TriedBarriers
    {
        private readonly HashSet<uint> tried = new();
        private string? quest;

        /// <summary>The guide found this barrier shut.</summary>
        public void Tried(uint id) => this.tried.Add(id);

        public bool Contains(uint id) => this.tried.Contains(id);

        /// <summary>Something changed (a boss fell): every barrier may be tried again.</summary>
        public void Retry() => this.tried.Clear();

        /// <summary>A new area: nothing tried, no quest step known.</summary>
        public void Clear()
        {
            this.tried.Clear();
            this.quest = null;
        }

        /// <summary>The quest tracker's lines now: a step that changed may have opened a barrier. The first reading only records.</summary>
        public void NoteQuest(IReadOnlyList<string> texts)
        {
            var now = string.Join("\n", texts);
            if (this.quest != null && this.quest != now)
            {
                this.Retry();
            }

            this.quest = now;
        }
    }
}
