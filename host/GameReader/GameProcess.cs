// <copyright file="GameProcess.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets;
    using System.Drawing;
    using Utils;

    /// <summary>
    ///     Allows process manipulation. It uses the (time/event based) co-routines
    ///     to continuously monitor and open a process with the specific name. It exposes
    ///     variables/events for the caller to use.
    ///     Base class OnControllerReady is only triggered when all static addresses are found.
    ///     Limitation: This class will not open a game process if multiple processes match
    ///     the name because it does not know which process to select.
    /// </summary>
    public class GameProcess
    {
        private readonly List<Process> processesInfo = new();
        private bool closeForcefully = false;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GameProcess" /> class.
        /// </summary>
        internal GameProcess()
        {
            CoroutineHandler.Start(this.FindAndOpen());
            CoroutineHandler.Start(this.FindStaticAddresses());
        }

        /// <summary>
        ///     Gets the Pid of the game or zero in case game isn't running..
        /// </summary>
        public uint Pid
        {
            get
            {
                try
                {
                    // Information is null until the game is found. Guard explicitly instead of
                    // catching a NullReferenceException so the debugger doesn't break on a
                    // first-chance exception every startup.
                    var info = this.Information;
                    if (info == null)
                    {
                        return 0;
                    }

                    return (uint)info.Id;
                }
                catch (InvalidOperationException)
                {
                    // Process exited; caller should re-attach. Returning 0 retains current behavior.
                    return 0;
                }
                catch (Exception ex)
                {
                    // Unexpected (e.g., Win32Exception access denied). Log so it's diagnosable.
                    Console.WriteLine($"[GameProcess.Pid] Unexpected exception: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the game is foreground or not.
        /// </summary>
        public bool Foreground { get; private set; }

        /// <summary>
        ///     Gets the game size and position with respect to the monitor screen.
        /// </summary>
        public Rectangle WindowArea { get; private set; } = Rectangle.Empty;

        // How often a background scan is looked at for its result.
        private const double ScanPollSeconds = 0.05;

        // The state object is looked for every few seconds while the game starts, then seldom, then no more: by then the
        // plugin has said it finds no game state (GameStateService.StatesUnreadMs).
        private const double ProveEverySeconds = 3;
        private const double ProveSlowlySeconds = 15;
        private const int ProveQuickTries = 10;
        private const int ProveTries = 40;

        /// <summary>
        ///     Gets the Base Address of the game.
        /// </summary>
        internal IntPtr Address
        {
            get
            {
                try
                {
                    var reader = this.Handle;
                    if (reader != null && !reader.IsClosed && !reader.IsInvalid)
                    {
                        return this.Information.MainModule?.BaseAddress ?? IntPtr.Zero;
                    }

                    return IntPtr.Zero;
                }
                catch (Exception)
                {
                    return IntPtr.Zero;
                }
            }

            private set { }
        }

        /// <summary>
        ///     Gets the event which is triggered when GameProcess
        ///     has found all the static offset patterns.
        /// </summary>
        internal Event OnStaticAddressFound { get; } = new();

        /// <summary>
        ///     Gets the static addresses (along with their names) found in the GameProcess
        ///     based on the GameOffsets.StaticOffsets file.
        /// </summary>
        internal Dictionary<string, IntPtr> StaticAddresses { get; } =
            new();

        /// <summary>
        ///     Gets the game diagnostics information.
        /// </summary>
        internal Process Information { get; private set; } = null!;

        /// <summary>
        ///     Gets the game handle.
        /// </summary>
        internal SafeMemoryHandle Handle { get; private set; } = null!;

        /// <summary>
        ///     Closes the handle for the game and releases all the resources.
        /// </summary>
        /// <param name="monitorForNewGame">
        ///     Set to true if caller wants to start monitoring for new game process after closing.
        /// </param>
        internal void Close(bool monitorForNewGame = true)
        {
            this.Handle?.Dispose();
            this.Information?.Close();
            this.WindowArea = Rectangle.Empty;
            this.Foreground = false;
            CoroutineHandler.RaiseEvent(GameHelperEvents.OnClose);
            if (monitorForNewGame)
            {
                CoroutineHandler.Start(this.FindAndOpen());
            }
        }

        /// <summary>
        ///     Finds the list of processes from the list of processes running on the system
        ///     based on the GameOffsets.GameProcessName class.
        /// </summary>
        /// <returns>
        ///     co-routine IWait.
        /// </returns>
        private IEnumerator<Wait> FindAndOpen()
        {
            while (true)
            {
                yield return new Wait(2d);

                this.processesInfo.Clear();
                foreach (var process in Process.GetProcesses())
                {
                    // The image's path is asked only of a process with the game's name: asking it of every process is slow.
                    if (GameProcessDetails.ProcessNames.Contains(process.ProcessName) &&
                        GameProcessDetails.IsGame(process.ProcessName, process.MainWindowTitle, ImagePath(process)))
                    {
                        this.processesInfo.Add(process);
                    }
                }

                if (this.processesInfo.Count > 0)
                {
                    // Several clients: the one in front, else the first (no one can pick from a popup).
                    var foreground = GetForegroundWindow();
                    this.Information = this.processesInfo.Find(p => p.MainWindowHandle == foreground) ?? this.processesInfo[0];
                    if (this.Open())
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     Monitors the game process for changes.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private IEnumerator<Wait> Monitor()
        {
            while (true)
            {
                bool shouldClose = false;
                try
                {
                    // Have to check MainWindowHandle because
                    // sometime HasExited returns false even when game isn't running..
                    if (this.Information == null ||
                        this.Information.HasExited ||
                        this.Information.MainWindowHandle.ToInt64() <= 0x00 ||
                        this.closeForcefully)
                    {
                        shouldClose = true;
                    }
                    else
                    {
                        this.UpdateIsForeground();
                        this.UpdateWindowRectangle();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Information was disposed by Close() between iterations — exit cleanly.
                    shouldClose = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameProcess.Monitor] {ex}");
                    shouldClose = true;
                }

                if (shouldClose)
                {
                    this.closeForcefully = false;
                    this.Close();
                    break;
                }

                yield return new Wait(1d);
            }
        }

        /// <summary>
        ///     Finds the game's statics each time a game is opened: by their byte patterns, and for the state object, which
        ///     everything hangs from, by proof (<see cref="ProveGameStates"/>).
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private IEnumerator<Wait> FindStaticAddresses()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnOpened);
                while (true)
                {
                    var (handle, module, size) = (this.Handle, this.Address, this.ModuleSize());
                    if (module == IntPtr.Zero || size <= 0)
                    {
                        break;
                    }

                    // Off the frame thread: the image is tens of megabytes.
                    var scan = Task.Run(() => ScanStatics(handle, module, size));
                    while (!scan.IsCompleted)
                    {
                        yield return new Wait(ScanPollSeconds);
                    }

                    if (!ReferenceEquals(handle, this.Handle))
                    {
                        // Another game was opened meanwhile, and its opening was not waited for.
                        continue;
                    }

                    if (scan.IsFaulted)
                    {
                        Console.WriteLine($"[GameProcess] Static scan failed: {scan.Exception?.GetBaseException()}");
                    }
                    else if (!handle.IsClosed)
                    {
                        var (addresses, proven, data) = scan.Result;
                        this.StaticAddresses.Clear();
                        foreach (var (name, address) in addresses)
                        {
                            this.StaticAddresses[name] = address;
                        }

                        CoroutineHandler.RaiseEvent(this.OnStaticAddressFound);
                        if (!proven)
                        {
                            CoroutineHandler.Start(this.ProveGameStates(handle, module, size, data), "[GameProcess] Proving the game states");
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        ///     The statics the patterns give. A pattern's answer is one place inside the image or it is no answer; the state
        ///     object's is also tried for what it holds, since a pattern that still matches can match another function.
        /// </summary>
        private static (Dictionary<string, IntPtr> Addresses, bool Proven, List<(long Start, long Size)> Data) ScanStatics(SafeMemoryHandle reader, IntPtr module, int size)
        {
            var sections = PatternFinder.Sections(reader, module, size);
            var hits = PatternFinder.Find(reader, module, PatternScan.Ranges(sections, size, section => section.IsExecutable));
            List<long> Targets(string pattern) => hits.GetValueOrDefault(pattern, new List<long>())
                .Select(at => reader.TryReadMemory<int>(module + (nint)at, out var displacement) ? PatternScan.Target(at, displacement, size) : -1)
                .Where(target => target >= 0).Distinct().ToList();

            var addresses = new Dictionary<string, IntPtr>();
            var cull = PatternScan.Pick(Targets(StaticOffsetsPatterns.Cull), Targets(StaticOffsetsPatterns.CullAlt));
            if (cull > 0)
            {
                addresses[StaticOffsetsPatterns.Cull] = module + (nint)cull;
            }

            var states = Targets(StaticOffsetsPatterns.GameStates).Select(target => module + (nint)target).ToList();
            var proven = states.FirstOrDefault(address => GameStatesSearch.Holds(reader, address));
            if (proven != IntPtr.Zero)
            {
                addresses[StaticOffsetsPatterns.GameStates] = proven;
            }
            else if (states.Count == 1)
            {
                // Right or wrong, it cannot be told yet: the game builds its states a while after it starts.
                addresses[StaticOffsetsPatterns.GameStates] = states[0];
            }

            Console.WriteLine(
                $"[GameProcess] Statics: cull {(cull > 0 ? $"+0x{cull:X}" : "not found")}, game states " +
                $"{(proven != IntPtr.Zero ? "proven" : states.Count == 1 ? "not proven yet" : $"{states.Count} places, none proven")} " +
                $"{string.Join(", ", states.Select(address => $"+0x{address.ToInt64() - module.ToInt64():X}"))}");
            return (addresses, proven != IntPtr.Zero, PatternScan.Ranges(sections, size, section => section.IsWritable));
        }

        /// <summary>
        ///     Until a static is known to hold the state object: tries the place the pattern gave again, and when it still
        ///     does not hold one, looks through the image's data for the static that does. So a patch that breaks the
        ///     pattern, or leaves it matching elsewhere, costs a few seconds at the start and nothing more.
        /// </summary>
        private IEnumerator<Wait> ProveGameStates(SafeMemoryHandle handle, IntPtr module, int size, List<(long Start, long Size)> data)
        {
            for (var tries = 0; tries < ProveTries; tries++)
            {
                yield return new Wait(tries < ProveQuickTries ? ProveEverySeconds : ProveSlowlySeconds);
                if (!ReferenceEquals(handle, this.Handle) || handle.IsClosed)
                {
                    yield break;
                }

                var current = this.StaticAddresses.GetValueOrDefault(StaticOffsetsPatterns.GameStates);
                if (current != IntPtr.Zero && GameStatesSearch.Holds(handle, current))
                {
                    Console.WriteLine("[GameProcess] Game states: the place the pattern gave holds the state object.");
                    yield break;
                }

                // The writable sections first; then the whole image, in case the header's flags say something else.
                var ranges = tries < ProveQuickTries ? data : new List<(long Start, long Size)> { (0, size) };
                var search = Task.Run(() => GameStatesSearch.Find(handle, module, size, ranges));
                while (!search.IsCompleted)
                {
                    yield return new Wait(ScanPollSeconds);
                }

                if (!ReferenceEquals(handle, this.Handle) || handle.IsClosed)
                {
                    yield break;
                }

                if (search.IsFaulted)
                {
                    Console.WriteLine($"[GameProcess] Game states search failed: {search.Exception?.GetBaseException()}");
                    continue;
                }

                var found = search.Result;
                if (found.Count == 0)
                {
                    continue;
                }

                var pick = found.Contains(current) ? current : found[0];
                Console.WriteLine(
                    $"[GameProcess] Game states: found by search at +0x{pick.ToInt64() - module.ToInt64():X} ({found.Count} place(s)); the pattern gave " +
                    $"{(current == IntPtr.Zero ? "nothing" : $"+0x{current.ToInt64() - module.ToInt64():X}")}. The pattern needs a look (docs/patch-day.md).");
                if (pick != current)
                {
                    this.StaticAddresses[StaticOffsetsPatterns.GameStates] = pick;
                    CoroutineHandler.RaiseEvent(this.OnStaticAddressFound);
                }

                yield break;
            }

            Console.WriteLine("[GameProcess] Game states: no static holds a state object; the object itself changed (GameStateLayout).");
        }

        /// <summary>Where the process runs from; null when Windows will not say (a game run as another user).</summary>
        private static string? ImagePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int ModuleSize()
        {
            try
            {
                return this.Information.MainModule?.ModuleMemorySize ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        ///     Opens the handle for the game process.
        /// </summary>
        private bool Open()
        {
            this.Handle?.Dispose();
            this.Handle = new SafeMemoryHandle(this.Information.Id);
            if (this.Handle.IsInvalid)
            {
                return false;
            }

            CoroutineHandler.Start(
                this.Monitor(), "[GameProcess] Monitoring Game Process");
            CoroutineHandler.RaiseEvent(GameHelperEvents.OnOpened);
            return true;
        }

        /// <summary>
        ///     Updates the Foreground Property of the GameProcess class.
        /// </summary>
        private void UpdateIsForeground()
        {
            var foreground = GetForegroundWindow() == this.Information.MainWindowHandle;
            if (foreground != this.Foreground)
            {
                this.Foreground = foreground;
                CoroutineHandler.RaiseEvent(GameHelperEvents.OnForegroundChanged);
            }
        }

        /// <summary>
        ///     Gets the game process window area with reference to the monitor screen.
        /// </summary>
        private void UpdateWindowRectangle()
        {
            GetClientRect(this.Information.MainWindowHandle, out var size);
            ClientToScreen(this.Information.MainWindowHandle, out var pos);
            var sizePos = size.ToRectangle(pos);
            if (sizePos != this.WindowArea && sizePos.Size != Size.Empty)
            {
                this.WindowArea = sizePos;
                CoroutineHandler.RaiseEvent(GameHelperEvents.OnMoved);
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, out Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            private readonly int left;
            private readonly int top;
            private readonly int right;
            private readonly int bottom;

            internal Rectangle ToRectangle(Point point)
            {
                return new Rectangle(point.X, point.Y, this.right - this.left, this.bottom - this.top);
            }
        }
    }
}
