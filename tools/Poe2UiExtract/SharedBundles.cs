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

    private static readonly object KeepGate = new();
    private static Index? keptIndex;
    private static string? keptDirectory;
    private static int keptLeases;
    private static bool keeping;

    /// <summary>Whether an index is being kept open for the runs to come.</summary>
    public static bool Keeping
    {
        get
        {
            lock (KeepGate)
            {
                return keeping;
            }
        }
    }

    /// <summary>
    ///     Until disposed, the index a run opens stays open for the runs after it: the launcher's batch, then its object
    ///     sounds, which opened and parsed the same index again (about 12 s of each run).
    /// </summary>
    public static IDisposable Keep()
    {
        lock (KeepGate)
        {
            keeping = true;
        }

        return new Keeper();
    }

    /// <summary>The install's index with its paths parsed, for one run: the one being kept when there is one, else its own.</summary>
    public static IndexLease OpenParsed(string gameDir)
    {
        var directory = Path.GetFullPath(gameDir);
        lock (KeepGate)
        {
            if (keeping && keptIndex != null && string.Equals(keptDirectory, directory, StringComparison.OrdinalIgnoreCase))
            {
                keptLeases++;
                Say.Out.WriteLine("Index: kept open from the run before this one");
                return new IndexLease(keptIndex, kept: true);
            }
        }

        var index = OpenAndParse(gameDir);
        lock (KeepGate)
        {
            if (keeping && keptIndex == null)
            {
                (keptIndex, keptDirectory, keptLeases) = (index, directory, 1);
                return new IndexLease(index, kept: true);
            }
        }

        return new IndexLease(index, kept: false);
    }

    /// <summary>A run is done with a kept index: it is closed once nothing keeps it and no run still reads it.</summary>
    internal static void Return()
    {
        lock (KeepGate)
        {
            keptLeases--;
            CloseKeptWhenFree();
        }
    }

    private static void CloseKeptWhenFree()
    {
        if (!keeping && keptLeases <= 0 && keptIndex != null)
        {
            keptIndex.Dispose();
            (keptIndex, keptDirectory, keptLeases) = (null, null, 0);
            GameContent.CloseAll();
        }
    }

    private sealed class Keeper : IDisposable
    {
        public void Dispose()
        {
            lock (KeepGate)
            {
                keeping = false;
                CloseKeptWhenFree();
            }
        }
    }

    /// <summary>The install's index with its paths parsed (a few never parse, a known PoE2 quirk: their count is printed).</summary>
    private static Index OpenAndParse(string gameDir)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var index = GameContent.OpenIndex(gameDir);
        var opened = watch.Elapsed.TotalSeconds;
        var unparsed = index.ParsePaths();
        Say.Out.WriteLine($"ParsePaths failed count: {unparsed}");
        Say.Out.WriteLine($"Index: opened in {opened:0.0} s, its paths parsed in {watch.Elapsed.TotalSeconds - opened:0.0} s");
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

/// <summary>An opened index for the length of one run; disposing it closes the index unless it is kept for the next run.</summary>
internal sealed class IndexLease : IDisposable
{
    private readonly bool kept;
    private bool returned;

    internal IndexLease(Index index, bool kept) => (this.Index, this.kept) = (index, kept);

    public Index Index { get; }

    public void Dispose()
    {
        if (this.returned)
        {
            return;
        }

        this.returned = true;
        if (this.kept)
        {
            SharedBundles.Return();
        }
        else
        {
            this.Index.Dispose();
        }
    }
}
