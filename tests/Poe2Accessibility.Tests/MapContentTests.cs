namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The world map's pictures: what a thing within an area is, whether it is done, and whether an area was entered.</summary>
    public static class MapContentTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APictureTellsItsKindAndWhetherItIsDone), APictureTellsItsKindAndWhetherItIsDone),
            (nameof(AThingIsSaidWithItsState), AThingIsSaidWithItsState),
            (nameof(AnAreaStateNeedsFlagsThatAgree), AnAreaStateNeedsFlagsThatAgree),
            (nameof(BehindAStopAreTheAreasNoTownLeadsTo), BehindAStopAreTheAreasNoTownLeadsTo),
        };

        private static void APictureTellsItsKindAndWhetherItIsDone()
        {
            const string Folder = "Art/Textures/Interface/2D/2DArt/UIImages/InGame/WorldMap/";
            Equal(("WorldMapContentBoss", true), MapContentIcons.Read(Folder + "WorldMapContentBossDone.dds|0,0,64,64|0")!.Value, "a slain boss");
            Equal(("WorldMapContentRareMonster", false), MapContentIcons.Read(Folder + "WorldMapContentRareMonster.dds|0,0,64,64|0")!.Value, "a rare monster left");
            Equal(("WorldMapContentTreasureMapFragments2", true), MapContentIcons.Read("Act4/WorldMapContentTreasureMapFragments2Done")!.Value, "a MapPins icon");
            Equal(("WorldMapFarmlandRune", true), MapContentIcons.Read(Folder + "Act1/WorldMapFarmlandRuneDone.dds|0,0,99,99|0")!.Value, "a runestone");
            Equal(("WorldMapContentGoddessOfJusticeRightActive", true), MapContentIcons.Read("Act4/WorldMapContentGoddessOfJusticeRightActive")!.Value, "a chosen scale");
            Equal(("WorldMapContentGoddessOfJustice", false), MapContentIcons.Read("Act4/WorldMapContentGoddessOfJustice")!.Value, "no scale chosen");
            Equal(true, MapContentIcons.Read(Folder + "4K/WorldMapInfoBg.dds|0,0,9,9|0") == null, "a frame");
            Equal(true, MapContentIcons.Read("Act4/WorldMapBorder_clouds_compass") == null, "the map's border");
            Equal(true, MapContentIcons.Read(string.Empty) == null, "no icon");
        }

        private static void AThingIsSaidWithItsState()
        {
            try
            {
                Localization.Detect("es");
                static string Kind(string picture) => picture == "WorldMapContentRareMonster" ? "Encuentro de monstruo raro" : string.Empty;
                Equal("Azote de los cielos, completado", MapContentIcons.Describe("Azote de los cielos", "WorldMapContentBossDone", Kind), "done");
                Equal("Cangrejo muerto, Encuentro de monstruo raro, pendiente", MapContentIcons.Describe("Cangrejo muerto", "WorldMapContentRareMonster", Kind), "left");
                Equal("Trozo de mapa rasgado, pendiente", MapContentIcons.Describe("Trozo de mapa rasgado", "Act4/WorldMapContentTreasureMapFragments1", Kind), "no word for the picture");
                Equal("Encuentro de monstruo raro", MapContentIcons.Describe("Encuentro de monstruo raro", "WorldMapContentRareMonster", Kind), "the map's legend");
                Equal("Kingsmarch", MapContentIcons.Describe("Kingsmarch", "Art/Other.dds|", Kind), "no such picture");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void BehindAStopAreTheAreasNoTownLeadsTo()
        {
            var connections = new Dictionary<string, string[]>
            {
                ["town"] = new[] { "keth", "deshar" },
                ["keth"] = new[] { "town", "lost city" },
                ["lost city"] = new[] { "keth", "shrines" },
                ["shrines"] = new[] { "lost city", "deshar" },
                ["deshar"] = new[] { "town", "spires" },
            };
            var catalog = new QuestGoalCatalog(new Dictionary<string, Dictionary<string, string[]>>(), connections);
            var towns = new HashSet<string> { "town" };
            Equal("lost city, shrines", string.Join(", ", catalog.Behind("keth", towns)), "behind Keth");
            Equal("spires", string.Join(", ", catalog.Behind("deshar", towns)), "behind Deshar");
            Equal(0, catalog.Behind("nowhere", towns).Count, "an unknown area");
        }

        private static void AnAreaStateNeedsFlagsThatAgree()
        {
            Equal(MapAreaState.Unknown, MapAreaStates.Of(1, 0, 0), "never entered");
            Equal(MapAreaState.Visited, MapAreaStates.Of(1, 1, 0), "entered");
            Equal(MapAreaState.Waypoint, MapAreaStates.Of(1, 1, 1), "its waypoint active");
            Equal(MapAreaState.None, MapAreaStates.Of(0, 0, 0), "a hidden pin");
            Equal(MapAreaState.None, MapAreaStates.Of(0, 1, 1), "entered and hidden");
            Equal(MapAreaState.None, MapAreaStates.Of(1, 7, 0), "no flags");
        }
    }
}
