namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The sounds of the exits' ambient emitters (<see cref="ExitBeacons"/>), per material: soft bed loops, one picked per
    ///     exit, and a pool of short events played over the bed at uneven intervals, a different one each time (round robin:
    ///     the owner, 2026-11-16, "para sonar más natural y no tan repetitivo"). Each sound has a muffled copy for an explored
    ///     exit. Filled from the game's own recordings (`sounds/exits/`, docs/research/game-audio.md).
    /// </summary>
    public sealed class ExitSoundSet
    {
        private readonly Dictionary<string, Material> materials = new();

        /// <summary>Whether any material has a sound.</summary>
        public bool IsEmpty => this.materials.Count == 0;

        /// <summary>A material's sounds; <paramref name="muffle"/> makes each one's explored copy.</summary>
        public void Add(string material, IReadOnlyList<float[]> beds, IReadOnlyList<float[]> events, Func<float[], float[]> muffle)
        {
            if (beds.Count == 0 && events.Count == 0)
            {
                return;
            }

            this.materials[material] = new Material(
                beds.Select(bed => (bed, muffle(bed))).ToList(),
                events.Select(sound => (sound, muffle(sound))).ToList());
        }

        /// <summary>A bed of the material, picked by <paramref name="pick"/> (any whole number), or null when it has none.</summary>
        public float[]? Bed(string material, bool explored, int pick) =>
            this.materials.TryGetValue(material, out var sounds) && sounds.Beds.Count > 0
                ? Choose(sounds.Beds[Math.Abs(pick % sounds.Beds.Count)], explored)
                : null;

        /// <summary>How many events the material has.</summary>
        public int EventCount(string material) => this.materials.TryGetValue(material, out var sounds) ? sounds.Events.Count : 0;

        /// <summary>The material's event at an index (see <see cref="ExitScatter"/>).</summary>
        public float[] Event(string material, bool explored, int index) => Choose(this.materials[material].Events[index], explored);

        /// <summary>For the log: each material and how many beds and events it has.</summary>
        public string Describe() => this.materials.Count == 0 ? "none"
            : string.Join(", ", this.materials.Select(pair => $"{pair.Key} {pair.Value.Beds.Count}+{pair.Value.Events.Count}"));

        private static float[] Choose((float[] Plain, float[] Muffled) sound, bool explored) => explored ? sound.Muffled : sound.Plain;

        private sealed record Material(List<(float[] Plain, float[] Muffled)> Beds, List<(float[] Plain, float[] Muffled)> Events);
    }

    /// <summary>
    ///     When an exit's emitter plays its next event and which: at an uneven interval within the material's range, never the
    ///     same event twice running, at a pitch and level a little off each time. Pure: the caller passes the clock and a source
    ///     of randomness.
    /// </summary>
    public sealed class ExitScatter
    {
        /// <summary>An event's pitch strays up to this much either way (±4%).</summary>
        public const float PitchSpread = 0.04f;

        /// <summary>An event's level strays up to this many decibels either way.</summary>
        public const float GainSpreadDb = 3f;

        private readonly long minMs;
        private readonly long maxMs;
        private readonly Random random;
        private long due;
        private int last = -1;

        public ExitScatter(long minMs, long maxMs, long now, Random random)
        {
            this.minMs = minMs;
            this.maxMs = maxMs;
            this.random = random;
            this.due = now + this.Gap(); // the first event comes some time after the bed starts, not with it
        }

        /// <summary>The event to play now among <paramref name="count"/>, with its pitch and gain factors; null when none is due.</summary>
        public (int Index, float Pitch, float Gain)? Next(long now, int count)
        {
            if (count <= 0 || now < this.due)
            {
                return null;
            }

            this.due = now + this.Gap();
            var index = count == 1 || this.last < 0 ? this.random.Next(count) : this.random.Next(count - 1);
            if (count > 1 && this.last >= 0 && index >= this.last)
            {
                index++; // skip the one just played
            }

            this.last = index;
            var pitch = 1f + (((float)this.random.NextDouble() * 2f) - 1f) * PitchSpread;
            var gain = MathF.Pow(10f, (((float)this.random.NextDouble() * 2f) - 1f) * GainSpreadDb / 20f);
            return (index, pitch, gain);
        }

        private long Gap() => this.minMs + (long)(this.random.NextDouble() * (this.maxMs - this.minMs));
    }
}
