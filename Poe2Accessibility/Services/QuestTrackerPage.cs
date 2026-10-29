namespace Poe2Accessibility.Services
{
    /// <summary>The quest tracker's shown page, said whenever it is not the page last said, across areas. Pure.</summary>
    public sealed class QuestTrackerPage
    {
        private string lastSaid = string.Empty;

        /// <summary>The page to say, or empty when the tracker shows none or shows the page last said.</summary>
        public string News(string page)
        {
            if (page.Length == 0 || page == this.lastSaid)
            {
                return string.Empty;
            }

            this.lastSaid = page;
            return page;
        }
    }
}
