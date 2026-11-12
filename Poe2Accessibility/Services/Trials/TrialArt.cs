namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     The UI images the Trial of the Sekhemas' map and counters are told by, each part of the image's name; the data
    ///     step "game names" checks each is still an image of the game (<see cref="GameNames"/>). Pure.
    /// </summary>
    public static class TrialArt
    {
        /// <summary>
        ///     The glow under the room the player stands in on the map: a child of that room's button,
        ///     "Art/Textures/Interface/2D/2DArt/UIImages/InGame/Sanctum/ValidPathGlow.dds" (measured 2026-11-12 on one map: the
        ///     only room of the player's column with it, the player's marker beside it; docs/research/trials-and-campaign.md §1.2).
        /// </summary>
        public const string CurrentRoomMarker = "/Sanctum/ValidPathGlow";

        /// <summary>
        ///     The icons before the counters, in the counters' order: the sacred water, then the bronze, silver and gold keys
        ///     ("…/InGame/Sanctum/GoldIcon.dds" before "78(+0)" on the HUD and "78" on the map window, measured 2026-11-12).
        /// </summary>
        public static readonly string[] CounterMarkers = { "/Sanctum/GoldIcon", "/Sanctum/BronzeKeyIcon", "/Sanctum/SilverKeyIcon", "/Sanctum/GoldKeyIcon" };

        /// <summary>
        ///     The icon of a slot of what a run carries, one slot a kind (minor and major affliction, minor and major boon, pact):
        ///     "…/InGame/Sanctum/BoonCurseIcons/MinorCurseIcon.dds", the first child of the slot (measured 2026-11-12).
        /// </summary>
        public const string EffectSlotMarker = "/Sanctum/BoonCurseIcons/";
    }
}
