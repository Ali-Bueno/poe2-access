namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>What a radar ray finds ahead.</summary>
    public enum RadarReading
    {
        Unknown,
        Open,
        Exit,
        Wall,
    }

    /// <summary>Distances in cells and times in ms that decide a ray's reading (see docs/radar.md).</summary>
    public sealed record RadarThresholds(float OpenCells, float CloseCells, float ConfirmCells, long StillMs, long StillConfirmMs, long OpeningIntervalMs);

    /// <summary>
    ///     One radar ray's state (D2R radar.cpp Sensor): a reading sounds only once it is confirmed — the player walked
    ///     on with it, or stood still for a moment — and only when it changed. The first reading after a reset is a
    ///     silent baseline. Openings keep a minimum interval and are dropped while the player only backs away from the
    ///     wall last heard; walls are never delayed.
    /// </summary>
    public sealed class RadarSensor
    {
        private readonly RadarThresholds limits;
        private RadarReading confirmed;
        private RadarReading candidate;
        private RadarReading? deferred;
        private RadarReading lastCue;
        private Vector2 candidateOrigin;
        private long candidateAt;
        private Vector2 lastPosition;
        private long lastMoveAt;
        private long lastOpeningAt = long.MinValue / 2;
        private (float Projection, Vector2 Origin)? frontier;
        private bool baseline = true;

        public RadarSensor(RadarThresholds limits) => this.limits = limits;

        public RadarReading Confirmed => this.confirmed;

        public void Reset()
        {
            this.confirmed = this.candidate = this.lastCue = RadarReading.Unknown;
            this.deferred = null;
            this.frontier = null;
            this.baseline = true;
            this.lastOpeningAt = long.MinValue / 2;
        }

        /// <summary>One poll of this ray; returns the reading to sound now, if any.</summary>
        /// <param name="position">Player position in cells.</param>
        /// <param name="axis">The ray's unit direction in cells.</param>
        public RadarReading? Update(long now, Vector2 position, Vector2 axis, PassageScan.Result scan, bool exitAhead)
        {
            if (Vector2.Distance(position, this.lastPosition) > 0)
            {
                this.lastMoveAt = now;
                this.lastPosition = position;
            }

            if (scan.Depth < this.limits.OpenCells && scan.Unknown)
            {
                this.candidate = RadarReading.Unknown; // the grid ends before the opening depth: no verdict
                return this.Deliver(now, position, axis, scan);
            }

            var observed = scan.Depth <= this.limits.CloseCells || (scan.Depth < this.limits.OpenCells && this.confirmed == RadarReading.Wall)
                ? RadarReading.Wall
                : exitAhead ? RadarReading.Exit : RadarReading.Open;
            if (this.baseline)
            {
                this.baseline = false;
                this.confirmed = this.lastCue = observed;
                return null;
            }

            if (observed == this.confirmed)
            {
                this.candidate = RadarReading.Unknown;
            }
            else
            {
                if (observed != this.candidate)
                {
                    this.candidate = observed;
                    this.candidateOrigin = position;
                    this.candidateAt = now;
                }

                var walkedOn = Vector2.Distance(position, this.candidateOrigin) >= this.limits.ConfirmCells;
                var stoodStill = now - this.lastMoveAt >= this.limits.StillMs && now - this.candidateAt >= this.limits.StillConfirmMs;
                if (walkedOn || stoodStill)
                {
                    this.confirmed = observed;
                    this.candidate = RadarReading.Unknown;
                    this.deferred = observed;
                    if (observed == RadarReading.Wall)
                    {
                        this.frontier = (Vector2.Dot(position, axis) + scan.Depth, position);
                    }
                }
            }

            return this.Deliver(now, position, axis, scan);
        }

        private RadarReading? Deliver(long now, Vector2 position, Vector2 axis, PassageScan.Result scan)
        {
            if (this.deferred is not { } cue)
            {
                return null;
            }

            if (cue != RadarReading.Wall)
            {
                if (this.frontier is { } wall)
                {
                    // Backing away from the wall just heard reads open again without any new way ahead: not news.
                    var lateral = MathF.Abs(Vector2.Dot(position - wall.Origin, new Vector2(-axis.Y, axis.X)));
                    var reach = Vector2.Dot(position, axis) + scan.Depth;
                    if (lateral <= 2 * this.limits.OpenCells && reach < wall.Projection + ((this.limits.OpenCells - this.limits.CloseCells) / 2))
                    {
                        this.deferred = null;
                        return null;
                    }
                }

                if (cue == this.lastCue)
                {
                    this.deferred = null;
                    return null;
                }

                if (now - this.lastOpeningAt < this.limits.OpeningIntervalMs)
                {
                    return null; // held, not dropped
                }

                this.lastOpeningAt = now;
            }

            this.deferred = null;
            this.lastCue = cue;
            return cue;
        }
    }
}
