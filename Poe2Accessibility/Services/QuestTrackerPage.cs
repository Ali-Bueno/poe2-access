namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The quest tracker's shown page, said whenever it is not the page last said, across areas. Pure.</summary>
    public sealed class QuestTrackerPage
    {
        private IReadOnlyList<string> lastSaid = Array.Empty<string>();

        /// <summary>A new page waits for the menu over the game to close.</summary>
        public bool Waiting { get; private set; }

        /// <summary>
        ///     What to say of the page, or empty when the tracker shows none or the page last said. A page that keeps a quest of
        ///     the last one says only the rest: every page opens with the quest in hand, and one changed objective read the
        ///     others with it (user 2026-11-07). In a menu it waits: said whole, it keeps the menu's own speech back
        ///     (docs/menu-speech-rules.md).
        /// </summary>
        /// <param name="quests">Each shown quest, title and objectives, as it would be said.</param>
        /// <param name="inMenu">A menu covers the game.</param>
        public string News(IReadOnlyList<string> quests, bool inMenu = false)
        {
            var fresh = quests.Count > 0 && !quests.SequenceEqual(this.lastSaid);
            this.Waiting = fresh && inMenu;
            if (!fresh || inMenu)
            {
                return string.Empty;
            }

            var changed = quests.Except(this.lastSaid).ToList();
            var keepsOne = changed.Count < quests.Count;
            this.lastSaid = quests;
            return Announcements.Sentences(keepsOne ? changed : quests);
        }
    }
}
