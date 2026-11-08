using Index = LibBundle3.Index;

/// <summary>Opening a table from the game's bundles; the reader itself (DatTable.cs) is pure, so tests build tables from bytes.</summary>
internal sealed partial class DatTable
{
    /// <summary>Opens <c>data/[balance/]&lt;name&gt;.datc64</c> (English); null when the index has no such table.</summary>
    public static DatTable? Open(Index index, string name)
    {
        var file = BundleFiles.Find(index, $"data/balance/{name}.datc64", $"data/{name}.datc64");
        return file == null ? null : FromBytes(file.Read().ToArray());
    }

    /// <summary>Opens <c>data/[balance/]&lt;language&gt;/&lt;name&gt;.datc64</c>, a translated copy; null when absent.</summary>
    public static DatTable? OpenTranslated(Index index, string name, string language)
    {
        var file = BundleFiles.Find(index, $"data/balance/{language}/{name}.datc64", $"data/{language}/{name}.datc64");
        return file == null ? null : FromBytes(file.Read().ToArray());
    }
}
