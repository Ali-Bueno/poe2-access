namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The gem behind whichever Id or name a reader has in hand (<see cref="GemIndex"/>), for the Build Planner.</summary>
    public static class GemIndexTests
    {
        private const string Gems =
            "{\"gems\":{" +
            "\"Metadata/Items/Gem/SkillGemIceNova\":{\"kind\":\"ActiveSkillGem\",\"effects\":[\"IceNova\"],\"skills\":[\"IceNovaPlayer\"],\"tags\":[\"intelligence\",\"grants_active_skill\",\"spell\",\"cold\"],\"str\":0,\"dex\":0,\"int\":100}," +
            "\"Metadata/Items/Gems/SkillGemHeraldOfAsh\":{\"kind\":\"ActiveSkillGem\",\"effects\":[\"HeraldOfAsh\"],\"skills\":[\"HeraldOfAshPlayer\"],\"tags\":[\"buff\",\"persistent\",\"fire\"],\"str\":100,\"dex\":0,\"int\":0}," +
            "\"Metadata/Items/Gems/UniqueSkillGemHeraldOfAsh\":{\"kind\":\"ActiveSkillGem\",\"effects\":[\"UniqueHeraldOfAsh\"],\"skills\":[\"HeraldOfAshPlayer\"],\"tags\":[\"buff\",\"persistent\",\"fire\"],\"str\":100,\"dex\":0,\"int\":0}," +
            "\"Metadata/items/Gems/SupportGemRageTwo\":{\"kind\":\"SupportSkillGem\",\"effects\":[\"RageSupportTwo\"],\"skills\":[\"SupportRagePlayerTwo\"],\"tags\":[\"support\",\"attack\"],\"str\":50,\"dex\":50,\"int\":0}," +
            "\"Metadata/Items/Gems/SkillGemCastOnShock\":{\"kind\":\"MetaSkillGem\",\"effects\":[\"CastOnShock\"],\"skills\":[\"MetaCastOnShockPlayer\"],\"tags\":[\"meta\",\"persistent\"],\"str\":0,\"dex\":0,\"int\":0}," +
            "\"Metadata/Items/Gems/SkillGemBlinkA\":{\"kind\":\"ActiveSkillGem\",\"effects\":[\"BlinkA\"],\"skills\":[\"BlinkAPlayer\"],\"tags\":[],\"str\":0,\"dex\":100,\"int\":0}," +
            "\"Metadata/Items/Gems/SupportGemBlinkB\":{\"kind\":\"SupportSkillGem\",\"effects\":[\"BlinkB\"],\"skills\":[],\"tags\":[],\"str\":0,\"dex\":100,\"int\":0}," +
            "\"Metadata/Items/Gems/NewKindGem\":{\"kind\":\"SpiritSkillGem\",\"effects\":[],\"skills\":[],\"tags\":[],\"str\":0,\"dex\":0,\"int\":0}}," +
            "\"names\":{\"en\":{\"Metadata/Items/Gem/SkillGemIceNova\":\"Ice Nova\",\"Metadata/Items/Gems/SkillGemHeraldOfAsh\":\"Herald of Ash\",\"Metadata/Items/Gems/UniqueSkillGemHeraldOfAsh\":\"Herald of Ash\"," +
            "\"Metadata/items/Gems/SupportGemRageTwo\":\"Rage II\",\"Metadata/Items/Gems/SkillGemBlinkA\":\"Blink\",\"Metadata/Items/Gems/SupportGemBlinkB\":\"Blink\"}," +
            "\"th\":{\"Metadata/Items/Gem/SkillGemIceNova\":\"<size:37>{Nova}\"},\"es\":{\"Metadata/Items/Gem/SkillGemIceNova\":\"Nova de hielo\"}}," +
            "\"tags\":{\"en\":{\"cold\":\"Cold\",\"spell\":\"Spell\",\"support\":\"Support\",\"attack\":\"Attack\"},\"es\":{\"cold\":\"Hielo\",\"spell\":\"Hechizo\"}}," +
            "\"support\":{\"en\":{\"Metadata/items/Gems/SupportGemRageTwo\":\"Supports Melee Attacks.\"},\"es\":{\"Metadata/items/Gems/SupportGemRageTwo\":\"Asiste ataques cuerpo a cuerpo.\"}}," +
            "\"skills\":{\"en\":{\"IceNovaPlayer\":\"Ice Nova\"}}}";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AGemIsFoundByAnyIdInHand), AGemIsFoundByAnyIdInHand),
            (nameof(AGemIsFoundByItsNameInTheGamesLanguage), AGemIsFoundByItsNameInTheGamesLanguage),
            (nameof(AGemSaysWhatItIs), AGemSaysWhatItIs),
            (nameof(TheShippedGemsAreAllFound), TheShippedGemsAreAllFound),
        };

        private static void AGemIsFoundByAnyIdInHand()
        {
            var index = new GemIndex(GemCatalog.Parse(Gems));
            Equal(8, index.Count, "every gem");
            Equal("SkillGemIceNova", index.ByBaseItem("Metadata/Items/Gem/SkillGemIceNova")?.Key, "the base item Id, whole");
            Equal("Metadata/Items/Gem/SkillGemIceNova", index.ByBaseItem("SkillGemIceNova")?.Id, "its last part, as a build plan keeps it");
            Equal("SupportGemRageTwo", index.ByBaseItem("Metadata/Items/Gems/SupportGemRageTwo")?.Key, "the path's case is the game's to mix (items, Items)");
            Equal("SupportGemRageTwo", index.ByBaseItem("Metadata/Items/Gem/SupportGemRageTwo")?.Key, "and Gem or Gems: the last part decides");
            Equal(null, index.ByBaseItem("Metadata/Items/Gems/SupportGemFrostNexus"), "a gem newer than the catalog");
            Equal("SkillGemIceNova", index.ByGemEffect("IceNova")?.Key, "a GemEffects Id");
            Equal("SkillGemIceNova", index.BySkill("IceNovaPlayer")?.Key, "a GrantedEffects Id");
            Equal("SkillGemHeraldOfAsh", index.BySkill("HeraldOfAshPlayer")?.Key, "a skill two gems grant: the first in Id order, not the unique's copy");
            Equal(null, index.BySkill("IceNova"), "a GemEffects Id is no GrantedEffects Id");
            Equal("SkillGemIceNova", index.Find("IceNovaPlayer", "es")?.Key, "whatever is in hand: a skill");
            Equal("SkillGemIceNova", index.Find("IceNova", "es")?.Key, "an effect");
            Equal("SkillGemIceNova", index.Find("nova de hielo", "es")?.Key, "a name");
            Equal(null, index.Find("Nothing Such", "es"), "or nothing");
            Equal(0, new GemIndex(GemCatalog.Parse("{}")).Count, "no catalog: nothing is found, the game's texts are all there is");
        }

        private static void AGemIsFoundByItsNameInTheGamesLanguage()
        {
            var index = new GemIndex(GemCatalog.Parse(Gems));
            Equal("SkillGemIceNova", index.ByName("Nova de hielo", "es").Single().Key, "the language's name");
            Equal("SkillGemIceNova", index.ByName(" NOVA DE HIELO ", "es").Single().Key, "case and outer spaces ignored");
            Equal("SkillGemIceNova", index.ByName("Ice Nova", "es").Single().Key, "English when the language has no such name");
            Equal("SkillGemIceNova", index.ByName("Nova", "th").Single().Key, "the size tag Thai names carry is no part of the name");
            Equal("Nova", index.ByBaseItem("SkillGemIceNova")!.Name("th"), "nor is it said");
            Equal("Spectre: {0}", GemInfo.PlainName(" Spectre: {0} "), "a place the game fills in is no tag: the name is a pattern, and is kept whole");
            Equal(0, index.ByName("Nova de fuego", "es").Count, "no such gem");
            Equal("SkillGemHeraldOfAsh,UniqueSkillGemHeraldOfAsh", string.Join(",", index.ByName("Herald of Ash", "en").Select(gem => gem.Key)), "a name two gems carry gives both");
            Equal("SkillGemHeraldOfAsh", index.Find("Herald of Ash", "en")?.Key, "alike in kind and attribute: either says what the name is");
            Equal(2, index.ByName("Blink", "en").Count, "a skill and a support under one name");
            Equal(null, index.Find("Blink", "en"), "are not chosen between");
        }

        private static void AGemSaysWhatItIs()
        {
            var index = new GemIndex(GemCatalog.Parse(Gems));
            var nova = index.ByBaseItem("SkillGemIceNova")!;
            Equal(GemKind.Skill, nova.Kind, "a skill gem");
            Equal(GemAttribute.Intelligence, nova.Attribute, "all intelligence");
            Equal((0, 0, 100), nova.Shares, "the shares themselves");
            Equal("Nova de hielo", nova.Name("es"), "its name");
            Equal("Ice Nova", nova.Name("fr"), "English when the language has none");
            Equal("Hechizo, Hielo", string.Join(", ", nova.Tags("es")), "the tags the game names, in the gem's order");
            Equal("intelligence,grants_active_skill,spell,cold", string.Join(",", nova.TagIds), "every tag Id, the unnamed too");
            Equal(false, nova.IsPersistent, "no Persistent tag");
            Equal(string.Empty, nova.SupportText("es"), "no support text on a skill");
            Equal("IceNova", nova.EffectIds.Single(), "its GemEffects");
            Equal("IceNovaPlayer", nova.SkillIds.Single(), "its GrantedEffects");
            var rage = index.ByBaseItem("SupportGemRageTwo")!;
            Equal(GemKind.Support, rage.Kind, "a support");
            Equal(GemAttribute.Mixed, rage.Attribute, "half strength, half dexterity");
            Equal("Asiste ataques cuerpo a cuerpo.", rage.SupportText("es"), "what it does");
            Equal("Supports Melee Attacks.", rage.SupportText("de"), "in English when the language has none");
            Equal("Support, Attack", string.Join(", ", rage.Tags("es")), "tags in English when the language names none");
            var meta = index.ByBaseItem("SkillGemCastOnShock")!;
            Equal(GemKind.Meta, meta.Kind, "a meta gem");
            Equal(GemAttribute.None, meta.Attribute, "no attribute share");
            Equal(true, meta.IsPersistent, "the game's Persistent tag");
            Equal(string.Empty, meta.Name("es"), "a gem the files do not name has no name");
            Equal(GemKind.Other, index.ByBaseItem("NewKindGem")!.Kind, "a kind a patch added is not guessed");
        }

        /// <summary>The shipped file: every gem reachable by each of its Ids, and by its English name.</summary>
        private static void TheShippedGemsAreAllFound()
        {
            if (FileCatalogTests.Shipped("gems.json") is not { } file)
            {
                return; // away from the repository
            }

            var catalog = GemCatalog.Parse(File.ReadAllText(file));
            var index = new GemIndex(catalog);
            Equal(catalog.Gems.Count, index.Count, "no two gems share a base item Id, whatever its case");
            Equal(0, catalog.Gems.Count(gem => index.ByBaseItem(gem.Id)?.Id != gem.Id), "each by its Id");
            Equal(0, catalog.Gems.Count(gem => index.ByBaseItem(gem.Id[(gem.Id.LastIndexOf('/') + 1)..])?.Id != gem.Id), "each by its Id's last part: no two gems share one");
            Equal(0, catalog.Gems.Count(gem => gem.Effects.Any(effect => index.ByGemEffect(effect) == null)), "each effect leads to a gem");
            Equal(0, catalog.Gems.Count(gem => gem.Skills.Any(skill => index.BySkill(skill) == null)), "each skill leads to a gem");
            // 0.5.x: four skills are granted by more than one gem (a placeholder skill by dozens of unused gems, a unique's copy).
            var shared = catalog.Gems.SelectMany(gem => gem.Skills.Distinct()).GroupBy(skill => skill).Count(gems => gems.Count() > 1);
            Equal(true, shared * 50 <= catalog.Gems.Count, $"few skills are granted by two gems ({shared}): a skill's Id names its gem");
            Equal(0, catalog.Gems.Count(gem => index.ByName(index.ByBaseItem(gem.Id)!.Name("en"), "en").All(found => found.Id != gem.Id)), "each by its English name");
            Equal(0, catalog.Gems.Count(gem => index.ByBaseItem(gem.Id)!.Kind == GemKind.Other), "each of a known kind");
            foreach (var language in Localization.Languages)
            {
                Equal(0, catalog.Gems.Count(gem => index.ByBaseItem(gem.Id)!.Name(language) is var name && (name.Contains('<') || name.Contains('{') && !name.Contains("{0}"))), $"no markup left in a name in {language}");
            }
        }
    }
}
