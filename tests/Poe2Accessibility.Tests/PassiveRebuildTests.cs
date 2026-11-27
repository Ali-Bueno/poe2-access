namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;
    using DatTableBuilder = DatColumnSearchTests.DatTableBuilder;

    /// <summary>The passive tree facts taken from the game's data and the verdicts that say a tree is not the catalog's (docs/research/passive-tree.md).</summary>
    public static class PassiveRebuildTests
    {
        // 20: a start of an ascendancy not spelt as one, 21: a node of it, 22: a notable that is a socket, 23: a node that offers an attribute, 24: spelt as one but not offering.
        private const string Tree =
            "{\"nodes\":{\"20\":\"warden_gate|as\",\"21\":\"rune_pact|na\",\"22\":\"eye_socket|nj\",\"23\":\"any_stat|\",\"24\":\"strength9|\",\"25\":\"AscendancyOld1Start|\"}," +
            "\"ascendancies\":{\"20\":\"Warden1\",\"21\":\"Warden1\"}," +
            "\"offers\":{\"23\":\"GenericAttribute\"}," +
            "\"jewelSlots\":{\"22\":5,\"99\":7}}";

        private const string OldTree = "{\"nodes\":{\"20\":\"AscendancyWarden1Start|\",\"21\":\"rune_pact|n\",\"22\":\"strength9|\"}}";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AnAscendancyNodeIsWhatTheFilesFlag), AnAscendancyNodeIsWhatTheFilesFlag),
            (nameof(ACatalogWithoutTheFlagsKeepsTheSpelling), ACatalogWithoutTheFlagsKeepsTheSpelling),
            (nameof(ASocketIsAPropertyANotableCanHave), ASocketIsAPropertyANotableCanHave),
            (nameof(AnAttributeNodeIsOneThatOffersTheChoice), AnAttributeNodeIsOneThatOffersTheChoice),
            (nameof(ABuildsGoneNodesAreCountedByWhatTheCatalogKnows), ABuildsGoneNodesAreCountedByWhatTheCatalogKnows),
            (nameof(ABuildOfNoFileIsToldFromOneOfNoFolder), ABuildOfNoFileIsToldFromOneOfNoFolder),
            (nameof(AscendancyPointsTheStageDoesNotSpendNameTheNextStage), AscendancyPointsTheStageDoesNotSpendNameTheNextStage),
            (nameof(ATreeIsNotTheCatalogsWhenManyNodesDiffer), ATreeIsNotTheCatalogsWhenManyNodesDiffer),
            (nameof(OneOddRowDoesNotCostTheGraphIdColumn), OneOddRowDoesNotCostTheGraphIdColumn),
            (nameof(AClassListNeedsHalfTheClassesNamedOnceEach), AClassListNeedsHalfTheClassesNamedOnceEach),
            (nameof(ANewKindOfFlagIsFoundBesideTheKnownOnes), ANewKindOfFlagIsFoundBesideTheKnownOnes),
            (nameof(ASocketKnowsTheSlotItsJewelLiesAt), ASocketKnowsTheSlotItsJewelLiesAt),
            (nameof(ASocketSaysWhatItHolds), ASocketSaysWhatItHolds),
        };

        private static void ASocketKnowsTheSlotItsJewelLiesAt()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            Equal(5, tree.ByGraphId(22)!.JewelSlot, "the socket's PassiveJewelSlots row, the x of its jewel in PassiveJewels1");
            Equal(null, tree.ByGraphId(21)!.JewelSlot, "a node that is no socket has none");
            Equal(null, tree.ByGraphId(99), "a slot of a node the tree lacks names nothing");
            Equal(null, PassiveTreeCatalog.Parse(OldTree).ByGraphId(21)!.JewelSlot, "a catalog written before the slots: none, and it still reads");
        }

        private static void ASocketSaysWhatItHolds()
        {
            Equal("Jewel socket, allocated: Solaz de perdición, Emerald, rare",
                  Announcements.SocketHolding("Jewel socket, allocated", Announcements.Item("Solaz de perdición", GameReader.RemoteEnums.Rarity.Rare, 0, "Emerald")),
                  "a filled socket: the jewel's name, its base, its rarity");
            Equal($"Jewel socket, allocated, {Announcements.EmptyCell}", Announcements.SocketHolding("Jewel socket, allocated", string.Empty), "an empty socket says so");
            Equal("Jewel socket", Announcements.SocketHolding("Jewel socket", null), "unread, or no socket: the node alone");
            Equal("Emerald", Announcements.Item("Emerald", GameReader.RemoteEnums.Rarity.Normal, 0, "Emerald"), "a normal jewel's name is its base: said once");
        }

        private static void AnAscendancyNodeIsWhatTheFilesFlag()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            Equal(true, tree.IsAscendancy("warden_gate"), "flagged, spelt otherwise");
            Equal(true, tree.IsAscendancy("rune_pact"), "flagged");
            Equal(false, tree.IsAscendancy("AscendancyOld1Start"), "spelt as one, the files say it is not");
            Equal(true, tree.IsAscendancy("AscendancyNew1Small1"), "a node the files lack falls back on its spelling");
            Equal(true, tree.IsAscendancyStart("warden_gate"), "an ascendancy's start by the files' flag");
            Equal(false, tree.IsAscendancyStart("rune_pact"), "a node of the ascendancy is no start");
            Equal(false, tree.IsAscendancyStart("AscendancyOld1Start"), "the spelling of a start does not make one");
            Equal("Warden1", tree.ById("rune_pact")!.AscendancyId, "its ascendancy's Id");
        }

        private static void ACatalogWithoutTheFlagsKeepsTheSpelling()
        {
            var tree = PassiveTreeCatalog.Parse(OldTree);
            Equal(true, tree.IsAscendancy("AscendancyWarden1Start"), "spelt as an ascendancy's");
            Equal(true, tree.IsAscendancyStart("AscendancyWarden1Start"), "spelt as a start");
            Equal(true, tree.IsAscendancyStart("AscendancyWitch1Start__"), "with the underscores the files add");
            Equal(false, tree.IsAscendancyStart("AscendancyWarrior3Notable6_0"), "a notable");
            Equal(false, tree.IsAscendancy("rune_pact"), "an ordinary node");
            Equal(true, PassiveTreeCatalog.Parse("{}").IsAscendancy("AscendancyMonk1Small2"), "an empty catalog reads the spelling too");
        }

        private static void ASocketIsAPropertyANotableCanHave()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            var node = tree.ByGraphId(22)!;
            Equal(PassiveClass.Notable, node.Class, "its kind stays the notable's");
            Equal(true, node.JewelSocket, "and it is a socket too");
            Equal(PassiveClass.JewelSocket, PassiveTreeCatalog.ClassOf(false, false, true, false, false), "a socket alone");
            Equal(PassiveClass.Keystone, PassiveTreeCatalog.ClassOf(true, true, true, false, false), "a keystone before the rest");
            Equal($"{Announcements.PassiveNotable}, {Announcements.PassiveJewelSocket}", Announcements.PassiveKind(PassiveClass.Notable, true), "said as both");
            Equal(Announcements.PassiveNotable, Announcements.PassiveKind(PassiveClass.Notable, false), "a notable alone");
            Equal(Announcements.PassiveJewelSocket, Announcements.PassiveKind(PassiveClass.JewelSocket, true), "a socket is said once");
            Equal(string.Empty, Announcements.PassiveKind(PassiveClass.Other, true), "a start or an icon says no kind");
        }

        private static void AnAttributeNodeIsOneThatOffersTheChoice()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            Equal(true, tree.IsAttributeNode("any_stat"), "offers the choice, named otherwise");
            Equal(false, tree.IsAttributeNode("strength9"), "named as one, offers none");
            Equal(true, tree.IsAttributeNode("strength30"), "a node the files lack falls back on its spelling");
            Equal(true, PassiveTreeCatalog.Parse(OldTree).IsAttributeNode("strength9"), "without any offers: the spelling");
            Equal(false, PassiveTreeCatalog.Parse(OldTree).IsAttributeNode("rune_pact"), "an ordinary node");
        }

        private static void ABuildsGoneNodesAreCountedByWhatTheCatalogKnows()
        {
            var tree = PassiveTreeCatalog.Parse(Tree);
            var plan = BuildPlan.Parse("{\"passives\":[\"rune_pact\",\"any_stat\",{\"id\":\"any_stat\",\"weapon_set\":1},\"melee9\",\"melee10\",\"AscendancyWarden1Notable1\",\"AscendancyNew9Notable1\"]}")!;
            var (gone, total) = plan.Gone(id => tree.ById(id) != null, tree.OfUnknownAscendancy);
            Equal(3, gone, "melee9, melee10 and the Warden node the files no longer have");
            Equal(6, total, "a node named again counts once");
            Equal(true, tree.OfUnknownAscendancy("AscendancyNew9Notable1"), "an ascendancy the files do not know is theirs to lack");
            Equal(false, tree.OfUnknownAscendancy("AscendancyWarden1Notable1"), "one they know, with a node they lack");
            Equal(false, tree.OfUnknownAscendancy("melee9"), "an ordinary node");
            Equal(0, plan.Gone(_ => true, _ => false).Gone, "nothing gone when the tree has all");
            Equal(false, PassiveTreeCatalog.Parse(OldTree).OfUnknownAscendancy("AscendancyNew9Notable1"), "no ascendancy data: counted");
        }

        private static void ABuildOfNoFileIsToldFromOneOfNoFolder()
        {
            var folder = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "builds-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                var empty = new BuildPlanFolder(folder, System.IO.Path.Join(folder, "config.ini"));
                empty.Matching("yarkey", new[] { "a", "b" });
                Equal(false, empty.FilesButNoMatch("yarkey"), "no file at all: a build of the website, nothing to say");
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Old.build"), "{\"name\":\"Old\",\"passives\":[\"x1\",\"x2\",\"x3\"]}");
                var plans = new BuildPlanFolder(folder, System.IO.Path.Join(folder, "config.ini"));
                Equal(null, plans.Matching("yarkey", new[] { "a", "b" })?.Name, "no file holds the marks");
                Equal(true, plans.FilesButNoMatch("yarkey"), "but files exist: said");
                Equal(false, plans.FilesButNoMatch("kehgan"), "another character's is its own");
                Equal("Old", plans.Matching("yarkey", new[] { "x1", "x2" })?.Name, "a file holds them");
                Equal(false, plans.FilesButNoMatch("yarkey"), "and nothing is said");
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }

        private static void AscendancyPointsTheStageDoesNotSpendNameTheNextStage()
        {
            // kehgan's guide (2026-11-19): "23+ Unarmed" names two ascendancy passives, "Act 3-6" two more, the endgame more.
            var folder = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "builds-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                static string File(string link, string ascendancy, params string[] passives) =>
                    $"{{\"link\":\"{link}\",\"ascendancy\":\"{ascendancy}\",\"passives\":[{string.Join(",", passives.Select(id => $"\"{id}\""))}]}}";
                var guide = "https://example.org/monk";
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "23+ Unarmed.build"), File(guide, "Monk1", "AscendancyMonk1Start", "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "x1"));
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Act 3-6.build"), File(guide, "Monk1", "AscendancyMonk1Start", "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "AscendancyMonk1Small3", "AscendancyMonk1Notable8", "x1", "x2"));
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Endgame.build"), File(guide, "Monk1", "AscendancyMonk1Start", "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "AscendancyMonk1Small3", "AscendancyMonk1Notable8", "AscendancyMonk1Small5", "AscendancyMonk1Notable4"));
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Other guide.build"), File("https://example.org/other", "Monk1", "AscendancyMonk1Small6"));
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Other ascendancy.build"), File(guide, "Monk2", "AscendancyMonk2Notable1"));
                var plans = new BuildPlanFolder(folder, System.IO.Path.Join(folder, "config.ini"));
                var active = plans.Matching("kehgan", new[] { "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "x1" });
                Equal("https://example.org/monk", active?.Link, "the file's guide is read");
                var taken = new HashSet<string>(StringComparer.Ordinal) { "AscendancyMonk1Start", "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "x1" };
                Equal(0, active!.AscendancyLeft(taken), "the stage has nothing left for the points");
                Equal("Act 3-6", plans.NextAscendancyStage(active, taken), "the nearest stage of the same guide and ascendancy");
                taken.UnionWith(new[] { "AscendancyMonk1Small3", "AscendancyMonk1Notable8" });
                Equal("Endgame", plans.NextAscendancyStage(active, taken), "then the next one");
                taken.UnionWith(new[] { "AscendancyMonk1Small5", "AscendancyMonk1Notable4" });
                Equal(null, plans.NextAscendancyStage(active, taken), "none of the guide has more");

                // "Campaign End" names the third trial's pair first; the earlier stages say the order the points went in.
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Campaign End.build"), File(guide, "Monk1", "AscendancyMonk1Start", "AscendancyMonk1Small7", "AscendancyMonk1Notable5", "x1", "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "AscendancyMonk1Small3", "AscendancyMonk1Notable8", "x2"));
                var campaign = new BuildPlanFolder(folder, System.IO.Path.Join(folder, "config.ini")).Matching("kehgan", new[] { "AscendancyMonk1Small7", "AscendancyMonk1Notable5", "AscendancyMonk1Small4", "AscendancyMonk1Notable3", "AscendancyMonk1Small3", "AscendancyMonk1Notable8", "x1", "x2" });
                Equal("AscendancyMonk1Start AscendancyMonk1Small4 AscendancyMonk1Notable3 x1 AscendancyMonk1Small3 AscendancyMonk1Notable8 AscendancyMonk1Small7 AscendancyMonk1Notable5 x2",
                    string.Join(' ', campaign!.Passives.Select(passive => passive.Id)), "the first trial's pair, then the second's, then the stage's own; the tree's passives stay");
                Equal("0 1 2 3 4 5 6 7 8", string.Join(' ', campaign.Passives.Select(passive => passive.Order)), "each place keeps its step");
                Localization.Set("es", new());
                Equal("2 puntos de ascendencia, la build no tiene más pasivas de ascendencia, la build Act 3-6 tiene más", Announcements.BuildAscendancyUnspent(2, "Act 3-6"), "said");
            }
            finally
            {
                Localization.Set("en", new());
                System.IO.Directory.Delete(folder, true);
            }
        }

        private static void ATreeIsNotTheCatalogsWhenManyNodesDiffer()
        {
            Equal(false, PassiveTreeVerdicts.LinksDisagree(5000, 0), "all agree");
            Equal(false, PassiveTreeVerdicts.LinksDisagree(5000, 500), "a tenth is the limit");
            Equal(true, PassiveTreeVerdicts.LinksDisagree(5000, 501), "past a tenth the game's own links stand");
            Equal(false, PassiveTreeVerdicts.LinksDisagree(PassiveTreeVerdicts.MinNodesJudged - 1, PassiveTreeVerdicts.MinNodesJudged - 1), "too few compared to judge");
            Equal(true, PassiveTreeVerdicts.LinksDisagree(PassiveTreeVerdicts.MinNodesJudged, PassiveTreeVerdicts.MinNodesJudged), "all compared differ");
            Equal(false, PassiveTreeVerdicts.CatalogStale(5000, 0), "every node known");
            Equal(false, PassiveTreeVerdicts.CatalogStale(5000, 500), "a tenth unknown");
            Equal(true, PassiveTreeVerdicts.CatalogStale(5000, 501), "past a tenth the catalog is older");
            Equal(false, PassiveTreeVerdicts.CatalogStale(10, 10), "a few nodes judge nothing");
        }

        private const int GraphId = 0x30, Flag = 0x40, List = 0x50, RowSize = 0x60;

        private static DatTable GraphIds(int rows, Func<int, int> idOf)
        {
            var builder = new DatTableBuilder(RowSize);
            for (var row = 0; row < rows; row++)
            {
                var id = idOf(row);
                builder.Row(write => write.Text(0, $"node{id}").Int(GraphId, id));
            }

            return builder.Build();
        }

        private static void OneOddRowDoesNotCostTheGraphIdColumn()
        {
            Equal(true, PassiveColumnRules.IsUniqueUInt16(GraphIds(500, row => row + 1), GraphId), "every row unique");
            Equal(true, PassiveColumnRules.IsUniqueUInt16(GraphIds(500, row => row == 7 ? 0 : row + 1), GraphId), "one zero row of five hundred");
            Equal(true, PassiveColumnRules.IsUniqueUInt16(GraphIds(500, row => row == 9 ? 3 : row + 1), GraphId), "one repeated row");
            Equal(false, PassiveColumnRules.IsUniqueUInt16(GraphIds(500, row => row % 20 == 0 ? 5 : row + 1), GraphId), "a twentieth odd is another column");
            Equal(false, PassiveColumnRules.IsUniqueUInt16(GraphIds(500, row => row % 7), GraphId), "a column of few values");
            var odd = PassiveColumnRules.GraphIdExceptions(GraphIds(500, row => row == 7 ? 0 : row == 9 ? 3 : row + 1), GraphId);
            Equal("7:0 9:3", string.Join(" ", odd.Select(entry => $"{entry.Row}:{entry.Value}")), "the exceptions are listed");
        }

        private static DatTable ClassLists(int classes, params long[][] lists)
        {
            var builder = new DatTableBuilder(RowSize);
            foreach (var keys in lists)
            {
                builder.Row(write => write.KeyList(List, keys));
            }

            return builder.Build();
        }

        private static void AClassListNeedsHalfTheClassesNamedOnceEach()
        {
            var all = ClassLists(12, new long[] { 0, 1 }, new long[] { 2, 3 }, new long[] { 4, 5 }, new long[] { 6, 7 }, new long[] { 8, 9 }, new long[] { 10, 11 }, Array.Empty<long>());
            Equal(true, PassiveColumnRules.IsKeyListInto(all, List, 12), "twelve of twelve named once");
            var most = ClassLists(12, new long[] { 0, 1 }, new long[] { 2, 3 }, new long[] { 4, 5 }, new long[] { 6, 7 }, new long[] { 8, 9 }, Array.Empty<long>());
            Equal(true, PassiveColumnRules.IsKeyListInto(most, List, 12), "ten of twelve: a new class not placed yet");
            Equal("10, 11", string.Join(", ", PassiveColumnRules.UnnamedTargets(most, List, 12)), "the classes left out are listed");
            var half = ClassLists(12, new long[] { 0, 1 }, new long[] { 2, 3 }, new long[] { 4, 5 }, Array.Empty<long>());
            Equal(true, PassiveColumnRules.IsKeyListInto(half, List, 12), "half named is enough");
            var few = ClassLists(12, new long[] { 0, 1 }, new long[] { 2, 3 }, new long[] { 4 }, Array.Empty<long>());
            Equal(false, PassiveColumnRules.IsKeyListInto(few, List, 12), "under half named");
            var twice = ClassLists(12, new long[] { 0, 1 }, new long[] { 2, 3 }, new long[] { 4, 5 }, new long[] { 6, 7 }, new long[] { 8, 9 }, new long[] { 0 });
            Equal(false, PassiveColumnRules.IsKeyListInto(twice, List, 12), "a class named twice");
            var beyond = ClassLists(12, new long[] { 0, 1, 2, 3, 4, 5, 6, 7 }, new long[] { 12 });
            Equal(false, PassiveColumnRules.IsKeyListInto(beyond, List, 12), "a key past the table");
        }

        private static void ANewKindOfFlagIsFoundBesideTheKnownOnes()
        {
            const int Known = 0x41, Unknown = 0x42, Few = 0x43;
            var builder = new DatTableBuilder(RowSize);
            for (var row = 0; row < 40; row++)
            {
                var index = row;
                builder.Row(write => write.Text(0, $"node{index}").Byte(Known, (byte)(index % 2)).Byte(Unknown, (byte)(index >= 20 ? 1 : 0)).Byte(Few, (byte)(index < 3 ? 1 : 0)));
            }

            var table = builder.Build();
            var rows = Enumerable.Range(0, 40).ToList();
            var found = PassiveColumnRules.OtherFlags(table, rows, new HashSet<int> { Known });
            Equal(1, found.Count, "one column the kinds do not use is set on ten rows or more");
            Equal(Unknown, found[0].Offset, "its offset");
            Equal(20, found[0].Rows, "its rows");
            Equal("node20, node21, node22", string.Join(", ", found[0].Examples), "three example Ids");
            Equal(0, PassiveColumnRules.OtherFlags(table, rows.Take(15).ToList(), new HashSet<int> { Known }).Count, "a flag set on fewer rows of the tree than ten is none");
        }
    }
}
