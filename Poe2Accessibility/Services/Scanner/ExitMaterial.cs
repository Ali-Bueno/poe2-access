namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>What an exit sounds as (<see cref="ExitBeacons"/>): a map piece along it, a narrow passage, else its setting. Pure.</summary>
    public static class ExitMaterial
    {
        /// <summary>Map pieces that give an exit their own sound, and the material they sound as.</summary>
        public static readonly IReadOnlyDictionary<string, string> Pieces = new Dictionary<string, string>
        {
            ["bridge"] = "bridge", ["walkway"] = "bridge", ["dock"] = "water",
            ["stairs"] = "stairs", ["stairs_up"] = "stairs", ["stairs_down"] = "stairs", ["ramp"] = "stairs",
            ["door"] = "door", ["gate"] = "gate",
        };

        /// <summary>
        ///     The range of the gaps between two events over a material's bed (ms): a bridge creaks now and then, a forest rustles
        ///     more often. First guesses, to tune by ear.
        /// </summary>
        public static (long Min, long Max) EventGapMs(string material) => material switch
        {
            "forest" or "water" => (1800, 4500),
            "bridge" or "stairs" or "cave" => (2500, 6000),
            "door" or "gate" => (3500, 8000),
            _ => (3000, 7000),
        };

        /// <summary>The material an exit sounds as; an interior when nothing is known.</summary>
        public static string Of(string? piece, bool narrow, string? biome) =>
            piece != null && Pieces.TryGetValue(piece, out var material) ? material
            : narrow ? "narrow"
            : biome ?? TerrainBiome.Interior;
    }
}
