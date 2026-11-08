using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns of the skill tables that the projectile speeds, the skill verdicts, the unblockable flags and the cull
///     skills read, each found by what it holds near where it was measured (<see cref="DatColumnSearch"/>) and shared by
///     the four extractors so they agree. A column not found leaves <see cref="Find"/> null.
/// </summary>
internal sealed class ProjectileColumns
{
    private const string VariationStat = "monster_projectile_variation";
    private const string BannerSkill = "war_banner";
    private const string BannerType = "Banner";

    // Flag stats of a stat set's implicit list; at least one is listed by some set while the game has them.
    private static readonly string[] FlagStats =
    {
        "kill_enemy_on_hit_if_under_10%_life", "base_cannot_be_blocked", "skill_cannot_be_blocked", "base_cannot_be_parried", "base_skill_cannot_be_parried",
    };

    // Offsets measured from the community dat-schema (2026-10-25), where each search starts.
    // GrantedEffects: ActiveSkill, StatSet.
    internal const int SkillActiveSkillOffset = 0x4F;
    internal const int SkillStatSetOffset = 0xA8;

    // GrantedEffectStatSets: ImplicitStats, ConstantStats, ConstantStatsValues.
    internal const int ImplicitStatsOffset = 0x18;
    internal const int ConstantStatsOffset = 0x28;
    internal const int ConstantValuesOffset = 0x38;

    // ActiveSkills: ActiveSkillTypes (ActiveSkillType rows, +0x00 Id).
    internal const int ActiveSkillTypesOffset = 0x40;

    // MonsterProjectileSpell and MonsterProjectileAttack: Id (i32), Projectile.
    internal const int VariationIdOffset = 0x00;
    internal const int VariationProjectileOffset = 0x04;

    // Projectiles: ProjectileSpeed (i32), UseTargetLocation (bool).
    internal const int ProjectileSpeedOffset = 0x38;
    internal const int TargetLocationOffset = 0x3C;

    /// <summary>A projectile's speed in world units a second stays under this (the fastest are a few thousand).</summary>
    private const int MaxSpeed = 100_000;

    /// <summary>Of the projectiles the variation rows reach, this share has a speed in range.</summary>
    private const double SpeedShare = 0.9;

    public int SkillActive, SkillStatSet;
    public int ImplicitStats, ConstantStats, ConstantValues;
    public int ActiveTypes;
    public int SpellId, SpellProjectile, AttackId, AttackProjectile;
    public int ProjectileSpeed, TargetLocation;

    /// <summary>The tables the columns are in.</summary>
    public sealed record Tables(DatTable Stats, DatTable Skills, DatTable StatSets, DatTable ActiveSkills, DatTable SkillTypes, DatTable Spells, DatTable Attacks, DatTable Projectiles);

    public static ProjectileColumns? Find(Tables t)
    {
        var search = new DatColumnSearch();
        var c = new ProjectileColumns();
        var variation = StatRow(t.Stats, VariationStat);
        var flags = FlagStats.Select(id => StatRow(t.Stats, id)).Where(row => row >= 0).ToHashSet();

        c.SkillActive = search.Key(t.Skills, "GrantedEffects.ActiveSkill", SkillActiveSkillOffset, t.ActiveSkills);
        c.SkillStatSet = search.Key(t.Skills, "GrantedEffects.StatSet", SkillStatSetOffset, t.StatSets, c.SkillActive);

        c.ImplicitStats = search.Find(t.StatSets, "GrantedEffectStatSets.ImplicitStats", ImplicitStatsOffset, offset =>
            DatColumnSearch.IsKeyList(t.StatSets, offset, t.Stats.RowCount) && (flags.Count == 0 || ListsAny(t.StatSets, offset, flags)));
        c.ConstantStats = search.Find(t.StatSets, "GrantedEffectStatSets.ConstantStats", ConstantStatsOffset, offset =>
            DatColumnSearch.IsKeyList(t.StatSets, offset, t.Stats.RowCount) && (variation < 0 || ListsAny(t.StatSets, offset, new HashSet<long> { variation })), c.ImplicitStats);
        c.ConstantValues = search.Find(t.StatSets, "GrantedEffectStatSets.ConstantStatsValues", ConstantValuesOffset, offset =>
            ValuesMatchStats(t.StatSets, offset, c.ConstantStats), c.ConstantStats);

        var banner = DatColumnSearch.RowOf(t.ActiveSkills, BannerSkill);
        c.ActiveTypes = search.KeyList(t.ActiveSkills, "ActiveSkills.ActiveSkillTypes", ActiveSkillTypesOffset, t.SkillTypes,
            known: (banner, list => list.Any(type => IdOf(t.SkillTypes, type) == BannerType)));

        // The variation numbers the skills hand to the monster projectile tables: an Id column holds some of them.
        var variations = c.ConstantStats < 0 || c.ConstantValues < 0 || variation < 0 ? new HashSet<int>() : VariationValues(t.StatSets, c.ConstantStats, c.ConstantValues, variation);
        c.SpellId = IdColumn(search, t.Spells, "MonsterProjectileSpell.Id", variations);
        c.SpellProjectile = search.Key(t.Spells, "MonsterProjectileSpell.Projectile", VariationProjectileOffset, t.Projectiles, c.SpellId);
        c.AttackId = IdColumn(search, t.Attacks, "MonsterProjectileAttack.Id", variations);
        c.AttackProjectile = search.Key(t.Attacks, "MonsterProjectileAttack.Projectile", VariationProjectileOffset, t.Projectiles, c.AttackId);

        var reached = ReachedProjectiles(t, c);
        c.ProjectileSpeed = search.Find(t.Projectiles, "Projectiles.ProjectileSpeed", ProjectileSpeedOffset, offset =>
            offset + sizeof(int) <= t.Projectiles.RowSize && (reached.Count == 0 ||
                reached.Count(row => t.Projectiles.Int32(row, offset) is > 0 and <= MaxSpeed) >= reached.Count * SpeedShare));
        c.TargetLocation = search.Byte(t.Projectiles, "Projectiles.UseTargetLocation", TargetLocationOffset, value => value is 0 or 1, c.ProjectileSpeed);
        return search.Report("skill tables") ? c : null;
    }

    private static long StatRow(DatTable stats, string id) => DatColumnSearch.RowOf(stats, id);

    private static string IdOf(DatTable table, long row) =>
        row >= 0 && row < table.RowCount ? table.StringOrNull((int)row, 0) ?? string.Empty : string.Empty;

    private static bool ListsAny(DatTable table, int offset, HashSet<long> stats) =>
        Enumerable.Range(0, table.RowCount).Any(row => table.KeyList(row, offset).Any(stats.Contains));

    /// <summary>The values list has an item for each constant stat of every set that lists any.</summary>
    private static bool ValuesMatchStats(DatTable statSets, int offset, int constants)
    {
        var filled = 0;
        for (var row = 0; row < statSets.RowCount; row++)
        {
            var count = statSets.KeyList(row, constants).Count;
            if (statSets.Int32List(row, offset).Count != count)
            {
                return false;
            }

            filled += count > 0 ? 1 : 0;
        }

        return filled > 0;
    }

    private static HashSet<int> VariationValues(DatTable statSets, int constants, int values, long variation)
    {
        var found = new HashSet<int>();
        for (var row = 0; row < statSets.RowCount; row++)
        {
            var at = statSets.KeyList(row, constants).ToList().IndexOf(variation);
            var list = statSets.Int32List(row, values);
            if (at >= 0 && at < list.Count)
            {
                found.Add(list[at]);
            }
        }

        return found;
    }

    /// <summary>The Id column of a monster projectile table: non-negative and holding some of the variation numbers the skills use.</summary>
    private static int IdColumn(DatColumnSearch search, DatTable table, string name, HashSet<int> variations) =>
        search.Find(table, name, VariationIdOffset, offset => offset + sizeof(int) <= table.RowSize &&
            Enumerable.Range(0, table.RowCount).All(row => table.Int32(row, offset) >= 0) &&
            (variations.Count == 0 || Enumerable.Range(0, table.RowCount).Any(row => variations.Contains(table.Int32(row, offset)))));

    private static HashSet<int> ReachedProjectiles(Tables t, ProjectileColumns c)
    {
        var reached = new HashSet<int>();
        foreach (var (table, column) in new[] { (t.Spells, c.SpellProjectile), (t.Attacks, c.AttackProjectile) })
        {
            for (var row = 0; column >= 0 && row < table.RowCount; row++)
            {
                var key = table.Key(row, column);
                if (key >= 0 && key < t.Projectiles.RowCount)
                {
                    reached.Add((int)key);
                }
            }
        }

        return reached;
    }
}
