using Index = LibBundle3.Index;

/// <summary>
///     `action-types` command: the Id of every ActionTypes row (the game's action names, the ones an .act maps to
///     animations), one a line, so the mod reads an action by name and not by a row number a patch can shift. Its own
///     command because the batch makes one output file per command.
/// </summary>
internal static class ActionTypeExtractor
{
    // ActionTypes: 0xE-byte rows, Id (string) at +0 (dat-strings, 2026-11-08).
    private const int RowSize = 0xE;

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var table = DatTable.Open(index, "actiontypes");
        if (!HitTimeExtractor.Sized(table, RowSize, "ActionTypes", "action-types"))
        {
            return 1;
        }

        var ids = Enumerable.Range(0, table!.RowCount).Select(row => table.String(row, 0)).Where(id => id.Length > 0).ToList();
        var header = new[] { "# The Id of every ActionTypes row, one a line (tools/Poe2UiExtract action-types), regenerated after patches." };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(ids));
        Say.Out.WriteLine($"Wrote {outFile}: {ids.Count} action types.");
        return 0;
    }
}
