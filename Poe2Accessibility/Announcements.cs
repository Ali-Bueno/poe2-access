namespace Poe2Accessibility
{
    using System;
    using System.Linq;

    /// <summary>
    ///     Every spoken string lives here (single translatable layer; no text inside services or hooks).
    /// </summary>
    public static partial class Announcements
    {
        private static string T(string en, string es) => Localization.Language switch
        {
            "en" => en,
            "es" => es,
            _ => Localization.Translate(en) ?? en,
        };

        /// <summary>Interpolated phrases: other languages look the English composite format up ("Life {0} percent").</summary>
        private static string T(Phrase en, Phrase es) => Localization.Language switch
        {
            "en" => en.Text(),
            "es" => es.Text(),
            _ => en.Text(Localization.Translate(en.Format)),
        };

        /// <summary>
        ///     A caption the game keeps in lower case and draws in small capitals ("reanudar juego" in the game menu,
        ///     "confirmar A" with its button, 0.5.5e) gets its first letter raised, so it reads as a name on a braille
        ///     display; a text with capitals in its words ("kehgan, Nivel 2 Monje") stays as written.
        /// </summary>
        public static string Capitalised(string text) =>
            text.Length > 0 && char.IsLower(text[0]) && !text.Split(' ').Any(word => word.Length > 1 && word.Any(char.IsUpper))
                ? char.ToUpperInvariant(text[0]) + text[1..]
                : text;

        /// <summary>"100%" or "(0/1)" still need a full stop; these already end a sentence or lead into the next.</summary>
        private const string SentenceEnds = ".!?:;";

        /// <summary>
        ///     What the game writes under an object's title, a line a sentence: "Restaura algo de honor. Obtienes una desventaja
        ///     menor aleatoria". An amount the game fills in as it draws the box ("Restaura {0} de honor") is not known here.
        /// </summary>
        public static string ObjectDescription(string body)
        {
            var sentences = new System.Collections.Generic.List<string>();
            foreach (var raw in body.Split('\n'))
            {
                var line = AmountHole.Replace(raw, T("some", "algo")).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                // A line that goes on in lower case is its sentence wrapped ("Interactúa para participar en / la Prueba de las sekhemas").
                if (sentences.Count > 0 && char.IsLower(line[0]))
                {
                    sentences[^1] += " " + line;
                }
                else
                {
                    sentences.Add(line);
                }
            }

            return Sentences(sentences);
        }

        private static readonly System.Text.RegularExpressions.Regex AmountHole = new(@"\{\d*\}", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Screen texts read in a row: each one is its own sentence unless it already ends in punctuation; empty ones are skipped.</summary>
        public static string Sentences(System.Collections.Generic.IEnumerable<string> parts)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0)
                {
                    continue; // "artesano. . Vacío" when nothing stood between the bench and the focus (2026-11-02)
                }

                if (builder.Length > 0)
                {
                    builder.Append(SentenceEnds.Contains(builder[^1]) ? " " : ". ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        /// <summary>Parts of one announcement: comma-separated, but a part that ends a sentence is not followed by a comma.</summary>
        private static string Clauses(System.Collections.Generic.IEnumerable<string> parts)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append(SentenceEnds.Contains(builder[^1]) ? " " : ", ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        /// <summary>Cells of one row or entry: a caption ending in ':' runs into its value ("LVL: 1, Punch").</summary>
        public static string Cells(System.Collections.Generic.IEnumerable<string> cells)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var cell in cells)
            {
                if (builder.Length > 0)
                {
                    builder.Append(builder[^1] == ':' || SentenceEnds.Contains(builder[^1]) ? " " : ", ");
                }

                builder.Append(cell);
            }

            return builder.ToString();
        }

        /// <summary>A label wrapped over several lines by the layout (the world map legend) read as one line.</summary>
        public static string Flatten(string text) =>
            SpaceRuns.Replace(text.Contains('\n') ? JoinLines(text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : text, " ");

        /// <summary>The word that stands for the game's ">" between a value and its change ("Nivel 4 a nivel 5").</summary>
        public static string ChangeWord => T("to", "a");

        /// <summary>Where inline art was cut out of a text ("Identify <<itemvisual:…>> x 1" reads "Identify   x 1").</summary>
        private static readonly System.Text.RegularExpressions.Regex SpaceRuns = new(" {2,}", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        ///     Lines of one text: a line starting in lower case continues the previous one (layout wrapping, "Socket
        ///     a Support Gem of any / colour to …"); any other starts a new sentence ("1 Strength = +2 Life / 5 Strength …").
        /// </summary>
        public static string JoinLines(System.Collections.Generic.IEnumerable<string> lines)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                if (builder.Length > 0)
                {
                    var continues = char.IsLower(line[0]) || SentenceEnds.Contains(builder[^1]) || EndsInLinkWord(builder);
                    builder.Append(continues ? " " : ". ");
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        /// <summary>
        ///     A line ending in a short lower-case word wraps mid-phrase even when the next starts in capitals: "Ocultar
        ///     las pestañas de" / "Solo retirar" (a tab type's name) is one option label.
        /// </summary>
        private static bool EndsInLinkWord(System.Text.StringBuilder builder)
        {
            var end = builder.Length;
            var start = end;
            while (start > 0 && char.IsLetter(builder[start - 1]))
            {
                start--;
            }

            return end - start is > 0 and <= MaxLinkWordLength && char.IsLower(builder[start]) &&
                   (start == 0 || builder[start - 1] == ' ');
        }

        /// <summary>"de", "del", "la", "of", "the", "to": the length of the words a phrase cannot end on.</summary>
        private const int MaxLinkWordLength = 3;

        /// <summary>A text too long to say at once: how many paragraphs it has and how to read them.</summary>
        public static string LongText(int paragraphs) => paragraphs == 1
            ? T("Long text, 1 paragraph: scroll it with the stick to read it", "Texto largo, 1 párrafo: desplázalo con el stick para leerlo")
            : T(
                $"Long text, {paragraphs} paragraphs: scroll it with the stick to read it",
                $"Texto largo, {paragraphs} párrafos: desplázalo con el stick para leerlo");

        /// <summary>One paragraph of a long text, after its place in it.</summary>
        public static string LongTextParagraph(string paragraph, int number, int total) => T($"{number} of {total}. {paragraph}", $"{number} de {total}. {paragraph}");

        /// <summary>Numbered tabs ("-1-" vendor pages, "1" default stash tabs) read "tab 1"; named tabs keep their name.</summary>
        public static string TabTitle(string caption)
        {
            var number = caption.Trim('-', ' ');
            return number.Length > 0 && number.All(char.IsDigit) ? T($"tab {number}", $"pestaña {number}") : caption;
        }
    }
}
