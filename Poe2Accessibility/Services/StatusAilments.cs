namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Harmful states the player can be in (see <see cref="StatusWatch"/>).</summary>
    public enum StatusAilment
    {
        Frozen,
        Chilled,
        Shocked,
        Electrocuted,
        Ignited,
        Poisoned,
        Bleeding,
        Stunned,
        Pinned,
        Slowed,
        Maimed,
        Blinded,
        Cursed,

        /// <summary>A boss's rule: stop moving or be imprisoned (the Voodoo King's voodoo_king_punishment_moving).</summary>
        StopMoving,

        /// <summary>A boss's rule: keep moving or be imprisoned (voodoo_king_punishment_stationary).</summary>
        KeepMoving,
    }

    /// <summary>Buff definition id → ailment, by its words (pure).</summary>
    public static class StatusAilments
    {
        /// <summary>
        ///     Words naming the state itself, as the game's BuffDefinitions ids spell them ("chilled", "ignited",
        ///     "stun_display_buff", "mud_slow", "curse_enfeeble"), matched as whole words of the id.
        /// </summary>
        private static readonly (string[] Words, StatusAilment Ailment)[] Names =
        {
            (new[] { "frozen" }, StatusAilment.Frozen),
            (new[] { "chill", "chilled" }, StatusAilment.Chilled),
            (new[] { "electrocute", "electrocuted" }, StatusAilment.Electrocuted),
            (new[] { "shock", "shocked" }, StatusAilment.Shocked),
            (new[] { "ignite", "ignited", "burning" }, StatusAilment.Ignited),
            (new[] { "poison", "poisoned" }, StatusAilment.Poisoned),
            (new[] { "bleed", "bleeding" }, StatusAilment.Bleeding),
            (new[] { "stun", "stunned" }, StatusAilment.Stunned),
            (new[] { "pinned" }, StatusAilment.Pinned),
            (new[] { "hinder", "hindered", "slow", "slowed" }, StatusAilment.Slowed),
            (new[] { "maim", "maimed" }, StatusAilment.Maimed),
            (new[] { "blind", "blinded" }, StatusAilment.Blinded),
            (new[] { "curse", "cursed" }, StatusAilment.Cursed),
        };

        /// <summary>
        ///     Words of ids that only mention a state: immunities and auras, build-up meters, counters and "recently"
        ///     markers, flask bonuses, effects on enemies, and vulnerabilities ("sprint_heavy_stunnable" and
        ///     "number_of_times_stunned_in_past_duration_from_ot" read as stuns, 2026-10-24).
        /// </summary>
        private static readonly HashSet<string> NotTheState = new(StringComparer.Ordinal)
        {
            "immunity", "immune", "aura", "avoid", "avoidance", "buildup", "chance", "prevention", "recently", "number",
            "count", "reserve", "cannot", "flask", "have", "was", "enemy", "enemies", "stunnable", "damage", "duration",
        };

        /// <summary>A boss's movement rule ("Stop moving or you will be imprisoned"): the id names what is punished.</summary>
        private const string PunishmentWord = "punishment";

        /// <summary>The ailment a buff id stands for; null for everything else (auras, flasks, charges, immunities).</summary>
        public static StatusAilment? FromBuffId(string id)
        {
            var words = id.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (words.Contains(PunishmentWord))
            {
                // Punished for moving: stand still; for standing: keep moving (BuffDefinitions descriptions).
                return words.Contains("moving") ? StatusAilment.StopMoving : words.Contains("stationary") ? StatusAilment.KeepMoving : null;
            }

            if (words.Any(NotTheState.Contains))
            {
                return null;
            }

            foreach (var (names, ailment) in Names)
            {
                if (words.Any(word => Array.IndexOf(names, word) >= 0))
                {
                    return ailment;
                }
            }

            return null;
        }
    }
}
