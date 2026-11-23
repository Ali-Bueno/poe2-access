namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>How far on foot each cell of the map is from one place, for following a trail there (docs/research/map-hints.md). Pure.</summary>
    public sealed class TrailField
    {
        /// <summary>The player may stand on a cell the grid closes: the ground beside counts this far off.</summary>
        private const int StandCells = 8;

        /// <summary>A trail's spot may lie in water or a wall: the ground beside it counts this far off.</summary>
        private const int SpotCells = 64;

        /// <summary>Two spots nearer than this stand on the same stretch: one is measured.</summary>
        private const int StretchCells = 12;

        private const int Sides = 4;

        private readonly ushort[] steps;
        private readonly int width;
        private readonly int height;

        private TrailField(ushort[] steps, int width, int height)
        {
            this.steps = steps;
            this.width = width;
            this.height = height;
        }

        /// <summary>The walks to <paramref name="goal"/> over the walkable grid; null when no ground lies by the goal. Tens of ms: off the frame.</summary>
        public static TrailField? Build(byte[] grid, int bytesPerRow, Vector2 goal)
        {
            if (bytesPerRow <= 0 || grid.Length < bytesPerRow || RoutePlanner.Snap(grid, bytesPerRow, goal) is not { } end)
            {
                return null;
            }

            var width = bytesPerRow * 2;
            var height = grid.Length / bytesPerRow;
            var steps = new ushort[width * height]; // 0 = not reached, else the steps + 1
            var queue = new Queue<int>();
            var start = ((int)end.Y * width) + (int)end.X;
            steps[start] = 1;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var next = (ushort)Math.Min(ushort.MaxValue, steps[cell] + 1);
                for (var side = 0; side < Sides; side++)
                {
                    var other = Beside(cell, side, width, height);
                    if (other >= 0 && steps[other] == 0 && RoutePlanner.IsWalkable(grid, bytesPerRow, other % width, other / width))
                    {
                        steps[other] = next;
                        queue.Enqueue(other);
                    }
                }
            }

            return new TrailField(steps, width, height);
        }

        /// <summary>Cells to walk from here to the goal; null with no way there.</summary>
        public int? Steps(Vector2 cell) => this.Near(cell, StandCells) is { } index ? this.steps[index] - 1 : null;

        /// <summary>Where the walk to the goal has got after <paramref name="count"/> cells from here; null with no way there.</summary>
        public Vector2? Ahead(Vector2 cell, int count)
        {
            if (this.Near(cell, StandCells) is not { } index)
            {
                return null;
            }

            for (; count > 0 && this.steps[index] > 1; count--)
            {
                for (var side = 0; side < Sides; side++)
                {
                    var other = Beside(index, side, this.width, this.height);
                    if (other >= 0 && this.steps[other] != 0 && this.steps[other] < this.steps[index])
                    {
                        index = other;
                        break;
                    }
                }
            }

            return new Vector2(index % this.width, index / this.width);
        }

        /// <summary>The walk from the trail's end nearest the goal and from its far end; null when no ground lies by any spot.</summary>
        public (int Near, int Far)? Ends(IEnumerable<Vector2> spots)
        {
            var measured = new HashSet<(int, int)>();
            (int Near, int Far)? ends = null;
            foreach (var spot in spots)
            {
                if (measured.Add(((int)spot.X / StretchCells, (int)spot.Y / StretchCells)) && this.Near(spot, SpotCells) is { } index)
                {
                    var walk = this.steps[index] - 1;
                    ends = ends is { } known ? (Math.Min(known.Near, walk), Math.Max(known.Far, walk)) : (walk, walk);
                }
            }

            return ends;
        }

        /// <summary>The cell beside this one to the left, right, up or down (0 to 3); -1 off the map.</summary>
        private static int Beside(int cell, int side, int width, int height) => side switch
        {
            0 => cell % width > 0 ? cell - 1 : -1,
            1 => cell % width < width - 1 ? cell + 1 : -1,
            2 => cell >= width ? cell - width : -1,
            _ => cell < width * (height - 1) ? cell + width : -1,
        };

        /// <summary>The nearest reached cell, ring by ring.</summary>
        private int? Near(Vector2 cell, int reach)
        {
            var (cx, cy) = ((int)cell.X, (int)cell.Y);
            for (var radius = 0; radius <= reach; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var across = Math.Abs(dy) == radius ? 1 : 2 * radius;
                    for (var dx = -radius; dx <= radius; dx += across)
                    {
                        var (x, y) = (cx + dx, cy + dy);
                        if (x >= 0 && x < this.width && y >= 0 && y < this.height && this.steps[(y * this.width) + x] != 0)
                        {
                            return (y * this.width) + x;
                        }
                    }
                }
            }

            return null;
        }
    }
}
