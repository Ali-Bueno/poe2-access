using System;
using System.Collections.Generic;
using System.Linq;
using Index = LibBundle3.Index;

/// <summary>
///     The name and description of the buffs the census's lines name (an empowering, sheltering, zone, stacking or holding
///     buff), in every language the game ships, so the plugin can say a buff's own words: <c>text|language|buff id|name|description</c>.
/// </summary>
internal static class BossBuffTexts
{
    private const string Table = "buffdefinitions";

    public static List<string> Lines(Index index, DatTable english, BuffColumns columns, IReadOnlyCollection<string> buffs)
    {
        var lines = new List<string>();
        foreach (var (code, folder) in AreaExtractor.Languages)
        {
            var table = folder == null ? english : DatTable.OpenTranslated(index, Table, folder);
            if (table == null)
            {
                continue;
            }

            var rows = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var row = 0; row < table.RowCount; row++)
            {
                rows.TryAdd(table.StringOrNull(row, DatColumnSearch.IdColumn) ?? string.Empty, row);
            }

            foreach (var buff in buffs.Where(rows.ContainsKey))
            {
                var row = rows[buff];
                lines.Add($"text|{code}|{buff}|{Text(table.StringOrNull(row, columns.Name))}|{Text(table.StringOrNull(row, columns.Description))}");
            }
        }

        return lines.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>A buff's text on one catalog line: its keyword links as shown ("[Tornado|tornadoes]" → tornadoes), no field separator, no line breaks.</summary>
    internal static string Text(string? text) => BossScripts.Clean(GameMarkup.Plain(text));
}
