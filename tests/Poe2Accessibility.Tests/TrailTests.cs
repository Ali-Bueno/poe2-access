namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The map's cues that run somewhere: the walk along them, the player on them, the rituals' swarm.</summary>
    public static class TrailTests
    {
        private const int Width = 400;
        private const int Height = 200;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(WalkGoesRoundTheWall), WalkGoesRoundTheWall),
            (nameof(TrailEndsCountWaterBesideTheBank), TrailEndsCountWaterBesideTheBank),
            (nameof(FollowedTrailSaysItsPartsOnce), FollowedTrailSaysItsPartsOnce),
            (nameof(WrongWayAndLeavingAreSaidOnce), WrongWayAndLeavingAreSaidOnce),
            (nameof(TrailComeBackToIsNewsOnlyAfterLong), TrailComeBackToIsNewsOnlyAfterLong),
            (nameof(SwarmFliesToTheNearestAltarLeft), SwarmFliesToTheNearestAltarLeft),
        };

        /// <summary>An open field with a wall at x 200 but for its last rows; a river (not walkable) along the top ten.</summary>
        private static byte[] Field()
        {
            var grid = Enumerable.Repeat((byte)0x11, Width / 2 * Height).ToArray();
            for (var y = 0; y < Height - 20; y++)
            {
                grid[(y * Width / 2) + 100] = 0;
            }

            Array.Clear(grid, 0, Width / 2 * 10);
            return grid;
        }

        private static void WalkGoesRoundTheWall()
        {
            var field = TrailField.Build(Field(), Width / 2, new Vector2(300, 50))!;
            Equal(0, field.Steps(new Vector2(300, 50)), "at the goal");
            Equal(50 + 100, field.Steps(new Vector2(350, 150)), "the steps of an open walk");
            Equal(true, field.Steps(new Vector2(100, 50)) > 200 + 200, "the wall is walked round");
            var ahead = field.Ahead(new Vector2(100, 50), 40)!.Value;
            Equal(field.Steps(new Vector2(100, 50)) - 40, field.Steps(ahead), "ahead is that much nearer");
            Equal(null, TrailField.Build(Array.Empty<byte>(), 0, Vector2.Zero), "no map yet");
        }

        private static void TrailEndsCountWaterBesideTheBank()
        {
            var field = TrailField.Build(Field(), Width / 2, new Vector2(399, 12))!;
            var river = Enumerable.Range(0, 18).Select(i => new Vector2(210 + (10 * i), 2)).ToList();
            var ends = field.Ends(river)!.Value;
            Equal(true, ends.Near < 30 && ends.Far is > 180 and < 210, $"the river's ends by its bank: {ends}");
            Equal(null, field.Ends(Array.Empty<Vector2>()), "no spots, no ends");
        }

        private static void FollowedTrailSaysItsPartsOnce()
        {
            var follower = new TrailFollower();
            var said = new List<string>();
            long now = 1000;
            for (var walk = 900; walk >= 0; walk -= 9, now += 500)
            {
                var weave = walk + (walk / 9 % 3 * 10); // round a rock and back
                if (follower.Update(now, weave, true, true, (0, 900)) is var news and not TrailNews.None)
                {
                    said.Add($"{news} {follower.Part}");
                }
            }

            Equal("Found Start, Part Middle, Part End", string.Join(", ", said), "met at its beginning and followed to its end");
            Equal(true, follower.Leads(now), "the sound leads all along");
        }

        private static void WrongWayAndLeavingAreSaidOnce()
        {
            var follower = new TrailFollower();
            long now = 1000;
            var said = new List<TrailNews>();
            void Walk(int from, int to, bool on)
            {
                for (var walk = from; from < to ? walk <= to : walk >= to; walk += from < to ? 10 : -10, now += 500)
                {
                    said.Add(follower.Update(now, walk, true, on, (0, 900)));
                }
            }

            string Said() => string.Join(", ", said.Where(news => news != TrailNews.None));
            Walk(500, 900, true);
            Equal("Found, Away", Said(), "the wrong way from the first step, said once");
            Equal(false, follower.Leads(now), "and no sound that way");
            said.Clear();
            Walk(890, 800, true);
            Equal(string.Empty, Said(), "the right way now: no line");
            Equal(true, follower.Leads(now), "the sound is back");
            now += TrailFollower.WarnMs;
            follower.Update(now, 800, true, true, (0, 900));
            Walk(800, 700, false);
            Equal("Left", Said(), "off the trail");
            Equal(false, follower.Leads(now), "nothing leads off it");

            var passed = new TrailFollower();
            passed.Update(now, 500, true, false, (0, 900));
            Equal(TrailNews.None, passed.Update(now + TrailFollower.LeftMs, 490, true, false, (0, 900)), "seen in passing, never walked on: nothing left");
        }

        private static void TrailComeBackToIsNewsOnlyAfterLong()
        {
            var follower = new TrailFollower();
            Equal(TrailNews.None, follower.Update(0, null, true, true, (0, 900)), "no way there");
            Equal(TrailNews.None, follower.Update(0, 600, false, false, (0, 900)), "not in sight");
            Equal(TrailNews.Found, follower.Update(0, 600, true, true, (0, 900)), "met");
            follower.Update(500, 600, true, true, (0, 900));
            follower.Update(500 + TrailFollower.LeftMs, 600, false, false, (0, 900));
            Equal(TrailNews.None, follower.Update(10000, 600, true, true, (0, 900)), "back soon");
            follower.Update(10000 + TrailFollower.LeftMs, 600, false, false, (0, 900));
            Equal(TrailNews.Found, follower.Update(10000 + TrailFollower.AgainMs, 200, true, true, (0, 900)), "back after long");
            Equal(TrailPart.End, follower.Part, "farther along");
        }

        private static void SwarmFliesToTheNearestAltarLeft()
        {
            // Freythorn as it stood on 2026-11-23: three ritual rooms and the boss's arena.
            var altars = new[] { new Vector2(2795, 794), new Vector2(2933, 1346), new Vector2(3209, 1346), new Vector2(2518, 1484), new Vector2(2795, 794) };
            var boss = new Vector2(2530, 1495);
            var rituals = new RitualProgress();
            var here = altars[0];
            Equal(false, rituals.Finished(7, 1, here), "ready");
            Equal(false, rituals.Finished(7, 2, here), "fought");
            Equal(true, rituals.Finished(7, 3, here), "won");
            Equal(false, rituals.Finished(7, 4, here), "its reward taken: nothing new");
            Equal(altars[1], rituals.Next(altars, here, boss), "the nearest of the others");
            Equal(false, rituals.Finished(8, 3, altars[1]), "come upon done already: no swarm");
            Equal(altars[2], rituals.Next(altars, here, boss), "but not flown to");
            rituals.Finished(9, 2, altars[2]);
            Equal(true, rituals.Finished(9, 3, altars[2]), "the last one");
            Equal(null, rituals.Next(altars, here, boss), "only the boss's altar is left");
        }
    }
}
