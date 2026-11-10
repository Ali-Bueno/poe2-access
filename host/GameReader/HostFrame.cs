// <copyright file="HostFrame.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader
{
    using System;
    using System.Diagnostics;
    using Coroutine;
    using CoroutineEvents;

    /// <summary>What the overlay's render loop did each frame, for a host without a window.</summary>
    public static class HostFrame
    {
        /// <summary>Starts the core's coroutines (process search, static addresses, states).</summary>
        public static void Start()
        {
            Core.Initialize();
            Core.InitializeCororutines();
        }

        /// <summary>The names of the parts of a frame, in the order <see cref="Run"/> times them.</summary>
        public static readonly string[] StepNames = { "coroutines", "reader", "plugin" };

        /// <summary>
        ///     Runs one frame: coroutines, the remote objects' updates, then <paramref name="tick"/>; adds each
        ///     part's Stopwatch ticks to <paramref name="elapsed"/> (one slot per <see cref="StepNames"/> entry).
        /// </summary>
        public static void Run(double deltaSeconds, Action tick, Action<string, Exception> fault, long[] elapsed)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                CoroutineHandler.Tick(deltaSeconds); // not through Step: a lambda over the frame's time is two objects a frame
            }
            catch (Exception error)
            {
                fault("tick", error);
            }

            var read = Stopwatch.GetTimestamp();
            Step("per frame data", () => CoroutineHandler.RaiseEvent(GameHelperEvents.PerFrameDataUpdate), fault);
            Step("post per frame data", () => CoroutineHandler.RaiseEvent(GameHelperEvents.PostPerFrameDataUpdate), fault);
            var plugin = Stopwatch.GetTimestamp();
            Step("plugin", tick, fault);
            var end = Stopwatch.GetTimestamp();
            elapsed[0] += read - start;
            elapsed[1] += plugin - read;
            elapsed[2] += end - plugin;
        }

        /// <summary>Gets a value indicating whether the game process is open.</summary>
        public static bool GameOpened => Core.Process.Address != IntPtr.Zero;

        /// <summary>Gets a value indicating whether the game window is in front.</summary>
        public static bool GameForeground => Core.Process.Foreground;

        /// <summary>Gets the current game state's name, for the host's log.</summary>
        public static string GameState => Core.States.GameCurrentState.ToString();

        /// <summary>Gets the reads of the game's memory made so far, by every thread (each a system call), by the type read.</summary>
        public static System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, long>> MemoryReadsByType => Utils.SafeMemoryHandle.ReadsByType;

        /// <summary>Closes the game handle.</summary>
        public static void Stop() => Core.Dispose();

        private static void Step(string name, Action action, Action<string, Exception> fault)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                fault(name, error);
            }
        }
    }
}
