namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Where along a trail the player stands, from the end away from the goal.</summary>
    public enum TrailPart
    {
        Start,
        Middle,
        End,
    }

    /// <summary>What a trail has to say.</summary>
    public enum TrailNews
    {
        None,

        /// <summary>Come upon, or come back to after long away.</summary>
        Found,

        /// <summary>Followed into its next part.</summary>
        Part,

        /// <summary>The player walks the wrong way along it.</summary>
        Away,

        /// <summary>The player walked off it.</summary>
        Left,
    }

    /// <summary>
    ///     A player along one trail: whether they are on it, which part of it, and whether they walk toward its goal. A turn is
    ///     one only past <see cref="TurnCells"/>, so a weave between rocks is none. Pure.
    /// </summary>
    public sealed class TrailFollower
    {
        /// <summary>Walked this far the other way, the player has turned: about half a screen.</summary>
        public const int TurnCells = 40;

        /// <summary>This much nearer the goal, the player is following.</summary>
        public const int FollowCells = 20;

        /// <summary>Off the trail this long, the player left it; a step round a tree is not leaving.</summary>
        public const int LeftMs = 3000;

        /// <summary>Away this long, coming back to the trail is news again.</summary>
        public const int AgainMs = 60000;

        /// <summary>Between two warnings of the same trail.</summary>
        public const int WarnMs = 20000;

        /// <summary>The sound shows the way for this long after the trail is found, before the player has walked.</summary>
        public const int LeadInMs = 5000;

        /// <summary>A part is entered this share of the trail past its line, so a walk along the line says it once.</summary>
        private const float PartMargin = 0.04f;

        private bool found;
        private bool left;
        private bool toward;
        private bool awaySaid;
        private bool walkedOn;
        private int extreme;
        private long onAt;
        private long foundAt;
        private long warnedAt = long.MinValue / 2;

        /// <summary>The part the player is in.</summary>
        public TrailPart Part { get; private set; }

        /// <summary>Come upon since the area was entered.</summary>
        public bool Known => this.found;

        /// <summary>The player walks toward the goal on the trail, or has just found it: the sound leads.</summary>
        public bool Leads(long now) => this.found && !this.left && (this.toward || now - this.foundAt < LeadInMs);

        /// <param name="walk">Cells to walk to the goal; null with no way there.</param>
        /// <param name="sees">The player is near enough to notice the trail.</param>
        /// <param name="on">The player walks on it.</param>
        /// <param name="ends">The walk from the trail's near and far ends.</param>
        public TrailNews Update(long now, int? walk, bool sees, bool on, (int Near, int Far) ends)
        {
            if (walk is not { } cells)
            {
                return TrailNews.None;
            }

            var share = ends.Far > ends.Near ? Math.Clamp((float)(cells - ends.Near) / (ends.Far - ends.Near), 0f, 1f) : 0f;
            if (!this.found || (this.left && now - this.onAt >= AgainMs))
            {
                if (!sees)
                {
                    return TrailNews.None;
                }

                (this.found, this.left, this.toward, this.awaySaid, this.walkedOn) = (true, false, false, false, false);
                (this.foundAt, this.onAt, this.extreme) = (now, now, cells);
                this.Part = PartOf(share);
                return TrailNews.Found;
            }

            if (!on)
            {
                if (this.left || now - this.onAt < LeftMs)
                {
                    return TrailNews.None;
                }

                // Seen in passing and never walked on: nothing to leave.
                (this.left, this.toward) = (true, false);
                return this.walkedOn && this.Warn(now) ? TrailNews.Left : TrailNews.None;
            }

            this.onAt = now;
            this.walkedOn = true;
            if (this.left)
            {
                // Back soon after: no line, only the sound once they walk on.
                (this.left, this.extreme, this.awaySaid) = (false, cells, false);
            }

            if (this.Turned(cells) && !this.toward && !this.awaySaid)
            {
                this.awaySaid = true;
                return this.Warn(now) ? TrailNews.Away : TrailNews.None;
            }

            var part = this.Entered(share);
            var onward = part > this.Part;
            this.Part = part;
            return onward && this.toward ? TrailNews.Part : TrailNews.None;
        }

        private static TrailPart PartOf(float share) => share > 2f / 3 ? TrailPart.Start : share > 1f / 3 ? TrailPart.Middle : TrailPart.End;

        /// <summary>Whether the player has gone far enough one way to tell which, kept in <see cref="toward"/>.</summary>
        private bool Turned(int cells)
        {
            if (this.toward)
            {
                this.extreme = Math.Min(this.extreme, cells);
                if (cells < this.extreme + TurnCells)
                {
                    return false;
                }

                (this.toward, this.extreme) = (false, cells);
                return true;
            }

            if (cells <= this.extreme - FollowCells)
            {
                (this.toward, this.extreme, this.awaySaid) = (true, cells, false);
                return true;
            }

            if (cells < this.extreme + TurnCells)
            {
                return false;
            }

            this.extreme = cells;
            return true;
        }

        /// <summary>The part, the one before until its line is clearly past.</summary>
        private TrailPart Entered(float share)
        {
            var part = PartOf(share);
            return PartOf(share + PartMargin) == part && PartOf(share - PartMargin) == part ? part : this.Part;
        }

        private bool Warn(long now)
        {
            if (now - this.warnedAt < WarnMs)
            {
                return false;
            }

            this.warnedAt = now;
            return true;
        }
    }
}
