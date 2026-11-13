namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What a boss fight puts on the player, by the catalog's buffs.</summary>
    public enum PlayerFightNewsKind
    {
        /// <summary>The shelter's buff is on the player.</summary>
        Sheltered,

        /// <summary>It is gone while the shelter is still needed.</summary>
        ShelterLost,

        /// <summary>The player stands in an area of the fight that gives a buff.</summary>
        Zone,

        /// <summary>A counted buff's count, once it holds still.</summary>
        Stack,

        /// <summary>The player is held in place.</summary>
        Grabbed,
    }

    public readonly record struct PlayerFightNews(PlayerFightNewsKind Kind, string Buff = "", int Count = 0);

    /// <summary>
    ///     The player's buffs of a boss fight (docs/research/boss-mechanics.md §14: SH, GZ, ST, GR), each said as it begins or
    ///     as its count settles; what is on the player when the fight starts is not news. Pure.
    /// </summary>
    public sealed class BossPlayerBuffs
    {
        /// <summary>The shelter's buff lasts 0.15 s and is given again while inside: unseen this long (ms), the player is out.</summary>
        public const int ShelterHoldMs = 600;

        /// <summary>A buff gone this long (ms) and back has begun again.</summary>
        public const int AgainMs = 2000;

        /// <summary>A count is said once it has held this long (ms), as the player's charges are.</summary>
        public const int CountQuietMs = 400;

        private readonly Dictionary<string, long> seenAt = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (int Count, long Since, int Said)> counts = new(StringComparer.Ordinal);
        private long shelterSeenAt = long.MinValue / 2;
        private bool sheltered;
        private bool started;

        /// <summary>Whether the shelter's buff has been on the player a moment ago.</summary>
        public bool Sheltered => this.sheltered;

        public void Reset()
        {
            this.seenAt.Clear();
            this.counts.Clear();
            this.shelterSeenAt = long.MinValue / 2;
            this.sheltered = false;
            this.started = false;
        }

        /// <param name="buffs">The player's buffs, each id once with its stacks.</param>
        /// <param name="shelterNeeded">A shelter of the fight is up: leaving it is worth a word.</param>
        public IReadOnlyList<PlayerFightNews> Update(BossMechanicsCatalog catalog, IReadOnlyList<(string Id, int Stacks)> buffs, bool shelterNeeded, long now)
        {
            var news = new List<PlayerFightNews>();
            var baseline = !this.started;
            this.started = true;
            if (buffs.Any(buff => catalog.IsShelter(buff.Id)))
            {
                this.shelterSeenAt = now;
            }

            var inside = now - this.shelterSeenAt < ShelterHoldMs;
            if (inside != this.sheltered)
            {
                this.sheltered = inside;
                if (inside)
                {
                    news.Add(new PlayerFightNews(PlayerFightNewsKind.Sheltered));
                }
                else if (shelterNeeded)
                {
                    news.Add(new PlayerFightNews(PlayerFightNewsKind.ShelterLost));
                }
            }

            foreach (var (id, stacks) in buffs)
            {
                var zone = catalog.IsZone(id);
                var held = catalog.IsGrabbed(id);
                if (zone || held)
                {
                    var again = !this.seenAt.TryGetValue(id, out var last) || now - last >= AgainMs;
                    this.seenAt[id] = now;
                    if (again && !baseline)
                    {
                        news.Add(new PlayerFightNews(held ? PlayerFightNewsKind.Grabbed : PlayerFightNewsKind.Zone, id));
                    }
                }

                if (catalog.IsStack(id))
                {
                    this.Count(id, stacks, baseline, now, news);
                }
            }

            foreach (var gone in this.counts.Keys.Where(id => buffs.All(buff => buff.Id != id)).ToList())
            {
                this.counts.Remove(gone);
            }

            return news;
        }

        private void Count(string id, int stacks, bool baseline, long now, List<PlayerFightNews> news)
        {
            if (!this.counts.TryGetValue(id, out var count))
            {
                count = (stacks, now, baseline ? stacks : 0);
            }
            else if (count.Count != stacks)
            {
                count = (stacks, now, count.Said);
            }

            if (count.Count != count.Said && count.Count > 0 && now - count.Since >= CountQuietMs)
            {
                count.Said = count.Count;
                news.Add(new PlayerFightNews(PlayerFightNewsKind.Stack, id, count.Count));
            }

            this.counts[id] = count;
        }
    }
}
