namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     The ways that opened on their own (<see cref="BarrierWatch"/>) and have not been walked to yet. One opens in the
    ///     middle of a fight, its line goes by among the rest, and afterwards nothing told where it was (user 2026-11-12):
    ///     it stays in the scanner and sounds until the player has stood at it and walked on, it closes again, or the area
    ///     is left. Any thread. Only the latest opening sounds: an Abyss pit that closes opens its ground, the next one along the
    ///     fissure after it, and the old one still ringing told the wrong way on (user 2026-11-17).
    /// </summary>
    public sealed class OpenedWays
    {
        /// <summary>The kind of an opened way's entry, and the key of its sound.</summary>
        public const string Kind = "opened_way";

        /// <summary>Standing at it, and walked on from it: the reach and re-arm of what is named on approach (<see cref="ProximityNarrator"/>).</summary>
        public const float ReachWorld = 200f;
        public const float LeftWorld = 300f;

        /// <summary>Ways opened this close together are one opening (a boss door's two halves, 13 ms apart in the 2026-11-17 log).</summary>
        public const long SameOpeningMs = 2000;

        private readonly object gate = new();
        private readonly Dictionary<uint, (ScanEntry Entry, bool Reached)> ways = new();
        private readonly Dictionary<uint, long> openedAt = new();

        /// <summary>A way opened at <paramref name="now"/> (ms); the ways of an earlier opening are forgotten.</summary>
        public void Add(ScanEntry entry, long now)
        {
            lock (this.gate)
            {
                List<uint>? older = null;
                foreach (var (id, at) in this.openedAt)
                {
                    if (id != entry.Id && now - at > SameOpeningMs)
                    {
                        (older ??= new List<uint>()).Add(id);
                    }
                }

                foreach (var id in older ?? (IEnumerable<uint>)Array.Empty<uint>())
                {
                    this.ways.Remove(id);
                    this.openedAt.Remove(id);
                }

                this.ways[entry.Id] = (entry, false);
                this.openedAt[entry.Id] = now;
            }
        }

        /// <summary>It closed again (an arena's gate), or its entity is something else now.</summary>
        public void Remove(uint id)
        {
            lock (this.gate)
            {
                this.ways.Remove(id);
                this.openedAt.Remove(id);
            }
        }

        /// <summary>A new area, or the same one loaded again.</summary>
        public void Clear()
        {
            lock (this.gate)
            {
                this.ways.Clear();
                this.openedAt.Clear();
            }
        }

        /// <summary>The ways still to be walked to, with the player at this world position; one stood at and left is forgotten.</summary>
        public IReadOnlyList<ScanEntry> Update(float playerX, float playerY)
        {
            lock (this.gate)
            {
                if (this.ways.Count == 0)
                {
                    return Array.Empty<ScanEntry>();
                }

                var left = new List<ScanEntry>(this.ways.Count);
                List<uint>? walkedOn = null;
                foreach (var (id, (entry, reached)) in this.ways)
                {
                    var distance = MathF.Sqrt(((entry.WorldX - playerX) * (entry.WorldX - playerX)) + ((entry.WorldY - playerY) * (entry.WorldY - playerY)));
                    if (reached && distance > LeftWorld)
                    {
                        (walkedOn ??= new List<uint>()).Add(id);
                        continue;
                    }

                    if (!reached && distance <= ReachWorld)
                    {
                        (walkedOn ??= new List<uint>()).Add(id); // marked below: the dictionary is being read
                    }

                    left.Add(entry);
                }

                foreach (var id in walkedOn ?? (IEnumerable<uint>)Array.Empty<uint>())
                {
                    if (this.ways[id].Reached)
                    {
                        this.ways.Remove(id);
                        this.openedAt.Remove(id);
                    }
                    else
                    {
                        this.ways[id] = (this.ways[id].Entry, true);
                    }
                }

                return left;
            }
        }
    }
}
