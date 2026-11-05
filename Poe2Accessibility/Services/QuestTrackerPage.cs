namespace Poe2Accessibility.Services
{
    /// <summary>The quest tracker's shown page, said whenever it is not the page last said, across areas. Pure.</summary>
    public sealed class QuestTrackerPage
    {
        private string lastSaid = string.Empty;

        /// <summary>A new page waits for the menu over the game to close.</summary>
        public bool Waiting { get; private set; }

        /// <summary>
        ///     The page to say, or empty when the tracker shows none or the page last said. In a menu it waits: said whole, it
        ///     keeps the menu's own speech back (docs/menu-speech-rules.md).
        /// </summary>
        public string News(string page, bool inMenu = false)
        {
            var fresh = page.Length > 0 && page != this.lastSaid;
            this.Waiting = fresh && inMenu;
            if (!fresh || inMenu)
            {
                return string.Empty;
            }

            this.lastSaid = page;
            return page;
        }
    }
}
