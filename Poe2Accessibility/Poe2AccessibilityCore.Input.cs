namespace Poe2Accessibility
{
    using System;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>The pad as <see cref="Poe2AccessibilityCore"/> reads it each frame: the mod button's gestures, confirm and direction times, and the repeat commands.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        private uint lastPadButtons;

        /// <summary>A stick pushed this far counts as a direction (our threshold: well past rest, before a full push).</summary>
        private const float DirectionDeflection = 0.5f;

        /// <summary>The focus of the previous frame, to notice a text field closing (pre-game focus goes to nothing).</summary>
        private AccessibleUiNode? previousFocus;

        /// <summary>When A (the confirm button) last went down, from the game's own pad state.</summary>
        private long lastConfirmAt = long.MinValue / 2;

        /// <summary>When a direction (d-pad or a stick) was last held, from the game's own pad state.</summary>
        private long lastDirectionAt = long.MinValue / 2;

        private bool confirmDown;

        private readonly BlockedMove blockedMove = new();

        private uint lastDPad;

        /// <summary>When the D-pad press <see cref="blockedMove"/> waits on went down, for what was said since.</summary>
        private DateTime blockedPressAt;

        /// <summary>Per frame: the mod button's gestures from the pad state the game received.</summary>
        private void ReadModButton()
        {
            this.padState = default;
            if (this.pad == null || this.focus == null)
            {
                return;
            }

            if (this.focus.Manager != this.padManager)
            {
                this.padManager = this.focus.Manager;
                this.inputSeat.Baseline();
            }

            GamepadReader.PadState? first = this.pad.TryRead(this.focus.Manager, out var one) ? one : null;
            GamepadReader.PadState? second = this.Settings.ReadPlayer2 && this.pad.TryRead(this.focus.Manager, out var two, 2) ? two : null;
            var seat = this.inputSeat.Update(this.Settings.ReadPlayer1, this.Settings.ReadPlayer2,
                first is { } a ? (a.Buttons, a.Deflection) : null, second is { } b ? (b.Buttons, b.Deflection) : null);
            if (seat != Seats.Input)
            {
                this.log?.Info($"Local co-op: reading player {seat}'s controller and cursor");
                Seats.Input = seat;
                this.focus.Seat = seat;
                this.gestures = new L3Gestures(); // a hold on the other controller is not this one's tap
            }

            if ((seat == 2 ? second : first) is not { } state)
            {
                return;
            }

            this.padState = state;
            // In the passive tree the triggers zoom and Y centres the view.
            var viewMoved = state.IsDown(GameLayout.PadButtons.LeftTrigger | GameLayout.PadButtons.RightTrigger | GameLayout.PadButtons.Y);
            this.semantics?.ObserveStick(state.LeftX, state.LeftY, state.RightX, state.RightY, viewMoved, Environment.TickCount64);
            this.CheckPadButtons(state);
            this.WatchBlockedMove(state);
            if (IsDirection(state))
            {
                this.lastDirectionAt = Environment.TickCount64;
            }

            // A opens screens and popups, LB/RB switch tabs: a focus that follows them waits for the new page.
            var confirm = state.IsDown(GameLayout.PadButtons.A) || state.IsDown(GameLayout.PadButtons.LeftShoulder) ||
                          state.IsDown(GameLayout.PadButtons.RightShoulder);
            if (confirm && !this.confirmDown)
            {
                this.lastConfirmAt = Environment.TickCount64;
                if (!state.IsDown(GameLayout.PadButtons.A))
                {
                    this.narrator?.OnTabKey();
                }
            }

            this.confirmDown = confirm;
            if (!this.Settings.ControllerCommands || this.commands == null)
            {
                return;
            }

            this.FindOnTree(state);
            this.gestures.FlicksWithoutL3 = this.commands.InSettingsMenu;
            this.commands.PressedAt ??= () => this.gestures.PressedAt;
            if (this.commands.InSettingsMenu && this.focus?.Current != null)
            {
                this.commands.CloseSettingsMenu();
            }

            foreach (var gesture in this.gestures.Update(Environment.TickCount64, state.IsDown(GameLayout.PadButtons.LeftStick), state.RightX, state.RightY, state.IsDown(GameLayout.PadButtons.RightStick), state.IsDown(GameLayout.PadButtons.LeftTrigger)))
            {
                this.commands.OnGesture(gesture);
            }
        }

        /// <summary>A D-pad press, which does nothing in the passive tree, goes to the tree's finder.</summary>
        private void FindOnTree(GamepadReader.PadState state)
        {
            var pressed = state.Buttons & ~this.lastPadButtons;
            this.lastPadButtons = state.Buttons;
            var (list, entry) = (pressed & GameLayout.PadButtons.DPadDown) != 0 ? (1, 0)
                : (pressed & GameLayout.PadButtons.DPadUp) != 0 ? (-1, 0)
                : (pressed & GameLayout.PadButtons.DPadRight) != 0 ? (0, 1)
                : (pressed & GameLayout.PadButtons.DPadLeft) != 0 ? (0, -1)
                : (0, 0);
            if (list != 0 || entry != 0)
            {
                this.commands?.OnTreeDirection(list, entry);
            }
        }

        /// <summary>
        ///     A D-pad press that leaves the cursor where it was says its control again (<see cref="BlockedMove"/>). Not the
        ///     sticks, which also scroll and turn previews; not a free cursor, nor the passive tree, whose D-pad is the finder's.
        ///     Not while something is still being said either: a press against the grid's edge said the item's name again
        ///     over its own tooltip, which was lost (user 2026-11-11); speech going on is no silence to break.
        /// </summary>
        private void WatchBlockedMove(GamepadReader.PadState state)
        {
            var dpad = state.Buttons & (GameLayout.PadButtons.DPadUp | GameLayout.PadButtons.DPadDown | GameLayout.PadButtons.DPadLeft | GameLayout.PadButtons.DPadRight);
            var pressed = (dpad & ~this.lastDPad) != 0;
            this.lastDPad = dpad;
            if (!pressed && !this.blockedMove.Waiting)
            {
                return;
            }

            var resting = this.Settings.RepeatOnBlockedMove && !state.IsDown(GameLayout.PadButtons.LeftStick) &&
                          this.focus?.Current is { IsHovered: false, IsPassiveNode: false } current ? current : null;
            var line = resting == null ? string.Empty : Announcements.Focused(resting);
            var now = Environment.TickCount64;
            if (pressed)
            {
                this.blockedMove.Pressed(resting?.Address ?? IntPtr.Zero, line, now);
                this.blockedPressAt = DateTime.UtcNow;
            }
            else if (this.blockedMove.Due(resting?.Address ?? IntPtr.Zero, line, this.output.SaidSince(string.Empty, this.blockedPressAt) || this.output.Speaking, now))
            {
                this.log?.Info("A direction moved nothing: the focus again");
                this.output.Moved(line);
            }
        }

        /// <summary>A d-pad direction or a pushed stick: what moves the cursor in a panel.</summary>
        private static bool IsDirection(GamepadReader.PadState state) =>
            state.IsDown(GameLayout.PadButtons.DPadUp | GameLayout.PadButtons.DPadDown | GameLayout.PadButtons.DPadLeft | GameLayout.PadButtons.DPadRight) ||
            MathF.Max(MathF.Max(MathF.Abs(state.LeftX), MathF.Abs(state.LeftY)), MathF.Max(MathF.Abs(state.RightX), MathF.Abs(state.RightY))) >= DirectionDeflection;

        /// <summary>
        ///     A text field gave the focus up (pre-game the controller cursor then rests on nothing, D2R "B ends editing"):
        ///     say what it holds and that editing ended, since nothing else changes on screen for the player.
        /// </summary>
        private void WatchTextFieldClosing()
        {
            var current = this.focus?.Current;
            if (current == null && this.previousFocus is { Role: UiRole.TextField } field &&
                this.gameState?.CurrentState != GameStateTypes.InGameState)
            {
                this.output.Speak(Announcements.EditFinished(field.Value), true);
            }

            this.previousFocus = current;
        }

        private void RepeatFocus()
        {
            if (this.focus?.Current != null)
            {
                this.output.Speak(Announcements.Focused(this.focus.Current, this.Settings.ReadTooltipsOnFocus), true);
            }
        }

        private void RepeatLast()
        {
            if (this.focus?.Current != null)
            {
                this.RepeatFocus();
            }
            else
            {
                this.output.Repeat();
            }
        }
    }
}
