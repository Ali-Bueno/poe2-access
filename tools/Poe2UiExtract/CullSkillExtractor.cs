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

    // GrantedEffectStatSets: ImplicitStats, a stat key list (6 sets list the stat; scan of every key list column, 2026-10-29).
    private const int ImplicitStatsOffset = 0x18;

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var skills = DatTable.Open(index, "grantedeffects");
        var statSets = DatTable.Open(index, "grantedeffectstatsets");
        var stats = DatTable.Open(index, "stats");
        if (!HitTimeExtractor.Sized(skills, ProjectileSpeeds.SkillRowSize, "GrantedEffects", Skipped) ||
            !HitTimeExtractor.Sized(statSets, ProjectileSpeeds.StatSetRowSize, "GrantedEffectStatSets", Skipped) || stats == null)
        {
            return 1;
        }

        var cull = ProjectileSpeeds.Find(stats, CullStat);
        if (cull < 0)
        {
            Console.WriteLine($"Stat '{CullStat}' not found: {Skipped} left out.");
            return 1;
        }

        var ids = new SortedSet<string>(StringComparer.Ordinal);
        for (var skill = 0; skill < skills!.RowCount; skill++)
        {
            var statSet = skills.Key(skill, ProjectileSpeeds.SkillStatSetOffset);
            if (statSet >= 0 && statSet < statSets!.RowCount && statSets.KeyList((int)statSet, ImplicitStatsOffset).Contains(cull))
            {
                ids.Add(skills.String(skill, 0));
            }
        }

        if (ids.Count == 0)
        {
            Console.WriteLine($"No granted effect lists '{CullStat}': the stat set columns moved; find them again (dat-rows, dat-strings).");
            return 1;
        }

        var header = new[]
        {
            "# Granted effects that deal Culling Strike (tools/Poe2UiExtract cull-skills), regenerated after patches;",
            "# one GrantedEffects id a line, as the player's Actor lists it.",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(ids));
        Console.WriteLine($"Wrote {outFile}: {ids.Count} cull skills.");
        return 0;
    }
}
