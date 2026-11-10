// <copyright file="Core.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Coroutine;
    using CoroutineEvents;
    using GameReader.Cache;
    using RemoteObjects;
    using Settings;
    using Utils;

    /// <summary>
    ///     Main Class that depends on the GameProcess Events
    ///     and updates the RemoteObjects. It also manages the
    ///     GameHelper settings.
    /// </summary>
    public static class Core
    {
        /// <summary>
        ///     Gets the GameHelper version.
        /// </summary>
        private static string version = null!;

        /// <summary>
        ///     Gets or sets what the host does when the game closes and <see cref="State.CloseWhenGameExit"/> is set.
        /// </summary>
        public static Action? CloseRequested { get; set; }

        /// <summary>
        ///     Gets the list of active coroutines.
        /// </summary>
        public static List<ActiveCoroutine> CoroutinesRegistrar { get; } = new();

        /// <summary>
        ///     Gets the GameStates instance. For details read class description.
        /// </summary>
        public static GameStates States { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the GameProcess instance. For details read class description.
        /// </summary>
        public static GameProcess Process { get; } = new();

        /// <summary>
        ///     Gets the GameHelper settings.
        /// </summary>
        public static State GHSettings { get; } = new(); // the mod's defaults; the host keeps no settings file

        /// <summary>
        ///     Gets the cache for all the GGPK data with value type string.
        /// </summary>
        internal static GgpkAddresses<string> GgpkStringCache {get;} = new();

        /// <summary>
        ///     Gets the cache for all the GGPK data with value type object.
        ///     Please use <seealso cref="GgpkStringCache"/> if possible as it's
        ///     little bit faster than this one.
        /// </summary>
        internal static GgpkAddresses<object> GgpkObjectCache { get;} = new();

        /// <summary>
        ///     Gets the values associated with the Game Window Scale.
        /// </summary>
        internal static GameWindowScale GameScale { get; } = new();

        /// <summary>
        ///     Gets the size of the cull (black bar) area in the game window.
        /// </summary>
        public static GameWindowCull GameCull { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Initializes the <see cref="Core" /> class.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var versionStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(); // the file version is unreadable inside a single-file executable
                if (string.IsNullOrEmpty(versionStr) || versionStr == "1.0.0.0")
                {
                    version = "Dev";
                }
                else
                {
                    var parts = versionStr.Split('.');
                    version = $"v{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read the host library's version: {ex.Message}.");
                version = "Dev";
            }
        }

        /// <summary>
        ///     Get GameHelper version.
        /// </summary>
        /// <returns>GameHelper version.</returns>
        public static string GetVersion()
        {
            return version.Trim();
        }

        /// <summary>
        ///     Initializes the <see cref="Core" /> class coroutines.
        /// </summary>
        internal static void InitializeCororutines()
        {
            CoroutineHandler.Start(GameClosedActions());
            CoroutineHandler.Start(UpdateStatesData(), priority: int.MaxValue - 3);
            CoroutineHandler.Start(UpdateCullData(), priority: int.MaxValue);
        }

        /// <summary>
        ///     Cleans up all the resources taken by the application core.
        /// </summary>
        internal static void Dispose()
        {
            Process.Close(false);
        }

        /// <summary>The two static patterns of <c>StaticOffsetsPatterns</c> that read the window cull; the second is the fallback.</summary>
        private const string CullPattern = "GameCullSize";
        private const string CullPatternAlt = "GameCullSize (alt)";

        /// <summary>
        ///     Co-routine to update the address where the
        ///     Game Cull Value is loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateCullData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                var cull = Process.StaticAddresses.GetValueOrDefault(CullPattern);
                GameCull.Address = cull != IntPtr.Zero ? cull : Process.StaticAddresses.GetValueOrDefault(CullPatternAlt);
                GameScale.Refresh();
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the Game States are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateStatesData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                States.Address = Process.StaticAddresses.GetValueOrDefault("Game States");
            }
        }

        /// <summary>
        ///     Co-routine to set All controllers addresses to Zero,
        ///     once the game closes.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> GameClosedActions()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnClose);
                States.Address = IntPtr.Zero;
                GameCull.Address = IntPtr.Zero;
                OffsetHealer.Forget();

                if (GHSettings.CloseWhenGameExit)
                {
                    CloseRequested?.Invoke();
                }
            }
        }
    }
}
