namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Which of the player's buffs are worth telling: those that stay. A buff with no duration that a held button puts
    ///     on and takes off (the buckler raised to parry gives one for as long as it is up) is the skill's own effect, and
    ///     naming it each time it comes and goes buries what matters under it (a Huntress's parry, reported 2026-11-10: "a
    ///     heap of spam about some projectiles"). So a buff with no duration is told only once it has stayed a while, and
    ///     any buff that keeps coming and going is no longer told at all. Pure.
    /// </summary>
    public sealed class SteadyBuffs
    {
        /// <summary>A buff with no duration must have been held this long before it is told.</summary>
        public const int HoldMs = 2000;

        /// <summary>A buff that came and went this many times within <see cref="WindowMs"/> is a button's own effect.</summary>
        public const int Flaps = 3;

        /// <summary>The time within which the comings and goings are counted.</summary>
        public const int WindowMs = 120_000;

        private readonly Dictionary<string, long> since = new();
        private readonly Dictionary<string, List<long>> gone = new();
        private readonly HashSet<string> quiet = new();
        private readonly HashSet<string> seen = new();

        /// <summary>Gets the buffs no longer told because they kept coming and going.</summary>
        public IReadOnlySet<string> Quiet => this.quiet;

        /// <summary>
        ///     Whether the buff <paramref name="key"/>, held now, is told. Call once per reading for each buff held, then
        ///     <see cref="EndReading"/>.
        /// </summary>
        /// <param name="key">The buff.</param>
        /// <param name="timed">It runs out by itself (it has a duration): told from its start.</param>
        /// <param name="nowMs">The reading's time.</param>
        public bool Tells(string key, bool timed, long nowMs)
        {
            this.seen.Add(key);
            if (!this.since.TryGetValue(key, out var start))
            {
                this.since[key] = start = nowMs;
            }

            return !this.quiet.Contains(key) && (timed || nowMs - start >= HoldMs);
        }

        /// <summary>Ends a reading: the buffs not held in it have gone. Returns those that became quiet by it, for the log.</summary>
        public IReadOnlyList<string> EndReading(long nowMs)
        {
            var silenced = new List<string>();
            foreach (var key in this.since.Keys.Where(key => !this.seen.Contains(key)).ToList())
            {
                this.since.Remove(key);
                if (!this.gone.TryGetValue(key, out var times))
                {
                    this.gone[key] = times = new List<long>();
                }

                times.Add(nowMs);
                times.RemoveAll(at => nowMs - at > WindowMs);
                if (times.Count >= Flaps && this.quiet.Add(key))
                {
                    silenced.Add(key);
                }
            }

            this.seen.Clear();
            return silenced;
        }

        /// <summary>A new character: what its buttons do is learnt again.</summary>
        public void Reset()
        {
            this.since.Clear();
            this.gone.Clear();
            this.quiet.Clear();
            this.seen.Clear();
        }
    }
}
