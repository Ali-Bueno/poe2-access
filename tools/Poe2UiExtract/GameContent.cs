using LibBundledGGPK3;
using LibGGPK3;
using LibGGPK3.Records;
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
        return new Index(new GGFileStream(index), false, false, new GGPKBundleFactory(ggpk, bundles2));
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
}
