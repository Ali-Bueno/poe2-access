using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `trial-data` command: what the Ascendancy trials show as icons or counters, from the game's tables, as
///     { "icons": { icon path: key }, language: { key: text } }. Keys: `effect:` a Sekhemas affliction or boon
///     (SanctumPersistentEffects), `mod:` a Trial of Chaos modifier (UltimatumModifiers), `room:` a Sekhemas room type
///     (SanctumRoomTypes), `ascendancy:` a class (Ascendancy); `key:about` holds the description; `label:` the HUD formats
///     (ClientStrings). Offsets measured with dat-strings (0.5.5f) and found again nearby by content. Read-only on the game.
/// </summary>
internal static class TrialDataExtractor
{
    private const string IconsEntry = "icons";
    private const string AboutSuffix = ":about";
    private const string ArtPrefix = "Art/";
    private const int IdColumn = 0x0;

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

    /// <summary>A table's rows to read: the key prefix, the measured columns, and which hold texts translated per language.</summary>
    private sealed record Source(string Table, string Prefix, int Name, int Icon, int[] About, int? SmallIcon = null);

    private static readonly Source[] Sources =
    {
        new("sanctumpersistenteffects", "effect:", 0x28, 0x30, new[] { 0x5D, 0x65 }, 0x55),
        new("ultimatummodifiers", "mod:", 0x49, 0x51, new[] { 0x7F }),
        new("sanctumroomtypes", "room:", -1, 0x2B, new[] { 0x34 }),
        new("ascendancy", "ascendancy:", 0x2C, -1, new[] { 0x34 }),
    };

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
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var icons = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var texts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var source in Sources)
            {
                var english = DatTable.Open(index, source.Table);
                var table = folder == null ? english : DatTable.OpenTranslated(index, source.Table, folder);
                if (english == null)
                {
                    Say.Out.WriteLine($"  no {source.Table}");
                    return 1;
                }

                if (table == null)
                {
                    Say.Out.WriteLine($"  {code}: no {source.Table}");
                    texts.Clear();
                    break;
                }

                ReadRows(english, table, source, texts, folder == null ? icons : null);
            }

            var strings = folder == null ? DatTable.Open(index, WorldTitleExtractor.StringsTable) : DatTable.OpenTranslated(index, WorldTitleExtractor.StringsTable, folder);
            if (strings != null)
            {
                var missing = ReadLabels(strings, texts);
                if (folder == null && missing.Count > 0)
                {
                    Say.Out.WriteLine($"  ClientStrings has no text for {string.Join(", ", missing)} (key +0x{WorldTitleExtractor.KeyColumn:X}, text +0x{WorldTitleExtractor.TextColumn:X}; check with dat-strings)");
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

    /// <summary>Ids and icons from the English table, the name and the description from the language's.</summary>
    private static void ReadRows(DatTable english, DatTable table, Source source, SortedDictionary<string, string> texts, SortedDictionary<string, string>? icons)
    {
        var name = Column(table, source.Name, Words);
        var about = source.About.Select(measured => Column(table, measured, Words)).OfType<int>().Distinct().ToList();
        var iconColumns = new[] { Column(english, source.Icon, Art), Column(english, source.SmallIcon ?? -1, Art) }.OfType<int>().Distinct().ToList();
        for (var row = 0; row < english.RowCount && row < table.RowCount; row++)
        {
            if (english.StringOrNull(row, IdColumn) is not { Length: > 0 } id)
            {
                continue;
            }

            var key = source.Prefix + id;
            if (name is { } nameColumn && TextAt(table, nameColumn, row) is { Length: > 0 } shown)
            {
                texts[key] = Link.Replace(shown, "$1");
            }

            var description = about.Select(column => TextAt(table, column, row)).FirstOrDefault(text => text.Length > 0);
            if (description != null)
            {
                texts[key + AboutSuffix] = Link.Replace(description, "$1");
            }

            foreach (var column in iconColumns)
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
    private static List<string> ReadLabels(DatTable strings, SortedDictionary<string, string> texts)
    {
        var wanted = Labels.ToDictionary(label => label.Key, label => label.Entry, StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 0; row < strings.RowCount; row++)
        {
            if (strings.StringOrNull(row, WorldTitleExtractor.KeyColumn) is { } key && wanted.TryGetValue(key, out var entry) &&
                strings.StringOrNull(row, WorldTitleExtractor.TextColumn)?.Trim() is { Length: > 0 } text)
            {
                texts[entry] = Link.Replace(text, "$1");
                found.Add(key);
            }
        }

        return wanted.Keys.Where(key => !found.Contains(key)).ToList();
    }

    /// <summary>The column fitting <paramref name="matches"/> nearest to where it was measured; null for none or no measure.</summary>
    private static int? Column(DatTable table, int measured, Func<string, bool> matches) =>
        measured < 0 ? null : DatContentColumns.Nearest(table, measured, offset => IsTextColumn(table, offset, matches));

    /// <summary>
    ///     A string in nearly every row (a translated table pools its strings, so a name that ends another string fails the
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
