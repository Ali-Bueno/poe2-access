namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     Values the interface, the inventory, the scanner and the speech queue must not say after the game changed them
    ///     (docs/research/robustness-audit-2026-11-10.md, "Stale values").
    /// </summary>
    public static class StaleValueTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ANumberChangingUnderTheCursorIsSaidAlone), ANumberChangingUnderTheCursorIsSaidAlone),
            (nameof(AClockInTheFocusedRowStaysQuiet), AClockInTheFocusedRowStaysQuiet),
        };

        private static void ANumberChangingUnderTheCursorIsSaidAlone()
        {
            Equal("2", FocusNews.ChangedNumbers(UiRole.GridCell, "Orbe de aumento, 3", "Orbe de aumento, 2"), "a stack used under the cursor");
            Equal(false, FocusNews.IsTick(UiRole.GridCell, "Orbe de aumento, 3", "Orbe de aumento, 2"), "and it is no clock");
            Equal("Nivel 8", FocusNews.ChangedNumbers(UiRole.ListItem, "Colmillos de escarcha, Nivel 7", "Colmillos de escarcha, Nivel 8"), "a gem levelled under the cursor");
            Equal(null, FocusNews.ChangedNumbers(UiRole.ItemSlot, "Anillo, 3, raro", "Anillo, 12, mágico"), "more than a number changed: the whole slot's news");
            Equal("DPS: 13,1", FocusNews.ChangedNumbers(UiRole.ListItem, "NIVEL: 1, Rayo atronador, DPS: 12,5", "NIVEL: 1, Rayo atronador, DPS: 13,1"), "a row's own figure");
            Equal(null, FocusNews.ChangedNumbers(UiRole.GridCell, "Orbe de aumento, 3", "Orbe de aumento, 3"), "nothing changed");
            Equal(null, FocusNews.ChangedNumbers(UiRole.GridCell, "Orbe de aumento, 2", "Orbe de aumento"), "the count gone is the whole cell's news");
            Equal(null, FocusNews.ChangedNumbers(UiRole.Button, "Comprar, 3", "Comprar, 2"), "a button's caption is not an item's number");
        }

        private static void AClockInTheFocusedRowStaysQuiet()
        {
            const string Before = "Entrar a la instancia, San Pablo (BR), Creada:12:28:45, 11 minutos 46 segundos";
            const string After = "Entrar a la instancia, San Pablo (BR), Creada:12:28:45, 11 minutos 45 segundos";
            Equal(true, FocusNews.IsTick(UiRole.Button, Before, After), "the Instance Manager's row, a button");
            Equal(true, FocusNews.IsTick(UiRole.ListItem, Before, After), "the same row as a list's");
            Equal(true, FocusNews.IsTick(UiRole.ListItem, "Sala, 0:59", "Sala, 0:58"), "a clock's digits");
            Equal(false, FocusNews.IsTick(UiRole.ListItem, "Sala, 0:59", "Otra sala, 0:58"), "another text is no tick");
        }
    }
}
