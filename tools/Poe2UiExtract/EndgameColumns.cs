using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns the endgame step reads, each found by its type near where it was measured and by its value on a row the
///     data still has (<see cref="DatColumnSearch"/>): every key column has a known row, so a neighbouring key of the same
///     table that also fits is told apart. Offsets: the community dat-schema (poe2/_Core.gql of 2026-11-05) on this
///     install's row sizes (2026-11-08). Pure.
/// </summary>
internal sealed class EndgameColumns
{
    private const int IdOffset = 0x0;

    // Measured offsets (0.5.5f), where each column is looked for first. EndgameMapObjectives
    private const int ObjectiveTextOffset = 0x8;
    private const int ObjectiveDoneOffset = 0x10;
    private const int ObjectiveArtOffset = 0x18;
    private const int ObjectiveIconOffset = 0x20;

    // EndgameMapContent
    private const int ContentTextOffset = 0x28;
    private const int ContentNameOffset = 0x30;
    private const int ContentObjectiveOffset = 0x3C;
    private const int ContentVisualOffset = 0x4C;

    // EndgameMapContentVisualIdentity: the icon an Atlas node shows for a content
    private const int VisualIconOffset = 0x8;

    // EndgameMapNodeStats: what a node's stat adds to it (a Delirium fog bank), with its own icon and words; a row is its stat
    internal const int NodeStatStatOffset = 0x0;
    internal const int NodeStatVisualOffset = 0x44;
    internal const int NodeStatNameOffset = 0x54;
    internal const int NodeStatTextOffset = 0x64;

    // EndgameMapBiomes
    private const int BiomeNameOffset = 0x28;

    // EndgameMaps: a row is its area; its flavour text (at +0x20, looked for anywhere) tells it apart without that key
    internal const int MapAreaOffset = 0x0;
    internal const int MapPinOffset = 0x3C;

    // EndgameMapAtlas: one row per Atlas node, keyed by its PassiveSkills row
    internal const int NodePassiveOffset = 0x0;
    internal const int NodeMapsOffset = 0x10;
    internal const int NodeBiomesOffset = 0x24;
    internal const int NodePinOffset = 0x68;
    internal const int NodeBlockedOffset = 0x7C;
    internal const int NodeObjectiveOffset = 0xA7;
    internal const int NodeHintOffset = 0x10D;

    // EndgameMapAtlasTowers: the Atlas nodes a tower reaches, and the tower's own node
    internal const int TowerReachOffset = 0x14;
    internal const int TowerSelfOffset = 0x34;

    // KeywordPopups, ClientStrings2, WorldAreas
    private const int KeywordTermOffset = 0x8;
    private const int KeywordHelpOffset = 0x10;
    private const int ClientTextOffset = 0x8;
    private const int AreaNameOffset = 0x8;

    // PassiveSkills (unaligned, as dat-columns finds them)
    private const int PassiveGraphIdOffset = 0x30;
    private const int PassiveNameOffset = 0x32;

    /// <summary>Rows the value checks look at while the data has them: an objective, a content, an endgame area and an Atlas node of 0.5.5f.</summary>
    internal const string KnownObjective = "Breach";

    private const string KnownContent = "Abyss";


    private const string KnownKeyword = "ContainsBreach";

    private const string KnownClient = "EndgameMapObjectiveClickPrecursorBeacon";

    // Known rows of the key columns, each read from the 0.5.5f files (2026-11-10, dat-keys and dat-kinds): the row, and
    // what its key names there, chosen where the name says why (the merchant shows the trader's icon).
    internal const string KnownVisualContent = "Merchant";
    internal const string KnownVisual = "Trader";

    /// <summary>The node stat of the Delirium mirror: its icon, name and description name it too.</summary>
    internal const string KnownNodeStat = "map_delirium_has_giga_mirror";
    internal const string KnownNodeStatVisual = "DeliriumGigaMirror";
    internal const string KnownNodeStatName = "MapDeliriumGigaMirrorName";
    internal const string KnownNodeStatText = "MapDeliriumGigaMirrorDescription";

    /// <summary>The Penitentiary's flavour text, on the endgame map row whose area is that map.</summary>
    internal const string KnownMapFlavour = "Restless prisoners yearn for freedom.";
    internal const string KnownMapArea = "MapPenitentiary";

    /// <summary>A tower map shows the plain tower pin (its corrupted and cleansed pins are the two keys after).</summary>
    internal const string KnownTowerMap = "MapLostTowers";
    internal const string KnownTowerMapPin = "DefaultTower";

    /// <summary>The Kingsmarch node shows the Kingsmarch pin; found by the pin, it names the node.</summary>
    internal const string KnownPinNode = "AtlasKingsmarch";
    internal const string KnownNodePin = "Kingsmarch";

    /// <summary>The fourth reactor's node can be the unique map of that reactor.</summary>
    internal const string KnownNode = "Atlas4thReactor";
    internal const string KnownNodeMap = "MapUniqueReactor_04";

    internal const string KnownBiomeNode = "AtlasNoMapOnlyMountain1";
    internal const string KnownNodeBiome = "Mountain";
    internal const string KnownBlockedNode = "AtlasLeagueAbyssCrackNode1";
    internal const string KnownNodeBlocked = "AbyssEndgameCrackInaccessibleNorth";
    internal const string KnownObjectiveNode = "AtlasLeagueRitualOuterForest1";
    internal const string KnownNodeObjective = "Ritual";
    internal const string KnownHintNode = "AtlasLeagueDeliriumGigaMirror";
    internal const string KnownNodeHint = "MapObjectiveGigaMirror";

    /// <summary>The seventh wall tower reaches the red fortress's first node, and no other tower does; its own node is its one item, its reach holds it too.</summary>
    internal const string KnownTower = "AtlasWallTower7";
    internal const string KnownTowerReach = "AtlasInsideFortressRed1";

    private const string ArtPrefix = "Art/";

    public int ObjectiveText, ObjectiveDone, ObjectiveArt, ObjectiveIcon;
    public int ContentText, ContentName, ContentObjective, ContentVisual;
    public int VisualIcon;
    public int NodeStatVisual, NodeStatName, NodeStatText;
    public int BiomeName;
    public int MapArea, MapPin;
    public int NodePassive, NodeMaps, NodeBiomes, NodePin, NodeBlocked, NodeObjective, NodeHint;
    public int TowerReach, TowerSelf;
    public int KeywordTerm, KeywordHelp;
    public int ClientText, AreaName;
    public int PassiveGraphId, PassiveName;

    public static EndgameColumns? Find(IReadOnlyDictionary<string, DatTable> tables)
    {
        var search = new DatColumnSearch();
        var objectives = tables["endgamemapobjectives"];
        var clients = tables["clientstrings2"];
        var passives = tables["passiveskills"];
        var areas = tables["worldareas"];
        var pins = tables["endgamemappins"];

        var c = new EndgameColumns();
        var objective = DatColumnSearch.RowOf(objectives, KnownObjective);
        c.ObjectiveText = search.Text(objectives, "EndgameMapObjectives.ObjectiveText", ObjectiveTextOffset, NotArt, known: (objective, text => text.Contains(KnownKeyword, StringComparison.Ordinal)));
        c.ObjectiveDone = search.Text(objectives, "EndgameMapObjectives.CompletionText", ObjectiveDoneOffset, NotArt, c.ObjectiveText, (objective, Sentence));
        c.ObjectiveArt = search.Text(objectives, "EndgameMapObjectives.ContentIcon", ObjectiveArtOffset, Art, c.ObjectiveDone);
        c.ObjectiveIcon = search.Key(objectives, "EndgameMapObjectives.MiniMapIcon", ObjectiveIconOffset, tables["minimapicons"], c.ObjectiveArt, (objective, Named(tables["minimapicons"], KnownObjective)));

        var contents = tables["endgamemapcontent"];
        var visuals = tables["endgamemapcontentvisualidentity"];
        var content = DatColumnSearch.RowOf(contents, KnownContent);
        c.ContentText = search.Text(contents, "EndgameMapContent.Description", ContentTextOffset, NotArt, known: (content, Sentence));
        c.ContentName = search.Text(contents, "EndgameMapContent.Name", ContentNameOffset, NotArt, c.ContentText, (content, Filled));
        c.ContentObjective = search.Key(contents, "EndgameMapContent.MapObjective", ContentObjectiveOffset, objectives, c.ContentName, (content, Is(objectives, KnownContent)));
        c.ContentVisual = search.Key(contents, "EndgameMapContent.VisualIdentity", ContentVisualOffset, visuals, c.ContentObjective,
            (DatColumnSearch.RowOf(contents, KnownVisualContent), Is(visuals, KnownVisual)));
        c.VisualIcon = search.Text(visuals, "EndgameMapContentVisualIdentity.AtlasIcon", VisualIconOffset, Art);

        c.FindNodeStats(search, tables["endgamemapnodestats"], visuals, clients, tables["stats"]);
        c.BiomeName = search.Text(tables["endgamemapbiomes"], "EndgameMapBiomes.Name", BiomeNameOffset, NotArt);
        c.FindMaps(search, tables["endgamemaps"], areas, pins);
        c.FindAtlas(search, tables["endgamemapatlas"], passives, tables["endgamemaps"], areas, tables["endgamemapbiomes"], pins, clients, objectives);
        c.FindTowers(search, tables["endgamemapatlastowers"], tables["endgamemapatlas"], passives);

        var keywords = tables["keywordpopups"];
        var keyword = DatColumnSearch.RowOf(keywords, KnownKeyword);
        c.KeywordTerm = search.Text(keywords, "KeywordPopups.Term", KeywordTermOffset, NotArt, known: (keyword, text => Filled(text) && text != KnownKeyword));
        c.KeywordHelp = search.Text(keywords, "KeywordPopups.Definition", KeywordHelpOffset, NotArt, c.KeywordTerm, (keyword, Sentence));
        c.ClientText = search.Text(clients, "ClientStrings2.Text", ClientTextOffset, known: (DatColumnSearch.RowOf(clients, KnownClient), Sentence));
        c.AreaName = search.Text(areas, NameAnchor.Areas.Column, AreaNameOffset, NotArt, known: (DatColumnSearch.RowOf(areas, NameAnchor.Areas.Id), text => text == NameAnchor.Areas.Name));
        c.PassiveGraphId = search.UniqueUInt16(passives, "PassiveSkills.GraphId", PassiveGraphIdOffset);
        c.PassiveName = search.Text(passives, NameAnchor.Passives.Column, PassiveNameOffset, NotArt, known: (DatColumnSearch.RowOf(passives, NameAnchor.Passives.Id), text => text == NameAnchor.Passives.Name));
        return search.Report("endgame") ? c : null;
    }

    /// <summary>A node stat's icon, name and description; the known row is the one whose stat (read where measured) is the mirror's.</summary>
    internal void FindNodeStats(DatColumnSearch search, DatTable nodeStats, DatTable visuals, DatTable clients, DatTable stats)
    {
        var mirror = DatAnchors.RowKeyed(nodeStats, NodeStatStatOffset, DatColumnSearch.RowOf(stats, KnownNodeStat));
        this.NodeStatVisual = search.Key(nodeStats, "EndgameMapNodeStats.VisualIdentity", NodeStatVisualOffset, visuals, known: (mirror, Is(visuals, KnownNodeStatVisual)));
        this.NodeStatName = search.Key(nodeStats, "EndgameMapNodeStats.Name", NodeStatNameOffset, clients, this.NodeStatVisual, (mirror, Is(clients, KnownNodeStatName)));
        this.NodeStatText = search.Key(nodeStats, "EndgameMapNodeStats.Description", NodeStatTextOffset, clients, this.NodeStatName, (mirror, Is(clients, KnownNodeStatText)));
    }

    /// <summary>An endgame area's world area and pin: the area by the row its flavour text names, the pin by the row the area found names.</summary>
    internal void FindMaps(DatColumnSearch search, DatTable maps, DatTable areas, DatTable pins)
    {
        this.MapArea = search.Key(maps, "EndgameMaps.WorldArea", MapAreaOffset, areas, known: (DatAnchors.RowWithText(maps, KnownMapFlavour), Is(areas, KnownMapArea)));
        var tower = DatAnchors.RowKeyed(maps, this.MapArea, DatColumnSearch.RowOf(areas, KnownTowerMap));
        this.MapPin = search.Key(maps, "EndgameMaps.MapPin", MapPinOffset, pins, this.MapArea, (tower, Is(pins, KnownTowerMapPin)));
    }

    /// <summary>
    ///     What an Atlas node holds. The node's passive by the row whose pin (read where measured) is Kingsmarch's; every
    ///     other column by a node found through the passive column found, so a move of the passive is followed.
    /// </summary>
    internal void FindAtlas(DatColumnSearch search, DatTable atlas, DatTable passives, DatTable maps, DatTable areas, DatTable biomes, DatTable pins, DatTable clients, DatTable objectives)
    {
        var pinned = DatAnchors.RowKeyed(atlas, NodePinOffset, DatColumnSearch.RowOf(pins, KnownNodePin));
        this.NodePassive = search.Key(atlas, "EndgameMapAtlas.Passives", NodePassiveOffset, passives, known: (pinned, Is(passives, KnownPinNode)));
        int Node(string id) => DatAnchors.RowKeyed(atlas, this.NodePassive, DatColumnSearch.RowOf(passives, id));
        var mapArea = this.MapArea;
        bool ListsMap(IReadOnlyList<long> list) => mapArea >= 0 && list.Any(map => map >= 0 && map < maps.RowCount && AreaOf(maps, mapArea, areas, map) == KnownNodeMap);

        this.NodeMaps = search.KeyList(atlas, "EndgameMapAtlas.Maps", NodeMapsOffset, maps, this.NodePassive, (Node(KnownNode), ListsMap));
        this.NodeBiomes = search.KeyList(atlas, "EndgameMapAtlas.Biomes", NodeBiomesOffset, biomes, this.NodeMaps, (Node(KnownBiomeNode), list => list.Any(biome => DatAnchors.IdOf(biomes, biome) == KnownNodeBiome)));
        this.NodePin = search.Key(atlas, "EndgameMapAtlas.MapPin", NodePinOffset, pins, this.NodeBiomes, (Node(KnownPinNode), Is(pins, KnownNodePin)));
        this.NodeBlocked = search.Key(atlas, "EndgameMapAtlas.BlockedMessage", NodeBlockedOffset, clients, this.NodePin, (Node(KnownBlockedNode), Is(clients, KnownNodeBlocked)));
        this.NodeObjective = search.Key(atlas, "EndgameMapAtlas.MapObjective", NodeObjectiveOffset, objectives, this.NodeBlocked, (Node(KnownObjectiveNode), Is(objectives, KnownNodeObjective)));
        this.NodeHint = search.Key(atlas, "EndgameMapAtlas.Hint", NodeHintOffset, clients, this.NodeObjective, (Node(KnownHintNode), Is(clients, KnownNodeHint)));
    }

    /// <summary>A tower's reach and its own node, each by the row the other (read where measured) names, through the passive column found.</summary>
    internal void FindTowers(DatColumnSearch search, DatTable towers, DatTable atlas, DatTable passives)
    {
        long AtlasRow(string id) => this.NodePassive < 0 ? -1 : DatAnchors.RowKeyed(atlas, this.NodePassive, DatColumnSearch.RowOf(passives, id));
        var (tower, reached) = (AtlasRow(KnownTower), AtlasRow(KnownTowerReach));
        this.TowerReach = search.KeyList(towers, "EndgameMapAtlasTowers.AtlasNodes", TowerReachOffset, atlas,
            known: (DatAnchors.RowListing(towers, TowerSelfOffset, tower), list => reached >= 0 && list.Contains(reached)));
        this.TowerSelf = search.KeyList(towers, "EndgameMapAtlasTowers.Tower", TowerSelfOffset, atlas, this.TowerReach,
            (DatAnchors.RowListing(towers, this.TowerReach, reached), list => tower >= 0 && list.Count == 1 && list[0] == tower));
    }

    /// <summary>The Id of the world area an endgame map row names, read raw.</summary>
    private static string? AreaOf(DatTable maps, int areaColumn, DatTable areas, long map)
    {
        using var probe = DatTable.Probe();
        return DatAnchors.IdOf(areas, maps.Raw64((int)map, areaColumn));
    }

    private static bool Sentence(string text) => text.Contains(' ', StringComparison.Ordinal);

    private static bool Art(string text) => text.StartsWith(ArtPrefix, StringComparison.Ordinal);

    private static bool NotArt(string text) => !Art(text);

    private static bool Filled(string text) => text.Length > 0;

    private static Func<long, bool> Named(DatTable target, string word) => key => (DatAnchors.IdOf(target, key) ?? string.Empty).Contains(word, StringComparison.Ordinal);

    private static Func<long, bool> Is(DatTable target, string id) => key => DatAnchors.IdOf(target, key) == id;
}
