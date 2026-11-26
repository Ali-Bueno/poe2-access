namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>The pieces of ground of one area the player is carried between, and what of the rest is not told meanwhile (docs/research/map-pieces.md).</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>Scanner ids of a quest goal said through the way to its ground, apart from the scanner's own ranges.</summary>
        private const uint WayThroughIdBase = 0xE8000000;

        /// <summary>An exit this near (cells) where a carry started or ended is the one that made it: the player stood within its reach.</summary>
        private const float CarryExitCells = 30f;

        private readonly GroundPieces pieces = new();
        private string piecesInstance = string.Empty;

        /// <summary>Each look at the player: what being carried there from <paramref name="carriedFrom"/> (<see cref="CarriedWatch"/>) meant.</summary>
        public PieceNews NoteGround(Vector2 player, Vector2? carriedFrom)
        {
            if (this.piecesInstance != this.instance)
            {
                this.pieces.Reset();
                this.piecesInstance = this.instance;
            }

            return this.Regions() is { } regions ? this.pieces.Update(player, carriedFrom, regions.RegionAt) : PieceNews.None;
        }

        /// <summary>The player stands on a piece of ground apart from the area's main ground, as in a tomb.</summary>
        public bool InPieceApart() =>
            this.piecesInstance == this.instance && this.pieces.AnyApart && Seats.Followed.TryGetComponent<Render>(out var player) &&
            this.Regions() is { } regions && this.pieces.InPieceApart(new Vector2(player.GridPosition.X, player.GridPosition.Y), regions.RegionAt);

        /// <summary>
        ///     Whether a grid cell lies on ground no walk from the player reaches and that a carry set apart (<see cref="GroundPieces.Hides"/>);
        ///     null when nothing is apart in this area, so callers can skip the test.
        /// </summary>
        public Func<Vector2, bool>? OffPlayersPiece()
        {
            if (this.piecesInstance != this.instance || !this.pieces.AnyApart || !Seats.Followed.TryGetComponent<Render>(out var player) ||
                this.Regions() is not { } regions)
            {
                return null;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            return cell => this.pieces.Hides(cell, here, regions.RegionAt);
        }

        /// <summary>
        ///     For quest goals on other ground, the way to it from the player's piece: the exit a carry between the two went by,
        ///     else, on a piece apart, its nearest exit; said "antes, pasa por …" under the quest. Null when no way is known.
        /// </summary>
        private ScanEntry? WayThrough(IReadOnlyList<ScanEntry> goals, IEnumerable<ScanEntry> listed, Render player, float toCells)
        {
            if (goals.Count == 0 || toCells <= 0 || this.Regions() is not { } regions)
            {
                return null;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var exits = listed.Where(entry => entry.Category == ScanCategory.Exits).ToList();
            var door = goals.Select(goal => this.pieces.WayToward(GridOf(goal, toCells), here, regions.RegionAt)).FirstOrDefault(cell => cell != null);
            ScanEntry? exit;
            if (door is { } at)
            {
                exit = exits.Where(entry => Vector2.Distance(GridOf(entry, toCells), at) <= CarryExitCells).OrderBy(entry => Vector2.Distance(GridOf(entry, toCells), at)).FirstOrDefault();
            }
            else if (this.pieces.InPieceApart(here, regions.RegionAt))
            {
                exit = exits.OrderBy(entry => Vector2.Distance(GridOf(entry, toCells), here)).FirstOrDefault();
            }
            else
            {
                return null;
            }

            if (exit == null && door == null)
            {
                return null; // on a piece apart whose way out is not seen yet
            }

            var spot = exit is { } found ? new Vector2(found.WorldX, found.WorldY) : door!.Value * toCells;
            var name = exit?.Name ?? Announcements.WhereCarried;
            return new ScanEntry(WayThroughIdBase + ((exit?.Id ?? 0) & 0xFFFFFF), ScanCategory.Quest, Announcements.GoThroughFirst(name), spot.X, spot.Y,
                exit?.WorldZ ?? player.WorldPosition.Z);
        }

        private static Vector2 GridOf(ScanEntry entry, float toCells) => new Vector2(entry.WorldX, entry.WorldY) / toCells;
    }
}
