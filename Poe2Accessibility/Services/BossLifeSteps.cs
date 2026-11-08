namespace Poe2Accessibility.Services
{
    using System.Linq;

    /// <summary>The boss life percentages said once each as a boss falls past them; a heal back past one says them again. Pure.</summary>
    public static class BossLifeSteps
    {
        /// <summary>No step said yet: the boss at full life.</summary>
        public const int Full = 100;

        /// <summary>Percent (D2R: 50, then every 10; 75 and 5 added for long PoE fights).</summary>
        private static readonly int[] Steps = { 75, 50, 40, 30, 20, 10, 5 };

        /// <summary>The step reached at <paramref name="percent"/>, from <paramref name="step"/>, the last one (0: dead, for good).</summary>
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
