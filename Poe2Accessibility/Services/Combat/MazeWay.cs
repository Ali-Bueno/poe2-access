namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>A rock of a boss's maze: <paramref name="Right"/> and <paramref name="Left"/> mark the two beside an opening.</summary>
    public readonly record struct MazeRock(Vector2 At, bool Left, bool Right);

    /// <summary>Where to walk next through a boss's maze: the point, and which wall it opens (-1 past the last).</summary>
    public readonly record struct MazeStep(Vector2 At, int Wall);

    /// <summary>
    ///     The way through the walls Benedictus raises, rings around him or lines across the arena (docs/research/boss-mechanics.md,
    ///     "Benedictus"): each wall's opening is between the rocks the game marks `right` and `left`. Pure.
    /// </summary>
    public static class MazeWay
    {
        /// <summary>Rocks of one wall differ by less than this across it (world units): walls stand 250 to 360 apart, rocks of one wall are off by 11.</summary>
        private const float SameWallWorld = 100f;

        /// <summary>About a rock's half depth and the body (world units): in the wall's own thickness.</summary>
        private const float DepthWorld = 60f;

        /// <summary>A wall is behind once the player is this far past it, so a turn right after it does not cut its rocks.</summary>
        private const float PastWorld = 100f;

        /// <summary>The way runs this far from a wall, before it and after it.</summary>
        private const float ClearWorld = 120f;

        /// <summary>Within this of the opening's middle the way goes straight through it (its half is 190, less the rock and the body).</summary>
        private const float LinedUpWorld = 80f;

        /// <summary>Along a ring the next point is at most this far round (radians): always walking at it, the way runs at its radius times the cosine of this.</summary>
        private const float RoundStep = 0.25f;

        /// <summary>The next point through lines of rocks, from the player toward <paramref name="goal"/> along <paramref name="forward"/>.</summary>
        public static MazeStep Lines(IReadOnlyList<MazeRock> rocks, Vector2 player, Vector2 forward, Vector2 goal)
        {
            var across = new Vector2(-forward.Y, forward.X);
            var here = Vector2.Dot(player, forward);
            var walls = Walls(rocks, rock => Vector2.Dot(rock.At, forward));
            for (var k = 0; k < walls.Count; k++)
            {
                var (at, wall) = walls[k];
                if (here > at + PastWorld || at > Vector2.Dot(goal, forward))
                {
                    continue;
                }

                if (Opening(wall, rock => Vector2.Dot(rock.At, across), closed: null) is not { } opening)
                {
                    continue;
                }

                var off = MathF.Abs(Vector2.Dot(player, across) - opening);
                if (here > at + DepthWorld && off > LinedUpWorld)
                {
                    continue; // aside of it and no longer in it: it was passed
                }

                // Lined up, or in the opening already: straight through; else along the wall first.
                var ahead = off <= LinedUpWorld || here > at - DepthWorld ? at + ClearWorld : MathF.Max(here, at - ClearWorld);
                return new MazeStep((forward * ahead) + (across * opening), k);
            }

            return new MazeStep((forward * Vector2.Dot(goal, forward)) + (across * Vector2.Dot(player, across)), -1);
        }

        /// <summary>The next point out of rings of rocks around <paramref name="centre"/>; null past the last.</summary>
        public static MazeStep? Rings(IReadOnlyList<MazeRock> rocks, Vector2 player, Vector2 centre)
        {
            var from = player - centre;
            var here = from.Length();
            var angle = MathF.Atan2(from.Y, from.X);
            var walls = Walls(rocks, rock => Vector2.Distance(rock.At, centre));
            for (var k = 0; k < walls.Count; k++)
            {
                var (radius, wall) = walls[k];
                if (here > radius + PastWorld || Opening(wall, rock => AngleOf(rock.At - centre), closed: 2f * MathF.PI) is not { } opening)
                {
                    continue;
                }

                var turn = Wrap(opening - angle);
                var through = MathF.Abs(turn) * here <= LinedUpWorld || here < 1f;
                if (here > radius + DepthWorld && !through)
                {
                    continue; // aside of it and no longer in it: it was passed
                }

                var inside = k == 0 ? 0f : walls[k - 1].At + ClearWorld;
                // Inside the first ring nothing is in the way: straight to before its opening; between rings, round them.
                var to = through ? opening : k == 0 ? opening : angle + Math.Clamp(turn, -RoundStep, RoundStep);
                var reach = through ? radius + ClearWorld : MathF.Max(inside, radius - ClearWorld);
                return new MazeStep(centre + (new Vector2(MathF.Cos(to), MathF.Sin(to)) * reach), k);
            }

            return null;
        }

        /// <summary>The walls by how far each is (along the way or from the centre), nearest first.</summary>
        private static List<(float At, List<MazeRock> Rocks)> Walls(IReadOnlyList<MazeRock> rocks, Func<MazeRock, float> distance)
        {
            var walls = new List<(float At, List<MazeRock> Rocks)>();
            foreach (var rock in rocks.OrderBy(distance))
            {
                var at = distance(rock);
                if (walls.Count == 0 || at - walls[^1].At > SameWallWorld)
                {
                    walls.Add((at, new List<MazeRock>()));
                }

                walls[^1].Rocks.Add(rock);
            }

            return walls;
        }

        /// <summary>The middle of a wall's opening along it: between its marked rocks, else its widest gap; <paramref name="closed"/> is the length of one that comes back to its start.</summary>
        private static float? Opening(List<MazeRock> wall, Func<MazeRock, float> along, float? closed)
        {
            if (wall.Any(rock => rock.Left) && wall.Any(rock => rock.Right))
            {
                var (left, right) = (along(wall.First(rock => rock.Left)), along(wall.First(rock => rock.Right)));
                return closed == null ? (left + right) / 2f : left + (Wrap(right - left) / 2f);
            }

            var places = wall.Select(along).OrderBy(place => place).ToList();
            if (places.Count < 2)
            {
                return null;
            }

            var gaps = Enumerable.Range(1, places.Count - 1).Select(i => (Width: places[i] - places[i - 1], Middle: (places[i] + places[i - 1]) / 2f)).ToList();
            if (closed is { } round)
            {
                gaps.Add((places[0] + round - places[^1], (places[0] + round + places[^1]) / 2f));
            }

            return gaps.MaxBy(gap => gap.Width).Middle;
        }

        private static float AngleOf(Vector2 way) => MathF.Atan2(way.Y, way.X);

        private static float Wrap(float angle) => MathF.IEEERemainder(angle, 2f * MathF.PI);
    }
}
