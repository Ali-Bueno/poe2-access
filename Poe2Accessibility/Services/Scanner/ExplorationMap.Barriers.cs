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
        /// <summary>Gates nearer than this are one gate remembered in pieces (317 and 3 cells a few rows apart, live).</summary>
        private const float GateMergeCells = 2 * BlockCells;

        /// <summary>
        ///     One place per barrier with unseen ground past it worth a look, at the barrier's near side, and per block the
        ///     ground reached only through a barrier (<paramref name="behind"/>) with the unseen ground behind that barrier
        ///     (<paramref name="behindGround"/>). A cell is past a barrier when the walk from the player reaches it only once
        ///     the cells <paramref name="closedMask"/> holds closed are open; it belongs to the first barrier on that walk.
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
                var gates = new List<(int Cell, int Steps)> { (-1, int.MaxValue) }; // by label; label 0 is none
                var piece = new Stack<int>();
                queue.Clear();

                // The pieces of the mask that touch the player's ground, each a barrier with its gate: the reached cell beside it with the shortest walk.
                for (var i = 0; i < closedMask.Length; i++)
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

                        var label = gates.Count;
                        var gate = (Cell: -1, Steps: int.MaxValue);
                        labels[seed] = label;
                        piece.Push(seed);
                        while (piece.TryPop(out var cell))
                        {
                            queue.Enqueue(cell);
                            if (ReachedBeside(cell) is { } beside && steps[beside] < gate.Steps)
                            {
                                gate = (beside, steps[beside]);
                            }

                            var (cx, cy) = (cell % width, cell / width);
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
                                        labels[next] = label;
                                        piece.Push(next);
                                    }
                                }
                            }
                        }

                        gates.Add(gate);
                    }
                }

                if (gates.Count == 1)
                {
                    return places;
                }

                // Out from the barriers over the ground the player cannot reach: each cell keeps the barrier it came through.
                while (queue.TryDequeue(out var cell))
                {
                    var (x, y) = (cell % width, cell / width);
                    Spread(x - 1, y, labels[cell]);
                    Spread(x + 1, y, labels[cell]);
                    Spread(x, y - 1, labels[cell]);
                    Spread(x, y + 1, labels[cell]);
                }

                // Per barrier, the unseen blocks of the ground past it.
                var unseen = new int[gates.Count];
                var owner = new int[behind.Length];
                for (var cell = 0; cell < cells; cell++)
                {
                    var (x, y) = (cell % width, cell / width);
                    if (labels[cell] == 0 || !Walkable(grid, bytesPerRow, x, y))
                    {
                        continue;
                    }

                    var block = this.BlockOf(cell, width);
                    behind[block] = true;
                    if (!this.seen[block] && owner[block] == 0)
                    {
                        owner[block] = labels[cell];
                        unseen[labels[cell]]++;
                    }
                }

                for (var block = 0; block < behind.Length; block++)
                {
                    behindGround[block] = owner[block] == 0 ? 0 : unseen[owner[block]];
                }

                // A gate beside a larger one is the same gate: its ground joins the larger one's.
                var kept = new List<(int Gate, int Unseen)>();
                foreach (var label in Enumerable.Range(1, gates.Count - 1).Where(label => gates[label].Cell >= 0).OrderByDescending(label => unseen[label]))
                {
                    var at = Centre(gates[label].Cell);
                    var same = kept.FindIndex(gate => Vector2.Distance(Centre(gate.Gate), at) <= GateMergeCells);
                    if (same >= 0)
                    {
                        kept[same] = (kept[same].Gate, kept[same].Unseen + unseen[label]);
                    }
                    else
                    {
                        kept.Add((gates[label].Cell, unseen[label]));
                    }
                }

                foreach (var (gate, ground) in kept.Where(gate => gate.Unseen >= PocketBlocks))
                {
                    places.Add(new FrontierPlace(Centre(gate), SizeOf(ground), steps[gate], Array.Empty<int>(), true));
                }

                return places;

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
                        labels[next] = label;
                        queue.Enqueue(next);
                    }
                }
            }
        }
    }
}
