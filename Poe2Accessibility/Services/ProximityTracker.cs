namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What the player just came up to (D2R ground items / NPC proximity): an entry is announced once when it comes
    ///     within reach, and again only after the player has moved beyond the re-arm distance from it and the
    ///     cooldown has passed (fighting beside a checkpoint walks in and out of reach every few seconds).
    /// </summary>
    public sealed class ProximityTracker
    {
        private readonly float reach;
        private readonly float rearm;
        private readonly int cooldownMs;
        private readonly HashSet<uint> announced = new();
        private readonly Dictionary<uint, long> announcedAt = new();

        public ProximityTracker(float reach, float rearm, int cooldownMs = 0)
        {
            this.reach = reach;
            this.rearm = rearm;
            this.cooldownMs = cooldownMs;
        }

        /// <summary>Entries newly within reach of the player, nearest first.</summary>
        public List<ScanEntry> Update(float playerX, float playerY, IEnumerable<ScanEntry> entries, long nowMs = 0)
        {
            var player = new Vector2(playerX, playerY);
            var fresh = new List<(ScanEntry Entry, float Distance)>();
            var near = new HashSet<uint>();
            foreach (var entry in entries)
            {
                var distance = Vector2.Distance(player, new Vector2(entry.WorldX, entry.WorldY));
                if (distance <= this.rearm)
                {
                    near.Add(entry.Id);
                }

                if (distance <= this.reach && !this.announced.Contains(entry.Id) &&
                    (!this.announcedAt.TryGetValue(entry.Id, out var at) || nowMs - at >= this.cooldownMs))
                {
                    this.announced.Add(entry.Id);
                    this.announcedAt[entry.Id] = nowMs;
                    fresh.Add((entry, distance));
                }
            }

            this.announced.RemoveWhere(id => !near.Contains(id));
            return fresh.OrderBy(item => item.Distance).Select(item => item.Entry).ToList();
        }

        /// <summary>A new area or a pause: everything near is news again.</summary>
        public void Reset()
        {
            this.announced.Clear();
            this.announcedAt.Clear();
        }
    }
}
