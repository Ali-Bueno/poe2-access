namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>One line of a card's column, and whether it belongs to the column's price block.</summary>
    public readonly record struct CardLine(string Text, bool Price);

    /// <summary>
    ///     A card: one focused entry whose columns hold more lines than a table row (a trade market listing: the seller's
    ///     column beside the item's). Said as a sighted player scans it: the item, its price, the other columns (seller,
    ///     online or not); the rest of the item column (properties, mods) follows as details. Pure.
    /// </summary>
    public static class CardText
    {
        /// <summary>The shop's banners are downloaded images: "…/ShopImages/public_news_2026-09-21_CotABox_small.png|0,0,0,0|1".</summary>
        private const string ShopImagesFolder = "/ShopImages/";

        private static readonly System.Text.RegularExpressions.Regex NewsPrefix = new(@"^(public_)?news_\d{4}-\d{2}-\d{2}_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>The words of the shop's image file names that name the file, not the offer ("client_shop_assets_…_Thumb").</summary>
        private static readonly HashSet<string> FileNoise = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "public", "client", "shop", "assets", "featured", "thumb", "small", "large", "news",
        };

        /// <summary>The game's own name in a file name ("PoE2EarlyAccess…"), dropped before the words are split.</summary>
        private static readonly System.Text.RegularExpressions.Regex GameName = new("PoE2?", System.Text.RegularExpressions.RegexOptions.None);

        private static readonly System.Text.RegularExpressions.Regex SizeSuffix = new(@"_(small|large|medium|banner|thumb)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        ///     A featured banner's title from its image file, the only name it has ("LegendsOfWraeclast" → "Legends Of
        ///     Wraeclast"); empty when the texture is no shop image.
        /// </summary>
        public static string ShopImageTitle(string textureKey)
        {
            var path = textureKey.Split('|')[0].Replace('\\', '/');
            var at = path.IndexOf(ShopImagesFolder, System.StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return string.Empty;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(path[(at + ShopImagesFolder.Length)..]);
            name = GameName.Replace(SizeSuffix.Replace(NewsPrefix.Replace(name, string.Empty), string.Empty), "_");
            var words = new List<string>();
            foreach (var part in name.Split(new[] { '_', '-', ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var start = 0;
                for (var i = 1; i <= part.Length; i++)
                {
                    // A capital after a small letter or digit, or the capital before a small one in a run (CotABox → Cot A Box).
                    if (i == part.Length || (char.IsUpper(part[i]) && (!char.IsUpper(part[i - 1]) || (i + 1 < part.Length && char.IsLower(part[i + 1])))))
                    {
                        if (!FileNoise.Contains(part[start..i]))
                        {
                            words.Add(part[start..i]);
                        }

                        start = i;
                    }
                }
            }

            return string.Join(" ", words);
        }

        /// <returns>Null when the lines fit a table row (<paramref name="rowCells"/>) or there is a single column.</returns>
        public static (string Label, List<string> Details)? Read(IReadOnlyList<IReadOnlyList<CardLine>> columns, int rowCells)
        {
            if (columns.Count < 2 || columns.Sum(column => column.Count) <= rowCells)
            {
                return null;
            }

            // The priced column is the item; without a price, the column with the most lines is.
            var main = columns.FirstOrDefault(column => column.Any(line => line.Price)) ?? columns.OrderByDescending(column => column.Count).First();
            var label = new List<string> { main[0].Text };
            var price = main.Where(line => line.Price).Select(line => line.Text).ToList();
            if (price.Count > 0)
            {
                label.Add(Announcements.Cells(price));
            }

            label.AddRange(columns.Where(column => !ReferenceEquals(column, main)).Select(column => Announcements.Cells(column.Select(line => line.Text))));
            return (string.Join(", ", label), main.Skip(1).Where(line => !line.Price).Select(line => line.Text).ToList());
        }
    }
}
