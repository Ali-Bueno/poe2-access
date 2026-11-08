using System;

/// <summary>
///     The BuffDefinitions columns the buff extractors read (name, type byte, description), each found by what it holds near
///     where it was measured (<see cref="DatColumnSearch"/>), by one finder so `buff-names`, `class-mechanics` and `hazards`
///     agree. The Id (text at +0x0) is read in place. Translated tables share the English table's offsets.
/// </summary>
internal sealed class BuffColumns
{
    // Measured offsets (dat-strings / dat-rows 2026-11-08; the host reads the type), where each search starts.
    internal const int NameOffset = 0x12;
    internal const int TypeOffset = 0x67;
    internal const int DescriptionOffset = 0x08;

    // Rows the data names (types: 1 auras, 2 ailments, 4 flasks, 5 curses, 7 and 9 shrines; research 2026-11-08): the Frozen ailment (a debuff, type 2) and Ignited, whose description says it burns.
    private const string KnownBuff = "frozen";
    private const string KnownName = "Frozen";
    private const int KnownType = 2;
    private const string BurningBuff = "ignited";
    private const string BurningWord = "damage over time";

    public int Name, Type, Description;

    /// <summary>The name and type columns, or null (said) when one was not found.</summary>
    public static BuffColumns? Find(DatTable buffs, string step) => FindColumns(buffs, step, withDescription: false);

    /// <summary>The name, type and description columns, or null (said) when one was not found.</summary>
    public static BuffColumns? FindWithDescription(DatTable buffs, string step) => FindColumns(buffs, step, withDescription: true);

    private static BuffColumns? FindColumns(DatTable buffs, string step, bool withDescription)
    {
        var search = new DatColumnSearch();
        var known = DatColumnSearch.RowOf(buffs, KnownBuff);
        var c = new BuffColumns
        {
            Name = search.Text(buffs, "BuffDefinitions.Name", NameOffset, known: (known, text => text == KnownName)),
            Type = search.Byte(buffs, "BuffDefinitions.BuffType", TypeOffset, value => value >= 0, known: (known, value => value == KnownType)),
        };
        if (withDescription)
        {
            var burning = DatColumnSearch.RowOf(buffs, BurningBuff);
            c.Description = search.Text(buffs, "BuffDefinitions.Description", DescriptionOffset, known: (burning, text => text.Contains(BurningWord, StringComparison.OrdinalIgnoreCase)));
        }

        return search.Report(step) ? c : null;
    }
}
