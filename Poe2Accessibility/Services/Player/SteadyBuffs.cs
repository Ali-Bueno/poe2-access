namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Which of the player's buffs are worth telling: those that stay. A buff with no duration and a single stack that a
    ///     held button puts on and takes off (the buckler raised to parry gives one for as long as it is up) is the skill's own
    ///     effect, and naming it each time it comes and goes buries what matters under it (a Huntress's parry, reported
    ///     2026-11-10: "a heap of spam about some projectiles"). So such a buff is told only once it has stayed a while, and
    ///     one that keeps coming and going is no longer told at all. A buff that runs out by itself or holds a count (Tailwind,
    ///     lost whole at every hit) is never a button's: it comes and goes as the fight does, and each time is news. Pure.
    /// </summary>
    public sealed class SteadyBuffs
    {
        /// <summary>A buff with no duration and a single stack must have been held this long before it is told.</summary>
        public const int HoldMs = 2000;

        /// <summary>A button's buff that came and went this many times within <see cref="WindowMs"/> is the button's own effect.</summary>
        public const int Flaps = 3;

        /// <summary>The time within which the comings and goings are counted.</summary>
        public const int WindowMs = 120_000;

        private readonly Dictionary<string, (long Since, bool Button)> held = new();
        private readonly Dictionary<string, List<long>> gone = new();
        private readonly HashSet<string> quiet = new();
        private readonly HashSet<string> seen = new();

        /// <summary>Gets the buffs whose coming and going is no longer told because they kept doing it.</summary>
        public IReadOnlySet<string> Quiet => this.quiet;

        /// <summary>
        ///     Whether a buff's duration as the game holds it is one: a positive, finite number of seconds. Zero, infinity and
        ///     what is no number all mean it lasts until something takes it off (which of them the game writes there is not
        ///     measured, so none is taken for a duration).
        /// </summary>
        public static bool RunsOut(float totalSeconds) => totalSeconds > 0 && float.IsFinite(totalSeconds);

        /// <summary>
        ///     Whether the buff <paramref name="key"/>, held now, is told. Call once per reading for each buff held, then
        ///     <see cref="EndReading"/>.
        /// </summary>
        /// <param name="key">The buff.</param>
        /// <param name="timed">It runs out by itself (<see cref="RunsOut"/>): told from its start.</param>
        /// <param name="nowMs">The reading's time.</param>
        /// <param name="stacks">The count it holds: past one it is something the player builds, told from its start.</param>
        /// <param name="alreadyThere">
        ///     The reading is of what an area or a character came with: a buff first seen in it has stayed for all that is
        ///     known, so it is not held back and then told as new once the wait ends.
        /// </param>
        public bool Tells(string key, bool timed, long nowMs, int stacks = 1, bool alreadyThere = false)
        {
            this.seen.Add(key);
            if (!this.held.TryGetValue(key, out var holding))
            {
                holding = (alreadyThere ? nowMs - HoldMs : nowMs, true);
            }

            // Once it ran out by itself or held a count, this holding of it is no button's, whatever it reads later.
            holding.Button &= !timed && stacks <= 1;
            this.held[key] = holding;
            return !holding.Button || (!this.quiet.Contains(key) && nowMs - holding.Since >= HoldMs);
        }

        /// <summary>Ends a reading: the buffs not held in it have gone. Returns those that became quiet by it, for the log.</summary>
        public IReadOnlyList<string> EndReading(long nowMs)
        {
            var silenced = new List<string>();
            foreach (var key in this.held.Keys.Where(key => !this.seen.Contains(key)).ToList())
            {
                var wasButton = this.held[key].Button;
                this.held.Remove(key);
                if (!wasButton)
                {
                    continue; // it ran out or was a count spent: not a button let go
                }

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
            this.held.Clear();
            this.gone.Clear();
            this.quiet.Clear();
            this.seen.Clear();
        }
    }
}
