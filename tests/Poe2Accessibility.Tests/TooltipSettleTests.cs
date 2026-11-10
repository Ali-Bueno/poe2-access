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
