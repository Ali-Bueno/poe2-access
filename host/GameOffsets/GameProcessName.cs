namespace GameOffsets
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public struct GameProcessDetails
    {
        /// <summary>The game's name, as its window's title and its install folder say it.</summary>
        public const string Title = "Path of Exile 2";

        /// <summary>
        ///     The names the game's process runs under, one per store (the executable's name without .exe). The first game
        ///     runs under the same ones.
        /// </summary>
        public static readonly HashSet<string> ProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "PathOfExile", // also on Garena
            "PathOfExile_KG",
            "PathOfExileSteam",
            "PathOfExile_x64",
            "PathOfExileEGS",
        };

        /// <summary>
        ///     Whether a process is the game. Its name alone is also the first game's, so the 2 has to show: in the window's
        ///     title, or, should a patch reword the title, in the folder the game runs from. A process with no window yet is
        ///     still starting.
        /// </summary>
        public static bool IsGame(string processName, string? windowTitle, string? imagePath)
        {
            if (!ProcessNames.Contains(processName) || string.IsNullOrWhiteSpace(windowTitle))
            {
                return false;
            }

            return windowTitle.TrimStart().StartsWith(Title, StringComparison.OrdinalIgnoreCase) ||
                   (Path.GetDirectoryName(imagePath)?.Contains(Title, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        /// <summary>Of several clients, the one whose window is in front, else the first: nobody can pick from a popup. Windows are read in order, up to that one.</summary>
        public static int Pick(int clients, Func<int, IntPtr> window, IntPtr foreground)
        {
            for (var i = 0; i < clients; i++)
            {
                if (window(i) == foreground)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        ///     Whether the game opened is gone: no process, one that exited, or no window (HasExited sometimes says false for a
        ///     game that is not running), or a close was asked for. Read in that order: an exited process's window throws.
        /// </summary>
        public static bool Gone(bool known, Func<bool> exited, Func<long> window, bool forced) => !known || exited() || window() <= 0 || forced;
    }
}
