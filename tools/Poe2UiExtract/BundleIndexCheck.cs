/// <summary>What an opened bundle index must show before any step trusts it: files, bundles and one table every step needs. Pure.</summary>
internal static class BundleIndexCheck
{
    /// <summary>The batch's exit code when the index could not be opened or does not look like the game's.</summary>
    public const int ExitCode = 2;

    /// <summary>The name the launcher lists in game-data-failed.txt for it.</summary>
    public const string Name = "bundle index";

    /// <summary>The cause, or null when the index looks right.</summary>
    public static string? Problem(int files, int bundles, bool statsTableFound, int unparsedPaths = 0)
    {
        if (files <= 0 || bundles <= 0)
        {
            return $"the index lists {files} files in {bundles} bundles";
        }

        if (unparsedPaths * 2 > files)
        {
            return $"{unparsedPaths} of {files} paths in the index did not parse";
        }

        return statsTableFound ? null : $"the index lists {files} files but not data/balance/stats.datc64 (nor data/stats.datc64)";
    }
}
