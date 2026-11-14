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

        /// <summary>The crossbow at Rapid Shot's maximum heat: no firing or reloading those bolts for a while.</summary>
        Overheated,

        /// <summary>A Trial's Resolve Weakness: 50% more honour lost.</summary>
        HonourWeakness,

        /// <summary>A Trial's Binding Chains, slowing the player.</summary>
        BindingChains,
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
            (new[] { "overheated" }, StatusAilment.Overheated),
            (new[] { "resolve" }, StatusAilment.HonourWeakness),
            (new[] { "binding" }, StatusAilment.BindingChains),
            (new[] { "curse", "cursed" }, StatusAilment.Cursed),
        };

        /// <summary>
        ///     Words of ids that only mention a state: immunities and auras, build-up meters, counters and "recently"
        ///     markers, flask bonuses, effects on enemies, and vulnerabilities ("sprint_heavy_stunnable" and
        ///     "number_of_times_stunned_in_past_duration_from_ot" read as stuns, 2026-10-24). The second line came from running
        ///     every BuffDefinitions id through this (2026-11-06): what a state grants ("life_regeneration_from_curse",
        ///     "fire_resistance_from_ignite"), supports and passives that only mention one.
        /// </summary>
        private static readonly HashSet<string> NotTheState = new(StringComparer.Ordinal)
        {
            "immunity", "immune", "aura", "avoid", "avoidance", "buildup", "chance", "prevention", "recently", "number",
            "count", "reserve", "cannot", "flask", "have", "was", "enemy", "enemies", "stunnable", "damage", "duration",
            "from", "support", "tracker", "while", "unravelling", "placeholder", "delay", "enrage",
        };

        /// <summary>An end the player acts on (free to move, the weapon ready) is said once the state has been gone this long.</summary>
        private const int PromptEndHoldMs = 500;

        /// <summary>
        ///     An end that only informs waits this long, so a state the next hit renews stays one state: of 213 ends said
        ///     in the play log of 2026-11-13, the same state was back within 2 s after 38 (docs/research/sweep-2026-11-13.md).
        /// </summary>
        private const int RenewedEndHoldMs = 2500;

        /// <summary>How long a state must be gone before its end is said.</summary>
        public static int EndHoldMs(StatusAilment ailment) => ailment switch
        {
            StatusAilment.Frozen or StatusAilment.Electrocuted or StatusAilment.Stunned or StatusAilment.Pinned or
                StatusAilment.StopMoving or StatusAilment.KeepMoving or StatusAilment.Overheated => PromptEndHoldMs,
            _ => RenewedEndHoldMs,
        };

        /// <summary>A boss's movement rule ("Stop moving or you will be imprisoned"): the id names what is punished.</summary>
        private const string PunishmentWord = "punishment";

        /// <summary>The ailment a buff id stands for; null for everything else (auras, flasks, charges, immunities).</summary>
        public static StatusAilment? FromBuffId(string id)
        {
            if ((BossMechanicsCatalog.Shared.MoveRule(id) ?? BossMechanicsCatalog.Shared.MoveRule(BuffIds.Base(id))) is { } rule)
            {
                return rule; // the files' own words: "Stop moving or…", "Keep moving…", "Moving breaks the vines"
            }

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
