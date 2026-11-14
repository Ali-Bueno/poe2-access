namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Which of the Trial's lines on screen are news: one that appears, or whose words change, is said; a clock or a count
    ///     running on is not, nor a banner that blinks back within a few seconds. Pure.
    /// </summary>
    public sealed class ChaosTrialHudNews
    {
        /// <summary>A line gone and back within this is the same line still: a blinking banner is said once.</summary>
        public const int BlinkMs = 5000;

        private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);
        private static readonly Regex Letters = new(@"\p{L}", RegexOptions.CultureInvariant);

        private readonly Dictionary<long, (string Words, long Seen)> known = new();
        private bool looked;

        /// <summary>A new area: its screen has not been looked at.</summary>
        public void Reset()
        {
            this.known.Clear();
            this.looked = false;
        }

        /// <summary>
        ///     The texts to say now, each element by its own id (two labels of one name are two lines); what stands when first
        ///     looked at in an area is not news.
        /// </summary>
        public List<string> Heard(IReadOnlyList<(long Id, string Text)> shown, long now)
        {
            var news = new List<string>();
            foreach (var (id, text) in shown)
            {
                var words = Digits.Replace(text, "#");
                var had = this.known.TryGetValue(id, out var before);
                // Said: a line with words in it that is new, changed in its words, or back after being gone a while.
                if (this.looked && Letters.IsMatch(text) && (!had || before.Words != words || now - before.Seen > BlinkMs) && !news.Contains(text))
                {
                    news.Add(text);
                }

                this.known[id] = (words, now);
            }

            this.looked = true;
            return news;
        }
    }
}
