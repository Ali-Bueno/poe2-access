namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Keeps GameHelper's windows out of Alt+Tab and the taskbar, so switching back to the game never lands on the
    ///     overlay. The overlay library rewrites the whole extended style when it toggles click-through
    ///     (ClickableTransparentOverlay.Win32.Utils.SetOverlayClickable), so the tool-window bit is put back when gone.
    /// </summary>
    public sealed class HostWindowHider
    {
        private const int ExtendedStyleIndex = -20; // GWL_EXSTYLE (winuser.h)
        private const long ToolWindow = 0x80; // WS_EX_TOOLWINDOW: no Alt+Tab entry, no taskbar button
        private const long AppWindow = 0x40000; // WS_EX_APPWINDOW: would force the taskbar button back
        private const int HideCommand = 0; // SW_HIDE
        private const int ShowNoActivateCommand = 8; // SW_SHOWNA

        /// <summary>The library puts the style back only on a click-through change: a check twice a second is enough.</summary>
        private const int CheckMs = 500;

        private readonly HashSet<IntPtr> reshown = new();
        private readonly EnumWindowsProc visit;
        private long nextCheckAt;

        public HostWindowHider()
        {
            this.visit = this.Visit;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        /// <summary>Call from the overlay's own thread (DrawUI), where hiding and showing its window is safe.</summary>
        public void Tick()
        {
            var now = Environment.TickCount64;
            if (now < this.nextCheckAt)
            {
                return;
            }

            this.nextCheckAt = now + CheckMs;
            EnumWindows(this.visit, IntPtr.Zero);
        }

        private bool Visit(IntPtr window, IntPtr parameter)
        {
            GetWindowThreadProcessId(window, out var process);
            if (process != (uint)Environment.ProcessId || !IsWindowVisible(window))
            {
                return true;
            }

            var style = (long)GetWindowLongPtr(window, ExtendedStyleIndex);
            if ((style & ToolWindow) != 0 && (style & AppWindow) == 0)
            {
                return true;
            }

            // The taskbar drops a button only for a window shown again after the change (SetWindowLongPtr remarks).
            var firstTime = this.reshown.Add(window);
            if (firstTime)
            {
                ShowWindow(window, HideCommand);
            }

            SetWindowLongPtr(window, ExtendedStyleIndex, (IntPtr)((style | ToolWindow) & ~AppWindow));
            if (firstTime)
            {
                ShowWindow(window, ShowNoActivateCommand);
            }

            return true;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);
    }
}
