namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The quest steps an area showed that name a quest item the player carried ("Coloca el núcleo de alma pequeño en el
    ///     Altar de piedra"): while one is carried again, that step still counts, as the next altar takes the next core though
    ///     the tracker moved on (the owner, Jiquani's Machinarium, 2026-11-19). Pure.
    /// </summary>
    public sealed class CarriedQuestSteps
    {
        /// <summary>Steps remembered at most in one area.</summary>
        private const int MaxSteps = 16;

        private readonly List<string> steps = new();
        private readonly object gate = new(); // listed on the scanner's thread, saved on the game's
        private string area = string.Empty;

        /// <summary>A copy of the steps remembered in the current area, for <see cref="AreaMemory"/>.</summary>
        public List<string> Steps
        {
            get
            {
                lock (this.gate)
                {
                    return this.steps.ToList();
                }
            }
        }

        /// <summary>Back in an area instance known from before (or after a host restart): its steps as they were left.</summary>
        public void Restore(string areaKey, IReadOnlyList<string>? steps)
        {
            lock (this.gate)
            {
                this.area = areaKey;
                this.steps.Clear();
                this.steps.AddRange((steps ?? new List<string>()).TakeLast(MaxSteps));
            }
        }

        /// <summary>The tracker's texts, plus the steps of this area that name a quest item carried now.</summary>
        public IReadOnlyList<string> With(IReadOnlyList<string> texts, string areaKey, IReadOnlyCollection<string> carried)
        {
            lock (this.gate)
            {
                if (!string.Equals(areaKey, this.area, System.StringComparison.Ordinal))
                {
                    this.area = areaKey;
                    this.steps.Clear();
                }

                if (carried.Count == 0)
                {
                    return texts;
                }

                foreach (var text in texts)
                {
                    if (!this.steps.Contains(text) && carried.Any(item => QuestMatcher.Mentions(text, item)))
                    {
                        if (this.steps.Count >= MaxSteps)
                        {
                            this.steps.RemoveAt(0);
                        }

                        this.steps.Add(text);
                    }
                }

                var kept = this.steps.Where(step => !texts.Contains(step) && carried.Any(item => QuestMatcher.Mentions(step, item))).ToList();
                return kept.Count == 0 ? texts : texts.Concat(kept).ToList();
            }
        }
    }
}
