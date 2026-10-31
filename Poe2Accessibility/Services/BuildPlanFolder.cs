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
        private readonly Dictionary<string, (DateTime Written, BuildPlan? Plan)> files = new(StringComparer.OrdinalIgnoreCase);
        private long? scannedAt;

        public BuildPlanFolder(string folder)
        {
            this.folder = folder;
        }

        public static string Default => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Path of Exile 2", "BuildPlanner");

        /// <summary>
        ///     The plan holding most of the passives the tree marks as the active build, if it holds at least half of them;
        ///     null for a build only on the website (no file to read notes from).
        /// </summary>
        public BuildPlan? Matching(IReadOnlyCollection<string> markedIds)
        {
            if (markedIds.Count == 0)
            {
                return null;
            }

            this.Rescan();
            var best = this.files.Values.Select(file => file.Plan).Where(plan => plan != null)
                .Select(plan => (Plan: plan!, Shared: plan!.Passives.Count(passive => markedIds.Contains(passive.Id))))
                .OrderByDescending(match => match.Shared).FirstOrDefault();
            return best.Plan != null && best.Shared * 2 >= markedIds.Count ? best.Plan : null;
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
