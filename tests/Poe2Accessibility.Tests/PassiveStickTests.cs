namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     The game's stick rule on the passive tree (<see cref="PassiveStick"/>): the 240 pushes recorded with the virtual
    ///     pad at 21:9 and 16:9 (2026-11-08, docs/research/passive-tree.md), its stages one by one, and the fitted fallback.
    /// </summary>
    public static class PassiveStickTests
    {
        /// <summary>Per window a header "# window W H cursor X Y", then one push per line: from, octant, landed, then "id angle distance L|-" per node in the 45-degree cone.</summary>
        private const string FixtureName = "stick-walks.txt";

        private const int RecordedPushes = 240;

        /// <summary>What the fitted model scored on the same pushes (validate.py), a check that the fixture holds what it did.</summary>
        private const int FittedRight = 208;

        /// <summary>The recordings are in screen pixels, so a tree unit is a pixel.</summary>
        private const float PixelsPerPixel = 1f;

        /// <summary>A window no node leaves, for tests about the rule alone.</summary>
        private static readonly PassiveStick.Window Anywhere = new(new Vector2(5000, 5000), new Vector2(10000, 10000), PixelsPerPixel);

        private static readonly int[] NoLinks = Array.Empty<int>();

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EveryRecordedPushLandsWhereTheGameDid), EveryRecordedPushLandsWhereTheGameDid),
            (nameof(ALinkWithinTwentyDegreesBeatsANearerNodeAhead), ALinkWithinTwentyDegreesBeatsANearerNodeAhead),
            (nameof(TheFirstLinkInEdgeOrderWins), TheFirstLinkInEdgeOrderWins),
            (nameof(TheConeKeepsTheLowestWeightedDistance), TheConeKeepsTheLowestWeightedDistance),
            (nameof(ANodeOutsideTheWindowIsNeverLandedOn), ANodeOutsideTheWindowIsNeverLandedOn),
            (nameof(TheWindowIsTheSettledViewNotTheMidPanOne), TheWindowIsTheSettledViewNotTheMidPanOne),
            (nameof(WithoutAWindowTheFittedModelStandsIn), WithoutAWindowTheFittedModelStandsIn),
            (nameof(ALinkIsNamedByThePushThatReachesIt), ALinkIsNamedByThePushThatReachesIt),
            (nameof(ARouteFollowsThePushesThatNear), ARouteFollowsThePushesThatNear),
            (nameof(ALinkBetweenTwoDirectionsIsNamedByThePushBetween), ALinkBetweenTwoDirectionsIsNamedByThePushBetween),
        };

        private static void EveryRecordedPushLandsWhereTheGameDid()
        {
            var pushes = Pushes().ToList();
            Expect(RecordedPushes, pushes.Count, "pushes recorded");
            var misses = new List<string>();
            var fitted = 0;
            var stayed = 0;
            foreach (var push in pushes)
            {
                var landed = PassiveStick.Pick(push.Offsets, push.Octant, push.Linked, push.Window);
                var id = landed >= 0 ? push.Ids[landed] : push.From;
                if (id != push.Landed)
                {
                    misses.Add($"{push.From} {push.Octant}: {id} not {push.Landed}");
                }

                stayed += landed < 0 ? 1 : 0;
                var old = PassiveStick.PickFitted(push.Offsets, push.Octant);
                fitted += (old >= 0 ? push.Ids[old] : push.From) == push.Landed ? 1 : 0;
            }

            Expect(string.Empty, string.Join("; ", misses.Take(5)), $"{misses.Count} of {pushes.Count} pushes miss");
            Expect(7, stayed, "pushes the game did not move on");
            Expect(FittedRight, fitted, "the fitted model on the same pushes");
        }

        private static void ALinkWithinTwentyDegreesBeatsANearerNodeAhead()
        {
            // A node straight below at 150, a link 15 degrees off at 160 (cone score 182), one off to the right.
            var offsets = Offsets((-90, 150), (-75, 160), (0, 77));
            Expect(1, PassiveStick.Pick(offsets, 6, new[] { 1 }, Anywhere), "the link, not the node ahead");
            Expect(0, PassiveStick.Pick(offsets, 6, NoLinks, Anywhere), "without links the node ahead");
            Expect(0, PassiveStick.Pick(offsets, 6, new[] { 2 }, Anywhere), "a link 90 degrees off is no link to take");
        }

        private static void TheFirstLinkInEdgeOrderWins()
        {
            // Both links are within 19.95 degrees of down; the game keeps the first one its edge vectors list.
            var offsets = Offsets((-108.8, 158), (-76.5, 164));
            Expect(0, PassiveStick.Pick(offsets, 6, new[] { 0, 1 }, Anywhere), "first listed, farther off the push");
            Expect(1, PassiveStick.Pick(offsets, 6, new[] { 1, 0 }, Anywhere), "first listed, nearer the push");
        }

        private static void TheConeKeepsTheLowestWeightedDistance()
        {
            // (5 - 4 cos) x distance: 100 at 44 degrees scores 212, 150 straight ahead scores 150, 250 at 20 degrees 275.
            var offsets = Offsets((44, 100), (0, 150), (20, 250), (46, 50));
            Expect(1, PassiveStick.Pick(offsets, 0, NoLinks, Anywhere), "straight ahead at 150 beats 100 at the cone's edge");
            Expect(-1, PassiveStick.Pick(Offsets((46, 50), (180, 10)), 0, NoLinks, Anywhere), "nothing within 45 degrees: no move");
        }

        private static void ANodeOutsideTheWindowIsNeverLandedOn()
        {
            // The cursor sits 586 px down a 1080 px window: a node 600 px below it is off the screen, one 500 px below is not.
            var window = new PassiveStick.Window(new Vector2(1279.75f, 585.9f), new Vector2(2560, 1080), PixelsPerPixel);
            Expect(-1, PassiveStick.Pick(Offsets((-90, 600)), 6, NoLinks, window), "off the bottom: no move");
            Expect(0, PassiveStick.Pick(Offsets((-90, 490)), 6, NoLinks, window), "inside: landed");
            Expect(1, PassiveStick.Pick(Offsets((-90, 600), (-70, 490)), 6, new[] { 0 }, window), "a link off the screen yields to the cone");
            Expect(-1, PassiveStick.Pick(Offsets((90, 586)), 2, NoLinks, window), "the top edge is outside (y must be at least 0)");
            Expect(0, PassiveStick.Pick(Offsets((90, 585.9)), 2, NoLinks, window), "y = 0 is inside");
        }

        private static void TheWindowIsTheSettledViewNotTheMidPanOne()
        {
            // Measured 2026-11-08 (2560x1440, x1.9): the frame 0,122 2560x1318; the node rests at 1279.8,781.2 and is drawn at
            // 1307,699 a frame after the push that reached it. A node 700 px below is off the screen once settled, on it mid-pan.
            var settled = PassiveStick.SettledCursor(new Vector2(0, 122), new Vector2(2560, 1318));
            Expect(new Vector2(1280, 781), settled, "the frame's centre");
            var client = new Vector2(2560, 1440);
            var below = Offsets((-90, 700));
            Expect(0, PassiveStick.Pick(below, 6, NoLinks, new PassiveStick.Window(new Vector2(1307, 699), client, PixelsPerPixel)), "mid-pan the far node is drawn");
            Expect(-1, PassiveStick.Pick(below, 6, NoLinks, new PassiveStick.Window(settled, client, PixelsPerPixel)), "settled it is off the screen: no move");
        }

        private static void WithoutAWindowTheFittedModelStandsIn()
        {
            // Moves recorded in the game (2026-10-31) that fitted the old model: screen offsets, the push, where it landed.
            var mana18 = new Vector2[] { new(66, -66), new(-67, -67), new(126, 0), new(-126, 0), new(-23, -201), new(27, -201), new(-181, -134), new(-236, 0), new(-181, 178), new(2, 273), new(-128, 241), new(-120, -260) };
            Expect(9, PassiveStick.Pick(mana18, 6, NoLinks, null), "down: the node straight below, past a nearer one off to the side");
            var notable = new Vector2[] { new(-59, 112), new(0, 129), new(-130, 31), new(-130, -31), new(-103, 156), new(16, 188), new(-184, 94), new(-184, -95), new(102, 186), new(228, 11), new(-238, 30), new(-238, -31) };
            Expect(9, PassiveStick.PickFitted(notable, 0), "right: the only node within 45 degrees, however far");
            Expect(-1, PassiveStick.PickFitted(new Vector2[] { new(100, 0) }, 4), "nothing within the cone");
        }

        private static void ALinkIsNamedByThePushThatReachesIt()
        {
            // A node 60 degrees up from the right beside one at 45: linked, up-right takes it (15 degrees off); unlinked, the
            // one at 45 hides it from up-right and up reaches it.
            var link = new Vector2[] { new(50, -87), new(200, 0), new(70, -70) };
            Expect((int?)1, PassiveStick.OctantTo(link, 0, new[] { 0 }, Anywhere), "a link within 20 degrees of the push");
            Expect((int?)2, PassiveStick.OctantTo(link, 0, NoLinks, Anywhere), "the push whose cone it wins");
            Expect((int?)null, PassiveStick.OctantTo(new Vector2[] { new(100, -10), new(300, -100) }, 1, NoLinks, Anywhere), "a node a nearer one hides is reached by no single push");
        }

        /// <summary>
        ///     The Druid's override node lay between Atributo (left) and Regeneración de vida (up left), all three linked: no
        ///     octant reaches it, a push halfway between the two does (2026-11-09, said "left, not in one push" before).
        /// </summary>
        private static void ALinkBetweenTwoDirectionsIsNamedByThePushBetween()
        {
            var links = new Vector2[] { new(-100, 0), new(-100, -42), new(-70, -70) };
            var linked = new[] { 0, 1, 2 };
            Expect(0, PassiveStick.Pick(links, 4, linked, Anywhere), "left lands on Atributo");
            Expect(2, PassiveStick.Pick(links, 3, linked, Anywhere), "up left on Regeneración de vida");
            Expect((int?)null, PassiveStick.OctantTo(links, 1, linked, Anywhere), "no octant reaches the node between them");
            Expect((int?)3, PassiveStick.BetweenTo(links, 1, linked, Anywhere), "the push between up left and left does");
            Expect((int?)null, PassiveStick.BetweenTo(links, 1, linked, null), "the fitted model knows octants only");
        }

        private static void ARouteFollowsThePushesThatNear()
        {
            var line = new Vector2[] { new(0, 0), new(100, 0), new(200, 0), new(300, 0) };
            Expect("0 0 0", string.Join(" ", PassiveStick.Route(line, 0, 3, 8, _ => NoLinks, Anywhere, out _) ?? new List<int>()), "right three times, each push stopping at the next node");
            Expect(true, PassiveStick.Route(line, 0, 3, 2, _ => NoLinks, Anywhere, out var ranOut) == null && ranOut, "too far for the moves allowed, and said so");
            var corner = new Vector2[] { new(0, 0), new(100, 0), new(200, 0), new(200, -100) };
            Expect("0 1", string.Join(" ", PassiveStick.Route(corner, 0, 3, 8, _ => NoLinks, Anywhere, out _) ?? new List<int>()), "right (the near node at exactly 45 degrees is in up-right's cone and wins it), then up-right");
        }

        /// <summary>Screen offsets from angles in degrees counter-clockwise from right and distances, y down.</summary>
        private static Vector2[] Offsets(params (double Angle, double Distance)[] polar) =>
            polar.Select(node => Offset(node.Angle, node.Distance)).ToArray();

        private static Vector2 Offset(double angle, double distance)
        {
            var radians = angle * Math.PI / 180;
            return new Vector2((float)(distance * Math.Cos(radians)), (float)(-distance * Math.Sin(radians)));
        }

        /// <summary>
        ///     The recorded pushes. The recordings hold no edge order, so the links go in by the game's own linked score
        ///     (the order the three pushes with two links in the cone, down from armour_and_evasion2, agree with); the live
        ///     protocol in docs/research/passive-tree.md confirms the order read from the record.
        /// </summary>
        private static IEnumerable<RecordedPush> Pushes()
        {
            PassiveStick.Window? window = null;
            foreach (var line in File.ReadLines(FixturePath()))
            {
                if (line.StartsWith("# window ", StringComparison.Ordinal))
                {
                    var header = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    window = new PassiveStick.Window(new Vector2(Single(header[5]), Single(header[6])), new Vector2(Single(header[2]), Single(header[3])), PixelsPerPixel);
                    continue;
                }

                var fields = line.Split(" | ");
                var head = fields[0].Split(' ');
                var ids = new List<string>();
                var offsets = new List<Vector2>();
                var linked = new List<int>();
                foreach (var node in fields.Skip(1))
                {
                    var parts = node.Split(' ');
                    ids.Add(parts[0]);
                    offsets.Add(Offset(double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture)));
                    if (parts[3] == "L")
                    {
                        linked.Add(ids.Count - 1);
                    }
                }

                var octant = int.Parse(head[1], CultureInfo.InvariantCulture);
                var direction = PassiveStick.Direction(octant);
                linked.Sort((a, b) => LinkedScore(offsets[a], direction).CompareTo(LinkedScore(offsets[b], direction)));
                yield return new RecordedPush(head[0], octant, head[2], ids, offsets, linked, window ?? throw new Exception("a push before its window"));
            }
        }

        /// <summary>The game's linked score, the order the fixture's links are fed in (see <see cref="Pushes"/>).</summary>
        private static float LinkedScore(Vector2 offset, Vector2 direction)
        {
            var distance = offset.Length();
            var cosine = Vector2.Dot(offset / distance, direction);
            return (1.5f - ((cosine - 0.94f) * 8.333333f)) * distance;
        }

        private static float Single(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        /// <summary>The fixture under the repository: found from the working directory or above the build output.</summary>
        private static string FixturePath()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var directory = start; directory != null; directory = Path.GetDirectoryName(directory))
                {
                    var path = Path.Join(directory, "tests", "Poe2Accessibility.Tests", "Fixtures", FixtureName);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            throw new FileNotFoundException(FixtureName);
        }

        private sealed record RecordedPush(string From, int Octant, string Landed, List<string> Ids, List<Vector2> Offsets, List<int> Linked, PassiveStick.Window Window);
    }
}
