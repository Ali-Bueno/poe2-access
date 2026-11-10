using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

/// <summary>
///     The cheapest sign that a text file of the game is still the format its reader knows: how it starts. The files the
///     extractors parse with patterns (.ao, .ot, .act, .amd, .arm, .tdt, .tmd, .ui) open with a version line or a fixed
///     word; a step notes each file it decodes, and when most files of a format start another way the step fails with
///     the format named, where the patterns would have matched nothing and written an empty catalog. Pure.
/// </summary>
internal sealed class GameFormats
{
    /// <summary>A format is judged from this many files on, and has changed when under this share starts as known.</summary>
    internal const int MinFiles = 5;
    internal const double KnownShare = 0.5;

    private const string VersionWord = "version";

    /// <summary>How a format's files start: a "version N" line up to the newest version read, or one of a few words.</summary>
    internal sealed record Rule(int NewestVersion, params string[] Words)
    {
        public bool Knows(string start) =>
            this.Words.Contains(start, StringComparer.Ordinal) ||
            (start.StartsWith(VersionWord + " ", StringComparison.Ordinal) && int.TryParse(start[(VersionWord.Length + 1)..], out var version) && version <= this.NewestVersion);

        public override string ToString() => this.NewestVersion > 0 ? $"a version line up to {this.NewestVersion}" : string.Join(" or ", this.Words);
    }

    /// <summary>
    ///     What the steps read in the 0.5.x bundles (2026-11-10, every file): .ao 3 (9,527 files), .ot 2 and 3 (8,324), .amd 1
    ///     to 5 (643), .arm 29 to 36 (15,580), .tmd 8 (5,810 of 6,337; the others are binary), .act "animated_object" or
    ///     "parent" (1,326). Older versions read alike; a newer one is a format nobody has looked at.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Rule> Known = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
    {
        [".ao"] = new(3), [".ot"] = new(3), [".amd"] = new(5), [".arm"] = new(36), [".tmd"] = new(8), [".act"] = new(0, "animated_object", "parent"),
    };

    private const string OtherStart = "(not text)";
    private const int LongestWord = 24;

    private static readonly AsyncLocal<GameFormats?> Running = new();
    private readonly Dictionary<string, Dictionary<string, int>> starts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Starts noting the files the calling step (and the tasks it starts) decodes.</summary>
    public static GameFormats Start()
    {
        var formats = new GameFormats();
        Running.Value = formats;
        return formats;
    }

    /// <summary>A bundled UTF-16 text file decoded, its byte-order mark dropped, and how it starts noted for the running step.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes, string? path)
    {
        return Noted(path, Encoding.Unicode.GetString(bytes).TrimStart('\uFEFF'));
    }

    /// <summary>A file's text as its reader decoded it, with how it starts noted for the running step.</summary>
    public static string Noted(string? path, string text)
    {
        Running.Value?.Note(path, text.TrimStart('\uFEFF'));
        return text;
    }

    /// <summary>Notes how a decoded file starts.</summary>
    public void Note(string? path, string text)
    {
        var dot = path?.LastIndexOf('.') ?? -1;
        if (dot < 0)
        {
            return;
        }

        var start = StartOf(text);
        lock (this.starts)
        {
            if (!this.starts.TryGetValue(path![dot..], out var counts))
            {
                this.starts[path[dot..]] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            counts[start] = counts.GetValueOrDefault(start) + 1;
        }
    }

    /// <summary>"version 3" for a file that opens with a version line, else its first word; "(empty)" for none.</summary>
    internal static string StartOf(string text)
    {
        var words = text.AsSpan().TrimStart();
        var end = words.IndexOfAny(" \t\r\n\"{=");
        var first = (end < 0 ? words : words[..end]).ToString();
        if (first != VersionWord || end < 0)
        {
            return first.Length == 0 ? "(empty)" : first.Length > LongestWord || first.Any(letter => letter is < ' ' or > '~') ? OtherStart : first;
        }

        var rest = words[end..].TrimStart();
        var digits = 0;
        while (digits < rest.Length && char.IsAsciiDigit(rest[digits]))
        {
            digits++;
        }

        return $"{VersionWord} {rest[..digits]}";
    }

    /// <summary>Why a format no longer reads as known, a line each; empty when every format judged still does.</summary>
    public IReadOnlyList<string> Problems()
    {
        lock (this.starts)
        {
            return this.starts.Where(format => Known.ContainsKey(format.Key)).Select(format => Problem(format.Key, format.Value)).OfType<string>().ToList();
        }
    }

    internal static string? Problem(string extension, IReadOnlyDictionary<string, int> starts)
    {
        var files = starts.Values.Sum();
        var known = starts.Where(start => Known[extension].Knows(start.Key)).Sum(start => start.Value);
        if (files < MinFiles || known >= files * KnownShare)
        {
            return null;
        }

        var most = starts.OrderByDescending(start => start.Value).First();
        return $"the {extension} format changed: {known} of {files} files read start as this reader knows ({Known[extension]}); most start with '{most.Key}'";
    }

    /// <summary>Prints how the files of each format started and says whether the step's output may stand.</summary>
    public bool Report(string step)
    {
        lock (this.starts)
        {
            foreach (var (extension, counts) in this.starts.OrderBy(format => format.Key, StringComparer.Ordinal))
            {
                Say.Out.WriteLine($"{step}: {counts.Values.Sum()} {extension} files read: {string.Join(", ", counts.OrderByDescending(start => start.Value).Take(4).Select(start => $"{start.Key} {start.Value}"))}");
            }
        }

        var problems = this.Problems();
        foreach (var problem in problems)
        {
            Say.Out.WriteLine($"  {problem}, {step} keeps its previous catalog");
        }

        return problems.Count == 0;
    }
}
