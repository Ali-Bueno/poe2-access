namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>The total experience each character level starts at (catalog/experience.json, from the game's ExperienceLevels table).</summary>
    public sealed class ExperienceTable
    {
        private readonly uint[] totals;

        public ExperienceTable(IEnumerable<uint> totals) => this.totals = totals.ToArray();

        /// <summary>The table, or an empty one (no progress said) when the file is missing or broken.</summary>
        public static ExperienceTable Load(string path, PluginLog log)
        {
            try
            {
                if (File.Exists(path) && JsonSerializer.Deserialize<uint[]>(File.ReadAllText(path)) is { Length: > 1 } totals)
                {
                    return new ExperienceTable(totals);
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                log.Warn($"Experience table unreadable ({exception.Message}): no level progress");
            }

            return new ExperienceTable(Array.Empty<uint>());
        }

        /// <summary>Whole percent of the way from <paramref name="level"/> to the next; null at the last level or when the experience lies outside the level.</summary>
        public int? PercentToNext(int level, uint experience)
        {
            if (level < 1 || level >= this.totals.Length)
            {
                return null;
            }

            uint start = this.totals[level - 1], next = this.totals[level];
            return experience >= start && experience < next ? (int)((experience - start) * 100L / (next - start)) : null;
        }
    }
}
