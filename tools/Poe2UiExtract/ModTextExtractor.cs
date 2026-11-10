using System.Text;
using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `mod-texts` command: the lines the game writes a modifier with, in every language, so the mod can tell which
///     modifier a line written in one language is and say it in another (docs/research/file-catalogs.md, "Modifier texts"):
///     { "source": the file, "texts": [ { "stats": "stat ids", "texts": { language: [ the block's lines as the file holds
///     them: conditions "text" handlers ] } } ] }, a block a line. The file is the one items are described from,
///     <see cref="Source"/>; conditions and handlers are kept whole for the plugin to apply. Read-only on the game.
/// </summary>
internal static class ModTextExtractor
{
    internal const string Step = "mod-texts";

    /// <summary>The descriptions of the stats an item's modifiers hold (advanced_mod_stat_descriptions only includes it).</summary>
    internal const string Source = "data/statdescriptions/stat_descriptions.csd";

    /// <summary>The file no longer reads as this format when more than one line in this many fits nowhere in it.</summary>
    internal const int UnplacedLinesPer = 100;

    // A block whose texts are known (read from the 0.5.x file, 2026-11-10): the fire resistance line.
    internal const string KnownStat = "base_fire_damage_resistance_%";
    internal const string KnownEnglish = "Fire Resistance", KnownSpanishLanguage = "Spanish", KnownSpanish = "fuego";

    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        if (BundleFiles.Find(index, Source) is not { } file)
        {
            Say.Out.WriteLine($"No {Source}: {Step} left out.");
            return 1;
        }

        var parsed = StatDescriptionFile.Parse(file.ReadText());
        Say.Out.WriteLine($"  {Source}: {parsed.Descriptions.Count} descriptions in {parsed.Lines} lines, {parsed.Unplaced} lines fit nowhere, includes: {(parsed.Includes.Count == 0 ? "none" : string.Join(", ", parsed.Includes))}");
        if (Problem(parsed) is { } problem)
        {
            Say.Out.WriteLine($"  {problem}, {Step} keeps its previous catalog");
            return 1;
        }

        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var text = new StringBuilder();
        text.Append("{\n\"source\": ").Append(JsonSerializer.Serialize(Source, Json)).Append(",\n\"texts\": [\n");
        var written = 0;
        foreach (var description in parsed.Descriptions)
        {
            var texts = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (language, lines) in description.Texts)
            {
                if (LanguageCode(language) is not { } code)
                {
                    unknown.Add(language);
                    continue;
                }

                texts[code] = lines;
                counts[code] = counts.GetValueOrDefault(code) + 1;
            }

            if (texts.Count == 0)
            {
                continue;
            }

            text.Append(written++ == 0 ? string.Empty : ",\n").Append(JsonSerializer.Serialize(new { stats = string.Join(' ', description.Stats), texts }, Json));
        }

        text.Append("\n]\n}\n");
        if (unknown.Count > 0)
        {
            Say.Out.WriteLine($"  languages the mod has no code for, left out: {string.Join(", ", unknown)}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, text.ToString());
        Say.Out.WriteLine($"Wrote {outFile}: {written} descriptions; {string.Join(", ", counts.Select(pair => $"{pair.Key} {pair.Value}"))}");
        return written > 0 ? 0 : 1;
    }

    /// <summary>Why the parsed file must not become a catalog, or null: no block, too many lines outside the format, or the known block reading otherwise.</summary>
    internal static string? Problem(StatDescriptionFile.Parsed parsed)
    {
        if (parsed.Descriptions.Count == 0)
        {
            return "no description block was read: the format changed";
        }

        if (parsed.Unplaced * UnplacedLinesPer > parsed.Lines)
        {
            return $"{parsed.Unplaced} of {parsed.Lines} lines fit nowhere in the format: it changed";
        }

        var known = parsed.Descriptions.FirstOrDefault(description => description.Stats.Count == 1 && description.Stats[0] == KnownStat);
        if (known == null)
        {
            Say.Out.WriteLine($"  the known block {KnownStat} is not in the file, so its texts were judged by the format alone");
            return null;
        }

        bool Holds(string language, string words) => known.Texts.TryGetValue(language, out var lines) && lines.Any(line => line.Contains(words, StringComparison.Ordinal));
        return Holds(StatDescriptionFile.English, KnownEnglish) && Holds(KnownSpanishLanguage, KnownSpanish)
            ? null
            : $"the block {KnownStat} no longer reads '{KnownEnglish}' in English and '{KnownSpanish}' in {KnownSpanishLanguage}";
    }

    /// <summary>The mod's code of a language as the file names it; null for one the mod has none for.</summary>
    internal static string? LanguageCode(string language) =>
        language == StatDescriptionFile.English ? "en" : AreaExtractor.LanguageCodes.TryGetValue(language, out var code) ? code : null;
}
