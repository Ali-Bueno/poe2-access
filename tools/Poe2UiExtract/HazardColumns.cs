using System;

/// <summary>
///     The ground-effect columns `hazards` reads: the buff key of GroundEffectTypes, found by content (a key into
///     BuffDefinitions; IgnitedGround gives ignited_ground). The Id (text at +0x0) is read in place.
/// </summary>
internal static class HazardColumns
{
    // GroundEffectTypes: the BuffDefinitions row a creature standing in the ground gets (a scan of the row's keys, 2026-10-28).
    internal const int TypeBuffOffset = 0x20;
    private const string KnownType = "IgnitedGround";
    private const string KnownBuff = "ignited_ground";

    /// <summary>The buff key column of the ground types, or -1 (said) when not found.</summary>
    public static int Find(DatTable types, DatTable buffs)
    {
        var search = new DatColumnSearch();
        var known = DatColumnSearch.RowOf(types, KnownType);
        var buff = search.Key(types, "GroundEffectTypes.Buff", TypeBuffOffset, buffs,
            known: (known, key => key >= 0 && key < buffs.RowCount && buffs.StringOrNull((int)key, 0) == KnownBuff));
        return search.Report("hazards") ? buff : -1;
    }
}
