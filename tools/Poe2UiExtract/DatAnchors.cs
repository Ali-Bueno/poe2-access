using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Rows of the game's tables whose values an extractor knows (read from the 0.5.x files on 2026-11-10 with dat-kinds),
///     so a column found by its type and shape is also the one that holds the known value on the known row. A row a patch
///     removed judges nothing and is said, as <see cref="DatColumnSearch"/> says it; a row that reads otherwise fails the
///     search. Pure.
/// </summary>
internal static class DatAnchors
{
    /// <summary>A campaign monster with files of its own, a skill of its own and no weapon.</summary>
    public const string Monster = "Metadata/Monsters/Urchins/MeleeUrchin1";
    public const string MonsterFolder = "Metadata/Monsters/Urchins/";

    /// <summary>A skill <see cref="Monster"/> is granted, and the Animation row it plays.</summary>
    public const string MonsterSkill = "UrchinSlingProjectile";
    public const string MonsterSkillAnimation = "Snipe";

    /// <summary>A monster whose main hand holds a bow (ItemClasses "Bow").</summary>
    public const string Archer = "Metadata/Monsters/Skeletons/PlayerSummoned/SkeletonArcherPlayerSummoned";
    public const string ArcherWeapon = "Bow";

    /// <summary>The row whose Id is <paramref name="id"/>; -1, and said, when the table no longer has it.</summary>
    public static int Row(DatTable table, string id, string tells)
    {
        var row = DatColumnSearch.RowOf(table, id);
        if (row < 0)
        {
            Say.Out.WriteLine($"  {tells}: its known row {id} is not in the table, so the column was judged by its type and shape alone");
        }

        return row;
    }

    /// <summary>The first row holding <paramref name="text"/> in a text column, or -1.</summary>
    public static int RowWith(DatTable table, int offset, string text)
    {
        using var probe = DatTable.Probe();
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.StringOrNull(row, offset) == text)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>The Id of a key's target row, or null for a null key or one past the table.</summary>
    public static string? IdOf(DatTable target, long row) => row >= 0 && row < target.RowCount ? target.StringOrNull((int)row, DatColumnSearch.IdColumn) : null;

    /// <summary>Whether a list column of file paths names, on the known monster's row, a file of that monster's folder.</summary>
    public static bool ListsOwnFile(DatTable monsters, int offset, int monster) =>
        monster < 0 || monsters.StringList(monster, offset).Any(path => path.StartsWith(MonsterFolder, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a key list grants the known monster its known skill.</summary>
    public static bool GrantsOwnSkill(DatTable monsters, int offset, int monster, int skill) =>
        monster < 0 || skill < 0 || monsters.KeyList(monster, offset).Contains(skill);

    /// <summary>
    ///     The lines of a finished catalog that known rows must have made: each a key the line starts with and the fragments
    ///     it holds. A key with no line is not judged (the caller says which rows the tables still have); one whose line lacks
    ///     a fragment is returned, for the step to fail with.
    /// </summary>
    public static IReadOnlyList<string> Unmet(IEnumerable<string> lines, char separator, IEnumerable<(string Key, string[] Holds)> known)
    {
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var at = line.IndexOf(separator);
            byKey.TryAdd(at < 0 ? line : line[..at], line);
        }

        return known.Where(check => byKey.TryGetValue(check.Key, out var line) && !check.Holds.All(part => line.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Select(check => $"{check.Key} no longer reads with {string.Join(", ", check.Holds)} (now: {byKey[check.Key]})").ToList();
    }
}
