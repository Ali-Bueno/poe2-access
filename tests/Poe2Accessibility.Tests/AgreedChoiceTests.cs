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
            (nameof(OneDropdownRebuiltIsStillOneOwner), OneDropdownRebuiltIsStillOneOwner),
        };

        /// <summary>The same dropdown rebuilt at a new address keeps its identity; another one does not share it.</summary>
        private static void OneDropdownRebuiltIsStillOneOwner()
        {
            var first = ComboIdentity.Of("Resolution", new[] { "Display", "Options" }, 12);
            var rebuilt = ComboIdentity.Of("Resolution", new[] { "Display", "Options" }, 12);
            Equal(first, rebuilt, "same name, parents and count give the same owner");
            var layout = new AgreedChoice<int>();
            Equal(false, layout.Note(first, 2), "the dropdown read");
            Equal(false, layout.Note(rebuilt, 2), "rebuilt at a new address it is still one owner, so nothing settles");
            Equal(null, layout.Agreed, "not settled by one dropdown");
            Equal(true, layout.Note(ComboIdentity.Of("Language", new[] { "Display", "Options" }, 12), 2), "a different dropdown settles it");
            Equal(false, ComboIdentity.Of("ab", new[] { "c" }, 1) == ComboIdentity.Of("a", new[] { "bc" }, 1), "names are separated");
            Equal(false, ComboIdentity.Of("Resolution", new[] { "Display" }, 12) == ComboIdentity.Of("Resolution", new[] { "Display" }, 13), "the entry count tells two apart");
        }

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
