namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>When a trade's other side is news and how it reads. Pure.</summary>
    public static class TradeOfferNews
    {
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
