using Index = LibBundle3.Index;

/// <summary>Opening a table from the game's bundles; the reader itself (DatTable.cs) is pure, so tests build tables from bytes.</summary>
internal sealed partial class DatTable
{
    /// <summary>Opens <c>data/[balance/]&lt;name&gt;.datc64</c> (English); null when the index has no such table.</summary>
    public static DatTable? Open(Index index, string name) => Open(index, $"data/balance/{name}.datc64", $"data/{name}.datc64");

    /// <summary>Opens <c>data/[balance/]&lt;language&gt;/&lt;name&gt;.datc64</c>, a translated copy; null when absent.</summary>
    public static DatTable? OpenTranslated(Index index, string name, string language) =>
        Open(index, $"data/balance/{language}/{name}.datc64", $"data/{language}/{name}.datc64");

    /// <summary>The first of the paths the index holds, or null.</summary>
    private static DatTable? Open(Index index, params string[] paths) => BundleFiles.Find(index, paths) is { } file ? Open(file) : null;

    /// <summary>A bundled .datc64 as a table whose reads the running step's audit will judge.</summary>
    public static DatTable Open(LibBundle3.Records.FileRecord file)
    {
        var table = FromBytes(file.Read().ToArray(), file.Path ?? "table");
        DatColumnAudit.Opened(table);
        return table;
    }
}
