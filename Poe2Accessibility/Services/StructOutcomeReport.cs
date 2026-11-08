namespace Poe2Accessibility.Services
{
    using GameReader.Utils;

    /// <summary>The host's judgments (<see cref="StructOutcome"/>) said the way every other layout part is.</summary>
    public static class StructOutcomeReport
    {
        /// <summary>Found, moved or missing in <paramref name="report"/>; a part not judged says nothing.</summary>
        public static void Record(this CompatibilityReport report, StructOutcome outcome)
        {
            switch (outcome.Health)
            {
                case StructHealth.Intact:
                    report.Found(outcome.Name);
                    break;
                case StructHealth.Moved:
                    report.Moved(outcome.Name, outcome.From, outcome.To);
                    break;
                case StructHealth.Lost:
                    report.Missing(outcome.Name);
                    break;
            }
        }
    }
}
