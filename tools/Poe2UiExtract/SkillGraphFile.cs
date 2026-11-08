using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     A passive skill graph file (.psg, version 3): the main tree, the Atlas passive tree, the Atlas map's own layout
///     (metadata/atlasskillgraphs/endgamemaplayoutgraph.psg) and the league trees. Read from the bytes alone: a header with
///     the nodes per orbit, the root node ids, then groups, each a position and its nodes, each node its links
///     (docs/research/endgame.md). Node ids are PassiveSkills' graph ids. Pure.
/// </summary>
internal sealed class SkillGraphFile
{
    /// <summary>The only layout this reader knows; another version is refused, not guessed at.</summary>
    public const int KnownVersion = 3;

    private const int RootEntryBytes = 8;
    private const int LinkBytes = 8;

    private SkillGraphFile(IReadOnlyList<Node> nodes) => this.Nodes = nodes;

    public IReadOnlyList<Node> Nodes { get; }

    /// <summary>The graph in <paramref name="bytes"/>, or null when it is not a version-3 graph read to its last byte.</summary>
    public static SkillGraphFile? Parse(byte[] bytes)
    {
        try
        {
            var at = 0;
            if (bytes.Length < 3 || bytes[at++] != KnownVersion)
            {
                return null;
            }

            at++; // graph kind
            var orbits = bytes[at++];
            at += orbits; // nodes per orbit
            var roots = ReadInt(bytes, ref at);
            at += roots * RootEntryBytes;
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
                    var to = new List<int>(links);
                    for (var link = 0; link < links; link++)
                    {
                        to.Add(ReadInt(bytes, ref at));
                        at += LinkBytes - sizeof(int); // the link's curve
                    }

                    nodes.Add(new Node(id, group, x, y, orbit, slot, to));
                }
            }

            return at == bytes.Length ? new SkillGraphFile(nodes) : null;
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
