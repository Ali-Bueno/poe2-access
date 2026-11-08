using System.Text.RegularExpressions;

/// <summary>The game's keyword links in its texts, "[Keyword|shown words]" or "[Keyword]". Pure.</summary>
internal static class GameMarkup
{
    private static readonly Regex Keyword = new(@"\[([^\]|]+)(?:\|([^\]]+))?\]", RegexOptions.Compiled);

    /// <summary>A game text as it is shown: "[ContainsBreach|Unstable Breaches]" shows "Unstable Breaches", "[Essence]" shows "Essence".</summary>
    public static string Plain(string? text) =>
        text == null ? string.Empty : Keyword.Replace(text, match => match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value).Trim();

    /// <summary>The keyword of a text's first link ("ContainsBreach" in "Open all [ContainsBreach|Unstable Breaches]"), empty for none.</summary>
    public static string FirstKeyword(string text) => Keyword.Match(text) is { Success: true } match ? match.Groups[1].Value : string.Empty;
}
