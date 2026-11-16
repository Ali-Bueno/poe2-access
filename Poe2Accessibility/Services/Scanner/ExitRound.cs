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
    ///     When a place's exits sound (<see cref="ExitBeacons"/>): one round on walking into the place, each exit in turn a
    ///     beat apart, then another round every so often while the player is still in it, as a sighted player glances at the
    ///     doorways again. Pure: the caller passes the clock, the place and how many exits it has now.
    /// </summary>
    public sealed class ExitRound
    {
        private readonly long beatMs;
        private readonly long roundEveryMs;
        private int? place;
        private int next;
        private long lastBeat = long.MinValue / 2;
        private long roundStarted = long.MinValue / 2;

        /// <param name="beatMs">Between two exits of a round.</param>
        /// <param name="roundEveryMs">From the start of one round to the start of the next while the player stays.</param>
        public ExitRound(long beatMs, long roundEveryMs)
        {
            this.beatMs = beatMs;
            this.roundEveryMs = roundEveryMs;
        }

        /// <summary>Out of every place, or silenced: the next place starts a round at once.</summary>
        public void Reset()
        {
            this.place = null;
            this.next = 0;
        }

        /// <summary>The index of the exit to sound now (in the caller's order), or null.</summary>
        public int? Due(long now, int? place, int exits)
        {
            if (place != this.place)
            {
                this.place = place;
                this.next = 0;
                this.roundStarted = now;
                this.lastBeat = long.MinValue / 2;
            }

            if (place == null || exits <= 0 || now - this.lastBeat < this.beatMs)
            {
                return null;
            }

            if (this.next >= exits)
            {
                if (now - this.roundStarted < this.roundEveryMs)
                {
                    return null;
                }

                this.next = 0;
                this.roundStarted = now;
            }

            this.lastBeat = now;
            return this.next++;
        }
    }
}
