using System.Runtime.CompilerServices;
using System.Text;
using LibBundle3.Records;
using Index = LibBundle3.Index;

/// <summary>The index's files by lower-case path, built once per index and shared by the commands that run side by side on it.</summary>
internal static class BundleFiles
{
    /// <summary>A bundled UTF-16 text file (.ao, .ot, ...), its byte-order mark dropped.</summary>
    public static string ReadText(this FileRecord file) => GameFormats.Decode(file.Read().Span, file.Path);

    private static readonly ConditionalWeakTable<Index, Dictionary<string, FileRecord>> Cache = new();
    private static readonly object Gate = new();

    /// <summary>Every file with a path, keyed by its invariant lower-case path (the first of any that differ only in case).</summary>
    public static Dictionary<string, FileRecord> ByLowerPath(Index index)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue(index, out var files))
            {
                files = new Dictionary<string, FileRecord>(StringComparer.Ordinal);
                foreach (var file in index.Files.Values)
                {
                    if (file.Path != null)
                    {
                        files.TryAdd(file.Path.ToLowerInvariant(), file);
                    }
                }

                Cache.Add(index, files);
            }

            return files;
        }
    }

    /// <summary>A bundle is decompressed whole for this many files of it or more; fewer are read one by one.</summary>
    private const int FilesWorthABundle = 8;

    /// <summary>
    ///     Reads many files bundle by bundle, a bundle decompressed once for all its files and a few bundles at a time, and
    ///     hands each file's bytes to <paramref name="handle"/>, which is called from several threads and in no order. One
    ///     read a file opens its bundle and decompresses a block each time: 22,000 terrain files took 12 s that way.
    /// </summary>
    public static void ReadGrouped(IEnumerable<FileRecord> files, Action<FileRecord, byte[]> handle, int bundlesAtOnce = 4)
    {
        var groups = files.GroupBy(file => file.BundleRecord).ToList();
        Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = bundlesAtOnce }, group =>
        {
            var wanted = group.ToList();
            if (wanted.Count < FilesWorthABundle)
            {
                wanted.ForEach(file => handle(file, file.Read().ToArray()));
                return;
            }

            if (!group.Key.TryGetBundle(out var bundle, out var error))
            {
                throw new IOException($"Bundle {group.Key.Path} could not be opened", error);
            }

            using (bundle)
            {
                var all = bundle.ReadWithoutCache();
                wanted.ForEach(file => handle(file, all.AsSpan(file.Offset, file.Size).ToArray()));
            }
        });
    }

    private static readonly ConditionalWeakTable<Index, System.Collections.Concurrent.ConcurrentDictionary<FileRecord, Lazy<byte[]>>> Tables = new();

    /// <summary>A table's bytes, read once per index: the steps of a batch open the same tables again and again.</summary>
    public static byte[] ReadOnce(Index index, FileRecord file) =>
        Tables.GetValue(index, _ => new()).GetOrAdd(file, record => new Lazy<byte[]>(() => record.Read().ToArray())).Value;

    /// <summary>The first of the given paths (any case) the index holds, or null.</summary>
    public static FileRecord? Find(Index index, params string[] paths)
    {
        var files = ByLowerPath(index);
        foreach (var path in paths)
        {
            if (files.TryGetValue(path.ToLowerInvariant(), out var file))
            {
                return file;
            }
        }

        return null;
    }
}
