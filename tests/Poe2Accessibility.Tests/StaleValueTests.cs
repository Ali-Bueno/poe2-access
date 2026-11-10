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
            (nameof(AMovedItemIsNamedAsItWasWhenItLeft), AMovedItemIsNamedAsItWasWhenItLeft),
            (nameof(AGroundTooltipIsItsOwnItems), AGroundTooltipIsItsOwnItems),
            (nameof(AVanishedTargetIsReachedOnlyIfUsingItRemovesIt), AVanishedTargetIsReachedOnlyIfUsingItRemovesIt),
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

        /// <summary>A stack of 10 scrolls, 3 used, then sold: "…, 7"; an item made magic in place; "unknown item" never kept.</summary>
        private static void AMovedItemIsNamedAsItWasWhenItLeft()
        {
            const int Normal = 0;
            const int Magic = 1;
            var names = new ItemReadings(() => "unknown item");
            var scrolls = (IntPtr)0x10;
            Equal(true, names.IsStale(scrolls, 10, Normal), "an item never read");
            Equal("Scroll of Wisdom, 10", names.Read(scrolls, "Scroll of Wisdom, 10", 10, Normal), "its first reading");
            Equal(false, names.IsStale(scrolls, 10, Normal), "unchanged, it is not read again");
            Equal(true, names.IsStale(scrolls, 7, Normal), "three used: read again");
            names.Read(scrolls, "Scroll of Wisdom, 7", 7, Normal);
            Equal("Scroll of Wisdom, 7", names.Last(scrolls), "sold as what it was when it left");

            var ring = (IntPtr)0x20;
            names.Read(ring, "Iron Ring", 0, Normal);
            Equal(true, names.IsStale(ring, 0, Magic), "made magic in place: read again");
            Equal("Iron Ring, magic", names.Read(ring, "Iron Ring, magic", 0, Magic), "with its rarity");
            Equal("Iron Ring, magic", names.Read(ring, null, 0, Magic), "a reading that fails leaves the last good one");
            Equal("Iron Ring, magic", names.Read(ring, "unknown item, magic", 0, Magic), "and so does one that names nothing");

            var gem = (IntPtr)0x30;
            Equal("unknown item", names.Read(gem, "unknown item", 0, Normal), "an unnamed item is said as such");
            Equal(true, names.IsStale(gem, 0, Normal), "but never kept: the next poll reads it again");
            Equal("Twister", names.Read(gem, "Twister", 0, Normal), "and names it once it reads");

            names.KeepOnly(item => item == ring);
            Equal("unknown item", names.Last(scrolls), "an item gone from every inventory is forgotten");
            Equal("Iron Ring, magic", names.Last(ring), "the ones in reach are kept");
        }

        /// <summary>Two stacks of one currency on the ground, 3 and 7: the tooltip is the targeted one's, never the last listed.</summary>
        private static void AGroundTooltipIsItsOwnItems()
        {
            var three = new GroundTooltip.Item("Orb of Augmentation", "Orb of Augmentation", "Orb of Augmentation, 3");
            var seven = new GroundTooltip.Item("Orb of Augmentation", "Orb of Augmentation", "Orb of Augmentation, 7");
            var rare = new GroundTooltip.Item("Long Staff", "Long Staff", "Long Staff, rare");
            var gold = new GroundTooltip.Item("Gold", string.Empty, string.Empty);
            var tooltip = new[] { "Orb of Augmentation", "Stack Size: 3/20" };
            var remembered = new Dictionary<string, string>();
            foreach (var item in new[] { three, seven, rare })
            {
                GroundTooltip.Remember(remembered, item.Title, item.Spoken);
            }

            string? Remembered(string title) => remembered.TryGetValue(title, out var spoken) ? spoken : null;
            Equal("Orb of Augmentation, 3", GroundTooltip.Name(tooltip, three, new[] { three, seven }, Remembered), "the stack the game targets");
            Equal("Orb of Augmentation, 7", GroundTooltip.Name(tooltip, seven, new[] { three, seven }, Remembered), "whichever was listed last");
            Equal("Orb of Augmentation", GroundTooltip.Name(tooltip, null, new[] { three, seven }, Remembered), "no target: the title, not one stack's count");
            Equal("Orb of Augmentation", GroundTooltip.Name(tooltip, rare, new[] { three, seven, rare }, Remembered), "a target the tooltip does not show is not its item");
            Equal("Orb of Augmentation, 7", GroundTooltip.Name(tooltip, null, new[] { seven }, Remembered), "one left in view: its own count");
            Equal("Orb of Augmentation", GroundTooltip.Name(tooltip, null, Array.Empty<GroundTooltip.Item>(), Remembered), "both gone: two were seen, the count is not known");
            Equal("Long Staff, rare", GroundTooltip.Name(new[] { "Fierce Long Staff of Impact", "Long Staff" }, null, Array.Empty<GroundTooltip.Item>(), Remembered), "a picked-up item's late tooltip, by its base line");
            Equal(string.Empty, GroundTooltip.Name(new[] { "Gold" }, null, new[] { gold, gold }, Remembered), "gold says nothing, however many piles");
            Equal(null, GroundTooltip.Name(new[] { "New checkpoint" }, three, new[] { three }, Remembered), "a text that is no item's");
        }

        /// <summary>The guide's target stops being listed 300 units away: a chest was opened, a boss only hid.</summary>
        private static void AVanishedTargetIsReachedOnlyIfUsingItRemovesIt()
        {
            const float Near = 600f;
            ScanEntry Entry(ScanCategory category, string kind = "", bool questMarked = false) => new(7, category, "x", 0, 0, 0, kind, QuestMarked: questMarked);
            Equal(GoneTarget.Reached, BeaconMath.Gone(Entry(ScanCategory.Chests), true, 300f, Near), "a chest opened");
            Equal(GoneTarget.Reached, BeaconMath.Gone(Entry(ScanCategory.Items), true, 300f, Near), "an item picked up");
            Equal(GoneTarget.Reached, BeaconMath.Gone(Entry(ScanCategory.Waypoints, ScanEntry.PortalKind), true, 300f, Near), "a portal taken");
            Equal(GoneTarget.Reached, BeaconMath.Gone(Entry(ScanCategory.Objects, questMarked: true), true, 300f, Near), "a quest object used");
            Equal(GoneTarget.LostFromView, BeaconMath.Gone(Entry(ScanCategory.Enemies), true, 300f, Near), "a boss that hid, burrowed or died");
            Equal(GoneTarget.LostFromView, BeaconMath.Gone(Entry(ScanCategory.Enemies, questMarked: true), true, 300f, Near), "the quest boss too");
            Equal(GoneTarget.LostFromView, BeaconMath.Gone(Entry(ScanCategory.Npcs), true, 300f, Near), "an NPC that left");
            Equal(GoneTarget.LostFromView, BeaconMath.Gone(Entry(ScanCategory.Waypoints, "checkpoint"), true, 300f, Near), "a checkpoint does not go when used");
            Equal(GoneTarget.Unknown, BeaconMath.Gone(Entry(ScanCategory.Chests), true, 900f, Near), "far away nothing is known");
            Equal(GoneTarget.Unknown, BeaconMath.Gone(Entry(ScanCategory.Places), false, 300f, Near), "a named place is not an entity");
        }
    }
}
