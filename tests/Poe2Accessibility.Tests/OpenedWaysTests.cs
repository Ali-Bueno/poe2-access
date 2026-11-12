namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A way that opened stays to be found until the player has stood at it and walked on.</summary>
    public static class OpenedWaysTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AWayStaysUntilStoodAtAndLeft), AWayStaysUntilStoodAtAndLeft),
            (nameof(AWayNeverReachedStaysFromAnyDistance), AWayNeverReachedStaysFromAnyDistance),
            (nameof(AWayThatShutOrANewAreaForgetsIt), AWayThatShutOrANewAreaForgetsIt),
            (nameof(EachWayIsForgottenOnItsOwn), EachWayIsForgottenOnItsOwn),
        };

        private static ScanEntry Way(uint id, float x, float y) => new(id, ScanCategory.Objects, "Paso abierto", x, y, 0, OpenedWays.Kind);

        private static void AWayStaysUntilStoodAtAndLeft()
        {
            var ways = new OpenedWays();
            ways.Add(Way(7, 1000, 0));
            Expect(1, ways.Update(0, 0).Count, "listed from afar");
            Expect(1, ways.Update(1000 - OpenedWays.ReachWorld, 0).Count, "listed while the player stands at it");
            Expect(1, ways.Update(1000 + OpenedWays.LeftWorld, 0).Count, "still listed within the distance that counts as there");
            Expect(0, ways.Update(1000 + OpenedWays.LeftWorld + 1, 0).Count, "forgotten once walked on");
            Expect(0, ways.Update(1000, 0).Count, "and not back when walked to again");
        }

        private static void AWayNeverReachedStaysFromAnyDistance()
        {
            var ways = new OpenedWays();
            ways.Add(Way(7, 1000, 0));
            Expect(1, ways.Update(1000 - OpenedWays.ReachWorld - 1, 0).Count, "just short of it");
            Expect(1, ways.Update(-50000, 0).Count, "walked far off without having stood at it");
        }

        private static void AWayThatShutOrANewAreaForgetsIt()
        {
            var ways = new OpenedWays();
            ways.Add(Way(7, 1000, 0));
            ways.Add(Way(8, 0, 1000));
            ways.Remove(7);
            Expect(true, ways.Update(0, 0).Select(way => way.Id).SequenceEqual(new uint[] { 8 }), "the gate that shut again is gone");
            ways.Clear();
            Expect(0, ways.Update(0, 0).Count, "a new area");
        }

        private static void EachWayIsForgottenOnItsOwn()
        {
            var ways = new OpenedWays();
            ways.Add(Way(7, 1000, 0));
            ways.Add(Way(8, -1000, 0));
            ways.Update(1000, 0);
            Expect(true, ways.Update(0, 0).Select(way => way.Id).SequenceEqual(new uint[] { 8 }), "the one walked through is gone, the other stays");
            ways.Add(Way(7, 1000, 0));
            Expect(2, ways.Update(0, 0).Count, "one that opens again is new");
        }
    }
}
