namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The arrival banner's tagline (<see cref="AreaBanner"/>).</summary>
    public static class AreaBannerTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(NameWithTwoTextParentGivesTheTagline), NameWithTwoTextParentGivesTheTagline),
            (nameof(StaleOrAbsentBannerGivesNothing), StaleOrAbsentBannerGivesNothing),
            (nameof(AnAreaHasItsTaglineOnce), AnAreaHasItsTaglineOnce),
        };

        private static IReadOnlyList<string> Group(params string[] texts) => texts;

        private static void NameWithTwoTextParentGivesTheTagline()
        {
            var groups = new[] { Group("Bahía de las anclas"), Group("Bahía de las anclas", "Incontables restos emergen de entre la niebla") };
            Equal("Incontables restos emergen de entre la niebla", AreaBanner.Match("Bahía de las anclas", groups), "the other text of the two-text parent");
        }

        private static void StaleOrAbsentBannerGivesNothing()
        {
            var stale = new[] { Group("Isla de los alcaudones", "Aquí los karui recogen huevos para mostrar su valía") };
            Equal(null, AreaBanner.Match("Bahía de las anclas", stale), "a banner of the previous area");
            Equal(null, AreaBanner.Match("Bahía de las anclas", new[] { Group("Bahía de las anclas", "a", "b") }), "three texts are not the banner");
            Equal(null, AreaBanner.Match("Bahía de las anclas", Array.Empty<IReadOnlyList<string>>()), "no banner");
        }

        private static void AnAreaHasItsTaglineOnce()
        {
            var banner = new AreaBanner();
            var groups = new[] { Group("Marca del rey", "Donde florecen el comercio y las oportunidades") };
            Equal("Donde florecen el comercio y las oportunidades", banner.TakeOnce("G1_town", "Marca del rey", groups), "first arrival");
            Equal(null, banner.TakeOnce("G1_town", "Marca del rey", groups), "a second visit says nothing");
            Equal("Donde florecen el comercio y las oportunidades", banner.TakeOnce("G2_town", "Marca del rey", groups), "another area id");
        }
    }
}
