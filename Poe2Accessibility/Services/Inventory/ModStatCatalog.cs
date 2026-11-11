namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    /// <summary>One stat a modifier gives and the least and most it rolls.</summary>
    public readonly record struct ModStat(string Stat, int Least, int Most);

    /// <summary>
    ///     What each modifier of the game gives, by its Mods Id (catalog/mod-stats.json, `Poe2UiExtract mod-stats`;
    ///     docs/research/item-mods.md): its stats in the order its values follow, each with what it rolls. Pure apart
    ///     from <see cref="Load"/>.
    /// </summary>
    public sealed class ModStatCatalog
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private readonly Dictionary<string, string> mods;
        private readonly Dictionary<string, string> unproven;
        private readonly HashSet<string> hidden;

        private ModStatCatalog(FileData data) =>
            (this.mods, this.unproven, this.hidden) = (new(data.Mods ?? new(), StringComparer.Ordinal), new(data.Unproven ?? new(), StringComparer.Ordinal), new(data.Hidden ?? new(), StringComparer.Ordinal));

        /// <summary>The catalog the plugin loaded; empty until then, so every modifier is unknown.</summary>
        public static ModStatCatalog Shared { get; private set; } = new(new FileData());

        /// <summary>Gets how many modifiers the catalog knows, whether their values are told apart or not.</summary>
        public int Count => this.mods.Count + this.unproven.Count;

        /// <summary>Reads the catalog; a missing or unreadable one is logged once and leaves every modifier unknown.</summary>
        public static void Load(PluginLog log, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    log.Warn($"Modifier stat catalog: {path} not found; no item's lines are made from its modifiers");
                    return;
                }

                Shared = Parse(File.ReadAllText(path));
                log.Info($"Modifier stat catalog: {Shared.mods.Count} modifiers, {Shared.unproven.Count} not told apart, {Shared.hidden.Count} hidden stats from the game files");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                log.Warn($"Modifier stat catalog unreadable ({error.Message}); no item's lines are made from its modifiers");
            }
        }

        /// <summary>The catalog in <paramref name="json"/>; a JsonException when it is not one.</summary>
        public static ModStatCatalog Parse(string json) => new(JsonSerializer.Deserialize<FileData>(json, Json) ?? new FileData());

        /// <summary>The modifier's stats in the order its values follow; null for one the files lack, one whose values are not told apart, or an entry that does not read.</summary>
        public IReadOnlyList<ModStat>? StatsOf(string mod)
        {
            if (!this.mods.TryGetValue(mod, out var written))
            {
                return null;
            }

            var words = written.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var stats = new ModStat[words.Length / 3];
            for (var at = 0; at < stats.Length; at++)
            {
                if (!int.TryParse(words[(at * 3) + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var least) ||
                    !int.TryParse(words[(at * 3) + 2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var most))
                {
                    return null;
                }

                stats[at] = new ModStat(words[at * 3], least, most);
            }

            return words.Length % 3 == 0 ? stats : null;
        }

        /// <summary>The stats of a modifier whose values are not told apart (a stat outside the columns the values follow); null when it is not one of those.</summary>
        public IReadOnlyList<string>? UnprovenStatsOf(string mod) =>
            this.unproven.TryGetValue(mod, out var written) ? written.Split(' ', StringSplitOptions.RemoveEmptyEntries) : null;

        /// <summary>Whether the game's description file says the stat has no line.</summary>
        public bool IsHidden(string stat) => this.hidden.Contains(stat);

        /// <summary>The file as `mod-stats` writes it.</summary>
        private sealed class FileData
        {
            public List<string>? Hidden { get; set; }

            public Dictionary<string, string>? Mods { get; set; }

            public Dictionary<string, string>? Unproven { get; set; }
        }
    }
}
