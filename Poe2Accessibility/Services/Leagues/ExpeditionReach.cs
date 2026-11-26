namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     How far an Expedition charge blows and how far from the last one the next can go, in grid units. No file of the game
    ///     holds them; they were measured in the game by the authors of the open-source ExpeditionIcons plugin for Path of Exile 2
    ///     (github.com/exCore2/ExpeditionIcons, ExpeditionIcons.cs, read 2026-11-14: circles made to touch, 27.95 and 27.23 apart
    ///     halves in maps and 33.94 in a logbook; charges in a line at full reach, 90.14 and 108). The area's own increase
    ///     (<c>map_expedition_explosion_radius_+%</c>, 10 to 40 % on a logbook) is not read, so a reach here is the least it can be:
    ///     the game's own state of a find (<see cref="LeaguePieces.CoveredState"/>) is believed first, this only when it says nothing.
    ///     Pure, but for the set last told.
    /// </summary>
    public static class ExpeditionReach
    {
        public const int MapRadius = 28;
        public const int LogbookRadius = 34;
        public const int MapRange = 90;
        public const int LogbookRange = 108;

        /// <summary>The start of a logbook area's id (WorldAreas: ExpeditionLogBook_Peninsula … _Prairie).</summary>
        private const string LogbookAreas = "ExpeditionLogBook_";

        private static volatile HashSet<uint> covered = new();

        public static bool IsLogbook(string areaId) => areaId.StartsWith(LogbookAreas, StringComparison.OrdinalIgnoreCase);

        /// <summary>The least radius of one charge's blast in the area.</summary>
        public static int Radius(string areaId) => IsLogbook(areaId) ? LogbookRadius : MapRadius;

        /// <summary>The least distance the next charge can stand from the one before.</summary>
        public static int Range(string areaId) => IsLogbook(areaId) ? LogbookRange : MapRange;

        /// <summary>Whether a charge at a place reaches a find, by their grid places.</summary>
        public static bool Reaches(Vector2 charge, Vector2 find, int radius) => Vector2.Distance(charge, find) <= radius;

        /// <summary>Whether any of the charges reaches the find.</summary>
        public static bool AnyReaches(IReadOnlyList<Vector2> charges, Vector2 find, int radius)
        {
            foreach (var charge in charges)
            {
                if (Reaches(charge, find, radius))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the charges placed reach this find, as last looked: for the scanner's name of it.</summary>
        public static bool IsCovered(uint id) => covered.Contains(id);

        /// <summary>The finds the charges placed reach now; the set is not touched again once given.</summary>
        public static void Publish(HashSet<uint> now) => covered = now;

        private static volatile bool detonated;

        /// <summary>
        ///     Gets a value indicating whether the area's detonator has blown: its markers are spent then, blown or not, though they stay
        ///     in the ground with the states they had (live 2026-11-26: still said as "Monstruos de expedición" after the encounter).
        /// </summary>
        public static bool Detonated => detonated;

        /// <summary>The area's detonator as last read: blown or not.</summary>
        public static void PublishDetonated(bool now) => detonated = now;
    }
}
