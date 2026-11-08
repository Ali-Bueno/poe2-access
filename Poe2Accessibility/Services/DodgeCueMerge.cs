namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     One dodge cue for blows that land together (docs/research/dodge-timing.md, "One cue for blows together"): a cue played
    ///     for a blow due at T covers a later warning whose blow falls within <see cref="WindowMs"/> of T, and warnings with no blow
    ///     time merge by when they are issued. Of one frame's warnings the most dangerous plays first: the rarer, then the nearer.
    ///     A rare's, unique's or boss's warning is never covered by a lesser monster's cue. Pure.
    /// </summary>
    public sealed class DodgeCueMerge
    {
        /// <summary>Blows this close together are one roll.</summary>
        public const int WindowMs = 250;

        /// <summary>The rank of a rare monster: from here up a warning is covered only by a cue of the same standing.</summary>
        public const int RareRank = 2;

        /// <summary>Cues older than this cover nothing more (a blow is at most a lead and a window after its cue).</summary>
        private const int KeepMs = 2000;

        private readonly List<Candidate> played = new();

        /// <param name="Id">The attacking monster.</param>
        /// <param name="Rank">0 normal, 1 magic, 2 rare, 3 unique (<see cref="RankOf"/>).</param>
        /// <param name="Distance">How far the monster is (world units).</param>
        /// <param name="BlowAt">When the blow lands (ms clock); null for a wind-up warned before its time is known.</param>
        /// <param name="IssuedAt">When the warning was given (ms clock).</param>
        public readonly record struct Candidate(uint Id, int Rank, float Distance, long? BlowAt, long IssuedAt);

        /// <summary>A warning and the monster whose cue covers it; null when its own cue plays.</summary>
        public readonly record struct Outcome(Candidate Warning, uint? CoveredBy);

        /// <summary>The rank of a monster rarity (Normal, Magic, Rare, Unique as the game numbers them).</summary>
        public static int RankOf(int rarity) => Math.Clamp(rarity, 0, 3);

        /// <summary>A new area: no cue covers anything.</summary>
        public void Reset() => this.played.Clear();

        /// <summary>One frame's warnings, the most dangerous first in the result; each plays or is covered by a cue already played.</summary>
        public IReadOnlyList<Outcome> Resolve(IEnumerable<Candidate> frame, long now)
        {
            this.played.RemoveAll(cue => now - cue.IssuedAt > KeepMs);
            var outcomes = new List<Outcome>();
            foreach (var warning in frame.OrderByDescending(candidate => candidate.Rank).ThenBy(candidate => candidate.Distance))
            {
                var cover = this.played.FindIndex(cue => Covers(cue, warning));
                if (cover >= 0)
                {
                    outcomes.Add(new Outcome(warning, this.played[cover].Id));
                    continue;
                }

                this.played.Add(warning);
                outcomes.Add(new Outcome(warning, null));
            }

            return outcomes;
        }

        private static bool Covers(Candidate cue, Candidate warning) =>
            (warning.Rank < RareRank || cue.Rank >= RareRank) &&
            (cue.BlowAt is { } played && warning.BlowAt is { } due
                ? Math.Abs(played - due) <= WindowMs
                : Math.Abs(cue.IssuedAt - warning.IssuedAt) <= WindowMs);
    }
}
