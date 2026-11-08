using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
///     `world-titles` command: the names objects show only in a WorldDescription, { language: { .ot path without its extension:
///     title } }. The game gives such an object no render name: the real Relic Altar, the Trial's Death Crystals. A .ot's
///     title_text is a ClientStrings key: +0x00 Id, +0x08 Text (dat-strings, 0.5.5f). Also "objective:" + path: the tracker line
///     without which an object means nothing, QuestStates +0x2C (row 655 for the crystals). Read-only on the game.
/// </summary>
internal static class WorldTitleExtractor
{
    internal const int KeyColumn = 0x0;
    internal const int TextColumn = 0x8;
    private const string ObjectExtension = ".ot";
    internal const string StringsTable = "clientstrings";
    private const string ObjectivesTable = "queststates";
    private const int ObjectiveColumn = 0x2C;
    private const string ObjectivePrefix = "objective:";

    /// <summary>"[Honour|Honor]: &lt;white&gt;{{0}/{1}}": what the Trial's HUD line starts with, a key of its own in the table.</summary>
    private const string HonourKey = "SanctumResolveValue";
    private const string HonourEntry = "label:honour";

    /// <summary>The last line of an unallocated passive's tooltip.</summary>
    private const string UnallocatedKey = "PassiveNodeUnallocated";
    private const string UnallocatedEntry = "label:unallocated";

    /// <summary>"Sacred Water: {0}": the Trial's currency, up to its colon.</summary>
    private const string WaterKey = "ItemDisplaySanctumGold";
    private const string WaterEntry = "label:water";

    /// <summary>A world map pin's popup: "Monster Level: {0}" and "Town".</summary>
    private const string PinLevelKey = "WorldPanelPinPopupMonsterLevel";
    private const string PinLevelEntry = "label:pin-level";
    private const string PinTownKey = "WorldPanelPinPopupTown";
    private const string PinTownEntry = "label:pin-town";

    private static readonly Regex TitleKey = new("title_text\\s*=\\s*\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>A colour around a title: "&lt;white&gt;{Torre petrolífera}".</summary>
    private static readonly Regex Colour = new(@"^<\w+>\{(.*)\}$", RegexOptions.Compiled);

    /// <summary>A linked word, "[Honour|Honor]" or "[Honour]", shown as its last half, up to the colon of the line.</summary>
    private static readonly Regex LinkedLabel = new(@"^\[(?:[^|\]]*\|)?([^\]]*)\]", RegexOptions.Compiled);

    /// <summary>Death Crystals stand in rooms of other kinds too, still targetable: only this objective makes them real.</summary>
    private static readonly (string Path, string Objective)[] Objectives =
    {
        ("metadata/monsters/marakethsanctumtrial/hazards/hourglasslethal", "Disable the Crystals before time runs out"),
    };

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in index.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(ObjectExtension, StringComparison.Ordinal)))
        {
            var title = TitleKey.Match(Encoding.Unicode.GetString(file.Read().ToArray()));
            if (title.Success)
            {
                keys[file.Path![..^ObjectExtension.Length]] = title.Groups[1].Value;
            }
        }

        var englishObjectives = DatTable.Open(index, ObjectivesTable);
        var objectiveRows = Objectives.Select(wanted => (wanted.Path, Row: Enumerable.Range(0, englishObjectives?.RowCount ?? 0)
            .FirstOrDefault(row => englishObjectives!.StringOrNull(row, ObjectiveColumn) == wanted.Objective, -1))).ToList();
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var languages = AreaExtractor.LanguageCodes.Select(pair => (Code: pair.Value, Folder: (string?)pair.Key)).Prepend(("en", null));
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? DatTable.Open(index, StringsTable) : DatTable.OpenTranslated(index, StringsTable, folder);
            if (table == null)
            {
                Console.WriteLine($"  {code}: no ClientStrings");
                continue;
            }

            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var row = 0; row < table.RowCount; row++)
            {
                if (table.StringOrNull(row, KeyColumn) is { Length: > 0 } key && table.StringOrNull(row, TextColumn)?.Trim() is { Length: > 0 } text)
                {
                    texts[key] = text;
                }
            }

            var titles = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (path, key) in keys)
            {
                if (texts.TryGetValue(key, out var text))
                {
                    titles[path] = Colour.Replace(text, "$1");
                }
            }

            if (texts.TryGetValue(HonourKey, out var honour) && LinkedLabel.Match(honour) is { Success: true } label)
            {
                titles[HonourEntry] = label.Groups[1].Value;
            }

            if (texts.TryGetValue(WaterKey, out var water) && water.IndexOf('{') is > 0 and var hole)
            {
                titles[WaterEntry] = water[..hole].TrimEnd(' ', ':', '：');
            }

            if (texts.TryGetValue(UnallocatedKey, out var unallocated))
            {
                titles[UnallocatedEntry] = unallocated;
            }

            // The world map's pin popup, for a pin whose popup the game does not build ("Nivel de monstruos: {0}", "Pueblo").
            if (texts.TryGetValue(PinLevelKey, out var pinLevel) && pinLevel.Contains("{0}", StringComparison.Ordinal))
            {
                titles[PinLevelEntry] = pinLevel;
            }

            if (texts.TryGetValue(PinTownKey, out var pinTown) && pinTown.Length > 0)
            {
                titles[PinTownEntry] = pinTown;
            }

            var objectives = folder == null ? englishObjectives : DatTable.OpenTranslated(index, ObjectivesTable, folder);
            foreach (var (path, row) in objectiveRows)
            {
                if (row >= 0 && objectives?.StringOrNull(row, ObjectiveColumn)?.Trim() is { Length: > 0 } text)
                {
                    titles[ObjectivePrefix + path] = text;
                }
            }

            Console.WriteLine($"  {code}: {titles.Count} of {keys.Count + Objectives.Length} titles");
            result[code] = titles;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Console.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }
}
