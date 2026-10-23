namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>What the player did with the mod button (L3), alone or with the right stick.</summary>
    public enum PadGesture
    {
        Tap,
        DoubleTap,
        Hold,
        FlickUp,
        FlickDown,
        FlickLeft,
        FlickRight,

        /// <summary>The right stick pressed (R3) while L3 is held: lock or release the guide beacon.</summary>
        StickClick,

        /// <summary>L3 held for a second and a half: the mod settings menu.</summary>
        LongHold,
    }

    /// <summary>
    ///     Turns per-frame samples of L3 and the right stick into gestures. A tap is reported once the
    ///     double-tap window has passed without a second press; a hold fires while still held, unless the right
    ///     stick was flicked during the press (flicks are the scanner, the press only enables them). Pure: the
    ///     caller passes the clock.
    /// </summary>
    public sealed class L3Gestures
    {
        public const int HoldMs = 500;

        public const int DoubleTapMs = 300;

        public const int LongHoldMs = 1500;

        /// <summary>The settings menu is open: flicks are read without holding L3.</summary>
        public bool FlicksWithoutL3 { get; set; }

        /// <summary>A flick counts past this deflection and re-arms once the stick is back under the release.</summary>
        private const float FlickPress = 0.7f;

        private const float FlickRelease = 0.3f;

        private bool down;
        private long downAt;
        private bool holdFired;
        private bool flicked;
        private bool stickOut;
        private bool r3Down;
        private bool longHoldFired;
        private bool freeStickOut;
        private long? pendingTapAt;

        public IReadOnlyList<PadGesture> Update(long nowMs, bool l3, float rightX, float rightY, bool r3 = false)
        {
            var gestures = new List<PadGesture>();
            var r3Pressed = r3 && !this.r3Down;
            this.r3Down = r3;
            if (this.FlicksWithoutL3 && !l3)
            {
                var free = Math.Max(Math.Abs(rightX), Math.Abs(rightY));
                if (!this.freeStickOut && free >= FlickPress)
                {
                    this.freeStickOut = true;
                    gestures.Add(Math.Abs(rightY) >= Math.Abs(rightX)
                        ? rightY > 0 ? PadGesture.FlickUp : PadGesture.FlickDown
                        : rightX > 0 ? PadGesture.FlickRight : PadGesture.FlickLeft);
                }
                else if (this.freeStickOut && free <= FlickRelease)
                {
                    this.freeStickOut = false;
                }
            }

            if (!l3 && this.pendingTapAt is { } tapAt && nowMs - tapAt > DoubleTapMs)
            {
                gestures.Add(PadGesture.Tap);
                this.pendingTapAt = null;
            }

            if (l3 && !this.down)
            {
                (this.down, this.downAt, this.holdFired, this.flicked, this.stickOut, this.longHoldFired) = (true, nowMs, false, false, false, false);
            }
            else if (l3)
            {
                if (r3Pressed)
                {
                    this.flicked = true;
                    this.pendingTapAt = null;
                    gestures.Add(PadGesture.StickClick);
                }

                var magnitude = Math.Max(Math.Abs(rightX), Math.Abs(rightY));
                if (!this.stickOut && magnitude >= FlickPress)
                {
                    this.stickOut = true;
                    this.flicked = true;
                    this.pendingTapAt = null;
                    gestures.Add(Math.Abs(rightY) >= Math.Abs(rightX)
                        ? rightY > 0 ? PadGesture.FlickUp : PadGesture.FlickDown
                        : rightX > 0 ? PadGesture.FlickRight : PadGesture.FlickLeft);
                }
                else if (this.stickOut && magnitude <= FlickRelease)
                {
                    this.stickOut = false;
                }

                if (!this.holdFired && !this.flicked && nowMs - this.downAt >= HoldMs)
                {
                    this.holdFired = true;
                    this.pendingTapAt = null;
                    gestures.Add(PadGesture.Hold);
                }

                if (this.holdFired && !this.longHoldFired && !this.flicked && nowMs - this.downAt >= LongHoldMs)
                {
                    this.longHoldFired = true;
                    gestures.Add(PadGesture.LongHold);
                }
            }
            else if (this.down)
            {
                this.down = false;
                if (!this.holdFired && !this.flicked)
                {
                    if (this.pendingTapAt != null)
                    {
                        this.pendingTapAt = null;
                        gestures.Add(PadGesture.DoubleTap);
                    }
                    else
                    {
                        this.pendingTapAt = nowMs;
                    }
                }
            }

            return gestures;
        }
    }
}
