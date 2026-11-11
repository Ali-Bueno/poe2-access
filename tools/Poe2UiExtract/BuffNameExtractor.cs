using Index = LibBundle3.Index;

/// <summary>
///     `buff-names` command: the display name of every harmful buff, { language: { buffId: name } }, so one the mod has no
///     wording for is spoken by the game's own name. BuffDefinitions Id +0x0, Name +0x12 (dat-strings, 0.5.5e); harmful =
///     the row's BuffType byte (+0x67, the one the host reads: 4 is a flask) is a debuff or a curse (research 2026-11-08:
///     1 holds auras and boosts, 2 ailments and boss debuffs, 5 the curses, 7 and 9 shrines). Read-only on the game.
/// </summary>
internal static class BuffNameExtractor
{
    private const int IdOffset = 0x0;
    private const int DebuffType = 2;
    private const int CurseType = 5;

    public static int Run(Index index, string outFile)
    {
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        if (DatTable.Open(index, "buffdefinitions") is not { } english || BuffColumns.Find(english, "buff-names") is not { } columns)
        {
            Say.Out.WriteLine("  BuffDefinitions missing or its columns not found");
            return 1;
        }

        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? english : DatTable.OpenTranslated(index, "buffdefinitions", folder);
            if (table == null)
            {
                Say.Out.WriteLine($"  {code}: no BuffDefinitions");
                continue;
            }

            var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (var row = 0; row < table.RowCount; row++)
            {
                var id = table.StringOrNull(row, IdOffset);
                var name = table.StringOrNull(row, columns.Name)?.Trim();
                var type = table.Byte(row, columns.Type);
                // A name the game never shows ("[DNT-UNUSED] Blazing Lance Burning Ground") was said as it stood (user 2026-11-11).
                if (!string.IsNullOrEmpty(id) && ClassMechanics.IsShownName(name) && type is DebuffType or CurseType)
                {
                    names[id] = name;
                }
            }

            if (names.Count == 0)
            {
                Say.Out.WriteLine($"  {code}: the Name column moved (check with dat-strings)");
                return 1;
            }

            Say.Out.WriteLine($"  {code}: {names.Count} buff names");
            result[code] = names;
        }

        AreaExtractor.WriteJson(outFile, result);
        Say.Out.WriteLine($"Wrote {outFile}");
        return 0;
    }
}
