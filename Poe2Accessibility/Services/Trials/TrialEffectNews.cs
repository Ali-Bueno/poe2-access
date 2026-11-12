namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Which afflictions and boons a Trial run gained since the last reading. One reading that misses an effect is no
    ///     loss of it: a slot read while the game rewrites it reads empty for a moment, and every effect of its kind would be
    ///     said gained again. Pure.
    /// </summary>
    public sealed class TrialEffectNews
    {
        /// <summary>Readings in a row without an effect before it counts as lost, so that gaining it again is news.</summary>
        public const int GoneAfter = 4;

        private readonly Dictionary<string, int> missed = new();
        private bool read;

        /// <summary>The effects not carried before, in the order read; none at the first reading.</summary>
        public IReadOnlyList<string> Gained(IReadOnlyCollection<string> carried)
        {
            var gained = this.read ? carried.Where(key => !this.missed.ContainsKey(key)).Distinct().ToList() : new List<string>();
            foreach (var key in this.missed.Keys.Where(key => !carried.Contains(key)).ToList())
            {
                if (++this.missed[key] >= GoneAfter)
                {
                    this.missed.Remove(key);
                }
            }

            foreach (var key in carried)
            {
                this.missed[key] = 0;
            }

            this.read = true;
            return gained;
        }
    }
}
