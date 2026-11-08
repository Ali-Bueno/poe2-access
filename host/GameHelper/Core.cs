// <copyright file="Core.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Diagnostics;
    using Coroutine;
    using CoroutineEvents;
    using GameHelper.Cache;
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
        ///     Gets the files loaded for the current area.
        /// </summary>
        public static LoadedFiles CurrentAreaLoadedFiles { get; } = new(IntPtr.Zero);

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
        ///     Gets the AreaChangeCounter instance. For details read class description.
        /// </summary>
        internal static AreaChangeCounter AreaChangeCounter { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the values associated with the Game Window Scale.
        /// </summary>
        internal static GameWindowScale GameScale { get; } = new();

        /// <summary>
        ///     Gets the size of the cull (black bar) area in the game window.
        /// </summary>
        internal static GameWindowCull GameCull { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the values associated with the terrain rotation selector.
        /// </summary>
        internal static TerrainHeightHelper RotationSelector { get; } = new(IntPtr.Zero, 9);

        /// <summary>
        ///     Gets the values associated with the terrain rotator helper.
        /// </summary>
        internal static TerrainHeightHelper RotatorHelper { get; } = new(IntPtr.Zero, 25); // if length ever changes, update terrain  height algo.

        /// <summary>
        ///     Initializes the <see cref="Core" /> class.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
                var versionStr = versionInfo.FileVersion;
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
                Console.WriteLine($"Failed to read GameHelper version: {ex.Message}.");
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
            CoroutineHandler.Start(UpdateFilesData(), priority: int.MaxValue - 2);
            CoroutineHandler.Start(UpdateAreaChangeData(), priority: int.MaxValue - 1);
            CoroutineHandler.Start(UpdateCullData(), priority: int.MaxValue);
            CoroutineHandler.Start(UpdateRotationSelectorData(), priority: int.MaxValue);
            CoroutineHandler.Start(UpdateRotatorHelperData(), priority: int.MaxValue);
        }

        /// <summary>
        ///     Cleans up all the resources taken by the application core.
        /// </summary>
        internal static void Dispose()
        {
            Process.Close(false);
        }

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
                GameCull.Address = Process.StaticAddresses.GetValueOrDefault("GameCullSize");
                GameScale.Refresh();
            }
        }

        /// <summary>
        ///     Co-routine to update the address where AreaChange object is loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateAreaChangeData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                AreaChangeCounter.Address = Process.StaticAddresses.GetValueOrDefault("AreaChangeCounter");
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the Files are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateFilesData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                // GameHelper's preload scan of each area (its PreloadAlert) is not read by the mod: left off.
                CurrentAreaLoadedFiles.Address = IntPtr.Zero;
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
        ///     Co-routine to update the address where the Rotation Selector values are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateRotationSelectorData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                RotationSelector.Address = Process.StaticAddresses.GetValueOrDefault("Terrain Rotation Selector");
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the rotator helper values are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateRotatorHelperData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                RotatorHelper.Address = Process.StaticAddresses.GetValueOrDefault("Terrain Rotator Helper");
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
                CurrentAreaLoadedFiles.Address = IntPtr.Zero;
                AreaChangeCounter.Address = IntPtr.Zero;
                GameCull.Address = IntPtr.Zero;
                RotationSelector.Address = IntPtr.Zero;
                RotatorHelper.Address = IntPtr.Zero;

                if (GHSettings.CloseWhenGameExit)
                {
                    CloseRequested?.Invoke();
                }
            }
        }
    }
}
