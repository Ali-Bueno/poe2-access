using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The rules that tell PassiveSkills' graph id and class-list columns, and the flag columns no step knows, from the bytes
///     (docs/research/passive-tree.md#ready-for-a-rebuilt-tree-2026-11-14). They accept nearly all rows, so one odd row of a
///     rebuilt tree costs a log line, not the catalog. Pure.
/// </summary>
internal static class PassiveColumnRules
{
    /// <summary>The share of rows whose graph id must be nonzero and unique (0.5.5f: all of them; the audit's margin for one odd row).</summary>
    public const double MinUniqueShare = 0.99;

    /// <summary>The share of the Characters rows the start nodes' class lists must name, each once (0.5.5f: all twelve).</summary>
    public const double MinNamedClassShare = 0.5;

    /// <summary>A flag set on this many rows of the tree is worth a line in the log when no step knows it.</summary>
    public const int MinOtherFlagRows = 10;

    /// <summary>Example Ids shown for such a flag.</summary>
    public const int ExamplesShown = 3;

    /// <summary>The rows whose u16 at <paramref name="offset"/> is zero or repeats an earlier row's, with that value, up to <paramref name="stopAfter"/>.</summary>
    public static IReadOnlyList<(int Row, int Value)> GraphIdExceptions(DatTable table, int offset, int stopAfter = int.MaxValue)
    {
        var seen = new HashSet<int>();
        var odd = new List<(int, int)>();
        for (var row = 0; row < table.RowCount && odd.Count <= stopAfter; row++)
        {
            if (table.UInt16(row, offset) is var value && (value <= 0 || !seen.Add(value)))
            {
                odd.Add((row, value));
            }
        }

        return odd;
    }

    /// <summary>Whether the column is a graph id: nonzero and unique on at least <see cref="MinUniqueShare"/> of the rows.</summary>
    public static bool IsUniqueUInt16(DatTable table, int offset)
    {
        var allowed = (int)(table.RowCount * (1 - MinUniqueShare));
        return GraphIdExceptions(table, offset, allowed).Count <= allowed;
    }

    /// <summary>
    ///     Whether the column is a list of keys into a table that names at least <see cref="MinNamedClassShare"/> of its rows over
    ///     a few rows, none twice (0.5.5f: six start nodes list the twelve Characters rows, two each).
    /// </summary>
    public static bool IsKeyListInto(DatTable table, int offset, int targetRows)
    {
        var named = new HashSet<long>();
        var filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var keys = table.KeyList(row, offset);
            if (keys.Any(key => key < 0 || key >= targetRows || !named.Add(key)))
            {
                return false;
            }

            filled += keys.Count > 0 ? 1 : 0;
        }

        return filled > 0 && filled <= targetRows && named.Count >= targetRows * MinNamedClassShare;
    }

    /// <summary>The rows of the target table that no row's list at <paramref name="offset"/> names.</summary>
    public static IReadOnlyList<int> UnnamedTargets(DatTable table, int offset, int targetRows)
    {
        var named = Enumerable.Range(0, table.RowCount).SelectMany(row => table.KeyList(row, offset)).ToHashSet();
        return Enumerable.Range(0, targetRows).Where(target => !named.Contains(target)).ToList();
    }

    /// <summary>
    ///     The flag columns, none of them <paramref name="known"/>, set on at least <see cref="MinOtherFlagRows"/> of the given rows:
    ///     a possible new kind of node, with the first Ids it is set on.
    /// </summary>
    public static IReadOnlyList<(int Offset, int Rows, IReadOnlyList<string> Examples)> OtherFlags(DatTable table, IReadOnlyCollection<int> rows, IReadOnlySet<int> known)
    {
        using var probe = DatTable.Probe();
        var found = new List<(int, int, IReadOnlyList<string>)>();
        for (var offset = 0; offset < table.RowSize; offset++)
        {
            if (known.Contains(offset) || !table.IsFlagColumn(offset))
            {
                continue;
            }

            var set = rows.Where(row => table.Bool(row, offset)).ToList();
            if (set.Count >= MinOtherFlagRows)
            {
                found.Add((offset, set.Count, set.Take(ExamplesShown).Select(row => table.String(row, 0)).ToList()));
            }
        }

        return found;
    }
}
