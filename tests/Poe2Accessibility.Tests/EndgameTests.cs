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
            (nameof(AtlasLandingSaysStateAfterName), AtlasLandingSaysStateAfterName),
            (nameof(AtlasGraphWalksLinksAndSectors), AtlasGraphWalksLinksAndSectors),
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
                ["en"] = new() { ["term/ContainsBreach"] = "Breach", ["passive/AtlasSmall1"] = "Essence Chance", ["area/MapUniqueInitialTower"] = "Precursor Tower", ["content/PowerfulMapBoss"] = "Powerful Map Boss", ["content/VaalBeacons"] = "Vaal Beacons",
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
            Expect("Powerful Map Boss", string.Join("|", catalog.ContentNamesIn("Area contains a Powerful Map Boss", "en")), "the full name stands free in prose");
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
            Localization.Set("es", new());
            Expect("Completado", Announcements.AtlasState(AtlasNodeState.Completed), "the Spanish state word");
        }

        private static void AtlasGraphWalksLinksAndSectors()
        {
            var links = new Dictionary<string, string[]>
            {
                ["A"] = new[] { "B", "C" }, ["B"] = new[] { "A", "D" }, ["C"] = new[] { "A" }, ["D"] = new[] { "B", "E" }, ["E"] = new[] { "D" }, ["Z"] = new string[0],
            };
            var distances = AtlasGraph.Distances("A", id => links.GetValueOrDefault(id));
            Expect(0, distances["A"], "here itself");
            Expect(2, distances["D"], "two links on");
            Expect(3, distances["E"], "three links on");
            Expect(false, distances.ContainsKey("Z"), "an unlinked node is unreached");
            Expect(4, AtlasGraph.GridSteps(13, 18, 17, 17), "Rockpools to Slash in king steps");
            var nearest = AtlasGraph.NearestPerDirection(Vector2.Zero, new (Vector2, string)[]
            {
                (new Vector2(10, 0), "FarEast"), (new Vector2(2, 0), "NearEast"), (new Vector2(0, -3), "North"), (Vector2.Zero, "Here"),
            });
            Expect("NearEast|North", string.Join("|", nearest), "one nearest per direction, itself left out, nearest first");
            Expect(new Vector2(13, -18), AtlasGraph.ScreenPlace(13, 18), "the grid's y flips to the screen's");
        }
    }
}
