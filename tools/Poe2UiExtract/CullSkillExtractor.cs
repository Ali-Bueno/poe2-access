using Index = LibBundle3.Index;

/// <summary>
///     `cull-skills` command: the granted effects that deal Culling Strike, one id a line, for the mod's culling cue. A
///     skill culls when its main stat set (GrantedEffects → GrantedEffectStatSets) lists kill_enemy_on_hit_if_under_10%_life
///     among its implicit stats, the stat the game's stat descriptions word as "Culling Strike".
/// </summary>
internal static class CullSkillExtractor
{
    private const string CullStat = "kill_enemy_on_hit_if_under_10%_life";
    private const string Skipped = "cull skills";

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        if (ProjectileSpeeds.Columns(index) is not var (t, c))
        {
            return 1;
        }

        var (skills, statSets) = (t.Skills, t.StatSets);
        var cull = ProjectileSpeeds.Find(t.Stats, CullStat);
        if (cull < 0)
        {
            Say.Out.WriteLine($"Stat '{CullStat}' not found: {Skipped} left out.");
            return 1;
        }

        var ids = new SortedSet<string>(StringComparer.Ordinal);
        for (var skill = 0; skill < skills.RowCount; skill++)
        {
            var statSet = skills.Key(skill, c.SkillStatSet);
            if (statSet >= 0 && statSet < statSets.RowCount && statSets.KeyList((int)statSet, c.ImplicitStats).Contains(cull))
            {
                ids.Add(skills.String(skill, 0));
            }
        }

        if (ids.Count == 0)
        {
            Say.Out.WriteLine($"No granted effect lists '{CullStat}': the stat set columns moved; find them again (dat-rows, dat-strings).");
            return 1;
        }

        var header = new[]
        {
            "# Granted effects that deal Culling Strike (tools/Poe2UiExtract cull-skills), regenerated after patches;",
            "# one GrantedEffects id a line, as the player's Actor lists it.",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(ids));
        Say.Out.WriteLine($"Wrote {outFile}: {ids.Count} cull skills.");
        return 0;
    }
}
