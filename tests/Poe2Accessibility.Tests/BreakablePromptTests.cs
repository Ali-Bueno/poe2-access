namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A row of pots says its prompt once.</summary>
    public static class BreakablePromptTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ARowOfPotsSaysItsPromptOnce), ARowOfPotsSaysItsPromptOnce),
        };

        private static void ARowOfPotsSaysItsPromptOnce()
        {
            var prompts = new BreakablePrompts();
            Equal(false, prompts.Repeats(true, "Tiesto", 0), "the first of a row is said");
            Equal(true, prompts.Repeats(true, "Tiesto", 1500), "the next pot");
            Equal(false, prompts.Repeats(true, "Urna", 2000), "another name");
            Equal(false, prompts.Repeats(false, "Urna", 2500), "another kind of object with that name");
            Equal(false, prompts.Repeats(true, "Urna", 3000), "a breakable after it");
            Equal(false, prompts.Repeats(true, "Urna", 3000 + BreakablePrompts.SameNameMs), "after the window");
            prompts.Reset();
            Equal(false, prompts.Repeats(true, "Urna", 3000 + BreakablePrompts.SameNameMs + 10), "a new area");
        }
    }
}
