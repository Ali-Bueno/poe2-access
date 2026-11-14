namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Pure: the texts of a screen the player asked to have read (legends aside), numbers kept where a word labels them.</summary>
    public static class ScreenReadout
    {
        /// <summary>The longest text that counts as a label for a number beside it; a longer one is a paragraph.</summary>
        private const int MaxLabelChars = 40;

        /// <summary>Longest one-word label drawn after its number ("11" then "DEX").</summary>
        private const int MaxTrailingLabelChars = 12;

        /// <summary>A text of digits and their separators only: "6", "25%", "0:08".</summary>
        public static bool IsNumber(string text) => text.All(c => char.IsDigit(c) || c is '.' or ',' or ':' or '%' or '/' or '+' or '-' or ' ');

        public static List<ScreenText> Texts(IEnumerable<ScreenText> texts, System.Func<ScreenText, bool> isLegend)
        {
            var list = texts.Where(text => !isLegend(text)).ToList();
            var result = new List<ScreenText>(list.Count);
            var labelOpen = false; // the last text kept is a label that has no number yet
            for (var i = 0; i < list.Count; i++)
            {
                var text = list[i];
                if (!IsNumber(text.Text))
                {
                    result.Add(text);
                    labelOpen = IsLabel(text.Text);
                    continue;
                }

                var number = text.Text.Trim();
                if (!number.Any(char.IsDigit))
                {
                    continue;
                }

                if (labelOpen)
                {
                    result[^1] = result[^1] with { Text = Joined(result[^1].Text, number) };
                    labelOpen = false;
                }
                else if (i + 1 < list.Count && list[i + 1].Text.Length is > 0 and <= MaxTrailingLabelChars && list[i + 1].Text.All(char.IsLetter))
                {
                    result.Add(list[i + 1] with { Text = Joined(list[i + 1].Text, number) });
                    i++;
                }
            }

            return result;
        }

        private static string Joined(string label, string number) => $"{label.TrimEnd().TrimEnd(':')}: {number}";

        private static bool IsLabel(string text) => text.Length <= MaxLabelChars && text.Any(char.IsLetter) && !text.TrimEnd().EndsWith('.');
    }
}
