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

    private static (Index Index, ProjectileColumns.Tables Tables, ProjectileColumns Columns)? shared;
    private static Index? failedFor;
    private static readonly object Gate = new();

    /// <summary>Lets go of the index the tables were found in, once a run is over.</summary>
    internal static void Release()
    {
        lock (Gate)
        {
            shared = null;
            failedFor = null;
        }
    }

    /// <summary>The skill tables of this index and their columns, found once by content and shared by the extractors that read them; null (said once) when a table or column is missing.</summary>
    internal static (ProjectileColumns.Tables Tables, ProjectileColumns Columns)? Columns(Index index)
    {
        lock (Gate)
        {
            return FindColumns(index);
        }
    }

    private static (ProjectileColumns.Tables Tables, ProjectileColumns Columns)? FindColumns(Index index)
    {
        if (shared is { } cached && cached.Index == index)
        {
            return (cached.Tables, cached.Columns);
        }

        if (failedFor == index)
        {
            return null;
        }

        failedFor = index;
        var opened = new[] { "stats", "grantedeffects", "grantedeffectstatsets", "activeskills", "activeskilltype", "monsterprojectilespell", "monsterprojectileattack", "projectiles" }.Select(name => DatTable.Open(index, name)).ToArray();
        if (opened.Any(table => table == null))
        {
            Say.Out.WriteLine($"A skill table is missing: {Skipped} and the skill kinds left out.");
            return null;
        }

        var tables = new ProjectileColumns.Tables(opened[0]!, opened[1]!, opened[2]!, opened[3]!, opened[4]!, opened[5]!, opened[6]!, opened[7]!);
        if (ProjectileColumns.Find(tables) is not { } columns)
        {
            return null;
        }

        failedFor = null;
        shared = (index, tables, columns);
        return (tables, columns);
    }

    /// <summary>Skill row → its projectile's speed; empty when a table or column was not found.</summary>
    public static Dictionary<int, int> Read(Index index, DatTable skills)
    {
        var speeds = new Dictionary<int, int>();
        if (Columns(index) is not var (t, c))
        {
            return speeds;
        }

        var variation = Find(t.Stats, VariationStat);
        var spellType = Find(t.SkillTypes, SpellType);
        var attackType = Find(t.SkillTypes, AttackType);
        if (variation < 0 || spellType < 0 || attackType < 0)
        {
            Say.Out.WriteLine($"Stats '{VariationStat}' or ActiveSkillType {SpellType}/{AttackType} not found: {Skipped} left out.");
            return speeds;
        }

        var spellRows = ById(t.Spells, c.SpellId);
        var attackRows = ById(t.Attacks, c.AttackId);
        for (var skill = 0; skill < skills.RowCount; skill++)
        {
            var statSet = skills.Key(skill, c.SkillStatSet);
            var active = skills.Key(skill, c.SkillActive);
            if (statSet < 0 || statSet >= t.StatSets.RowCount || active < 0 || active >= t.ActiveSkills.RowCount)
            {
                continue;
            }

            var at = t.StatSets.KeyList((int)statSet, c.ConstantStats).ToList().IndexOf(variation);
            var values = t.StatSets.Int32List((int)statSet, c.ConstantValues);
            if (at < 0 || at >= values.Count)
            {
                continue;
            }

            var types = t.ActiveSkills.KeyList((int)active, c.ActiveTypes);
            var spell = types.Contains(spellType);
            if ((!spell && !types.Contains(attackType)) || !(spell ? spellRows : attackRows).TryGetValue(values[at], out var variationRow))
            {
                continue;
            }

            var projectile = (spell ? t.Spells : t.Attacks).Key(variationRow, spell ? c.SpellProjectile : c.AttackProjectile);
            if (projectile >= 0 && projectile < t.Projectiles.RowCount && !t.Projectiles.Bool((int)projectile, c.TargetLocation) &&
                t.Projectiles.Int32((int)projectile, c.ProjectileSpeed) is > 0 and var speed)
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

    private static Dictionary<int, int> ById(DatTable table, int idColumn)
    {
        var rows = new Dictionary<int, int>();
        for (var row = 0; row < table.RowCount; row++)
        {
            rows.TryAdd(table.Int32(row, idColumn), row);
        }

        return rows;
    }
}
