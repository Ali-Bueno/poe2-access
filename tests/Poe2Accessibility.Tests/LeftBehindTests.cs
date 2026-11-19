namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>What the player leaves behind unexplored: a nook walked past, and the parts of the map finished.</summary>
    public static class LeftBehindTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ANookBesideACorridorIsSmallUnseenGroundNear), ANookBesideACorridorIsSmallUnseenGroundNear),
            (nameof(ANookWalkedAwayFromIsToldOnce), ANookWalkedAwayFromIsToldOnce),
            (nameof(OnlyANookCameNearAndNotSeenIsTold), OnlyANookCameNearAndNotSeenIsTold),
            (nameof(NooksLeftTogetherAreToldApartAndAJumpForgetsThem), NooksLeftTogetherAreToldApartAndAJumpForgetsThem),
            (nameof(APartFinishedIsNewsOnce), APartFinishedIsNewsOnce),
        };

        private static void ANookBesideACorridorIsSmallUnseenGroundNear()
        {
            // A corridor 2000 cells long, 20 wide, and a nook 30 by 80 off its side at x 300; the player walked the first 600.
            var grid = MapRadarTests.Carved(2000, 220, (0, 100, 2000, 120), (300, 120, 330, 200));
            var map = new ExplorationMap();
            for (var x = 0; x <= 600; x += 20)
            {
                map.Visit(new Vector2(x, 110), 2000, 220);
            }

            var player = new Vector2(600, 110);
            map.Frontier(grid, 1000, player, 8);
            var nook = map.SmallUnseenNear(new Vector2(320, 110), LeftBehindWatch.NearCells);
            Equal(true, nook.Count > 0 && nook.All(cell => cell.X is >= 296 and <= 336 && cell.Y > 160), "the nook's far end, past a look from the corridor");
            Equal(0, map.SmallUnseenNear(player, LeftBehindWatch.NearCells).Count, "the corridor ahead goes on: no nook");
        }

        private static void ANookWalkedAwayFromIsToldOnce()
        {
            var watch = new LeftBehindWatch();
            var nook = new Vector2(80, 0);
            var edge = new[] { nook, new Vector2(80, 30), new Vector2(110, 10) };
            var told = new List<Vector2>();
            var now = 0L;
            for (var x = 0; x >= -300; x -= 20, now += 500)
            {
                if (watch.Update(new Vector2(x, 0), x == 0 ? edge : Array.Empty<Vector2>(), _ => false, false, now) is { } cell)
                {
                    told.Add(cell);
                }
            }

            Equal(true, told.Count == 1 && edge.Contains(told[0]), "once for the whole nook, from its edge, two looks away");
        }

        private static void OnlyANookCameNearAndNotSeenIsTold()
        {
            var far = new LeftBehindWatch();
            Equal(false, Walk(far, new Vector2(200, 0), _ => false, _ => false).Any(), "a nook never within reach is not one walked past");

            var seen = new LeftBehindWatch();
            Equal(false, Walk(seen, new Vector2(80, 0), _ => true, _ => false).Any(), "a nook looked into is explored");

            var fight = new LeftBehindWatch();
            var heard = Walk(fight, new Vector2(80, 0), _ => false, x => x > -260).ToList();
            Equal(true, heard.Count == 1 && heard[0] <= -260, "a fight holds it back until it is over");
        }

        private static void NooksLeftTogetherAreToldApartAndAJumpForgetsThem()
        {
            var watch = new LeftBehindWatch();
            var nooks = new[] { new Vector2(80, 0), new Vector2(0, 85) };
            var told = new List<long>();
            var now = 0L;
            for (var x = 0; x >= -400; x -= 20, now += 500)
            {
                if (watch.Update(new Vector2(x, x), x == 0 ? nooks : Array.Empty<Vector2>(), _ => false, false, now) is not null)
                {
                    told.Add(now);
                }
            }

            Equal(true, told.Count == 2 && told[1] - told[0] >= LeftBehindWatch.GapMs, "two nooks, the second after a gap");

            var jump = new LeftBehindWatch();
            jump.Update(Vector2.Zero, nooks, _ => false, false, 0);
            Equal(null, jump.Update(new Vector2(-500, 0), Array.Empty<Vector2>(), _ => false, false, 500), "a waypoint is no walk away");
            Equal(null, jump.Update(new Vector2(-520, 0), Array.Empty<Vector2>(), _ => false, false, 1000), "and what was near before it is forgotten");
        }

        private static void APartFinishedIsNewsOnce()
        {
            var done = new MapPartsDone();
            MapPart Part(int across, bool explored) => new(across, 0, explored ? 90 : 30, explored);
            Equal(null, done.Update(new[] { Part(-1, true), Part(0, false), Part(1, false) }), "how the map stands on arrival is no news");
            Equal(null, done.Update(new[] { Part(-1, true), Part(0, false), Part(1, false) }), "nothing changed");
            var news = done.Update(new[] { Part(-1, true), Part(0, false), Part(1, true) });
            Equal("1|0", news is { } n ? $"{string.Join(",", n.Done.Select(part => part.Across))}|{string.Join(",", n.Left.Select(part => part.Across))}" : "none",
                "the right third finished, the middle left");
            Equal(null, done.Update(new[] { Part(-1, true), Part(0, false), Part(1, true) }), "said once");
            Equal(null, done.Update(new[] { Part(0, true) }), "one part is no map of parts");
            Equal("Done: Right edge. Left: Middle of the map", Announcements.MapPartsDone(new[] { Part(1, true) }, new[] { Part(0, false) }), "the words");
            Equal(true, Announcements.MapPartsDone(new[] { Part(0, true) }, Array.Empty<MapPart>()).EndsWith("The whole map is explored", StringComparison.Ordinal), "nothing left");
        }

        /// <summary>Stands by a nook at the origin's side, then walks 300 cells away; the x of each cue heard.</summary>
        private static IEnumerable<int> Walk(LeftBehindWatch watch, Vector2 nook, Func<Vector2, bool> seen, Func<int, bool> quiet)
        {
            var now = 0L;
            for (var x = 0; x >= -300; x -= 20, now += 500)
            {
                if (watch.Update(new Vector2(x, 0), new[] { nook }, seen, quiet(x), now) is not null)
                {
                    yield return x;
                }
            }
        }
    }
}
