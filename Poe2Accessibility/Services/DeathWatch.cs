namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     The death screen, read from its own pointer (<see cref="GameLayout.InGameUi.DeathScreenOffset"/>) when the
    ///     screen narrator has not said it soon after the player died: once (2026-10-24) the game focused it but it
    ///     read as hidden, so neither the layer nor its button was spoken and the player waited, dead, unaware of it.
    ///     A hidden screen is said as hidden, with the button the game's hint gives to show it, and so is the screen
    ///     going hidden while the player is dead (Y toggles it and is often a skill button).
    /// </summary>
    public sealed class DeathWatch
    {
        private const int PollMs = 250;

        /// <summary>The screen opened about 0.7 s after the death on four deaths (2026-10-24); past this it was missed.</summary>
        private const int ScreenDueMs = 2000;

        /// <summary>
        ///     A screen gone hidden is said only if it stays so this long: resurrecting hides it too, about a second before
        ///     the area starts loading (2026-10-24).
        /// </summary>
        private const int HiddenSettleMs = 2000;

        /// <summary>Title and buttons sit a few levels under the screen (death_screen > button_layout > button > label).</summary>
        private const int MaxDepth = 5;

        private readonly UiManagerLocator locator;
        private readonly UiElementReader elements;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private long diedAt;
        private bool dead;
        private bool said;
        private bool wasShown;
        private long hiddenSince;
        private long seenAt = long.MinValue / 2;
        private long lastPoll;
        private IntPtr screen;

        public DeathWatch(UiManagerLocator locator, UiElementReader elements, IAccessibilityOutputService output, PluginLog log)
        {
            this.locator = locator;
            this.elements = elements;
            this.output = output;
            this.log = log;
        }

        /// <summary>The screen narrator reported this layer (opened or on top): the death screen needs no fallback.</summary>
        public void Saw(IntPtr layer)
        {
            if (layer != IntPtr.Zero && layer == this.screen)
            {
                this.seenAt = Environment.TickCount64;
            }
        }

        /// <summary>The player has just died (once per death): the place is kept for the scanner.</summary>
        public Action? Died { get; set; }

        /// <summary>Per frame in game.</summary>
        public void Tick(IntPtr inGameState)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs ||
                !Seats.Followed.TryGetComponent<Life>(out var life) || life.Health.Total <= 0)
            {
                return;
            }

            this.lastPoll = now;
            this.screen = this.locator.InGameDeathScreen(inGameState);
            if (life.IsAlive)
            {
                this.dead = false;
                return;
            }

            if (!this.dead)
            {
                (this.dead, this.said, this.diedAt, this.wasShown, this.hiddenSince) = (true, false, now, false, 0);
                this.Died?.Invoke();
                return;
            }

            var shown = this.screen != IntPtr.Zero && this.elements.TryReadElement(this.screen, out var info) && info.IsVisibleFlag;
            if (this.said && this.wasShown && !shown)
            {
                this.hiddenSince = now;
            }

            this.wasShown = shown;
            if (this.hiddenSince != 0 && (shown || now - this.hiddenSince >= HiddenSettleMs))
            {
                if (!shown)
                {
                    this.log.Info("Death screen hidden while dead");
                    this.output.Speak(Announcements.DeathScreenHidden, true);
                }

                this.hiddenSince = 0;
                return;
            }

            if (this.said || now - this.diedAt < ScreenDueMs)
            {
                return;
            }

            this.said = true;
            if (this.seenAt >= this.diedAt - PollMs)
            {
                return; // the screen narrator read it
            }

            var texts = this.screen == IntPtr.Zero ? new List<string>() : this.Texts(this.screen);
            this.log.Info($"Death screen not narrated: element 0x{this.screen.ToInt64():X}, visible {shown}, texts \"{string.Join(" | ", texts)}\"");
            this.output.Speak(Announcements.DeathScreen(texts, shown), true);
        }

        /// <summary>The screen's texts in order: its title and the buttons the game shows (a hidden screen still holds them).</summary>
        private List<string> Texts(IntPtr root)
        {
            var texts = new List<string>();
            void Walk(IntPtr address, int depth)
            {
                if (!this.elements.TryReadElement(address, out var info) || (depth > 0 && !info.IsVisibleFlag))
                {
                    return; // a button the game keeps hidden (revive at checkpoint) is not an option
                }

                if (this.elements.TryReadSpokenText(address, out var text) && text.Length > 0 && !texts.Contains(text))
                {
                    texts.Add(text);
                }

                if (depth < MaxDepth)
                {
                    foreach (var child in this.elements.ReadChildren(info))
                    {
                        Walk(child, depth + 1);
                    }
                }
            }

            Walk(root, 0);
            return texts;
        }
    }
}
