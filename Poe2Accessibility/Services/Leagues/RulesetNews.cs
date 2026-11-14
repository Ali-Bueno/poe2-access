namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>A Breach's ring as the client keeps it (<see cref="GameLayout.Rulesets"/>): where it is and how far it has grown.</summary>
    /// <param name="Centre">Its centre, in grid cells.</param>
    /// <param name="Value">How far it has grown, 0 to 100.</param>
    /// <param name="State">The encounter's state, as <c>encounter_state</c> of BrequelInitiator: 1 expanding, 2 enraged, 3 and 4 closing.</param>
    public readonly record struct BreachRing(Vector2 Centre, float Value, int State)
    {
        /// <summary>The ring's radius in grid cells, as the game draws it: 300 × (value / 100) ^ 0.6.</summary>
        public float Radius => BreachRings.RadiusOf(this.Value);
    }

    /// <summary>What a Breach's ring says to a player who cannot see it. Pure.</summary>
    public static class BreachRings
    {
        /// <summary>The radius of a ring fully grown, in grid cells (the constant of the game's draw).</summary>
        public const float FullRadius = 300f;

        /// <summary>A ring smaller than this is the hand just touched: nobody is "outside" it yet.</summary>
        public const float SmallestJudged = 20f;

        /// <summary>The ring's value from the game's own numbers: from one size to another over a time, clamped; null when they cannot be a ring's.</summary>
        public static float? ValueOf(float from, float to, float duration, float elapsed)
        {
            if (!float.IsFinite(from) || !float.IsFinite(to) || !float.IsFinite(duration) || !float.IsFinite(elapsed) ||
                from < 0f || from > 100f || to < 0f || to > 100f || duration < 0f || elapsed < 0f)
            {
                return null;
            }

            var part = duration <= 0f ? 1f : Math.Clamp(elapsed / duration, 0f, 1f);
            return from + ((to - from) * part);
        }

        public static float RadiusOf(float value) => FullRadius * MathF.Pow(Math.Clamp(value / 100f, 0f, 1f), 0.6f);

        /// <summary>The quarter of its growth a ring has reached: 0 to 4.</summary>
        public static int QuarterOf(float value) => Math.Clamp((int)(value / 25f), 0, 4);
    }

    /// <summary>A change of what the rulesets hold that is news.</summary>
    public enum RulesetLine
    {
        BreachQuarter,
        BreachHalf,
        BreachThreeQuarters,
        OutsideBreach,
        InsideBreach,
        TrialRound,
    }

    /// <summary>
    ///     The news of the client's rulesets: a Breach's ring growing and the player leaving it, a Trial of Chaos round. Each is
    ///     said on change only; what stands when first read in an area is not news. Pure.
    /// </summary>
    public sealed class RulesetNews
    {
        /// <summary>Fewest milliseconds between two lines about the ring's edge: a player walking along it hears it once.</summary>
        public const int EdgeGapMs = 3000;

        /// <summary>Inside below this share of the radius, outside above the next: no flutter on the line itself.</summary>
        private const float InsideShare = 0.9f;
        private const float OutsideShare = 1.05f;

        private int quarter = -1;
        private bool? inside;
        private long edgeSaid = long.MinValue / 2;
        private int? round;
        private string? objective;
        private int progressQuarter = -1;
        private int progressSaid;
        private Vector2? ringCentre;

        /// <summary>A new area: nothing has been read.</summary>
        public void Reset() =>
            (this.quarter, this.inside, this.edgeSaid, this.round, this.objective, this.progressQuarter, this.progressSaid, this.ringCentre) =
                (-1, null, long.MinValue / 2, null, null, -1, 0, null);

        /// <summary>The lines of the ring nearest the player while it grows; none for a ring closing or not there.</summary>
        public List<RulesetLine> Breach(BreachRing? ring, Vector2 player, long now)
        {
            var lines = new List<RulesetLine>();
            if (ring is not { State: 1 or 2 } open)
            {
                (this.quarter, this.inside, this.ringCentre) = (-1, null, null);
                return lines;
            }

            if (this.ringCentre != open.Centre)
            {
                // Another ring than the one last looked at: what was known of that one says nothing of this.
                (this.quarter, this.inside, this.ringCentre) = (-1, null, open.Centre);
            }

            var reached = BreachRings.QuarterOf(open.Value);
            if (this.quarter >= 0 && reached > this.quarter && reached is >= 1 and <= 3)
            {
                lines.Add(reached == 1 ? RulesetLine.BreachQuarter : reached == 2 ? RulesetLine.BreachHalf : RulesetLine.BreachThreeQuarters);
            }

            this.quarter = Math.Max(this.quarter, reached);
            var radius = open.Radius;
            if (radius < BreachRings.SmallestJudged)
            {
                return lines;
            }

            var distance = Vector2.Distance(player, open.Centre);
            bool? side = distance < radius * InsideShare ? true : distance > radius * OutsideShare ? false : null;
            if (side is not { } nowInside)
            {
                return lines; // on the line itself: as before
            }

            if (this.inside is not { } was)
            {
                // Only once the player has been inside: a ring first read from afar is nothing left.
                this.inside = nowInside ? true : null;
            }
            else if (nowInside != was && now - this.edgeSaid >= EdgeGapMs)
            {
                // Told when it can be said, so what was last heard is where the player stands.
                lines.Add(nowInside ? RulesetLine.InsideBreach : RulesetLine.OutsideBreach);
                (this.inside, this.edgeSaid) = (nowInside, now);
            }

            return lines;
        }

        /// <summary>
        ///     The round's objective when it is another than the one last said, the first of a Trial included: walking into the
        ///     room is when the player needs it. Null otherwise.
        /// </summary>
        public string? Objective(string? shown)
        {
            if (shown == null || shown == this.objective)
            {
                return null;
            }

            this.objective = shown;
            return shown;
        }

        /// <summary>
        ///     The quarter of the Trial's bar just reached, as a percentage (25, 50, 75); null otherwise. The bar going back, a new
        ///     round, starts it again; what it shows when first read is not news.
        /// </summary>
        public int? Progress(float? shown)
        {
            if (shown is not { } value)
            {
                this.progressQuarter = -1;
                return null;
            }

            var reached = Math.Clamp((int)(value * 4f), 0, 4);
            var first = this.progressQuarter < 0;
            this.progressQuarter = reached;
            if (value < EmptyBar)
            {
                this.progressSaid = 0; // the bar emptied: a new round's quarters are said anew
            }

            if (first)
            {
                this.progressSaid = reached; // what the bar shows when first read is not news
                return null;
            }

            // The highest quarter said in the round is kept: a bar wavering about a quarter says it once.
            if (reached <= this.progressSaid || reached is < 1 or > 3)
            {
                return null;
            }

            this.progressSaid = reached;
            return reached * 25;
        }

        /// <summary>A bar below this is a round beginning.</summary>
        private const float EmptyBar = 0.05f;

        /// <summary>The Trial of Chaos round now, counted from 1, when it has just changed; null otherwise, and for what stands when first read.</summary>
        public int? Round(int? shown)
        {
            var was = this.round;
            this.round = shown;
            if (shown != was)
            {
                this.progressSaid = 0;
            }

            return shown is { } now && was is { } before && now != before ? now : null;
        }
    }
}
