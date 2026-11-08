using System;
using System.Linq;

/// <summary>
///     The Animation columns that link actions (docs/research/dodge-timing.md), each found by what it holds near where it was
///     measured (<see cref="DatColumnSearch"/>): the fallback key, the keys before and after, and the list of others played
///     together, all into Animation itself. The Id (text at +0x0) is read in place.
/// </summary>
internal sealed class AnimationColumns
{
    // Measured offsets (community dat-schema, 0x6A-byte rows), where each search starts: +0x0A "Mainhand_AnimationKey", the
    // fallback (SpellProjectileFire → SpellProjectile); +0x2B / +0x34 the actions before and after (Charge: ChargeStart,
    // ChargeEnd); +0x3C a key list of others it plays.
    internal const int FallbackOffset = 0x0A;
    internal const int BeforeOffset = 0x2B;
    internal const int AfterOffset = 0x34;
    internal const int OthersOffset = 0x3C;

    private const string FallbackAction = "SpellProjectileFire";
    private const string FallbackTarget = "SpellProjectile";
    private const string ChainAction = "Charge";
    private const string ChainBefore = "ChargeStart";
    private const string ChainAfter = "ChargeEnd";

    public int Fallback, Before, After, Others;

    /// <summary>The columns, or null (said) when one was not found.</summary>
    public static AnimationColumns? Find(DatTable animations)
    {
        var search = new DatColumnSearch();
        Func<string, Func<long, bool>> Is = id => key => key >= 0 && key < animations.RowCount && animations.StringOrNull((int)key, 0) == id;
        var fallback = DatColumnSearch.RowOf(animations, FallbackAction);
        var chain = DatColumnSearch.RowOf(animations, ChainAction);
        var c = new AnimationColumns();
        c.Fallback = search.Key(animations, "Animation.Fallback", FallbackOffset, animations, known: (fallback, Is(FallbackTarget)));
        c.Before = search.Key(animations, "Animation.Before", BeforeOffset, animations, c.Fallback, (chain, Is(ChainBefore)));
        c.After = search.Key(animations, "Animation.After", AfterOffset, animations, c.Before, (chain, Is(ChainAfter)));
        c.Others = search.Find(animations, "Animation.Others", OthersOffset, offset => IsRowList(animations, offset), c.After);
        return search.Report("animation actions") ? c : null;
    }

    /// <summary>A list of 8-byte keys into the table itself: every item a row, some row with items.</summary>
    private static bool IsRowList(DatTable table, int offset)
    {
        var filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var keys = table.RowList(row, offset);
            if (keys.Any(key => key < 0 || key >= table.RowCount))
            {
                return false;
            }

            filled += keys.Count > 0 ? 1 : 0;
        }

        return filled > 0;
    }
}
