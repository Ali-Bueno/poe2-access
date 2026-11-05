namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>Which part of the area's ground a place lies in as the screen draws it, by thirds across and down. Pure.</summary>
    public static class MapCorner
    {
        /// <summary>This share of the ground at each end does not stretch the map: a stray patch far out is no edge.</summary>
        private const float StrayShare = 0.02f;

        private const float Third = 1f / 3f;

        /// <summary>-1, 0 or 1 across (left to right) and down (top to bottom); null without ground.</summary>
        /// <param name="cell">The place in grid cells.</param>
        /// <param name="alongX">How the screen draws a step along grid +X.</param>
        /// <param name="alongY">How the screen draws a step along grid +Y.</param>
        public static (int Across, int Down)? Of(byte[] grid, int bytesPerRow, Vector2 cell, Vector2 alongX, Vector2 alongY)
        {
            if (bytesPerRow <= 0 || grid.Length < bytesPerRow)
            {
                return null;
            }

            var width = bytesPerRow * 2;
            var height = grid.Length / bytesPerRow;
            var blocks = ExplorationMap.WalkableBlocks(grid, bytesPerRow, width, height);
            var blocksAcross = (width + ExplorationMap.BlockCells - 1) / ExplorationMap.BlockCells;
            var across = new List<float>();
            var down = new List<float>();
            for (var block = 0; block < blocks.Length; block++)
            {
                if (blocks[block])
                {
                    var centre = new Vector2(block % blocksAcross, block / blocksAcross) * ExplorationMap.BlockCells;
                    var drawn = Drawn(centre, alongX, alongY);
                    across.Add(drawn.X);
                    down.Add(drawn.Y);
                }
            }

            if (across.Count == 0)
            {
                return null;
            }

            var here = Drawn(cell, alongX, alongY);
            return (Part(across, here.X), Part(down, here.Y));
        }

        private static Vector2 Drawn(Vector2 cell, Vector2 alongX, Vector2 alongY) => (cell.X * alongX) + (cell.Y * alongY);

        private static int Part(List<float> values, float at)
        {
            values.Sort();
            var low = values[(int)(StrayShare * (values.Count - 1))];
            var high = values[(int)((1f - StrayShare) * (values.Count - 1))];
            if (high <= low)
            {
                return 0;
            }

            var share = (at - low) / (high - low);
            return share < Third ? -1 : share > 1f - Third ? 1 : 0;
        }
    }
}
