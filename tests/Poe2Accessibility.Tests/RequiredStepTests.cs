namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using static Check;

    /// <summary>A required game-data step that loses files or a language does not report done.</summary>
    public static class RequiredStepTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AFewLostUiScriptsAreToleratedAShareIsNot), AFewLostUiScriptsAreToleratedAShareIsNot),
        };

        private static void AFewLostUiScriptsAreToleratedAShareIsNot()
        {
            Expect(true, UiLoss.Tolerated(0, 0), "nothing lost, nothing found: the entry count judges the rest");
            Expect(true, UiLoss.Tolerated(0, 500), "nothing lost");
            Expect(true, UiLoss.Tolerated(10, 500), "two in a hundred");
            Expect(false, UiLoss.Tolerated(11, 500), "more than two in a hundred");
            Expect(false, UiLoss.Tolerated(1, 10), "a tenth of a small set");
            Expect(true, UiLoss.Failure("script files", 11, 500).Contains("11 of 500 script files"), "the failure line names the loss");
        }
    }
}
