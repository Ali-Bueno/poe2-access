namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using static Check;
    using DatTableBuilder = DatColumnSearchTests.DatTableBuilder;

    /// <summary>The gem catalog's columns are found by what known gems hold (docs/research/file-catalogs.md).</summary>
    public static class GemColumnTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(GemColumnsAreFoundWhereMeasured), GemColumnsAreFoundWhereMeasured),
            (nameof(GemColumnsThatMovedAreFoundByTheKnownGems), GemColumnsThatMovedAreFoundByTheKnownGems),
            (nameof(AGemTableThatNoLongerSaysIceNovaIsColdFails), AGemTableThatNoLongerSaysIceNovaIsColdFails),
        };

        /// <summary>The six tables with Ice Nova, Leap Slam, Lightning Arrow and a fire support in them; the SkillGems columns after the key moved by <paramref name="shift"/>.</summary>
        private static GemColumns.Tables Tables(int shift = 0, string iceNovaTag = "cold")
        {
            var items = new DatTableBuilder(0x30);
            foreach (var (id, kind) in new[] { (GemColumns.KnownGem, GemColumns.KnownGemKind), (GemColumns.KnownStrengthGem, GemColumns.KnownGemKind), (GemColumns.KnownDexterityGem, GemColumns.KnownGemKind), ("Metadata/Items/Gems/SupportGemFire", "Metadata/Items/Gems/SupportSkillGem") })
            {
                items.Row(w => w.Text(0x0, id).Text(GemColumns.BaseKindOffset, kind));
            }

            var active = new DatTableBuilder(0x10);
            var skills = new DatTableBuilder(0x60);
            var effects = new DatTableBuilder(0x40);
            var tags = new DatTableBuilder(0x10);
            foreach (var (id, name) in new[] { ("fire", "[Fire]"), ("cold", "[Cold]"), ("spell", "[Spell]"), ("attack", "[Attack]") })
            {
                tags.Row(w => w.Text(0x0, id).Text(GemColumns.TagNameOffset, name));
            }

            var rows = new[] { ("ice_nova", GemColumns.KnownSkill, GemColumns.KnownEffect, iceNovaTag == "cold" ? 1L : 0L, string.Empty), ("leap_slam", "LeapSlamPlayer", "LeapSlam", 3L, string.Empty), ("lightning_arrow", "LightningArrowPlayer", "LightningArrow", 3L, string.Empty), ("support_fire", "SupportFirePlayer", GemColumns.KnownSupportEffect, 0L, "Supports any skill, adding [Fire] damage") };
            for (var row = 0; row < rows.Length; row++)
            {
                var (activeId, skillId, effectId, tag, text) = rows[row];
                var index = row;
                active.Row(w => w.Text(0x0, activeId));
                skills.Row(w => w.Text(0x0, skillId).WideKey(GemColumns.SkillActiveOffset, index));
                effects.Row(w => w.Text(0x0, effectId).WideKey(GemColumns.EffectSkillOffset, index).Text(GemColumns.EffectSupportTextOffset, text).KeyList(GemColumns.EffectTagsOffset, tag, 2));
            }

            var gems = new DatTableBuilder(0x9C + shift);
            foreach (var (item, str, dex, intelligence) in new[] { (0, 0, 0, 100), (1, 100, 0, 0), (2, 0, 100, 0), (3, 100, 0, 0) })
            {
                gems.Row(w => w.WideKey(GemColumns.GemBaseOffset, item).Int(GemColumns.GemStrengthOffset + shift, str).Int(GemColumns.GemDexterityOffset + shift, dex)
                    .Int(GemColumns.GemIntelligenceOffset + shift, intelligence).KeyList(GemColumns.GemEffectsOffset + shift, item));
            }

            return new GemColumns.Tables(gems.Build(), effects.Build(), tags.Build(), items.Build(), skills.Build(), active.Build());
        }

        private static void GemColumnsAreFoundWhereMeasured()
        {
            var c = GemColumns.Find(Tables(), 0x20, 0x8) ?? throw new Exception("not found");
            Expect((GemColumns.GemBaseOffset, GemColumns.GemStrengthOffset, GemColumns.GemDexterityOffset, GemColumns.GemIntelligenceOffset, GemColumns.GemEffectsOffset),
                (c.GemBase, c.Strength, c.Dexterity, c.Intelligence, c.GemEffects), "SkillGems");
            Expect((GemColumns.EffectSkillOffset, GemColumns.EffectSupportTextOffset, GemColumns.EffectTagsOffset), (c.EffectSkill, c.SupportText, c.EffectTags), "GemEffects");
            Expect((GemColumns.TagNameOffset, GemColumns.BaseKindOffset, GemColumns.SkillActiveOffset), (c.TagName, c.ItemKind, c.SkillActive), "the other tables");
        }

        private static void GemColumnsThatMovedAreFoundByTheKnownGems()
        {
            const int Shift = 8;
            var c = GemColumns.Find(Tables(Shift), 0x20, 0x8) ?? throw new Exception("not found after a shift");
            Expect((GemColumns.GemStrengthOffset + Shift, GemColumns.GemDexterityOffset + Shift, GemColumns.GemIntelligenceOffset + Shift, GemColumns.GemEffectsOffset + Shift),
                (c.Strength, c.Dexterity, c.Intelligence, c.GemEffects), "each attribute where its known gem holds 100, the effects where Ice Nova's is");
        }

        private static void AGemTableThatNoLongerSaysIceNovaIsColdFails()
        {
            Expect(true, GemColumns.Find(Tables(iceNovaTag: "fire"), 0x20, 0x8) == null, "Ice Nova's tags without cold: the tag list is another column, the step fails");
        }
    }
}
