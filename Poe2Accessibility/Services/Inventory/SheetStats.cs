namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>A number of the character sheet: the game's stat behind a widget, and what the widget shows of it.</summary>
    public sealed record SheetStat(string Stat, BuildNotes.Attribute? Attribute = null, string? Uncapped = null);

    /// <summary>
    ///     Tells which stat a character sheet widget shows by the English keys of its tooltip's markup, the same in every
    ///     language and at every resolution; the widgets hold no number, and at some sizes no caption (docs/research/ui-layers.md).
    /// </summary>
    public static class SheetStats
    {
        private const string ResistanceMark = "UncappedResist";
        private const string AttributeMark = "SupportGem";

        private static readonly Regex Key = new(@"\[([A-Za-z]+)(?:\|[^\]]*)?\]", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> Resistances = new()
        {
            ["Fire"] = "fire",
            ["Cold"] = "cold",
            ["Lightning"] = "lightning",
            ["Chaos"] = "chaos",
        };

        private static readonly Dictionary<string, BuildNotes.Attribute> Attributes = new()
        {
            ["Strength"] = BuildNotes.Attribute.Strength,
            ["Dexterity"] = BuildNotes.Attribute.Dexterity,
            ["Intelligence"] = BuildNotes.Attribute.Intelligence,
        };

        /// <summary>The stat of a widget by its tooltip's markup; null for any other tooltip.</summary>
        public static SheetStat? FromMarkup(string markup)
        {
            var keys = Key.Matches(markup).Select(match => match.Groups[1].Value).ToList();
            if (keys.Contains(ResistanceMark) && keys.FirstOrDefault(Resistances.ContainsKey) is { } damage)
            {
                var element = Resistances[damage];
                return new SheetStat($"{element}_damage_resistance_%", Uncapped: $"uncapped_{element}_damage_resistance_%");
            }

            return keys.Contains(AttributeMark) && Attributes.TryGetValue(keys[0], out var attribute)
                ? new SheetStat(keys[0].ToLowerInvariant(), attribute)
                : null;
        }
    }
}
