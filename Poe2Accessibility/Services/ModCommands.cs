namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     What the mod button does. In menus (something has the focus): tap reads the next line of the focused
    ///     element (its announcement, then each tooltip line), double tap the previous one, hold the whole tooltip.
    ///     In gameplay: tap says where the scanner's choice is (or repeats), double tap reads life and mana, hold
    ///     reads the screen and the button hints; holding it and flicking the right stick is the scanner (up/down
    ///     category, left/right entry), holding it and pressing the right stick locks the guide beacon. The game
    ///     sees the same presses (L3 does nothing in gameplay or the inventory; on the world map it toggles the
    ///     legend).
    /// </summary>
    public sealed class ModCommands
    {
        private readonly IAccessibilityOutputService output;
        private readonly FocusTracker focus;
        private readonly UiSemantics semantics;
        private readonly ScreenNarrator narrator;
        private readonly Func<ScreenSnapshot> screen;
        private readonly PluginLog log;
        private readonly EntityCatalog catalog;
        private readonly ScanBrowser browser = new();
        private readonly Beacon? beacon;
        private readonly Func<string?> vitals;
        private IntPtr lineOwner;
        private int lineIndex = -1;

        public ModCommands(IAccessibilityOutputService output, FocusTracker focus, UiSemantics semantics, ScreenNarrator narrator, Func<ScreenSnapshot> screen, EntityCatalog catalog, Beacon? beacon, Func<string?> vitals, PluginLog log)
        {
            this.beacon = beacon;
            this.vitals = vitals;
            this.output = output;
            this.focus = focus;
            this.semantics = semantics;
            this.narrator = narrator;
            this.screen = screen;
            this.catalog = catalog;
            this.log = log;
        }

        public void OnGesture(PadGesture gesture)
        {
            this.log.Debug($"Mod button: {gesture}");
            var current = this.focus.Current;
            if (current != null)
            {
                this.InMenu(gesture, current);
            }
            else
            {
                this.InGameplay(gesture);
            }
        }

        private void InMenu(PadGesture gesture, AccessibleUiNode current)
        {
            var fresh = this.semantics.Describe(current.Address, current.IsHovered) ?? current;
            switch (gesture)
            {
                case PadGesture.Tap:
                    this.SpeakLine(fresh, 1);
                    break;
                case PadGesture.DoubleTap:
                    this.SpeakLine(fresh, -1);
                    break;
                case PadGesture.Hold:
                    this.output.Speak(Announcements.Details(fresh), true);
                    break;
            }
        }

        private void InGameplay(PadGesture gesture)
        {
            switch (gesture)
            {
                case PadGesture.Tap:
                    // Where the scanner's choice is now (distance and turn refreshed); nothing chosen: say it again.
                    if (this.browser.Current(this.catalog.Entries(), Environment.TickCount64) is { } target)
                    {
                        this.output.Speak(this.catalog.Describe(target), true);
                    }
                    else
                    {
                        this.output.Repeat();
                    }

                    break;
                case PadGesture.DoubleTap:
                    if (this.vitals() is { } reading)
                    {
                        this.output.Speak(reading, true);
                    }

                    break;
                case PadGesture.Hold:
                    this.narrator.SpeakScreen(this.screen(), IntPtr.Zero, true);
                    break;
                case PadGesture.StickClick:
                    this.ToggleBeacon();
                    break;
                default:
                    this.Scan(gesture);
                    break;
            }
        }

        private void Scan(PadGesture flick)
        {
            var entries = this.catalog.Entries();
            var now = Environment.TickCount64;
            var answer = flick switch
            {
                PadGesture.FlickUp => this.browser.ChangeCategory(entries, -1, now),
                PadGesture.FlickDown => this.browser.ChangeCategory(entries, 1, now),
                PadGesture.FlickLeft => this.browser.ChangeEntry(entries, -1, now),
                _ => this.browser.ChangeEntry(entries, 1, now),
            };
            if (answer.Entry == null)
            {
                this.output.Speak(Announcements.NothingFound, true);
                return;
            }

            var spoken = this.catalog.Describe(answer.Entry);
            this.output.Speak(answer.CategoryChanged ? Announcements.InContext(Announcements.ScanCategoryName(answer.Entry.Category), spoken) : spoken, true);
        }

        /// <summary>
        ///     Locks the guide on the scanner's choice (D2R: what was spoken last), else on the first thing the quest
        ///     names; the same choice again releases it.
        /// </summary>
        private void ToggleBeacon()
        {
            if (this.beacon == null)
            {
                return;
            }

            var entries = this.catalog.Entries();
            var choice = this.browser.Current(entries, Environment.TickCount64) ??
                         entries.FirstOrDefault(entry => entry.Category == ScanCategory.Quest);
            if (choice == null)
            {
                if (this.beacon.Target != null)
                {
                    this.beacon.Clear(true);
                }
                else
                {
                    this.output.Speak(Announcements.BeaconNeedsChoice, true);
                }

                return;
            }

            this.beacon.Toggle(choice);
        }

        /// <summary>Line by line through the focused element; a new focus starts again from its first line.</summary>
        private void SpeakLine(AccessibleUiNode node, int step)
        {
            var lines = new List<string> { Announcements.Focused(node) };
            lines.AddRange(node.Tooltip);
            if (node.Address != this.lineOwner)
            {
                (this.lineOwner, this.lineIndex) = (node.Address, -1);
            }

            this.lineIndex = Math.Clamp(this.lineIndex + step, 0, lines.Count - 1);
            this.output.Speak(lines[this.lineIndex], true);
        }
    }
}
