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
