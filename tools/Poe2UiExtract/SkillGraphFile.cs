using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     A passive skill graph file (.psg, version 3): the main tree, the Atlas passive tree, the Atlas map's own layout
///     (metadata/atlasskillgraphs/endgamemaplayoutgraph.psg) and the league trees. Read from the bytes alone: a header with
///     the nodes per orbit, the root node ids, then groups, each a position and its nodes, each node its links
///     (docs/research/endgame.md). Node ids are PassiveSkills' graph ids. A file of another version is read with the same
///     layout and kept only when it validates (<see cref="IsConsistent"/>), never guessed at. Pure.
/// </summary>
internal sealed class SkillGraphFile
{
    /// <summary>The layout this reader was made on (0.5.x files); <see cref="VersionKnown"/> says whether a file is one.</summary>
    public const int KnownVersion = 3;

    private const int RootEntryBytes = 8;
    private const int LinkBytes = 8;

    private SkillGraphFile(int version, IReadOnlyList<Node> nodes, IReadOnlyList<int> orbitSizes, IReadOnlyList<int> roots) =>
        (this.Version, this.Nodes, this.OrbitSizes, this.Roots) = (version, nodes, orbitSizes, roots);

    public IReadOnlyList<Node> Nodes { get; }

    /// <summary>The header's nodes per orbit: a node's orbit is an index into it and its slot a place on that ring.</summary>
    public IReadOnlyList<int> OrbitSizes { get; }

    /// <summary>The header's root node ids (where a class's tree starts).</summary>
    public IReadOnlyList<int> Roots { get; }

    /// <summary>The file's version byte.</summary>
    public int Version { get; }

    public bool VersionKnown => this.Version == KnownVersion;

    /// <summary>
    ///     Whether the file reads as a graph whatever its version says: some nodes, every link's end a node of the file, every group position finite, every node on an orbit and a slot the header gives, every root a node.
    ///     (<see cref="Parse"/> has already checked that the counts consume the file to its last byte.) A known version is
    ///     trusted as read; an unknown one is accepted only when this holds (and its nodes resolve in PassiveSkills, which the
    ///     extractor checks).
    /// </summary>
    public bool IsConsistent
    {
        get
        {
            var ids = this.Nodes.Select(node => node.Id).ToHashSet();
            return this.Nodes.Count > 0 &&
                   this.Nodes.All(node => node.Links.All(ids.Contains) && float.IsFinite(node.X) && float.IsFinite(node.Y) &&
                                          node.Orbit < this.OrbitSizes.Count && node.Slot < this.OrbitSizes[node.Orbit]) &&
                   this.Roots.All(ids.Contains);
        }
    }

    /// <summary>The graph in <paramref name="bytes"/>, or null when the layout does not read it to its last byte.</summary>
    public static SkillGraphFile? Parse(byte[] bytes)
    {
        try
        {
            var at = 0;
            if (bytes.Length < 3)
            {
                return null;
            }

            var version = bytes[at++];

            at++; // graph kind
            var orbits = bytes[at++];
            if (at + orbits > bytes.Length)
            {
                return null;
            }

            var orbitSizes = bytes.AsSpan(at, orbits).ToArray().Select(size => (int)size).ToList();
            at += orbits; // nodes per orbit
            var rootCount = ReadInt(bytes, ref at);
            var roots = new List<int>();
            for (var root = 0; root < rootCount; root++)
            {
                roots.Add(ReadInt(bytes, ref at));
                at += RootEntryBytes - sizeof(int);
            }

            var groups = ReadInt(bytes, ref at);
            var nodes = new List<Node>();
            for (var group = 0; group < groups; group++)
            {
                var x = ReadFloat(bytes, ref at);
                var y = ReadFloat(bytes, ref at);
                at += sizeof(int) + sizeof(int) + 1; // flags, two unknowns
                var count = ReadInt(bytes, ref at);
                for (var i = 0; i < count; i++)
                {
                    var id = ReadInt(bytes, ref at);
                    var orbit = ReadInt(bytes, ref at);
                    var slot = ReadInt(bytes, ref at);
                    var links = ReadInt(bytes, ref at);
                    if (links > (bytes.Length - at) / LinkBytes)
                    {
                        return null; // more links than the file has bytes for
                    }

                    var to = new List<int>(links);
                    for (var link = 0; link < links; link++)
                    {
                        to.Add(ReadInt(bytes, ref at));
                        at += LinkBytes - sizeof(int); // the link's curve
                    }

                    nodes.Add(new Node(id, group, x, y, orbit, slot, to));
                }
            }

            return at == bytes.Length ? new SkillGraphFile(version, nodes, orbitSizes, roots) : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Each node's links in both directions (a file lists each link once, on one of its ends; an id listed twice is one node).</summary>
    public Dictionary<int, SortedSet<int>> Links()
    {
        var links = new Dictionary<int, SortedSet<int>>();
        foreach (var node in this.Nodes)
        {
            links.TryAdd(node.Id, new SortedSet<int>());
        }

        foreach (var node in this.Nodes)
        {
            foreach (var other in node.Links.Where(links.ContainsKey))
            {
                links[node.Id].Add(other);
                links[other].Add(node.Id);
            }
        }

        return links;
    }

    private static int ReadInt(byte[] bytes, ref int at)
    {
        if (at + sizeof(int) > bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        var value = BitConverter.ToInt32(bytes, at);
        at += sizeof(int);
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        return value;
    }

    private static float ReadFloat(byte[] bytes, ref int at)
    {
        if (at + sizeof(float) > bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        var value = BitConverter.ToSingle(bytes, at);
        at += sizeof(float);
        return value;
    }

    /// <param name="Orbit">The ring around its group's centre (0 = the centre itself).</param>
    /// <param name="Slot">Its place on that ring.</param>
    public sealed record Node(int Id, int Group, float X, float Y, int Orbit, int Slot, IReadOnlyList<int> Links);
}
