namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Where each character's cursor last was on the passive tree, by node id, kept in a file across sessions: the game opens
    ///     the tree at the class's start, and a blind player cannot see where they had got to (user 2026-11-10). One line per
    ///     character, "name, tab, node id"; a file that cannot be written leaves the place for this session only.
    /// </summary>
    public sealed class PassiveResume
    {
        private readonly string? path;
        private readonly Dictionary<string, string> places = new(StringComparer.Ordinal);

        /// <param name="path">The file; null keeps the places in memory only.</param>
        public PassiveResume(string? path)
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
                    if (line.Split('\t') is [{ Length: > 0 } character, { Length: > 0 } id])
                    {
                        this.places[character] = id;
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable: start without places.
            }
            catch (UnauthorizedAccessException)
            {
                // The same.
            }
        }

        /// <returns>The id of the node the character's cursor was last on; null when none is known.</returns>
        public string? Get(string character) => this.places.GetValueOrDefault(character);

        /// <summary>Notes the node the character's cursor is on, written to the file when it changed.</summary>
        public void Set(string character, string id)
        {
            if (this.places.GetValueOrDefault(character) == id)
            {
                return;
            }

            this.places[character] = id;
            if (this.path == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
                File.WriteAllLines(this.path, this.places.Select(place => $"{place.Key}\t{place.Value}"));
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
