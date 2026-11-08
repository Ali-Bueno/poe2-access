namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Where the time to a blow came from, best last.</summary>
    public enum BlowTiming
    {
        /// <summary>Not known: the warning sounds at the wind-up.</summary>
        None,

        /// <summary>Learnt from earlier blows, or the files' nominal time (speed 1 from the attack's start).</summary>
        Nominal,

        /// <summary>A ground telegraph's own hit time (<see cref="AttackTelegraph"/>).</summary>
        Telegraph,

        /// <summary>The playing clip's own event, at the speed it plays (<see cref="AttackClock"/>).</summary>
        Live,
    }

    /// <summary>Whether the dodge warning sounds for one monster's attack now, later, or not at all.</summary>
    public enum DodgeVerdict
    {
        Silent,
        Wait,
        Warn,
    }

    /// <summary>
    ///     What the game draws on the ground for an attack (the telegraph study, docs/research/attack-telegraphs.md), when a
    ///     telegraph source knows: whether it is on screen, whether its area catches the player, and when it hits.
    /// </summary>
    public readonly record struct AttackTelegraph(bool Seen, bool? CatchesPlayer, int? HitInMs);

    /// <summary>What is known of one attack this frame; a field the mod could not read is Unknown or null and falls back.</summary>
    /// <param name="Threat">The attack's kind (<see cref="DodgeTimings.Threat"/>, else <see cref="DodgeThreats"/>).</param>
    /// <param name="Aim">At whom the action is aimed (<see cref="AttackAim"/>).</param>
    /// <param name="InReach">Whether the blow reaches the player (<see cref="AttackReach"/>); null when not known.</param>
    /// <param name="FallbackInReach">The reach rule used when the aim or the reach is not known (rarity and body gap).</param>
    /// <param name="Seen">Whether the attacker's body is on screen (<see cref="AttackSight.BodySeen"/>).</param>
    /// <param name="SeenInMs">From an attacker off screen: in how long its projectile comes into view; null when nothing of it shows.</param>
    /// <param name="BlowInMs">In how long the blow lands (a projectile: reaches the player); null when not known.</param>
    /// <param name="Timing">Where <paramref name="BlowInMs"/> came from.</param>
    /// <param name="Telegraph">The attack's ground telegraph, when a telegraph source knows it.</param>
    public readonly record struct DodgeFacts(
        DodgeThreat Threat, AimKind Aim, bool? InReach, bool FallbackInReach, bool Seen, int? SeenInMs, int? BlowInMs, BlowTiming Timing,
        AttackTelegraph? Telegraph = null);

    /// <summary>The verdict, in how long the warning is due (0 for now), and why, for the log and the live protocol's counts.</summary>
    public readonly record struct DodgeDecision(DodgeVerdict Verdict, int InMs, string Reason);

    /// <summary>
    ///     The dodge warning's rules: only an attack aimed at the player (or whose area, path or sweep catches the player),
    ///     within reach, that a sighted player would see, timed a lead before its real blow. Pure (docs/research/dodge-timing.md).
    /// </summary>
    public static class DodgeGate
    {
        public static DodgeDecision Decide(in DodgeFacts facts, int leadMs)
        {
            if (facts.Threat == DodgeThreat.None)
            {
                return Silent("calm");
            }

            // What the ground shows wins over the guesses: an area drawn under the player reaches it, one drawn elsewhere does not.
            var reach = facts.Telegraph?.CatchesPlayer ?? facts.InReach;
            if (facts.Aim == AimKind.Elsewhere && reach != true)
            {
                return Silent("aimed elsewhere");
            }

            // Unknown reach: a ranged attack aimed at the player is taken to reach it; a melee blow, or an unknown aim, keeps the old rule.
            var reaches = reach ?? ((facts.Aim != AimKind.Unknown && facts.Threat != DodgeThreat.Melee) || facts.FallbackInReach);
            if (!reaches)
            {
                return Silent("out of reach");
            }

            var seen = facts.Seen || facts.Telegraph?.Seen == true;
            if (!seen && facts.SeenInMs == null)
            {
                return Silent("off screen");
            }

            var seenIn = seen ? 0 : facts.SeenInMs ?? 0;
            var (blowIn, timing) = facts.BlowInMs is { } live && facts.Timing != BlowTiming.None ? (live, facts.Timing)
                : facts.Telegraph?.HitInMs is { } hit ? (hit, BlowTiming.Telegraph) : (0, BlowTiming.None);
            if (timing != BlowTiming.None)
            {
                if (blowIn <= 0)
                {
                    return Silent("blow passed");
                }

                var due = Math.Max(Math.Max(0, blowIn - leadMs), seenIn);
                return due >= blowIn ? Silent("seen too late") : Due(due, Reason(timing));
            }

            // Untimed: at the wind-up, or as its projectile comes into view.
            return Due(seenIn, "wind-up");
        }

        private static string Reason(BlowTiming timing) => timing switch
        {
            BlowTiming.Live => "timed live",
            BlowTiming.Telegraph => "timed by its telegraph",
            _ => "timed nominal",
        };

        private static DodgeDecision Due(int inMs, string reason) =>
            inMs <= 0 ? new DodgeDecision(DodgeVerdict.Warn, 0, reason) : new DodgeDecision(DodgeVerdict.Wait, inMs, reason);

        private static DodgeDecision Silent(string reason) => new(DodgeVerdict.Silent, 0, reason);
    }
}
