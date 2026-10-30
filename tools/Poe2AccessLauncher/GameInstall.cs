using System.IO; // also built into the tests, which have no implicit usings

namespace Poe2AccessLauncher;

/// <summary>
///     The two layouts of a Path of Exile 2 install: Steam's loose Bundles2 folder, and the Korean client's (Kakao Games,
///     C:\Kakaogames\Path of Exile2) single Content.ggpk holding Bundles2 and the FMOD banks.
/// </summary>
internal static class GameInstall
{
    /// <summary>The game's process names: Steam, standalone, and the Kakao Games client (the names GameHelper2's GameOffsets looks for).</summary>
    public static readonly string[] ProcessNames = ["PathOfExileSteam", "PathOfExile", "PathOfExile_KG"];

    private const string KakaoProcessName = "PathOfExile_KG";

    /// <summary>
    ///     Whether the launcher must restart as administrator for GameHelper to read the running game: the Korean client
    ///     denies its memory to other processes unless they run as administrator (tested 2026-10-31).
    /// </summary>
    public static bool NeedsAdministrator(string? runningProcessName, bool isAdministrator) =>
        !isAdministrator && string.Equals(runningProcessName, KakaoProcessName, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>The file listing every bundle, so its size and time move with each content patch, or null when the folder holds no game content.</summary>
    public static string? ContentFile(string gameDirectory)
    {
        var index = Path.Join(gameDirectory, "Bundles2", "_.index.bin");
        if (File.Exists(index))
        {
            return index;
        }

        var ggpk = Path.Join(gameDirectory, "Content.ggpk");
        return File.Exists(ggpk) ? ggpk : null;
    }
}
