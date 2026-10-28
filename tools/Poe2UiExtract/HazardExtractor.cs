using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `hazards` command: the buffs a player gets from standing in harmful ground or an enemy's damaging zone, one id a line,
///     for the mod's hazard alarm (docs/research/parry-and-stun-bar.md): the buff of every GroundEffectTypes row but the
///     helpful ones, and every BuffDefinitions row whose English description says the player takes damage while standing
///     in something (boss arenas: "You are taking Fire Damage over Time while standing in the Flame").
/// </summary>
internal static class HazardExtractor
{
    // GroundEffectTypes: the BuffDefinitions row a creature standing in the ground gets (IgnitedGround → ignited_ground;
    // a scan of the row's keys, 2026-10-28).
    private const int TypeRowSize = 0x40;
    private const int TypeBuffOffset = 0x20;

    // BuffDefinitions: Description (dat-strings, 2026-10-28).
    private const int BuffRowSize = 0x15F;
    private const int BuffDescriptionOffset = 0x08;

    /// <summary>Ground types that help the one standing in them, or whose buff is a generic one other effects share (Smoke).</summary>
    private static readonly HashSet<string> NotHazards = new(StringComparer.Ordinal)
    {
        "Consecration", "Haste", "Blizzard", "Oasis", "FeatheredGround", "Leyline", "Smoke", "Unused",
    };

    private static readonly Regex Standing = new(@"standing (in|on)|in the (beam|aura|cloud)", RegexOptions.IgnoreCase);
    private static readonly Regex Taking = new(@"taking .*damage", RegexOptions.IgnoreCase);

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var types = DatTable.Open(index, "groundeffecttypes");
        var buffs = DatTable.Open(index, "buffdefinitions");
        if (!HitTimeExtractor.Sized(types, TypeRowSize, "GroundEffectTypes", "hazards") || !HitTimeExtractor.Sized(buffs, BuffRowSize, "BuffDefinitions", "hazards"))
        {
            return 1;
        }

        var ids = new SortedSet<string>(StringComparer.Ordinal);
        for (var row = 0; row < types!.RowCount; row++)
        {
            var buff = types.Key(row, TypeBuffOffset);
            if (!NotHazards.Contains(types.String(row, 0)) && buff >= 0 && buff < buffs!.RowCount)
            {
                ids.Add(buffs.String((int)buff, 0));
            }
        }

        for (var row = 0; row < buffs!.RowCount; row++)
        {
            if (buffs.StringOrNull(row, BuffDescriptionOffset) is { } description && Standing.IsMatch(description) && Taking.IsMatch(description))
            {
                ids.Add(buffs.String(row, 0));
            }
        }

        var header = new[]
        {
            "# Buffs a player gets from standing in harmful ground or an enemy's damaging zone (tools/Poe2UiExtract hazards),",
            "# regenerated after patches; a buff id here, or one that starts with it and \"_\", sounds the hazard alarm.",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(ids));
        Console.WriteLine($"Wrote {outFile}: {ids.Count} hazard buffs.");
        return 0;
    }
}
