namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A D-pad press that moves nothing says the control again (<see cref="BlockedMove"/>): the guild page's only button, 2026-11-10.</summary>
    public static class BlockedMoveTests
    {
        private const string Button = "Crear hermandad, botón";

        private static readonly IntPtr Create = new(0x1000);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APressThatMovesNothingSaysTheControlAgain), APressThatMovesNothingSaysTheControlAgain),
            (nameof(AMoveAChangeOrAnyLineIsItsOwnAnswer), AMoveAChangeOrAnyLineIsItsOwnAnswer),
        };

        private static void APressThatMovesNothingSaysTheControlAgain()
        {
            var move = new BlockedMove();
            Equal(false, move.Due(Create, Button, false, 0), "nothing pressed");
            move.Pressed(Create, Button, 1000);
            Equal(false, move.Due(Create, Button, false, 1000 + BlockedMove.WaitMs - 1), "a move may still be on its way");
            Equal(true, move.Due(Create, Button, false, 1000 + BlockedMove.WaitMs), "the page's only button: said again");
            Equal(false, move.Due(Create, Button, false, 1000 + BlockedMove.WaitMs + 16), "once a press");

            move.Pressed(Create, Button, 5000);
            move.Pressed(Create, Button, 5300);
            Equal(false, move.Due(Create, Button, false, 5000 + BlockedMove.WaitMs), "pressed again: counted from the last press");
            Equal(true, move.Due(Create, Button, false, 5300 + BlockedMove.WaitMs), "then once");
        }

        private static void AMoveAChangeOrAnyLineIsItsOwnAnswer()
        {
            var move = new BlockedMove();
            move.Pressed(Create, Button, 1000);
            Equal(false, move.Due(new IntPtr(0x2000), "Unirse, botón", false, 1100), "the cursor moved");
            Equal(false, move.Due(Create, Button, false, 1000 + BlockedMove.WaitMs), "and coming back is not that press");

            move.Pressed(Create, "Volumen, 40, control deslizante", 2000);
            Equal(false, move.Due(Create, "Volumen, 50, control deslizante", false, 2000 + BlockedMove.WaitMs), "the press changed its value");

            move.Pressed(Create, Button, 3000);
            Equal(false, move.Due(Create, Button, true, 3000 + BlockedMove.WaitMs), "something was said meanwhile");

            // Against the bag's edge with the item's tooltip still being read (2026-11-11): its name again cut the tooltip.
            move.Pressed(Create, "Arco corto, raro, columna 10, fila 1", 3500);
            Equal(false, move.Due(Create, "Arco corto, raro, columna 10, fila 1", true, 3516), "its own lines are still being said");
            Equal(false, move.Due(Create, "Arco corto, raro, columna 10, fila 1", false, 3500 + BlockedMove.WaitMs), "and the press is answered for good");

            move.Pressed(IntPtr.Zero, string.Empty, 4000);
            Equal(false, move.Waiting, "no control under the cursor (play, a free cursor): nothing to wait for");
            Equal(false, move.Due(IntPtr.Zero, string.Empty, false, 4000 + BlockedMove.WaitMs), "and nothing to say");
        }
    }
}
