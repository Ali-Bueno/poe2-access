namespace Poe2Accessibility.Services
{
    using System.Linq;
    using GameReader.Utils;

    /// <summary>Debounced global key check (the host's physical key state), so hotkeys fire while the game has focus.</summary>
    public sealed class HotkeyService
    {
        /// <summary>A held key repeats no faster than this.</summary>
        private const int DebounceMs = 250;

        /// <summary>Win32 virtual key of Enter.</summary>
        private const int EnterKey = 0x0D;

        /// <summary>Ctrl, Alt and both Windows keys: a key held with one of them types nothing.</summary>
        private static readonly VK[] Modifiers = { (VK)0x11, (VK)0x12, (VK)0x5B, (VK)0x5C };

        /// <summary>Win32 virtual keys of space, ",", "-", ".", digits, letters and the numeric pad; other punctuation can be a dead key, which types nothing alone.</summary>
        private static readonly VK[] TypingKeys = new[] { 0x20, 0xBC, 0xBD, 0xBE }
            .Concat(Enumerable.Range(0x30, 10)).Concat(Enumerable.Range(0x41, 26)).Concat(Enumerable.Range(0x60, 16))
            .Select(code => (VK)code).ToArray();

        /// <summary>Keys held on the last <see cref="WentDown"/> check, so a held key counts once.</summary>
        private readonly System.Collections.Generic.HashSet<VK> held = new();

        public bool Pressed(VK key) => key != 0 && KeyState.IsKeyPressedAndNotTimeout(key, DebounceMs);

        /// <summary>The key went down since the last check: once per press, however long it is held (Ctrl held for Ctrl+click stops speech once).</summary>
        public bool WentDown(VK key)
        {
            if (key == 0)
            {
                return false;
            }

            if (!KeyState.IsKeyPressed(key))
            {
                this.held.Remove(key);
                return false;
            }

            return this.held.Add(key);
        }

        public bool Enter() => this.Pressed((VK)EnterKey);

        /// <summary>A key that types a character went down, not as part of a shortcut.</summary>
        public bool Typed() => !Modifiers.Any(KeyState.IsKeyPressed) && TypingKeys.Any(this.Pressed);
    }
}
