namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The endgame's data from the game's files: skill graph files, keyword markup, the catalog's lookups, link sides.</summary>
    public static class EndgameTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(SkillGraphFileReadsGroupsNodesAndLinks), SkillGraphFileReadsGroupsNodesAndLinks),
            (nameof(SkillGraphFileRefusesWhatItDoesNotKnow), SkillGraphFileRefusesWhatItDoesNotKnow),
            (nameof(MarkupShowsTheLinkedWords), MarkupShowsTheLinkedWords),
            (nameof(ObjectiveIconsNameWhatTheyMark), ObjectiveIconsNameWhatTheyMark),
            (nameof(AtlasNodesAreNamedByWhatTheirDataHolds), AtlasNodesAreNamedByWhatTheirDataHolds),
            (nameof(AtlasNodeDetailsAreTheGamesWords), AtlasNodeDetailsAreTheGamesWords),
            (nameof(LinksAreSaidClockwiseFromUp), LinksAreSaidClockwiseFromUp),
            (nameof(ContentIconsAreNamedByTheirArt), ContentIconsAreNamedByTheirArt),
            (nameof(TowersCountWhatTheyReach), TowersCountWhatTheyReach),
            (nameof(AtlasStateReadsBitsNotEquality), AtlasStateReadsBitsNotEquality),
            (nameof(AMovedAtlasNodeFieldIsNotBelieved), AMovedAtlasNodeFieldIsNotBelieved),
            (nameof(AtlasLandingSaysStateAfterName), AtlasLandingSaysStateAfterName),
            (nameof(AtlasGraphWalksLinksAndSectors), AtlasGraphWalksLinksAndSectors),
            (nameof(AtlasTreeMapReadsClustersKindsAndOrdering), AtlasTreeMapReadsClustersKindsAndOrdering),
            (nameof(AtlasTreeFinderWalksListsAndCountsPushes), AtlasTreeFinderWalksListsAndCountsPushes),
            (nameof(AtlasTreeNextHopStartsAlongTheLinks), AtlasTreeNextHopStartsAlongTheLinks),
            (nameof(AtlasDestinationFinderSkipsEmptyListsAndArrives), AtlasDestinationFinderSkipsEmptyListsAndArrives),
            (nameof(AtlasProgressSaysWhatChangedSinceLastOpening), AtlasProgressSaysWhatChangedSinceLastOpening),
            (nameof(AtlasProgressTakesAnotherAtlasSilently), AtlasProgressTakesAnotherAtlasSilently),
            (nameof(AtlasProgressKeepsStatesInItsFile), AtlasProgressKeepsStatesInItsFile),
            (nameof(AtlasTreeKindsComeFromTheMarksOverTheIds), AtlasTreeKindsComeFromTheMarksOverTheIds),
            (nameof(TheTreeFinderWalksOnlyTheChosenTrees), TheTreeFinderWalksOnlyTheChosenTrees),
            (nameof(TheShippedCatalogKeepsTheAtlasTreeATree), TheShippedCatalogKeepsTheAtlasTreeATree),
        };

        /// <summary>A version-3 graph: kind, orbit sizes, roots, then groups of nodes with their links (and each link's curve).</summary>
        private static byte[] Graph(params (float X, float Y, (int Id, int[] Links)[] Nodes)[] groups)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)SkillGraphFile.KnownVersion);
            writer.Write((byte)1);
            writer.Write((byte)2);
            writer.Write(new byte[] { 1, 6 });
            writer.Write(1);
            writer.Write(groups[0].Nodes[0].Id);
            writer.Write(0);
            writer.Write(groups.Length);
            foreach (var (x, y, nodes) in groups)
            {
                writer.Write(x);
                writer.Write(y);
                writer.Write(0);
                writer.Write(0);
                writer.Write((byte)0);
                writer.Write(nodes.Length);
                foreach (var (id, links) in nodes)
                {
                    writer.Write(id);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(links.Length);
                    foreach (var link in links)
                    {
                        writer.Write(link);
                        writer.Write(-3);
                    }
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static void SkillGraphFileReadsGroupsNodesAndLinks()
        {
            var bytes = Graph((0, 0, new[] { (10, new[] { 20 }), (11, Array.Empty<int>()) }), (100, -50, new[] { (20, Array.Empty<int>()) }));
            var graph = SkillGraphFile.Parse(bytes) ?? throw new Exception("not parsed");
            Expect(3, graph.Nodes.Count, "nodes");
            Expect(100f, graph.Nodes.Single(node => node.Id == 20).X, "a node takes its group's place");
            var links = graph.Links();
            Expect(true, links[20].Contains(10), "a link is known from both ends");
            Expect(0, links[11].Count, "a lone node");
        }

        private static void SkillGraphFileRefusesWhatItDoesNotKnow()
        {
            var bytes = Graph((0, 0, new[] { (10, Array.Empty<int>()) }));
            Expect(true, SkillGraphFile.Parse(bytes.Concat(new byte[] { 0 }).ToArray()) == null, "a byte left over");
            bytes[0] = SkillGraphFile.KnownVersion + 1;
            Expect(false, SkillGraphFile.Parse(bytes)!.VersionKnown, "another version is read, and flagged (DatColumnSearchTests say when it is kept)");
            Expect(true, SkillGraphFile.Parse(new byte[] { SkillGraphFile.KnownVersion, 1, 2, 1 }) == null, "cut short");
        }

        private static void MarkupShowsTheLinkedWords()
        {
            Expect("Open all Unstable Breaches", GameMarkup.Plain("Open all [ContainsBreach|Unstable Breaches]"), "shown words");
            Expect("Free all Essence Monsters", GameMarkup.Plain("Free all [Essence] Monsters"), "a bare keyword");
            Expect("Restore {0} of your Honour\nGain a random Minor Affliction", GameMarkup.BoxBody("Restore {0} of your [Honour]\r\nGain a random [Affliction|Minor Affliction]"), "a box's body: its links shown, its lines apart");
            Expect("Explodes when detonated", GameMarkup.BoxBody("<enchanted>{Explodes when detonated}"), "a colour is no text");
            Expect(string.Empty, GameMarkup.BoxBody("[IncursionCrystal|Energised Crystals] obtained: 0"), "a count the game fills in is left out");
            Expect(string.Empty, GameMarkup.BoxBody("획득한 에너지를 띤 수정: 0개"), "with its counter word too");
            Expect(string.Empty, GameMarkup.BoxBody("คริสตัลอาบพลังงานที่มี: 0 ชิ้น"), "a word apart");
            Expect("Restore 25% of Honour: at once", GameMarkup.BoxBody("Restore 25% of Honour: at once"), "a colon with words after it is no counter");
            Equal("Restore some of your Honour. Gain a random Minor Affliction", Announcements.ObjectDescription("Restore {0} of your Honour\nGain a random Minor Affliction"), "said a line a sentence, the amount unknown");
            Equal("Interact to take part in the Trial of the Sekhemas", Announcements.ObjectDescription("Interact to take part in\nthe Trial of the Sekhemas"), "a wrapped line is one sentence");
            Equal(string.Empty, Announcements.ObjectDescription(string.Empty), "nothing written, nothing said");
            Expect("5% increased Experience Gain\n-5% to Elemental Resistances\n3% reduced Movement Speed",
                GameMarkup.BoxBody("5% increased Experience Gain\r\n<corrupted>{-5% to Elemental Resistances\r\n3% reduced Movement Speed}"), "a colour over several lines (Alima's Disgrace, Qimah)");
            Equal("Movement Speed reduced by 3%. armour reduced by 15%", Announcements.ObjectDescription("Movement Speed reduced by 3%\narmour reduced by 15%"),
                "a lower-case line after an amount is a stat of its own");
            Expect("ContainsBreach", GameMarkup.FirstKeyword("Open all [ContainsBreach|Unstable Breaches] and [Shrine|Shrines]"), "the first link");
            Expect(string.Empty, GameMarkup.FirstKeyword("Defeat all Rare Monsters"), "no link");
        }

        private static EndgameCatalog Catalog() => new(new EndgameCatalog.CatalogData
        {
            Objectives = new()
            {
                ["Breach"] = new() { Icon = "Breach", Keyword = "ContainsBreach" },
                ["MapBoss"] = new() { Keyword = "MapBoss" },
            },
            Icons = new() { ["AtlasIconContentBreach"] = new() { Name = "term/ContainsBreach", Text = "objective/Breach" } },
            Nodes = new()
            {
                ["AtlasSmall1"] = new() { Graph = "atlasskillgraph", X = 0, Y = 0, Links = new() { "AtlasSmall2" } },
                ["AtlasOuterTower1"] = new() { Graph = "endgamemaplayoutgraph", X = 10, Y = 10, Maps = new() { "MapUniqueInitialTower" }, Biomes = new() { "Forest" }, Hint = "ClickBeacon" },
                ["AtlasPath1"] = new() { Graph = "endgamemaplayoutgraph", Biomes = new() { "Forest", "Swamp" }, Blocked = "NeedsTower", Objective = "Breach", Towers = new() { "AtlasOuterTower1" } },
                ["AtlasPath2"] = new() { Graph = "endgamemaplayoutgraph", Towers = new() { "AtlasOuterTower1" } },
            },
            Text = new()
            {
                ["en"] = new() { ["term/ContainsBreach"] = "Breach", ["passive/AtlasSmall1"] = "Essence Chance", ["area/MapUniqueInitialTower"] = "Precursor Tower", ["content/PowerfulMapBoss"] = "Powerful Map Boss", ["content/MapBoss"] = "Map Boss", ["content/VaalBeacons"] = "Vaal Beacons",
                    ["biome/Forest"] = "Forest", ["biome/Swamp"] = "Swamp", ["objective/Breach"] = "Open all Unstable Breaches", ["client/NeedsTower"] = "Complete the Tower",
                    ["client/ClickBeacon"] = "Activate the Precursor Beacon" },
                ["es"] = new() { ["term/ContainsBreach"] = "Fisura", ["biome/Forest"] = "Bosque", ["biome/Swamp"] = "Pantano" },
            },
        });

        private static void ObjectiveIconsNameWhatTheyMark()
        {
            var catalog = Catalog();
            Expect(true, catalog.IsObjectiveIcon("Breach"), "the breach's icon");
            Expect(false, catalog.IsObjectiveIcon("AbyssPitInactive"), "a spent icon is no objective");
            Expect(false, catalog.IsObjectiveIcon(null), "no icon");
            Expect("Fisura", catalog.DeviceName("Breach", "es"), "the keyword's word in the language");
            Expect("Breach", catalog.DeviceName("Breach", "fr"), "English without the language");
            Expect(string.Empty, catalog.DeviceName("Shrine", "es"), "an icon no objective names");
        }

        private static void AtlasNodesAreNamedByWhatTheirDataHolds()
        {
            var catalog = Catalog();
            Localization.Set("es", new());
            try
            {
                Expect("Essence Chance", catalog.NodeName("AtlasSmall1", "es"), "a passive's name, English without the language");
                Expect("Precursor Tower", catalog.NodeName("AtlasOuterTower1", "en"), "the one map a node can be");
                Expect("Bosque o Pantano", catalog.NodeName("AtlasPath1", "es"), "the biomes a node can be in");
                Expect(string.Empty, catalog.NodeName("AtlasPath2", "en"), "a node drawn at random");
                Expect(string.Empty, catalog.NodeName("Unknown", "en"), "no such node");
                Expect(true, catalog.SameGraph("AtlasPath1", "AtlasPath2") && !catalog.SameGraph("AtlasSmall1", "AtlasPath1"), "graphs");
            }
            finally
            {
                Localization.Set("en", new());
            }
        }

        private static void AtlasNodeDetailsAreTheGamesWords()
        {
            var catalog = Catalog();
            Expect("Open all Unstable Breaches|Complete the Tower", string.Join("|", catalog.NodeDetails("AtlasPath1", "en")), "objective, then what opens it");
            Expect("Activate the Precursor Beacon", string.Join("|", catalog.NodeDetails("AtlasOuterTower1", "es")), "the hint, in English without the language");
            Expect(0, catalog.NodeDetails("AtlasPath2", "en").Count, "nothing to say");
        }

        private static void LinksAreSaidClockwiseFromUp()
        {
            Localization.Set("en", new());
            var links = AtlasLinks.Describe(Vector2.Zero, new (Vector2?, string)[]
            {
                (new Vector2(-10, 0), "West"), (new Vector2(0, 10), "South"), (new Vector2(10, 0), "East"), (new Vector2(0, -10), "North"),
                (Vector2.Zero, "Here"), (null, "Unplaced"), (new Vector2(10, -10), string.Empty),
            });
            Expect("up, North|right, East|down, South|left, West|Here|Unplaced", string.Join("|", links), "clockwise from up, unplaced last, unnamed left out");
            Expect("North", string.Join("|", AtlasLinks.Describe(null, new (Vector2?, string)[] { (new Vector2(0, -10), "North") })), "no side without the node's own place");
        }

        private static void ContentIconsAreNamedByTheirArt()
        {
            var catalog = Catalog();
            Expect("Fisura", catalog.IconName("Art/2DArt/UIImages/InGame/AtlasScreen/AtlasIconContent/AtlasIconContentBreach.dds", "es"), "by the file, folder and extension aside");
            Expect("Breach", catalog.IconName(@"Art\2DArt\AtlasIconContentBreach|tint", "en"), "back slashes and a suffix");
            Expect(string.Empty, catalog.IconName("Art/2DArt/UIImages/InGame/Gold.dds", "en"), "other art");
            Expect(true, catalog.IsContentName("Breach", "en"), "a content's own name as text (canvas icons carry no readable art)");
            Expect(true, catalog.IsContentName("Fisura", "es"), "the same name in Spanish");
            Expect(false, catalog.IsContentName("Open all Unstable Breaches", "en"), "a description is not a content name");
            Expect(false, catalog.IsContentName("Breach", "es"), "names are per language");
            Expect("Vaal Beacons", string.Join("|", catalog.ContentNamesIn("Area contains Vaal Beacons", "en")), "the popup's own contents line");
            Expect("Powerful Map Boss", string.Join("|", catalog.ContentNamesIn("Area contains a Powerful Map Boss", "en")), "the longer name wins its place over the shorter inside it");
            Expect("Vaal Beacons|Breach", string.Join("|", catalog.ContentNamesIn("Vaal Beacons and a Breach", "en")), "contents in the text's order, not the pool's");
            Expect(string.Empty, string.Join("|", catalog.ContentNamesIn("Breaches everywhere", "en")), "no match inside a longer word");
        }

        private static void TowersCountWhatTheyReach()
        {
            var catalog = Catalog();
            Localization.Set("en", new());
            Expect(2, catalog.TowerReach("AtlasOuterTower1"), "the nodes in a tower's reach");
            Expect(0, catalog.TowerReach("AtlasPath1"), "no tower");
            Expect("up left", AtlasLinks.Side(new Vector2(10, 10), new Vector2(0, 0)), "the side a tower lies on, y down");
            Expect(string.Empty, AtlasLinks.Side(Vector2.One, Vector2.One), "the same place");
        }

        private static void AtlasStateReadsBitsNotEquality()
        {
            // The certified points of 2026-11-25, and the higher bits seen on special nodes never faking a state.
            Expect(AtlasNodeState.Locked, AtlasState.Of(0), "locked: The Burning Monolith");
            Expect(AtlasNodeState.Accessible, AtlasState.Of(1), "accessible: Gateway before, Steaming Springs");
            Expect(AtlasNodeState.Completed, AtlasState.Of(3), "completed: Gateway after");
            Expect(AtlasNodeState.Completed, AtlasState.Of(2), "the completed bit alone still reads completed");
            Expect(AtlasNodeState.Accessible, AtlasState.Of(0x11), "a higher special bit does not mask accessible");
            Expect(AtlasNodeState.Locked, AtlasState.Of(0x10), "special bits alone are not accessible");
            Expect(AtlasNodeState.Completed, AtlasState.Of(0x13), "a special bit over both state bits still reads completed");
        }

        /// <summary>A game update that moves the status byte or the grid reads other fields there: the canvas is judged before it is believed.</summary>
        private static void AMovedAtlasNodeFieldIsNotBelieved()
        {
            static List<(int, int, byte)> Canvas(int nodes, Func<int, byte> status, Func<int, int>? cell = null) =>
                Enumerable.Range(0, nodes).Select(index => (cell?.Invoke(index) ?? index - 20, -index, status(index))).ToList();

            Expect(true, AtlasState.Plausible(Canvas(60, index => (byte)(index < 20 ? 3 : index < 25 ? 1 : 0))) == true, "completed, a frontier and the locked rest");
            Expect(true, AtlasState.Plausible(Canvas(60, index => (byte)(index < 20 ? 0x13 : index < 25 ? 0x11 : 0x10))) == true, "special bits over them change nothing");
            Expect(true, AtlasState.Plausible(Canvas(60, index => (byte)(index == 0 ? 1 : 0))) == true, "an Atlas just opened: one node to enter");
            Expect(true, AtlasState.Plausible(Canvas(60, _ => 0)) == false, "no node can be entered: not the status byte");
            Expect(true, AtlasState.Plausible(Canvas(60, index => (byte)(index < 30 ? 2 : 1))) == false, "completed nodes that cannot be entered: not the status byte");
            Expect(true, AtlasState.Plausible(Canvas(60, index => (byte)(index < 20 ? 3 : 1), index => 0x7F000000 + index)) == false, "cells like pointers: not the grid");
            Expect(true, AtlasState.Plausible(Canvas(10, _ => 0)) == null, "too few catalogued nodes to tell");
        }

        private static void AtlasLandingSaysStateAfterName()
        {
            Localization.Set("en", new());
            var catalog = Catalog();
            Expect(
                "Steaming Springs. Accessible. Biome: Forest. Holds: Delirium. Links: right, Hilda's Campsite",
                Announcements.AtlasLanding("Steaming Springs", Announcements.AtlasState(AtlasNodeState.Accessible), "Forest", "Holds: Delirium", "Links: right, Hilda's Campsite"),
                "name, state, biome, contents, links");
            Expect(
                "The Burning Monolith. Locked",
                Announcements.AtlasLanding("The Burning Monolith", Announcements.AtlasState(AtlasNodeState.Locked), string.Empty, string.Empty, string.Empty),
                "no biome, contents or links said");
            Expect("Forest", catalog.BiomeNameIn("Biome: Forest", "en"), "the popup's biome line");
            Expect("Bosque", catalog.BiomeNameIn("Bioma: Bosque", "es"), "the same line in Spanish");
            Expect(string.Empty, catalog.BiomeNameIn("Long lost secrets lie buried beyond.", "en"), "flavour ends in no biome");
            Expect(string.Empty, catalog.BiomeNameIn("A Forest long lost to time.", "en"), "a biome named mid-flavour is not the line");
            Expect(string.Empty, catalog.BiomeNameIn("The old Forest", "en"), "a title ending in a biome, with no label, is not its biome");
            Localization.Set("es", new());
            try
            {
                Expect("Completado", Announcements.AtlasState(AtlasNodeState.Completed), "the Spanish state word");
            }
            finally
            {
                Localization.Set("en", new());
            }
        }

        private static void AtlasGraphWalksLinksAndSectors()
        {
            var links = new Dictionary<string, string[]>
            {
                ["A"] = new[] { "B", "C" }, ["B"] = new[] { "A", "D" }, ["C"] = new[] { "A", "E" }, ["D"] = new[] { "B", "E" }, ["E"] = new[] { "D", "C" }, ["Z"] = new string[0],
            };
            var distances = AtlasGraph.Distances("A", id => links.GetValueOrDefault(id));
            Expect(0, distances["A"], "here itself");
            Expect(2, distances["D"], "two links on");
            Expect(2, distances["E"], "the shortest of two ways");
            Expect(false, distances.ContainsKey("Z"), "an unlinked node is unreached");
            var revealed = AtlasGraph.Distances("A", id => links.GetValueOrDefault(id), id => id != "B");
            Expect(false, revealed.ContainsKey("B"), "an unrevealed node is not walked through");
            Expect(3, revealed["D"], "the admitted way goes around it");
            Expect("A|C|E", string.Join("|", AtlasGraph.Route("A", "E", id => links.GetValueOrDefault(id), id => id != "B")), "the admitted shortest way");
            var states = new Dictionary<string, AtlasNodeState>
            {
                ["A"] = AtlasNodeState.Completed, ["B"] = AtlasNodeState.Accessible, ["C"] = AtlasNodeState.Completed,
                ["D"] = AtlasNodeState.Locked, ["E"] = AtlasNodeState.Locked,
            };
            Expect("B", AtlasGraph.FirstGate("D", id => links.GetValueOrDefault(id), _ => true, id => states.GetValueOrDefault(id)) ?? string.Empty,
                "the accessible map just past completed ground");
            Expect(string.Empty, AtlasGraph.FirstGate("A", id => links.GetValueOrDefault(id), _ => true, id => states.GetValueOrDefault(id)) ?? string.Empty,
                "a completed destination has no gate");
            Expect("B", AtlasGraph.FirstGate("B", id => links.GetValueOrDefault(id), _ => true, id => states.GetValueOrDefault(id)) ?? string.Empty,
                "an accessible destination is itself");
            Expect(0, AtlasGraph.Route("A", "Z", id => links.GetValueOrDefault(id), _ => true).Count, "no way to an unlinked node");
            Expect(4, AtlasGraph.GridSteps(13, 18, 17, 17), "Rockpools to Slash in king steps");
            var nearest = AtlasGraph.NearestPerDirection(Vector2.Zero, new (Vector2, string)[]
            {
                (new Vector2(10, 0), "FarEast"), (new Vector2(2, 0), "NearEast"), (new Vector2(0, -3), "North"), (Vector2.Zero, "Here"),
            });
            Expect("NearEast|North", string.Join("|", nearest), "one nearest per direction, itself left out, nearest first");
            Expect(new Vector2(13, -18), AtlasGraph.ScreenPlace(13, 18), "the grid's y flips to the screen's");
        }

        /// <summary>Two clusters in a line: the generic tree's start, a small, a notable; the Abyss cluster the same, unlinked.</summary>
        private static AtlasTreeMap AtlasTreeFixture() => AtlasTreeMap.Build(new (string, int, int, IReadOnlyList<string>?)[]
        {
            ("AtlasGenericStart", 0, 0, new[] { "AtlasGenericSmall1" }),
            ("AtlasGenericSmall1", 100, 0, new[] { "AtlasGenericNotable1" }),
            ("AtlasGenericNotable1", 200, 0, new[] { "AtlasBiomeCity1" }),
            ("AtlasBiomeCity1", 300, 0, null),
            ("AtlasGenericMastery1", 100, 50, null),
            ("AtlasAbyssStart", 500, 0, new[] { "AtlasAbyssSmall1" }),
            ("AtlasAbyssSmall1", 600, 0, new[] { "AtlasAbyssNotable1" }),
            ("AtlasAbyssNotable1", 700, 0, null),
            ("AtlasIncursionStart_", 900, 0, null),
        });

        private static void AtlasTreeMapReadsClustersKindsAndOrdering()
        {
            Expect("Abyss", AtlasTreeMap.ClusterOf("AtlasAbyssNotable1"), "the kind ends the cluster");
            Expect("Generic", AtlasTreeMap.ClusterOf("AtlasGenericSmall78_"), "a small of the generic tree");
            Expect("AzmeriSpirit", AtlasTreeMap.ClusterOf("AtlasAzmeriSpiritEssenceChance"), "a named small with no kind word");
            Expect("AzmeriSpirit", AtlasTreeMap.ClusterOf("AtlasAzmeriSpiritEssenceNotable"), "an essence notable keeps its cluster, not a cluster of its own");
            Expect("BiomeCity", AtlasTreeMap.ClusterOf("AtlasBiomeCity1"), "a bare number ends the cluster");
            Expect("Azmeri Spirit", AtlasTreeMap.SaidCluster("AzmeriSpirit"), "a cluster as said");
            var tree = AtlasTreeFixture();
            Expect(8, tree.Ids.Length, "the mastery icon is left out");
            Expect("Generic|Abyss", $"{tree.Clusters[tree.Index["AtlasBiomeCity1"]]}|{tree.Clusters[tree.Index["AtlasAbyssNotable1"]]}",
                "a node's tree is its start's, not its id's finer part (\"Biome City\" was said, live 2026-11-27)");
            var small = tree.Index["AtlasGenericSmall1"];
            var distances = tree.DistancesFrom(small);
            Expect(1, distances[tree.Index["AtlasGenericNotable1"]], "its notable one link on, the link read back the other way");
            Expect(PassiveMap.NoPath, distances[tree.Index["AtlasAbyssNotable1"]], "no links join the clusters");
            Expect("AtlasGenericNotable1|AtlasAbyssNotable1", string.Join("|", tree.Members(AtlasTreeCategory.Notables, small, distances)),
                "notables: linked first in links, the other cluster's after");
            Expect("AtlasGenericStart|AtlasAbyssStart|AtlasIncursionStart_", string.Join("|", tree.Members(AtlasTreeCategory.Clusters, small, distances)),
                "cluster starts by drawn distance, the trailing-underscore start among them");
        }

        private static void AtlasTreeFinderWalksListsAndCountsPushes()
        {
            var tree = AtlasTreeFixture();
            var small = tree.Index["AtlasGenericSmall1"];
            var distances = tree.DistancesFrom(small);
            IReadOnlyList<string> Members(AtlasTreeCategory category) => tree.Members(category, small, distances);
            var finder = new AtlasTreeFinder();
            finder.ChangeCategory(1, Members);
            Expect("Notables", finder.Category.ToString(), "down once from Position");
            Expect("AtlasGenericNotable1", finder.Target, "the nearest notable is the target, nothing to confirm");
            finder.ChangeEntry(1, Members(finder.Category));
            Expect("AtlasAbyssNotable1", finder.Target, "right walks the entries");
            finder.ChangeEntry(1, Members(finder.Category));
            Expect("AtlasGenericNotable1", finder.Target, "entries wrap");
            finder.ChangeCategory(1, Members);
            Expect("AtlasGenericStart", finder.Target, "the clusters' nearest start");
            finder.ChangeCategory(1, Members);
            Expect("Position", finder.Category.ToString(), "lists wrap back to Position");
            Expect(string.Empty, finder.Target, "Position lets the target go");
            finder.ChangeCategory(1, Members);
            Expect(true, finder.Arrive("AtlasGenericNotable1"), "landing on the target arrives");
            Expect(string.Empty, finder.Target, "arrival ends the guide");
            Expect(false, finder.Arrive("AtlasGenericNotable1"), "arrival fires once");
            var way = tree.Way(tree.Places, tree.Index["AtlasGenericStart"], tree.Index["AtlasGenericNotable1"], 60, out var far);
            Expect(false, far, "two pushes are not far");
            Expect(0, way?.Octant ?? -1, "the first push is right, octant 0");
            Expect(2, way?.Pushes ?? -1, "start to notable through the small: 2 pushes");
            finder.ChangeCategory(-1, Members);
            Expect("Position", finder.Category.ToString(), "up walks the lists back");
        }

        private static void AtlasTreeNextHopStartsAlongTheLinks()
        {
            var tree = AtlasTreeMap.Build(new (string, int, int, IReadOnlyList<string>?)[]
            {
                ("AtlasGenericStart", 0, 0, new[] { "AtlasGenericSmall1" }),
                ("AtlasGenericSmall1", 10, 50, new[] { "AtlasGenericNotable1" }),
                ("AtlasGenericNotable1", 100, 0, null),
                ("AtlasAbyssStart", 500, 0, null),
            });
            var start = tree.Index["AtlasGenericStart"];
            Expect(6, tree.NextHopOctant(tree.Places, start, tree.Index["AtlasGenericNotable1"]) ?? -1,
                "the shortest way begins down to the small, though the notable lies right");
            Expect(-1, tree.NextHopOctant(tree.Places, start, tree.Index["AtlasAbyssStart"]) ?? -1,
                "no links lead to another cluster");
            Expect(-1, tree.NextHopOctant(tree.Places, start, start) ?? -1, "already there");
            var shared = AtlasTreeMap.Build(new (string, int, int, IReadOnlyList<string>?)[]
            {
                ("AtlasGenericStart", 0, 0, new[] { "AtlasGenericSmall1" }),
                ("AtlasGenericSmall1", 0, 0, new[] { "AtlasGenericNotable1" }),
                ("AtlasGenericNotable1", 100, 0, null),
            });
            Expect(-1, shared.NextHopOctant(shared.Places, shared.Index["AtlasGenericStart"], shared.Index["AtlasGenericNotable1"]) ?? -1,
                "a next hop on the same group centre has no side to say, never an invented right");
        }

        private static void AtlasDestinationFinderSkipsEmptyListsAndArrives()
        {
            IReadOnlyList<string> Members(AtlasDestinationCategory category) => category switch
            {
                AtlasDestinationCategory.Accessible => new[] { "near", "far" },
                AtlasDestinationCategory.SeasonalActivities => new[] { "objective" },
                _ => Array.Empty<string>(),
            };
            var finder = new AtlasDestinationFinder();
            finder.ChangeCategory(1, Members);
            Expect("Accessible", finder.Category.ToString(), "the first list with destinations");
            Expect("near", finder.Target, "its nearest destination is chosen at once");
            finder.ChangeEntry(1, Members(finder.Category));
            Expect("far", finder.Target, "entries walk");
            finder.ChangeEntry(1, Members(finder.Category));
            Expect("near", finder.Target, "entries wrap");
            finder.ChangeCategory(1, Members);
            Expect("objective", finder.Target, "the empty landmarks list is passed over");
            Expect(true, finder.Arrive("objective"), "landing there arrives");
            Expect(string.Empty, finder.Target, "arrival drops the destination");
            Expect(false, finder.Arrive("objective"), "arrival fires once");
            finder.ChangeCategory(1, Members);
            Expect("Position", finder.Category.ToString(), "lists wrap home");
            finder.ChangeCategory(-1, Members);
            Expect("objective", finder.Target, "walking the lists back reaches the last list with destinations");
        }

        private static (int, int, AtlasNodeState, string)[] Opening(params (int X, AtlasNodeState State, string Name)[] nodes) =>
            nodes.Select(node => (node.X, 0, node.State, node.Name)).ToArray();

        private static void AtlasProgressSaysWhatChangedSinceLastOpening()
        {
            var progress = new AtlasProgress(null);
            Expect(true, progress.Update("hero", Opening((1, AtlasNodeState.Completed, "Home"), (2, AtlasNodeState.Accessible, "Caldera"), (3, AtlasNodeState.Locked, "Pit"),
                (4, AtlasNodeState.Locked, "Bastille"), (5, AtlasNodeState.Locked, "Slash"), (6, AtlasNodeState.Accessible, "Marrow")), 3) == null, "the first opening has nothing to compare");
            var reopened = Opening((1, AtlasNodeState.Completed, "Home"), (2, AtlasNodeState.Completed, "Caldera"), (3, AtlasNodeState.Accessible, "Pit"),
                (4, AtlasNodeState.Completed, "Bastille"), (5, AtlasNodeState.Accessible, ""), (6, AtlasNodeState.Locked, "Marrow"), (7, AtlasNodeState.Accessible, "New"));
            var change = progress.Update("hero", reopened, 3)!;
            Expect("Bastille|Caldera", string.Join("|", change.Completed), "completed, a locked one cleared at once included, by name");
            Expect(2, change.NowAccessibleCount, "a locked place now accessible counts, named or not");
            Expect("Pit", string.Join("|", change.NowAccessible), "only named places are said; a place new to the opening has no earlier state");
            Expect(true, progress.Update("other", Opening((1, AtlasNodeState.Locked, "Home")), 3) == null, "each character has its own Atlas");
            Expect(true, progress.Update("hero", reopened, 3)!.IsEmpty, "a reopening with nothing new is empty");
            Expect(true, progress.Update("dup", Opening((1, AtlasNodeState.Locked, "A"), (1, AtlasNodeState.Accessible, "")), 3) == null, "a place drawn twice is one place");
            Expect(0, progress.Update("dup", Opening((1, AtlasNodeState.Accessible, "A")), 3)!.NowAccessibleCount, "it kept its furthest state");

            var many = new AtlasProgressChange(new[] { "A", "B", "C" }, 5, new[] { "D" }, 1);
            Expect("Since last time: 5 completed: A, B, C and 2 more. 1 now accessible: D", Announcements.AtlasSinceLastTime(many), "names first, the rest counted");
            Expect("Since last time: 2 now accessible", Announcements.AtlasSinceLastTime(new AtlasProgressChange(Array.Empty<string>(), 0, Array.Empty<string>(), 2)), "unnamed places are counted");
        }

        private static void AtlasProgressTakesAnotherAtlasSilently()
        {
            var progress = new AtlasProgress(null);
            _ = progress.Update("hero", Enumerable.Range(0, 10).Select(x => (x, 0, AtlasNodeState.Locked, $"Old{x}")), 3);
            // A reworked Atlas after a patch: few places in common, most of them changed.
            var reworked = Enumerable.Range(6, 10).Select(x => (x, 0, AtlasNodeState.Accessible, $"New{x}")).ToList();
            Expect(true, progress.Update("hero", reworked, 3) == null, "under half the places shared: another Atlas, kept without a diff");
            Expect(true, progress.Update("hero", reworked, 3)!.IsEmpty, "and compared against from then on");
            Expect(1, progress.Update("hero", reworked.Take(9).Append((15, 0, AtlasNodeState.Completed, "New15")), 3)!.CompletedCount, "a canvas built in part still diffs on what it shares");
        }

        private static void AtlasProgressKeepsStatesInItsFile()
        {
            var file = Path.Combine(Path.GetTempPath(), $"atlas-progress-{Guid.NewGuid():N}.txt");
            try
            {
                _ = new AtlasProgress(file).Update("hero", Opening((1, AtlasNodeState.Locked, "Pit"), (2, AtlasNodeState.Accessible, "Caldera")), 3);
                File.AppendAllLines(file, new[] { "broken line", "hero\t9,9\tNotAState", "hero\t8,8\t7" });
                var change = new AtlasProgress(file).Update("hero", Opening((1, AtlasNodeState.Accessible, "Pit"), (2, AtlasNodeState.Completed, "Caldera")), 3)!;
                Expect("Caldera / Pit", $"{string.Join("|", change.Completed)} / {string.Join("|", change.NowAccessible)}", "a new session reads the last opening; bad lines are passed over");
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static void AtlasTreeKindsComeFromTheMarksOverTheIds()
        {
            // Ids that say nothing of their kind, as a reworked tree may name them: the marks and the roots tell.
            var nodes = new (string, int, int, IReadOnlyList<string>?)[]
            {
                ("AtlasNode1", 0, 0, new[] { "AtlasNode2" }),
                ("AtlasNode2", 100, 0, new[] { "AtlasNode3", "AtlasNode4" }),
                ("AtlasNode3", 200, 0, null),
                ("AtlasNode4", 100, 50, null),
                ("AtlasNode5", 300, 0, new[] { "AtlasNode3" }),
            };
            var marks = new Dictionary<string, (string?, bool)>
            {
                ["AtlasNode1"] = (null, true), ["AtlasNode3"] = ("n", false), ["AtlasNode4"] = ("i", false), ["AtlasNode5"] = ("k", false),
            };
            var tree = AtlasTreeMap.Build(nodes, id => marks.TryGetValue(id, out var mark) ? mark : (null, false));
            var first = tree.Index["AtlasNode1"];
            var last = tree.Index["AtlasNode5"];
            Expect(false, tree.Index.ContainsKey("AtlasNode4"), "a mastery by its mark is left out");
            Expect("AtlasNode3|AtlasNode5", string.Join("|", tree.Members(AtlasTreeCategory.Notables, first, tree.DistancesFrom(first))), "notables and keystones by their marks");
            Expect("AtlasNode1", string.Join("|", tree.Members(AtlasTreeCategory.Clusters, last, tree.DistancesFrom(last))), "a cluster starts at a root of the graph");
            var unmarked = AtlasTreeMap.Build(new (string, int, int, IReadOnlyList<string>?)[] { ("AtlasGenericStart", 0, 0, null), ("AtlasGenericNotable1", 1, 0, null), ("AtlasGenericMastery1", 2, 0, null) }, _ => (null, false));
            var start = unmarked.Index["AtlasGenericStart"];
            Expect("AtlasGenericNotable1", string.Join("|", unmarked.Members(AtlasTreeCategory.Notables, start, unmarked.DistancesFrom(start))), "a catalog with no marks reads the kinds from the ids");
            Expect(false, unmarked.Index.ContainsKey("AtlasGenericMastery1"), "and its masteries too");
        }

        /// <summary>The catalog as shipped: the Atlas skill tree's pins and its root's town map do not make it the map (the owner, 2026-11-27: the finder was silent).</summary>
        private static void TheShippedCatalogKeepsTheAtlasTreeATree()
        {
            var directory = AppContext.BaseDirectory;
            while (!File.Exists(Path.Join(directory, "Poe2Accessibility", "catalog", "endgame.json")))
            {
                directory = Path.GetDirectoryName(directory) ?? throw new FileNotFoundException("endgame.json is not under any folder above the tests");
            }

            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<EndgameCatalog.CatalogData>(File.ReadAllText(Path.Join(directory, "Poe2Accessibility", "catalog", "endgame.json")))!;
            var catalog = new EndgameCatalog(data);
            Expect(true, catalog.HasTreeFinder("atlasskillgraph"), "the Atlas skill tree has its finder");
            Expect(false, catalog.IsTree("endgamemaplayoutgraph"), "the Atlas map's layout is no tree");
            Expect(string.Empty, catalog.NodeName("AtlasGenericStart", "es"), "the tree's root is not named as the town (El refugio del zigurat)");
        }

        private static void TheTreeFinderWalksOnlyTheChosenTrees()
        {
            var catalog = new EndgameCatalog(new EndgameCatalog.CatalogData
            {
                Graphs = new()
                {
                    ["atlaspassivegraph"] = "metadata/atlasskillgraphs/atlaspassivegraph.psg",
                    ["endgamemaplayoutgraph"] = "metadata/atlasskillgraphs/endgamemaplayoutgraph.psg",
                    ["chayulatreepassiveskillgraph"] = "metadata/leagueskillgraphs/chayulatreepassiveskillgraph.psg",
                },
                Nodes = new()
                {
                    ["Renamed1"] = new() { Graph = "atlaspassivegraph", Root = true, Flags = "n" },
                    ["Renamed2"] = new() { Graph = "atlaspassivegraph", Pin = "PrecursorMap" },
                    ["Renamed3"] = new() { Graph = "atlaspassivegraph", Root = true, Pin = "BaseCamp", Maps = new() { "G_Endgame_Town" } },
                    ["Map1"] = new() { Graph = "endgamemaplayoutgraph", Root = true, Pin = "WaypointPin", Biomes = new() { "Forest" } },
                    ["Map2"] = new() { Graph = "endgamemaplayoutgraph" },
                    ["League1"] = new() { Graph = "chayulatreepassiveskillgraph", Root = true },
                },
            });
            Expect(true, catalog.HasTreeFinder("atlaspassivegraph"), "a rooted tree in the Atlas's folder, whatever its file is named");
            Expect(true, catalog.IsTree("atlaspassivegraph"), "a tree whose nodes carry pins and its root the town's map stays a tree (catalog of 2026-11-25)");
            Expect(false, catalog.HasTreeFinder("endgamemaplayoutgraph"), "the map's layout is never a tree, roots or not");
            Expect(false, catalog.HasTreeFinder("chayulatreepassiveskillgraph"), "a league tree is not chosen, rooted or not");
            Expect(false, catalog.HasTreeFinder("unknowngraph"), "a graph the catalog has no file for");
            var old = new EndgameCatalog(new EndgameCatalog.CatalogData { Nodes = new() { ["Old1"] = new() { Graph = AtlasTreeMap.LegacyGraphName }, ["League1"] = new() { Graph = "chayulatreepassiveskillgraph", Root = true } } });
            Expect("True|False", $"{old.HasTreeFinder(AtlasTreeMap.LegacyGraphName)}|{old.HasTreeFinder("chayulatreepassiveskillgraph")}", "a catalog without graph paths keeps the 0.5 Atlas tree alone");
            Expect<(string?, bool)>(("n", true), catalog.Marks("Renamed1"), "a node's marks");

            var nodes = Enumerable.Range(0, 200).ToDictionary(at => $"Map{at}", at => new EndgameCatalog.CatalogNode { Graph = "layout", Pin = at < 190 ? "PrecursorMap" : at < 192 ? "CitadelPin" : "Breach" });
            var pins = new EndgameCatalog(new EndgameCatalog.CatalogData { Nodes = nodes });
            Expect("False|True|False", $"{pins.HasRarePin("Map0")}|{pins.HasRarePin("Map190")}|{pins.HasRarePin("Map195")}", "a pin on 1% of the map's nodes at most is a one-off place, whatever its name");
        }
    }
}
