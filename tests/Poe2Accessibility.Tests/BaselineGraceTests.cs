namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The window after an area's first reading in which what is read is still baseline (<see cref="BaselineGrace"/>).</summary>
    public static class BaselineGraceTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheWindowStartsAtTheFirstReadingAndEnds), TheWindowStartsAtTheFirstReadingAndEnds),
        };

        private static void TheWindowStartsAtTheFirstReadingAndEnds()
        {
            var grace = new BaselineGrace(1000);
            Equal(true, grace.Active(5000), "the first reading is baseline");
            Equal(true, grace.Active(5999), "and so is what follows within the window");
            Equal(false, grace.Active(6000), "after it a change is news");
            Equal(false, grace.Active(9000), "and stays news");
            grace.Reset();
            Equal(true, grace.Active(20000), "a new area starts a new window at its first reading");
            Equal(false, grace.Active(21000), "which ends again");
        }
    }
}
