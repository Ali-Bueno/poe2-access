namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Poe2Accessibility.Tests.DatColumnSearchTests;
    using static Check;

    /// <summary>The shared column finders of the extractors (buffs, ground effects, animation links), on small synthetic tables.</summary>
    public static class ExtractorColumnTests
    {
        private const int BuffRowSize = 0x70;
        private const int AnimationRowSize = 0x6A;
        private const int NoKey = -1;
        private const int DescriptionOffset = 0x08;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(BuffColumnsAreFoundWhereMeasured), BuffColumnsAreFoundWhereMeasured),
            (nameof(BuffColumnsFollowAShift), BuffColumnsFollowAShift),
            (nameof(BuffTypeIsToldByTheFrozenRow), BuffTypeIsToldByTheFrozenRow),
            (nameof(BuffColumnsFailWithoutNames), BuffColumnsFailWithoutNames),
            (nameof(HazardBuffKeyIsFoundAndFollowsAShift), HazardBuffKeyIsFoundAndFollowsAShift),
            (nameof(AnimationLinksAreFoundWhereMeasured), AnimationLinksAreFoundWhereMeasured),
            (nameof(AnimationLinksFollowAShift), AnimationLinksFollowAShift),
        };

        /// <summary>Buffs as the game lays them out (Id, Description, Name, type byte); <paramref name="shift"/> moves every column after the Id.</summary>
        private static DatTable Buffs(int shift, byte frozenType = 2)
        {
            var builder = new DatTableBuilder(BuffRowSize + shift);
            builder.Row(w => w.Text(0, "frozen").Text(DescriptionOffset + shift, "Cannot move or take actions").Text(BuffColumns.NameOffset + shift, "Frozen").Byte(BuffColumns.TypeOffset + shift, frozenType));
            builder.Row(w => w.Text(0, "ignited").Text(DescriptionOffset + shift, "Debuff inflicts [Fire] damage over time").Text(BuffColumns.NameOffset + shift, "Ignited").Byte(BuffColumns.TypeOffset + shift, 2));
            builder.Row(w => w.Text(0, "flask_effect_life").Text(DescriptionOffset + shift, string.Empty).Text(BuffColumns.NameOffset + shift, "Healing").Byte(BuffColumns.TypeOffset + shift, 4));
            builder.Row(w => w.Text(0, "curse").Text(DescriptionOffset + shift, "Cursed").Text(BuffColumns.NameOffset + shift, "Enfeeble").Byte(BuffColumns.TypeOffset + shift, 5));
            return builder.Build();
        }

        private static void BuffColumnsAreFoundWhereMeasured()
        {
            var columns = BuffColumns.FindWithDescription(Buffs(0), "test") ?? throw new Exception("not found");
            Expect(BuffColumns.NameOffset, columns.Name, "name");
            Expect(BuffColumns.TypeOffset, columns.Type, "type");
            Expect(DescriptionOffset, columns.Description, "description");
        }

        private static void BuffColumnsFollowAShift()
        {
            const int shift = 0x10;
            var columns = BuffColumns.FindWithDescription(Buffs(shift), "test") ?? throw new Exception("not found");
            Expect(BuffColumns.NameOffset + shift, columns.Name, "name");
            Expect(BuffColumns.TypeOffset + shift, columns.Type, "type");
            Expect(DescriptionOffset + shift, columns.Description, "description");
        }

        /// <summary>A byte that is only plausible is not enough: the Frozen ailment's row says which byte is the type.</summary>
        private static void BuffTypeIsToldByTheFrozenRow()
        {
            Expect(BuffColumns.TypeOffset, BuffColumns.Find(Buffs(0, frozenType: 2), "test")?.Type, "Frozen is a debuff");
            Expect(true, BuffColumns.Find(Buffs(0, frozenType: 5), "test")?.Type != BuffColumns.TypeOffset, "the byte saying 5 for Frozen is not the type");
        }

        private static void BuffColumnsFailWithoutNames()
        {
            var table = new DatTableBuilder(BuffRowSize).Row(w => w.Text(0, "frozen")).Row(w => w.Text(0, "ignited")).Build();
            Expect(true, BuffColumns.Find(table, "test") == null, "no name column");
        }

        private static void HazardBuffKeyIsFoundAndFollowsAShift()
        {
            var buffs = new DatTableBuilder(0x10).Row(w => w.Text(0, "frozen")).Row(w => w.Text(0, "ignited_ground")).Row(w => w.Text(0, "chilled_ground")).Build();
            DatTable Types(int at) => new DatTableBuilder(0x40).Row(w => w.Text(0, "IgnitedGround").WideKey(at, 1)).Row(w => w.Text(0, "ChilledGround").WideKey(at, 2)).Row(w => w.Text(0, "Unused").WideKey(at, NoKey)).Build();
            Expect(HazardColumns.TypeBuffOffset, HazardColumns.Find(Types(HazardColumns.TypeBuffOffset), buffs), "measured");
            Expect(HazardColumns.TypeBuffOffset + 8, HazardColumns.Find(Types(HazardColumns.TypeBuffOffset + 8), buffs), "moved");
            Expect(-1, HazardColumns.Find(Types(HazardColumns.TypeBuffOffset), new DatTableBuilder(0x10).Row(w => w.Text(0, "other")).Build()), "buffs no key resolves into");
        }

        /// <summary>Animations: SpellProjectileFire falls back to SpellProjectile; Charge plays ChargeStart before, ChargeEnd after and lists both.</summary>
        private static DatTable Animations(int shift)
        {
            var builder = new DatTableBuilder(AnimationRowSize + shift);
            var names = new[] { "SpellProjectileFire", "SpellProjectile", "Charge", "ChargeStart", "ChargeEnd" };
            foreach (var (name, row) in names.Select((name, row) => (name, row)))
            {
                builder.Row(w =>
                {
                    w.Text(0, name)
                        .Key(AnimationColumns.FallbackOffset + shift, row == 0 ? 1 : NoKey)
                        .Key(AnimationColumns.BeforeOffset + shift, row == 2 ? 3 : NoKey)
                        .Key(AnimationColumns.AfterOffset + shift, row == 2 ? 4 : NoKey);
                    if (row == 2)
                    {
                        w.KeyList(AnimationColumns.OthersOffset + shift, 3, 4);
                    }
                });
            }

            return builder.Build();
        }

        private static void AnimationLinksAreFoundWhereMeasured()
        {
            var columns = AnimationColumns.Find(Animations(0)) ?? throw new Exception("not found");
            Expect((AnimationColumns.FallbackOffset, AnimationColumns.BeforeOffset, AnimationColumns.AfterOffset, AnimationColumns.OthersOffset), (columns.Fallback, columns.Before, columns.After, columns.Others), "columns");
        }

        private static void AnimationLinksFollowAShift()
        {
            const int shift = 0x10;
            var columns = AnimationColumns.Find(Animations(shift)) ?? throw new Exception("not found");
            Expect((AnimationColumns.FallbackOffset + shift, AnimationColumns.BeforeOffset + shift, AnimationColumns.AfterOffset + shift, AnimationColumns.OthersOffset + shift), (columns.Fallback, columns.Before, columns.After, columns.Others), "columns");
        }
    }
}
