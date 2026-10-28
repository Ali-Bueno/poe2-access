using Index = LibBundle3.Index;

/// <summary>
///     Monster skills the player cannot block, and so cannot parry (KeywordPopups "Block": boss skills shown with a red glow
///     and an audio cue during their wind-up), or cannot parry: flag stats in the skill's main stat set
///     (docs/research/parry-and-stun-bar.md).
/// </summary>
internal static class SkillFlags
{
    public const string NoBlock = "noblock";
    public const string NoParry = "noparry";
    private const string Skipped = "unblockable skills";

    /// <summary>GrantedEffectStatSets: a stat key list holding the skill's flag stats (66 sets carry one of these; scan 2026-10-28).</summary>
    private const int FlagStatsOffset = 0x18;

    private static readonly Dictionary<string, string> FlagOfStat = new(StringComparer.Ordinal)
    {
        ["base_cannot_be_blocked"] = NoBlock,
        ["skill_cannot_be_blocked"] = NoBlock,
        ["base_cannot_be_parried"] = NoParry,
        ["base_skill_cannot_be_parried"] = NoParry,
    };

    /// <summary>Skill row → its flags; empty when a table moved.</summary>
    public static Dictionary<int, SortedSet<string>> Read(Index index, DatTable skills)
    {
        var flags = new Dictionary<int, SortedSet<string>>();
        var stats = DatTable.Open(index, "stats");
        var statSets = DatTable.Open(index, "grantedeffectstatsets");
        if (!HitTimeExtractor.Sized(skills, ProjectileSpeeds.SkillRowSize, "GrantedEffects", Skipped) ||
            !HitTimeExtractor.Sized(statSets, ProjectileSpeeds.StatSetRowSize, "GrantedEffectStatSets", Skipped) || stats == null)
        {
            return flags;
        }

        var flagOfRow = FlagOfStat.Select(pair => (Row: ProjectileSpeeds.Find(stats, pair.Key), Flag: pair.Value)).Where(pair => pair.Row >= 0).ToDictionary(pair => pair.Row, pair => pair.Flag);
        if (flagOfRow.Count == 0)
        {
            Console.WriteLine($"Stats {string.Join(", ", FlagOfStat.Keys)} not found: {Skipped} left out.");
            return flags;
        }

        for (var skill = 0; skill < skills.RowCount; skill++)
        {
            var statSet = skills.Key(skill, ProjectileSpeeds.SkillStatSetOffset);
            if (statSet < 0 || statSet >= statSets!.RowCount)
            {
                continue;
            }

            var found = statSets.KeyList((int)statSet, FlagStatsOffset).Where(flagOfRow.ContainsKey).Select(key => flagOfRow[key]).ToList();
            if (found.Count > 0)
            {
                flags[skill] = new SortedSet<string>(found, StringComparer.Ordinal);
            }
        }

        return flags;
    }
}
