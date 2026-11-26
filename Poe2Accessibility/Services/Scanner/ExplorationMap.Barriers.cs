namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The ground of <see cref="ExplorationMap"/> past closed barriers: what a player penned in by a gate has not explored
    ///     (Hunting Grounds, 2026-11-26: the Crowbell's gate shut behind the player and the mod said the whole map was
    ///     explored, with Freythorn 170 steps away past it). docs/superpowers/specs/2026-11-26-ground-behind-closed-barriers-design.md.
    /// </summary>
    public sealed partial class ExplorationMap
    {
        /// <summary>Pieces nearer than this are one gate remembered in pieces (317 and 3 cells a few rows apart, live).</summary>
        private const float GateMergeCells = 2 * BlockCells;

        /// <summary>
        ///     One place per barrier with unseen ground past it worth a look, at the barrier's near side, and per block the
        ///     ground reached only through a barrier (<paramref name="behind"/>) with the unseen ground behind that barrier
        ///     (<paramref name="behindGround"/>). A cell is past a barrier when the walk from the player reaches it only once
        ///     the cells <paramref name="closedMask"/> holds closed are open; it belongs to the first barrier on that walk. A
        ///     barrier is the pieces of the mask that touch the player's ground, joined when their outlines lie within
        ///     <see cref="GateMergeCells"/>, and keeps its first cell as its identity wherever the player stands.
        /// </summary>
        private List<FrontierPlace> BarrierPlaces(byte[] grid, int bytesPerRow, int width, int height, Vector2 from, byte[] closedMask, bool[] behind, int[] behindGround)
        {
            var places = new List<FrontierPlace>();
            if (NearestWalkable(grid, bytesPerRow, width, height, from) is not { } start)
            {
                return places;
            }

            var cells = width * height;
            lock (this.walks)
            {
                Walk(grid, bytesPerRow, width, height, start, this.walks, true);
                var (marks, steps, queue) = (this.walks.Marks, this.walks.Steps, this.walks.Queue);
                var labels = this.walks.FitLabels(cells);
                var pieces = new List<Piece> { default }; // by label; label 0 is none
                var stack = new Stack<int>();
                queue.Clear();

                // The pieces of the mask that touch the player's ground: each with its outline, its first cell and its gate,
                // the reached cell beside it with the shortest walk.
                for (var i = 0; i < closedMask.Length && pieces.Count < ushort.MaxValue; i++) // labels are ushort: half the walk's steps
                {
                    if (closedMask[i] == 0)
                    {
                        continue;
                    }

                    for (var half = 0; half < 2; half++)
                    {
                        var (x, y) = (((i % bytesPerRow) * 2) + half, i / bytesPerRow);
                        if (x >= width || y >= height || !Walkable(closedMask, bytesPerRow, x, y))
                        {
                            continue;
                        }

                        var seed = (y * width) + x;
                        if (marks[seed] != 0 || labels[seed] != 0 || ReachedBeside(seed) is null)
                        {
                            continue;
                        }

                        var label = pieces.Count;
                        var piece = new Piece(x, y, x, y, seed, -1, int.MaxValue);
                        labels[seed] = (ushort)label;
                        stack.Push(seed);
                        while (stack.TryPop(out var cell))
                        {
                            queue.Enqueue(cell);
                            var (cx, cy) = (cell % width, cell / width);
                            piece = piece with
                            {
                                Left = Math.Min(piece.Left, cx), Top = Math.Min(piece.Top, cy), Right = Math.Max(piece.Right, cx), Bottom = Math.Max(piece.Bottom, cy),
                                First = Math.Min(piece.First, cell),
                            };
                            if (ReachedBeside(cell) is { } beside && steps[beside] < piece.GateSteps)
                            {
                                piece = piece with { Gate = beside, GateSteps = steps[beside] };
                            }

                            for (var dy = -1; dy <= 1; dy++)
                            {
                                for (var dx = -1; dx <= 1; dx++)
                                {
                                    var (nx, ny) = (cx + dx, cy + dy);
                                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || !Walkable(closedMask, bytesPerRow, nx, ny))
                                    {
                                        continue;
                                    }

                                    var next = (ny * width) + nx;
                                    if (labels[next] == 0 && marks[next] == 0)
                                    {
                                        labels[next] = (ushort)label;
                                        stack.Push(next);
                                    }
                                }
                            }
                        }

                        pieces.Add(piece);
                    }
                }

                if (pieces.Count == 1)
                {
                    return places;
                }

                // Pieces whose outlines nearly touch are one gate, wherever the player stands along it.
                var gate = Enumerable.Range(0, pieces.Count).ToArray();
                for (var a = 1; a < pieces.Count; a++)
                {
                    for (var b = a + 1; b < pieces.Count; b++)
                    {
                        if (Apart(pieces[a], pieces[b]) <= GateMergeCells)
                        {
                            gate[Find(b)] = Find(a);
                        }
                    }
                }

                // Out from the barriers over the ground the player cannot reach: each cell keeps the piece it came through.
                while (queue.TryDequeue(out var cell))
                {
                    var (x, y) = (cell % width, cell / width);
                    Spread(x - 1, y, labels[cell]);
                    Spread(x + 1, y, labels[cell]);
                    Spread(x, y - 1, labels[cell]);
                    Spread(x, y + 1, labels[cell]);
                }

                // Per gate, the unseen blocks of the ground past it.
                var unseen = new int[pieces.Count];
                var owner = new int[behind.Length];
                for (var cell = 0; cell < cells; cell++)
                {
                    if (labels[cell] == 0)
                    {
                        continue;
                    }

                    var (x, y) = (cell % width, cell / width);
                    if (!Walkable(grid, bytesPerRow, x, y))
                    {
                        continue;
                    }

                    var block = this.BlockOf(cell, width);
                    behind[block] = true;
                    var root = Find(labels[cell]);
                    if (!this.seen[block] && owner[block] == 0)
                    {
                        owner[block] = root;
                        unseen[root]++;
                    }
                }

                for (var block = 0; block < behind.Length; block++)
                {
                    behindGround[block] = owner[block] == 0 ? 0 : unseen[owner[block]];
                }

                foreach (var group in Enumerable.Range(1, pieces.Count - 1).GroupBy(Find))
                {
                    var nearest = group.Select(label => pieces[label]).Where(piece => piece.Gate >= 0).OrderBy(piece => piece.GateSteps).Cast<Piece?>().FirstOrDefault();
                    if (nearest is not { } near || unseen[group.Key] < PocketBlocks)
                    {
                        continue;
                    }

                    var first = group.Min(label => pieces[label].First);
                    places.Add(new FrontierPlace(Centre(near.Gate), SizeOf(unseen[group.Key]), near.GateSteps, Array.Empty<int>(), true) { Barrier = Centre(first) });
                }

                return places;

                int Find(int label)
                {
                    while (gate[label] != label)
                    {
                        label = gate[label] = gate[gate[label]];
                    }

                    return label;
                }

                Vector2 Centre(int cell) => new((cell % width) + 0.5f, (cell / width) + 0.5f);

                // The reached cell beside this one (four neighbours) with the shortest walk; null when none is reached.
                int? ReachedBeside(int cell)
                {
                    var (x, y) = (cell % width, cell / width);
                    int? best = null;
                    Consider(x - 1, y);
                    Consider(x + 1, y);
                    Consider(x, y - 1);
                    Consider(x, y + 1);
                    return best;

                    void Consider(int nx, int ny)
                    {
                        var next = (ny * width) + nx;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && marks[next] != 0 && (best is not { } known || steps[next] < steps[known]))
                        {
                            best = next;
                        }
                    }
                }

                void Spread(int x, int y, int label)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        return;
                    }

                    var next = (y * width) + x;
                    if (marks[next] == 0 && labels[next] == 0 && Walkable(grid, closedMask, bytesPerRow, x, y))
                    {
                        labels[next] = (ushort)label;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        /// <summary>The gap between two pieces' outlines, in cells; 0 when they touch or overlap.</summary>
        private static float Apart(Piece a, Piece b)
        {
            var dx = Math.Max(0, Math.Max(a.Left - b.Right, b.Left - a.Right));
            var dy = Math.Max(0, Math.Max(a.Top - b.Bottom, b.Top - a.Bottom));
            return MathF.Sqrt((dx * dx) + (dy * dy));
        }

        /// <summary>A piece of the barrier mask: its outline (cells), its first cell, and its gate (the reached cell beside it with the shortest walk, -1 for none).</summary>
        private readonly record struct Piece(int Left, int Top, int Right, int Bottom, int First, int Gate, int GateSteps);
    }
}
