using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     A stat description file of the game (data/statdescriptions/*.csd, UTF-16 text; docs/research/file-catalogs.md,
///     "Modifier texts"): blocks that start with "description", then a line "N stat … stat", then per language (English
///     first and unnamed, the others after a line lang "Name") a count and that many lines, each
///     conditions "text" handlers. Lines "no_description stat" and include "file" stand between blocks. Pure.
/// </summary>
internal static class StatDescriptionFile
{
    /// <summary>The language of the lines before a block's first lang line.</summary>
    public const string English = "English";

    private const string DescriptionWord = "description", NoDescriptionWord = "no_description", IncludeWord = "include", LanguageWord = "lang";

    /// <summary>One block: the stats it describes and, per language name, its lines as the file holds them.</summary>
    public sealed record Description(IReadOnlyList<string> Stats, Dictionary<string, List<string>> Texts);

    /// <summary>The file's blocks, the files it includes, its lines and how many of them fit nowhere in the format.</summary>
    public sealed record Parsed(List<Description> Descriptions, List<string> Includes, int Lines, int Unplaced);

    public static Parsed Parse(string text)
    {
        var descriptions = new List<Description>();
        var includes = new List<string>();
        Description? current = null;
        var (awaitingStats, language, lines, unplaced) = (false, English, 0, 0);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            lines++;
            var word = line.Split(new[] { ' ', '\t' }, 2)[0];
            if (word == DescriptionWord)
            {
                (current, awaitingStats, language) = (null, true, English);
            }
            else if (word == NoDescriptionWord)
            {
                (current, awaitingStats) = (null, false);
            }
            else if (word == IncludeWord)
            {
                includes.Add(Quoted(line));
            }
            else if (awaitingStats)
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                awaitingStats = false;
                if (int.TryParse(parts[0], out var count) && count > 0 && parts.Length == count + 1)
                {
                    current = new Description(parts[1..], new Dictionary<string, List<string>>(StringComparer.Ordinal));
                    descriptions.Add(current);
                }
                else
                {
                    unplaced++;
                }
            }
            else if (current == null)
            {
                unplaced++;
            }
            else if (word == LanguageWord)
            {
                language = Quoted(line);
            }
            else if (line.Contains('"'))
            {
                if (!current.Texts.TryGetValue(language, out var texts))
                {
                    current.Texts[language] = texts = new List<string>();
                }

                texts.Add(line);
            }
            else if (!int.TryParse(line, out _))
            {
                unplaced++; // not the count of the lines that follow
            }
        }

        return new Parsed(descriptions, includes, lines, unplaced);
    }

    /// <summary>The text between a line's first and last quote, or empty.</summary>
    private static string Quoted(string line)
    {
        var (first, last) = (line.IndexOf('"'), line.LastIndexOf('"'));
        return last > first ? line[(first + 1)..last] : string.Empty;
    }
}
