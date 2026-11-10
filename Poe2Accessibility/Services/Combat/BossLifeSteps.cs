namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The boss life percentages said once each as a boss falls past them; a heal back past one says them again. Pure.</summary>
    public static class BossLifeSteps
    {
        /// <summary>No step said yet: the boss at full life.</summary>
        public const int Full = 100;

        /// <summary>Percent (D2R: 50, then every 10; 75 and 5 added for long PoE fights).</summary>
        private static readonly int[] Steps = { 75, 50, 40, 30, 20, 10, 5 };

        /// <summary>
        ///     The game reset an engaged boss's fight: it is back at full life and the player died after it was last hurt (the
        ///     encounter restarts with the player's return). A heal phase after the death leaves it hurt in between.
        /// </summary>
        public static bool FightRestarted(long? diedAt, long hurtAt, bool engaged, int percent) =>
            engaged && percent >= Full && diedAt is { } died && died >= hurtAt;

        /// <summary>
        ///     Which engaged boss the vitals readout names, by its place in <paramref name="engaged"/>; null for none. Each
        ///     comes with its life as the game holds it when asked (null: not there to read, so nothing is known of it now),
        ///     never the last one polled: a boss the player died to is back at full life unseen. Of several, the one whose
        ///     life changed last is the one being fought.
        /// </summary>
        public static int? Spoken(IReadOnlyList<(int? Percent, long ChangedAt, long HurtAt)> engaged, long? diedAt)
        {
            int? spoken = null;
            for (var i = 0; i < engaged.Count; i++)
            {
                if (engaged[i].Percent is { } percent && !FightRestarted(diedAt, engaged[i].HurtAt, true, percent) &&
                    (spoken is not { } best || engaged[i].ChangedAt > engaged[best].ChangedAt))
                {
                    spoken = i;
                }
            }

            return spoken;
        }

        /// <summary>A boss must read not alive this long, with no living reading between, before its defeat is said.</summary>
        public const int DefeatConfirmMs = 500;

        /// <summary>
        ///     Whether a boss first read not alive at <paramref name="deadSince"/> (0: it reads alive) is defeated: one reading is
        ///     not enough, as a life read while the game rewrites it, or a component read that failed, says dead for a frame.
        /// </summary>
        public static bool Defeated(long deadSince, long now) => deadSince != 0 && now - deadSince >= DefeatConfirmMs;

        /// <summary>
        ///     The step a boss that reads alive is followed from: one said defeated (0) has risen under the same entity, a
        ///     second life the game gives some of them, and starts over. Left at 0 its life was never said again, nor its
        ///     real defeat, and the vitals readout left it out.
        /// </summary>
        public static int Alive(int step) => step == 0 ? Full : step;

        /// <summary>The step reached at <paramref name="percent"/>, from <paramref name="step"/>, the last one (0: defeated, until it reads alive again).</summary>
        public static int Next(int step, int percent)
        {
            // Back up to the step above the last one said (the fight restarted after a death, a heal phase): those below come again.
            if (step > 0 && percent >= Steps.Where(threshold => threshold > step).DefaultIfEmpty(Full).Min())
            {
                step = Steps.Where(threshold => threshold > percent).DefaultIfEmpty(Full).Min();
            }

            foreach (var threshold in Steps)
            {
                if (percent < threshold && threshold < step)
                {
                    step = threshold;
                }
            }

            return step;
        }
    }
}
