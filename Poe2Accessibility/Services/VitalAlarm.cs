namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>
    ///     Low-life warnings for a player who cannot see the globe: each threshold speaks once on the way down and
    ///     re-arms only after the value climbs back past it by a margin, so regeneration hovering at the line does
    ///     not repeat it. Pure: the caller passes the percentage.
    /// </summary>
    public sealed class VitalAlarm
    {
        private readonly int[] thresholds;
        private readonly int rearmMargin;
        private readonly bool[] fired;

        public VitalAlarm(int rearmMargin, params int[] thresholds)
        {
            this.thresholds = thresholds.OrderByDescending(threshold => threshold).ToArray();
            this.rearmMargin = rearmMargin;
            this.fired = new bool[this.thresholds.Length];
        }

        /// <summary>The lowest threshold this value newly fell below, or null (one warning even for a big hit).</summary>
        public int? Update(int percent)
        {
            int? crossed = null;
            for (var i = 0; i < this.thresholds.Length; i++)
            {
                if (percent >= this.thresholds[i] + this.rearmMargin)
                {
                    this.fired[i] = false;
                }
                else if (percent < this.thresholds[i] && !this.fired[i])
                {
                    this.fired[i] = true;
                    crossed = this.thresholds[i];
                }
            }

            return crossed;
        }
    }

    /// <summary>On at or under <see cref="Below"/>, off once life is <see cref="ReleasePoints"/> above it. Pure.</summary>
    public sealed class HeartbeatGate
    {
        public const int ReleasePoints = 5;

        private bool on;

        public int Below { get; set; } = 25;

        public bool Update(int? lifePercent)
        {
            if (lifePercent is not { } life || life <= 0)
            {
                this.on = false;
            }
            else if (life <= this.Below)
            {
                this.on = true;
            }
            else if (life >= this.Below + ReleasePoints)
            {
                this.on = false;
            }

            return this.on;
        }
    }
}
