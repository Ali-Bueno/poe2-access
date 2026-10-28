using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `experience` command: the total experience each character level starts at, from ExperienceLevels.datc64, as a
///     JSON array (index 0 = level 1). Columns are found by content: the level is the 4-byte column counting the rows
///     from 1, the experience the other one rising from 0 (0.5.5e: +0x8 and +0xC, unsigned; level 100 at 4250334444).
/// </summary>
internal static class ExperienceExtractor
{
    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, "experiencelevels") is not { RowCount: > 1 } table)
        {
            Console.WriteLine("experiencelevels: table missing");
            return 1;
        }

        var offsets = Enumerable.Range(0, table.RowSize / 4).Select(column => column * 4).ToList();
        var level = offsets.FirstOrDefault(offset => Enumerable.Range(0, table.RowCount).All(row => table.Int32(row, offset) == row + 1), -1);
        var experience = offsets.Where(offset => offset != level).FirstOrDefault(offset => IsRisingFromZero(table, offset), -1);
        if (level < 0 || experience < 0)
        {
            Console.WriteLine($"experiencelevels: columns not recognised (level {level}, experience {experience})");
            return 1;
        }

        var totals = Enumerable.Range(0, table.RowCount).Select(row => (uint)table.Int32(row, experience)).ToArray();
        File.WriteAllText(outFile, JsonSerializer.Serialize(totals));
        Console.WriteLine($"experiencelevels: {totals.Length} levels (level +0x{level:X}, experience +0x{experience:X}), last {totals[^1]}");
        return 0;
    }

    private static bool IsRisingFromZero(DatTable table, int offset)
    {
        uint previous = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var value = (uint)table.Int32(row, offset);
            if ((row == 0 && value != 0) || (row > 0 && value <= previous))
            {
                return false;
            }

            previous = value;
        }

        return true;
    }
}
