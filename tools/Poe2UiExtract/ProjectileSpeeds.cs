using Index = LibBundle3.Index;

/// <summary>
///     How fast each monster skill's projectile flies, in world units a second (docs/research/dodge-timing.md): the skill's
///     main stat set gives monster_projectile_variation, the Id of a MonsterProjectileSpell row (a spell) or a
///     MonsterProjectileAttack row (an attack), whose Projectiles row holds ProjectileSpeed. Mortars (UseTargetLocation)
///     are left out: their flight is not the distance at that speed.
/// </summary>
internal static class ProjectileSpeeds
{
    private const string VariationStat = "monster_projectile_variation";
    private const string SpellType = "Spell";
    private const string AttackType = "Attack";
    private const string Skipped = "projectile speeds";

    // Offsets and row sizes from the community dat-schema (2026-10-25), every row size matching this install.
    // GrantedEffects: ActiveSkill, StatSet.
    internal const int SkillRowSize = 0x129;
    private const int SkillActiveSkillOffset = 0x4F;
    internal const int SkillStatSetOffset = 0xA8;

    // GrantedEffectStatSets: ConstantStats, ConstantStatsValues.
    internal const int StatSetRowSize = 0x65;
    private const int ConstantStatsOffset = 0x28;
    private const int ConstantValuesOffset = 0x38;

    // ActiveSkills: ActiveSkillTypes (ActiveSkillType rows, +0x00 Id).
    private const int ActiveSkillRowSize = 0x152;
    private const int ActiveSkillTypesOffset = 0x40;

    // MonsterProjectileSpell and MonsterProjectileAttack: Id (i32), Projectile.
    private const int SpellVariationRowSize = 0x2A;
    private const int AttackVariationRowSize = 0x1B;
    private const int VariationIdOffset = 0x00;
    private const int VariationProjectileOffset = 0x04;

    // Projectiles: ProjectileSpeed (i32), UseTargetLocation (bool).
    private const int ProjectileRowSize = 0x170;
    private const int ProjectileSpeedOffset = 0x38;
    private const int TargetLocationOffset = 0x3C;

    /// <summary>Skill row → its projectile's speed; empty when a table moved.</summary>
    public static Dictionary<int, int> Read(Index index, DatTable skills)
    {
        var speeds = new Dictionary<int, int>();
        var stats = DatTable.Open(index, "stats");
        var statSets = DatTable.Open(index, "grantedeffectstatsets");
        var activeSkills = DatTable.Open(index, "activeskills");
        var skillTypes = DatTable.Open(index, "activeskilltype");
        var spells = DatTable.Open(index, "monsterprojectilespell");
        var attacks = DatTable.Open(index, "monsterprojectileattack");
        var projectiles = DatTable.Open(index, "projectiles");
        if (!HitTimeExtractor.Sized(skills, SkillRowSize, "GrantedEffects", Skipped) || !HitTimeExtractor.Sized(statSets, StatSetRowSize, "GrantedEffectStatSets", Skipped) ||
            !HitTimeExtractor.Sized(activeSkills, ActiveSkillRowSize, "ActiveSkills", Skipped) ||
            !HitTimeExtractor.Sized(spells, SpellVariationRowSize, "MonsterProjectileSpell", Skipped) ||
            !HitTimeExtractor.Sized(attacks, AttackVariationRowSize, "MonsterProjectileAttack", Skipped) ||
            !HitTimeExtractor.Sized(projectiles, ProjectileRowSize, "Projectiles", Skipped) || stats == null || skillTypes == null)
        {
            return speeds;
        }

        var variation = Find(stats, VariationStat);
        var spellType = Find(skillTypes, SpellType);
        var attackType = Find(skillTypes, AttackType);
        if (variation < 0 || spellType < 0 || attackType < 0)
        {
            Console.WriteLine($"Stats '{VariationStat}' or ActiveSkillType {SpellType}/{AttackType} not found: {Skipped} left out.");
            return speeds;
        }

        var spellRows = ById(spells!);
        var attackRows = ById(attacks!);
        for (var skill = 0; skill < skills.RowCount; skill++)
        {
            var statSet = skills.Key(skill, SkillStatSetOffset);
            var active = skills.Key(skill, SkillActiveSkillOffset);
            if (statSet < 0 || statSet >= statSets!.RowCount || active < 0 || active >= activeSkills!.RowCount)
            {
                continue;
            }

            var at = statSets.KeyList((int)statSet, ConstantStatsOffset).ToList().IndexOf(variation);
            var values = statSets.Int32List((int)statSet, ConstantValuesOffset);
            if (at < 0 || at >= values.Count)
            {
                continue;
            }

            var types = activeSkills.KeyList((int)active, ActiveSkillTypesOffset);
            var spell = types.Contains(spellType);
            if ((!spell && !types.Contains(attackType)) || !(spell ? spellRows : attackRows).TryGetValue(values[at], out var variationRow))
            {
                continue;
            }

            var projectile = (spell ? spells! : attacks!).Key(variationRow, VariationProjectileOffset);
            if (projectile >= 0 && projectile < projectiles!.RowCount && !projectiles.Bool((int)projectile, TargetLocationOffset) &&
                projectiles.Int32((int)projectile, ProjectileSpeedOffset) is > 0 and var speed)
            {
                speeds[skill] = speed;
            }
        }

        return speeds;
    }

    internal static long Find(DatTable table, string id)
    {
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.String(row, 0) == id)
            {
                return row;
            }
        }

        return -1;
    }

    private static Dictionary<int, int> ById(DatTable table)
    {
        var rows = new Dictionary<int, int>();
        for (var row = 0; row < table.RowCount; row++)
        {
            rows.TryAdd(table.Int32(row, VariationIdOffset), row);
        }

        return rows;
    }
}
