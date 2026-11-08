namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using GameHelper.Utils;
    using static LayoutSearchTests;

    /// <summary>The host's searches added for the field-protection gaps, on synthetic memory (docs/research/field-protection-audit.md).</summary>
    public static class HostSearchTests
    {
        private const long Heap = 0x20_0000_0000;
        private const int EntityStride = 0x10000;
        private static readonly string[] ComponentNames = { "Positioned", "Render", "Life", "Actor", "Player" };

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EntityFieldsWhereMeasuredStayThere), EntityFieldsWhereMeasuredStayThere),
            (nameof(EntityFieldsAreFoundWhereAPatchMovedThem), EntityFieldsAreFoundWhereAPatchMovedThem),
            (nameof(WhatIsNoEntityGivesNoEntityLayout), WhatIsNoEntityGivesNoEntityLayout),
            (nameof(AMapOfOtherNamesIsNoComponentMap), AMapOfOtherNamesIsNoComponentMap),
            (nameof(TheStateListAndStackAreFoundWhereverTheyMoved), TheStateListAndStackAreFoundWhereverTheyMoved),
            (nameof(PointersThatRepeatAreNoStateList), PointersThatRepeatAreNoStateList),
            (nameof(TheStateInPlayIsTheOneThatLeadsToAnArea), TheStateInPlayIsTheOneThatLeadsToAnArea),
            (nameof(TheLoadingStateIsWhatCameBeforeTwoNewAreas), TheLoadingStateIsWhatCameBeforeTwoNewAreas),
            (nameof(ElementBaseFieldsWhereMeasuredStayThere), ElementBaseFieldsWhereMeasuredStayThere),
            (nameof(ElementBaseGroupsFollowTheShiftThatFits), ElementBaseGroupsFollowTheShiftThatFits),
            (nameof(AnElementOfAnotherSizeIsNoRoot), AnElementOfAnotherSizeIsNoRoot),
            (nameof(TheFocusIsTheSlotThatTakesEachElementFirst), TheFocusIsTheSlotThatTakesEachElementFirst),
            (nameof(AVitalThatGrewInsideIsReadAgain), AVitalThatGrewInsideIsReadAgain),
            (nameof(AGroundItemLeadsToAnItem), AGroundItemLeadsToAnItem),
            (nameof(InventoriesAreFoundByTheItemsInThem), InventoriesAreFoundByTheItemsInThem),
        };

        private static void InventoriesAreFoundByTheItemsInThem()
        {
            var measured = InventoryFields.Measured;
            foreach (var shift in new[] { 0, 8, -0x18 })
            {
                var at = new InventoryFields(measured.Players + shift, measured.Inventories - shift, measured.Boxes + shift, measured.Cells + shift);
                var (memory, server) = Inventories(at, "Metadata/Items/Weapons/TwoHandWeapons/TwoHandMaces/TwoHandMace1");
                Equal(at, new InventoryLayoutSearch(memory).Find(server, measured), $"inventory fields moved by {shift:X}");
            }

            var (monsters, address) = Inventories(measured, "Metadata/Monsters/FungusZombie/FungusZombieFungalmancer");
            Equal(new InventoryFields(-1, -1, -1, -1), new InventoryLayoutSearch(monsters).Find(address, measured), "cells that hold no item");
        }

        /// <summary>Server data with one player and three inventories (12 x 5 with an item, 5 x 1 empty, 2 x 4 with the same item twice).</summary>
        private static (FakeMemory Memory, long Server) Inventories(InventoryFields at, string itemPath)
        {
            var memory = new FakeMemory();
            var (server, players, player, entries, item, entity) = (Heap + 0x300000, Heap + 0x301000, Heap + 0x302000, Heap + 0x303000, Heap + 0x304000, Heap + 0x305000);
            var serverBody = new byte[InventoryFields.Measured.Players + InventoryLayoutSearch.Reach];
            Put(serverBody, at.Players, players, players + 8, players + 8);
            memory.Blocks[server] = serverBody;
            memory.Blocks[players] = BitConverter.GetBytes(player);
            var playerBody = new byte[InventoryFields.Measured.Inventories + InventoryLayoutSearch.Reach];
            var sizes = new[] { (12, 5), (5, 1), (2, 4) };
            Put(playerBody, at.Inventories, entries, entries + (sizes.Length * 0x18), entries + (sizes.Length * 0x18));
            memory.Blocks[player] = playerBody;
            var entryBytes = new byte[sizes.Length * 0x18];
            for (var i = 0; i < sizes.Length; i++)
            {
                var inventory = Heap + 0x310000 + (i * 0x1000);
                var cells = inventory + 0x800;
                Put(entryBytes, i * 0x18, i + 1, inventory, inventory - 0x10);
                var body = new byte[InventoryFields.Measured.Cells + InventoryLayoutSearch.Reach];
                BitConverter.TryWriteBytes(body.AsSpan(at.Boxes), sizes[i].Item1);
                BitConverter.TryWriteBytes(body.AsSpan(at.Boxes + 4), sizes[i].Item2);
                var count = sizes[i].Item1 * sizes[i].Item2;
                Put(body, at.Cells, cells, cells + (count * 8), cells + (count * 8));
                memory.Blocks[inventory] = body;
                var slots = new byte[count * 8];
                if (i != 1)
                {
                    Put(slots, 0, item, item);
                }

                memory.Blocks[cells] = slots;
            }

            memory.Blocks[entries] = entryBytes;
            memory.Blocks[item] = BitConverter.GetBytes(entity);
            memory.Paths[entity] = itemPath;
            return (memory, server);
        }

        private static void AGroundItemLeadsToAnItem()
        {
            var memory = new FakeMemory();
            memory.Paths[Heap + 0x100] = "Metadata/Items/Currency/CurrencyRerollRare";
            memory.Paths[Heap + 0x200] = "Metadata/Monsters/FungusZombie/FungusZombieFungalmancer";
            var block = new byte[0x80];
            Put(block, 0x8, Heap + 0x200, 0, 0, 0, Heap + 0x100);
            Equal(0x28, StructShapes.Nearest(0x30, 8, block.Length - 8, at => StructShapes.IsItemEntity(block, at, memory)), "the item, not the monster beside it");
            Equal(false, StructShapes.IsItemEntity(block, 0x8, memory), "a monster is no item");
        }

        private static void AVitalThatGrewInsideIsReadAgain()
        {
            const long component = Heap + 0x7000;
            foreach (var (back, total) in new[] { (8, 0x2C), (0x10, 0x34), (8, 0x1C) })
            {
                var block = new byte[0x600];
                foreach (var (at, life) in new[] { (0x1B0, 812), (0x208, 120), (0x248, 0), (0x2E8, 0), (0x338, 30) })
                {
                    Put(block, at + back, component);
                    BitConverter.TryWriteBytes(block.AsSpan(at + total), life + 100);
                    BitConverter.TryWriteBytes(block.AsSpan(at + total + 4), life);
                }

                // A start 8 bytes later with the measured pair reads the same bytes: only the gap between the two says the layout.
                var found = StructShapes.VitalLayout(block, component, 8, 0x2C, 4, 3);
                Equal((int?)(total - back), found?.Total - found?.BackPointer, $"vitals with the back-pointer at {back:X} and the total at {total:X}");
            }

            Equal(((int, int)?)null, StructShapes.VitalLayout(new byte[0x600], component, 8, 0x2C, 4, 3), "no vital at all");

            // Full vitals with the total 4 bytes early: the measured places read each current as a total and a 0 after it.
            var early = new byte[0x600];
            foreach (var at in new[] { 0x1B0, 0x208, 0x248 })
            {
                Put(early, at + 8, component);
                BitConverter.TryWriteBytes(early.AsSpan(at + 0x28), 500);
                BitConverter.TryWriteBytes(early.AsSpan(at + 0x2C), 500);
            }

            var measuredReads = StructShapes.Vitals(early, component, 8, 0x2C, 0x30);
            Equal(0, StructShapes.Full(early, measuredReads, 0x2C, 4), "no vital reads full one field late");
            Equal(((int, int)?)(8, 0x28), StructShapes.VitalLayout(early, component, 8, 0x2C, 4, 3, true), "full vitals place the total");
        }

        private static void TheFocusIsTheSlotThatTakesEachElementFirst()
        {
            const int measured = 0x748;
            foreach (var shift in new[] { 0, 8, -0x18 })
            {
                var check = new Poe2Accessibility.Services.FocusSlotCheck(measured);
                var focus = measured + shift;
                int? judged = null;
                long previous = 0;

                // A root-like element that never changes sits beside them; the previous-focus slot lags one step behind.
                foreach (var element in new long[] { 0x1000, 0x2000, 0x3000, 0x4000, 0x5000 })
                {
                    var slots = new Dictionary<int, long> { [measured - 0x10] = 0x9000, [focus] = element, [focus + 8] = previous };
                    slots.TryAdd(measured, 0);
                    judged = check.See(slots);
                    previous = element;
                }

                Equal((int?)focus, judged, $"focus moved by {shift:X}");
            }

            var still = new Poe2Accessibility.Services.FocusSlotCheck(measured);
            for (var look = 0; look < 10; look++)
            {
                still.See(new Dictionary<int, long> { [measured] = 0x1000, [measured + 8] = 0x2000 });
            }

            Equal((int?)null, still.Judged, "a focus that never moves proves nothing");
        }

        private static void ElementBaseFieldsWhereMeasuredStayThere()
        {
            var measured = UiElementFields.Measured;
            var (memory, root) = UiTree(measured);
            Equal(measured, new UiElementLayoutSearch(memory).Find(root, measured), "element fields that did not move");
        }

        private static void ElementBaseGroupsFollowTheShiftThatFits()
        {
            var measured = UiElementFields.Measured;
            foreach (var (tree, rest) in new[] { (8, 8), (8, 0x18), (-8, 0x10), (0, -0x18) })
            {
                var moved = new UiElementFields(measured.Self + tree, measured.Children + tree, measured.Parent + tree, measured.Position + rest, measured.Flags + rest, measured.Size + rest);
                var (memory, root) = UiTree(moved);
                Equal(moved, new UiElementLayoutSearch(memory).Find(root, measured), $"tree moved {tree:X}, the rest {rest:X}");
            }
        }

        private static void AnElementOfAnotherSizeIsNoRoot()
        {
            var measured = UiElementFields.Measured;
            var (memory, root) = UiTree(measured, 1920);
            Equal(-1, new UiElementLayoutSearch(memory).Find(root, measured).Size, "an element 1920 wide is no root");
        }

        /// <summary>A UI root laid out as <paramref name="at"/> says, with three layers (one hidden) and a grandchild.</summary>
        private static (FakeMemory Memory, long Root) UiTree(UiElementFields at, float width = 2560)
        {
            var memory = new FakeMemory();
            var root = Heap + 0x200000;
            var layers = new[] { root + 0x1000, root + 0x2000, root + 0x3000 };
            Element(memory, at, root, 0, layers, true, width);
            Element(memory, at, layers[0], root, new[] { root + 0x4000 }, true, 800);
            Element(memory, at, layers[1], root, Array.Empty<long>(), false, 800);
            Element(memory, at, layers[2], root, Array.Empty<long>(), true, 800);
            Element(memory, at, root + 0x4000, layers[0], Array.Empty<long>(), true, 100);
            return (memory, root);
        }

        private static void Element(FakeMemory memory, UiElementFields at, long address, long parent, long[] children, bool visible, float width)
        {
            var body = new byte[UiElementLayoutSearch.BlockBytes];
            Put(body, 0, Heap + 0x900000); // the vtable
            Put(body, at.Self, address);
            var storage = address + 0x800;
            Put(body, at.Children, storage, storage + (children.Length * 8), storage + (children.Length * 8));
            Put(body, at.Parent, parent);
            Put(body, at.Position, 0, 0);
            BitConverter.TryWriteBytes(body.AsSpan(at.Position + 0x18), 1f);
            BitConverter.TryWriteBytes(body.AsSpan(at.Flags), visible ? 0x00502EF4u : 0x00502EF4u & ~(1u << 0x0B));
            body[at.Flags + 0xA] = parent == 0 ? (byte)3 : (byte)2;
            BitConverter.TryWriteBytes(body.AsSpan(at.Size), width);
            BitConverter.TryWriteBytes(body.AsSpan(at.Size + 4), 1600f);
            memory.Blocks[address] = body;
            if (children.Length > 0)
            {
                var vector = new byte[children.Length * 8];
                for (var i = 0; i < children.Length; i++)
                {
                    Put(vector, i * 8, children[i]);
                }

                memory.Blocks[storage] = vector;
            }
        }

        private static void TheStateListAndStackAreFoundWhereverTheyMoved()
        {
            var measured = GameStateFields.Measured;
            foreach (var shift in new[] { 0, 8, -8, 0x18 })
            {
                var (memory, block, _) = StateObject(measured.Stack + shift, measured.States + shift);
                Equal(new GameStateFields(measured.Stack + shift, measured.States + shift), GameStateLayout.Locate(block, measured, memory), $"state fields moved by {shift:X}");
            }
        }

        private static void PointersThatRepeatAreNoStateList()
        {
            var (memory, block, _) = StateObject(GameStateFields.Measured.Stack, GameStateFields.Measured.States);
            for (var slot = 0; slot < 13; slot++)
            {
                Put(block, GameStateFields.Measured.States + (slot * 0x10), Heap + 0x500, Heap + 0x600);
            }

            Equal(new GameStateFields(-1, -1), GameStateLayout.Locate(block, GameStateFields.Measured, memory), "one state thirteen times");
        }

        private static void TheStateInPlayIsTheOneThatLeadsToAnArea()
        {
            var states = Slots();
            var roles = new GameStateRoles(states);
            roles.See(states[6], 0);
            Equal((-1, -1), (roles.InGame, roles.AreaLoading), "a login screen says nothing");
            roles.See(states[5], Heap + 0x9000);
            Equal((5, -1), (roles.InGame, roles.AreaLoading), "the state that leads to an area is in play");
        }

        private static void TheLoadingStateIsWhatCameBeforeTwoNewAreas()
        {
            var states = Slots();
            var roles = new GameStateRoles(states);
            roles.See(states[4], Heap + 0x9000);
            roles.See(states[3], 0);
            roles.See(states[4], Heap + 0x9000);
            Equal(-1, roles.AreaLoading, "a menu over the same area loads nothing");
            roles.See(states[0], 0);
            roles.See(states[4], Heap + 0xA000);
            Equal(-1, roles.AreaLoading, "one new area is not proof yet");
            roles.See(states[0], 0);
            roles.See(states[4], Heap + 0xB000);
            Equal((4, 0), (roles.InGame, roles.AreaLoading), "the state before two new areas loads them");
        }

        private static long[] Slots()
        {
            var states = new long[13];
            for (var slot = 0; slot < states.Length; slot++)
            {
                states[slot] = slot is 1 or 2 ? 0 : Heap + 0x40000 + (slot * 0x1000); // 0.5.5f: two unused slots after the first
            }

            return states;
        }

        /// <summary>A game state object with its stack at <paramref name="stack"/> (current: slot 4) and its list at <paramref name="list"/>.</summary>
        private static (FakeMemory Memory, byte[] Block, long[] States) StateObject(int stack, int list)
        {
            var memory = new FakeMemory();
            var block = new byte[GameStateLayout.BlockBytes];
            var states = Slots();
            for (var slot = 0; slot < states.Length; slot++)
            {
                Put(block, list + (slot * 0x10), states[slot], states[slot] == 0 ? 0 : states[slot] + 0x800);
            }

            var entries = Heap + 0x30000;
            Put(block, stack, entries, entries + 0x20, entries + 0x40);
            memory.Pointers[entries + 0x10] = states[4];

            // After the list a pointer with no control block (0.5.5f); before the stack, a vector whose last entry is no state.
            Put(block, list + 0xD0, Heap + 0x700, 0);
            Put(block, stack + 0x18, entries + 0x100, entries + 0x120, entries + 0x120);
            return (memory, block, states);
        }

        private static void EntityFieldsWhereMeasuredStayThere()
        {
            var memory = new FakeMemory();
            var measured = EntityLayout.Measured;
            var entities = new[] { Entity(memory, 0, measured), Entity(memory, 1, measured) };
            Equal(measured, new EntityLayoutSearch(memory).Find(entities, measured), "entity fields that did not move");
            Equal(true, new EntityLayoutSearch(memory).ReadsPath(entities[0], measured), "the path read where measured");
        }

        private static void EntityFieldsAreFoundWhereAPatchMovedThem()
        {
            var measured = EntityLayout.Measured;
            foreach (var shift in new[] { 8, 0x18, -8 })
            {
                var memory = new FakeMemory();
                var moved = new EntityLayout(measured.Details + 8, measured.Path + shift, measured.Components + 0x18, measured.LookUp + shift, measured.NameMap - 0x18);
                var entities = new[] { Entity(memory, 0, moved), Entity(memory, 1, moved) };
                Equal(moved, new EntityLayoutSearch(memory).Find(entities, measured), $"entity fields moved by {shift:X}");
                Equal(false, new EntityLayoutSearch(memory).ReadsPath(entities[0], measured), "the path no longer read where measured");
            }
        }

        private static void WhatIsNoEntityGivesNoEntityLayout()
        {
            var memory = new FakeMemory();
            memory.Blocks[Heap] = new byte[EntityLayoutSearch.BlockBytes];
            Put(memory.Blocks[Heap], 0, Heap + 0x100, Heap + 0x200, Heap + 0x300, Heap + 0x300);
            var found = new EntityLayoutSearch(memory).Find(new[] { Heap, 0L }, EntityLayout.Measured);
            Equal(new EntityLayout(-1, -1, -1, -1, -1), found, "a block of pointers that lead nowhere");
        }

        private static void AMapOfOtherNamesIsNoComponentMap()
        {
            var memory = new FakeMemory();
            var measured = EntityLayout.Measured;
            var entity = Entity(memory, 0, measured, new[] { "abc_def", "x", "Unheard" });
            var found = new EntityLayoutSearch(memory).Find(new[] { entity }, measured);
            Equal((measured.Details, measured.Components, -1), (found.Details, found.Components, found.LookUp), "names that are no known components");
        }

        /// <summary>Entity <paramref name="index"/> laid out as <paramref name="at"/> says, with its details, path, lookup and components.</summary>
        private static long Entity(FakeMemory memory, int index, EntityLayout at, string[]? names = null)
        {
            names ??= ComponentNames;
            var entity = Heap + 0x100000 + (index * EntityStride);
            var (details, text, lookUp, entries, vector) = (entity + 0x1000, entity + 0x2000, entity + 0x3000, entity + 0x4000, entity + 0x5000);
            var body = new byte[EntityLayoutSearch.BlockBytes];
            Put(body, 0, entity + 0x8000); // the vtable, a pointer that leads nowhere readable
            Put(body, at.Details, details);
            Put(body, at.Components, vector, vector + (names.Length * 8), vector + (names.Length * 8));
            memory.Blocks[entity] = body;

            var path = "Metadata/Monsters/FungusZombie/FungusZombieFungalmancer";
            var detailsBody = new byte[EntityLayoutSearch.BlockBytes];
            Put(detailsBody, at.Path, text, 0, path.Length, 0x3F);
            Put(detailsBody, at.LookUp, lookUp);
            memory.Blocks[details] = detailsBody;
            memory.Texts[text] = path;

            var lookUpBody = new byte[EntityLayoutSearch.BlockBytes];
            Put(lookUpBody, at.NameMap, entries, entries + (names.Length * 0x10), entries + (names.Length * 0x10));
            memory.Blocks[lookUp] = lookUpBody;

            var map = new byte[names.Length * 0x10];
            var components = new byte[names.Length * 8];
            for (var i = 0; i < names.Length; i++)
            {
                var name = entity + 0x6000 + (i * 0x40);
                var bytes = new byte[0x40];
                Encoding.ASCII.GetBytes(names[i]).CopyTo(bytes, 0);
                memory.Blocks[name] = bytes;
                Put(map, i * 0x10, name, names.Length - 1 - i);
                var component = entity + 0x7000 + (i * 0x100);
                Put(components, i * 8, component);
                memory.Pointers[component + 8] = entity;
            }

            memory.Blocks[entries] = map;
            memory.Blocks[vector] = components;
            return entity;
        }
    }
}
