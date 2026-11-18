using System.Globalization;
using Index = LibBundle3.Index;

/// <summary>
///     The `passive-tree` step's "jewelSlots": { socket's graph id: PassiveJewelSlots row }. A jewel socketed in the tree lies in
///     the server inventory PassiveJewels1 at x = that row (docs/research/passive-tree.md#socketed-jewels). Each row's first
///     column keys the socket's PassiveSkills row; it is found by the row measured live and every row is checked to name a
///     jewel socket of the tree. A table of another shape gives none: the tree is written without them.
/// </summary>
internal static class PassiveJewelSlots
{
    private const string Step = "passive-tree";
    private const string Table = "passivejewelslots";

    /// <summary>Where the row's PassiveSkills key was measured (0.5.5f).</summary>
    private const int PassiveColumn = 0x0;

    /// <summary>Measured live 2026-11-18: an Emerald at x 5 of PassiveJewels1 sat in jewel_slot1961 (graph id 61834).</summary>
    private const int KnownRow = 5;

    private const string KnownSocket = "jewel_slot1961";
    private const char JewelSocketFlag = 'j';
    private const char FlagSeparator = '|';

    /// <summary>The slots of <paramref name="nodes"/> (the step's "graph id: Id|flags"), by graph id; empty, said, when the column is not found.</summary>
    public static SortedDictionary<string, int> Read(Index index, DatTable passives, int graphIdColumn, IReadOnlyDictionary<string, string> nodes)
    {
        var slots = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (DatTable.Open(index, Table) is not { } table)
        {
            Say.Out.WriteLine($"  {Step}: PassiveJewelSlots not found: no jewel slots written");
            return slots;
        }

        var search = new DatColumnSearch();
        var column = search.Key(table, "PassiveJewelSlots.Passive", PassiveColumn, passives,
            known: (KnownRow < table.RowCount ? KnownRow : -1, key => DatAnchors.IdOf(passives, key) == KnownSocket));
        if (!search.AllFound)
        {
            Say.Out.WriteLine($"  {Step}: no PassiveJewelSlots column keys PassiveSkills with row {KnownRow} on {KnownSocket}: no jewel slots written");
            return slots;
        }

        search.Report(Step);
        var others = new List<string>();
        var unflagged = new List<string>();
        for (var row = 0; row < table.RowCount; row++)
        {
            var key = table.Key(row, column);
            var id = DatAnchors.IdOf(passives, key);
            var graphId = id == null ? null : passives.UInt16((int)key, graphIdColumn).ToString(CultureInfo.InvariantCulture);
            if (graphId == null || !nodes.TryGetValue(graphId, out var node))
            {
                others.Add($"row {row} {id ?? "no row"}");
                continue;
            }

            if (!slots.TryAdd(graphId, row))
            {
                others.Add($"row {row} {id} (again)");
                continue;
            }

            if (!IsSocket(node))
            {
                unflagged.Add($"row {row} {id}");
            }
        }

        var sockets = nodes.Count(node => IsSocket(node.Value));
        var unslotted = nodes.Where(node => IsSocket(node.Value) && !slots.ContainsKey(node.Key)).Select(node => node.Value[..node.Value.IndexOf(FlagSeparator)]).ToList();
        Say.Out.WriteLine($"  {Step}: {slots.Count} of {table.RowCount} PassiveJewelSlots rows name a node of the tree ({slots.Count - unflagged.Count} flagged a jewel socket); " +
                          $"{sockets - unslotted.Count} of the tree's {sockets} jewel sockets have a row");
        if (unflagged.Count > 0)
        {
            Say.Out.WriteLine($"  {Step}: jewel slots on a node not flagged a socket: {string.Join(", ", unflagged)}");
        }

        if (others.Count > 0)
        {
            Say.Out.WriteLine($"  {Step}: jewel slots left out, on no node of the tree: {string.Join(", ", others)}");
        }

        if (unslotted.Count > 0)
        {
            Say.Out.WriteLine($"  {Step}: jewel sockets of the tree with no slot: {string.Join(", ", unslotted)}");
        }

        return slots;
    }

    private static bool IsSocket(string node) => node[(node.IndexOf(FlagSeparator) + 1)..].Contains(JewelSocketFlag);
}
