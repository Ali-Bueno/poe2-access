namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>The parts of <see cref="ExplorationMap"/>: how much of each third of the map has been seen, from the last frontier's reachable ground.</summary>
    public sealed partial class ExplorationMap
    {
        private const int PartsAcross = 3;

        /// <summary>
        ///     The parts of the map (<see cref="MapFrame.Part"/>) holding ground worth a look, left to right and top to bottom:
        ///     the share of each that has been seen and whether only nooks are left in it. Empty before the first
        ///     <see cref="Frontier"/>.
        /// </summary>
        public IReadOnlyList<MapPart> Parts(MapFrame frame)
        {
            var (reachable, unseenGround, _) = this.ground;
            if (reachable.Length == 0 || reachable.Length != this.seen.Length)
            {
                return Array.Empty<MapPart>();
            }

            // Ground past a closed barrier is ground of the map still to explore (Hunting Grounds, 2026-11-26).
            var (behind, behindGround) = this.behind.Behind.Length == reachable.Length ? this.behind : (new bool[reachable.Length], new int[reachable.Length]);

            var total = new int[PartsAcross * PartsAcross];
            var seenBlocks = new int[total.Length];
            var goesOn = new int[total.Length];
            var splits = frame.Splits(SeenRadiusCells);
            for (var block = 0; block < reachable.Length; block++)
            {
                if (!reachable[block] && !behind[block])
                {
                    continue;
                }

                var (across, down) = frame.Part(this.Centre(block), splits);
                var part = ((down + 1) * PartsAcross) + across + 1;
                total[part]++;
                if (this.seen[block])
                {
                    seenBlocks[part]++;
                }
                else if (Math.Max(unseenGround[block], behindGround[block]) >= PocketBlocks)
                {
                    goesOn[part]++;
                }
            }

            var parts = new List<MapPart>();
            for (var part = 0; part < total.Length; part++)
            {
                // Less ground than a nook is the fringe of the part beside it.
                if (total[part] >= PocketBlocks)
                {
                    parts.Add(new MapPart((part % PartsAcross) - 1, (part / PartsAcross) - 1, seenBlocks[part] * 100 / total[part], goesOn[part] == 0));
                }
            }

            return parts;
        }
    }

    /// <summary>A third of the map across and down (-1, 0, 1): the percent of its ground seen, and whether nothing but nooks is left unseen in it.</summary>
    public readonly record struct MapPart(int Across, int Down, int Percent, bool Explored);
}
