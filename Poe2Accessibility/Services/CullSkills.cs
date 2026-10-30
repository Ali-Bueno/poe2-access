namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Granted effects that deal Culling Strike (catalog/cull-skills.txt, from the game's skill stat sets), by the id the
    ///     player's Actor lists ("KillingPalmPlayer"). Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class CullSkills
    {
        private readonly HashSet<string> ids;

        public CullSkills(IEnumerable<string> lines) =>
            this.ids = lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToHashSet(StringComparer.Ordinal);

        public int Count => this.ids.Count;

        public static CullSkills Load(string path) => new(File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>());

        public bool Culls(string skillId) => this.ids.Contains(skillId);

        /// <summary>True when any of the granted effect ids deals Culling Strike.</summary>
        public bool AnyCulls(IEnumerable<string> skillIds) => skillIds.Any(this.ids.Contains);
    }
}
