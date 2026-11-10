using Index = LibBundle3.Index;

/// <summary>
///     Flag and key columns of the `dat-columns` command, each found by what it holds rather than beside a name, so a
///     column added between them is followed too (docs/compatibility.md). Measured offsets are 0.5.5e's (2026-10-25).
/// </summary>
internal static class DatContentColumns
{
    /// <summary>A flag column is recognised by the rows whose Id carries its word: at least this many of them.</summary>
    private const int MinMarkedRows = 10;

    /// <summary>The campaign has few towns: one an act.</summary>
    private const int MinTowns = 3;

    /// <summary>...and at least this share of the rows the flag is set on (0.5.5e: notable 37%, keystone 40%; others ≤ 1%).</summary>
    private const double MinMarkedShare = 0.25;

    /// <summary>A key column into a table referenced row by row (levels of an effect) reaches nearly all of its rows.</summary>
    private const double MostTargetRows = 0.9;

    /// <summary>The ActiveSkills key: target Ids found inside the effect's Id (0.5.5e: 1160 rows, the next column 41).</summary>
    private const int MinNamedKeys = 100;

    /// <summary>A class's Id is the player's metadata path, which the plugin compares with the player entity's.</summary>
    private const string CharacterFolder = "Metadata/Characters/";

    /// <summary>The columns <see cref="Find"/> yields, so one whose table is gone or renamed is missed rather than silently absent.</summary>
    internal static readonly string[] Names =
    {
        "PassiveSkills.GraphId", "PassiveSkills.Characters", "PassiveSkills.IsKeystone", "PassiveSkills.IsNotable", "PassiveSkills.IsIconOnly",
        "PassiveSkills.IsJewelSocket", "Characters.Id", "GrantedEffects.Id", "WorldAreas.Id", "WorldAreas.IsTown", "WorldAreas.AreaLevel",
        "MapPins.WorldArea", "MapPins.Name", "MapPins.Description", "MapPins.Content", "GrantedEffectsPerLevel.GrantedEffect",
        "GrantedEffects.ActiveSkill",
    };

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

        // The Ids the plugin reads (a class's metadata path, a skill's effect Id, an area's Id): by format at +0, checked here.
        if (DatTable.Open(index, "characters") is { } classes)
        {
            yield return ("Characters.Id", Nearest(classes, 0x0, offset => DatColumnSearch.IsIdColumn(classes, offset, id => id.StartsWith(CharacterFolder, StringComparison.Ordinal))), 0x0);
        }

        if (DatTable.Open(index, "grantedeffects") is { } granted)
        {
            yield return ("GrantedEffects.Id", Nearest(granted, 0x0, offset => DatColumnSearch.IsIdColumn(granted, offset)), 0x0);
        }

        if (DatTable.Open(index, "worldareas") is { } areas)
        {
            yield return ("WorldAreas.Id", Nearest(areas, 0x0, offset => DatColumnSearch.IsIdColumn(areas, offset)), 0x0);
            yield return ("WorldAreas.IsTown", FlagOfIds(areas, "town", MinTowns), 0x14);
            yield return ("WorldAreas.AreaLevel", Nearest(areas, 0x26, offset => IsAreaLevel(areas, offset)), 0x26);

            // A world map pin: its area, and the name and words its popup shows (MapPins, 2026-11-07).
            if (DatTable.Open(index, "mappins") is { } pins)
            {
                var area = Nearest(pins, 0x8, offset => pins.IsKeyColumn(offset, areas.RowCount));
                yield return ("MapPins.WorldArea", area, 0x8);
                var name = area is { } key ? Nearest(pins, 0x28, offset => IsAreaName(pins, offset, key, areas)) : null;
                yield return ("MapPins.Name", name, 0x28);
                yield return ("MapPins.Description", name is { } named ? Nearest(pins, 0x30, offset => offset > named && IsPinText(pins, offset)) : null, 0x30);
                yield return ("MapPins.Content", Nearest(pins, 0x7C, offset => IsContentIcon(pins, offset)), 0x7C);
            }
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
    internal static int? Nearest(DatTable table, int measured, Func<int, bool> fits)
    {
        using var probe = DatTable.Probe();
        return Enumerable.Range(0, table.RowSize).OrderBy(offset => Math.Abs(offset - measured)).Where(fits).Select(offset => (int?)offset).FirstOrDefault();
    }

    /// <summary>An area's level: every row 0 to 100, most of them above 1, many values (0.5.5e: +0x26, the Riverbank 1, the Trial's hub 22).</summary>
    private static bool IsAreaLevel(DatTable areas, int offset)
    {
        var values = Enumerable.Range(0, areas.RowCount).Select(row => areas.Int32(row, offset)).ToList();
        return values.All(value => value is >= 0 and <= 100) && values.Count(value => value > 1) * 2 > values.Count && values.Distinct().Count() >= 20;
    }

    /// <summary>The pin's name: for the pin named like its area ("G2_13"), the area's own name, on most of them.</summary>
    private static bool IsAreaName(DatTable pins, int offset, int areaKey, DatTable areas)
    {
        var (own, same) = (0, 0);
        for (var row = 0; row < pins.RowCount; row++)
        {
            if (pins.Key(row, areaKey) is var key and >= 0 && key < areas.RowCount && areas.String((int)key, 0) == pins.String(row, 0))
            {
                own++;
                same += pins.StringOrNull(row, offset) is { Length: > 0 } text && text == areas.String((int)key, 0x8) ? 1 : 0;
            }
        }

        return own >= MinMarkedRows && same * 2 > own;
    }

    /// <summary>The content icon of a pin that is something within its area: "WorldMapContentBoss" and the like on many rows.</summary>
    private static bool IsContentIcon(DatTable pins, int offset) =>
        Enumerable.Range(0, pins.RowCount).All(row => pins.StringOrNull(row, offset) != null) &&
        Enumerable.Range(0, pins.RowCount).Count(row => pins.String(row, offset).StartsWith("WorldMapContent", StringComparison.Ordinal)) >= MinMarkedRows;

    /// <summary>A text column: a readable string on every row, a sentence on many.</summary>
    private static bool IsPinText(DatTable pins, int offset) =>
        Enumerable.Range(0, pins.RowCount).All(row => pins.StringOrNull(row, offset) != null) &&
        Enumerable.Range(0, pins.RowCount).Count(row => pins.String(row, offset).Contains(' ', StringComparison.Ordinal)) >= MinMarkedRows;

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

    /// <summary>
    ///     A list of keys into a table that names each of its rows once over a few rows: every class has one start node
    ///     (0.5.5e: six start nodes list the twelve Characters rows, two each).
    /// </summary>
    private static bool IsKeyListInto(DatTable table, int offset, int targetRows)
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

        return filled > 0 && filled <= targetRows && named.Count == targetRows;
    }

    /// <summary>The flag column set on the most rows whose Id carries <paramref name="word"/>, those being a fair share of its rows.</summary>
    private static int? FlagOfIds(DatTable table, string word, int minRows = MinMarkedRows)
    {
        var marked = Enumerable.Range(0, table.RowCount).Where(row => table.String(row, 0).Contains(word, StringComparison.OrdinalIgnoreCase)).ToList();
        using var probe = DatTable.Probe();
        return Enumerable.Range(0, table.RowSize)
            .Where(table.IsFlagColumn)
            .Select(offset => (Offset: offset, Marked: marked.Count(row => table.Bool(row, offset)), Set: Enumerable.Range(0, table.RowCount).Count(row => table.Bool(row, offset))))
            .Where(column => column.Marked >= minRows && column.Marked >= column.Set * MinMarkedShare)
            .OrderByDescending(column => column.Marked)
            .Select(column => (int?)column.Offset)
            .FirstOrDefault();
    }

    /// <summary>The key column that reaches the most rows of its target, nearly all of them.</summary>
    private static int? FullestKey(DatTable table, int targetRows)
    {
        using var probe = DatTable.Probe();
        return Enumerable.Range(0, table.RowSize)
            .Where(offset => table.IsKeyColumn(offset, targetRows))
            .Select(offset => (Offset: offset, Reached: Enumerable.Range(0, table.RowCount).Select(row => table.Key(row, offset)).Where(key => key >= 0).Distinct().Count()))
            .Where(column => column.Reached >= targetRows * MostTargetRows)
            .OrderByDescending(column => column.Reached)
            .Select(column => (int?)column.Offset)
            .FirstOrDefault();
    }

    /// <summary>The key column into <paramref name="targets"/> whose rows' Ids (underscores dropped) most often appear in the row's own Id.</summary>
    private static int? NamedKey(DatTable table, DatTable targets)
    {
        static string Plain(string id) => id.Replace("_", string.Empty).ToLowerInvariant();
        var targetIds = Enumerable.Range(0, targets.RowCount).Select(row => Plain(targets.String(row, 0))).ToArray();
        var ids = Enumerable.Range(0, table.RowCount).Select(row => Plain(table.String(row, 0))).ToArray();
        using var probe = DatTable.Probe();
        var ranked = Enumerable.Range(0, table.RowSize)
            .Where(offset => table.IsKeyColumn(offset, targets.RowCount))
            .Select(offset => (Offset: offset, Named: Enumerable.Range(0, table.RowCount).Count(row => table.Key(row, offset) is var key and >= 0 && targetIds[key].Length > 0 && ids[row].Contains(targetIds[key]))))
            .OrderByDescending(column => column.Named)
            .ToList();
        return ranked.Count > 0 && ranked[0].Named >= MinNamedKeys && (ranked.Count == 1 || ranked[0].Named >= 2 * ranked[1].Named) ? ranked[0].Offset : null;
    }
}
