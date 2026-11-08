namespace Poe2Access
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using GameReader;
    using Microsoft.Win32.SafeHandles;

    /// <summary>Runs the reader and the plugin at a fixed rate on one thread, sleeping between frames.</summary>
    internal sealed class FrameLoop(WaitHandle stop, Action tick)
    {
        /// <summary>The rate the mod always ran at (GameHelper's FPS limit of 60, vsync off).</summary>
        private const double FramesPerSecond = 60;

        /// <summary>
        ///     With the game behind another window (or not open) the plugin plays and says nothing about the world and
        ///     only keeps its state: chat, area changes and screen changes are still taken in, at their own poll rates
        ///     of 150 ms and more, so a slower frame costs nothing audible.
        /// </summary>
        private const double BackgroundFramesPerSecond = 20;

        private readonly Dictionary<string, string> lastFault = new();
        private readonly FrameStats stats = new();

        public void Run()
        {
            using var timer = FrameTimer.Create();
            var frequency = Stopwatch.Frequency;
            var clock = Stopwatch.StartNew();
            var last = clock.ElapsedTicks;
            var next = last;
            while (!stop.WaitOne(0))
            {
                var now = clock.ElapsedTicks;
                HostFrame.Run((now - last) / (double)frequency, tick, this.Fault, this.stats.Steps);
                this.stats.EndFrame(clock.ElapsedTicks - now);
                last = now;
                var period = (long)(frequency / (HostFrame.GameOpened && HostFrame.GameForeground ? FramesPerSecond : BackgroundFramesPerSecond));
                next += period;
                var wait = next - clock.ElapsedTicks;
                if (wait <= -period)
                {
                    next = clock.ElapsedTicks; // a long frame (an area load): start over, do not run frames back to back
                }
                else if (wait > 0 && timer.Wait(stop, TimeSpan.FromTicks(wait * TimeSpan.TicksPerSecond / frequency)))
                {
                    break;
                }
            }
        }

        private void Fault(string step, Exception error)
        {
            var text = error.ToString();
            if (this.lastFault.TryGetValue(step, out var previous) && previous == text)
            {
                return;
            }

            this.lastFault[step] = text;
            HostLog.Line($"Frame step '{step}' threw: {text}");
        }

        /// <summary>A high-resolution waitable timer (Windows 10 1803+), else a plain wait on the stop handle.</summary>
        private sealed class FrameTimer : IDisposable
        {
            private const uint HighResolution = 0x2; // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
            private const uint AllAccess = 0x1F0003; // TIMER_ALL_ACCESS

            private readonly TimerHandle? handle;

            private FrameTimer(TimerHandle? handle) => this.handle = handle;

            public static FrameTimer Create()
            {
                var raw = CreateWaitableTimerExW(IntPtr.Zero, null, HighResolution, AllAccess);
                if (raw == IntPtr.Zero)
                {
                    HostLog.Line("High-resolution timer unavailable; frames paced by the system timer");
                    return new FrameTimer(null);
                }

                return new FrameTimer(new TimerHandle(raw));
            }

            /// <summary>Waits <paramref name="delay"/>; true when <paramref name="stop"/> was signalled instead.</summary>
            public bool Wait(WaitHandle stop, TimeSpan delay)
            {
                if (this.handle == null)
                {
                    return stop.WaitOne(delay);
                }

                var due = -delay.Ticks; // negative: relative, in 100 ns units
                if (!SetWaitableTimer(this.handle.SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
                {
                    return stop.WaitOne(delay);
                }

                return WaitHandle.WaitAny([stop, this.handle]) == 0;
            }

            public void Dispose() => this.handle?.Dispose();

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period, IntPtr completion, IntPtr argument, bool resume);

            private sealed class TimerHandle : WaitHandle
            {
                public TimerHandle(IntPtr raw) => this.SafeWaitHandle = new SafeWaitHandle(raw, ownsHandle: true);
            }
        }
    }
}
