using System; // also built into the tests, which have no implicit usings
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Poe2Access;

/// <summary>
///     What a regenerated catalog must hold before it replaces the previous one: its entries (lines of a text file, values of a
///     JSON one) not under a quarter of what today's game gives, not under half of the previous file, and no section of a
///     JSON file gone or halved. A column that moved reads few rows, so a size alone (<see cref="GameDataStamp.Collapsed"/>)
///     misses a first run and the loss of one language or section. Pure.
/// </summary>
internal static class GameDataCounts
{
    /// <summary>A file under this share of the entries it has today is refused, whatever the previous file was.</summary>
    public const int MinimumShare = 4;

    /// <summary>
    ///     Entries of each catalog made from the 0.5.x game files on 2026-11-10 (the extractor's real output: data lines of the
    ///     text files, values of the JSON ones, see <see cref="Count"/>). A catalog with no entry here has no minimum.
    /// </summary>
    private static readonly Dictionary<string, int> Today = new(StringComparer.Ordinal)
    {
        ["action-types.txt"] = 964, ["area-names.json"] = 4480, ["attack-reach.txt"] = 7309, ["boss-mechanics.txt"] = 2202,
        ["buff-names.json"] = 5986, ["class-mechanics.json"] = 47569, ["cull-skills.txt"] = 6, ["currency-art.json"] = 4140,
        ["dat-columns.json"] = 24, ["endgame.json"] = 22382, ["experience.json"] = 100, ["game-ids.txt"] = 1105,
        ["game-keys.txt"] = 13118, ["hazards.txt"] = 71, ["hit-times.txt"] = 9025, ["inline-icons.txt"] = 187,
        ["item-sounds.json"] = 3583, ["passive-overrides.txt"] = 62, ["places.json"] = 9023, ["telegraph-effects.txt"] = 1175,
        ["telegraph-skills.txt"] = 2453, ["trials.json"] = 5796, ["ui-catalog.json"] = 11866,
    };

    /// <summary>The fewest entries the catalog may hold; 0 for one with no recorded count.</summary>
    public static int Minimum(string file) => Today.TryGetValue(file, out var entries) ? Math.Max(1, entries / MinimumShare) : 0;

    /// <summary>Data lines of a text file (not blank, not a # comment); values of a JSON file, however deep.</summary>
    public static int Count(string file, string text) =>
        file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? Values(Parse(text))
            : text.Split('\n').Count(line => line.Trim().Length > 0 && !line.StartsWith('#'));

    /// <summary>Entries of each top-level object or array of a JSON object (a language, a section); empty for anything else.</summary>
    public static IReadOnlyDictionary<string, int> Sections(string text)
    {
        var sections = new Dictionary<string, int>(StringComparer.Ordinal);
        if (Parse(text) is { ValueKind: JsonValueKind.Object } root)
        {
            foreach (var property in root.EnumerateObject().Where(property => property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array))
            {
                sections[property.Name] = Values(property.Value);
            }
        }

        return sections;
    }

    /// <summary>Why <paramref name="made"/> must not replace <paramref name="previous"/> (null when there is none), or null when it may.</summary>
    public static string? Problem(string file, string? previous, string made)
    {
        var count = Count(file, made);
        if (count < Minimum(file))
        {
            return $"{count} entries, under the {Minimum(file)} a catalog of this kind has, a column probably moved";
        }

        if (previous is null)
        {
            return null;
        }

        var before = Count(file, previous);
        if (count * 2 < before)
        {
            return $"{count} entries, down from {before}, a column probably moved";
        }

        var was = Sections(previous);
        var now = Sections(made);
        foreach (var (name, entries) in was.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!now.TryGetValue(name, out var left))
            {
                return $"section '{name}' of {entries} entries is gone";
            }

            if (left * 2 < entries)
            {
                return $"section '{name}' fell from {entries} to {left} entries";
            }
        }

        return null;
    }

    private static JsonElement Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default; // not JSON: nothing in it counts
        }
    }

    private static int Values(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Sum(property => Values(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Sum(Values),
        JsonValueKind.Undefined => 0,
        _ => 1,
    };
}
