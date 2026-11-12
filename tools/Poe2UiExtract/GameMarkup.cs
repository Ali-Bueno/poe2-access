using System.Linq;
using System.Text.RegularExpressions;

/// <summary>The game's keyword links in its texts, "[Keyword|shown words]" or "[Keyword]". Pure.</summary>
internal static class GameMarkup
{
    private static readonly Regex Keyword = new(@"\[([^\]|]+)(?:\|([^\]]+))?\]", RegexOptions.Compiled);

    /// <summary>A game text as it is shown: "[ContainsBreach|Unstable Breaches]" shows "Unstable Breaches", "[Essence]" shows "Essence".</summary>
    public static string Plain(string? text) =>
        text == null ? string.Empty : Keyword.Replace(text, match => match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value).Trim();

    /// <summary>A colour inside a line: "&lt;enchanted&gt;{Explota cuando se detona}".</summary>
    private static readonly Regex Coloured = new(@"<\w+>\{([^{}]*)\}", RegexOptions.Compiled);

    /// <summary>A count the game fills in as it draws the box ("Cristales energizados obtenidos: 0"): not what the object is for.</summary>
    private static readonly Regex Counter = new(@"[:：]\s*\d+\s*\S{0,4}\s*$", RegexOptions.Compiled);

    /// <summary>The body of an object's box (a WorldDescription) as shown, its lines apart, links and colours gone; empty for a counter.</summary>
    public static string BoxBody(string text)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n').Select(line => Plain(Coloured.Replace(line, "$1"))).Where(line => line.Length > 0).ToList();
        return lines.Any(line => Counter.IsMatch(line)) ? string.Empty : string.Join("\n", lines);
    }

    /// <summary>The keyword of a text's first link ("ContainsBreach" in "Open all [ContainsBreach|Unstable Breaches]"), empty for none.</summary>
    public static string FirstKeyword(string text) => Keyword.Match(text) is { Success: true } match ? match.Groups[1].Value : string.Empty;
}
