namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     The Build Planner files the game lists (Documents\My Games\Path of Exile 2\BuildPlanner, GGG developer docs),
    ///     read again when one changes; the active build is the file whose passives the tree marks.
    /// </summary>
    public sealed class BuildPlanFolder
    {
        /// <summary>The folder is looked at again at most this often.</summary>
        private const int RescanMs = 5000;

        private readonly string folder;
        private readonly string config;
        private readonly Dictionary<string, (DateTime Written, BuildPlan? Plan)> files = new(StringComparer.OrdinalIgnoreCase);
        private long? scannedAt;
        private (DateTime Written, string Text) configRead;

        /// <summary>Per character, what its own tree's marks matched last and when: one character's build is never another's.</summary>
        private readonly Dictionary<string, (BuildPlan? Plan, DateTime At)> matched = new(StringComparer.Ordinal);

        public BuildPlanFolder(string folder, string config)
        {
            this.folder = folder;
            this.config = config;
        }

        public static string Default => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Path of Exile 2", "BuildPlanner");

        /// <summary>The game's settings file, which keeps each character's active build ("active_builds").</summary>
        public static string DefaultConfig => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Path of Exile 2", "poe2_production_Config.ini");

        /// <summary>
        ///     The character's active build without the tree open: what the tree's marks showed since the game last wrote its
        ///     settings (a build, or none), else the file those settings name for the character; null when there is none.
        /// </summary>
        public BuildPlan? Active(string character)
        {
            this.Rescan();
            var onTree = this.matched.TryGetValue(character, out var seen);
            try
            {
                var written = File.Exists(this.config) ? File.GetLastWriteTimeUtc(this.config) : DateTime.MinValue;
                if (onTree && seen.At >= written)
                {
                    return seen.Plan;
                }

                if (written != this.configRead.Written)
                {
                    this.configRead = (written, written == DateTime.MinValue ? string.Empty : File.ReadAllText(this.config));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return seen.Plan; // the settings being written: what this character's tree showed last
            }

            var path = BuildPlan.ActivePath(this.configRead.Text, character);
            var name = path == null ? null : Path.GetFileName(path);
            return name == null ? seen.Plan
                : this.files.FirstOrDefault(file => string.Equals(Path.GetFileName(file.Key), name, StringComparison.OrdinalIgnoreCase)).Value.Plan;
        }

        /// <summary>
        ///     The plan holding most of the passives the tree marks as the active build, if it holds at least half of them;
        ///     null for a build only on the website (no file to read notes from). Remembered for <paramref name="character"/>,
        ///     whose tree it is; for nobody when the name is not known.
        /// </summary>
        public BuildPlan? Matching(string character, IReadOnlyCollection<string> markedIds)
        {
            if (markedIds.Count == 0)
            {
                this.Remember(character, null);
                return null;
            }

            this.Rescan();
            var best = this.files.Values.Select(file => file.Plan).Where(plan => plan != null)
                .Select(plan => (Plan: plan!, Shared: plan!.Passives.Count(passive => markedIds.Contains(passive.Id))))
                .OrderByDescending(match => match.Shared).FirstOrDefault();
            var plan = best.Plan != null && best.Shared * 2 >= markedIds.Count ? best.Plan : null;
            this.Remember(character, plan);
            return plan;
        }

        private void Remember(string character, BuildPlan? plan)
        {
            if (character.Length > 0)
            {
                this.matched[character] = (plan, DateTime.UtcNow);
            }
        }

        private void Rescan()
        {
            var now = Environment.TickCount64;
            if (this.scannedAt is { } at && now - at < RescanMs)
            {
                return;
            }

            this.scannedAt = now;
            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in Directory.Exists(this.folder) ? Directory.EnumerateFiles(this.folder, "*.build") : Enumerable.Empty<string>())
                {
                    seen.Add(path);
                    var written = File.GetLastWriteTimeUtc(path);
                    if (!this.files.TryGetValue(path, out var known) || known.Written != written)
                    {
                        this.files[path] = (written, BuildPlan.Parse(File.ReadAllText(path)));
                    }
                }

                foreach (var gone in this.files.Keys.Where(path => !seen.Contains(path)).ToList())
                {
                    this.files.Remove(gone);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A file being written: read on the next scan.
            }
        }
    }
}
