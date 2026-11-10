using System;
using System.Linq;

/// <summary>
///     The three key columns of ClassPassiveSkillOverrides (a class, the node it sees replaced, the node shown instead), each
///     found by what it holds near where it was measured (<see cref="DatColumnSearch"/>) and checked against a known override
///     of the table. Pure.
/// </summary>
internal sealed class PassiveOverrideColumns
{
    // Measured 0.5.5f (a row is three 16-byte keys), where each search starts.
    internal const int CharacterOffset = 0x00;
    internal const int ReplacedOffset = 0x10;
    internal const int ShownOffset = 0x20;

    private const string CharacterFolder = "Metadata/Characters/";

    // An override of 0.5.5f the data names: the Witch sees "spells18" as "spell_and_minion_damage1".
    internal const string KnownCharacter = "Metadata/Characters/Int/IntFour";
    internal const string KnownReplaced = "spells18";
    internal const string KnownShown = "spell_and_minion_damage1";

    public int Character, Replaced, Shown;

    /// <summary>The columns, or null (said) when one was not found or the known override reads differently.</summary>
    public static PassiveOverrideColumns? Find(DatTable overrides, DatTable characters, DatTable passives)
    {
        var search = new DatColumnSearch();
        var c = new PassiveOverrideColumns();
        c.Character = search.Find(overrides, "ClassPassiveSkillOverrides.Character", CharacterOffset, offset => IsCharacter(overrides, offset, characters));
        c.Replaced = search.Find(overrides, "ClassPassiveSkillOverrides.Replaced", ReplacedOffset, offset => IsPassive(overrides, offset, passives), c.Character);
        c.Shown = search.Find(overrides, "ClassPassiveSkillOverrides.Shown", ShownOffset, offset => IsPassive(overrides, offset, passives) && Differs(overrides, c.Replaced, offset), c.Replaced);
        if (search.AllFound)
        {
            CheckKnownOverride(overrides, characters, passives, c, search);
        }

        return search.Report("passive-overrides") ? c : null;
    }

    /// <summary>Every row names a class: a key into Characters whose Id is a player's metadata path.</summary>
    private static bool IsCharacter(DatTable overrides, int offset, DatTable characters) =>
        offset + sizeof(long) <= overrides.RowSize && Enumerable.Range(0, overrides.RowCount).All(row =>
            overrides.Key(row, offset) is >= 0 and var key && key < characters.RowCount &&
            characters.StringOrNull((int)key, 0) is { } id && id.StartsWith(CharacterFolder, StringComparison.Ordinal));

    /// <summary>Only three classes have overrides: too few keys for a count of distinct ones to tell, but a node is replaced once per class.</summary>
    private static bool IsPassive(DatTable overrides, int offset, DatTable passives) =>
        overrides.IsKeyColumn(offset, passives.RowCount) && Enumerable.Range(0, overrides.RowCount).All(row =>
            overrides.Key(row, offset) is >= 0 and var key && passives.StringOrNull((int)key, 0) is { Length: > 0 });

    private static bool Differs(DatTable overrides, int replaced, int shown) =>
        Enumerable.Range(0, overrides.RowCount).All(row => overrides.Key(row, replaced) != overrides.Key(row, shown));

    /// <summary>
    ///     The Witch's override of "spells18" must read as it did: found in the table it is checked, absent it is said and
    ///     the columns stand on their shape alone; present with another node shown, the columns are wrong.
    /// </summary>
    private static void CheckKnownOverride(DatTable overrides, DatTable characters, DatTable passives, PassiveOverrideColumns c, DatColumnSearch search)
    {
        string IdOf(DatTable table, long key) => key >= 0 && key < table.RowCount ? table.StringOrNull((int)key, 0) ?? string.Empty : string.Empty;
        // Looked for by the node alone: looked for by the class too, a class column read from the wrong place found no row,
        // and that was said as "not in the table" with every override filed under another class.
        var rows = Enumerable.Range(0, overrides.RowCount).Where(row => IdOf(passives, overrides.Key(row, c.Replaced)) == KnownReplaced).ToList();
        if (rows.Count == 0)
        {
            search.Skipped("ClassPassiveSkillOverrides", $"the known override ({KnownReplaced} for the Witch) is not in the table, so the columns were judged by their type and shape alone");
        }
        else if (!rows.Any(row => IdOf(characters, overrides.Key(row, c.Character)) == KnownCharacter && IdOf(passives, overrides.Key(row, c.Shown)) == KnownShown))
        {
            search.Fail($"ClassPassiveSkillOverrides ({KnownReplaced} is no longer replaced by {KnownShown} for the Witch: the columns moved)");
        }
    }
}
