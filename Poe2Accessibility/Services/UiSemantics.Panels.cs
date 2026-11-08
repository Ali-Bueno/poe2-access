namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Widgets of the panels not reached in play yet (docs/research/ui-unseen-panels.md): a control that shows only an
    ///     item's picture (the Currency Exchange's "I want" / "I have" pickers once a currency is chosen, a bench's input).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The picture of an icon-only control sits on it or one or two levels below (frame → icon).</summary>
        private const int ArtSearchDepth = 2;

        /// <summary>At most this many pictures are looked at under one control.</summary>
        private const int MaxArtKeys = 8;

        /// <summary>The item an icon-only control shows, named by the game's tables; empty when it shows none they name.</summary>
        private string ArtCaption(in UiElementReader.ElementInfo info)
        {
            var keys = new List<string>();
            this.CollectArt(info, ArtSearchDepth, keys);
            return ArtCaptions.Pick(keys);
        }

        private void CollectArt(in UiElementReader.ElementInfo node, int depth, List<string> keys)
        {
            if (keys.Count >= MaxArtKeys)
            {
                return;
            }

            if (this.elements.ReadTextureKey(node) is { } key)
            {
                keys.Add(key);
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var child in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(child, out var info) && info.IsVisibleFlag)
                {
                    this.CollectArt(info, depth - 1, keys);
                }
            }
        }
    }
}
