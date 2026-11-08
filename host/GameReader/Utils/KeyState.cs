// <copyright file="KeyState.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>Physical key state for hotkeys; works from a thread without a message queue.</summary>
    public static class KeyState
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly long[] NextAllowed = new long[256];

        /// <summary>Whether the key is down now.</summary>
        public static bool IsKeyPressed(VK key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

        /// <summary>Whether the key is down and was not reported in the last <paramref name="timeout"/> ms.</summary>
        public static bool IsKeyPressedAndNotTimeout(VK key, int timeout = 200)
        {
            var index = (int)key & 0xFF;
            var now = Clock.ElapsedMilliseconds;
            if (!IsKeyPressed(key) || now <= NextAllowed[index])
            {
                return false;
            }

            NextAllowed[index] = now + timeout;
            return true;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
    }
}
