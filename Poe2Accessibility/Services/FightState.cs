namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>Whether a fight is on, for loot or characters' lines to fall quiet (settings QuietItemsInFights, QuietChatInFights).</summary>
    public sealed class FightState
    {
        public const string RaresAndUniques = "rares";
        public const string AnyEnemy = "any";

        /// <summary>The choices in the settings menu's order.</summary>
        public static readonly string[] Modes = { RaresAndUniques, AnyEnemy, CullThreshold.Never };

        /// <summary>The fight outlasts the last enemy seen by this long, so loot does not flicker in while a boss leaps away.</summary>
        public const int HoldMs = 2000;

        private long? lastEngagedAt;

        /// <summary>One look around: whether an enemy this fight counts was in range; returns whether the fight is on.</summary>
        public bool Update(bool engaged, long now)
        {
            if (engaged)
            {
                this.lastEngagedAt = now;
            }

            return this.IsOn(now);
        }

        public bool IsOn(long now) => this.lastEngagedAt is { } at && now - at < HoldMs;

        /// <summary>
        ///     What keeps quiet in a fight (setting QuietItemsInFights): items on the ground, chests, bodies, breakables and
        ///     league altars (user 2026-10-31); exits, waypoints, shrines and characters still guide.
        /// </summary>
        public static bool QuietsInFight(ScanCategory category, string kind) =>
            category is ScanCategory.Items or ScanCategory.Encounters || kind is "chest" or "corpse" or "breakable";
    }

    /// <summary>
    ///     Which enemies can make a fight: those in play. A boss waits in its arena alive, hostile and out of play until the
    ///     player walks in, and Lachlann, 56 cells from a ritual altar, kept it and all the loot around quiet for good (user
    ///     2026-11-04). One that has been in play still counts for a while out of it: a boss between phases.
    /// </summary>
    public sealed class FightRoster
    {
        /// <summary>Long enough for a boss's phase change (not measured); one that never comes back must not quiet the area for good.</summary>
        public const int OutOfPlayMs = 20000;

        private readonly Dictionary<uint, long> lastInPlay = new();

        public bool Counts(uint id, bool inPlay, long now)
        {
            if (inPlay)
            {
                this.lastInPlay[id] = now;
                return true;
            }

            return this.lastInPlay.TryGetValue(id, out var at) && now - at < OutOfPlayMs;
        }

        /// <summary>Forgets those past the hold: the next area uses the same ids.</summary>
        public void Prune(long now)
        {
            foreach (var (id, at) in this.lastInPlay)
            {
                if (now - at >= OutOfPlayMs)
                {
                    this.lastInPlay.Remove(id);
                }
            }
        }
    }
}
