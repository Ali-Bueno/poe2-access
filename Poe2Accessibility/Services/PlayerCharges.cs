namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     What a player builds up and spends, by class: charges (Power from the Monk's Killing Palm, Frenzy, Endurance,
    ///     Spirit), the Sorceress's elemental Infusions, the bow and crossbow Surges, the Druid's Druidic Prowess, Arcane
    ///     Surge, the Monk's Tempest Bell Combo, and Rage (Warrior, Druid, "Rage on Hit" passives).
    /// </summary>
    public enum ChargeKind
    {
        Power,
        Frenzy,
        Endurance,
        Spirit,
        FireInfusion,
        ColdInfusion,
        LightningInfusion,
        FireSurge,
        ColdSurge,
        LightningSurge,
        ChaosSurge,
        DruidicProwess,
        ArcaneSurge,

        /// <summary>
        ///     Tempest Bell's Combo: built by hitting with other skills, 4 needed to ring the bell, lost after 8 s without
        ///     building (its tooltip, 2026-10-29). The only combo kept as a buff: the hidden `tempest_bell_combo_tracker`.
        /// </summary>
        Combo,
        Rage,
    }

    /// <summary>
    ///     Charges as the player's buffs show them: one buff per kind, its stack count the number held ("power_charge_79",
    ///     logged 2026-10-29; the other ids are BuffDefinitions rows read 2026-10-29). Rage is a stat, not a buff. Pure.
    /// </summary>
    public static class PlayerCharges
    {
        /// <summary>Maximum Rage when no stat raises it (KeywordPopups "Rage", read 2026-10-29).</summary>
        public const int DefaultMaxRage = 30;

        /// <summary>The kind a buff id holds, or null for any other buff.</summary>
        public static ChargeKind? FromBuffId(string id) => BuffIds.Base(id) switch
        {
            "power_charge" => ChargeKind.Power,
            "frenzy_charge" => ChargeKind.Frenzy,
            "endurance_charge" => ChargeKind.Endurance,
            "spirit_charge" => ChargeKind.Spirit,
            "fire_infusion" => ChargeKind.FireInfusion,
            "cold_infusion" => ChargeKind.ColdInfusion,
            "lightning_infusion" => ChargeKind.LightningInfusion,
            "instilling_fire" => ChargeKind.FireSurge,
            "instilling_cold" => ChargeKind.ColdSurge,
            "instilling_lightning" => ChargeKind.LightningSurge,
            "instilling_chaos" => ChargeKind.ChaosSurge,
            "druidic_prowess" => ChargeKind.DruidicProwess,
            "arcane_surge" => ChargeKind.ArcaneSurge,
            "tempest_bell_combo_tracker" => ChargeKind.Combo,
            _ => null,
        };

        /// <summary>What a buff entry holds: its stack count, at least one while the buff is there.</summary>
        public static int Held(short stacks) => stacks > 0 ? stacks : 1;

        /// <summary>
        ///     Rage moves with every hit and drains away, so only its edges are news: reaching the maximum and running out.
        ///     Null when the change is neither.
        /// </summary>
        public static bool? RageEdge(int before, int after, int max) =>
            after >= max && before < max ? true : after == 0 && before > 0 ? false : null;
    }
}
