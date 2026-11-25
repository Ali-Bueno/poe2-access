namespace Poe2Accessibility.Services
{
    /// <summary>What the minimap draws of an icon by its pointer reach (MinimapIcons +0x12, docs/research/minimap.md "Scanner rule"). Pure.</summary>
    public static class MinimapPointer
    {
        /// <summary>
        ///     Whether the minimap draws an icon <paramref name="cells"/> off: everywhere in a town or hideout and for what is neither
        ///     a character nor an exit; those elsewhere only within the icon's pointer reach, <paramref name="reachCells"/> (0 when
        ///     not read: none for a character, the usual for an exit).
        /// </summary>
        public static bool Drawn(bool town, bool character, bool exit, float cells, float reachCells) =>
            town || !(character || exit) || cells <= (reachCells > 0 || character ? reachCells : UsualReachCells);

        /// <summary>The reach of 1,011 of the 1,050 rows (0.5.5h).</summary>
        public const float UsualReachCells = 250f;
    }
}
