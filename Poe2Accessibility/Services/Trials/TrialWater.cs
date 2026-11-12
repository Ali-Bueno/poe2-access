namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The sacred water a Trial of the Sekhemas' monsters drop. Pure.</summary>
    public static class TrialWater
    {
        /// <summary>
        ///     The drop's script. It has no name of its own, so it was "Objeto sin nombre, Sanctum Mote" (user 2026-11-12): its
        ///     art is `sacred_water_drop.ao`, a magnet flies it to a player within 24 cells, and the pad cannot select it
        ///     (`gamepad_cannot_select`), so it is walked up to, not used.
        /// </summary>
        public const string DropFile = "Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote";

        private const char VariantMark = '@';

        /// <summary>Whether an object is a drop of sacred water, whatever its level.</summary>
        public static bool IsDrop(string path) =>
            path.StartsWith(DropFile, StringComparison.Ordinal) && (path.Length == DropFile.Length || path[DropFile.Length] == VariantMark);
    }
}
