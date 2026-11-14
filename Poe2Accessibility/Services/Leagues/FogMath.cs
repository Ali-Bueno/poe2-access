namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The speeds of the Delirium fog for an area's level, a row of AfflictionBalancePerLevel as the game uses it.</summary>
    /// <param name="FrontSpeed">The front's speed once its ramp is over, cells per second (row +0x04).</param>
    /// <param name="RearSpeed">The speed the rear starts at (+0x08).</param>
    /// <param name="RearDelay">Seconds before the rear starts (+0x0C).</param>
    /// <param name="RearGain">The rear's acceleration (+0x10).</param>
    /// <param name="Ramp">Seconds the front takes to slow from its first speed to <paramref name="FrontSpeed"/> (+0x34).</param>
    /// <param name="FirstSpeed">The front's speed as the mirror is walked through (+0x38).</param>
    public readonly record struct FogBalance(float FrontSpeed, float RearSpeed, float RearDelay, float RearGain, float Ramp, float FirstSpeed);

    /// <summary>The fog's clocks as the client keeps them, and what the area adds to the rear.</summary>
    /// <param name="Time">The front's clock, seconds; below 0 before the start.</param>
    /// <param name="RearTime">The rear's clock, which stops while the fog is paused.</param>
    /// <param name="End">When the encounter ended on the rear's clock; infinite while it runs.</param>
    /// <param name="GainFactor">What the area multiplies the rear's acceleration by.</param>
    /// <param name="ExtraDelay">Seconds the area adds to the rear's delay.</param>
    /// <param name="Never">The fog never fades (a map's modifier).</param>
    public readonly record struct FogClocks(float Time, float RearTime, float End, float GainFactor, float ExtraDelay, bool Never);

    /// <summary>
    ///     The Delirium fog as the client computes it (exe of 0.5.5f, functions 0x1F66EF0, 0x1F66F60 and 0x1F66820, ported line by
    ///     line: docs/research/exe-layouts.md, "The Delirium fog"). The fog is no circle: every place has a depth, its path distance
    ///     in grid cells from the mirror, and the fog is the band of depths between a rear and a front that both move on clocks.
    ///     Single precision in the game's own order of operations. Pure.
    /// </summary>
    public static class FogMath
    {
        /// <summary>Grid cells to world units, and back, as the exe holds them.</summary>
        private const float GridToWorld = 10.8695650100708f;
        private const float WorldToGrid = 0.0920000001788f;

        /// <summary>A tile is 23 grid cells; the depth map's cells and nodes are half tiles.</summary>
        public const int TileCells = 23;

        /// <summary>The depth the game gives a place its fog cannot reach.</summary>
        public const int Unreached = 8000;

        /// <summary>Whether six numbers can be a row of the table: the checks a reader makes before believing the pointer.</summary>
        public static bool IsPlausible(FogBalance row) =>
            float.IsFinite(row.FrontSpeed) && float.IsFinite(row.RearSpeed) && float.IsFinite(row.RearDelay) && float.IsFinite(row.RearGain) &&
            float.IsFinite(row.Ramp) && float.IsFinite(row.FirstSpeed) &&
            row.Ramp > 0f && row.Ramp < 3600f && row.FirstSpeed > 0f && row.FirstSpeed < 5000f && row.FrontSpeed > 0f && row.FrontSpeed < 5000f &&
            row.RearSpeed >= 0f && row.RearSpeed < 5000f && row.RearDelay >= 0f && row.RearDelay < 3600f && row.RearGain >= 0f && row.RearGain < 1000f;

        /// <summary>How deep the fog's front has gone.</summary>
        public static float Front(FogBalance row, float time)
        {
            if (time < 0f)
            {
                return 0f;
            }

            var half = ((row.FrontSpeed - row.FirstSpeed) / row.Ramp) * 0.5f;
            return time > row.Ramp
                ? (((half * row.Ramp) * row.Ramp) + (row.FirstSpeed * row.Ramp)) + ((time - row.Ramp) * row.FrontSpeed)
                : ((half * time) * time) + (row.FirstSpeed * time);
        }

        /// <summary>How deep the fog's rear has come: behind it the fog has faded. It never passes the front.</summary>
        public static float Rear(FogBalance row, FogClocks clocks)
        {
            if (clocks.Never)
            {
                return 0f;
            }

            var delay = clocks.ExtraDelay + row.RearDelay;
            var began = delay < clocks.End ? delay : clocks.End;
            var moving = clocks.RearTime - began;
            if (moving < 0f)
            {
                return 0f;
            }

            var gain = row.RearGain * clocks.GainFactor;
            var untilEnd = clocks.End - began;
            var halfGain = gain * 0.5f;
            float rear;
            if (moving > untilEnd)
            {
                // After the end the rear keeps its speed and gains two thousand times faster: the band closes.
                var since = moving - untilEnd;
                rear = ((halfGain * untilEnd) * untilEnd) + (untilEnd * row.RearSpeed);
                rear += ((untilEnd * gain) + row.RearSpeed) * since;
                rear += (((gain * 2000f) * 0.5f) * since) * since;
            }
            else
            {
                rear = ((halfGain * moving) * moving) + (row.RearSpeed * moving);
            }

            var front = Front(row, clocks.Time);
            return front < rear ? front : rear;
        }

        /// <summary>A depth as the game's bar shows it: whole cells, never past the most.</summary>
        public static int Shown(float depth, int most) => float.IsFinite(depth) ? Math.Min((int)depth, most) : most;

        /// <summary>The cell of the depth map a grid place falls in; the map's origin is in tiles.</summary>
        public static (int X, int Y) CellOf(int x, int y, int originX, int originY) =>
            (((2 * (x - (TileCells * originX))) + 10) / TileCells, ((2 * (y - (TileCells * originY))) + 10) / TileCells);

        /// <summary>The node of the distance grid at or before a grid place, along one axis.</summary>
        public static int NodeOf(int cell) => (2 * GridOf(cell)) / TileCells;

        /// <summary>
        ///     The distance from the region's seed at a place, between the four nodes around it (v00 the node itself, v10 the next
        ///     along x, v01 the next along y, v11 both).
        /// </summary>
        public static float Between(float v00, float v10, float v01, float v11, int x, int y)
        {
            var worldX = (x + 0.5f) * GridToWorld;
            var worldY = (y + 0.5f) * GridToWorld;
            var nodeX = ((NodeOf(x) * TileCells) >> 1) + 0.5f;
            var nodeY = ((NodeOf(y) * TileCells) >> 1) + 0.5f;
            var alongX = ((worldX - (nodeX * GridToWorld)) * 2f) / 250f;
            var alongY = ((worldY - (nodeY * GridToWorld)) * 2f) / 250f;
            var top = ((v10 - v00) * alongX) + v00;
            var bottom = ((v11 - v01) * alongX) + v01;
            return ((bottom - top) * alongY) + top;
        }

        /// <summary>
        ///     The depth of a place from its region's own depth and its distance in the region: rounded up and kept between 1 and
        ///     the most; null for a region the fog does not reach.
        /// </summary>
        public static int? DepthOf(float regionDepth, float within, int most)
        {
            if (float.IsPositiveInfinity(regionDepth) || !float.IsFinite(regionDepth) || !float.IsFinite(within))
            {
                return null;
            }

            var depth = (int)MathF.Ceiling(within + regionDepth);
            return most > 0 ? Math.Clamp(depth, 1, most) : Math.Max(depth, 1);
        }

        private static int GridOf(int cell) => (int)MathF.Floor(((cell + 0.5f) * GridToWorld) * WorldToGrid);
    }

    /// <summary>What to tell of the fog.</summary>
    public enum FogLine
    {
        /// <summary>The fog has faded where the player stands: it went on ahead.</summary>
        LeftBehind,

        /// <summary>The player has gone past the front: the fog is coming from behind.</summary>
        Outran,

        /// <summary>The rear is about to reach the player.</summary>
        RearNear,

        /// <summary>The player is at the front's edge.</summary>
        FrontNear,
    }

    /// <summary>
    ///     When to speak of the fog's edges. Nothing is said until what was computed has agreed with the game's own word on
    ///     whether the player is in the fog (the stat the server sets) a few times in a row, well away from either edge: the
    ///     computation comes from the exe alone and has to earn its voice in each encounter. Pure.
    /// </summary>
    public sealed class FogNews
    {
        /// <summary>The rear this near, in cells of depth, is about to leave the player out.</summary>
        public const int RearNearCells = 30;

        /// <summary>The front this near is the player running at its edge.</summary>
        public const int FrontNearCells = 12;

        /// <summary>Only samples this far from both edges judge the computation.</summary>
        public const int JudgedBeyondCells = 10;

        /// <summary>Agreements needed before a word is said.</summary>
        public const int TrustAfter = 5;

        /// <summary>Out of the fog there are four seconds: said again this often.</summary>
        public const int OutsideRepeatMs = 3000;

        public const int NearRepeatMs = 7000;

        private int agreed;
        private int disagreed;
        private FogLine? last;
        private long lastSaid = long.MinValue / 2;

        /// <summary>Gets a value indicating whether the computation has agreed with the game often enough to be spoken from.</summary>
        public bool Trusted => this.agreed >= TrustAfter && this.disagreed * 5 <= this.agreed;

        /// <summary>Gets how the computation has fared against the game's word, for the log.</summary>
        public (int Agreed, int Disagreed) Record => (this.agreed, this.disagreed);

        /// <summary>A new area or encounter: nothing has been judged.</summary>
        public void Reset() => (this.agreed, this.disagreed, this.last, this.lastSaid) = (0, 0, null, long.MinValue / 2);

        /// <summary>The line to say now, or null. <paramref name="inFog"/> is the game's own word, null when unread.</summary>
        public FogLine? Heard(int depth, int front, int rear, int most, bool? inFog, long now)
        {
            var inBand = rear < depth && depth <= front;
            if (inFog is { } said && Math.Abs(depth - rear) > JudgedBeyondCells && Math.Abs(front - depth) > JudgedBeyondCells)
            {
                if (said == inBand)
                {
                    this.agreed++;
                }
                else
                {
                    this.disagreed++;
                }
            }

            if (!this.Trusted)
            {
                return null;
            }

            FogLine? line = !inBand ? (depth <= rear ? FogLine.LeftBehind : FogLine.Outran)
                : rear > 0 && depth - rear < RearNearCells ? FogLine.RearNear
                : front < most && front - depth < FrontNearCells ? FogLine.FrontNear
                : null;
            if (line is not { } heard)
            {
                return null; // the line last said is kept: back at the same edge at once is not said again
            }

            var repeat = heard is FogLine.LeftBehind or FogLine.Outran ? OutsideRepeatMs : NearRepeatMs;
            if (this.last == heard && now - this.lastSaid < repeat)
            {
                return null;
            }

            (this.last, this.lastSaid) = (heard, now);
            return heard;
        }
    }
}
