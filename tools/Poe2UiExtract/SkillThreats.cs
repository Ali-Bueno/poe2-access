using Index = LibBundle3.Index;

/// <summary>
///     What the game's tables say a monster skill does to the player (docs/research/dodge-timing.md, "Kinds from the
///     data"): the skill's ActiveSkills kind (its Id: `melee`, `geometry_attack`, `teleport`…), its types and the stats
///     of its main stat set. A blow is sure when a damage or projectile stat, or the melee kind, says so; a skill is
///     sure to be harmless when its stats say it deals no damage or its kind is a move, a buff, a summon or a stance;
///     everything else is mute (an `empty_action` the AI fills with triggered skills, a dash whose landing is another
///     skill) and the mod falls back to the animation's name, logging it.
/// </summary>
internal sealed class SkillThreats
{
    public enum Verdict
    {
        Melee,
        Ranged,
        Calm,
        Mute,
    }

    public const string MeleeToken = "melee";
    public const string RangedToken = "ranged";
    public const string CalmToken = "calm";

    /// <summary>Carried between an action's skills only: a mute skill keeps a calm one from deciding, and is never written.</summary>
    public const string MuteToken = "mute";

    private const string Skipped = "skill kinds";

    // ActiveSkills kinds (Id column). The melee kind is every monster's weapon blow; geometry attacks are shaped blows
    // beside the monster; the others never hurt by themselves.
    private const string MeleeKind = "melee";
    private const string PlayerMeleeKindPrefix = "player_melee";
    private const string GeometryAttackKindPrefix = "geometry_attack";

    // Kinds whose "deals no damage" stat only means the payload is another skill's: a mortar's shell, a beam's or a
    // geometry's carrier, a trigger (the Ram Giant's wheel throw, Volatile Dead's detonation).
    private static readonly string[] CarrierKindParts = { "mortar", "projectile", "trigger", "invoke", "beam", "geometry", "channel", "execute" };

    private static readonly string[] CalmKindPrefixes =
    {
        "teleport", "monster_dodge", "dodge_roll", "change_to_stance", "transform", "summon_", "revive", "raise_",
        "necromancer_revive", "marker_warp", "izaro_warp", "add_buff", "unload_ammo", "inspiring_cry", "brute_warcry",
    };

    // ActiveSkillType rows: a charge or leap reaches the player from afar, whatever else it is.
    private static readonly string[] FromAfarTypes = { "Movement", "Travel", "Blink" };

    // Stats of the skill's main set (GrantedEffectStatSets implicit and constant lists).
    private static readonly string[] ProjectileStats = { "base_is_projectile", "monster_projectile_variation" };

    private static readonly string[] DamageStats =
    {
        "is_area_damage", "spell_minimum_base_physical_damage", "spell_minimum_base_fire_damage", "spell_minimum_base_cold_damage",
        "spell_minimum_base_lightning_damage", "spell_minimum_base_chaos_damage", "base_chaos_damage_to_deal_per_minute",
    };

    private static readonly string[] NoDamageStats = { "base_deal_no_damage", "deal_no_damage" };

    private readonly Dictionary<int, (Verdict Verdict, string Reason)> verdicts = new();

    private SkillThreats()
    {
    }

    /// <summary>The verdict of each skill row; every skill mute when a table moved.</summary>
    public static SkillThreats Read(Index index, DatTable skills, IReadOnlyDictionary<int, int> speeds)
    {
        var result = new SkillThreats();
        var stats = DatTable.Open(index, "stats");
        var statSets = DatTable.Open(index, "grantedeffectstatsets");
        var activeSkills = DatTable.Open(index, "activeskills");
        var skillTypes = DatTable.Open(index, "activeskilltype");
        if (!HitTimeExtractor.Sized(skills, ProjectileSpeeds.SkillRowSize, "GrantedEffects", Skipped) ||
            !HitTimeExtractor.Sized(statSets, ProjectileSpeeds.StatSetRowSize, "GrantedEffectStatSets", Skipped) ||
            !HitTimeExtractor.Sized(activeSkills, ProjectileSpeeds.ActiveSkillRowSize, "ActiveSkills", Skipped) || stats == null || skillTypes == null)
        {
            return result;
        }

        var projectile = Rows(stats, ProjectileStats);
        var damage = Rows(stats, DamageStats);
        var noDamage = Rows(stats, NoDamageStats);
        var fromAfar = Rows(skillTypes, FromAfarTypes);
        if (projectile.Count == 0 || damage.Count == 0 || noDamage.Count == 0 || fromAfar.Count == 0)
        {
            Console.WriteLine($"Stats or ActiveSkillType rows for {Skipped} not found: every skill left mute.");
            return result;
        }

        for (var skill = 0; skill < skills.RowCount; skill++)
        {
            var active = skills.Key(skill, ProjectileSpeeds.SkillActiveSkillOffset);
            if (active < 0 || active >= activeSkills!.RowCount)
            {
                continue;
            }

            var kind = activeSkills.String((int)active, 0);
            var types = activeSkills.KeyList((int)active, ProjectileSpeeds.ActiveSkillTypesOffset);
            var statSet = skills.Key(skill, ProjectileSpeeds.SkillStatSetOffset);
            var set = statSet >= 0 && statSet < statSets!.RowCount
                ? statSets.KeyList((int)statSet, SkillFlags.ImplicitStatsOffset).Concat(statSets.KeyList((int)statSet, ProjectileSpeeds.ConstantStatsOffset)).ToHashSet()
                : new HashSet<long>();
            result.verdicts[skill] = Judge(kind, types.Any(fromAfar.Contains), set, projectile, damage, noDamage, speeds.ContainsKey(skill));
        }

        return result;
    }

    /// <summary>A skill's verdict and the datum behind it; mute for a skill the tables do not describe.</summary>
    public (Verdict Verdict, string Reason) Of(int skill) => this.verdicts.TryGetValue(skill, out var found) ? found : (Verdict.Mute, "no skill row");

    /// <summary>The verdict of an action its skills play together: a hit from afar wins, then a blow, then calm; mute only when every skill is.</summary>
    public static Verdict Combine(IEnumerable<Verdict> verdicts)
    {
        var all = verdicts.ToList();
        return all.Contains(Verdict.Ranged) ? Verdict.Ranged
            : all.Contains(Verdict.Melee) ? Verdict.Melee
            : all.Count > 0 && all.All(verdict => verdict == Verdict.Calm) ? Verdict.Calm
            : Verdict.Mute;
    }

    /// <summary>The flag token written to hit-times.txt; null for a mute action.</summary>
    public static string? Token(Verdict verdict) => verdict == Verdict.Mute ? null : Name(verdict);

    /// <summary>Every verdict's token, the mute one included, for carrying between skills.</summary>
    public static string Name(Verdict verdict) => verdict switch
    {
        Verdict.Melee => MeleeToken,
        Verdict.Ranged => RangedToken,
        Verdict.Calm => CalmToken,
        _ => MuteToken,
    };

    /// <summary>The verdict a token stands for.</summary>
    public static Verdict Parse(string token) => token switch
    {
        MeleeToken => Verdict.Melee,
        RangedToken => Verdict.Ranged,
        CalmToken => Verdict.Calm,
        _ => Verdict.Mute,
    };

    private static (Verdict, string) Judge(string kind, bool fromAfar, HashSet<long> set, HashSet<long> projectile, HashSet<long> damage, HashSet<long> noDamage, bool hasSpeed)
    {
        var melee = kind == MeleeKind || kind.StartsWith(PlayerMeleeKindPrefix, StringComparison.Ordinal);
        if (hasSpeed || set.Overlaps(projectile))
        {
            return (Verdict.Ranged, "projectile");
        }

        if (fromAfar && (melee || set.Overlaps(damage)))
        {
            return (Verdict.Ranged, $"{kind}: reaches from afar");
        }

        if (melee)
        {
            return (Verdict.Melee, kind);
        }

        if (set.Overlaps(damage))
        {
            return kind.StartsWith(GeometryAttackKindPrefix, StringComparison.Ordinal) ? (Verdict.Melee, kind) : (Verdict.Ranged, $"{kind}: damage stat");
        }

        if (set.Overlaps(noDamage) && !CarrierKindParts.Any(part => kind.Contains(part, StringComparison.Ordinal)))
        {
            return (Verdict.Calm, $"{kind}: deals no damage");
        }

        return CalmKindPrefixes.Any(prefix => kind.StartsWith(prefix, StringComparison.Ordinal)) ? (Verdict.Calm, kind) : (Verdict.Mute, kind);
    }

    private static HashSet<long> Rows(DatTable table, IEnumerable<string> ids) =>
        ids.Select(id => ProjectileSpeeds.Find(table, id)).Where(row => row >= 0).ToHashSet();
}
