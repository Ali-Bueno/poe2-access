namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A Trial shrine's cost in sacred water, read from its shared state and said with its line.</summary>
    public static class ShrineCostTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheCostIsTheSharedStateWhenItIsThereAndNonZero), TheCostIsTheSharedStateWhenItIsThereAndNonZero),
            (nameof(TheCostIsSaidInTheCurrencyOrNotAtAll), TheCostIsSaidInTheCurrencyOrNotAtAll),
        };

        private static void TheCostIsTheSharedStateWhenItIsThereAndNonZero()
        {
            Equal(5, ShrineCost.Of(new[] { ("open", 0L), (ShrineCost.StateName, 5L) }), "the state is there");
            Equal(0, ShrineCost.Of(new[] { ("open", 1L) }), "no such state");
            Equal(0, ShrineCost.Of(new[] { (ShrineCost.StateName, 0L) }), "zero");
            Equal(0, ShrineCost.Of(new[] { (ShrineCost.StateName, -3L) }), "negative");
            Equal(0, ShrineCost.Of(new[] { (ShrineCost.StateName, 1L << 40) }), "beyond a cost");
            Equal(0, ShrineCost.Of(Array.Empty<(string, long)>()), "no states");
        }

        private static void TheCostIsSaidInTheCurrencyOrNotAtAll()
        {
            try
            {
                Localization.Detect("es");
                Equal("Honra a Tabana, A, cuesta 5 de agua sagrada", Announcements.WithCost("Honra a Tabana, A", 5), "Spanish");
                Equal("Honra a Tabana, A", Announcements.WithCost("Honra a Tabana, A", 0), "no cost");
                Localization.Detect("en");
                Equal("Shrine, A, costs 5 Sacred Water", Announcements.WithCost("Shrine, A", 5), "English");
            }
            finally
            {
                Localization.Detect("en");
            }
        }
    }
}
