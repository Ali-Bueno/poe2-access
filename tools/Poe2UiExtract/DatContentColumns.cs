using Index = LibBundle3.Index;

/// <summary>
///     Flag and key columns of the `dat-columns` command, each found by what it holds rather than beside a name, so a
///     column added between them is followed too (docs/compatibility.md). Measured offsets are 0.5.5e's (2026-10-25).
/// </summary>
internal static class DatContentColumns
{
    /// <summary>A flag column is recognised by the rows whose Id carries its word: at least this many of them.</summary>
    private const int MinMarkedRows = 10;

    /// <summary>...and at least this share of the rows the flag is set on (0.5.5e: notable 37%, keystone 40%; others ≤ 1%).</summary>
    private const double MinMarkedShare = 0.25;

    /// <summary>A key column into a table referenced row by row (levels of an effect) reaches nearly all of its rows.</summary>
    private const double MostTargetRows = 0.9;

    /// <summary>The ActiveSkills key: target Ids found inside the effect's Id (0.5.5e: 1160 rows, the next column 41).</summary>
    private const int MinNamedKeys = 100;

    public static IEnumerable<(string Column, int? Offset, int Measured)> Find(Index index)
    {
        if (DatTable.Open(index, "passiveskills") is { } passives)
        {
            yield return ("PassiveSkills.GraphId", Nearest(passives, 0x30, offset => IsUniqueUInt16(passives, offset)), 0x30);
            if (DatTable.Open(index, "characters") is { } characters)
            {
                yield return ("PassiveSkills.Characters", Nearest(passives, 0x3A, offset => IsKeyListInto(passives, offset, characters.RowCount)), 0x3A);
            }

            yield return ("PassiveSkills.IsKeystone", FlagOfIds(passives, "keystone"), 0x4A);
            yield return ("PassiveSkills.IsNotable", FlagOfIds(passives, "notable"), 0x4B);
            yield return ("PassiveSkills.IsIconOnly", FlagOfIds(passives, "mastery"), 0x54);
            yield return ("PassiveSkills.IsJewelSocket", FlagOfIds(passives, "jewel"), 0x65);
        }

        var effects = DatTable.Open(index, "grantedeffects");
        if (effects != null && DatTable.Open(index, "grantedeffectsperlevel") is { } levels)
        {
            yield return ("GrantedEffectsPerLevel.GrantedEffect", FullestKey(levels, effects.RowCount), 0x0);
        }

        if (effects != null && DatTable.Open(index, "activeskills") is { } skills)
        {
            yield return ("GrantedEffects.ActiveSkill", NamedKey(effects, skills), 0x4F);
        }
    }

    /// <summary>The fitting offset nearest to where the column was measured.</summary>
    private static int? Nearest(DatTable table, int measured, Func<int, bool> fits) =>
        Enumerable.Range(0, table.RowSize).OrderBy(offset => Math.Abs(offset - measured)).Where(fits).Select(offset => (int?)offset).FirstOrDefault();

    private static bool IsUniqueUInt16(DatTable table, int offset)
    {
        var seen = new HashSet<int>();
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.UInt16(row, offset) is var value && (value <= 0 || !seen.Add(value)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A list of keys into a table, set on some rows (class start nodes list their classes).</summary>
    private static bool IsKeyListInto(DatTable table, int offset, int targetRows)
    {
        var filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var keys = table.KeyList(row, offset);
            if (keys.Any(key => key < 0 || key >= targetRows))
            {
                return false;
            }

            filled += keys.Count > 0 ? 1 : 0;
        }

        return filled > 0;
    }

    /// <summary>The flag column set on the most rows whose Id carries <paramref name="word"/>, those being a fair share of its rows.</summary>
    private static int? FlagOfIds(DatTable table, string word)
    {
        var marked = Enumerable.Range(0, table.RowCount).Where(row => table.String(row, 0).Contains(word, StringComparison.OrdinalIgnoreCase)).ToList();
        return Enumerable.Range(0, table.RowSize)
            .Where(table.IsFlagColumn)
            .Select(offset => (Offset: offset, Marked: marked.Count(row => table.Bool(row, offset)), Set: Enumerable.Range(0, table.RowCount).Count(row => table.Bool(row, offset))))
            .Where(column => column.Marked >= MinMarkedRows && column.Marked >= column.Set * MinMarkedShare)
            .OrderByDescending(column => column.Marked)
            .Select(column => (int?)column.Offset)
            .FirstOrDefault();
    }

    /// <summary>The key column that reaches the most rows of its target, nearly all of them.</summary>
    private static int? FullestKey(DatTable table, int targetRows) =>
        Enumerable.Range(0, table.RowSize)
            .Where(offset => table.IsKeyColumn(offset, targetRows))
            .Select(offset => (Offset: offset, Reached: Enumerable.Range(0, table.RowCount).Select(row => table.Key(row, offset)).Where(key => key >= 0).Distinct().Count()))
            .Where(column => column.Reached >= targetRows * MostTargetRows)
            .OrderByDescending(column => column.Reached)
            .Select(column => (int?)column.Offset)
            .FirstOrDefault();

    /// <summary>The key column into <paramref name="targets"/> whose rows' Ids (underscores dropped) most often appear in the row's own Id.</summary>
    private static int? NamedKey(DatTable table, DatTable targets)
    {
        static string Plain(string id) => id.Replace("_", string.Empty).ToLowerInvariant();
        var targetIds = Enumerable.Range(0, targets.RowCount).Select(row => Plain(targets.String(row, 0))).ToArray();
        var ids = Enumerable.Range(0, table.RowCount).Select(row => Plain(table.String(row, 0))).ToArray();
        var ranked = Enumerable.Range(0, table.RowSize)
            .Where(offset => table.IsKeyColumn(offset, targets.RowCount))
            .Select(offset => (Offset: offset, Named: Enumerable.Range(0, table.RowCount).Count(row => table.Key(row, offset) is var key and >= 0 && targetIds[key].Length > 0 && ids[row].Contains(targetIds[key]))))
            .OrderByDescending(column => column.Named)
            .ToList();
        return ranked.Count > 0 && ranked[0].Named >= MinNamedKeys && (ranked.Count == 1 || ranked[0].Named >= 2 * ranked[1].Named) ? ranked[0].Offset : null;
    }
}
