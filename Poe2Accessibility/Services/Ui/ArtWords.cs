namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The words of a picture's file name, for a control or icon nothing else names (docs/research/ui-unseen-panels.md).</summary>
    public static class ArtWords
    {
        /// <summary>The stem of a picture and the words it gives.</summary>
        public readonly record struct ArtName(string Stem, string Words);

        private const char KeyDetailsMark = '|';
        private const string GlyphFolder = "Controller/";
        private const string GamepadFolder = "/Gamepad/";

        /// <summary>Fewest letters the words of a stem keep to be a name ("bg", "ui" say nothing).</summary>
        private const int MinLetters = 3;

        /// <summary>Fewest sibling controls that make a stem they all share the widget's skin and not its meaning.</summary>
        private const int MinSiblingsForSkin = 2;

        /// <summary>Words of a file name that are frames, states or decoration, not what the picture shows.</summary>
        private static readonly HashSet<string> Decoration = new(StringComparer.OrdinalIgnoreCase)
        {
            "frame", "bg", "background", "border", "hover", "pressed", "normal", "disabled", "button", "btn", "shadow", "glow",
            "highlight", "divider", "line", "panel", "window", "corner", "left", "right", "top", "bottom", "mid", "icon",
        };

        /// <summary>"Art/…/BagIcon.dds|0,0|0" → "bagicon": the lower-case file stem; empty when the key names no file.</summary>
        public static string Stem(string artKey) => FileStem(artKey).ToLowerInvariant();

        /// <summary>
        ///     The file's words ("Bag Icon" → "Bag") without decoration words and number pieces; empty for a controller glyph or
        ///     a stem with fewer than <see cref="MinLetters"/> letters left.
        /// </summary>
        public static string Words(string artKey)
        {
            if (artKey.StartsWith(GlyphFolder, StringComparison.Ordinal) || artKey.Contains(GamepadFolder, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var kept = ObjectNames.FromPath(FileStem(artKey))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !Decoration.Contains(word) && !word.All(char.IsDigit))
                .ToList();
            return kept.Sum(word => word.Count(char.IsLetter)) >= MinLetters ? string.Join(' ', kept) : string.Empty;
        }

        /// <summary>
        ///     The picture that names a control: of its own pictures, the one found in the fewest sibling controls; a stem all
        ///     the siblings share is their skin and is left out. Null when no picture gives words.
        /// </summary>
        public static ArtName? Pick(IReadOnlyList<string> own, IReadOnlyList<IReadOnlyList<string>> siblings)
        {
            var sibling = siblings.Select(keys => keys.Select(Stem).ToHashSet(StringComparer.Ordinal)).ToList();
            ArtName? best = null;
            var bestShare = int.MaxValue;
            foreach (var key in own)
            {
                var stem = Stem(key);
                var share = sibling.Count(stems => stems.Contains(stem));
                if (Words(key) is not { Length: > 0 } words || share >= bestShare || (sibling.Count >= MinSiblingsForSkin && share == sibling.Count))
                {
                    continue;
                }

                (best, bestShare) = (new ArtName(stem, words), share);
            }

            return best;
        }

        private static string FileStem(string artKey)
        {
            var end = artKey.IndexOf(KeyDetailsMark);
            var path = end < 0 ? artKey : artKey[..end];
            var file = path[(path.LastIndexOf('/') + 1)..];
            var dot = file.IndexOf('.');
            return dot < 0 ? file : file[..dot];
        }
    }
}
