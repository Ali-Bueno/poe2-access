namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>The small unseen ground near the player, of <see cref="ExplorationMap"/>: what <see cref="LeftBehindWatch"/> follows.</summary>
    public sealed partial class ExplorationMap
    {
        /// <summary>
        ///     The edge blocks (centres, in grid cells) of unseen ground the player can reach that soon ends, nooks included,
        ///     within <paramref name="radius"/> of <paramref name="from"/>, by the last <see cref="Frontier"/>'s survey.
        /// </summary>
        public IReadOnlyList<Vector2> SmallUnseenNear(Vector2 from, float radius)
        {
            var (reachable, unseenGround, _) = this.ground;
            var cells = new List<Vector2>();
            if (reachable.Length == 0 || reachable.Length != this.seen.Length)
            {
                return cells;
            }

            var reach = (int)MathF.Ceiling(radius / BlockCells);
            var (centreX, centreY) = ((int)(from.X / BlockCells), (int)(from.Y / BlockCells));
            for (var y = Math.Max(0, centreY - reach); y <= Math.Min(this.blocksY - 1, centreY + reach); y++)
            {
                for (var x = Math.Max(0, centreX - reach); x <= Math.Min(this.blocksX - 1, centreX + reach); x++)
                {
                    var block = (y * this.blocksX) + x;
                    if (reachable[block] && !this.seen[block] && unseenGround[block] is > 0 and < SmallBlocks && this.BesideSeen(block, null) &&
                        Vector2.Distance(this.Centre(block), from) <= radius)
                    {
                        cells.Add(this.Centre(block));
                    }
                }
            }

            return cells;
        }
    }
}
