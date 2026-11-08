using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     What the class-mechanics catalog derives from the game's tables, by name alone (pure, unit-tested): the player's
///     counted resources are Stats pairs "current_x" / "maximum_x", their limits "number_of_active_y" /
///     "number_of_y_allowed", so a resource a patch adds is found without a hand list. Stat keys are rows + 1.
/// </summary>
internal static class ClassMechanics
{
    internal const string CurrentPrefix = "current_";
    internal const string ActivePrefix = "number_of_active_";
    internal const string AllowedSuffix = "_allowed";
    internal const int StatKeyFromRow = 1;

    /// <summary>The partners of "current_x", the raised one first: the game caps at the virtual maximum when it has one.</summary>
    private static readonly string[] MaximumPrefixes = { "virtual_maximum_", "maximum_", "max_", "base_maximum_" };

    /// <summary>The partners of "number_of_active_y", the raised one first.</summary>
    private static readonly string[] AllowedPrefixes = { "virtual_number_of_", "number_of_", "base_number_of_" };

    /// <summary>Buff names the game never shows: "[DNT-UNUSED] …", "[Not Displayed] …".</summary>
    private const char HiddenNameMark = '[';

    /// <summary>A counted resource: its name after "current_" and the keys of its current value and maximum (0 when the table has none).</summary>
    internal sealed record Resource(string Name, int Current, int Maximum, int VirtualMaximum);

    /// <summary>A limit: its name after "number_of_active_" and the keys of the active count and the allowed count (0 when none).</summary>
    internal sealed record Limit(string Name, int Active, int Allowed);

    /// <summary>Every "current_x" stat with its maximum partners, in table order; a lone current is kept, its maxima 0.</summary>
    internal static List<Resource> Resources(IReadOnlyList<string> statIds)
    {
        var rows = Rows(statIds);
        var resources = new List<Resource>();
        for (var row = 0; row < statIds.Count; row++)
        {
            var id = statIds[row];
            if (!id.StartsWith(CurrentPrefix, StringComparison.Ordinal) || id.Length == CurrentPrefix.Length)
            {
                continue;
            }

            var name = id[CurrentPrefix.Length..];
            var virtualMaximum = Key(rows, MaximumPrefixes[0] + name);
            var maximum = MaximumPrefixes.Skip(1).Select(prefix => Key(rows, prefix + name)).FirstOrDefault(key => key > 0);
            resources.Add(new Resource(name, row + StatKeyFromRow, maximum, virtualMaximum));
        }

        return resources;
    }

    /// <summary>Every "number_of_active_y" stat whose "number_of_y_allowed" exists, in table order.</summary>
    internal static List<Limit> Limits(IReadOnlyList<string> statIds)
    {
        var rows = Rows(statIds);
        var limits = new List<Limit>();
        for (var row = 0; row < statIds.Count; row++)
        {
            var id = statIds[row];
            if (!id.StartsWith(ActivePrefix, StringComparison.Ordinal) || id.Length == ActivePrefix.Length)
            {
                continue;
            }

            var name = id[ActivePrefix.Length..];
            var allowed = AllowedPrefixes.Select(prefix => Key(rows, prefix + name + AllowedSuffix)).FirstOrDefault(key => key > 0);
            if (allowed > 0)
            {
                limits.Add(new Limit(name, row + StatKeyFromRow, allowed));
            }
        }

        return limits;
    }

    /// <summary>Whether a buff's display name is one the game shows (not a "[DNT]" or "[Not Displayed]" note).</summary>
    internal static bool IsShownName(string? name) => !string.IsNullOrWhiteSpace(name) && name.TrimStart()[0] != HiddenNameMark;

    private static Dictionary<string, int> Rows(IReadOnlyList<string> statIds)
    {
        var rows = new Dictionary<string, int>(statIds.Count, StringComparer.Ordinal);
        for (var row = 0; row < statIds.Count; row++)
        {
            rows.TryAdd(statIds[row], row);
        }

        return rows;
    }

    private static int Key(Dictionary<string, int> rows, string id) => rows.TryGetValue(id, out var row) ? row + StatKeyFromRow : 0;
}
