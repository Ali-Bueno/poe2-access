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

        /// <summary>The material an exit sounds as; an interior when nothing is known.</summary>
        public static string Of(string? piece, bool narrow, string? biome) =>
            piece != null && Pieces.TryGetValue(piece, out var material) ? material
            : narrow ? "narrow"
            : biome ?? TerrainBiome.Interior;
    }

    /// <summary>
    ///     When a place's exits sound (<see cref="ExitBeacons"/>): all together on walking into the place, then again every so
    ///     often while the player is still in it, as a sighted player glances at the doorways again (the owner, 2026-11-16: "no
    ///     hace falta que suenen por turno… pueden sonar al mismo tiempo"). Pure: the caller passes the clock and the place.
    /// </summary>
    public sealed class ExitRound
    {
        private readonly long everyMs;
        private int? place;
        private long lastRound = long.MinValue / 2;

        /// <param name="everyMs">Between two rounds while the player stays in a place.</param>
        public ExitRound(long everyMs) => this.everyMs = everyMs;

        /// <summary>Out of every place, or silenced: the next place sounds at once.</summary>
        public void Reset() => this.place = null;

        /// <summary>Whether the place's exits sound now.</summary>
        public bool Due(long now, int? place)
        {
            if (place != this.place)
            {
                this.place = place;
                this.lastRound = long.MinValue / 2;
            }

            if (place == null || now - this.lastRound < this.everyMs)
            {
                return false;
            }

            this.lastRound = now;
            return true;
        }
    }
}
