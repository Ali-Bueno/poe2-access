using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns the census rules of `boss-mechanics` read (docs/research/boss-mechanics.md §13-§15), each found by what it
///     holds near where it was measured (<see cref="DatColumnSearch"/>) and checked on a row the game's data names: the skill
///     chain (GeometryAttack's ExecuteGEAL and buff, GeometryTrigger's skill, ExecuteGEAL's texts, SpawnObject, MiscObjects,
///     MoveDaemon, SummonedSpecificMonsters, a skill's extra stat sets), BuffDefinitions' count flag and the arena's areas.
/// </summary>
internal sealed class BossCensusColumns
{
    // Measured offsets (dat-kinds on the 0.5.5f files, 2026-11-12), where each search starts.
    internal const int AdditionalStatSetsOffset = 0xB8;
    internal const int GeometryGealOffset = 0x101;
    internal const int GeometryBuffOffset = 0x7F;
    internal const int TriggerSkillOffset = 0x4;
    internal const int GealScriptOffset = 0x60;
    internal const int GealObjectsOffset = 0x7B;
    internal const int GealCommandOffset = 0x8B;
    internal const int GealMoreCommandOffset = 0xAB;
    internal const int SpawnObjectsOffset = 0x4;
    internal const int ObjectPathOffset = 0x8;
    internal const int DaemonMonsterOffset = 0x18;
    internal const int SummonMonsterOffset = 0x4;
    internal const int ShowCountOffset = 0x2F;
    internal const int AreaBossesOffset = 0x9C;
    internal const int AreaTopologiesOffset = 0x60;
    internal const int TopologyPathOffset = 0x8;

    // Rows the 0.5.5f data names (§13): Azarian's censer GEAL, Kabala's bone barrier, Zalmarath's sword, Omniphobia's stew, Jamanra's
    // obsidian storm (a move-daemon), Siora's storm (a trigger of its single impact), Asinia's bone wall (a summon).
    internal const string CenserCommand = "CGEForsakenBurningGround";
    internal const string CenserScript = "AddLifeThreshold";
    internal const string Censer = "Metadata/Monsters/ForsakenSon/BossArenaObjects/ForsakenSonCenser";
    internal const string BarrierSkill = "GSSerpentCasterBossBoneBarrierEG";
    internal const string BarrierTarget = "Metadata/Monsters/SkeletonSnake/SkeletonSnakeSerpentCasterMinion";
    internal const string SwordSkill = "SOTitanBossStalagmite";
    internal const string Sword = "Metadata/Monsters/TitanBoss/BrainStalagmite";
    internal const string CountedBuff = "vomit_chunk_stew";
    internal const string StewSkill = "DeliriumBossPhysSprayExplode";
    internal const string DaemonSkill = "MDPerennialKingObsidianBall";
    internal const string Daemon = "Metadata/Monsters/InvisibleFire/MDPerennialKingObsidianStorm_";
    internal const string TriggerSkillId = "GTEzomyteReaperStormImpact";
    internal const string TriggeredSkill = "GSEzomyteReaperStormImpactSingle";
    internal const string SummonedWall = "Metadata/Monsters/WifeMonster/WifeMonsterBoneWall";
    internal const string TerrainFolder = "Metadata/Terrain/";

    public int AdditionalStatSets, GeometryGeal, GeometryBuff, TriggerSkill, GealScript, GealObjects, GealCommand, GealMoreCommand;
    public int SpawnObjects, ObjectPath, DaemonMonster, SummonMonster, ShowCount, AreaBosses, AreaTopologies, TopologyPath;

    /// <summary>The tables the columns are in.</summary>
    internal sealed record Tables(
        DatTable Monsters, DatTable Skills, DatTable StatSets, DatTable Geometry, DatTable Triggers, DatTable Geals, DatTable Spawns,
        DatTable Objects, DatTable Daemons, DatTable Summons, DatTable Buffs, DatTable Areas, DatTable Topologies);

    /// <summary>Every column, or null (said) when one was not found.</summary>
    /// <param name="skillId">GrantedEffects' SkillID column; <paramref name="statSet"/> its StatSet; <paramref name="boss"/> the known boss's row.</param>
    public static BossCensusColumns? Find(Tables t, int skillId, int statSet, int boss)
    {
        var search = new DatColumnSearch();
        var c = new BossCensusColumns();
        var censer = RowHolding(t.Geals, CenserCommand);
        c.GealCommand = search.Text(t.Geals, "ExecuteGEAL.ScriptCommand", GealCommandOffset, known: (censer, text => text.Contains(CenserCommand, StringComparison.Ordinal)));
        c.GealScript = search.Text(t.Geals, "ExecuteGEAL.Script", GealScriptOffset, known: (censer, text => text.Contains(CenserScript, StringComparison.Ordinal)));
        c.GealMoreCommand = search.Text(t.Geals, "ExecuteGEAL.MoreCommand", GealMoreCommandOffset, after: c.GealCommand);
        c.GealObjects = search.Checking("ExecuteGEAL.MetadataIDs", censer).Find(t.Geals, "ExecuteGEAL.MetadataIDs", GealObjectsOffset, offset =>
            t.Geals.IsStringListColumn(offset, path => path.StartsWith("Metadata/", StringComparison.Ordinal)) && (censer < 0 || t.Geals.StringList(censer, offset).Contains(Censer)));

        var geometryIds = IdRows(t.Geometry);
        var barrier = Through(t.Skills, BarrierSkill, skillId, geometryIds);
        c.GeometryGeal = search.Key(t.Geometry, "GeometryAttack.ExecuteGEAL", GeometryGealOffset, t.Geals,
            known: (barrier, key => c.GealObjects < 0 || (key >= 0 && key < t.Geals.RowCount && t.Geals.StringList((int)key, c.GealObjects).Contains(BarrierTarget))));
        var stew = Through(t.Skills, StewSkill, skillId, geometryIds);
        c.GeometryBuff = search.Key(t.Geometry, "GeometryAttack.Buff", GeometryBuffOffset, t.Buffs, known: (stew, key => DatAnchors.IdOf(t.Buffs, key) == CountedBuff));
        var storm = Through(t.Skills, TriggerSkillId, skillId, IdRows(t.Triggers));
        c.TriggerSkill = search.Key(t.Triggers, "GeometryTrigger.Skill", TriggerSkillOffset, t.Skills, known: (storm, key => DatAnchors.IdOf(t.Skills, key) == TriggeredSkill));

        c.ObjectPath = search.Text(t.Objects, "MiscObjects.Path", ObjectPathOffset, text => text.StartsWith("Metadata/", StringComparison.Ordinal));
        var sword = Through(t.Skills, SwordSkill, skillId, IdRows(t.Spawns));
        c.SpawnObjects = search.KeyList(t.Spawns, "SpawnObject.MiscObjects", SpawnObjectsOffset, t.Objects,
            known: (sword, keys => c.ObjectPath < 0 || keys.Any(key => key >= 0 && key < t.Objects.RowCount && t.Objects.StringOrNull((int)key, c.ObjectPath) == Sword)));
        var daemon = Through(t.Skills, DaemonSkill, skillId, IdRows(t.Daemons));
        c.DaemonMonster = search.Key(t.Daemons, "MoveDaemon.Monster", DaemonMonsterOffset, t.Monsters, known: (daemon, key => DatAnchors.IdOf(t.Monsters, key) == Daemon));
        var wall = DatColumnSearch.RowOf(t.Monsters, SummonedWall);
        c.SummonMonster = search.Checking("SummonedSpecificMonsters.Monster", wall).Find(t.Summons, "SummonedSpecificMonsters.Monster", SummonMonsterOffset, offset =>
            DatColumnSearch.IsKey(t.Summons, offset, t.Monsters.RowCount, wide: true) && (wall < 0 || DatAnchors.RowKeyed(t.Summons, offset, wall) >= 0));
        c.AdditionalStatSets = search.KeyList(t.Skills, "GrantedEffects.AdditionalStatSets", AdditionalStatSetsOffset, t.StatSets, statSet);

        var counted = DatColumnSearch.RowOf(t.Buffs, CountedBuff);
        c.ShowCount = search.Byte(t.Buffs, "BuffDefinitions.ShowCount", ShowCountOffset, value => value is 0 or 1, known: (counted, value => value == 1));
        c.TopologyPath = search.Text(t.Topologies, "Topologies.Path", TopologyPathOffset, text => text.StartsWith(TerrainFolder, StringComparison.OrdinalIgnoreCase));
        c.AreaBosses = search.Checking("WorldAreas.Bosses", boss).Find(t.Areas, "WorldAreas.Bosses", AreaBossesOffset, offset =>
            DatColumnSearch.IsKeyList(t.Areas, offset, t.Monsters.RowCount) && (boss < 0 || DatAnchors.RowListing(t.Areas, offset, boss) >= 0));
        c.AreaTopologies = search.Find(t.Areas, "WorldAreas.Topologies", AreaTopologiesOffset, offset =>
            DatColumnSearch.IsKeyList(t.Areas, offset, t.Topologies.RowCount) && (c.TopologyPath < 0 || LeadsToTerrain(t, offset, c.TopologyPath)));
        return search.Report("boss-mechanics census") ? c : null;
    }

    /// <summary>A table's Id (an i32 at +0) → its row.</summary>
    internal static Dictionary<int, int> IdRows(DatTable table)
    {
        var rows = new Dictionary<int, int>();
        for (var row = 0; row < table.RowCount; row++)
        {
            rows.TryAdd(table.Int32(row, 0), row);
        }

        return rows;
    }

    /// <summary>The row a named skill's SkillID reaches in a table of i32 Ids, or -1.</summary>
    private static int Through(DatTable skills, string skill, int skillId, IReadOnlyDictionary<int, int> ids)
    {
        using var probe = DatTable.Probe();
        return DatColumnSearch.RowOf(skills, skill) is var row and >= 0 && ids.TryGetValue(skills.Int32(row, skillId), out var target) ? target : -1;
    }

    /// <summary>The first row with a text holding <paramref name="fragment"/> in any column, or -1: a known row wherever its column moved.</summary>
    private static int RowHolding(DatTable table, string fragment)
    {
        using var probe = DatTable.Probe();
        return Enumerable.Range(0, table.RowCount).FirstOrDefault(row =>
            Enumerable.Range(0, table.RowSize).Any(offset => table.StringOrNull(row, offset) is { } text && text.Contains(fragment, StringComparison.Ordinal)), -1);
    }

    /// <summary>Whether most areas' topologies at this list lie under the terrain folder (the areas' own graphs).</summary>
    private static bool LeadsToTerrain(Tables t, int offset, int pathColumn)
    {
        int listed = 0, terrain = 0;
        for (var row = 0; row < t.Areas.RowCount; row++)
        {
            foreach (var key in t.Areas.KeyList(row, offset))
            {
                listed++;
                terrain += t.Topologies.StringOrNull((int)key, pathColumn) is { } path && path.StartsWith(TerrainFolder, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
        }

        return listed > 0 && terrain * 2 > listed;
    }
}
