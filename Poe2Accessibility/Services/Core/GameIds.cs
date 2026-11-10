namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     The game's own numbers for animations and stats (catalog/game-ids.txt, read from its tables after each patch). GameHelper's
    ///     enums hold the same names as they were when generated, and a row inserted since shifts all that follow. Pure.
    /// </summary>
    public sealed class GameIds
    {
        private const string AnimationLine = "animation";
        private const string StatLine = "stat";
        private const char Comment = '#';

        private readonly string?[] animations;
        private readonly Dictionary<string, int> stats;

        private GameIds(string?[] animations, Dictionary<string, int> stats)
        {
            this.animations = animations;
            this.stats = stats;
        }

        /// <summary>Gets the ids in use: empty until the catalog is loaded.</summary>
        public static GameIds Current { get; private set; } = new(Array.Empty<string?>(), new Dictionary<string, int>());

        /// <summary>Gets a value indicating whether the game's table is known, so a name it lacks is new rather than unread.</summary>
        public bool HasAnimations => this.animations.Length > 0;

        /// <summary>Gets every animation the game's table names.</summary>
        public IEnumerable<string> AnimationNames => this.animations.OfType<string>();

        /// <summary>Loads the catalog, if it is there; the compiled names stand without it.</summary>
        public static void Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Current = Parse(File.ReadLines(path));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A locked file leaves the compiled names; it must not stop the mod from starting.
            }
        }

        public static GameIds Parse(IEnumerable<string> lines)
        {
            var animations = new SortedDictionary<int, string>();
            var stats = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var line in lines.Where(line => line.Length > 0 && line[0] != Comment))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3)
                {
                    continue;
                }

                if (parts[0] == AnimationLine && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var row))
                {
                    animations[row] = parts[2];
                }
                else if (parts[0] == StatLine && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var key))
                {
                    stats[parts[1]] = key;
                }
            }

            var names = new string?[animations.Count == 0 ? 0 : animations.Keys.Last() + 1];
            foreach (var (row, name) in animations)
            {
                names[row] = name;
            }

            return new GameIds(names, stats);
        }

        /// <summary>The animation's name in the game's table; null beyond it, as a patch's new rows are until the next extraction.</summary>
        public string? Animation(int id) => id >= 0 && id < this.animations.Length ? this.animations[id] : null;

        /// <summary>The key of a stat, by its name in the game; <paramref name="compiled"/> when the catalog does not list it.</summary>
        public int Stat(string name, int compiled) => this.stats.GetValueOrDefault(name, compiled);
    }
}
