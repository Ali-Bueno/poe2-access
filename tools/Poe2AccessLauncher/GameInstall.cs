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

    /// <summary>Path of Exile 2's Steam app id.</summary>
    public const string SteamAppId = "2694490";

    private const string KakaoProcessName = "PathOfExile_KG";

    /// <summary>
    ///     Whether the game is started by asking for the Korean client: only when Steam does not have the game, so a PC
    ///     with both clients (or a Kakao Games folder left behind) still starts the Steam one, as before the Korean client.
    /// </summary>
    public static bool StartsKakaoClient(bool steamHasGame, string? kakaoDirectory) => !steamHasGame && kakaoDirectory is not null;

    /// <summary>
    ///     Whether one of the Steam libraries holds the game's manifest: Steam's own folder and every library listed in
    ///     its steamapps\libraryfolders.vdf (whose paths escape their backslashes).
    /// </summary>
    public static bool SteamHasGame(string? steamDirectory)
    {
        if (string.IsNullOrEmpty(steamDirectory))
        {
            return false;
        }

        var libraries = new System.Collections.Generic.List<string> { steamDirectory };
        var folders = Path.Join(steamDirectory, "steamapps", "libraryfolders.vdf");
        if (File.Exists(folders))
        {
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(folders), "\"path\"\\s+\"([^\"]*)\""))
            {
                libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            }
        }

        return libraries.Exists(library => File.Exists(Path.Join(library, "steamapps", $"appmanifest_{SteamAppId}.acf")));
    }

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
