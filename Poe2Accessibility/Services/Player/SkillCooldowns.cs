namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Which skills can be used again: each one spent (every use recharging) for at least <see cref="MinSpentMs"/> and usable now.</summary>
    public sealed class SkillCooldowns
    {
        /// <summary>A shorter wait is part of using the skill, not news: a skill on a one-second cooldown would be named every second.</summary>
        public const int MinSpentMs = 1500;

        private readonly Dictionary<uint, long> spentSince = new();
        private bool baseline = true;

        /// <summary>A new area or character: the next reading only remembers what is spent.</summary>
        public void Reset()
        {
            this.spentSince.Clear();
            this.baseline = true;
        }

        /// <param name="usable">Each skill listed now and whether it can be used.</param>
        /// <returns>The skills usable again since the last update, in <paramref name="usable"/>'s order.</returns>
        public List<uint> Update(IReadOnlyDictionary<uint, bool> usable, long nowMs)
        {
            var ready = new List<uint>();
            foreach (var (id, canUse) in usable)
            {
                if (!canUse)
                {
                    // Spent already when the area began: how long is unknown, so its end is news.
                    this.spentSince.TryAdd(id, this.baseline ? nowMs - MinSpentMs : nowMs);
                }
                else if (this.spentSince.Remove(id, out var since) && nowMs - since >= MinSpentMs)
                {
                    ready.Add(id);
                }
            }

            foreach (var gone in this.spentSince.Keys.Where(id => !usable.ContainsKey(id)).ToList())
            {
                this.spentSince.Remove(gone);
            }

            this.baseline = false;
            return ready;
        }
    }
}
