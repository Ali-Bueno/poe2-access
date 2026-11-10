using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `experience` command: the total experience each character level starts at, from ExperienceLevels.datc64, as a
///     JSON array (index 0 = level 1). Columns are found by content: the level is the 4-byte column counting the rows
///     from 1, the experience the other one rising from 0 (0.5.5e: +0x8 and +0xC, unsigned; level 100 at 4250334444).
///     Two known levels tell it from any other rising column; with a new curve the only rising column is read, and said.
/// </summary>
internal static class ExperienceExtractor
{
    /// <summary>Rows whose total is known (0.5.x, 2026-11-10): level 2 starts at 525, level 100 at 4250334444.</summary>
    private static readonly (int Row, uint Total)[] Known = { (1, 525), (99, 4250334444) };

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, "experiencelevels") is not { RowCount: > 1 } table)
        {
            Say.Out.WriteLine("experiencelevels: table missing");
            return 1;
        }

        var offsets = Enumerable.Range(0, table.RowSize / 4).Select(column => column * 4).ToList();
        int level, experience;
        using (DatTable.Probe())
        {
            level = offsets.FirstOrDefault(offset => Enumerable.Range(0, table.RowCount).All(row => table.Int32(row, offset) == row + 1), -1);
            var rising = offsets.Where(offset => offset != level && IsRisingFromZero(table, offset)).ToList();
            var known = rising.Where(offset => Known.All(check => check.Row < table.RowCount && (uint)table.Int32(check.Row, offset) == check.Total)).ToList();
            experience = known.Count > 0 ? known[0] : rising.Count == 1 ? rising[0] : -1;
            if (known.Count == 0 && rising.Count > 0)
            {
                Say.Out.WriteLine(rising.Count == 1
                    ? "experiencelevels: the known levels no longer start where they did (a new experience curve); the one column rising from zero is read"
                    : $"experiencelevels: {rising.Count} columns rise from zero and none holds the known levels: the experience column cannot be told");
            }
        }

        if (level < 0 || experience < 0)
        {
            Say.Out.WriteLine($"experiencelevels: columns not recognised (level {level}, experience {experience})");
            return 1;
        }

        var totals = Enumerable.Range(0, table.RowCount).Select(row => (uint)table.Int32(row, experience)).ToArray();
        File.WriteAllText(outFile, JsonSerializer.Serialize(totals));
        Say.Out.WriteLine($"experiencelevels: {totals.Length} levels (level +0x{level:X}, experience +0x{experience:X}), last {totals[^1]}");
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
