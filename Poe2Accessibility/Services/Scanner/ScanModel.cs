namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>Scanner categories in browsing order: first how to leave, then where to go, then who and what is around.</summary>
    public enum ScanCategory
    {
        /// <summary>What the quest tracker names (copies of entries of the other categories).</summary>
        Quest,
        Exits,

        /// <summary>What the minimap marks with a name: bosses' arenas, secrets, quest landmarks (<see cref="MinimapMarks"/>).</summary>
        PointsOfInterest,

        /// <summary>Named map pieces and props of the terrain files (bridges, stairs, a cart, a well: <see cref="PlaceNarrator"/>).</summary>
        Places,

        /// <summary>Ground never seen yet next to ground walked (D2R unexplored areas), one entry per run and direction.</summary>
        Unexplored,
        Waypoints,

        /// <summary>League devices the player uses: the Ritual altar (to start a ritual, then to spend its tribute).</summary>
        Encounters,
        Npcs,
        Enemies,
        Objects,
        Chests,
        Items,
        Players,
    }

    /// <summary>One thing the scanner can name; positions in world units; <c>Kind</c> picks its sound ("gold", "chest", "checkpoint").</summary>
    /// <param name="BaseId">For an item, its base type id (the game picks its drop sound by it).</param>
    /// <param name="Loot">For an item, its worth for the loot filter.</param>
    /// <param name="Sound">For a breakable, its material's sound (<see cref="Breakables"/>), empty for the common one.</param>
    /// <param name="QuestMarked">The minimap draws the quest's own icon on it (<see cref="MinimapMarks.IsQuestIcon"/>).</param>
    /// <param name="Timed">What a clock is running against, to be heard from afar: a Death Crystal.</param>
    /// <param name="Subject">For a boss room, whom or what its label names ("Asinia"): what an objective would name.</param>
    /// <param name="About">What the game writes under the object's title, said once with its button (<see cref="Announcements.ObjectDescription"/>).</param>
    public sealed record ScanEntry(uint Id, ScanCategory Category, string Name, float WorldX, float WorldY, float WorldZ, string Kind = "", string BaseId = "",
        LootTier Loot = LootTier.Valuable, string Sound = "", bool QuestMarked = false, string Subject = "", bool Timed = false, string About = "")
    {
        public const string PortalKind = "portal";

        /// <summary>It may be gone by the time the player is back: opened, picked up, a portal that closed or a quest's object once used.</summary>
        public bool CanVanish => this.Category is ScanCategory.Chests or ScanCategory.Items || this.Kind == PortalKind || this.QuestMarked;
    }

    /// <summary>
    ///     The eight directions the left stick walks, for a world offset. The stick's right is the world direction
    ///     that projects to screen right, its up the world perpendicular that projects to screen up; the offset is
    ///     binned in that world frame, not on screen, because the isometric squash would make left/right sectors
    ///     far wider than up/down (D2R: a doorway on a world axis was called "east" and the player walked into the
    ///     wall). Octants: 0 right, 1 up-right, 2 up, 3 up-left, 4 left, 5 down-left, 6 down, 7 down-right.
    /// </summary>
    public static class StickDirection
    {
        /// <param name="worldXOnScreen">Screen offset (pixels, y down) of one world unit along +X.</param>
        /// <param name="worldYOnScreen">Screen offset of one world unit along +Y.</param>
        public static int Octant(Vector2 worldXOnScreen, Vector2 worldYOnScreen, float dx, float dy) =>
            (int)(((Math.Round(Angle(worldXOnScreen, worldYOnScreen, dx, dy) / 45) % 8) + 8) % 8);

        /// <summary>
        ///     As <see cref="Octant(Vector2, Vector2, float, float)"/>, but a way within <paramref name="margin"/> degrees past
        ///     the edge of <paramref name="keep"/>'s octant stays <paramref name="keep"/>: a route running near the 22.5° edge
        ///     between two directions was said "up 2, then up left" and "up left 2, then up" on alternate asks as the player
        ///     walked it; the direction said last is kept until the way is clearly past the edge.
        /// </summary>
        public static int Octant(Vector2 worldXOnScreen, Vector2 worldYOnScreen, float dx, float dy, int? keep, double margin)
        {
            var angle = Angle(worldXOnScreen, worldYOnScreen, dx, dy);
            if (keep is { } kept)
            {
                var off = Math.Abs(((((angle - (kept * 45)) % 360) + 540) % 360) - 180);
                if (off <= 22.5 + margin)
                {
                    return kept;
                }
            }

            return (int)(((Math.Round(angle / 45) % 8) + 8) % 8);
        }

        /// <summary>The offset's angle in degrees, counter-clockwise from the stick's right (up is 90).</summary>
        private static double Angle(Vector2 worldXOnScreen, Vector2 worldYOnScreen, float dx, float dy)
        {
            var right = new Vector2(worldYOnScreen.Y, -worldXOnScreen.Y);
            if (OnScreen(right, worldXOnScreen, worldYOnScreen).X < 0)
            {
                right = -right;
            }

            right = Vector2.Normalize(right);
            var up = new Vector2(-right.Y, right.X);
            if (OnScreen(up, worldXOnScreen, worldYOnScreen).Y > 0)
            {
                up = -up;
            }

            return Math.Atan2((dx * up.X) + (dy * up.Y), (dx * right.X) + (dy * right.Y)) * 180 / Math.PI;
        }

        /// <summary>The unit world direction that the camera draws as <paramref name="screen"/> (x right, y down); null if degenerate.</summary>
        public static Vector2? WorldDirection(Vector2 screen, Vector2 worldXOnScreen, Vector2 worldYOnScreen)
        {
            var determinant = (worldXOnScreen.X * worldYOnScreen.Y) - (worldYOnScreen.X * worldXOnScreen.Y);
            if (MathF.Abs(determinant) < float.Epsilon)
            {
                return null;
            }

            var world = new Vector2(
                (screen.X * worldYOnScreen.Y) - (worldYOnScreen.X * screen.Y),
                (worldXOnScreen.X * screen.Y) - (screen.X * worldXOnScreen.Y)) / determinant;
            return world.LengthSquared() > 0 ? Vector2.Normalize(world) : null;
        }

        private static Vector2 OnScreen(Vector2 world, Vector2 worldXOnScreen, Vector2 worldYOnScreen) =>
            (world.X * worldXOnScreen) + (world.Y * worldYOnScreen);
    }

    /// <summary>What a scanner flick selected: an entry, with its category named when the category changed.</summary>
    public sealed record ScanAnswer(ScanEntry? Entry, bool CategoryChanged);

    /// <summary>
    ///     Scanner browsing (D2R / LeAccess rules): up/down change category (empty ones skipped), left/right move
    ///     through the category nearest first; both wrap. The selection follows the entity, not the index, so the
    ///     list may re-sort under it; after <see cref="StaleMs"/> without a question it restarts at the nearest.
    ///     Pure: the caller passes the entries (nearest first) and the clock.
    /// </summary>
    public sealed class ScanBrowser
    {
        public const int StaleMs = 10000;

        private ScanCategory? category;
        private uint? selected;
        private long lastAnswerAt = long.MinValue / 2;
        private (long Address, string Hash) area = (0, string.Empty);

        /// <summary>
        ///     Forgets the choice (the category is kept): an id names a thing in one area instance only. Tiles and unexplored
        ///     ground are numbered from the same base in every area, and entity ids start over in a new instance.
        /// </summary>
        public void Reset()
        {
            this.selected = null;
            this.lastAnswerAt = long.MinValue / 2;
        }

        /// <summary>The area instance the entries passed from now on belong to; another one than before forgets the choice.</summary>
        public void InArea(long address, string hash)
        {
            if ((address, hash) != this.area)
            {
                this.area = (address, hash);
                this.Reset();
            }
        }

        /// <param name="step">+1 next, -1 previous.</param>
        public ScanAnswer ChangeCategory(IReadOnlyList<ScanEntry> entries, int step, long nowMs)
        {
            var present = Enum.GetValues<ScanCategory>().Where(candidate => entries.Any(entry => entry.Category == candidate)).ToList();
            if (present.Count == 0)
            {
                return this.Answer(null, false, nowMs);
            }

            var index = this.category is { } current ? present.IndexOf(current) : -1;
            var next = index < 0 ? (step > 0 ? 0 : present.Count - 1) : (((index + step) % present.Count) + present.Count) % present.Count;
            this.category = present[next];
            return this.Answer(entries.First(entry => entry.Category == this.category), true, nowMs);
        }

        public ScanAnswer ChangeEntry(IReadOnlyList<ScanEntry> entries, int step, long nowMs)
        {
            if (this.category == null || !entries.Any(entry => entry.Category == this.category))
            {
                return this.ChangeCategory(entries, 1, nowMs);
            }

            var inCategory = entries.Where(entry => entry.Category == this.category).ToList();
            var index = nowMs - this.lastAnswerAt >= StaleMs ? -1 : inCategory.FindIndex(entry => entry.Id == this.selected);
            var next = index < 0 ? 0 : (((index + step) % inCategory.Count) + inCategory.Count) % inCategory.Count;
            return this.Answer(inCategory[next], false, nowMs);
        }

        /// <summary>
        ///     The selected entry, refreshed from <paramref name="entries"/> (where it is now), however long ago it was
        ///     chosen: the player walks to it asking again. Null when nothing is selected or it is gone (chest opened).
        /// </summary>
        public ScanEntry? Current(IReadOnlyList<ScanEntry> entries, long nowMs)
        {
            var entry = entries.FirstOrDefault(candidate => candidate.Id == this.selected);
            if (entry != null)
            {
                this.lastAnswerAt = nowMs;
            }

            return entry;
        }

        private ScanAnswer Answer(ScanEntry? entry, bool categoryChanged, long nowMs)
        {
            this.selected = entry?.Id;
            this.lastAnswerAt = nowMs;
            return new ScanAnswer(entry, categoryChanged);
        }
    }
}
