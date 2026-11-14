namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What of the named places to say as the player walks (D2R named places): a map piece when its kind changes,
    ///     held over plain ground near it so a bridge built of several pieces is one, then the nearest new prop in reach,
    ///     unless one of its kind is still remembered: a graveyard is one "tomb", not one per grave
    ///     (docs/research/sweep-2026-11-13.md).
    /// </summary>
    public sealed class PlaceTracker
    {
        private readonly float holdCells;
        private readonly float reachCells;
        private readonly float forgetCells;
        private readonly Dictionary<Vector2, string> saidProps = new();
        private readonly List<Vector2> forgotten = new();
        private string? current;
        private string? candidate;
        private Vector2 lastOnPiece;

        /// <param name="holdCells">Plain ground this far from the last named piece keeps its name (one sub-tile).</param>
        /// <param name="reachCells">A prop this near is named.</param>
        /// <param name="forgetCells">A named prop is named again once the player went this far from it.</param>
        public PlaceTracker(float holdCells, float reachCells, float forgetCells)
        {
            this.holdCells = holdCells;
            this.reachCells = reachCells;
            this.forgetCells = forgetCells;
        }

        /// <summary>The kinds to say now, piece first; a piece's kind is said once it is seen on two updates running.</summary>
        public IReadOnlyList<string> Update(string? pieceKind, Vector2 player, IReadOnlyList<(Vector2 Grid, string Kind)> props)
        {
            var say = new List<string>();
            if (pieceKind == null)
            {
                this.candidate = null;
                if (this.current != null && Vector2.Distance(player, this.lastOnPiece) > this.holdCells)
                {
                    this.current = null;
                }
            }
            else if (pieceKind == this.current)
            {
                this.lastOnPiece = player;
                this.candidate = null;
            }
            else if (pieceKind == this.candidate)
            {
                this.current = pieceKind;
                this.lastOnPiece = player;
                this.candidate = null;
                say.Add(pieceKind);
            }
            else
            {
                this.candidate = pieceKind;
            }

            this.forgotten.Clear();
            foreach (var prop in this.saidProps.Keys)
            {
                if (Vector2.Distance(player, prop) > this.forgetCells)
                {
                    this.forgotten.Add(prop);
                }
            }

            foreach (var prop in this.forgotten)
            {
                this.saidProps.Remove(prop);
            }

            var nearest = props.Where(prop => Vector2.Distance(player, prop.Grid) <= this.reachCells)
                .OrderBy(prop => Vector2.Distance(player, prop.Grid))
                .FirstOrDefault(prop => !this.saidProps.ContainsKey(prop.Grid));
            if (nearest.Kind != null)
            {
                var another = this.saidProps.ContainsValue(nearest.Kind);
                this.saidProps[nearest.Grid] = nearest.Kind;
                if (!another)
                {
                    say.Add(nearest.Kind);
                }
            }

            return say;
        }
    }
}
