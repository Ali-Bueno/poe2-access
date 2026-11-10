namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The catalogs of static game data read from the game's files (docs/research/file-catalogs.md): the passive tree and the gems.</summary>
    public static class FileCatalogTests
    {
        private const string Tree =
            "{\"nodes\":{\"10\":\"blind17|\",\"11\":\"area25|n\",\"12\":\"keystone3|k\",\"13\":\"socket9|nj\",\"14\":\"mastery2|i\",\"15\":\"six704|\",\"x\":\"bad|\",\"16\":\"noflags\"}," +
            "\"links\":{\"10\":[11,15],\"11\":[10,11,12],\"12\":[11],\"13\":[],\"15\":[10]}," +
            "\"starts\":{\"15\":[\"Metadata/Characters/DexInt/DexIntFour\",\"Metadata/Characters/DexInt/DexIntFourb\"]}," +
            "\"classes\":{\"en\":{\"Metadata/Characters/DexInt/DexIntFour\":\"Shadow\",\"Metadata/Characters/DexInt/DexIntFourb\":\"Monk\"},\"es\":{\"Metadata/Characters/DexInt/DexIntFourb\":\"Monje\"}}," +
            "\"names\":{\"en\":{\"blind17\":\"Damage vs Blinded\",\"socket9\":\"[Jewel|Jewel] Socket\"},\"es\":{\"socket9\":\"Engarce de [Jewel|joya]\"}}}";

        private const string Gems =
            "{\"gems\":{\"Metadata/Items/Gem/SkillGemIceNova\":{\"kind\":\"ActiveSkillGem\",\"effects\":[\"IceNova\"],\"skills\":[\"IceNovaPlayer\"],\"tags\":[\"intelligence\",\"spell\",\"cold\"],\"str\":0,\"dex\":0,\"int\":100}}," +
            "\"names\":{\"en\":{\"Metadata/Items/Gem/SkillGemIceNova\":\"Ice Nova\"},\"es\":{\"Metadata/Items/Gem/SkillGemIceNova\":\"Nova de hielo\"}}," +
            "\"tags\":{\"en\":{\"cold\":\"Cold\",\"spell\":\"Spell\"},\"es\":{\"cold\":\"Hielo\"}}," +
            "\"support\":{\"en\":{}}," +
            "\"skills\":{\"en\":{\"IceNovaPlayer\":\"Ice Nova\",\"ParryPlayer\":\"Parry\"},\"es\":{\"IceNovaPlayer\":\" Nova de hielo \"}}}";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APassiveNodeIsWhatTheFilesSay), APassiveNodeIsWhatTheFilesSay),
            (nameof(APassiveNameIsKnownInEveryLanguage), APassiveNameIsKnownInEveryLanguage),
            (nameof(AMissingOrBrokenCatalogLeavesTheLastOne), AMissingOrBrokenCatalogLeavesTheLastOne),
            (nameof(TheShippedPassiveTreeHoldsTogether), TheShippedPassiveTreeHoldsTogether),
            (nameof(ASkillIsNamedByItsGrantedEffect), ASkillIsNamedByItsGrantedEffect),
            (nameof(TheShippedGemsHoldTogether), TheShippedGemsHoldTogether),
            (nameof(TheTwoSourcesAreComparedOnce), TheTwoSourcesAreComparedOnce),
            (nameof(TheAllocatedListIsJudgedByEveryIdOfTheFiles), TheAllocatedListIsJudgedByEveryIdOfTheFiles),
        };

        /// <summary>The repository's shipped catalog file, found from the test's folder upwards; null away from the repository.</summary>
        internal static string? Shipped(string name)
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = Path.Combine(folder.FullName, "Poe2Accessibility", "catalog", name);
                if (File.Exists(file))
                {
                    return file;
                }
            }

            return null;
        }

        private static void APassiveNodeIsWhatTheFilesSay()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            Equal(6, tree.Count, "a graph id that is no number and a node without flags are left out");
            Equal(PassiveClass.Small, tree.ById("blind17")!.Class, "no flag");
            Equal(PassiveClass.Notable, tree.ByGraphId(11)!.Class, "n");
            Equal(PassiveClass.Keystone, tree.ByGraphId(12)!.Class, "k");
            Equal(PassiveClass.Notable, tree.ByGraphId(13)!.Class, "a notable that is a socket too reads as the row does: notable first");
            Equal(true, tree.ByGraphId(13)!.JewelSocket, "and keeps its socket flag");
            Equal(PassiveClass.Other, tree.ByGraphId(14)!.Class, "a mastery icon");
            Equal(true, tree.ByGraphId(14)!.IconOnly, "i");
            Equal(PassiveClass.Other, tree.ByGraphId(15)!.Class, "a class start");
            Equal("10,12", string.Join(",", tree.ByGraphId(11)!.Links), "links without the node itself");
            Equal(0, tree.ByGraphId(14)!.Links.Count, "a node the file gives no links");
            Equal(15, tree.StartOf("metadata/characters/dexint/dexintfourb")!.GraphId, "the player's path, whatever its case");
            Equal(null, tree.StartOf("Metadata/Characters/Str/StrFour"), "a class the files give no start");
            Equal(null, tree.ById("blind18"), "a node newer than the catalog: its row is read");
            Equal(true, tree.GraphIds.SetEquals(new[] { 10, 11, 12, 13, 14, 15 }), "every graph id, for the allocated list");
            Equal(0, PassiveTreeCatalog.Parse("{}").Count, "an empty file is an empty catalog");
        }

        private static void APassiveNameIsKnownInEveryLanguage()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            Equal("Engarce de [Jewel|joya]", tree.Name("socket9", "es"), "the language's name, markup as the game holds it");
            Equal("Damage vs Blinded", tree.Name("blind17", "es"), "English when the language has none");
            Equal(null, tree.Name("area25", "es"), "a node the files do not name");
            Equal("Monje", tree.ClassName("Metadata/Characters/DexInt/DexIntFourb", "es"), "a class");
            Equal("Shadow", tree.ClassName("metadata/characters/dexint/dexintfour", "es"), "English, and the path's case ignored");
            Equal(true, tree.IsNameOf("socket9", "[Jewel|Jewel] Socket"), "the game in English, the mod in Spanish: still the node's name");
            Equal(true, tree.IsNameOf("socket9", " Engarce de [Jewel|joya] "), "outer spaces ignored");
            Equal(false, tree.IsNameOf("socket9", "Damage vs Blinded"), "another node's name");
            Equal(true, tree.IsNameOf("area25", string.Empty), "a node with no name reads empty");
            Equal(false, tree.IsNameOf("area25", "Area"), "and nothing else");
            Equal(true, tree.IsNameOf("socket9", "Engarce de joya", name => name.Replace("[Jewel|joya]", "joya")), "as the screen shows it");
        }

        private static void AMissingOrBrokenCatalogLeavesTheLastOne()
        {
            var folder = Path.Join(Path.GetTempPath(), "poe2access-tests");
            Directory.CreateDirectory(folder);
            var log = new PluginLog(folder, false);
            var good = Path.Join(folder, "passive-tree-good.json");
            var broken = Path.Join(folder, "passive-tree-broken.json");
            File.WriteAllText(good, Tree);
            File.WriteAllText(broken, "{\"nodes\": [1, 2");
            PassiveTreeCatalog.Load(log, good);
            Equal(6, PassiveTreeCatalog.Shared.Count, "loaded");
            PassiveTreeCatalog.Load(log, Path.Join(folder, "no-such-file.json"));
            Equal(6, PassiveTreeCatalog.Shared.Count, "a missing file throws nothing and changes nothing");
            PassiveTreeCatalog.Load(log, broken);
            Equal(6, PassiveTreeCatalog.Shared.Count, "a broken file throws nothing and changes nothing");
            GemCatalog.Load(log, Path.Join(folder, "no-such-file.json"));
            GemCatalog.Load(log, broken);
            Equal(null, GemCatalog.Parse("{}").SkillName("IceNovaPlayer", "en"), "an empty catalog names nothing: memory is read");
        }

        /// <summary>The shipped file as the plugin reads it: a graph that closes on itself, every start a named class, every name clean.</summary>
        private static void TheShippedPassiveTreeHoldsTogether()
        {
            if (Shipped("passive-tree.json") is not { } file)
            {
                return; // away from the repository
            }

            var tree = PassiveTreeCatalog.Parse(File.ReadAllText(file));
            Equal(true, tree.Count > 1000, $"a whole tree ({tree.Count} nodes)");
            var nodes = tree.GraphIds.Select(id => tree.ByGraphId(id)!).ToList();
            Equal(nodes.Count, nodes.Select(node => node.Id).Distinct().Count(), "one graph id per PassiveSkills Id");
            Equal(0, nodes.Count(node => node.Links.Any(other => tree.ByGraphId(other) is not { } linked || !linked.Links.Contains(node.GraphId))), "every link's end is a node that links back");
            Equal(0, nodes.Count(node => node.Links.Contains(node.GraphId)), "no node links to itself");
            var starts = nodes.Where(node => node.IsStart).ToList();
            Equal(true, starts.Count >= 6, $"the classes' starts ({starts.Count})");
            Equal(0, starts.SelectMany(node => node.StartClasses).Count(character => tree.StartOf(character) == null), "every class finds its start");
            foreach (var language in Localization.Languages)
            {
                Equal(0, starts.SelectMany(node => node.StartClasses).Count(character => string.IsNullOrWhiteSpace(tree.ClassName(character, language))), $"every class named in {language}");
                Equal(true, nodes.Count(node => tree.Name(node.Id, language) != null) * 10 >= nodes.Count * 9, $"nine nodes in ten named in {language}");
            }

            Equal(true, nodes.Any(node => node.Keystone) && nodes.Any(node => node.Notable) && nodes.Any(node => node.JewelSocket) && nodes.Any(node => node.IconOnly), "each kind of node is there");
        }

        private static void ASkillIsNamedByItsGrantedEffect()
        {
            var gems = GemCatalog.Parse(Gems);
            Equal(1, gems.Gems.Count, "one gem");
            Equal("ActiveSkillGem", gems.Gems[0].Kind, "its kind");
            Equal(100, gems.Gems[0].Intelligence, "its attribute share");
            Equal(" Nova de hielo ", gems.SkillName("IceNovaPlayer", "es"), "the skill, by GrantedEffects Id, as the file holds it");
            Equal("Parry", gems.SkillName("ParryPlayer", "es"), "English when the language has none");
            Equal(null, gems.SkillName("NewSkillPlayer", "es"), "a skill newer than the catalog: the game's name is the only one");
            Equal(true, gems.IsSkillName("IceNovaPlayer", "Ice Nova"), "the game in English, the mod in Spanish");
            Equal(true, gems.IsSkillName("IceNovaPlayer", "Nova de hielo"), "outer spaces ignored");
            Equal(false, gems.IsSkillName("IceNovaPlayer", "Parry"), "another skill's name");
            Equal("Hielo", gems.TagName("cold", "es"), "a tag");
            Equal(null, gems.TagName("intelligence", "es"), "a tag the game shows no name for");
            Equal("Nova de hielo", gems.GemName("Metadata/Items/Gem/SkillGemIceNova", "es"), "the gem");
        }

        private static void TheTwoSourcesAreComparedOnce()
        {
            var check = new CatalogCrossCheck(3);
            Equal(true, check.Wants("a"), "a node not compared yet");
            Equal(null, check.Note("a", null), "one node says nothing yet");
            Equal(false, check.Wants("a"), "each node once");
            Equal(null, check.Note("a", "its name"), "and a second look at it does not count");
            Equal(null, check.Note("b", "the row's name reads 'x'"), "two");
            var verdict = check.Note("c", null);
            Equal(true, verdict is { Compared: 3, Disagreeing: 1 }, "the verdict comes with the third node");
            Equal("b: the row's name reads 'x'", verdict!.Value.First, "and names the first that differed");
            Equal(false, verdict.Value.MostDisagree, "one in three is a renamed node, not a moved column");
            Equal(true, check.Done, "given once");
            Equal(false, check.Wants("d"), "nothing more is read for it");
            Equal(null, check.Note("d", "its name"), "nor counted");
            check.Reset();
            Equal(true, check.Wants("a"), "a new game process compares again");
            check.Note("a", "x");
            check.Note("b", "x");
            Equal(true, check.Note("c", null)!.Value.MostDisagree, "two in three: one source reads another column");
        }

        private static void TheAllocatedListIsJudgedByEveryIdOfTheFiles()
        {
            var files = PassiveTreeCatalog.Parse(Tree).GraphIds;
            var rows = new HashSet<int> { 10, 9000 }; // the rows read so far, one of them a class's own node (no node of the graph)
            var all = AllocatedListCheck.AllIds(files, rows);
            Equal(7, all.Count, "the files' six and the override's");
            Equal(AllocatedVerdict.Kept, AllocatedListCheck.Judge(new[] { 12, 13, 9000 }, all), "nodes whose rows were never read, and the node shown in another's place");
            Equal(AllocatedVerdict.Unjudged, AllocatedListCheck.Judge(new[] { 12, 13, 9000 }, rows), "the rows alone could not tell");
            Equal(true, AllocatedListCheck.Strong(new[] { 11, 12, 9000 }, all), "such a list may take the measured one's place");
            Equal(AllocatedVerdict.Broken, AllocatedListCheck.Judge(Enumerable.Range(20000, 12).ToList(), all), "a list of no node at all is another list");
        }

        /// <summary>
        ///     The shipped file as the plugin reads it. A gem's name and its skill's name are two columns of two tables
        ///     (BaseItemTypes, ActiveSkills) that mostly say the same: when they stop doing so, one of them is another column.
        /// </summary>
        private static void TheShippedGemsHoldTogether()
        {
            if (Shipped("gems.json") is not { } file)
            {
                return;
            }

            var gems = GemCatalog.Parse(File.ReadAllText(file));
            Equal(true, gems.Gems.Count > 500, $"the game's gems ({gems.Gems.Count})");
            Equal(0, gems.Gems.Count(gem => gem.Kind is not ("ActiveSkillGem" or "SupportSkillGem" or "MetaSkillGem")), "every gem of a kind the plugin knows");
            Equal(0, gems.Gems.Count(gem => gem.Strength < 0 || gem.Dexterity < 0 || gem.Intelligence < 0 || gem.Strength + gem.Dexterity + gem.Intelligence > 100), "attribute shares within 0 to 100");
            Equal(0, gems.Gems.Count(gem => string.IsNullOrWhiteSpace(gems.GemName(gem.Id, "en"))), "every gem named");
            var active = gems.Gems.Where(gem => gem.Kind == "ActiveSkillGem" && gem.Skills.Count > 0).ToList();
            foreach (var language in Localization.Languages)
            {
                var named = active.Where(gem => gems.SkillName(gem.Skills[0], language) != null).ToList();
                Equal(true, named.Count * 10 >= active.Count * 9, $"nine skill gems in ten have a skill name in {language}");
                // Thai gem names carry a size tag ("<size:37>{…}"), which the skill names do not.
                var same = named.Count(gem => gems.SkillName(gem.Skills[0], language) == BuildNotes.Plain(gems.GemName(gem.Id, language) ?? string.Empty));
                Equal(true, same * 3 >= named.Count * 2, $"two skill names in three are the gem's name in {language} ({same} of {named.Count})");
            }
        }
    }
}
