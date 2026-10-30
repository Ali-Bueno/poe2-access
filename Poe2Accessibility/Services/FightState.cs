namespace Poe2Accessibility.Services
{
    /// <summary>Whether a fight is on, for ground items or characters' lines to fall quiet (settings QuietItemsInFights, QuietChatInFights).</summary>
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
    }
}
