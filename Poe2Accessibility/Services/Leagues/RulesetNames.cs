namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     The rows of Rulesets.dat whose live object the mod reads (<see cref="GameLayout.Rulesets"/>, <see cref="GameLayout.Fog"/>),
    ///     by their Id: each is checked against the game's files after a patch (<see cref="GameNames"/>), as a renamed row
    ///     leaves its mechanic unread.
    /// </summary>
    public static class RulesetNames
    {
        public const string Breach = "ExpandingBrequelEncounterRules";

        public const string Expedition = "ExpeditionRules";

        public const string Ultimatum = "UltimatumRules";

        public const string Fog = "AfflictionEncounterRules";

        /// <summary>The fog's endgame variant, which has no clocks.</summary>
        public const string FogOnMaps = "AfflictionMapRules";

        public static readonly string[] All = { Breach, Expedition, Ultimatum, Fog, FogOnMaps };
    }
}
