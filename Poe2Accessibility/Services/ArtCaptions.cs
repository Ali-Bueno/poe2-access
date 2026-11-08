namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Pure: which of an icon-only control's pictures names it (`UiSemantics.ArtCaption`).</summary>
    public static class ArtCaptions
    {
        /// <summary>
        ///     The first picture that is an item the game's tables name (currency art: "Exalted Orb") or an Atlas content
        ///     (the breach icon: "Fisura", catalog/endgame.json); else empty.
        /// </summary>
        public static string Pick(IEnumerable<string> textureKeys) =>
            textureKeys.Select(key => CurrencyIcons.FromTextureKey(key) is { Length: > 0 } item ? item : EndgameCatalog.Shared.IconName(key, Localization.Language))
                .FirstOrDefault(name => name is { Length: > 0 }) ?? string.Empty;
    }
}
