namespace Poe2Accessibility.Services
{
    using System;

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
    }

    /// <summary>Buff definition id → ailment, by stem (pure).</summary>
    public static class StatusAilments
    {
        private static readonly (string Stem, StatusAilment Ailment)[] Stems =
        {
            ("frozen", StatusAilment.Frozen), ("freeze", StatusAilment.Frozen),
            ("chill", StatusAilment.Chilled),
            ("electrocut", StatusAilment.Electrocuted),
            ("shock", StatusAilment.Shocked),
            ("ignite", StatusAilment.Ignited), ("burning", StatusAilment.Ignited),
            ("poison", StatusAilment.Poisoned),
            ("bleed", StatusAilment.Bleeding),
            ("stun", StatusAilment.Stunned),
            ("pinned", StatusAilment.Pinned),
            ("hinder", StatusAilment.Slowed), ("slow", StatusAilment.Slowed),
            ("maim", StatusAilment.Maimed),
            ("blind", StatusAilment.Blinded),
            ("curse", StatusAilment.Cursed),
        };

        /// <summary>The ailment a buff id stands for; null for everything else (auras, flasks, charges, immunities).</summary>
        public static StatusAilment? FromBuffId(string id)
        {
            var lower = id.ToLowerInvariant();
            if (lower.Contains("immun", StringComparison.Ordinal) || lower.Contains("aura", StringComparison.Ordinal) ||
                lower.Contains("avoid", StringComparison.Ordinal) || lower.Contains("buildup", StringComparison.Ordinal) ||
                lower.Contains("chance", StringComparison.Ordinal))
            {
                // "chill_immunity", the player's own auras, and PoE2's ailment build-up meters
                // ("flammability_ignite_chance_buildup", seen 2026-10-23) that are not the ailment yet.
                return null;
            }

            foreach (var (stem, ailment) in Stems)
            {
                if (lower.Contains(stem, StringComparison.Ordinal))
                {
                    return ailment;
                }
            }

            return null;
        }
    }
}
