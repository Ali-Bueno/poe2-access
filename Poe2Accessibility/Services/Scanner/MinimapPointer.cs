namespace Poe2Accessibility.Services
{
    /// <summary>What the minimap draws of an icon by its pointer reach (MinimapIcons +0x12, docs/research/minimap.md "Scanner rule"). Pure.</summary>
    public static class MinimapPointer
    {
        /// <summary>
        ///     Whether the minimap draws an icon <paramref name="cells"/> off: everywhere in a town or hideout and for anything but a
        ///     character; a character elsewhere only within its icon's pointer reach, <paramref name="reachCells"/>.
        /// </summary>
        public static bool Drawn(bool town, bool character, float cells, float reachCells) => town || !character || cells <= reachCells;
    }
}
