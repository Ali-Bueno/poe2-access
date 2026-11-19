namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Small unseen ground the player came near and walks away from without seeing it: a nook or a short branch left behind.
    ///     Each one is told once an area, nearest first, one at a time. Pure; docs/radar.md "Left behind".
    /// </summary>
    public sealed class LeftBehindWatch
    {
        /// <summary>Unseen ground this near was in the player's reach: just past the edge of a look.</summary>
        public const float NearCells = 1.5f * ExplorationMap.SeenRadiusCells;

        /// <summary>Once the player is this far from it, they have walked away from it.</summary>
        public const float LeaveCells = 2f * ExplorationMap.SeenRadiusCells;

        /// <summary>A frontier place this near one already known is the same ground (it moves as its edge is seen): two blocks.</summary>
        public const float SameCells = 3f * ExplorationMap.BlockCells;

        /// <summary>Two cues of this kind stay this far apart, so leaving a stretch with several nooks is not a burst.</summary>
        public const long GapMs = 3000;

        /// <summary>Moving farther than this between two updates is a jump, not a walk (half a second of walking is about 20 cells).</summary>
        public const float JumpCells = ExplorationMap.SeenRadiusCells;

        private readonly List<(Vector2 Cell, bool Told)> known = new();
        private long toldAt = long.MinValue / 2;
        private Vector2? last;

        /// <summary>A new area: its ground is all news.</summary>
        public void Reset()
        {
            this.known.Clear();
            this.last = null;
            this.toldAt = long.MinValue / 2;
        }

        /// <summary>The cell of the small unseen ground just left behind, to sound from; null when there is none to tell now.</summary>
        /// <param name="player">The player's grid cell.</param>
        /// <param name="small">Where small unseen ground begins (nooks and short branches), from the last frontier search.</param>
        /// <param name="seen">Whether a cell has been seen since.</param>
        /// <param name="quiet">A fight: nothing is told, what is left behind waits until it is over.</param>
        public Vector2? Update(Vector2 player, IEnumerable<Vector2> small, Func<Vector2, bool> seen, bool quiet, long nowMs)
        {
            // A portal or a waypoint within the area is no walk away: what was near before it is not told all at once.
            var jumped = this.last is { } before && Vector2.Distance(before, player) > JumpCells;
            this.last = player;
            if (jumped)
            {
                this.known.RemoveAll(place => !place.Told);
            }

            foreach (var cell in small)
            {
                var index = this.known.FindIndex(place => Vector2.Distance(place.Cell, cell) <= SameCells);
                if (index >= 0)
                {
                    this.known[index] = this.known[index] with { Cell = cell };
                }
                else if (Vector2.Distance(player, cell) <= NearCells)
                {
                    this.known.Add((cell, false));
                }
            }

            this.known.RemoveAll(place => !place.Told && seen(place.Cell));
            if (quiet || nowMs - this.toldAt < GapMs)
            {
                return null;
            }

            var left = this.known.Select((place, index) => (place, index))
                .Where(entry => !entry.place.Told && Vector2.Distance(player, entry.place.Cell) > LeaveCells)
                .OrderBy(entry => Vector2.Distance(player, entry.place.Cell))
                .Cast<((Vector2 Cell, bool Told) place, int index)?>()
                .FirstOrDefault();
            if (left is not { } told)
            {
                return null;
            }

            // The edge of one nook is many blocks: all of it within a look is the same ground, told once.
            for (var i = 0; i < this.known.Count; i++)
            {
                if (Vector2.Distance(this.known[i].Cell, told.place.Cell) <= ExplorationMap.SeenRadiusCells)
                {
                    this.known[i] = this.known[i] with { Told = true };
                }
            }

            this.toldAt = nowMs;
            return told.place.Cell;
        }
    }
}
