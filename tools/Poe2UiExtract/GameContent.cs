using LibBundle3;
using LibGGPK3;
using LibGGPK3.Records;
using BundleRecord = LibBundle3.Records.BundleRecord;
using Index = LibBundle3.Index;

/// <summary>
///     The game's files in either install layout: Steam keeps Bundles2 and the FMOD banks as loose files, the Korean
///     client (Kakao Games) keeps both inside one Content.ggpk. The GGPK is opened once per run, read-only and shared
///     with the running game (LibGGPK3's path constructor asks for write access), and stays open until the extractor exits.
/// </summary>
internal static class GameContent
{
    private const string GgpkName = "Content.ggpk";
    private static readonly Dictionary<string, GGPK> Opened = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the install holds FMOD banks (loose, or in its Content.ggpk).</summary>
    public static bool HasBanks(string gameDir) =>
        Directory.Exists(BankDirectory(gameDir)) || File.Exists(Path.Combine(gameDir, GgpkName));

    /// <summary>The install's bundle index, its paths not parsed yet.</summary>
    public static Index OpenIndex(string gameDir)
    {
        var indexPath = Path.Combine(gameDir, "Bundles2", "_.index.bin");
        if (File.Exists(indexPath) || Ggpk(gameDir) is not { } ggpk)
        {
            return SharedBundles.Open(indexPath);
        }

        var bundles2 = ggpk.Root["Bundles2"] as DirectoryRecord ?? throw new DirectoryNotFoundException($"No Bundles2 in {GgpkName}");
        var index = bundles2["_.index.bin"] as FileRecord ?? throw new FileNotFoundException($"No Bundles2/_.index.bin in {GgpkName}");
        return new Index(new MemoryStream(index.Read(), false), false, false, new GgpkBundles(bundles2));
    }

    /// <summary>The bank FMOD/Desktop/<paramref name="name"/>.bank, or null when the install has none by that name.</summary>
    public static byte[]? ReadBank(string gameDir, string name)
    {
        var file = Path.Combine(BankDirectory(gameDir), name + ".bank");
        if (File.Exists(file))
        {
            return File.ReadAllBytes(file);
        }

        if (Ggpk(gameDir) is { } ggpk && ggpk.Root.TryFindNode($"FMOD/Desktop/{name}.bank", out var node) && node is FileRecord record)
        {
            return record.Read();
        }

        return null;
    }

    private static string BankDirectory(string gameDir) => Path.Combine(gameDir, "FMOD", "Desktop");

    private static GGPK? Ggpk(string gameDir)
    {
        var path = Path.GetFullPath(Path.Combine(gameDir, GgpkName));
        lock (Opened)
        {
            if (Opened.TryGetValue(path, out var open))
            {
                return open;
            }

            if (!File.Exists(path))
            {
                return null;
            }

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Opened[path] = new GGPK(stream);
        }
    }

    /// <summary>
    ///     Each bundle read through its own <see cref="RecordStream" />: LibBundledGGPK3's factory opens a GGFileStream, of
    ///     which LibGGPK3 allows one per file at a time, so the batch's extractions reading one bundle side by side failed
    ///     ("An instance of GGFileStream is already created for this FileRecord", places, 2026-10-31). Copying the whole
    ///     bundle for every read instead made the batch four times slower (83 s instead of 20 s).
    /// </summary>
    private sealed class GgpkBundles(DirectoryRecord bundles2) : IBundleFactory
    {
        public Bundle GetBundle(BundleRecord record) =>
            bundles2.TryFindNode(record.Path, out var node) && node is FileRecord file
                ? new Bundle(new RecordStream(file), false, record)
                : throw new FileNotFoundException($"No Bundles2/{record.Path} in {GgpkName}");

        public Stream CreateBundle(string bundlePath) => throw new NotSupportedException("The game's bundles are only read.");

        public bool DeleteBundle(string bundlePath) => throw new NotSupportedException("The game's bundles are only read.");
    }

    /// <summary>A read-only view of one file of the GGPK, any number at once: FileRecord.Read locks the GGPK's stream.</summary>
    private sealed class RecordStream(FileRecord record) : Stream
    {
        private long position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => record.DataLength;

        public override long Position
        {
            get => this.position;
            set => this.position = Math.Clamp(value, 0, this.Length);
        }

        public override int Read(Span<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, this.Length - this.position);
            if (count <= 0)
            {
                return 0;
            }

            record.Read(buffer[..count], (int)this.position);
            this.position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

        public override long Seek(long offset, SeekOrigin origin) => this.Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => this.position + offset,
            _ => this.Length + offset,
        };

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
