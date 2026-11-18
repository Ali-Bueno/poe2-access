using Index = LibBundle3.Index;

/// <summary>
///     `game-ids` command: the game's numbers for what the mod reads by name. GameHelper's enums were generated long ago and a
///     row inserted in a table shifts every name after it (Animation: 500 of 1084 wrong in 0.5.5f, Stats: 22541 of 26943), so
///     the names come from the tables after each patch (docs/compatibility.md).
/// </summary>
internal static class GameIdExtractor
{
    private const string Skipped = "game ids";
    private const string FirstAnimation = "Idle";
    private const string FirstStat = "level";

    // A stat's key in memory is its row plus one (Stats row 0 is "level", key 1: read live on a Monk, 2026-11-06). No file
    // holds the key: a Stats row has its Id, flags, a type and a 32-bit hash (dat-kinds, 2026-11-10), and every other table
    // names a stat by its row. Only memory can check the rule: key 1 must read the character's level.
    private const int StatKeyFromRow = 1;

    // The stats the mod reads, by the Id column.
    private static readonly string[] Stats =
    {
        "current_rage", "maximum_rage", "virtual_maximum_rage",
        "culling_strike_threshold_+%", "culling_strike_threshold_+%_vs_rare_or_unique_monsters",
        "strength", "dexterity", "intelligence",
        "fire_damage_resistance_%", "cold_damage_resistance_%", "lightning_damage_resistance_%", "chaos_damage_resistance_%",
        "uncapped_fire_damage_resistance_%", "uncapped_cold_damage_resistance_%", "uncapped_lightning_damage_resistance_%", "uncapped_chaos_damage_resistance_%",
        "is_dead", // the host's dormant-monster check
        "is_in_affliction_fog", // the Delirium fog alarm (LeaguePieces.FogStat)
        "monster_inside_monolith", // a monster an Essence's crystal holds (EssenceWatch logs it beside the rule by place)
        "map_minimap_revealed", // the map modifier that shows the whole layout (RulesetWatch.MinimapRevealed)
        "map_area_level_override", // the area level, which every area's stats hold: names the log's first pair
    };

    /// <summary>Row 0 of Rulesets, known: a table that reads otherwise has moved its Id.</summary>
    private const string FirstRuleset = "StandardRules";

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var animations = DatTable.Open(index, "animation");
        var stats = DatTable.Open(index, "stats");
        if (animations == null || stats == null)
        {
            Say.Out.WriteLine($"Animation or Stats table missing: {Skipped} left out.");
            return 1;
        }

        // Row 0 of each is known: a table that reads otherwise has moved its Id, and an old file beats a wrong one.
        if (animations.String(0, 0) != FirstAnimation || stats.String(0, 0) != FirstStat)
        {
            Say.Out.WriteLine($"Animation or Stats does not start as it did: {Skipped} left out.");
            return 1;
        }

        var lines = new List<string>
        {
            "# The game's numbers for what the mod reads by name (tools/Poe2UiExtract game-ids), regenerated after patches;",
            "# 'animation <row> <name>' from Animation, 'stat <id> <key>' from Stats, 'ruleset <name> <row>' from Rulesets.",
        };
        for (var row = 0; row < animations.RowCount; row++)
        {
            lines.Add($"animation {row} {animations.String(row, 0)}");
        }

        foreach (var stat in Stats)
        {
            var row = ProjectileSpeeds.Find(stats, stat);
            if (row < 0)
            {
                Say.Out.WriteLine($"Stat '{stat}' not found: the mod falls back on the host's number for it.");
                continue;
            }

            lines.Add($"stat {stat} {row + StatKeyFromRow}");
        }

        // The rows of Rulesets are the places of the client's ruleset objects in its session (the plugin's RulesetReader).
        var rulesets = DatTable.Open(index, "rulesets");
        if (rulesets != null && rulesets.String(0, 0) == FirstRuleset)
        {
            for (var row = 0; row < rulesets.RowCount; row++)
            {
                lines.Add($"ruleset {rulesets.String(row, 0)} {row}");
            }
        }
        else
        {
            Say.Out.WriteLine("Rulesets table missing or not starting as it did: its rows are left out, the mod keeps the ones it was built with.");
        }

        File.WriteAllLines(outFile, lines);
        Say.Out.WriteLine($"{animations.RowCount} animations and {Stats.Length} stats written to {outFile}");
        return 0;
    }
}
