namespace Poe2Accessibility.Services
{
    using System;
    using System.Text;

    /// <summary>A readable name from an entity's metadata file name, for objects the game's tables do not name.</summary>
    public static class ObjectNames
    {
        private const char VariantMark = '@';
        private const char FolderSeparator = '/';
        private const string LeagueFolder = "/Leagues/";
        private const string DoodadWord = "Doodad ";

        /// <summary>"Metadata/Terrain/RitualAltar_Small@12" becomes "Ritual Altar Small"; empty when the path has no file name.</summary>
        public static string FromPath(string path)
        {
            var end = path.IndexOf(VariantMark);
            var file = end < 0 ? path : path[..end];
            file = file[(file.LastIndexOf(FolderSeparator) + 1)..];
            var text = new StringBuilder(file.Length + 4);
            for (var i = 0; i < file.Length; i++)
            {
                var c = file[i];
                if (c is '_' or '-' or ' ')
                {
                    AppendSpace(text);
                    continue;
                }

                var startsWord = i > 0 && char.IsUpper(c) && (char.IsLower(file[i - 1]) || char.IsDigit(file[i - 1])
                    || (char.IsUpper(file[i - 1]) && i + 1 < file.Length && char.IsLower(file[i + 1])));
                if (startsWord)
                {
                    AppendSpace(text);
                }

                text.Append(c);
            }

            // "Doodad" is the engine's word for set dressing, not a player's: the town chair one can sit on is a "Chair" (2026-11-08).
            var name = text.ToString().Trim();
            return name.StartsWith(DoodadWord, StringComparison.Ordinal) && name.Length > DoodadWord.Length ? name[DoodadWord.Length..] : name;
        }

        /// <summary>
        ///     What is said for a name made from a file: in English the file's words are a fair name; in another language they
        ///     would be foreign words posing as one, so the object is said unnamed with the word kept to tell it apart. A file
        ///     the mod has its own word for (<paramref name="known"/>) is said by that word in every language.
        /// </summary>
        public static string Spoken(string fileWord, string language, Func<string, string> unnamed, Func<string, string?>? known = null) =>
            fileWord.Length == 0 ? fileWord : known?.Invoke(fileWord) is { Length: > 0 } own ? own : language == "en" ? fileWord : unnamed(fileWord);

        /// <summary>The game's label, else the league folder of the path ("Metadata/Terrain/Leagues/Delirium/..." gives "Delirium"), else empty.</summary>
        public static string LeagueName(string label, string path)
        {
            if (label.Length > 0)
            {
                return label;
            }

            var at = path.IndexOf(LeagueFolder, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return string.Empty;
            }

            var rest = path[(at + LeagueFolder.Length)..];
            var slash = rest.IndexOf(FolderSeparator);
            return slash > 0 ? FromPath(rest[..slash]) : string.Empty;
        }

        private static void AppendSpace(StringBuilder text)
        {
            if (text.Length > 0 && text[^1] != ' ')
            {
                text.Append(' ');
            }
        }
    }
}
