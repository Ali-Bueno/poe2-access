namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     The walkable grid split into the pieces of ground joined to each other (eight neighbours, so a corner gap joins: the
    ///     doubt is on the side of "reachable"). Two cells apart cannot be walked between. Pure.
    /// </summary>
    public sealed class WalkableRegions
    {
        /// <summary>A cell this near the walkable ground stands on it: characters and the player stand at the edge of walls.</summary>
        public const int SnapCells = 4;

        private readonly int[] labels;
        private readonly int width;
        private readonly int height;

        private WalkableRegions(int[] labels, int width, int height)
        {
            this.labels = labels;
            this.width = width;
            this.height = height;
        }

        /// <summary>Labels every walkable cell of a nibble grid (<see cref="RoutePlanner.IsWalkable"/>) with its piece of ground.</summary>
        public static WalkableRegions Of(byte[] grid, int bytesPerRow)
        {
            var width = bytesPerRow * 2;
            var height = bytesPerRow > 0 ? grid.Length / bytesPerRow : 0;
            var labels = new int[width * height];
            var stack = new Stack<int>();
            var next = 0;
            for (var start = 0; start < labels.Length; start++)
            {
                if (labels[start] != 0 || !RoutePlanner.IsWalkable(grid, bytesPerRow, start % width, start / width))
                {
                    continue;
                }

                labels[start] = ++next;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var cell = stack.Pop();
                    var (x, y) = (cell % width, cell / width);
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var (nx, ny) = (x + dx, y + dy);
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                            {
                                continue;
                            }

                            var near = (ny * width) + nx;
                            if (labels[near] == 0 && RoutePlanner.IsWalkable(grid, bytesPerRow, nx, ny))
                            {
                                labels[near] = next;
                                stack.Push(near);
                            }
                        }
                    }
                }
            }

            return new WalkableRegions(labels, width, height);
        }

        /// <summary>The piece of ground at a grid cell, or the nearest within <see cref="SnapCells"/>; 0 when none.</summary>
        public int RegionAt(Vector2 cell)
        {
            var (cx, cy) = ((int)MathF.Round(cell.X), (int)MathF.Round(cell.Y));
            for (var radius = 0; radius <= SnapCells; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var (x, y) = (cx + dx, cy + dy);
                        if ((Math.Abs(dx) == radius || Math.Abs(dy) == radius) && x >= 0 && y >= 0 && x < this.width && y < this.height &&
                            this.labels[(y * this.width) + x] is var label and > 0)
                        {
                            return label;
                        }
                    }
                }
            }

            return 0;
        }

        /// <summary>Both cells stand on walkable ground, and on two pieces of it no walk joins.</summary>
        public bool Apart(Vector2 a, Vector2 b) => this.RegionAt(a) is var first and > 0 && this.RegionAt(b) is var second and > 0 && first != second;
    }
}
