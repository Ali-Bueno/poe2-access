namespace Poe2Accessibility.Services
{
    /// <summary>Whether a fight is on, for ground items to fall quiet (settings QuietItemsInFights).</summary>
    public sealed class FightState
    {
        public const string RaresAndUniques = "rares";
        public const string AnyEnemy = "any";

        /// <summary>The choices in the settings menu's order.</summary>
        public static readonly string[] Modes = { RaresAndUniques, AnyEnemy, CullThreshold.Never };

        /// <summary>The fight outlasts the last enemy seen by this long, so loot does not flicker in while a boss leaps away.</summary>
        public const int HoldMs = 2000;

        private long? lastEngagedAt;

        /// <summary>An alive enemy in range starts a fight under this mode.</summary>
        public static bool Engages(string mode, bool rareOrUnique) => mode switch
        {
            AnyEnemy => true,
            RaresAndUniques => rareOrUnique,
            _ => false,
        };

        /// <summary>One look around: whether an enemy that engages was in range; returns whether the fight is on.</summary>
        public bool Update(bool engaged, long now)
        {
            if (engaged)
            {
                this.lastEngagedAt = now;
            }

            return this.IsOn(now);
        }

        public bool IsOn(long now) => this.lastEngagedAt is { } at && now - at < HoldMs;

        /// <summary>A new area or the setting turned off: no fight.</summary>
        public void Reset() => this.lastEngagedAt = null;
    }
}
