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
        };

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
