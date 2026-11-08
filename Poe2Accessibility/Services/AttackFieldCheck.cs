namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>How far a field the dodge warning reads is trusted.</summary>
    public enum FieldTrust
    {
        /// <summary>Not enough evidence yet: read, but the old rules decide.</summary>
        Unjudged,

        Intact,

        /// <summary>Its readings do not hold what it must: not read again this session; the old rules decide.</summary>
        Lost,
    }

    /// <summary>
    ///     Pairs each monster's clock readings far enough apart to judge a rate: frame to frame the game's own animation updates
    ///     (a monster's clip moved in steps the host's 60 frames do not match) and the millisecond clock's 15.6 ms ticks made
    ///     most pairs run at twice or none of their rate, so the clock was judged lost in Clearfell (2026-11-08, imps and hags).
    ///     Pure.
    /// </summary>
    public sealed class ClockBaseline
    {
        /// <summary>Readings at least this far apart (ms) are compared; a step of the game's updates is then a small share.</summary>
        public const long BaselineMs = 200;

        private readonly Dictionary<uint, (AnimationReading Reading, long At)> bases = new();

        /// <summary>A monster's new reading: the verdict of the pair it closes (null: none closed, or it said nothing).</summary>
        public bool? Offer(uint id, AnimationReading reading, long now)
        {
            if (!this.bases.TryGetValue(id, out var start) || reading.Clip < 0 || AttackClock.NewSwing(start.Reading, reading))
            {
                this.bases[id] = (reading, now);
                return null;
            }

            if (now - start.At < BaselineMs)
            {
                return null;
            }

            this.bases[id] = (reading, now);
            return AttackFieldCheck.ClockAdvances(start.Reading, reading, now - start.At);
        }

        public void Clear() => this.bases.Clear();
    }

    /// <summary>
    ///     The live checks of the fields behind the dodge warning (the animation controller, the clip's events, the current
    ///     action), each a rule the value must keep, so a patch that moves one is noticed and the warning falls back to the
    ///     old rules instead of reading garbage. Pure (docs/research/dodge-timing.md, "Protections").
    /// </summary>
    public sealed class AttackFieldCheck
    {
        /// <summary>Comparable readings a verdict needs.</summary>
        public const int MinSamples = 20;

        /// <summary>
        ///     Clip events a verdict needs: an id equal to the hash of the name read beside it is one chance in 32768 by accident, and
        ///     each clip is read once, so twenty took most of a fight's clips to come (Mud Burrow, 2026-11-08: unjudged after ten minutes).
        /// </summary>
        public const int EventSamples = 6;

        /// <summary>The share of comparable readings that must keep the rule for the field to be intact; at or below the rest it is lost.</summary>
        private const double KeptShare = 0.8;

        private const double LostShare = 0.2;

        /// <summary>The clock may run this much off its rate between two frames (rounding, a frame caught mid-update).</summary>
        private const double RateSlack = 0.25;

        /// <summary>Readings this close together (ms) say nothing about a rate.</summary>
        private const double MinElapsedMs = 10;

        /// <summary>A position must advance at least this much (seconds) to count, else a paused clip proves nothing.</summary>
        private const float MinAdvance = 0.005f;

        private readonly int minSamples;
        private int kept;
        private int broken;

        public AttackFieldCheck(int minSamples = MinSamples) => this.minSamples = minSamples;

        public FieldTrust Trust { get; private set; }

        /// <summary>
        ///     Whether a playing clip's position advanced as its rate says between two readings <paramref name="elapsedMs"/>
        ///     apart; null when the pair says nothing (another clip, the clip started over, too close, a paused clip).
        /// </summary>
        public static bool? ClockAdvances(AnimationReading before, AnimationReading after, double elapsedMs)
        {
            if (before.Clip < 0 || after.Clip < 0)
            {
                return null; // nothing playing
            }

            if (!AttackClock.IsPlausible(before) || !AttackClock.IsPlausible(after))
            {
                return false;
            }

            if (AttackClock.NewSwing(before, after) || elapsedMs < MinElapsedMs || after.Position - before.Position < MinAdvance)
            {
                return null;
            }

            var measured = (after.Position - before.Position) * 1000.0 / elapsedMs;
            var rate = AttackClock.Rate(before);
            return Math.Abs(measured - rate) <= RateSlack * rate;
        }

        /// <summary>
        ///     An event read where the layout says: its id is the game's hash of its name. Null for a chain's end marker (no name,
        ///     id 0, at the clip's end), which says nothing: every named chain ends with one, so counting it as broken kept the
        ///     check unjudged at about half and the live clock unused (Clearfell, 2026-11-08).
        /// </summary>
        public static bool? EventLayoutHolds(string? name, ushort id) =>
            string.IsNullOrEmpty(name) && id == 0 ? null : !string.IsNullOrEmpty(name) && AttackClock.EventId(name) == id;

        /// <summary>An action read where the layout says: its type id is one of ActionTypes.dat's.</summary>
        public static bool ActionTypeKnown(ushort id, IReadOnlySet<ushort> actionTypes) => actionTypes.Contains(id);

        /// <summary>An action's target place is a place on the map near its monster (within <paramref name="maxWorld"/>).</summary>
        public static bool TargetPointPlausible(Vector2 target, Vector2 attacker, float maxWorld) =>
            float.IsFinite(target.X) && float.IsFinite(target.Y) && Vector2.Distance(target, attacker) <= maxWorld;

        /// <summary>One judged reading (null: it said nothing); the verdict settles once enough comparable readings came.</summary>
        public FieldTrust Observe(bool? holds)
        {
            if (holds is not { } held || this.Trust != FieldTrust.Unjudged)
            {
                return this.Trust;
            }

            if (held)
            {
                this.kept++;
            }
            else
            {
                this.broken++;
            }

            var total = this.kept + this.broken;
            if (total >= this.minSamples)
            {
                var share = this.kept / (double)total;
                this.Trust = share >= KeptShare ? FieldTrust.Intact : share <= LostShare ? FieldTrust.Lost : FieldTrust.Unjudged;
            }

            return this.Trust;
        }
    }
}
