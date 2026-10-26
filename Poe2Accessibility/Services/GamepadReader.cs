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

        /// <param name="seat">The local player, 1 or 2 (couch co-op; <see cref="GameLayout.UiManager.PlayerStride"/>).</param>
        public bool TryRead(IntPtr manager, out PadState state, int seat = 1)
        {
            state = default;
            return manager != IntPtr.Zero &&
                   this.reader.TryRead(manager + GameLayout.UiManager.PadStateOffset + ((seat - 1) * GameLayout.UiManager.PlayerStride), out state) &&
                   Plausible(state);
        }

        /// <summary>Sticks within ±1 and triggers within 0..1, with room for rounding: anything else is not a pad state.</summary>
        private static bool Plausible(in PadState state)
        {
            static bool Within(float value, float low) => !float.IsNaN(value) && value >= low - Slack && value <= 1f + Slack;
            return Within(state.LeftX, -1f) && Within(state.LeftY, -1f) && Within(state.RightX, -1f) && Within(state.RightY, -1f) &&
                   Within(state.LeftTrigger, 0f) && Within(state.RightTrigger, 0f);
        }

        private const float Slack = 0.05f;

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

            /// <summary>The largest stick or trigger deflection, 0..1.</summary>
            public float Deflection => Math.Max(Math.Max(Math.Max(Math.Abs(this.LeftX), Math.Abs(this.LeftY)), Math.Max(Math.Abs(this.RightX), Math.Abs(this.RightY))),
                Math.Max(this.LeftTrigger, this.RightTrigger));
        }
    }
}
