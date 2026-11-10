namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>When a trade's other side is news and how it reads. Pure.</summary>
    public static class TradeOfferNews
    {
        // The window's body as measured: the other side's grid, the player's grid, then each one's title in the same order.
        private const int TitleAfterGrid = 2;
        private const int Sides2 = 2;

        /// <summary>
        ///     Which children of a trade window's body are the other side's grid and each side's title, the player's grid being
        ///     child <paramref name="mine"/>: where measured (grid, grid, title, title) when that still fits; else, should a patch
        ///     reorder the body, the only other grid, the two only texts being the titles in the grids' order. Null when the
        ///     body is no such window.
        /// </summary>
        public static (int Theirs, int TheirTitle, int OwnTitle)? Sides(IReadOnlyList<(bool Grid, bool Text)> parts, int mine)
        {
            if (mine < 0 || mine >= parts.Count)
            {
                return null;
            }

            if (mine < TitleAfterGrid && parts.Count >= 2 * TitleAfterGrid)
            {
                var other = TitleAfterGrid - 1 - mine;
                if (parts[other + TitleAfterGrid].Text && parts[mine + TitleAfterGrid].Text)
                {
                    return (other, other + TitleAfterGrid, mine + TitleAfterGrid);
                }
            }

            var grids = new List<int>();
            var texts = new List<int>();
            for (var index = 0; index < parts.Count; index++)
            {
                (parts[index].Grid ? grids : parts[index].Text ? texts : null)?.Add(index);
            }

            if (grids.Count != Sides2 || texts.Count != Sides2 || !grids.Contains(mine))
            {
                return null;
            }

            var own = grids.IndexOf(mine);
            return (grids[1 - own], texts[1 - own], texts[own]);
        }

        /// <summary>The line for an offer that changed; null when it did not. <paramref name="said"/> is null for a new window.</summary>
        public static string? Line(string title, IReadOnlyList<string> items, string nothing, ref string? said)
        {
            var offer = string.Join(", ", items);
            if (offer == said || (said == null && offer.Length == 0))
            {
                said = offer;
                return null;
            }

            said = offer;
            return $"{title}: {(offer.Length == 0 ? nothing : offer)}";
        }
    }
}
