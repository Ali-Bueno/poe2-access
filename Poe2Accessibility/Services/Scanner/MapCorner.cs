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

        internal const float Third = 1f / 3f;

        /// <summary>-1, 0 or 1 across (left to right) and down (top to bottom); null without ground.</summary>
        /// <param name="cell">The place in grid cells.</param>
        /// <param name="alongX">How the screen draws a step along grid +X.</param>
        /// <param name="alongY">How the screen draws a step along grid +Y.</param>
        public static (int Across, int Down)? Of(byte[] grid, int bytesPerRow, Vector2 cell, Vector2 alongX, Vector2 alongY) =>
            Frame(grid, bytesPerRow, alongX, alongY)?.Part(cell);

        /// <summary>The ends of the area's ground as the screen draws it; null without ground. It sorts every block: once per area.</summary>
        public static MapFrame? Frame(byte[] grid, int bytesPerRow, Vector2 alongX, Vector2 alongY)
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

            var (left, right) = Ends(across);
            var (top, bottom) = Ends(down);
            return new MapFrame(left, right, top, bottom, alongX, alongY);
        }

        /// <summary>The third a share of the map (0 to 1) falls in: -1, 0 or 1.</summary>
        public static int ThirdOf(float share) => share < Third ? -1 : share > 1f - Third ? 1 : 0;

        internal static Vector2 Drawn(Vector2 cell, Vector2 alongX, Vector2 alongY) => (cell.X * alongX) + (cell.Y * alongY);

        private static (float Low, float High) Ends(List<float> values)
        {
            values.Sort();
            return (values[(int)(StrayShare * (values.Count - 1))], values[(int)((1f - StrayShare) * (values.Count - 1))]);
        }
    }

    /// <summary>The area's ground as the screen draws it, between its ends across and down. Pure.</summary>
    public readonly record struct MapFrame(float Left, float Right, float Top, float Bottom, Vector2 AlongX, Vector2 AlongY)
    {
        private const float Middle = 0.5f;

        /// <summary>Where a place (grid cells) lies between the ends, 0 to 1 across and down; the middle where the ground has no extent.</summary>
        public Vector2 Share(Vector2 cell)
        {
            var drawn = MapCorner.Drawn(cell, this.AlongX, this.AlongY);
            return new Vector2(
                this.Right > this.Left ? (drawn.X - this.Left) / (this.Right - this.Left) : Middle,
                this.Bottom > this.Top ? (drawn.Y - this.Top) / (this.Bottom - this.Top) : Middle);
        }

        /// <summary>The share of the map, across and down, that a length in grid cells is drawn over; the whole of it where the ground has no extent.</summary>
        public Vector2 Span(float cells) => new(
            this.Right > this.Left ? cells * MathF.Sqrt((this.AlongX.X * this.AlongX.X) + (this.AlongY.X * this.AlongY.X)) / (this.Right - this.Left) : 1f,
            this.Bottom > this.Top ? cells * MathF.Sqrt((this.AlongX.Y * this.AlongX.Y) + (this.AlongY.Y * this.AlongY.Y)) / (this.Bottom - this.Top) : 1f);

        /// <summary>
        ///     Whether the map has thirds across and down for a player who sees <paramref name="lookCells"/> around: a third
        ///     narrower than one look is no place of its own, and that way the map is one part.
        /// </summary>
        public (bool Across, bool Down) Splits(float lookCells)
        {
            var look = this.Span(2 * lookCells);
            return (look.X <= MapCorner.Third, look.Y <= MapCorner.Third);
        }

        /// <summary>The part a place lies in, -1, 0 or 1 across and down; with a look, only the ways the map splits (<see cref="Splits"/>).</summary>
        public (int Across, int Down) Part(Vector2 cell, float lookCells = 0f) => this.Part(cell, this.Splits(lookCells));

        /// <summary>The part a place lies in by the ways the map splits, for many places asked in a row.</summary>
        public (int Across, int Down) Part(Vector2 cell, (bool Across, bool Down) splits)
        {
            var share = this.Share(cell);
            return (splits.Across ? MapCorner.ThirdOf(share.X) : 0, splits.Down ? MapCorner.ThirdOf(share.Y) : 0);
        }
    }
}
