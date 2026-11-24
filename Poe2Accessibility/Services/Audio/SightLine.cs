namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>Whether an enemy or a character is in the player's sight for its sound, on the game's terrain grids (pure).</summary>
    public static class SightLine
    {
        /// <summary>Line-of-sight cells skipped at each end: the player and the monster stand on the edge of what they block.</summary>
        public const int EndCells = 2;

        /// <summary>Blocked cells in a row on the sight layer before the target counts as hidden: the staircase of a diagonal past a corner.</summary>
        public const int MaxBlockedSightCells = 2;

        /// <summary>Blocked cells in a row on the walkable grid before the target counts as hidden: ~90 units, thicker than props.</summary>
        public const int MaxBlockedWalkCells = 8;

        /// <summary>Blocked cells in a row on the sight layer before a character counts as hidden: a totem to talk past is 7 wide.</summary>
        public const int MaxBlockedCharacterCells = 8;

        /// <summary>
        ///     Neither the sight layer (empty: not read) nor the walkable grid blocks the line: the sight layer leaves pits and
        ///     water open, so an enemy past a wide one sounded though no one can walk or fight there (owner, 2026-11-16).
        /// </summary>
        public static bool Seen(byte[] sight, byte[] walkable, int bytesPerRow, Vector2 from, Vector2 to) =>
            (sight.Length == 0 || Clear(sight, bytesPerRow, from, to, MaxBlockedSightCells)) &&
            (walkable.Length == 0 || Clear(walkable, bytesPerRow, from, to, MaxBlockedWalkCells));

        /// <summary>
        ///     A character or another player, seen and talked to over ground no one walks: Freya Hartlin and Tujen, in and past
        ///     the Karui totems' circle (45 cells of it), fell silent at its edge (owner, 2026-11-24).
        /// </summary>
        public static bool SeenCharacter(byte[] sight, byte[] walkable, int bytesPerRow, Vector2 from, Vector2 to) =>
            sight.Length == 0
                ? walkable.Length == 0 || Clear(walkable, bytesPerRow, from, to, MaxBlockedWalkCells)
                : Clear(sight, bytesPerRow, from, to, MaxBlockedCharacterCells);

        /// <summary>No run of more than <paramref name="maxBlocked"/> blocking cells on the straight line, ends excepted.</summary>
        public static bool Clear(byte[] grid, int bytesPerRow, Vector2 from, Vector2 to, int maxBlocked)
        {
            var cells = (int)MathF.Ceiling(Vector2.Distance(from, to));
            var run = 0;
            for (var i = EndCells; i <= cells - EndCells; i++)
            {
                var cell = Vector2.Lerp(from, to, (float)i / cells);
                run = RoutePlanner.IsWalkable(grid, bytesPerRow, (int)cell.X, (int)cell.Y) ? 0 : run + 1;
                if (run > maxBlocked)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
