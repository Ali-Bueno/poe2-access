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

        /// <summary>
        ///     Whether the exploring guide names the ground it turns to: ground apart from the last as before, and a gate
        ///     always, however near, so "Still closed" never comes of a target the player was not told of (review, 2026-11-26).
        /// </summary>
        public static bool SaysNext(bool apart, string kind) => apart || kind == ExplorationMap.BarrierGroundKind;

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

        /// <summary>Forgets the barriers no longer listed: a gate seen open leaves the list, and is tried afresh if it shuts again.</summary>
        public void KeepOnly(IReadOnlyCollection<uint> listed) => this.tried.IntersectWith(listed);

        /// <summary>Something changed (a boss fell): every barrier may be tried again.</summary>
        public void Retry() => this.tried.Clear();

        /// <summary>A new area: nothing tried, no quest step known.</summary>
        public void Clear()
        {
            this.tried.Clear();
            this.quest = null;
        }

        /// <summary>
        ///     The quest's objective lines now: a step that changed may have opened a barrier. The first reading only records,
        ///     and an empty one (the tracker hidden or not read) is no step at all.
        /// </summary>
        public void NoteQuest(IReadOnlyList<string> texts)
        {
            if (texts.Count == 0)
            {
                return;
            }

            var now = string.Join("\n", texts);
            if (this.quest != null && this.quest != now)
            {
                this.Retry();
            }

            this.quest = now;
        }
    }
}
