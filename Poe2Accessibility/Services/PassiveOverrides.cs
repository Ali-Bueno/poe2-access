namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    /// <summary>
    ///     The passive nodes a class sees in another node's place (the game's ClassPassiveSkillOverrides, catalog
    ///     passive-overrides.txt from `Poe2UiExtract passive-overrides`): the tree's node record keeps the replaced
    ///     PassiveSkills row while the game shows the other one, so a Druid heard "Daño crítico de hechizos" over the tooltip
    ///     of "Regeneración de vida" (2026-11-09). The Witch, the Huntress and the Druid have overrides in 0.5.5f. Pure apart
    ///     from <see cref="Load"/>.
    /// </summary>
    public sealed class PassiveOverrides
    {
        private readonly Dictionary<string, Swap> swaps = new(StringComparer.Ordinal);

        private PassiveOverrides(int rowSize) => this.RowSize = rowSize;

        /// <summary>Gets the size of a PassiveSkills row, the step between two rows in memory.</summary>
        public int RowSize { get; }

        public int Count => this.swaps.Count;

        public static PassiveOverrides Load(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadLines(path)) : Parse(Array.Empty<string>());
            }
            catch (IOException)
            {
                return Parse(Array.Empty<string>());
            }
        }

        /// <summary>Lines "row-size N", then "character id, replaced id, its row, shown id, its row" by tabs; '#' starts a comment.</summary>
        public static PassiveOverrides Parse(IEnumerable<string> lines)
        {
            var rowSize = 0;
            var rows = new List<string[]>();
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (line.StartsWith('#') || parts.Length < 2)
                {
                    continue;
                }

                if (parts[0] == "row-size")
                {
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out rowSize);
                }
                else if (parts.Length >= 5)
                {
                    rows.Add(parts);
                }
            }

            var overrides = new PassiveOverrides(rowSize);
            if (rowSize <= 0)
            {
                return overrides; // no step to the shown row: nothing is swapped
            }

            foreach (var parts in rows)
            {
                if (int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var replacedRow) &&
                    int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var shownRow))
                {
                    overrides.swaps[Key(parts[0], parts[1])] = new Swap(replacedRow, parts[3], shownRow);
                }
            }

            return overrides;
        }

        /// <summary>The node the character's class sees in place of the replaced PassiveSkills Id; null when it sees that one.</summary>
        public Swap? Find(string characterPath, string replacedId) =>
            this.swaps.TryGetValue(Key(characterPath, replacedId), out var swap) ? swap : null;

        /// <summary>Bytes from the replaced row to the shown one, PassiveSkills rows lying in table order.</summary>
        public long Step(Swap swap) => (long)(swap.ShownRow - swap.ReplacedRow) * this.RowSize;

        private static string Key(string characterPath, string replacedId) => $"{characterPath.ToLowerInvariant()}\n{replacedId}";

        /// <summary>One override: the replaced node's row, and the Id and row of the node shown in its place.</summary>
        public readonly record struct Swap(int ReplacedRow, string ShownId, int ShownRow);
    }
}
