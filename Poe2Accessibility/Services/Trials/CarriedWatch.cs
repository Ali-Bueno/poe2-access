namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>
    ///     Tells when the player was carried to ground apart, as by the stairs between the rooms of a Trial's floor, which load
    ///     no area: farther between two looks than feet carry, and from ground the player can no longer walk to. Pure.
    /// </summary>
    public sealed class CarriedWatch
    {
        /// <summary>What the player sees around in grid cells: a walk, a roll or a blink between two looks stays well inside it.</summary>
        public const float FartherThanFeet = ExplorationMap.SeenRadiusCells;

        private Vector2? last;

        /// <summary>A new area: its first place is no place carried to.</summary>
        public void Reset() => this.last = null;

        /// <summary>True on the look that finds the player carried to other ground.</summary>
        /// <param name="cell">Where the player is, in grid cells.</param>
        /// <param name="walkable">Whether the player can walk to a cell from where they are now; null while not known.</param>
        public bool Update(Vector2 cell, Func<Vector2, bool?> walkable)
        {
            var before = this.last;
            this.last = cell;
            return before is { } from && Vector2.Distance(from, cell) > FartherThanFeet && walkable(from) != true;
        }
    }
}
