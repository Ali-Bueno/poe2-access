namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>The skeleton read as runs between places for <see cref="MapTopology.Build"/>: fork clusters labelled, runs traced from their ends, each end's place.</summary>
    public sealed partial class MapTopology
    {
        private sealed partial class Builder
        {
            /// <summary>A segment's end with no place: the end of a line (an end place is made there).</summary>
            private const int Free = 0;

            /// <summary>A segment's end with no place and no end: a closed loop with no fork on it (a place is made on it, the loop its way).</summary>
            private const int None = -1;

            /// <summary>A run of skeleton cells between places (path cells <see cref="Start"/> on), and the place at each end.</summary>
            private readonly record struct Segment(int Start, int Count, int A, int B);

            /// <summary>A fork's cells: their widest distance to a wall and their mean position.</summary>
            private readonly record struct Cluster(float Reach, Vector2 Centre);

            /// <summary>Labels each cluster of fork cells (three or more neighbours, not yet labelled) with -(first + index).</summary>
            private List<Cluster> LabelJunctions(int first)
            {
                var clusters = new List<Cluster>();
                foreach (var p in this.skeletonCells)
                {
                    if (this.labels[p] != 0 || this.Neighbours(p) < 3)
                    {
                        continue;
                    }

                    var label = -(first + clusters.Count);
                    this.labels[p] = label;
                    this.queue[0] = p;
                    var count = 1;
                    var reach = 0f;
                    var sum = Vector2.Zero;
                    for (var head = 0; head < count; head++)
                    {
                        var c = this.queue[head];
                        reach = Math.Max(reach, this.distance[c]);
                        sum += this.CropCell(c);
                        foreach (var offset in this.around)
                        {
                            var q = c + offset;
                            if (this.skeleton[q] != 0 && this.labels[q] == 0 && this.Neighbours(q) >= 3)
                            {
                                this.labels[q] = label;
                                this.queue[count++] = q;
                            }
                        }
                    }

                    clusters.Add(new Cluster(reach, (sum / count) + new Vector2(this.left + 0.5f, this.top + 0.5f)));
                }

                return clusters;
            }

            /// <summary>Traces the runs of unlabelled skeleton cells, from their ends first, then the closed loops.</summary>
            private List<Segment> TraceSegments(List<int> path)
            {
                path.Clear();
                var segments = new List<Segment>();
                foreach (var p in this.skeletonCells)
                {
                    if (this.labels[p] == 0 && this.RunNeighbours(p) <= 1)
                    {
                        this.Trace(p, segments, path);
                    }
                }

                foreach (var p in this.skeletonCells)
                {
                    if (this.labels[p] == 0)
                    {
                        this.Trace(p, segments, path);
                    }
                }

                return segments;
            }

            private int RunNeighbours(int p)
            {
                var count = 0;
                foreach (var offset in this.around)
                {
                    count += this.skeleton[p + offset] != 0 && this.labels[p + offset] >= 0 ? 1 : 0;
                }

                return count;
            }

            private void Trace(int first, List<Segment> segments, List<int> path)
            {
                var id = segments.Count + 1;
                var start = path.Count;
                for (var at = first; at >= 0;)
                {
                    this.labels[at] = id;
                    path.Add(at);
                    var next = -1;
                    foreach (var offset in this.around)
                    {
                        if (this.skeleton[at + offset] != 0 && this.labels[at + offset] == 0)
                        {
                            next = at + offset;
                            break;
                        }
                    }

                    at = next;
                }

                var count = path.Count - start;
                var (a, b) = count == 1 ? this.EndsOfDot(first) : (this.EndAt(first), this.EndAt(path[^1]));
                segments.Add(new Segment(start, count, a, b));
            }

            /// <summary>The place next to a segment's end cell, <see cref="Free"/> at a line's end, <see cref="None"/> inside a loop.</summary>
            private int EndAt(int p)
            {
                foreach (var offset in this.around)
                {
                    if (this.skeleton[p + offset] != 0 && this.labels[p + offset] < 0)
                    {
                        return -this.labels[p + offset];
                    }
                }

                return this.Neighbours(p) <= 1 ? Free : None;
            }

            /// <summary>The places at the two ends of a one-cell segment.</summary>
            private (int A, int B) EndsOfDot(int p)
            {
                var (a, b, touching) = (None, None, 0);
                foreach (var offset in this.around)
                {
                    if (this.skeleton[p + offset] == 0 || this.labels[p + offset] >= 0)
                    {
                        continue;
                    }

                    var place = -this.labels[p + offset];
                    touching++;
                    if (a == None)
                    {
                        a = place;
                    }
                    else if (b == None && place != a)
                    {
                        b = place;
                    }
                }

                return a == None ? (Free, Free) : b != None ? (a, b) : (a, touching >= 2 ? a : Free);
            }

            private void ClearLabels()
            {
                foreach (var p in this.skeletonCells)
                {
                    this.labels[p] = 0;
                }
            }

            private Vector2 CropCell(int p) => new(p % this.width, p / this.width);

            /// <summary>The grid cell (its centre) of a crop index.</summary>
            private Vector2 GridCell(int p) => new((p % this.width) + this.left + 0.5f, (p / this.width) + this.top + 0.5f);
        }
    }
}
