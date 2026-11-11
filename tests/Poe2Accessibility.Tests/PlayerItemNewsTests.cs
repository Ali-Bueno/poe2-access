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
            (nameof(AMovedBitNeedsASecondRound), AMovedBitNeedsASecondRound),
            (nameof(ANodesRowIsBelievedOnlyWhileMostRowsAgree), ANodesRowIsBelievedOnlyWhileMostRowsAgree),
            (nameof(AnotherAreaAtTheSameAddressIsASecondWitness), AnotherAreaAtTheSameAddressIsASecondWitness),
            (nameof(ABuffListMayBeginWithACapitalisedId), ABuffListMayBeginWithACapitalisedId),
            (nameof(AFlaskDrunkIsNotEquippedAgain), AFlaskDrunkIsNotEquippedAgain),
        };

        /// <summary>Drinking makes the flask a new entity in its own slot: "Equipped" on every drink (live 2026-11-11).</summary>
        private static void AFlaskDrunkIsNotEquippedAgain()
        {
            static string Said(List<ItemMove> moves) => string.Join(" | ", moves.Select(move => $"{move.Kind} {move.Name} {move.Where}".Trim()));
            static List<(InventoryName, string)> Items(params (InventoryName, string)[] items) => items.ToList();
            const InventoryName Main = InventoryName.MainInventory1;
            const InventoryName Belt = InventoryName.Flask1;
            var where = Announcements.MoveSlotName(Belt);
            Equal(string.Empty, Said(ItemMoves.Read(Items((Belt, "Frasco de vida, Mágico")), Items((Belt, "Frasco de vida, Mágico")), Announcements.MoveSlotName, false, 0, string.Empty)),
                  "drunk in play: the same flask, made anew where it was");
            Equal(string.Empty, Said(ItemMoves.Read(Items((Belt, "Arco de carcelero")), Items((Belt, "Frasco de vida, Mágico")), Announcements.MoveSlotName, false, 0, string.Empty)),
                  "in play nothing was moved there, even when the new entity's name read wrong");
            Equal(string.Empty, Said(ItemMoves.Read(Items((Belt, "Frasco de vida, Mágico")), Items((Belt, "Frasco de vida, Mágico")), Announcements.MoveSlotName, true, 0, string.Empty)),
                  "drunk with the inventory open");
            Equal($"Equipped Frasco de maná {where}", Said(ItemMoves.Read(Items((Belt, "Frasco de maná")), Items((Main, "Frasco de maná")), Announcements.MoveSlotName, true, 0, string.Empty)),
                  "put on the belt from the inventory");
            Equal($"Equipped Frasco de maná {where} | Unequipped Frasco de vida", Said(ItemMoves.Read(Items((Belt, "Frasco de maná"), (Main, "Frasco de vida")),
                      Items((Main, "Frasco de maná"), (Belt, "Frasco de vida")), Announcements.MoveSlotName, true, 0, string.Empty)),
                  "swapped for another flask from the inventory");
            Equal($"Equipped Frasco de vida {where}", Said(ItemMoves.Read(Items((Belt, "Frasco de vida")), Items(), Announcements.MoveSlotName, false, 0, string.Empty)),
                  "picked up in play straight onto an empty belt slot");
        }

        private static void AnotherAreaAtTheSameAddressIsASecondWitness()
        {
            // The game loaded the next area into the instance it already had: by the address alone the loss was never said.
            const long Instance = 0x1F2_3456_7000;
            var misses = new LossWitnesses();
            Equal(false, misses.Against(LossWitnesses.AreaKey(Instance, "A1B2C3D4")), "one area");
            Equal(false, misses.Against(LossWitnesses.AreaKey(Instance, "A1B2C3D4")), "the same area again");
            Equal(true, misses.Against(LossWitnesses.AreaKey(Instance, "0F0E0D0C")), "another area at that address is the second");
            Equal(LossWitnesses.AreaKey(Instance, "A1B2C3D4"), LossWitnesses.AreaKey(Instance, "A1B2C3D4"), "one key per area");
        }

        private static void ABuffListMayBeginWithACapitalisedId()
        {
            const long Heap = 0x10_0000_0000;
            var memory = new LayoutSearchTests.FakeMemory();
            var block = new byte[0x40];
            LayoutSearchTests.Put(block, 0x10, Heap + 0x100, Heap + 0x108, Heap + 0x110);
            memory.Pointers[Heap + 0x100] = Heap + 0x200;
            memory.Pointers[Heap + 0x208] = Heap + 0x300;
            memory.Pointers[Heap + 0x300] = Heap + 0x400;
            memory.Texts[Heap + 0x400] = "AoD_beam_degen";
            Equal(true, GameReader.Utils.StructShapes.IsBuffList(block, 0x10, memory, 8), "an id with capitals is an id, as IsBuffId reads it");
            memory.Texts[Heap + 0x400] = "Not an Id";
            Equal(false, GameReader.Utils.StructShapes.IsBuffList(block, 0x10, memory, 8), "other text is still none");
            memory.Texts[Heap + 0x400] = "ab";
            Equal(false, GameReader.Utils.StructShapes.IsBuffList(block, 0x10, memory, 8), "nor two letters");
        }

        private static void AMovedBitNeedsASecondRound()
        {
            // Twelve passive tree nodes carried 0x12 and not the navigable bit: one round of one odd kind moves nothing.
            var moved = new MovedBit();
            Equal(false, moved.Confirms(0x12, witness: 1), "one round naming another bit");
            Equal(false, moved.Confirms(0x12, witness: 1), "the same round again is no second witness");
            Equal(true, moved.Confirms(0x12, witness: 2), "a second round names the same bit");

            var other = new MovedBit();
            other.Confirms(0x12, 1);
            Equal(false, other.Confirms(0x0B, 2), "a second round naming another bit confirms neither");
            Equal(true, other.Confirms(0x0B, 3), "until a third agrees with it");

            var cleared = new MovedBit();
            cleared.Confirms(0x12, 1);
            cleared.Clear(); // a round on which the measured bit fitted
            Equal(false, cleared.Confirms(0x12, 2), "what was named before the measured bit fitted no longer counts");
        }

        private static void ANodesRowIsBelievedOnlyWhileMostRowsAgree()
        {
            static CatalogCrossCheck.Verdict After(int compared, int differing)
            {
                var check = new CatalogCrossCheck(compared);
                CatalogCrossCheck.Verdict? verdict = null;
                for (var node = 0; node < compared; node++)
                {
                    verdict = check.Note($"node{node}", node < differing ? "the row's name reads 'x'" : null);
                }

                return verdict!.Value;
            }

            Equal(true, After(64, 0).RowsDecide, "the rows agree with the files: one that differs later is the game's own word");
            Equal(true, After(64, 3).RowsDecide, "a patch renamed three nodes after the catalog was made: their rows are said");
            Equal(false, After(64, 40).RowsDecide, "most rows differ: their columns moved, the files are said");
            Equal(false, After(64, 33).RowsDecide, "just over half");
            Equal(true, After(64, 32).RowsDecide, "half is not most");
        }

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
