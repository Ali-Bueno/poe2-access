using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `trial-data` command: what the Ascendancy trials show as icons or counters, from the game's tables, as
///     { "icons": { icon path: key }, language: { key: text } }. Keys: `effect:` a Sekhemas affliction or boon
///     (SanctumPersistentEffects), `mod:` a Trial of Chaos modifier (UltimatumModifiers), `room:` a Sekhemas room type
///     (SanctumRoomTypes), `ascendancy:` a class (Ascendancy); `key:about` holds the description; `label:` the HUD formats
///     (ClientStrings). Each column is found in the English table near where it was measured (dat-strings, 0.5.5f), by its
///     content and by what a known row holds there, and read at the same offset in every language. Read-only on the game.
/// </summary>
internal static class TrialDataExtractor
{
    private const string IconsEntry = "icons";
    private const string AboutSuffix = ":about";
    private const string ArtPrefix = "Art/";
    private const int IdColumn = 0x0;
    private const string Step = "trial-data";

    /// <summary>Filled rows a text column needs: the room types and the classes are small tables.</summary>
    private const int MinTexts = 3;

    /// <summary>Rows that must decode as a string for a column to be text.</summary>
    private const double NearlyAllRows = 0.9;

    /// <summary>A read landing inside the string pool is cut here: the longest real text is the map window's (about 500).</summary>
    private const int LongestText = 1000;

    /// <summary>A row's id, "BoonMovementSpeed": one ASCII word with a capital inside, which no shown name has.</summary>
    private static readonly Regex IdLike = new(@"^[A-Z][a-z0-9]*[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

    /// <summary>A text column's strings are nearly all shown words, in any script: not an id, not a path.</summary>
    private static readonly Func<string, bool> Words = text => !IdLike.IsMatch(text) && !text.StartsWith(ArtPrefix, StringComparison.Ordinal);

    private static readonly Func<string, bool> Art = text => text.StartsWith(ArtPrefix, StringComparison.Ordinal);

    /// <summary>A linked word in the game's text, "[SacredWater|Sacred Water]" or "[Hit]", read as what is shown.</summary>
    private static readonly Regex Link = new(@"\[(?:[^|\]]*\|)?([^\]]*)\]", RegexOptions.Compiled);

    /// <summary>A column: where it was measured, and a row whose English value holds a known fragment there (0.5.x, 2026-11-10).</summary>
    private sealed record Column(int Measured, string KnownId, string Holds);

    /// <summary>A table's rows to read: the key prefix and the columns; the names and descriptions are translated per language.</summary>
    private sealed record Source(string Table, string Prefix, Column? Name, Column[] Icons, Column[] About);

    private static readonly Source[] Sources =
    {
        new("sanctumpersistenteffects", "effect:", new(0x28, "AfflictionMonsterMoreDamage", "Honed Claws"),
            new Column[] { new(0x30, "AfflictionMonsterMoreDamage", "CurseHonedClaws"), new(0x55, "AfflictionMonsterMoreDamage", "CurseHonedClaws") },
            new Column[] { new(0x5D, "BoonMonsterLessDamage", "Monsters deal"), new(0x65, "AfflictionMonsterMoreDamage", "Monsters deal") }),
        new("ultimatummodifiers", "mod:", new(0x49, "PlayerDebuffRecoveryReduction1", "Reduced Recovery"),
            new Column[] { new(0x51, "PlayerDebuffRecoveryReduction1", "Icons/ReducedRecovery") }, new Column[] { new(0x7F, "PlayerDebuffRecoveryReduction1", "Recovery") }),
        new("sanctumroomtypes", "room:", null, new Column[] { new(0x2B, "BronzeKey", "BronzeKeyIcon") }, new Column[] { new(0x34, "BronzeKey", "Bronze Key") }),
        new("ascendancy", "ascendancy:", new(0x2C, "Warrior1", "Titan"), Array.Empty<Column>(), new Column[] { new(0x34, "Warrior1", " ") }),
    };

    /// <summary>A source's columns as found in the English table.</summary>
    private sealed record Found(Source Source, DatTable English, int? Name, IReadOnlyList<int> Icons, IReadOnlyList<int> About);

    /// <summary>HUD formats to find on screen: "Round {0}/{1}", "{0} Death Crystals remaining", the modifiers legend's title.</summary>
    private static readonly (string Key, string Entry)[] Labels =
    {
        ("UltimatumWindowRound", "label:round"),
        ("SanctumHourglassesRemaining", "label:crystals"),
        ("UltimatumActiveModifierLegendsTitle", "label:modifiers"),
        ("SanctumMapWindowInfo", "label:map"),
    };

    public static int Run(Index index, string outFile)
    {
        var search = new DatColumnSearch();
        var found = new List<Found>();
        foreach (var source in Sources)
        {
            if (DatTable.Open(index, source.Table) is not { } english)
            {
                Say.Out.WriteLine($"  no {source.Table}");
                return 1;
            }

            found.Add(Find(english, source, search));
        }

        var englishStrings = DatTable.Open(index, ClientStringColumns.Table);
        var stringColumns = englishStrings == null ? null : ClientStringColumns.Find(englishStrings, search);
        if (!search.Report(Step))
        {
            return 1;
        }

        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var icons = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var texts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var source in found)
            {
                var table = folder == null ? source.English : DatTable.OpenTranslated(index, source.Source.Table, folder);
                if (table == null)
                {
                    Say.Out.WriteLine($"  {code}: no {source.Source.Table}");
                    texts.Clear();
                    break;
                }

                if (!ClientStringColumns.SameShape(source.English, table))
                {
                    Say.Out.WriteLine($"  {code}: {source.Source.Table} differs from the English table in rows or row size: its columns cannot be told, {Step} left out");
                    return 1;
                }

                ReadRows(source, table, texts, folder == null ? icons : null);
            }

            var strings = folder == null ? englishStrings : DatTable.OpenTranslated(index, ClientStringColumns.Table, folder);
            if (strings != null && englishStrings != null && stringColumns is { } columns && ClientStringColumns.SameShape(englishStrings, strings))
            {
                var missing = ReadLabels(ClientStringColumns.Read(strings, columns), texts);
                if (folder == null && missing.Count > 0)
                {
                    Say.Out.WriteLine($"  ClientStrings has no text for {string.Join(", ", missing)} (key +0x{columns.Key:X}, text +0x{columns.Text:X}; check with dat-strings)");
                    return 1;
                }
            }

            if (texts.Count == 0)
            {
                continue; // a language the game has no tables for (zh-CN)
            }

            Say.Out.WriteLine($"  {code}: {texts.Count} texts");
            result[code] = texts;
        }

        if (icons.Count == 0)
        {
            Say.Out.WriteLine("  no icon column found (check with dat-strings)");
            return 1;
        }

        result[IconsEntry] = icons;
        AreaExtractor.WriteJson(outFile, result);
        Say.Out.WriteLine($"Wrote {outFile}: {icons.Count} icons");
        return 0;
    }

    /// <summary>The source's columns in the English table; one not found is recorded in <paramref name="search"/>.</summary>
    private static Found Find(DatTable english, Source source, DatColumnSearch search)
    {
        int Place(Column column, string what, Func<string, bool> matches)
        {
            var known = DatAnchors.Row(english, column.KnownId, $"{source.Table}'s {what}");
            return search.Find(english, $"{source.Table}.{what}", column.Measured, offset => IsTextColumn(english, offset, matches) &&
                (known < 0 || (english.StringOrNull(known, offset) ?? string.Empty).Contains(column.Holds, StringComparison.OrdinalIgnoreCase)));
        }

        int? name = source.Name is { } named ? Place(named, "name", Words) : null;
        var about = source.About.Select(column => Place(column, "description", Words)).Distinct().ToList();
        var icons = source.Icons.Select(column => Place(column, "icon", Art)).Distinct().ToList();
        return new Found(source, english, name, icons, about);
    }

    /// <summary>Ids and icons from the English table, the name and the description from the language's.</summary>
    private static void ReadRows(Found found, DatTable table, SortedDictionary<string, string> texts, SortedDictionary<string, string>? icons)
    {
        var english = found.English;
        for (var row = 0; row < english.RowCount && row < table.RowCount; row++)
        {
            if (english.StringOrNull(row, IdColumn) is not { Length: > 0 } id)
            {
                continue;
            }

            var key = found.Source.Prefix + id;
            if (found.Name is { } nameColumn && TextAt(table, nameColumn, row) is { Length: > 0 } shown)
            {
                texts[key] = Link.Replace(shown, "$1");
            }

            var description = found.About.Select(column => TextAt(table, column, row)).FirstOrDefault(text => text.Length > 0);
            if (description != null)
            {
                texts[key + AboutSuffix] = Link.Replace(description, "$1");
            }

            foreach (var column in found.Icons)
            {
                // The tiers of a modifier share one icon: the first tier names it.
                if (icons != null && english.StringOrNull(row, column) is { Length: > 0 } icon)
                {
                    icons.TryAdd(icon.ToLowerInvariant(), key);
                }
            }
        }
    }

    /// <summary>Reads the HUD labels into <paramref name="texts"/>; the keys the table had no text for.</summary>
    private static List<string> ReadLabels(Dictionary<string, string> strings, SortedDictionary<string, string> texts)
    {
        var missing = new List<string>();
        foreach (var (key, entry) in Labels)
        {
            if (strings.TryGetValue(key, out var text))
            {
                texts[entry] = Link.Replace(text, "$1");
            }
            else
            {
                missing.Add(key);
            }
        }

        return missing;
    }

    /// <summary>
    ///     A string in nearly every row (a table pools its strings, so a name that ends another string fails the
    ///     start-of-string test), enough of them filled and every filled one matching: the tables here are small.
    /// </summary>
    private static bool IsTextColumn(DatTable table, int offset, Func<string, bool> matches)
    {
        var texts = Enumerable.Range(0, table.RowCount).Select(row => table.StringOrNull(row, offset)).ToList();
        var filled = texts.Where(text => !string.IsNullOrEmpty(text)).ToList();
        return texts.Count(text => text != null) >= table.RowCount * NearlyAllRows && filled.Count >= MinTexts && filled.All(text => matches(text!));
    }

    /// <summary>A chosen column's text in a row, read without the start-of-string test; empty when it is not a text.</summary>
    private static string TextAt(DatTable table, int offset, int row)
    {
        var text = table.String(row, offset).Trim();
        return text.Length <= LongestText ? text : string.Empty;
    }
}
