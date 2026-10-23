namespace Poe2Accessibility.Services
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     The controller state the game itself received this frame, read from the active UI manager
    ///     (GameLayout.UiManager.PadStateOffset). Whatever the device or Steam Input mapping, this is what the
    ///     game sees — the mod cannot hide a press from it, only notice it too.
    /// </summary>
    public sealed class GamepadReader
    {
        private readonly HostMemoryReader reader;

        public GamepadReader(HostMemoryReader reader)
        {
            this.reader = reader;
        }

        public bool TryRead(IntPtr manager, out PadState state)
        {
            state = default;
            return manager != IntPtr.Zero && this.reader.TryRead(manager + GameLayout.UiManager.PadStateOffset, out state);
        }

        /// <summary>Buttons (GameLayout.PadButtons), sticks (up and right positive) and triggers (0..1).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public readonly struct PadState
        {
            public readonly uint Buttons;
            public readonly float LeftX;
            public readonly float LeftY;
            public readonly float RightX;
            public readonly float RightY;
            public readonly float LeftTrigger;
            public readonly float RightTrigger;

            public bool IsDown(uint button) => (this.Buttons & button) != 0;
        }
    }
}
