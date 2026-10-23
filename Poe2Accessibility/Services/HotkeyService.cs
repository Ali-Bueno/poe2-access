namespace Poe2Accessibility.Services
{
    using ClickableTransparentOverlay.Win32;

    /// <summary>
    ///     Debounced global key check (Win32 GetKeyState through the overlay library), the same mechanism
    ///     GameHelper2's own plugins use so hotkeys fire while the game window has focus.
    /// </summary>
    public sealed class HotkeyService
    {
        /// <summary>Same debounce GameHelper2's shipped plugins pass to IsKeyPressedAndNotTimeout.</summary>
        private const int DebounceMs = 250;

        public bool Pressed(VK key) => key != 0 && Utils.IsKeyPressedAndNotTimeout(key, DebounceMs);
    }
}
