using System.Text;
using Index = LibBundle3.Index;

/// <summary>
///     `passive-overrides` command: the nodes a class sees in another node's place (ClassPassiveSkillOverrides, 0.5.5f: rows
///     of three keys, Characters +0x00, the PassiveSkills row replaced +0x10 and the one shown +0x20; 61 rows for the Witch,
///     the Huntress and the Druid). The tree's node record keeps the replaced row while the game shows the other one: a Druid
///     heard "Daño crítico de hechizos" over the tooltip of "Regeneración de vida" (2026-11-09). One line per override:
///     character Id, replaced Id and row, shown Id and row; first the PassiveSkills row size, so the plugin can step from a
///     row in memory to its override and check the Id it finds there. Read-only on the game.
/// </summary>
internal static class PassiveOverrideExtractor
{
    private const int CharacterOffset = 0x00;
    private const int ReplacedOffset = 0x10;
    private const int ShownOffset = 0x20;
    private const int IdOffset = 0x0;

    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, "classpassiveskilloverrides") is not { } overrides || DatTable.Open(index, "characters") is not { } characters ||
            DatTable.Open(index, "passiveskills") is not { } passives)
        {
            Say.Out.WriteLine("  ClassPassiveSkillOverrides, Characters or PassiveSkills missing");
            return 1;
        }

        // Only three classes have overrides: too few distinct keys for IsKeyColumn, so that column is checked by its range.
        if (overrides.RowSize < ShownOffset + 8 ||
            Enumerable.Range(0, overrides.RowCount).Any(row => overrides.Key(row, CharacterOffset) is var key && (key < 0 || key >= characters.RowCount)) ||
            !overrides.IsKeyColumn(ReplacedOffset, passives.RowCount) || !overrides.IsKeyColumn(ShownOffset, passives.RowCount))
        {
            Say.Out.WriteLine($"  ClassPassiveSkillOverrides' columns moved (row size 0x{overrides.RowSize:X}; check with dat-strings)");
            return 1;
        }

        var text = new StringBuilder();
        text.Append("# character id\treplaced passive id\trow\tshown passive id\trow (catalog of Poe2UiExtract passive-overrides)\n");
        text.Append($"row-size\t{passives.RowSize}\n");
        var count = 0;
        for (var row = 0; row < overrides.RowCount; row++)
        {
            var character = overrides.Key(row, CharacterOffset);
            var replaced = overrides.Key(row, ReplacedOffset);
            var shown = overrides.Key(row, ShownOffset);
            if (character < 0 || character >= characters.RowCount || replaced < 0 || replaced >= passives.RowCount || shown < 0 || shown >= passives.RowCount ||
                characters.StringOrNull((int)character, IdOffset) is not { Length: > 0 } characterId ||
                passives.StringOrNull((int)replaced, IdOffset) is not { Length: > 0 } replacedId ||
                passives.StringOrNull((int)shown, IdOffset) is not { Length: > 0 } shownId)
            {
                continue;
            }

            text.Append($"{characterId}\t{replacedId}\t{replaced}\t{shownId}\t{shown}\n");
            count++;
        }

        if (count == 0)
        {
            Say.Out.WriteLine("  no class overrides read (check ClassPassiveSkillOverrides with dat-strings)");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, text.ToString());
        Say.Out.WriteLine($"  {count} class overrides of passive nodes");
        Say.Out.WriteLine($"Wrote {outFile}");
        return 0;
    }
}
