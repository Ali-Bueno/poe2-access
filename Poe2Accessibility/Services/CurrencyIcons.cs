namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Names the currency an icon shows: prices and the gold counter are an item image beside a number.</summary>
    public static class CurrencyIcons
    {
        /// <summary>Spoken unit for a currency icon's art key; null for other images and currencies not named yet.</summary>
        public static string? FromTextureKey(string key)
        {
            if (!key.StartsWith(GameLayout.Image.CurrencyArtPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            return key.Contains(GameLayout.Image.GoldArtMarker, StringComparison.Ordinal) ? Announcements.Gold : null;
        }
    }
}
