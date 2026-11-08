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
        };

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
