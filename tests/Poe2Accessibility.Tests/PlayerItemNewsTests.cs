namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The player's and the items' news after the second audit of 2026-11-10: what a rule made quiet that was still news, and what was said twice.</summary>
    public static class PlayerItemNewsTests
    {
        private const string Tailwind = "buff:tailwind";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ABuffThatRunsOutIsToldHoweverOftenItComes), ABuffThatRunsOutIsToldHoweverOftenItComes),
            (nameof(ABuffThatHoldsACountIsNoButtons), ABuffThatHoldsACountIsNoButtons),
            (nameof(WhatAnAreaCameWithIsNotHeldBack), WhatAnAreaCameWithIsNotHeldBack),
            (nameof(ADurationIsAPositiveFiniteNumber), ADurationIsAPositiveFiniteNumber),
            (nameof(ACountBackWhereItWasIsNotSaidAgain), ACountBackWhereItWasIsNotSaidAgain),
            (nameof(WhatATradeGivesAndTakesIsBothSaid), WhatATradeGivesAndTakesIsBothSaid),
        };

        private static void WhatATradeGivesAndTakesIsBothSaid()
        {
            static string Said(List<ItemMove> moves) => string.Join(" | ", moves.Select(move => $"{move.Kind} {move.Name} {move.Where}".Trim()));
            const InventoryName Main = InventoryName.MainInventory1;
            var ring = new List<(InventoryName, string)> { (Main, "Anillo de hierro") };
            var orbs = new List<(InventoryName, string)> { (Main, "Orbe de caos, 3") };

            // One item for another: paired as one item in a new place, neither was said.
            Equal("Obtained Anillo de hierro | DroppedOrUsed Orbe de caos, 3", Said(ItemMoves.Read(ring, orbs, Announcements.SlotName, true, 0, string.Empty, inTrade: true)),
                  "a trade: what came was received and what left was handed over");
            Equal(string.Empty, Said(ItemMoves.Read(ring, orbs, Announcements.SlotName, true, 0, string.Empty)),
                  "outside a trade an arrival beside a departure is still the inventory's own item, changed in place");
            Equal("Obtained Anillo de hierro | DroppedOrUsed Orbe de caos, 3", Said(ItemMoves.Read(ring, orbs, Announcements.SlotName, true, 0, "Comercio", inTrade: true)),
                  "the trade window is no panel the items moved to or were taken from");
            var unread = new List<(InventoryName, string)> { (Main, Announcements.UnknownItem) };
            Equal(string.Empty, Said(ItemMoves.Read(ring, unread, Announcements.SlotName, true, 0, string.Empty, inTrade: true)),
                  "an item whose departure never read, moved inside the inventory while the trade is open, is no news");
            var shards = new List<(InventoryName, string)> { (Main, "Frasco de vida"), (Main, "Esquirla de transmutación") };
            Equal("DroppedOrUsed Frasco de vida", Said(ItemMoves.Read(new List<(InventoryName, string)> { (Main, "Esquirla de transmutación, 2") }, shards, Announcements.SlotName, true, 0, string.Empty, inTrade: true)),
                  "a stack that grew is still no departure and no arrival");
        }

        private static void ABuffThatRunsOutIsToldHoweverOftenItComes()
        {
            // A warcry's buff, used every twenty seconds: it comes and goes six times in two minutes and is news each time.
            var steady = new SteadyBuffs();
            for (var use = 0; use < 6; use++)
            {
                var now = 10_000L + (use * 20_000);
                Equal(true, steady.Tells("buff:warcry", timed: true, now), $"use {use}: told from its start");
                Equal(0, steady.EndReading(now).Count, "held");
                Equal(0, steady.EndReading(now + 10_000).Count, $"use {use}: run out, and not a button let go");
            }

            Equal(false, steady.Quiet.Contains("buff:warcry"), "a buff that runs out never becomes a button's effect");
        }

        private static void ABuffThatHoldsACountIsNoButtons()
        {
            // Tailwind: no duration known, stacks built by skills and lost whole at every hit taken.
            var steady = new SteadyBuffs();
            for (var hit = 0; hit < 5; hit++)
            {
                var now = 10_000L + (hit * 8000);
                steady.Tells(Tailwind, timed: false, now, stacks: 1);
                steady.EndReading(now);
                Equal(true, steady.Tells(Tailwind, timed: false, now + 500, stacks: 3), $"hit {hit}: a count is told at once");
                steady.EndReading(now + 500);
                Equal(true, steady.Tells(Tailwind, timed: false, now + 600, stacks: 1), "and stays told while this holding lasts");
                steady.EndReading(now + 600);
                Equal(0, steady.EndReading(now + 1000).Count, "lost to a hit");
            }

            Equal(false, steady.Quiet.Contains(Tailwind), "what holds a count is never a button's effect");
        }

        private static void WhatAnAreaCameWithIsNotHeldBack()
        {
            // The tracker's baseline lasts as long as the wait: held back, an aura would be told as new the moment both end.
            var steady = new SteadyBuffs();
            Equal(true, steady.Tells("buff:aura", timed: false, 5000, alreadyThere: true), "there on entering: it has stayed");
            steady.EndReading(5000);
            Equal(true, steady.Tells("buff:aura", timed: false, 5100), "and still at the next reading");
            steady.EndReading(5100);
            Equal(false, steady.Tells("buff:parry", timed: false, 5100 + SteadyBuffs.HoldMs), "one that begins later waits");
        }

        private static void ADurationIsAPositiveFiniteNumber()
        {
            Equal(true, SteadyBuffs.RunsOut(12.5f), "seconds");
            Equal(false, SteadyBuffs.RunsOut(0f), "zero");
            Equal(false, SteadyBuffs.RunsOut(-1f), "negative");
            Equal(false, SteadyBuffs.RunsOut(float.PositiveInfinity), "infinity is no duration");
            Equal(false, SteadyBuffs.RunsOut(float.NaN), "nor what is no number");
        }

        private static void ACountBackWhereItWasIsNotSaidAgain()
        {
            static ResourceTracker.Reading Jade(int count) => new("buff:jade", count, 0, ResourceMode.Counted);
            var tracker = new ResourceTracker();
            Equal(true, tracker.InBaseline(0), "before the first reading");
            tracker.Update(new[] { Jade(3) }, 0);
            Equal(false, tracker.InBaseline(ResourceTracker.BaselineGraceMs), "the grace over");

            // Taken off and put on again at once (a buff cast again): absent in one reading, back in the next.
            var now = 10_000L;
            Equal(0, tracker.Update(Array.Empty<ResourceTracker.Reading>(), now).Count, "gone, not settled");
            Equal(0, tracker.Update(new[] { Jade(3) }, now + 100).Count, "back");
            Equal(0, tracker.Update(new[] { Jade(3) }, now + 100 + ResourceTracker.SettleMs).Count, "settled where the player knew it: nothing to say");

            // A stack gained and spent at once.
            now = 20_000L;
            tracker.Update(new[] { Jade(4) }, now);
            tracker.Update(new[] { Jade(3) }, now + 100);
            Equal(0, tracker.Update(new[] { Jade(3) }, now + 100 + ResourceTracker.SettleMs).Count, "three again: said before");

            // A real change is still said, once.
            now = 30_000L;
            tracker.Update(new[] { Jade(5) }, now);
            var news = tracker.Update(new[] { Jade(5) }, now + ResourceTracker.SettleMs);
            Equal(1, news.Count, "five is news");
            Equal(5, news[0].Count, "its count");
            Equal(0, tracker.Update(new[] { Jade(5) }, now + (2 * ResourceTracker.SettleMs)).Count, "once");
        }
    }
}
