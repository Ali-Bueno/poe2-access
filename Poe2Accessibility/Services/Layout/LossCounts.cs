namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Readings that found a part missing, counted before it is said lost: one bad read is no moved field. Pure.</summary>
    public sealed class MissCount
    {
        private readonly int toSay;
        private int count;

        public MissCount(int toSay) => this.toSay = toSay;

        /// <summary>One more miss; true at the one that makes the count, once (later misses are no news).</summary>
        public bool Once() => ++this.count == this.toSay;

        /// <summary>One more miss; true at that one and every later one, until cleared.</summary>
        public bool Reached() => ++this.count >= this.toSay;

        public void Clear() => this.count = 0;
    }

    /// <summary>
    ///     Misses counted per area (<see cref="LossWitnesses.AreaKey"/>): true at the reading that makes the count in the
    ///     area that makes the number of areas, once; one area's monsters can all lack a record. Pure.
    /// </summary>
    public sealed class AreaMisses
    {
        private readonly MissCount inArea;
        private readonly MissCount areas;
        private long area;

        public AreaMisses(int perArea, int areas) => (this.inArea, this.areas) = (new MissCount(perArea), new MissCount(areas));

        public bool Miss(long areaKey)
        {
            if (areaKey != this.area)
            {
                this.area = areaKey;
                this.inArea.Clear();
            }

            return this.inArea.Once() && this.areas.Once();
        }

        public void Clear()
        {
            this.area = 0;
            this.inArea.Clear();
            this.areas.Clear();
        }
    }

    /// <summary>When a part missed in play is looked for again, and when misses make it lost (docs/compatibility.md, "When a part is said lost"). Pure.</summary>
    public static class PartLoss
    {
        /// <summary>
        ///     Whether the parts missed are looked for again now: with a manager to look from, not before the next look is due,
        ///     and only while something is still missed (a UI part after a miss in play, or a state's layer roots).
        /// </summary>
        public static bool LookAgainDue(bool hasManager, long now, long nextAt, bool missedInGame, bool partsKnown, bool statePending, bool layerRootsKnown) =>
            hasManager && now >= nextAt && ((missedInGame && !partsKnown) || (statePending && !layerRootsKnown));

        /// <summary>
        ///     Whether a field missed <paramref name="misses"/> times in a row is said lost: never one that may only not exist
        ///     yet; at once when the tree shows its element; else once <paramref name="secondArea"/> (asked last: it records the
        ///     area) says another area missed it too.
        /// </summary>
        public static bool AfterMisses(bool mayNotExist, int misses, int toSay, bool shown, Func<bool> secondArea) =>
            !mayNotExist && misses >= toSay && (shown || secondArea());
    }
}
