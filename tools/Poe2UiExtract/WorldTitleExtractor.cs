using System.Text;
using System.Text.RegularExpressions;

/// <summary>
///     `world-titles` command: the names objects show only in a WorldDescription, { language: { .ot path without its extension:
///     title } }. The game gives such an object no render name: the real Relic Altar, the Trial's Death Crystals. A .ot's
///     title_text is a ClientStrings key (<see cref="ClientStringColumns"/>). Also "objective:" + path: the tracker line without
///     which an object means nothing, the QuestStates text column that holds it (+0x59, row 655 for the crystals; +0x2C read
///     the same text until 2026-11-10 only because the list before it is empty on that row). Read-only on the game.
/// </summary>
internal static class WorldTitleExtractor
{
    private const string ObjectExtension = ".ot";
    private const string StringsTable = ClientStringColumns.Table;
    private const string ObjectivesTable = "queststates";
    private const int ObjectiveColumn = 0x59;
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

    /// <summary>What the box says under its title, a ClientStrings key too: what the object is for.</summary>
    private static readonly Regex BodyKey = new("body_text\\s*=\\s*\"([^\"]+)\"", RegexOptions.Compiled);
    private const string BodyPrefix = "body:";

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
        using var lease = SharedBundles.OpenParsed(gameDir);
        var index = lease.Index;
        var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var bodies = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in index.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(ObjectExtension, StringComparison.Ordinal)))
        {
            var script = GameFormats.Noted(file.Path, Encoding.Unicode.GetString(file.Read().Span));
            var title = TitleKey.Match(script);
            if (title.Success)
            {
                keys[file.Path![..^ObjectExtension.Length]] = title.Groups[1].Value;
            }

            var body = BodyKey.Match(script);
            if (body.Success)
            {
                bodies[file.Path![..^ObjectExtension.Length]] = body.Groups[1].Value;
            }
        }

        // Both tables' columns are told in English by rows whose text is known, and read at the same offsets in every language.
        var english = DatTable.Open(index, StringsTable);
        var englishObjectives = DatTable.Open(index, ObjectivesTable);
        if (english == null || englishObjectives == null)
        {
            Say.Out.WriteLine("ClientStrings or QuestStates not found: world-titles left out.");
            return 1;
        }

        var search = new DatColumnSearch();
        var strings = ClientStringColumns.Find(english, search);
        var objectiveColumn = search.Find(englishObjectives, "QuestStates.Objective", ObjectiveColumn, offset =>
            DatColumnSearch.IsText(englishObjectives, offset, _ => true) && Objectives.All(wanted => DatAnchors.RowWith(englishObjectives, offset, wanted.Objective) >= 0));
        if (!search.Report("world-titles") || strings is not { } columns)
        {
            return 1;
        }

        var objectiveRows = Objectives.Select(wanted => (wanted.Path, Row: DatAnchors.RowWith(englishObjectives, objectiveColumn, wanted.Objective))).ToList();
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? english : DatTable.OpenTranslated(index, StringsTable, folder);
            if (table == null)
            {
                Say.Out.WriteLine($"  {code}: no ClientStrings");
                continue;
            }

            if (!ClientStringColumns.SameShape(english, table))
            {
                Say.Out.WriteLine($"  {code}: ClientStrings differs from the English table in rows or row size: its columns cannot be told, world-titles left out");
                return 1;
            }

            var texts = ClientStringColumns.Read(table, columns);

            var titles = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (path, key) in keys)
            {
                if (texts.TryGetValue(key, out var text))
                {
                    titles[path] = Colour.Replace(text, "$1");
                }
            }

            foreach (var (path, key) in bodies)
            {
                if (texts.TryGetValue(key, out var text) && GameMarkup.BoxBody(text) is { Length: > 0 } shown)
                {
                    titles[BodyPrefix + path] = shown;
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
            if (objectives != null && !ClientStringColumns.SameShape(englishObjectives, objectives))
            {
                Say.Out.WriteLine($"  {code}: QuestStates differs from the English table in rows or row size: world-titles left out");
                return 1;
            }

            foreach (var (path, row) in objectiveRows)
            {
                if (row >= 0 && objectives?.StringOrNull(row, objectiveColumn)?.Trim() is { Length: > 0 } text)
                {
                    titles[ObjectivePrefix + path] = text;
                }
            }

            Say.Out.WriteLine($"  {code}: {titles.Count} of {keys.Count + Objectives.Length} titles");
            result[code] = titles;
        }

        AreaExtractor.WriteJson(outFile, result);
        Say.Out.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }
}
