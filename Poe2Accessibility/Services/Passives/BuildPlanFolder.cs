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

        /// <summary>Per character, the file its own tree's marks matched last and when: one character's build is never another's.</summary>
        private readonly Dictionary<string, (string? Path, DateTime At)> matched = new(StringComparer.Ordinal);

        /// <summary>Per character, the file the planner's own list named last.</summary>
        private readonly Dictionary<string, string> named = new(StringComparer.Ordinal);

        /// <summary>Per character, the last build and source told to <see cref="Log"/>.</summary>
        private readonly Dictionary<string, string> told = new(StringComparer.Ordinal);

        public BuildPlanFolder(string folder, string config)
        {
            this.folder = folder;
            this.config = config;
        }

        /// <summary>Told once per change of a character's build or of where it was read from (set by the plugin).</summary>
        public Action<string>? Log { get; set; }

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
            if (!this.ReadConfig() || (onTree && seen.At >= this.configRead.Written))
            {
                return this.PlanAt(seen.Path); // the settings being written, or older than what this character's tree showed
            }

            if (this.SettingsFile(character) is not { } name)
            {
                return this.PlanAt(seen.Path);
            }

            var set = this.files.FirstOrDefault(file => IsFile(file.Key, name));
            this.Tell(character, set.Key ?? name, set.Key != null ? "the game's settings" : "the game's settings, no such file read");
            return set.Value.Plan;
        }

        /// <summary>
        ///     The plan the tree's marks are: the one holding most of the marked passives, if at least half of them, and
        ///     among equals the one with the fewest passives besides, then the file the game's settings name; null for a
        ///     build only on the website (no file to read notes from). Remembered for <paramref name="character"/>, whose
        ///     tree it is; for nobody when the name is not known.
        /// </summary>
        public BuildPlan? Matching(string character, IReadOnlyCollection<string> markedIds)
        {
            if (markedIds.Count == 0)
            {
                this.Remember(character, null, "the tree marks no node");
                return null;
            }

            this.Rescan();
            var ranked = this.files.Where(file => file.Value.Plan != null)
                .Select(file => (Path: file.Key, Plan: file.Value.Plan!, Fit: file.Value.Plan!.Fit(markedIds)))
                .OrderByDescending(entry => entry.Fit.Shared).ThenBy(entry => entry.Fit.Others).ToList();
            if (ranked.Count == 0 || ranked[0].Fit.Shared * 2 < markedIds.Count)
            {
                this.Remember(character, null, $"no file holds half of the tree's {markedIds.Count} marked nodes");
                return null;
            }

            // Files of one tree (a guide's variants) differ in their gems alone: the planner's list says which is active,
            // else the game's settings.
            var alike = ranked.TakeWhile(entry => entry.Fit == ranked[0].Fit).ToList();
            var listed = alike.Count > 1 && this.named.TryGetValue(character, out var shown) && alike.Any(entry => entry.Path == shown) ? shown : null;
            var name = alike.Count > 1 && listed == null && this.ReadConfig() ? this.SettingsFile(character) : null;
            var chosen = alike.FirstOrDefault(entry => entry.Path == listed || (name != null && IsFile(entry.Path, name)));
            var match = chosen.Path != null ? chosen : alike[0];
            var among = alike.Count == 1 ? string.Empty : chosen.Path == null ? $", first of {alike.Count} files alike" : listed != null ? $", the planner's list among {alike.Count} files alike" : $", the game's settings among {alike.Count} files alike";
            this.Remember(character, match.Path, $"the tree's marks: {match.Fit.Shared} of {markedIds.Count} marked, {match.Fit.Others} besides{among}");
            return match.Plan;
        }

        /// <summary>
        ///     The planner's own list showing <paramref name="name"/> as the character's build: the file of that name, or
        ///     the only one whose "name" it is, is the active one. A name of no file (none, a website build) tells nothing.
        /// </summary>
        public void Shown(string character, string name)
        {
            this.Rescan();
            if (character.Length == 0 || this.FileNamed(name.Trim()) is not { } path)
            {
                return;
            }

            this.named[character] = path;
            this.Remember(character, path, "the planner's list");
        }

        private string? FileNamed(string name)
        {
            bool Is(string text) => string.Equals(text.Trim(), name, StringComparison.OrdinalIgnoreCase);
            var titled = this.files.Where(file => file.Value.Plan is { } plan && Is(plan.Name)).Select(file => file.Key).ToList();
            return this.files.Keys.FirstOrDefault(path => Is(Path.GetFileNameWithoutExtension(path))) ?? (titled.Count == 1 ? titled[0] : null);
        }

        private static bool IsFile(string path, string name) => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);

        /// <summary>Reads the game's settings again once written; false while they cannot be read.</summary>
        private bool ReadConfig()
        {
            try
            {
                var written = File.Exists(this.config) ? File.GetLastWriteTimeUtc(this.config) : DateTime.MinValue;
                if (written != this.configRead.Written)
                {
                    this.configRead = (written, written == DateTime.MinValue ? string.Empty : File.ReadAllText(this.config));
                }

                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>The name of the file the settings read last give the character's build; null with none.</summary>
        private string? SettingsFile(string character) =>
            BuildPlan.ActivePath(this.configRead.Text, character) is { } path ? Path.GetFileName(path) : null;

        /// <summary>The plan of a file as it reads now: one written again is read again, one gone is no build.</summary>
        private BuildPlan? PlanAt(string? path) => path != null && this.files.TryGetValue(path, out var file) ? file.Plan : null;

        private void Remember(string character, string? path, string source)
        {
            if (character.Length > 0)
            {
                this.matched[character] = (path, DateTime.UtcNow);
                this.Tell(character, path, source);
            }
        }

        private void Tell(string character, string? path, string source)
        {
            var line = $"{(path == null ? "no file" : Path.GetFileName(path))} ({source})";
            if (this.Log != null && !(this.told.TryGetValue(character, out var last) && last == line))
            {
                this.told[character] = line;
                this.Log($"Build plan: {character} follows {line}");
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
