using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Reads skills through their kind's table (docs/research/boss-mechanics.md §13), each once.</summary>
internal sealed class BossSkills
{
    /// <summary>The stat of a skill that cannot be dodged, evaded or blocked, and the one naming a summon's SummonedSpecificMonsters row.</summary>
    internal const string UnavoidableStat = "base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked";
    internal const string SummonStat = "alternate_minion";

    private static readonly Regex BuffCall = new(@"\b\w*Buff\w*\(\s*([A-Za-z_]\w*)");

    private readonly BossCensusColumns.Tables t;
    private readonly DatTable kinds;
    private readonly BossCensusColumns c;
    private readonly ProjectileColumns stats;
    private readonly int skillKind, skillId, statSet, geometryLength;
    private readonly Dictionary<int, int> geometry, triggers, spawns, daemons, summons;
    private readonly Dictionary<string, int> gealRows = new(StringComparer.Ordinal);
    private readonly HashSet<string> buffIds, monsterIds;
    private readonly long unavoidable, summon;
    private readonly Dictionary<int, BossSkill> read = new();

    public BossSkills(BossCensusColumns.Tables tables, BossCensusColumns columns, ProjectileColumns statColumns, DatTable kinds, DatTable stats, int skillKindColumn, int skillIdColumn, int geometryLengthColumn)
    {
        (this.t, this.c, this.stats) = (tables, columns, statColumns);
        (this.skillKind, this.skillId, this.statSet, this.geometryLength) = (skillKindColumn, skillIdColumn, statColumns.SkillStatSet, geometryLengthColumn);
        this.kinds = kinds;
        (this.geometry, this.triggers, this.spawns) = (BossCensusColumns.IdRows(tables.Geometry), BossCensusColumns.IdRows(tables.Triggers), BossCensusColumns.IdRows(tables.Spawns));
        (this.daemons, this.summons) = (BossCensusColumns.IdRows(tables.Daemons), BossCensusColumns.IdRows(tables.Summons));
        for (var row = 0; row < tables.Geals.RowCount; row++)
        {
            this.gealRows.TryAdd(tables.Geals.String(row, 0), row);
        }

        this.buffIds = Enumerable.Range(0, tables.Buffs.RowCount).Select(row => tables.Buffs.String(row, 0)).ToHashSet(StringComparer.Ordinal);
        this.monsterIds = Enumerable.Range(0, tables.Monsters.RowCount).Select(row => tables.Monsters.String(row, 0)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        (this.unavoidable, this.summon) = (DatColumnSearch.RowOf(stats, UnavoidableStat), DatColumnSearch.RowOf(stats, SummonStat));
    }

    /// <summary>The buffs a script or command text names in a buff call ("AddBuff( asala_sand_shield, 0.15 )"): only ids BuffDefinitions has.</summary>
    public IEnumerable<string> BuffsIn(string text) => BuffCall.Matches(text).Select(match => match.Groups[1].Value).Where(this.buffIds.Contains).Distinct();

    public bool IsBuff(string id) => this.buffIds.Contains(id);

    /// <summary>A skill row's facts, with the skills it triggers read the same way by the caller.</summary>
    public BossSkill Read(int row)
    {
        if (this.read.TryGetValue(row, out var known))
        {
            return known;
        }

        var kind = DatAnchors.IdOf(this.kinds, this.t.Skills.Key(row, this.skillKind)) ?? string.Empty;
        var id = this.t.Skills.Int32(row, this.skillId);
        var (area, length) = (false, 0);
        var (geals, objects, daemonIds, summonIds, buffs, triggered) = (new List<BossGeal>(), new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());
        if (kind.StartsWith("geometry_trigger", StringComparison.Ordinal) && this.triggers.TryGetValue(id, out var trigger))
        {
            area = true;
            AddId(triggered, this.t.Skills, this.t.Triggers.Key(trigger, this.c.TriggerSkill));
        }
        else if (kind.StartsWith("geometry_", StringComparison.Ordinal) && !kind.StartsWith("geometry_projectiles", StringComparison.Ordinal) && this.geometry.TryGetValue(id, out var shape))
        {
            (area, length) = (true, this.t.Geometry.Int32(shape, this.geometryLength));
            if (this.t.Geometry.Key(shape, this.c.GeometryGeal) is var geal and >= 0 && geal < this.t.Geals.RowCount)
            {
                geals.Add(this.Geal((int)geal));
            }

            AddId(buffs, this.t.Buffs, this.t.Geometry.Key(shape, this.c.GeometryBuff));
        }

        if (kind.StartsWith("execute_geal", StringComparison.Ordinal) && this.gealRows.TryGetValue(id.ToString(System.Globalization.CultureInfo.InvariantCulture), out var gealRow))
        {
            geals.Add(this.Geal(gealRow));
        }

        if (kind.StartsWith("spawn_object", StringComparison.Ordinal) && this.spawns.TryGetValue(id, out var spawn))
        {
            objects.AddRange(this.t.Spawns.KeyList(spawn, this.c.SpawnObjects).Where(item => item >= 0 && item < this.t.Objects.RowCount)
                .Select(item => this.t.Objects.StringOrNull((int)item, this.c.ObjectPath)).OfType<string>().Where(path => path.Length > 0));
        }

        if (kind == "move_daemon" && this.daemons.TryGetValue(id, out var daemon))
        {
            AddId(daemonIds, this.t.Monsters, this.t.Daemons.Key(daemon, this.c.DaemonMonster));
        }

        var (constants, implicits) = this.StatsOf(row);
        if (this.summon >= 0 && constants.TryGetValue(this.summon, out var minion) && this.summons.TryGetValue(minion, out var summoned))
        {
            AddId(summonIds, this.t.Monsters, this.t.Summons.Key(summoned, this.c.SummonMonster));
        }

        buffs.AddRange(geals.SelectMany(geal => this.BuffsIn(geal.Self + " " + geal.Each)));
        var unavoidableSkill = this.unavoidable >= 0 && (implicits.Contains(this.unavoidable) || constants.GetValueOrDefault(this.unavoidable) != 0);
        var skill = new BossSkill(this.t.Skills.String(row, 0), kind, area, length, geals, objects.Distinct().ToList(), daemonIds, summonIds, buffs.Distinct().ToList(), triggered, unavoidableSkill);
        this.read[row] = skill;
        return skill;
    }

    /// <summary>The ExecuteGEAL rows of GeometryAttack areas no skill casts (the server's own, as Asala's shelter).</summary>
    public IReadOnlyList<BossGeal> Orphans()
    {
        var cast = Enumerable.Range(0, this.t.Skills.RowCount).Select(row => this.t.Skills.Int32(row, this.skillId)).ToHashSet();
        var orphans = new List<BossGeal>();
        for (var row = 0; row < this.t.Geometry.RowCount; row++)
        {
            if (!cast.Contains(this.t.Geometry.Int32(row, 0)) && this.t.Geometry.Key(row, this.c.GeometryGeal) is var geal and >= 0 && geal < this.t.Geals.RowCount)
            {
                orphans.Add(this.Geal((int)geal));
            }
        }

        return orphans;
    }

    private BossGeal Geal(int row)
    {
        var each = new[] { this.t.Geals.StringOrNull(row, this.c.GealCommand), this.t.Geals.StringOrNull(row, this.c.GealMoreCommand) }.Where(text => !string.IsNullOrWhiteSpace(text));
        var objects = this.t.Geals.StringList(row, this.c.GealObjects);
        return new BossGeal(this.t.Geals.String(row, 0), objects.Where(path => !this.monsterIds.Contains(path)).ToList(), Flat(this.t.Geals.StringOrNull(row, this.c.GealScript)),
            Flat(string.Join(" || ", each)), objects.Where(this.monsterIds.Contains).ToList());
    }

    /// <summary>A skill's constant stats with their values and its implicit stats, over its main and additional stat sets.</summary>
    private (Dictionary<long, int> Constants, HashSet<long> Implicits) StatsOf(int row)
    {
        var (constants, implicits) = (new Dictionary<long, int>(), new HashSet<long>());
        var sets = new List<long> { this.t.Skills.Key(row, this.statSet) };
        sets.AddRange(this.t.Skills.KeyList(row, this.c.AdditionalStatSets));
        foreach (var set in sets.Where(set => set >= 0 && set < this.t.StatSets.RowCount).Select(set => (int)set))
        {
            var keys = this.t.StatSets.KeyList(set, this.stats.ConstantStats);
            var values = this.t.StatSets.Int32List(set, this.stats.ConstantValues);
            for (var i = 0; i < keys.Count && i < values.Count; i++)
            {
                constants[keys[i]] = values[i];
            }

            implicits.UnionWith(this.t.StatSets.KeyList(set, this.stats.ImplicitStats));
        }

        return (constants, implicits);
    }

    private static void AddId(List<string> ids, DatTable target, long key)
    {
        if (DatAnchors.IdOf(target, key) is { Length: > 0 } id)
        {
            ids.Add(id);
        }
    }

    private static string Flat(string? text) => (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}
