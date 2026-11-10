namespace Poe2Accessibility.Services
{
    using System.Linq;

    /// <summary>Whose skill names are said.</summary>
    public enum SkillNameSource
    {
        /// <summary>Too few skills to tell: the game's name is said when it is text.</summary>
        Unjudged,

        /// <summary>The game's read agrees with the files on most skills: where it differs, it is the game's own display name.</summary>
        Game,

        /// <summary>The game's read disagrees with the files on most skills at once: its offsets moved, the files' names are said.</summary>
        Files,
    }

    /// <summary>
    ///     Judges a skill's name read from the game against the game's files (<see cref="GemCatalog"/>). The game's name is
    ///     what the player sees, so it is said whenever it is text; the files stand in when it is not, and for every skill
    ///     once the game's read disagrees with them on most of the character's skills in two readings running, which only a
    ///     moved offset does. Pure.
    /// </summary>
    public sealed class SkillNameCheck
    {
        /// <summary>Fewer skills with a name in the files judge nothing: one renamed skill of two is not "most".</summary>
        public const int MinJudged = 3;

        private bool lastDisagreed;

        /// <summary>Gets whose names are said now.</summary>
        public SkillNameSource Source { get; private set; } = SkillNameSource.Unjudged;

        /// <summary>A name the game could show: some letter or digit and no control character (a moved offset reads other bytes).</summary>
        public static bool IsText(string? name) =>
            !string.IsNullOrWhiteSpace(name) && name.Any(char.IsLetterOrDigit) && !name.Any(character => char.IsControl(character) || character == '�');

        /// <summary>The name to say: the game's when it is text and its read is not judged moved, else the files', else nothing.</summary>
        public static string Choose(string? fromGame, string? fromFiles, SkillNameSource source) =>
            source == SkillNameSource.Files && !string.IsNullOrEmpty(fromFiles) ? fromFiles
            : IsText(fromGame) ? fromGame!
            : fromFiles ?? string.Empty;

        /// <summary>What one reading of all the character's skills says: <paramref name="compared"/> have a name in the files.</summary>
        public static SkillNameSource Judge(int compared, int disagreeing) =>
            compared < MinJudged ? SkillNameSource.Unjudged
            : disagreeing * 2 > compared ? SkillNameSource.Files
            : SkillNameSource.Game;

        /// <summary>
        ///     Takes one reading; true when <see cref="Source"/> changed. The files win only on the second reading in a row
        ///     that disagrees on most skills; one reading that agrees on most gives the game its names back.
        /// </summary>
        public bool Observe(int compared, int disagreeing)
        {
            var now = Judge(compared, disagreeing);
            if (now == SkillNameSource.Unjudged)
            {
                return false; // a character with two skills says nothing either way
            }

            var confirmed = now == SkillNameSource.Files && !this.lastDisagreed ? this.Source : now;
            this.lastDisagreed = now == SkillNameSource.Files;
            if (confirmed == this.Source)
            {
                return false;
            }

            this.Source = confirmed;
            return true;
        }

        /// <summary>A new game process: judged again.</summary>
        public void Reset() => (this.Source, this.lastDisagreed) = (SkillNameSource.Unjudged, false);
    }
}
