namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Finds the Actor's heavy stun record, which nothing else checks: it starts {int 5, float 8.0, int 0}, then the
    ///     threshold and the buildup, in every monster alike (<see cref="GameLayout.ActorComponent"/>). The head names the
    ///     place, so a patch that moves it is followed without anyone being hit.
    /// </summary>
    public static class PoiseSearch
    {
        public const int ThresholdFromHead = 0xC;
        public const int BuildupFromHead = 0x10;

        /// <summary>Fewer actors than this say nothing: an empty room is no evidence.</summary>
        public const int MinActors = 5;

        private const int HeadKind = 5;
        private const float HeadRate = 8f;
        private const int RecordSize = 0x14;
        private const int Step = sizeof(int);
        private const double AgreedShare = 0.8;

        /// <summary>Each place of a block of an Actor that holds the head and a threshold.</summary>
        public static IReadOnlyList<int> Heads(ReadOnlySpan<byte> block)
        {
            var heads = new List<int>();
            for (var at = 0; at + RecordSize <= block.Length; at += Step)
            {
                if (BitConverter.ToInt32(block[at..]) == HeadKind &&
                    BitConverter.ToSingle(block[(at + Step)..]) == HeadRate &&
                    BitConverter.ToInt32(block[(at + (2 * Step))..]) == 0 &&
                    BitConverter.ToInt32(block[(at + ThresholdFromHead)..]) > 0)
                {
                    heads.Add(at);
                }
            }

            return heads;
        }

        /// <summary>The place most actors share, nearest to <paramref name="measured"/>; null when too few agree.</summary>
        public static int? Agreed(IReadOnlyList<IReadOnlyList<int>> actors, int measured)
        {
            if (actors.Count < MinActors)
            {
                return null;
            }

            var needed = actors.Count * AgreedShare;
            var shared = actors.SelectMany(heads => heads.Distinct())
                .GroupBy(head => head)
                .Where(group => group.Count() >= needed)
                .Select(group => group.Key)
                .OrderBy(head => Math.Abs(head - measured))
                .ToList();
            return shared.Count > 0 ? shared[0] : null;
        }

        /// <summary>
        ///     The one place most actors share, for a search of the whole component once none is near where measured: two
        ///     shared places are not chosen between (the head's three values can stand elsewhere by chance).
        /// </summary>
        public static int? AgreedOnly(IReadOnlyList<IReadOnlyList<int>> actors)
        {
            if (actors.Count < MinActors)
            {
                return null;
            }

            var needed = actors.Count * AgreedShare;
            var shared = actors.SelectMany(heads => heads.Distinct()).GroupBy(head => head).Where(group => group.Count() >= needed).Select(group => group.Key).ToList();
            return shared.Count == 1 ? shared[0] : null;
        }
    }

    /// <summary>
    ///     Whether the walkable grid still means what the mod reads into it: where the player has stood must read walkable.
    ///     A grid whose cells changed meaning would leave the guide and the radar wrong with no field out of place.
    /// </summary>
    public sealed class WalkableCheck
    {
        private const int EnoughCells = 30;
        private const double WalkableShare = 0.7;

        private readonly HashSet<(int X, int Y)> cells = new();
        private int walkable;

        /// <summary>Notes a cell the player stands on; once enough were seen, whether the grid held up.</summary>
        public bool? Add(int x, int y, bool isWalkable)
        {
            if (!this.cells.Add((x, y)))
            {
                return null;
            }

            this.walkable += isWalkable ? 1 : 0;
            if (this.cells.Count < EnoughCells)
            {
                return null;
            }

            var held = this.walkable >= this.cells.Count * WalkableShare;
            this.cells.Clear();
            this.walkable = 0;
            return held;
        }
    }
}
