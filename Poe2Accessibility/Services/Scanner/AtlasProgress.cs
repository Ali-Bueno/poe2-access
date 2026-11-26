namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>What changed on the Atlas since the character's last opening (atlas-navigator Phase 4).</summary>
    public sealed record AtlasProgressChange(IReadOnlyList<string> Completed, int CompletedCount, IReadOnlyList<string> NowAccessible, int NowAccessibleCount)
    {
        public bool IsEmpty => this.CompletedCount == 0 && this.NowAccessibleCount == 0;
    }

    /// <summary>
    ///     Each character's Atlas node states at the last opening, kept by grid place (read off the node, so no
    ///     catalog id or game update renames it), and the diff against a new opening. One line per node,
    ///     "character, tab, x,y, tab, state"; a file that cannot be written keeps the states for this session.
    /// </summary>
    public sealed class AtlasProgress
    {
        /// <summary>
        ///     A new opening sharing fewer of the stored places than this is another Atlas (a new league, a
        ///     reworked one after a patch): it is stored without a diff rather than told as hundreds of changes.
        /// </summary>
        public const double MinSharedPlaces = 0.5;

        private readonly string? path;
        private readonly Dictionary<string, Dictionary<string, AtlasNodeState>> states = new(StringComparer.Ordinal);

        /// <param name="path">The file; null keeps the states in memory only.</param>
        public AtlasProgress(string? path)
        {
            this.path = path;
            if (path == null || !File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.Split('\t') is [{ Length: > 0 } character, { Length: > 0 } place, { } state] &&
                        Enum.TryParse<AtlasNodeState>(state, out var parsed) && Enum.IsDefined(parsed))
                    {
                        this.For(character)[place] = parsed;
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable: start without states.
            }
            catch (UnauthorizedAccessException)
            {
                // The same.
            }
        }

        /// <summary>The grid place a node is kept by.</summary>
        public static string Place(int gridX, int gridY) => $"{gridX},{gridY}";

        /// <summary>
        ///     Compares the opening's nodes with the character's last ones, then keeps the opening's. Null when
        ///     there is nothing to compare: the first opening, or another Atlas (<see cref="MinSharedPlaces"/>).
        /// </summary>
        /// <param name="nodes">Every node of the opening: grid place, state and spoken name (empty when unnamed).</param>
        /// <param name="named">How many names each list says; the rest are counted.</param>
        public AtlasProgressChange? Update(string character, IEnumerable<(int GridX, int GridY, AtlasNodeState State, string Name)> nodes, int named)
        {
            // A place drawn twice keeps its furthest state and its first name.
            var now = new Dictionary<string, (AtlasNodeState State, string Name)>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                var place = Place(node.GridX, node.GridY);
                now[place] = now.TryGetValue(place, out var seen)
                    ? (seen.State >= node.State ? seen.State : node.State, seen.Name.Length > 0 ? seen.Name : node.Name)
                    : (node.State, node.Name);
            }

            if (now.Count == 0)
            {
                return null;
            }

            var before = this.states.GetValueOrDefault(character);
            var change = before == null ? null : Diff(before, now, named);
            this.Store(character, now.ToDictionary(pair => pair.Key, pair => pair.Value.State, StringComparer.Ordinal));
            return change;
        }

        private static AtlasProgressChange? Diff(IReadOnlyDictionary<string, AtlasNodeState> before, IReadOnlyDictionary<string, (AtlasNodeState State, string Name)> now, int named)
        {
            var shared = before.Keys.Count(now.ContainsKey);
            if (before.Count == 0 || shared < before.Count * MinSharedPlaces)
            {
                return null;
            }

            // Only places known both times change: a place new to this opening (fog drawn in, a canvas built
            // further) has no earlier state to differ from.
            var completed = new List<string>();
            var accessible = new List<string>();
            foreach (var (place, (state, name)) in now.OrderBy(pair => pair.Value.Name, StringComparer.CurrentCulture).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!before.TryGetValue(place, out var was) || was == state)
                {
                    continue;
                }

                if (state == AtlasNodeState.Completed)
                {
                    completed.Add(name);
                }
                else if (state == AtlasNodeState.Accessible && was == AtlasNodeState.Locked)
                {
                    accessible.Add(name);
                }
            }

            static IReadOnlyList<string> Named(List<string> names, int count) => names.Where(name => name.Length > 0).Take(count).ToList();
            return new AtlasProgressChange(Named(completed, named), completed.Count, Named(accessible, named), accessible.Count);
        }

        private Dictionary<string, AtlasNodeState> For(string character)
        {
            if (!this.states.TryGetValue(character, out var places))
            {
                places = new Dictionary<string, AtlasNodeState>(StringComparer.Ordinal);
                this.states[character] = places;
            }

            return places;
        }

        private void Store(string character, Dictionary<string, AtlasNodeState> places)
        {
            var before = this.states.GetValueOrDefault(character);
            if (before != null && before.Count == places.Count && before.All(pair => places.TryGetValue(pair.Key, out var state) && state == pair.Value))
            {
                return;
            }

            this.states[character] = places;
            if (this.path == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
                File.WriteAllLines(this.path, this.states.SelectMany(who => who.Value.Select(place => $"{who.Key}\t{place.Key}\t{place.Value}")));
            }
            catch (IOException)
            {
                // Kept in memory for this session.
            }
            catch (UnauthorizedAccessException)
            {
                // The same.
            }
        }
    }
}
