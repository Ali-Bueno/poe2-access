namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     The graph half of <see cref="MapTopology.Build"/>: rooms from the distance to walls, forks and ways from the skeleton,
    ///     short ways merged into the places and ways the topology keeps.
    /// </summary>
    public sealed partial class MapTopology
    {
        /// <summary>What the builder hands the topology; all arrays, positions in grid cells.</summary>
        private sealed class Draft
        {
            public float HalfWidth;
            public float RoomDistance;
            public int Left;
            public int Top;
            public int BlocksX;
            public int BlocksY;
            public ushort[] Owner = Array.Empty<ushort>();
            public TopologyNodeKind[] Kinds = Array.Empty<TopologyNodeKind>();
            public Vector2[] Centres = Array.Empty<Vector2>();
            public float[] Radii = Array.Empty<float>();
            public int[] EdgeA = Array.Empty<int>();
            public int[] EdgeB = Array.Empty<int>();
            public float[] Lengths = Array.Empty<float>();
            public float[] Widths = Array.Empty<float>();
            public Vector2[][] Paths = Array.Empty<Vector2[]>();
            public int[] ItemArea = Array.Empty<int>();
            public int[] ItemBox = Array.Empty<int>();
            public List<Narrow> Narrows = new();
        }

        private sealed partial class Builder
        {
            /// <summary>The rooms' core cells and their rooms: the ground is shared out from them too, so a room's corners are the room's.</summary>
            private readonly List<int> coreCells = new();
            private readonly List<int> coreRooms = new();

            /// <summary>A place before merging.</summary>
            private record struct Place(TopologyNodeKind Kind, Vector2 Centre, float Radius);

            /// <summary>A way before merging: its places, its cells in the path pool, length and mean width (cells).</summary>
            private readonly record struct Link(int A, int B, int Start, int Count, float Length, float Width);

            /// <summary>Rooms, forks and ways from the pruned skeleton, merged, then the ground shared out and the narrows found.</summary>
            private Draft Assemble()
            {
                this.cancel.ThrowIfCancellationRequested();
                var draft = new Draft { HalfWidth = this.MedianHalfWidth() };
                draft.RoomDistance = Math.Max(RoomMinDistanceCells, RoomWidthFactor * draft.HalfWidth);
                var places = this.FindRooms(draft.RoomDistance);
                var rooms = places.Count;
                foreach (var fork in this.LabelJunctions(rooms + 1))
                {
                    places.Add(new Place(TopologyNodeKind.Junction, fork.Centre, fork.Reach));
                }

                var path = new List<int>();
                var links = this.Touching(places.Count);
                var segments = this.TraceSegments(path);
                var segmentLink = new int[segments.Count];
                var segmentPlace = new int[segments.Count];
                for (var s = 0; s < segments.Count; s++)
                {
                    (segmentLink[s], segmentPlace[s]) = (-1, -1);
                    var segment = segments[s];
                    if (segment.A == None || segment.B == None)
                    {
                        var ring = places.Count;
                        places.Add(new Place(TopologyNodeKind.Junction, this.GridCell(path[segment.Start]), this.distance[path[segment.Start]]));
                        segmentLink[s] = links.Count;
                        links.Add(this.LinkOf(ring, ring, segment, path));
                        continue;
                    }

                    var a = segment.A == Free ? this.EndPlace(places, path[segment.Start]) : segment.A - 1;
                    if (segment.Count == 1 && segment.A == Free && segment.B == Free)
                    {
                        segmentPlace[s] = a;
                        continue;
                    }

                    var b = segment.B == Free ? this.EndPlace(places, path[segment.Start + segment.Count - 1]) : segment.B - 1;
                    segmentLink[s] = links.Count;
                    links.Add(this.LinkOf(a, b, segment, path));
                }

                this.cancel.ThrowIfCancellationRequested();
                var root = Merge(places, links);
                this.Emit(draft, places, links, root, path, out var linkEdge, out var placeNode);
                this.ShareGround(draft, links, segmentLink, segmentPlace, linkEdge, placeNode);
                this.FindNarrows(draft, links, linkEdge, path);
                return draft;
            }

            /// <summary>The median of the ways' mean half-widths, each way counted once however long (rooms would weigh in by their length).</summary>
            private float MedianHalfWidth()
            {
                var path = new List<int>();
                this.LabelJunctions(1);
                var segments = this.TraceSegments(path);
                this.ClearLabels();
                if (segments.Count == 0)
                {
                    return 0;
                }

                var widths = new float[segments.Count];
                for (var s = 0; s < widths.Length; s++)
                {
                    for (var i = 0; i < segments[s].Count; i++)
                    {
                        widths[s] += this.distance[path[segments[s].Start + i]] / segments[s].Count;
                    }
                }

                Array.Sort(widths);
                return widths[widths.Length / 2];
            }

            /// <summary>The rooms: cores of ground at least <paramref name="reach"/> from walls, large and wide enough; their skeleton cells labelled.</summary>
            private List<Place> FindRooms(float reach)
            {
                var scale = reach / RoomMinDistanceCells;
                var minCore = RoomMinCoreCells * scale * scale;
                var minPeak = RoomMinPeakCells * scale;
                var places = new List<Place>();
                for (var start = 0; start < this.distance.Length; start++)
                {
                    if (this.distance[start] < reach || this.labels[start] != 0)
                    {
                        continue;
                    }

                    this.labels[start] = -1;
                    this.queue[0] = start;
                    var count = 1;
                    var (peak, sum) = (0f, Vector2.Zero);
                    for (var head = 0; head < count; head++)
                    {
                        var p = this.queue[head];
                        (peak, sum) = (Math.Max(peak, this.distance[p]), sum + this.CropCell(p));
                        foreach (var offset in this.around)
                        {
                            var q = p + offset;
                            if (this.distance[q] >= reach && this.labels[q] == 0)
                            {
                                this.labels[q] = -1;
                                this.queue[count++] = q;
                            }
                        }
                    }

                    if (count < minCore || peak < minPeak)
                    {
                        continue;
                    }

                    var mean = sum / count;
                    var centre = this.queue[0];
                    for (var i = 0; i < count; i++)
                    {
                        this.labels[this.queue[i]] = places.Count + 1;
                        this.coreCells.Add(this.queue[i]);
                        this.coreRooms.Add(places.Count);
                        if (Vector2.DistanceSquared(this.CropCell(this.queue[i]), mean) < Vector2.DistanceSquared(this.CropCell(centre), mean))
                        {
                            centre = this.queue[i];
                        }
                    }

                    places.Add(new Place(TopologyNodeKind.Room, this.GridCell(centre), MathF.Sqrt(count / MathF.PI) + reach));
                }

                var roomOf = new int[this.skeletonCells.Count];
                for (var i = 0; i < roomOf.Length; i++)
                {
                    var p = this.skeletonCells[i];
                    roomOf[i] = Math.Max(0, this.labels[p]);
                    foreach (var offset in this.around)
                    {
                        roomOf[i] = Math.Max(roomOf[i], this.labels[p + offset]);
                    }
                }

                Array.Clear(this.labels);
                for (var i = 0; i < roomOf.Length; i++)
                {
                    this.labels[this.skeletonCells[i]] = -roomOf[i];
                }

                return places;
            }

            /// <summary>Ways of no length between places whose cells touch (a fork beside a room), so merging joins them.</summary>
            private List<Link> Touching(int places)
            {
                var pairs = new HashSet<long>();
                var links = new List<Link>();
                foreach (var p in this.skeletonCells)
                {
                    if (this.labels[p] >= 0)
                    {
                        continue;
                    }

                    foreach (var offset in this.around)
                    {
                        var other = this.labels[p + offset];
                        if (this.skeleton[p + offset] != 0 && other < this.labels[p] && pairs.Add(((long)-this.labels[p] * (places + 1)) - other))
                        {
                            links.Add(new Link(-this.labels[p] - 1, -other - 1, 0, 0, 0, 0));
                        }
                    }
                }

                return links;
            }

            private int EndPlace(List<Place> places, int p)
            {
                places.Add(new Place(TopologyNodeKind.End, this.GridCell(p), this.distance[p]));
                return places.Count - 1;
            }

            private Link LinkOf(int a, int b, Segment segment, List<int> path)
            {
                var (length, width) = (0f, 0f);
                for (var i = 0; i < segment.Count; i++)
                {
                    var p = path[segment.Start + i];
                    width += this.distance[p];
                    if (i > 0)
                    {
                        length += Vector2.Distance(this.CropCell(p), this.CropCell(path[segment.Start + i - 1]));
                    }
                }

                return new Link(a, b, segment.Start, segment.Count, Math.Max(length, 1), 2 * width / segment.Count);
            }

            /// <summary>Joins places linked by ways shorter than their width plus <see cref="MergeFloorCells"/>, shortest first; two rooms stay apart.</summary>
            private static int[] Merge(List<Place> places, List<Link> links)
            {
                var root = new int[places.Count];
                for (var v = 0; v < root.Length; v++)
                {
                    root[v] = v;
                }

                int Find(int v)
                {
                    while (root[v] != v)
                    {
                        v = root[v] = root[root[v]];
                    }

                    return v;
                }

                var order = new int[links.Count];
                for (var i = 0; i < order.Length; i++)
                {
                    order[i] = i;
                }

                Array.Sort(order, (x, y) => links[x].Length.CompareTo(links[y].Length));
                foreach (var i in order)
                {
                    var link = links[i];
                    if (places[link.A].Kind == TopologyNodeKind.End || places[link.B].Kind == TopologyNodeKind.End || !IsShort(link))
                    {
                        continue;
                    }

                    var (a, b) = (Find(link.A), Find(link.B));
                    if (a == b || (places[a].Kind == TopologyNodeKind.Room && places[b].Kind == TopologyNodeKind.Room))
                    {
                        continue;
                    }

                    if (places[b].Kind == TopologyNodeKind.Room)
                    {
                        (a, b) = (b, a);
                    }

                    root[b] = a;
                }

                for (var v = 0; v < root.Length; v++)
                {
                    root[v] = Find(v);
                }

                return root;
            }

            private static bool IsShort(Link link) => link.Length < link.Width + MergeFloorCells;

            /// <summary>The final places (a merged fork centred on its members, its radius covering them) and ways (those still between places, or long loops).</summary>
            private void Emit(Draft draft, List<Place> places, List<Link> links, int[] root, List<int> path, out int[] linkEdge, out int[] placeNode)
            {
                placeNode = new int[places.Count];
                var nodes = new List<int>();
                for (var v = 0; v < places.Count; v++)
                {
                    if (root[v] == v)
                    {
                        placeNode[v] = nodes.Count;
                        nodes.Add(v);
                    }
                }

                for (var v = 0; v < places.Count; v++)
                {
                    placeNode[v] = placeNode[root[v]];
                }

                var sum = new Vector2[nodes.Count];
                var members = new int[nodes.Count];
                for (var v = 0; v < places.Count; v++)
                {
                    sum[placeNode[v]] += places[v].Centre;
                    members[placeNode[v]]++;
                }

                draft.Kinds = new TopologyNodeKind[nodes.Count];
                draft.Centres = new Vector2[nodes.Count];
                draft.Radii = new float[nodes.Count];
                for (var n = 0; n < nodes.Count; n++)
                {
                    var head = places[nodes[n]];
                    draft.Kinds[n] = head.Kind;
                    draft.Centres[n] = head.Kind == TopologyNodeKind.Room ? head.Centre : sum[n] / members[n];
                }

                for (var v = 0; v < places.Count; v++)
                {
                    var n = placeNode[v];
                    var reach = draft.Kinds[n] == TopologyNodeKind.Room && places[v].Kind != TopologyNodeKind.Room ? 0 : Vector2.Distance(places[v].Centre, draft.Centres[n]) + places[v].Radius;
                    draft.Radii[n] = Math.Max(draft.Radii[n], reach);
                }

                linkEdge = new int[links.Count];
                var kept = new List<int>();
                for (var i = 0; i < links.Count; i++)
                {
                    var link = links[i];
                    var loop = placeNode[link.A] == placeNode[link.B];
                    linkEdge[i] = loop && (link.Count == 0 || IsShort(link)) ? -1 : kept.Count;
                    if (linkEdge[i] >= 0)
                    {
                        kept.Add(i);
                    }
                }

                draft.EdgeA = new int[kept.Count];
                draft.EdgeB = new int[kept.Count];
                draft.Lengths = new float[kept.Count];
                draft.Widths = new float[kept.Count];
                draft.Paths = new Vector2[kept.Count][];
                for (var e = 0; e < kept.Count; e++)
                {
                    var link = links[kept[e]];
                    (draft.EdgeA[e], draft.EdgeB[e], draft.Lengths[e], draft.Widths[e]) = (placeNode[link.A], placeNode[link.B], link.Length, link.Width);
                    draft.Paths[e] = new Vector2[link.Count];
                    for (var i = 0; i < link.Count; i++)
                    {
                        draft.Paths[e][i] = this.GridCell(path[link.Start + i]);
                    }
                }
            }
        }
    }
}
