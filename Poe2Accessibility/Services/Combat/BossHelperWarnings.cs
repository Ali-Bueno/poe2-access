namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     A boss's invisible helpers that walk toward the player (Jamanra's obsidian storm, Zalmarath's laser target;
    ///     docs/research/boss-mechanics.md §14, D): each warns once as its danger reaches the player, and again only after
    ///     the player has been clear of it a while. Pure.
    /// </summary>
    public sealed class BossHelperWarnings
    {
        /// <summary>Clear of a helper this long (ms), its return is a new danger; shorter is the edge of its area.</summary>
        public const int RearmMs = 1500;

        /// <summary>A helper whose skills have no area in the files warns within this gap (world units): a rare's blow warns as near (<see cref="BossWatch"/>).</summary>
        public const float UponWorld = 150f;

        /// <summary>The area of a helper the files draw none for: only that it is upon the player.</summary>
        public static readonly AttackArea Unknown = new(string.Empty, 0f, 0f, 0f, 0f, 0);

        private readonly Dictionary<uint, long> reachedAt = new();

        public void Reset() => this.reachedAt.Clear();

        /// <param name="helpers">Each helper in the arena and whether its danger holds the player now.</param>
        /// <returns>The helpers to warn of.</returns>
        public IReadOnlyCollection<uint> Update(IReadOnlyList<(uint Id, bool Reaches)> helpers, long now)
        {
            var news = new HashSet<uint>();
            foreach (var (id, reaches) in helpers)
            {
                if (!reaches)
                {
                    continue;
                }

                if (!this.reachedAt.TryGetValue(id, out var last) || now - last >= RearmMs)
                {
                    news.Add(id);
                }

                this.reachedAt[id] = now;
            }

            foreach (var gone in this.reachedAt.Keys.Where(id => helpers.All(helper => helper.Id != id)).ToList())
            {
                this.reachedAt.Remove(gone);
            }

            return news;
        }
    }
}
