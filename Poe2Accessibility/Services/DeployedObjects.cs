namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What a skill of the player's placed and left standing to be hit (<see cref="DeployedWatch"/>): which entities,
    ///     when one counts as standing, and how often and how loud it sounds by distance, as the object sounds do. Pure.
    /// </summary>
    public static class DeployedObjects
    {
        /// <summary>About a screen: as far as the object sounds reach.</summary>
        public const float RangeWorld = 1300f;

        /// <summary>Within this the sound comes from the centre: the player stands at it.</summary>
        public const float CentreWorld = 150f;

        /// <summary>A placed object is heard once it has stood this long.</summary>
        public const int SettleMs = 400;

        /// <summary>Moving more than this from where it appeared means it is not a placed object.</summary>
        public const float MoveWorld = 40f;

        /// <summary>Two placed objects never sound closer together than this, so two crystals are heard as two.</summary>
        public const int GapMs = 180;

        private const int NearIntervalMs = 700;
        private const int FarIntervalMs = 2200;
        private const float FarGain = 0.35f;

        /// <summary>
        ///     Entity paths of what player skills place to be struck: Tempest Bell and its skins (game metadata
        ///     `metadata/effects/spells/monk_bell/tempest_bell.ot`, `mtx_bell*.ot`), the spectral bells, and Frozen Locus's ice
        ///     crystal (`Metadata/MiscellaneousObjects/FrostWall/OrbOfIce`, turned up beside the player live 2026-10-29). The
        ///     Actor's own list of placed objects is not where GameHelper reads it (0xC28) and held no entity id anywhere in
        ///     its first 0x1400 bytes with the crystal down (survey 2026-10-29), so the kinds are named here.
        /// </summary>
        private static readonly string[] PlacedPaths =
        {
            "Metadata/Effects/Spells/monk_bell/",
            "Metadata/Effects/Spells/monk_spectral_bells/",
            "Metadata/MiscellaneousObjects/FrostWall/OrbOfIce",
        };

        public static bool IsPlaced(string path) => PlacedPaths.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        ///     The game's own sound for what stands there (SoundExtractor cues: the bell's toll `TempestBell_FX_OnHit_01`, the
        ///     crystal forming `FrozenLocus_FX_Appear_01`; docs/research/game-audio.md): the player asked for the bell's sound
        ///     rather than a beep (2026-10-29).
        /// </summary>
        public static string SoundKey(string path) => path.Contains("FrostWall/OrbOfIce", StringComparison.OrdinalIgnoreCase) ? "ice_crystal" : "tempest_bell";

        public static bool Moved(Vector2 from, Vector2 to) => Vector2.Distance(from, to) > MoveWorld;

        private static float Closeness(float distance) => 1f - Math.Clamp(distance / RangeWorld, 0f, 1f);

        /// <summary>Time between two sounds: often near, seldom far.</summary>
        public static int IntervalMs(float distance) => (int)(FarIntervalMs - (Closeness(distance) * (FarIntervalMs - NearIntervalMs)));

        public static float GainAt(float distance) => FarGain + ((1f - FarGain) * Closeness(distance));
    }
}
