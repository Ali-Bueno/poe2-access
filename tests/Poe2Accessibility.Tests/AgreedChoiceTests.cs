namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A layout read on one dropdown becomes every dropdown's only once a second one agrees (<see cref="AgreedChoice{T}"/>).</summary>
    public static class AgreedChoiceTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(OneDropdownSettlesNothing), OneDropdownSettlesNothing),
            (nameof(TwoThatDisagreeNeverSettle), TwoThatDisagreeNeverSettle),
        };

        private static void OneDropdownSettlesNothing()
        {
            var layout = new AgreedChoice<int>();
            Equal(false, layout.Note(0x1000, 2), "the first dropdown read");
            Equal(false, layout.Note(0x1000, 2), "the same one read again is still one");
            Equal(null, layout.Agreed, "nothing is everyone's yet");
            Equal(true, layout.Note(0x2000, 2), "a second dropdown reads with the same layout");
            Equal(2, layout.Agreed, "now it is");
            Equal(false, layout.Note(0x3000, 5), "and stays, whatever a later one would have chosen");
            Equal(2, layout.Agreed, "unchanged");
        }

        private static void TwoThatDisagreeNeverSettle()
        {
            var layout = new AgreedChoice<int>();
            layout.Note(0x1000, 2);
            Equal(false, layout.Note(0x2000, 5), "text found another way in the second");
            Equal(false, layout.Note(0x1000, 2), "the first again");
            Equal(false, layout.Note(0x2000, 5), "the second again");
            Equal(null, layout.Agreed, "each keeps its own");
            Equal(true, layout.Note(0x3000, 5), "a third sides with the second");
            Equal(5, layout.Agreed, "which settles it");
        }
    }
}
