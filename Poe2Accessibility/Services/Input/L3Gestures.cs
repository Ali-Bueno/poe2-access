namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What the player did with the mod button (L3), alone or with the right stick, or with the left trigger and R3.</summary>
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

        /// <summary>The right stick pressed (R3) while the left trigger is held, without L3: a glance at the map.</summary>
        TriggerClick,
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

        /// <summary>Gets when the mod button last went down.</summary>
        public long PressedAt => this.downAt;

        /// <summary>
        ///     A focus that began with the press or after it came from the press: the game gave the button a job there (a
        ///     stash's "Edit search" opens its field), and the new focus has just been read.
        /// </summary>
        public static bool MovedFocus(long pressedAt, long focusSince) => focusSince >= pressedAt;

        /// <summary>
        ///     Whether a tap or double tap is the game's own press, not the mod's: in a menu, and the focus it moved to began with
        ///     the press (<see cref="MovedFocus"/>), so that focus was just read and its name is not said again.
        /// </summary>
        public static bool GamesPress(PadGesture gesture, bool inMenu, long? pressedAt, long focusSince) =>
            inMenu && gesture is PadGesture.Tap or PadGesture.DoubleTap && pressedAt is { } pressed && MovedFocus(pressed, focusSince);

        /// <summary>
        ///     Whether the game's own legend gives the mod button a job on this screen, so the gesture is the game's and
        ///     the mod does not act on it too (the game sees every press; the mod cannot hide one). A hold the legend
        ///     binds is the game's anywhere (a stash's "Clear search"). In gameplay, where the button is otherwise the
        ///     game's to leave alone, any legend binding takes the whole button. In a menu the tap stays shared as
        ///     before (the world map's legend toggle, a stash's "Edit search": <see cref="GamesPress"/> has the
        ///     focus-moving tap). The map glance is not the mod button and is never taken. (A hideout's editor toggle
        ///     is no legend binding either: there, with its switch on and in gameplay, the trigger is fed as the mod
        ///     button, Poe2AccessibilityCore.Input, and the legend is read for the trigger's glyph, ModCommands.)
        /// </summary>
        public static bool GamesButton(PadGesture gesture, bool inMenu, bool buttonHinted, bool holdHinted) =>
            gesture != PadGesture.TriggerClick &&
            (((gesture is PadGesture.Hold or PadGesture.LongHold) && holdHinted) || (!inMenu && buttonHinted));

        /// <summary>
        ///     Whether the left trigger stands in as the mod button in the core's pad reading: its switch on, a
        ///     hideout, gameplay (menus there keep L3), and in the game at all - through a loading screen the area
        ///     last read says nothing about the one being entered.
        /// </summary>
        public static bool TriggerStandsIn(bool switchOn, bool hideout, bool inMenu, bool inGame) =>
            switchOn && hideout && !inMenu && inGame;

        /// <summary>
        ///     Whether a screen's legend (hint bar) binds a button, from its entries' button texts and the
        ///     button's names (both controller families, and their hold forms): bound when an entry names it - a
        ///     combination naming it ("RT plus L3") counts - and its hold bound when an entry is a hold form. The
        ///     button judged is the one being fed: L3, or the trigger where it stands in.
        /// </summary>
        public static (bool Hinted, bool HoldHinted) LegendBinds(IEnumerable<string> legendButtons, IEnumerable<string> buttonNames, IEnumerable<string> holdNames)
        {
            var buttons = legendButtons.ToList();
            var names = buttonNames.ToList();
            var holds = holdNames.ToList();
            return (buttons.Any(button => names.Any(button.Contains)), buttons.Any(button => holds.Any(hold => button == hold)));
        }

        /// <summary>
        ///     The button feeding these gestures changed (the trigger standing in, or no longer): a press begun on
        ///     the other button is not this one's tap or hold, so the press in flight is forgotten. A button already
        ///     down at the change counts as spent until released: no hold, no tap from a press not begun here.
        /// </summary>
        public void ForgetPress(bool buttonDown)
        {
            this.down = buttonDown;
            this.holdFired = true;
            this.longHoldFired = true;
            this.flicked = true;
            this.pendingTapAt = null;
        }
        private bool holdFired;
        private bool flicked;
        private bool stickOut;
        private bool r3Down;
        private bool longHoldFired;
        private bool freeStickOut;
        private long? pendingTapAt;

        public IReadOnlyList<PadGesture> Update(long nowMs, bool l3, float rightX, float rightY, bool r3 = false, bool leftTrigger = false)
        {
            var gestures = new List<PadGesture>();
            var r3Pressed = r3 && !this.r3Down;
            this.r3Down = r3;
            if (r3Pressed && leftTrigger && !l3)
            {
                gestures.Add(PadGesture.TriggerClick);
            }

            if (r3)
            {
                // The stick pressed in is the game's button (Inspect Mode's Close, held its Toggle Advanced Info), and pressing
                // it pushes the stick too, also the press that opens the mode before its table takes the stick: none of it is
                // a flick until the stick is back at the centre.
                this.freeStickOut = true;
            }

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
