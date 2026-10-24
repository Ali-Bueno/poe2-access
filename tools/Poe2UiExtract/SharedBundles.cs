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
        return new Index(indexPath, false, new SharedBundles(directory));
    }

    public Bundle GetBundle(BundleRecord record)
    {
        var stream = new FileStream(Path.Join(this.directory, record.Path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new Bundle(stream, false, record);
    }

    public Stream CreateBundle(string bundlePath) => throw new NotSupportedException("The game's bundles are only read.");

    public bool DeleteBundle(string bundlePath) => throw new NotSupportedException("The game's bundles are only read.");
}
