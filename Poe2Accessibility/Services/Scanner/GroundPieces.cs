namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>What a carry onto other ground of the same area meant: into a piece apart, back to the main ground, or nothing new.</summary>
    public enum PieceNews
    {
        None,
        EnteredApart,
        BackToMain,
    }

    /// <summary>
    ///     The pieces of ground of one area the player was carried between without an area load (a tomb's stairs, a Trial
    ///     room's): the ground first stood on is the main one, ground carried to is apart. Ground a closed gate cuts off is
    ///     never apart, as no carry reached it. Kept as cells, since the pieces are labelled again when the grid changes.
    ///     Pure (docs/research/map-pieces.md).
    /// </summary>
    public sealed class GroundPieces
    {
        private readonly List<Vector2> apart = new();
        private readonly List<(Vector2 From, Vector2 To)> carries = new();
        private Vector2? main;

        /// <summary>A carry has set some ground apart in this area.</summary>
        public bool AnyApart => this.apart.Count > 0;

        /// <summary>A new area: nothing is known of its ground.</summary>
        public void Reset()
        {
            this.main = null;
            this.apart.Clear();
            this.carries.Clear();
        }

        /// <summary>Each look at the player: notes the main ground, and the piece a carry brought them to.</summary>
        /// <param name="player">The player's cell.</param>
        /// <param name="carriedFrom">Where the player was carried from just now (<see cref="CarriedWatch"/>); null when not carried.</param>
        /// <param name="pieceAt">The piece of ground at a cell, 0 when none or not known (<see cref="WalkableRegions.RegionAt"/>).</param>
        public PieceNews Update(Vector2 player, Vector2? carriedFrom, Func<Vector2, int> pieceAt)
        {
            var here = pieceAt(player);
            if (here == 0)
            {
                return PieceNews.None;
            }

            if (this.main is not { } main || pieceAt(main) == 0)
            {
                this.main = player;
                return PieceNews.None;
            }

            if (carriedFrom is not { } from || pieceAt(from) is not (var left and > 0) || left == here)
            {
                return PieceNews.None;
            }

            this.carries.RemoveAll(carry => pieceAt(carry.From) == left && pieceAt(carry.To) == here);
            this.carries.Add((from, player));

            if (here == pieceAt(main))
            {
                return PieceNews.BackToMain;
            }

            if (!this.apart.Any(cell => pieceAt(cell) == here))
            {
                this.apart.Add(player);
            }

            return PieceNews.EnteredApart;
        }

        /// <summary>The player stands on a piece apart.</summary>
        public bool InPieceApart(Vector2 player, Func<Vector2, int> pieceAt) =>
            pieceAt(player) is var here and > 0 && this.IsApart(here, pieceAt);

        /// <summary>
        ///     Whether a place is out of the player's reach by any walk and should not be told: on a piece apart the player is
        ///     not on, or anywhere off the piece apart the player is on. Unknown ground is never hidden.
        /// </summary>
        public bool Hides(Vector2 place, Vector2 player, Func<Vector2, int> pieceAt)
        {
            if (this.apart.Count == 0 || pieceAt(place) is not (var there and > 0) || pieceAt(player) is not (var here and > 0) || there == here)
            {
                return false;
            }

            return this.IsApart(here, pieceAt) || this.IsApart(there, pieceAt);
        }

        /// <summary>
        ///     Where on the player's piece a carry once joined a place's piece: the way taken from here to there, else where a
        ///     carry from there arrived (the way back stands by it); null when none is known.
        /// </summary>
        public Vector2? WayToward(Vector2 place, Vector2 player, Func<Vector2, int> pieceAt)
        {
            var (there, here) = (pieceAt(place), pieceAt(player));
            if (here == 0 || there == 0 || here == there)
            {
                return null;
            }

            Vector2? back = null;
            foreach (var carry in this.carries)
            {
                var (from, to) = (pieceAt(carry.From), pieceAt(carry.To));
                if (from == here && to == there)
                {
                    return carry.From;
                }

                if (from == there && to == here)
                {
                    back = carry.To;
                }
            }

            return back;
        }

        private bool IsApart(int piece, Func<Vector2, int> pieceAt) =>
            this.main is { } main && pieceAt(main) != piece && this.apart.Any(cell => pieceAt(cell) == piece);
    }
}
