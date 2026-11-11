namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The name the game gave a rare or unique item, made from the Words.dat rows its Mods component keeps
    ///     (<see cref="GameLayout.ItemNameWords"/>): what its tooltip's title shows, known without the tooltip (a ground
    ///     label, a move of an item the cursor was never on). A unique has one row, its name; a rare has two, and each
    ///     language writes the space between them into the word that goes second ("Chimeric" + " Edge" in English,
    ///     "Filo" + " quimérico" in Spanish), so the order is read from the words and no language is assumed. Pure.
    /// </summary>
    public static class ItemNameWords
    {
        /// <summary>One row: its text in the client's language and the gender and number it gives the other word ("FP"), often empty.</summary>
        public readonly record struct Word(string Text, string Inflection);

        /// <summary>One branch of a gendered text: "&lt;if:MS&gt;{ quimérico}", "&lt;elif:FS&gt;{ quimérica}".</summary>
        private static readonly Regex Branch = new(@"<(?:if|elif):([^>]*)>\{([^}]*)\}", RegexOptions.Compiled);

        /// <summary>A marker that sets the gender for the words after it and shows nothing ("&lt;&lt;set:MS&gt;&gt;").</summary>
        private static readonly Regex Marker = new(@"<<[^<>]*>>", RegexOptions.Compiled);

        /// <summary>
        ///     The item's name, or empty when the words do not say it for certain: more than two, an empty one, markup
        ///     left over, or two words of which not exactly one begins with the space (a language written without
        ///     spaces: the order is not known, and the caller keeps the base name).
        /// </summary>
        public static string Compose(IReadOnlyList<Word> words)
        {
            if (words.Count is 0 or > 2)
            {
                return string.Empty;
            }

            // The word that carries an inflection is the noun; the other one's branches follow it.
            // A row gives the gender alone for a singular noun ("F" on "Estrella", "Banda", "Zarpa") and both letters
            // for a plural ("FP" on "Cuentas"); the branches are keyed by both ("FS"): "Estrella quimérica".
            var gender = words.Select(word => word.Inflection).FirstOrDefault(inflection => inflection.Length > 0) ?? string.Empty;
            gender = gender.Length == 1 ? gender + "S" : gender;
            var pieces = words.Select(word => Resolve(word.Text, gender)).ToList();
            if (pieces.Any(piece => piece.Trim().Length == 0 || piece.IndexOfAny(new[] { '<', '>', '{', '}' }) >= 0))
            {
                return string.Empty;
            }

            if (pieces.Count == 1)
            {
                return pieces[0].Trim();
            }

            var (first, second) = (pieces[0], pieces[1]);
            // The space may be a no-break one ("\u00A0de kraken"): it stays in the name as the game writes it.
            var (firstSpaced, secondSpaced) = (char.IsWhiteSpace(first[0]), char.IsWhiteSpace(second[0]));
            return firstSpaced == secondSpaced ? string.Empty
                : secondSpaced ? first + second
                : second + first;
        }

        /// <summary>The branch for a gender, the first branch when none is for it (the game's own default), the text itself when it has none.</summary>
        public static string Resolve(string text, string gender)
        {
            text = Marker.Replace(text, string.Empty);
            var branches = Branch.Matches(text);
            if (branches.Count == 0)
            {
                return text;
            }

            var chosen = branches.FirstOrDefault(branch => branch.Groups[1].Value.Equals(gender, StringComparison.OrdinalIgnoreCase)) ?? branches[0];
            return chosen.Groups[2].Value;
        }
    }

    /// <summary>A game restarted patched while the mod kept running: the mod's game data was made for the build before. Pure.</summary>
    public static class GameBuildChange
    {
        /// <summary>Whether a game process's exe is another than the last one's under this mod; the first process is no change.</summary>
        public static bool Patched(string lastBuild, string build) => lastBuild.Length > 0 && build.Length > 0 && lastBuild != build;
    }

    /// <summary>
    ///     Whether the names made from an item's words are believed: every tooltip title read is compared with them, and
    ///     they are given up once the titles disagree twice with nothing in their favour (a patch moved the rows). Pure.
    /// </summary>
    public sealed class ItemNameTrust
    {
        /// <summary>Disagreements with no agreement that end the trust.</summary>
        public const int LostAfter = 2;

        public int Agreed { get; private set; }

        public int Differed { get; private set; }

        /// <summary>Gets a value indicating whether the names are not used any more.</summary>
        public bool Lost => this.Differed >= LostAfter && this.Agreed < this.Differed;

        /// <returns>Whether this observation changed <see cref="Lost"/>.</returns>
        public bool Observe(string fromWords, string title)
        {
            var before = this.Lost;
            if (fromWords.Length == 0 || title.Length == 0)
            {
                return false;
            }

            if (string.Equals(fromWords, title.Trim(), StringComparison.Ordinal))
            {
                this.Agreed++;
            }
            else
            {
                this.Differed++;
            }

            return this.Lost != before;
        }
    }
}
