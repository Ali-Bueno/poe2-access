namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>
    ///     The setting a map tile belongs to, from the folder of its file (`metadata/terrain/desert/…`, `woods/manor/interior/…`),
    ///     for the sound an exit makes (the owner, 2026-11-16: "un sonido de viento en salidas de desiertos"). The folders were
    ///     counted over every bundled `.tdt` (docs/research/map-topology.md); the first rule a path matches wins, so a manor's
    ///     interior inside `woods/` is an interior, not a wood. Pure.
    /// </summary>
    public static class TerrainBiome
    {
        public const string Interior = "interior";
        public const string Cave = "cave";
        public const string Water = "water";
        public const string Desert = "desert";
        public const string City = "city";
        public const string Forest = "forest";

        private static readonly (string Biome, string[] Words)[] Rules =
        {
            (Interior, new[] { "/interior", "manor/", "dungeon/", "church/", "temple", "vault", "sanctum", "machinarium", "graveyarddungeons", "crypt", "tomb", "prison", "penitentiary", "library", "hideouts/" }),
            (Cave, new[] { "cave", "cavern", "mines", "mudburrow", "burrow", "anthill", "antnest", "snakepit", "geode", "karst", "/pit/", "sump", "seepage", "cenotes" }),
            (Water, new[] { "river", "lake", "waterfront", "/port/", "flotsam", "channel", "sevenwaters", "swamp", "bog", "mire", "dock", "beach", "shore", "coast", "islands/" }),
            (Desert, new[] { "desert/", "dune", "oasis", "badlands", "aridplains", "mesa", "quarry", "outlands", "mountain/", "plateau", "precipice", "bluff", "alpine", "snow", "frozen", "iceberg" }),
            (City, new[] { "city", "town", "village", "ruins", "gothic", "stronghold", "towers", "keep", "gates", "maraketh", "theopolis", "oriath", "encampment" }),
            (Forest, new[] { "woods", "forest", "jungle", "grove", "canopy", "grimtangle", "overgrown", "greenhouse", "huntinggrounds", "farm", "field", "graveyard" }),
        };

        /// <summary>The setting of a tile file, or null for filler and tiles no rule names (black, blank, unwalkable borders).</summary>
        public static string? Of(string path)
        {
            var file = path.Replace('\\', '/').ToLowerInvariant();
            var name = file[(file.LastIndexOf('/') + 1)..];
            if (name.Contains("blank", StringComparison.Ordinal) || name.StartsWith("black", StringComparison.Ordinal) ||
                name.Contains("impassable", StringComparison.Ordinal))
            {
                return null;
            }

            return Rules.FirstOrDefault(rule => rule.Words.Any(word => file.Contains(word, StringComparison.Ordinal))).Biome;
        }
    }
}
