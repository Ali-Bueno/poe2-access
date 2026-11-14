namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>The quest tracker's shown page, said whenever it is not the page last said, across areas. Pure.</summary>
    public sealed class QuestTrackerPage
    {
        private static readonly Regex PageNumber = new(@"(\d+)/\d+", RegexOptions.Compiled);

        private IReadOnlyList<string> lastSaid = Array.Empty<string>();
        private string shownPage = string.Empty;
        private bool turning;

        /// <summary>Quests a newcomer pushed off the page: back once it leaves, they are the page closing up, not news.</summary>
        private readonly HashSet<string> pushedOff = new();

        /// <summary>The news last given is a page the player turned to: its number on the tracker's legend changed.</summary>
        public bool Turned { get; private set; }

        /// <summary>A new page waits: for the menu over the game to close, or for the area's own line.</summary>
        public bool Waiting { get; private set; }

        /// <summary>Another character: its quests are news even where they read as the last one's did.</summary>
        public void Reset()
        {
            (this.lastSaid, this.shownPage, this.turning) = (Array.Empty<string>(), string.Empty, false);
            (this.Turned, this.Waiting) = (false, false);
            this.pushedOff.Clear();
        }

        /// <summary>
        ///     What to say of the page, or empty when the tracker shows none or the page last said. A page that keeps a quest of
        ///     the last one says only the rest: every page opens with the quest in hand, and one changed objective read the
        ///     others with it (user 2026-11-07). A quest that only comes back to the page it was pushed off, the newcomer
        ///     gone, is not said again: each room of a Trial done read the quest behind the Trial's (user 2026-11-14). In a
        ///     menu it waits: said whole, it keeps the menu's own speech back (docs/menu-speech-rules.md).
        /// </summary>
        /// <param name="quests">Each shown quest, title and objectives, as it would be said.</param>
        /// <param name="waits">A menu covers the game, or the area just entered is still to be named: the page comes after.</param>
        /// <param name="legend">The tracker's page legend: it is read after the pages, so a turn can show there first.</param>
        public string News(IReadOnlyList<string> quests, bool waits = false, string legend = "")
        {
            var numbered = this.NoteLegend(legend);
            this.Turned = false;
            if (quests.Count == 0 && this.turning)
            {
                this.lastSaid = Array.Empty<string>(); // turned to a page with nothing on it: the one turned back to is said again
            }

            var fresh = quests.Count > 0 && !quests.SequenceEqual(this.lastSaid);
            this.Waiting = fresh && waits;
            if (!fresh || waits)
            {
                return string.Empty;
            }

            var changed = quests.Except(this.lastSaid).ToList();
            if (this.turning || !numbered)
            {
                this.pushedOff.Clear(); // another page, or no page number to tell a turn by
            }
            else
            {
                var back = changed.Where(this.pushedOff.Contains).ToList();
                changed = changed.Except(back).ToList();
                this.pushedOff.ExceptWith(back);
                if (changed.Count > 0)
                {
                    this.pushedOff.UnionWith(this.lastSaid.Except(quests));
                }
            }

            var rest = changed.Count < quests.Count && (changed.Count > 0 || !this.turning);
            this.lastSaid = quests;
            var news = Announcements.Sentences(rest ? changed : quests);
            this.Turned = this.turning && news.Length > 0;
            this.turning &= news.Length == 0;
            return news;
        }

        /// <summary>Notes a turn of the page by its number on the legend; false when the legend shows none.</summary>
        private bool NoteLegend(string legend)
        {
            if (PageNumber.Match(legend) is not { Success: true } number)
            {
                return false;
            }

            this.turning |= this.shownPage.Length > 0 && number.Groups[1].Value != this.shownPage;
            this.shownPage = number.Groups[1].Value;
            return true;
        }
    }
}
