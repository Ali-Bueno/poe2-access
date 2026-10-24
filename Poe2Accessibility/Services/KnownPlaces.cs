namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    ///     The exits and waypoints each character has been told about, kept across sessions in a text file of
    ///     "character, tab, place key" lines, appended as places are found: the camp's waypoint was "new" again after
    ///     every host restart (2026-10-24).
    /// </summary>
    public sealed class KnownPlaces
    {
        private const char Separator = '\t';

        private readonly string path;
        private readonly Dictionary<string, HashSet<string>> byCharacter = new(StringComparer.Ordinal);

        public KnownPlaces(string path)
        {
            this.path = path;
            try
            {
                foreach (var line in File.Exists(path) ? File.ReadLines(path) : Array.Empty<string>())
                {
                    var split = line.IndexOf(Separator);
                    if (split > 0)
                    {
                        this.Of(line[..split]).Add(line[(split + 1)..]);
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable: places are new again this session.
            }
        }

        /// <summary>True the first time <paramref name="character"/> meets this place; it is remembered from then on.</summary>
        public bool Add(string character, string key)
        {
            if (!this.Of(character).Add(key))
            {
                return false;
            }

            if (character.Length > 0)
            {
                try
                {
                    File.AppendAllText(this.path, $"{character}{Separator}{key}{Environment.NewLine}");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Not written: known for this session only.
                }
            }

            return true;
        }

        private HashSet<string> Of(string character)
        {
            if (!this.byCharacter.TryGetValue(character, out var places))
            {
                this.byCharacter[character] = places = new HashSet<string>(StringComparer.Ordinal);
            }

            return places;
        }
    }
}
