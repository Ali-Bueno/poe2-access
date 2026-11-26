namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>A way out of a fork that went on when the player was there, not explored since (grid cells).</summary>
    public sealed record PendingWay(int Node, string Place, Vector2 Centre, Way Way);

    /// <summary>
    ///     The ways of the forks walked through that still go on: what a sighted player sees left on the minimap and the
    ///     owner forgot going on fast (2026-11-21, Aggorat). Told when the fork is left behind, at a dead end, at an exit of
    ///     the area. Pure; docs/radar.md "Ways left pending".
    /// </summary>
    public sealed class PendingWays
    {
        /// <summary>A way within this angle (60°) of where the player went from the fork is the way taken.</summary>
        private const float TakenCosine = 0.5f;

        private readonly float leaveCells;
        private readonly List<PendingWay> ways = new();
        private readonly List<(int Node, Vector2 Centre, float Radius)> leaving = new();

        /// <param name="leaveCells">A fork is left once the player is this far past its radius.</param>
        public PendingWays(float leaveCells) => this.leaveCells = leaveCells;

        public int Count => this.ways.Count;

        /// <summary>The pending ways but those standing where <paramref name="hidden"/> says (ground apart, docs/research/map-pieces.md).</summary>
        public int CountShown(Func<Vector2, bool>? hidden) => hidden == null ? this.ways.Count : this.ways.Count(way => !hidden(way.Way.Ahead));

        public IReadOnlyList<PendingWay> All => this.ways;

        /// <summary>A new area or map shape: nothing is pending.</summary>
        public void Reset()
        {
            this.ways.Clear();
            this.leaving.Clear();
        }

        /// <summary>The player is at a fork: its ways going on now (the way back left out) replace what it had pending.</summary>
        public void Note(int node, string place, Vector2 centre, float radius, IEnumerable<Way> onward)
        {
            this.ways.RemoveAll(way => way.Node == node);
            this.ways.AddRange(onward.Select(way => new PendingWay(node, place, centre, way)));
            this.leaving.RemoveAll(fork => fork.Node == node);
            this.leaving.Add((node, centre, radius));
        }

        /// <summary>
        ///     Once per fork walked through, as the player gets past it: the ways still pending there but the one taken (it stays
        ///     pending until explored); null while still at the fork, and when nothing else is left there.
        /// </summary>
        /// <param name="player">The player's cell.</param>
        /// <param name="hidden">Ground not told now: a fork there was left by a carry, not a walk, and is let go unsaid.</param>
        public IReadOnlyList<PendingWay>? Leave(Vector2 player, Func<Vector2, bool>? hidden = null)
        {
            if (hidden != null)
            {
                this.leaving.RemoveAll(fork => hidden(fork.Centre));
            }

            var passed = this.leaving.FindIndex(fork => Vector2.Distance(player, fork.Centre) > fork.Radius + this.leaveCells);
            if (passed < 0)
            {
                return null;
            }

            var fork = this.leaving[passed];
            this.leaving.RemoveAt(passed);
            var here = this.ways.Where(way => way.Node == fork.Node).ToList();
            var went = Vector2.Normalize(player - fork.Centre);
            var taken = here.Select(way => (Pending: way, Dot: Vector2.Dot(Vector2.Normalize(way.Way.Direction), went)))
                .Where(way => way.Dot >= TakenCosine).OrderByDescending(way => way.Dot).Select(way => way.Pending).FirstOrDefault();
            var left = here.Where(way => !ReferenceEquals(way, taken)).ToList();
            return left.Count == 0 ? null : left;
        }

        /// <summary>Forgets the ways explored or seen to end since.</summary>
        public void Drop(Func<PendingWay, bool> done) => this.ways.RemoveAll(way => done(way));

        /// <summary>The pending way whose start is nearest the player, leaving out <paramref name="exceptNode"/>'s; null for none.</summary>
        public PendingWay? Nearest(Vector2 player, int? exceptNode = null, Func<Vector2, bool>? hidden = null) =>
            this.ways.Where(way => way.Node != exceptNode && hidden?.Invoke(way.Way.Ahead) != true).OrderBy(way => Vector2.Distance(player, way.Way.Ahead)).FirstOrDefault();
    }
}
