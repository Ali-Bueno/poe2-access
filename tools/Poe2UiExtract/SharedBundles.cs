using LibBundle3;
using LibBundle3.Records;
using Index = LibBundle3.Index;

/// <summary>
///     Opens the game's bundle index read-only, letting the running game keep its own handles: LibBundle3's default
///     factory asks for exclusive reading, which fails on bundles the game holds open in game (statdescriptions,
///     holding BaseItemTypes, 2026-10-24), and the launcher regenerates the data with the game already started.
/// </summary>
internal sealed class SharedBundles : IBundleFactory
{
    private readonly string directory;

    private SharedBundles(string directory) => this.directory = directory;

    public static Index Open(string indexPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(indexPath))!;

        // The index too is read through a shared handle, copied to memory: LibBundle3 opens a path exclusively, and two
        // extractions at once (the launcher beside a research run) failed on the second (2026-11-08).
        using var file = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var copy = new MemoryStream();
        file.CopyTo(copy);
        copy.Position = 0;
        CheckHeader(copy.GetBuffer().AsSpan(0, (int)Math.Min(copy.Length, BundleIndexCheck.HeaderBytes)), copy.Length);
        return new Index(copy, false, false, new SharedBundles(directory));
    }

    /// <summary>Refuses an index file whose container is not the one the library reads, before the library is handed it.</summary>
    internal static void CheckHeader(ReadOnlySpan<byte> head, long fileLength)
    {
        if (BundleIndexCheck.HeaderProblem(head, fileLength) is { } problem)
        {
            throw new InvalidDataException($"the bundle index is not the container this reader knows: {problem}");
        }
    }

    /// <summary>The install's index with its paths parsed (a few never parse, a known PoE2 quirk: their count is printed).</summary>
    public static Index OpenParsed(string gameDir)
    {
        var index = GameContent.OpenIndex(gameDir);
        var unparsed = index.ParsePaths();
        Say.Out.WriteLine($"ParsePaths failed count: {unparsed}");
        if (BundleIndexCheck.Problem(index.Files.Count, index.Bundles.Length, BundleFiles.Find(index, "data/balance/stats.datc64", "data/stats.datc64") != null, unparsed) is { } problem)
        {
            index.Dispose();
            throw new InvalidDataException(problem);
        }

        return index;
    }

    public Bundle GetBundle(BundleRecord record)
    {
        var stream = new FileStream(Path.Join(this.directory, record.Path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new Bundle(stream, false, record);
    }

    public Stream CreateBundle(string bundlePath) => throw new NotSupportedException("The game's bundles are only read.");

    public bool DeleteBundle(string bundlePath) => throw new NotSupportedException("The game's bundles are only read.");
}
