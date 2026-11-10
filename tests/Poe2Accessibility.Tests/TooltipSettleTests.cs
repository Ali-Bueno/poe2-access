namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>An item's first tooltip is read again until it holds still (<see cref="TooltipSettle"/>): the belt's false "missing" lines, 2026-11-10.</summary>
    public static class TooltipSettleTests
    {
        private static readonly IntPtr Belt = new(0x1000);

        private static readonly IntPtr Ring = new(0x2000);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AFirstTooltipWaitsUntilItHoldsStill), AFirstTooltipWaitsUntilItHoldsStill),
            (nameof(ATooltipSeenWholeIsSaidAtOnce), ATooltipSeenWholeIsSaidAtOnce),
            (nameof(AnItemItsTooltipNamesWaitsForATooltipNotUpYet), AnItemItsTooltipNamesWaitsForATooltipNotUpYet),
        };

        private static void AFirstTooltipWaitsUntilItHoldsStill()
        {
            var settle = new TooltipSettle();
            Equal(false, settle.Filling(IntPtr.Zero, 0, 0, 0), "an empty cell");
            Equal(false, settle.Filling(Belt, 0, 0, 0), "no tooltip yet: the name alone is said");
            Equal(true, settle.Filling(Belt, 3, 111, 1000), "first seen: it may lack its modifiers");
            Equal(true, settle.Filling(Belt, 7, 222, 1000 + TooltipSettle.HoldMs), "it grew: counted again from here");
            Equal(true, settle.Filling(Belt, 7, 222, 1000 + (2 * TooltipSettle.HoldMs) - 1), "not still for long enough");
            Equal(false, settle.Filling(Belt, 7, 222, 1000 + (2 * TooltipSettle.HoldMs)), "the same twice, far enough apart: whole");

            Equal(true, settle.Filling(Ring, 2, 1, 5000), "another item starts over");
            Equal(true, settle.Filling(Ring, 2, 2, 5000 + TooltipSettle.GiveUpMs - 1), "one that keeps changing waits");
            Equal(false, settle.Filling(Ring, 2, 3, 5000 + TooltipSettle.GiveUpMs), "but not for ever");
        }

        /// <summary>A worn weapon's tooltip came 170 ms after the cursor: its base name was said, then its own name (2026-11-10).</summary>
        private static void AnItemItsTooltipNamesWaitsForATooltipNotUpYet()
        {
            var settle = new TooltipSettle();
            Equal(false, settle.Filling(Belt, 0, 0, 0, nameAwaited: false), "a normal item's line is the same with or without: never held back");
            Equal(true, settle.Filling(Ring, 0, 0, 1000, nameAwaited: true), "a rare item with no tooltip yet: its name is to come");
            Equal(true, settle.Filling(Ring, 0, 0, 1100, nameAwaited: true), "still none");
            Equal(true, settle.Filling(Ring, 9, 555, 1200), "the tooltip came: it may still be filling");
            Equal(false, settle.Filling(Ring, 9, 555, 1200 + TooltipSettle.HoldMs), "held still: one line, with its own name");

            Equal(true, settle.Filling(Belt, 0, 0, 5000, nameAwaited: true), "another item waits in its turn");
            Equal(true, settle.Filling(Belt, 0, 0, 5000 + TooltipSettle.NameWaitMs - 1, nameAwaited: true), "up to the limit");
            Equal(false, settle.Filling(Belt, 0, 0, 5000 + TooltipSettle.NameWaitMs, nameAwaited: true), "no tooltip came: the base name is said");
            Equal(false, settle.Filling(Ring, 9, 555, 6000), "the ring was seen whole");
            Equal(false, settle.Filling(Belt, 0, 0, 7000, nameAwaited: true), "an item that showed none is not waited for again");
            Equal(true, settle.Filling(Belt, 4, 77, 7100), "a tooltip that comes after all is still read until it holds still");
            settle.Forget();
            Equal(true, settle.Filling(Belt, 0, 0, 9000, nameAwaited: true), "the panel was rebuilt: its items are new ones");
        }

        private static void ATooltipSeenWholeIsSaidAtOnce()
        {
            var settle = new TooltipSettle();
            settle.Filling(Belt, 7, 222, 1000);
            Equal(false, settle.Filling(Belt, 7, 222, 1000 + TooltipSettle.HoldMs), "held still");
            Equal(false, settle.Filling(Belt, 8, 333, 9000), "built before: the game shows it whole, a change is the item's own");
            settle.Forget();
            Equal(true, settle.Filling(Belt, 7, 222, 20000), "the panel was rebuilt: its tooltips are first ones again");
        }
    }
}
