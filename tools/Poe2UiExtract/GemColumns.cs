using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns the `gems` catalog is read through (SkillGems, GemEffects, GemTags, BaseItemTypes, GrantedEffects), each
///     found near where it was measured (dat-kinds, 0.5.x, 2026-11-10) by its kind and by what the rows of known gems
///     hold there: Ice Nova is an intelligence gem tagged cold whose effect grants IceNovaPlayer. Pure.
/// </summary>
internal sealed class GemColumns
{
    internal const string Step = "gems";

    // Measured offsets, where each search starts.
    internal const int GemBaseOffset = 0x0, GemStrengthOffset = 0x10, GemDexterityOffset = 0x14, GemIntelligenceOffset = 0x18, GemEffectsOffset = 0x8C;
    internal const int EffectSkillOffset = 0x10, EffectSupportTextOffset = 0x20, EffectTagsOffset = 0x30;
    internal const int TagNameOffset = 0x8;
    internal const int BaseKindOffset = 0x28;
    internal const int SkillActiveOffset = 0x4F;
    internal const int MaxShare = 100;

    // Rows whose values are known: a gem of each attribute, one support, and what Ice Nova is made of.
    internal const string KnownGem = "Metadata/Items/Gems/SkillGemIceNova";
    internal const string KnownGemKind = "Metadata/Items/Gems/ActiveSkillGem";
    internal const string KnownStrengthGem = "Metadata/Items/Gems/SkillGemLeapSlam";
    internal const string KnownDexterityGem = "Metadata/Items/Gems/SkillGemLightningArrow";
    internal const string KnownEffect = "IceNova";
    internal const string KnownSkill = "IceNovaPlayer";
    internal const string KnownActiveSkill = "ice_nova";
    internal const string KnownTag = "cold";
    internal const string KnownTagName = "Cold";
    internal const string KnownSupportEffect = "AddedFireDamageSupport";
    internal const string KnownSupportWord = "Fire";

    /// <summary>The tables the columns are in.</summary>
    public sealed record Tables(DatTable Gems, DatTable Effects, DatTable Tags, DatTable Items, DatTable Skills, DatTable ActiveSkills);

    public int GemBase, Strength, Dexterity, Intelligence, GemEffects;
    public int EffectSkill, SupportText, EffectTags, TagName, ItemName, ItemKind, SkillActive, SkillName;

    /// <summary>The columns, or null (said) when one was not found; the two name columns are found by their own anchors.</summary>
    public static GemColumns? Find(Tables t, int itemName, int skillName)
    {
        var search = new DatColumnSearch();
        int Item(string id) => DatAnchors.Row(t.Items, id, "SkillGems' columns");
        var (knownItem, strengthItem, dexterityItem) = (Item(KnownGem), Item(KnownStrengthGem), Item(KnownDexterityGem));
        var gemBase = search.Find(t.Gems, "SkillGems.BaseItemType", GemBaseOffset, offset => DatColumnSearch.IsKey(t.Gems, offset, t.Items.RowCount) && (knownItem < 0 || GemOf(t.Gems, offset, knownItem) >= 0));
        if (gemBase < 0)
        {
            search.Report(Step);
            return null;
        }

        var (gem, strengthGem, dexterityGem) = (GemOf(t.Gems, gemBase, knownItem), GemOf(t.Gems, gemBase, strengthItem), GemOf(t.Gems, gemBase, dexterityItem));
        int Share(string name, int measured, int after, params (int Row, int Value)[] known) =>
            search.Find(t.Gems, name, measured, offset => offset + sizeof(int) <= t.Gems.RowSize && Enumerable.Range(0, t.Gems.RowCount).All(row => t.Gems.Int32(row, offset) is >= 0 and <= MaxShare) &&
                                                         known.All(check => check.Row < 0 || t.Gems.Int32(check.Row, offset) == check.Value), after);
        var strength = Share("SkillGems.StrengthShare", GemStrengthOffset, gemBase, (gem, 0), (strengthGem, MaxShare), (dexterityGem, 0));
        var dexterity = Share("SkillGems.DexterityShare", GemDexterityOffset, strength, (gem, 0), (strengthGem, 0), (dexterityGem, MaxShare));
        var intelligence = Share("SkillGems.IntelligenceShare", GemIntelligenceOffset, dexterity, (gem, MaxShare), (strengthGem, 0), (dexterityGem, 0));

        var effect = DatAnchors.Row(t.Effects, KnownEffect, "GemEffects' columns");
        var skill = DatAnchors.Row(t.Skills, KnownSkill, "GemEffects' skill and GrantedEffects' active skill");
        var tag = DatAnchors.Row(t.Tags, KnownTag, "GemEffects' tags");
        var supportEffect = DatAnchors.Row(t.Effects, KnownSupportEffect, "GemEffects' support text");
        var activeSkill = DatAnchors.Row(t.ActiveSkills, KnownActiveSkill, "GrantedEffects' active skill");
        var gemEffects = search.KeyList(t.Gems, "SkillGems.GemEffects", GemEffectsOffset, t.Effects, known: (effect < 0 ? -1 : gem, keys => keys.Contains(effect)));
        var effectSkill = search.Key(t.Effects, "GemEffects.GrantedEffect", EffectSkillOffset, t.Skills, known: (skill < 0 ? -1 : effect, key => key == skill));
        var effectTags = search.KeyList(t.Effects, "GemEffects.GemTags", EffectTagsOffset, t.Tags, known: (tag < 0 ? -1 : effect, keys => keys.Contains(tag)));
        var supportText = search.Text(t.Effects, "GemEffects.SupportText", EffectSupportTextOffset, known: (supportEffect, text => text.Contains(KnownSupportWord, StringComparison.Ordinal)));
        var tagName = search.Text(t.Tags, "GemTags.Name", TagNameOffset, known: (tag, text => text.Contains(KnownTagName, StringComparison.Ordinal)));
        var itemKind = search.Text(t.Items, "BaseItemTypes.InheritsFrom", BaseKindOffset, text => text.StartsWith("Metadata/", StringComparison.Ordinal), known: (knownItem, text => text == KnownGemKind));
        var skillActive = search.Key(t.Skills, "GrantedEffects.ActiveSkill", SkillActiveOffset, t.ActiveSkills, known: (activeSkill < 0 ? -1 : skill, key => key == activeSkill));
        return !search.Report(Step) ? null : new GemColumns
        {
            GemBase = gemBase, Strength = strength, Dexterity = dexterity, Intelligence = intelligence, GemEffects = gemEffects, EffectSkill = effectSkill, SupportText = supportText,
            EffectTags = effectTags, TagName = tagName, ItemName = itemName, ItemKind = itemKind, SkillActive = skillActive, SkillName = skillName,
        };
    }

    /// <summary>The SkillGems row whose key at <paramref name="offset"/> is the BaseItemTypes row <paramref name="item"/>, or -1.</summary>
    private static int GemOf(DatTable gems, int offset, int item)
    {
        using var probe = DatTable.Probe();
        return item < 0 ? -1 : Enumerable.Range(0, gems.RowCount).FirstOrDefault(row => gems.Key(row, offset) == item, -1);
    }
}
