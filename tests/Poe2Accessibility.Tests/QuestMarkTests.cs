namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The world map's quest marks: the nearest active waypoint over the map's connections, and what is said of a mark.</summary>
    public static class QuestMarkTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheWaypointNextDoorIsOneExitAway), TheWaypointNextDoorIsOneExitAway),
            (nameof(AMarkWithItsOwnWaypointNeedsNoOther), AMarkWithItsOwnWaypointNeedsNoOther),
            (nameof(WaypointsAsNearAreAllNamed), WaypointsAsNearAreAllNamed),
            (nameof(AFartherWaypointCountsItsExits), AFartherWaypointCountsItsExits),
            (nameof(NoWaypointWithinReachIsNone), NoWaypointWithinReachIsNone),
            (nameof(ALoopOfAreasEndsTheWalk), ALoopOfAreasEndsTheWalk),
            (nameof(TheGlanceSaysQuestPlaceStateAndWaypoint), TheGlanceSaysQuestPlaceStateAndWaypoint),
            (nameof(TheGlanceOfAPlaceWithItsWaypointEndsAtItsState), TheGlanceOfAPlaceWithItsWaypointEndsAtItsState),
            (nameof(TheGlanceSaysWhenNoWaypointIsNear), TheGlanceSaysWhenNoWaypointIsNear),
            (nameof(AWayNotReadClaimsNoWaypoint), AWayNotReadClaimsNoWaypoint),
            (nameof(TwoWaypointsAsNearGiveNoSide), TwoWaypointsAsNearGiveNoSide),
            (nameof(AQuestOfTheListSaysWhereItIsMarked), AQuestOfTheListSaysWhereItIsMarked),
        };

        // Act 1's west as the live map had it (2026-11-27): Ogham Manor's one exit is the Ramparts, whose waypoint is active.
        private static readonly Dictionary<string, string[]> Act1 = new()
        {
            ["G1_15"] = new[] { "G1_14" },
            ["G1_14"] = new[] { "G1_13_2", "G1_15" },
            ["G1_13_2"] = new[] { "G1_13_1", "G1_14" },
            ["G1_13_1"] = new[] { "G1_11", "G1_13_2" },
            ["G1_11"] = new[] { "G1_13_1" },
        };

        private static IEnumerable<string> Next(string area) => Act1.TryGetValue(area, out var next) ? next : Array.Empty<string>();

        private static void Expect<T>(T expected, T actual, string what) => Check.Expect(expected, actual, what);

        private static void TheWaypointNextDoorIsOneExitAway()
        {
            var (steps, goals) = QuestMarkRoutes.Nearest("G1_15", Next, area => area is "G1_14" or "G1_13_2");
            Expect(1, steps, "exits");
            Expect("G1_14", string.Join(",", goals), "waypoints");
        }

        private static void AMarkWithItsOwnWaypointNeedsNoOther()
        {
            var (steps, goals) = QuestMarkRoutes.Nearest("G1_14", Next, area => area is "G1_14" or "G1_13_2");
            Expect(0, steps, "exits");
            Expect("G1_14", string.Join(",", goals), "its own");
        }

        private static void WaypointsAsNearAreAllNamed()
        {
            var (steps, goals) = QuestMarkRoutes.Nearest("G1_14", Next, area => area is "G1_15" or "G1_13_2");
            Expect(1, steps, "exits");
            Expect("G1_13_2,G1_15", string.Join(",", goals), "both, in the order found");
        }

        private static void AFartherWaypointCountsItsExits()
        {
            var (steps, goals) = QuestMarkRoutes.Nearest("G1_15", Next, area => area == "G1_13_1");
            Expect(3, steps, "exits");
            Expect("G1_13_1", string.Join(",", goals), "waypoint");
        }

        private static void NoWaypointWithinReachIsNone()
        {
            var (steps, goals) = QuestMarkRoutes.Nearest("G1_15", Next, _ => false);
            Expect(-1, steps, "exits");
            Expect(0, goals.Count, "waypoints");
        }

        private static void ALoopOfAreasEndsTheWalk()
        {
            var loop = new Dictionary<int, int[]> { [1] = new[] { 2 }, [2] = new[] { 3 }, [3] = new[] { 1 } };
            var (steps, _) = QuestMarkRoutes.Nearest(1, area => loop[area], _ => false);
            Expect(-1, steps, "a loop with no waypoint");
        }

        private static QuestMark Manor(IReadOnlyList<string> waypoints, int steps, string side, MapAreaState state = MapAreaState.Unknown) =>
            new(new[] { "The Mad Wolf of Ogham" }, "Ogham Manor", state, IntPtr.Zero, waypoints, steps, side);

        private static void TheGlanceSaysQuestPlaceStateAndWaypoint() => Expect(
            "Quest location: The Mad Wolf of Ogham, Ogham Manor, Not visited. Nearest waypoint: The Manor Ramparts, one area away, below it on the map",
            Announcements.QuestMarkGlance(Manor(new[] { "The Manor Ramparts" }, 1, Announcements.OnTheMapFrom(6))),
            "the look at the map");

        private static void TheGlanceOfAPlaceWithItsWaypointEndsAtItsState() => Expect(
            "Quest location: The Mad Wolf of Ogham, Ogham Manor, Visited, waypoint active",
            Announcements.QuestMarkGlance(Manor(Array.Empty<string>(), 0, string.Empty, MapAreaState.Waypoint)),
            "its own waypoint");

        private static void TheGlanceSaysWhenNoWaypointIsNear() => Expect(
            "Quest location: The Mad Wolf of Ogham, Ogham Manor, Not visited. No active waypoint near it",
            Announcements.QuestMarkGlance(Manor(Array.Empty<string>(), QuestMark.NoneNear, string.Empty)),
            "none near");

        // Connections that did not read, or a travel map: nothing is said of waypoints, rather than "none near".
        private static void AWayNotReadClaimsNoWaypoint() => Expect(
            "Quest location: The Mad Wolf of Ogham, Ogham Manor, Not visited",
            Announcements.QuestMarkGlance(Manor(Array.Empty<string>(), QuestMark.NotKnown, string.Empty)),
            "not known");

        private static void TwoWaypointsAsNearGiveNoSide() => Expect(
            "Quest location: The Mad Wolf of Ogham, Ogham Manor, Not visited. Nearest waypoint: The Manor Ramparts or Ogham Village, 2 areas away",
            Announcements.QuestMarkGlance(Manor(new[] { "The Manor Ramparts", "Ogham Village" }, 2, Announcements.OnTheMapFrom(6))),
            "one side would point at one of the two only");

        private static void AQuestOfTheListSaysWhereItIsMarked() =>
            Expect("marked on Ogham Manor and The Manor Ramparts", Announcements.QuestMarkedOn(new[] { "Ogham Manor", "The Manor Ramparts" }), "list");
    }
}
