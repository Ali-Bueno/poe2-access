namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Natives;

    /// <summary>
    ///     A boss's long blast: an attack whose blow lands so far ahead that the player can walk out of its reach, and whose own
    ///     skill draws no area while the skills it sets off draw a wide circle around the boss (<see cref="AttackData.BlastRadius"/>).
    ///     Doryani's Triumph's self-destruct is one: the files time its smash at 10.19 s of <c>floating_explode_mech_01</c>
    ///     (hit-times v6, the 10.2 s of both hits in the owner's log of 2026-11-21), 64 cells (700 world units) around the
    ///     hidden machine, which follows the player, in an arena of about 80 cells (docs/research/boss-mechanics.md, Doryani).
    ///     The wind-up says how long is left; every <see cref="BlastGuideGapMs"/> while the player stands in reach of where the
    ///     boss now is, the way straight away from it; once out of reach, that it is so. The boss's vanishing in it is not said:
    ///     it is the blast, not a pause.
    /// </summary>
    public sealed partial class BossMechanicsWatch
    {
        /// <summary>The way away is said again this often (ms) while the player is still in reach: the boss moves, so does the player.</summary>
        private const int BlastGuideGapMs = 1500;

        /// <summary>A blast is forgotten this long (ms) after its blow: the boss's coming back is not news either.</summary>
        private const int BlastForgetMs = 2500;

        private readonly Dictionary<(uint Id, long StartedAt), PendingBlast> blastsPending = new();
        private readonly HashSet<uint> blastHidden = new();

        /// <summary>Whether long blasts are guided at all (<see cref="AccessibilitySettings.BlastWarnings"/>).</summary>
        public Func<bool> BlastOn { get; set; } = () => true;

        /// <summary>
        ///     The pending long blast nearest its blow: where its boss was last seen, how long is left (ms) and whether the player
        ///     stood in its reach then; null when none is pending (<see cref="BlastCue"/>).
        /// </summary>
        public (Vector2 At, int MsLeft, bool InReach)? BlastCountdown
        {
            get
            {
                var now = Environment.TickCount64;
                var next = this.blastsPending.Values.Where(blast => blast.BlowAt > now).OrderBy(blast => blast.BlowAt).FirstOrDefault();
                return next == null ? null : (next.At, (int)(next.BlowAt - now), next.InReach);
            }
        }

        /// <summary>Whether a boss is in the middle of a long blast: its vanishing and coming back are not said.</summary>
        private bool InBlast(uint id, long now) => this.blastsPending.Any(pair => pair.Key.Id == id && now <= pair.Value.BlowAt + BlastForgetMs);

        /// <summary>
        ///     Each frame of a unique's attack: a long blast (<see cref="BlastGuide.LongBlastMs"/>) is announced at its start, then
        ///     the player is led out of its reach: out of a circle of <paramref name="radius"/> around <paramref name="monster"/>,
        ///     where the boss stands now (a self-destruct), else out of the attack's own or set-off <paramref name="shaped"/> area
        ///     aimed at <paramref name="target"/> (Zalmarath's beam, a cone of 150 cells at 5.8 s).
        /// </summary>
        public void Blast(uint id, bool unique, long startedAt, int? blowInMs, float? radius, AttackArea? shaped, Vector2 monster, Vector2 target,
                          Vector2 player, float playerRadius, StdTuple3D<float> origin, string? skill, long now)
        {
            foreach (var old in this.blastsPending.Where(pair => now > pair.Value.BlowAt + BlastForgetMs).Select(pair => pair.Key).ToList())
            {
                this.blastsPending.Remove(old);
            }

            var key = (id, startedAt);
            var isNew = !this.blastsPending.TryGetValue(key, out var blast);
            if (isNew)
            {
                var area = BlastGuide.Guided(shaped);
                if (!this.BlastOn() || !BlastGuide.Starts(unique, radius ?? area?.Length, blowInMs))
                {
                    return;
                }

                blast = new PendingBlast(now + blowInMs!.Value, radius, radius == null ? area : null);
                this.blastsPending[key] = blast;
            }

            var word = this.BlastWord(blast!, monster, target, player, playerRadius, origin);
            blast!.At = monster;
            blast.InReach = word != null;
            if (isNew)
            {
                var seconds = (blowInMs!.Value + 500) / 1000;
                this.output.Urge(Announcements.LongBlast(seconds), WordHoldMs);
                this.lastWordAt = now;
                blast.GuidedAt = now;
                this.log.Info($"Boss blast: monster {id} {skill ?? "?"} lands in {blowInMs} ms, " +
                              (blast.Radius is { } around ? $"{around:0} world units around it" : $"{blast.Area?.Shape} of {blast.Area?.Length:0}") +
                              $"; the player is {Vector2.Distance(player, monster):0} away, {(word == null ? "out of" : "in")} its reach");
                return;
            }

            if (now - blast.GuidedAt < BlastGuideGapMs || blast.BlowAt - now < MinWordLeadMs)
            {
                return;
            }

            blast.GuidedAt = now;
            if (word != null)
            {
                blast.Out = false;
                this.output.Urge(word, WordHoldMs);
                this.lastWordAt = now;
                this.log.Info($"Boss blast: {Vector2.Distance(player, monster):0} from monster {id}, {blast.BlowAt - now} ms left -> {word}");
            }
            else if (!blast.Out)
            {
                blast.Out = true;
                this.output.Urge(Announcements.OutOfBlast, WordHoldMs);
                this.lastWordAt = now;
                this.log.Info($"Boss blast: out of reach, {Vector2.Distance(player, monster):0} from monster {id}, {blast.BlowAt - now} ms left");
            }
        }

        /// <summary>The way out of a pending blast for the player where they stand; null when out of its reach.</summary>
        private string? BlastWord(PendingBlast blast, Vector2 monster, Vector2 target, Vector2 player, float playerRadius, StdTuple3D<float> origin)
        {
            if (blast.Radius is { } radius)
            {
                var gap = Vector2.Distance(player, monster);
                var steps = BlastGuide.StepsOut(radius, gap, playerRadius);
                var away = gap > 0f ? (player - monster) / gap : Vector2.UnitX;
                return steps > 0 ? Announcements.BlastAway(ScreenProjection.Octant(origin, away * ProbeWorld), steps) : null;
            }

            if (blast.Area is not { } area || BossCounterplay.Of(area, monster, target, player, playerRadius, AttackAim.MarginWorld) is not { } answer)
            {
                return null;
            }

            return Announcements.Counterplay(answer.Kind, answer.Kind == CounterplayKind.Step ? ScreenProjection.Octant(origin, answer.Way * ProbeWorld) : 0);
        }

        private sealed class PendingBlast
        {
            public PendingBlast(long blowAt, float? radius, AttackArea? area)
            {
                this.BlowAt = blowAt;
                this.Radius = radius;
                this.Area = area;
            }

            public long BlowAt { get; }

            /// <summary>A circle around the boss (world units), or null for <see cref="Area"/>.</summary>
            public float? Radius { get; }

            /// <summary>The attack's own or set-off area, aimed at its target.</summary>
            public AttackArea? Area { get; }

            public long GuidedAt { get; set; }

            public bool Out { get; set; }

            /// <summary>Where the boss was at the last frame of its attack.</summary>
            public Vector2 At { get; set; }

            /// <summary>Whether the player stood in its reach then.</summary>
            public bool InReach { get; set; }
        }
    }
}
