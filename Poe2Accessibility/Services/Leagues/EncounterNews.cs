namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     What is news on the HUD's league encounter tracker (<see cref="EncounterReader"/>), whose lines were only said when the
    ///     player asked for the vitals: a line of a new kind is the encounter beginning ("Encuentro de expedición", ClientStrings
    ///     SideEncounter*Title), and a count out of a total is said as it steps ("Explosivos colocados: 2/5",
    ///     SideEncounterExpeditionPlacedCount, the one place the game tells how many charges there are). A bare number running on
    ///     (the Ritual's tribute) is not news: it stays in the vitals. Pure.
    /// </summary>
    public sealed class EncounterNews
    {
        /// <summary>A count is said once it has stood this long: several steps at once are one line.</summary>
        public const int SettleMs = 700;

        private static readonly Regex Fraction = new(@"\d+\s*/\s*\d+", RegexOptions.CultureInvariant);
        private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);

        private readonly Dictionary<string, (string Text, long At)> pending = new(StringComparer.Ordinal);
        private readonly HashSet<string> titlesSaid = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> counts = new(StringComparer.Ordinal);
        private Dictionary<string, string>? shown;

        /// <summary>Gets a value indicating whether the tracker shows a count out of a total now: the game's own word on it.</summary>
        public bool ShowsCount { get; private set; }

        /// <summary>A new area: its tracker has not been looked at.</summary>
        public void Reset()
        {
            this.pending.Clear();
            this.titlesSaid.Clear();
            this.counts.Clear();
            (this.shown, this.ShowsCount) = (null, false);
        }

        /// <summary>The lines to say now; what the tracker shows when first looked at in an area is not news.</summary>
        public List<string> Heard(IReadOnlyList<string> lines, long now)
        {
            var current = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                current[Digits.Replace(line, "#")] = line;
            }

            this.ShowsCount = lines.Any(line => Fraction.IsMatch(line));
            var news = new List<string>();
            if (this.shown == null)
            {
                this.shown = current;
                this.titlesSaid.UnionWith(current.Keys); // a menu hiding the HUD and showing it again makes no title new
                foreach (var (kind, text) in current)
                {
                    this.counts[kind] = text;
                }

                return news;
            }

            foreach (var (kind, text) in current)
            {
                if (!this.shown.ContainsKey(kind) && !Digits.IsMatch(text))
                {
                    if (this.titlesSaid.Add(kind))
                    {
                        news.Add(text);
                    }
                }
                else if (Fraction.IsMatch(text))
                {
                    // Against the count last said, not the one last shown: the HUD hidden by a menu comes back with the same count.
                    if (this.counts.TryGetValue(kind, out var last) && last == text)
                    {
                        this.pending.Remove(kind);
                    }
                    else if (!this.pending.TryGetValue(kind, out var waiting) || waiting.Text != text)
                    {
                        this.pending[kind] = (text, now);
                    }
                }
            }

            foreach (var kind in this.pending.Keys.ToList())
            {
                if (!current.ContainsKey(kind))
                {
                    this.pending.Remove(kind); // the tracker closed before the count stood still
                }
                else if (now - this.pending[kind].At >= SettleMs)
                {
                    news.Add(this.pending[kind].Text);
                    this.counts[kind] = this.pending[kind].Text;
                    this.pending.Remove(kind);
                }
            }

            this.shown = current;
            return news;
        }
    }
}
