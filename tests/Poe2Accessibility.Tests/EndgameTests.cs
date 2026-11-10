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
                ["en"] = new() { ["term/ContainsBreach"] = "Breach", ["passive/AtlasSmall1"] = "Essence Chance", ["area/MapUniqueInitialTower"] = "Precursor Tower",
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
    }
}
