namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>The ground shared out among places and ways for <see cref="MapTopology.Build"/>, and the narrow passages along the ways.</summary>
    public sealed partial class MapTopology
    {
        private sealed partial class Builder
        {
            /// <summary>
            ///     Gives every walkable cell to the place or way of its nearest skeleton or room core cell (a walk from all at once), sums
            ///     each one's area and box, and keeps the owners per <see cref="OwnerBlock"/> block for lookups.
            /// </summary>
            private void ShareGround(Draft draft, List<Link> links, int[] segmentLink, int[] segmentPlace, int[] linkEdge, int[] placeNode)
            {
                var nodes = draft.Kinds.Length;
                var items = nodes + draft.EdgeA.Length;
                var seeds = new int[this.skeletonCells.Count];
                for (var i = 0; i < seeds.Length; i++)
                {
                    var label = this.labels[this.skeletonCells[i]];
                    var (s, item) = (label - 1, -1);
                    if (label < 0)
                    {
                        item = placeNode[-label - 1];
                    }
                    else if (segmentLink[s] >= 0)
                    {
                        var link = segmentLink[s];
                        item = linkEdge[link] >= 0 ? nodes + linkEdge[link] : placeNode[links[link].A];
                    }
                    else if (segmentPlace[s] >= 0)
                    {
                        item = placeNode[segmentPlace[s]];
                    }

                    seeds[i] = item;
                }

                Array.Clear(this.labels);
                var count = 0;
                for (var i = 0; i < seeds.Length; i++)
                {
                    var p = this.skeletonCells[i];
                    this.labels[p] = seeds[i] >= 0 ? seeds[i] + 1 : -1;
                    this.queue[count++] = p;
                }

                for (var i = 0; i < this.coreCells.Count; i++)
                {
                    var p = this.coreCells[i];
                    if (this.labels[p] == 0)
                    {
                        this.labels[p] = placeNode[this.coreRooms[i]] + 1;
                        this.queue[count++] = p;
                    }
                }

                for (var head = 0; head < count; head++)
                {
                    var p = this.queue[head];
                    foreach (var offset in this.around)
                    {
                        var q = p + offset;
                        if (this.walk[q] != 0 && this.labels[q] == 0)
                        {
                            this.labels[q] = this.labels[p];
                            this.queue[count++] = q;
                        }
                    }
                }

                this.cancel.ThrowIfCancellationRequested();
                draft.ItemArea = new int[items];
                draft.ItemBox = new int[4 * items];
                for (var i = 0; i < items; i++)
                {
                    (draft.ItemBox[4 * i], draft.ItemBox[(4 * i) + 1], draft.ItemBox[(4 * i) + 2], draft.ItemBox[(4 * i) + 3]) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
                }

                (draft.Left, draft.Top) = (this.left, this.top);
                (draft.BlocksX, draft.BlocksY) = ((this.width + OwnerBlock - 1) / OwnerBlock, (this.height + OwnerBlock - 1) / OwnerBlock);
                draft.Owner = new ushort[draft.BlocksX * draft.BlocksY];
                for (var p = 0; p < this.labels.Length; p++)
                {
                    var item = this.labels[p] - 1;
                    if (this.walk[p] == 0 || item < 0)
                    {
                        continue;
                    }

                    var (x, y) = (p % this.width, p / this.width);
                    draft.ItemArea[item]++;
                    var box = 4 * item;
                    (draft.ItemBox[box], draft.ItemBox[box + 1]) = (Math.Min(draft.ItemBox[box], x + this.left), Math.Min(draft.ItemBox[box + 1], y + this.top));
                    (draft.ItemBox[box + 2], draft.ItemBox[box + 3]) = (Math.Max(draft.ItemBox[box + 2], x + this.left), Math.Max(draft.ItemBox[box + 3], y + this.top));
                    var block = ((y / OwnerBlock) * draft.BlocksX) + (x / OwnerBlock);
                    if (draft.Owner[block] == 0 && item < ushort.MaxValue)
                    {
                        draft.Owner[block] = (ushort)(item + 1);
                    }
                }
            }

            /// <summary>
            ///     The narrow stretches of each way: skeleton cells where the walkable width across the way is at most
            ///     <see cref="NarrowWidthCells"/>, stretches closer than <see cref="NarrowJoinCells"/> joined, each told at its narrowest cell.
            /// </summary>
            private void FindNarrows(Draft draft, List<Link> links, int[] linkEdge, List<int> path)
            {
                for (var i = 0; i < links.Count; i++)
                {
                    var (link, edge) = (links[i], linkEdge[i]);
                    if (edge < 0 || link.Count == 0)
                    {
                        continue;
                    }

                    var (best, bestWidth, gap) = (-1, float.MaxValue, NarrowJoinCells);
                    for (var k = 0; k <= link.Count; k++)
                    {
                        var across = k < link.Count && this.distance[path[link.Start + k]] <= (NarrowWidthCells / 2) + 1 ? this.Across(path, link, k) : float.MaxValue;
                        if (across <= NarrowWidthCells)
                        {
                            (best, bestWidth, gap) = across < bestWidth ? (k, across, 0) : (best, bestWidth, 0);
                            continue;
                        }

                        if (best >= 0 && (++gap >= NarrowJoinCells || k == link.Count))
                        {
                            draft.Narrows.Add(new Narrow(this.GridCell(path[link.Start + best]), bestWidth, this.Along(path, link, best), false, edge));
                            (best, bestWidth) = (-1, float.MaxValue);
                        }
                    }
                }
            }

            /// <summary>The unit direction of a way at its <paramref name="k"/>th cell, over <see cref="NarrowDirectionCells"/> cells either side.</summary>
            private Vector2 Along(List<int> path, Link link, int k)
            {
                var from = this.CropCell(path[link.Start + Math.Max(0, k - NarrowDirectionCells)]);
                var to = this.CropCell(path[link.Start + Math.Min(link.Count - 1, k + NarrowDirectionCells)]);
                return (to - from).LengthSquared() > 0 ? Vector2.Normalize(to - from) : Vector2.UnitX;
            }

            /// <summary>The walkable width (cells) across a way at its <paramref name="k"/>th cell, counted along the perpendicular.</summary>
            private float Across(List<int> path, Link link, int k)
            {
                var direction = this.Along(path, link, k);
                var normal = new Vector2(-direction.Y, direction.X);
                var centre = this.CropCell(path[link.Start + k]);
                var width = 1;
                foreach (var side in new[] { normal, -normal })
                {
                    for (var step = 1; step <= NarrowWidthCells + 1; step++)
                    {
                        var at = centre + (side * step);
                        var (x, y) = ((int)MathF.Round(at.X), (int)MathF.Round(at.Y));
                        if (x < 0 || y < 0 || x >= this.width || y >= this.height || this.walk[(y * this.width) + x] == 0)
                        {
                            break;
                        }

                        width++;
                    }
                }

                return width;
            }
        }
    }
}
