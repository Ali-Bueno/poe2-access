namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Rules of the scanner, the guide and the combat cues found wanting by the second audit of 2026-11-10.</summary>
    public static class WorldCombatAuditTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ABossSharesItsNameOnlyWithWhatCanBeTargeted), ABossSharesItsNameOnlyWithWhatCanBeTargeted),
        };

        /// <summary>A boss's next form waits untargetable under its name: the life said is still the fighting one's.</summary>
        private static void ABossSharesItsNameOnlyWithWhatCanBeTargeted()
        {
            const string Boss = "Conde Geonor";
            var fighting = new[] { ((IntPtr)0x100, Boss, 900f) };
            var waiting = new[] { ((IntPtr)0x200, Boss, 100f) };
            Equal(((IntPtr)0x100, true), TargetMatch.Pick(Boss, fighting, waiting), "the one in play is the target, however near the other stands");
            Equal(((IntPtr)0x200, true), TargetMatch.Pick(Boss, Array.Empty<(IntPtr, string, float)>(), waiting), "none in play: the one set aside is all there is");
            var pack = new[] { ((IntPtr)0x300, "Lobo", 400f), ((IntPtr)0x400, "Lobo", 250f) };
            Equal(((IntPtr)0x400, false), TargetMatch.Pick("Lobo", pack, waiting), "a pack in play still shares its name");
            Equal((IntPtr.Zero, false), TargetMatch.Pick(Boss, Array.Empty<(IntPtr, string, float)>(), Array.Empty<(IntPtr, string, float)>()), "no one carries it");
        }
    }
}
