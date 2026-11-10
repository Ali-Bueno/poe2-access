using System.Text;
using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `inline-icons` command: the game's inline markup images, the `&lt;&lt;token&gt;&gt;` a text shows as a picture
///     ("&lt;&lt;village_gold&gt;&gt; 500" in the Currency Exchange, "&lt;&lt;RitualTribute&gt;&gt;"), as "token TAB art" lines. They are
///     the `MarkupImages` props of `metadata/ui/uisettings.xml` (0.5.5f: 166 entries, the pad glyphs as "Controller/Button4",
///     the rest as "Art/2DArt/UIImages/…"). The plugin names a token by its art (`InlineIcons`), so a token a patch adds that
///     points at known art is read without a code change.
/// </summary>
internal static class InlineIconExtractor
{
    private const string SettingsPath = "metadata/ui/uisettings.xml";

    private static readonly Regex Section = new(@"<Props\s+id=""MarkupImages""\s*>(.*?)</Props>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Entry = new(@"<String\s+id=""(\w+)""\s+value=""([^""]+)""\s*/>", RegexOptions.Compiled);

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var file = index.Files.Values.FirstOrDefault(candidate => string.Equals(candidate.Path, SettingsPath, StringComparison.OrdinalIgnoreCase));
        if (file == null)
        {
            Say.Out.WriteLine($"inline-icons: {SettingsPath} missing");
            return 1;
        }

        var text = file.ReadText();
        var lines = Parse(text);
        if (lines.Count == 0)
        {
            Say.Out.WriteLine("inline-icons: no MarkupImages entries");
            return 1;
        }

        File.WriteAllLines(outFile, lines.Select(pair => $"{pair.Token}\t{pair.Art}"));
        Say.Out.WriteLine($"inline-icons: {lines.Count} markup images");
        return 0;
    }

    /// <summary>Token and art of every MarkupImages entry, in file order; a token defined twice keeps its first art.</summary>
    internal static List<(string Token, string Art)> Parse(string settings)
    {
        var found = new List<(string Token, string Art)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match section in Section.Matches(settings))
        {
            foreach (Match entry in Entry.Matches(section.Groups[1].Value))
            {
                if (seen.Add(entry.Groups[1].Value))
                {
                    found.Add((entry.Groups[1].Value, entry.Groups[2].Value));
                }
            }
        }

        return found;
    }
}
