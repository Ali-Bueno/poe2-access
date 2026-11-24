namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The quest page, shop and countdown checks of <see cref="ScreenNarrator"/>: what a quest page, a shop page and a running timer say.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>New HUD texts (quest tracker, shrines, ground items) once per area, never the whole HUD.</summary>
        /// <summary>
        ///     The quest tracker's page whenever it is not the page last said, held whole: an objective must not be lost (an
        ///     area's new one came with the HUD after loading, which is no news, and was never said: Freythorn, 2026-10-29).
        /// </summary>
        /// <returns>The tracker's texts, for the HUD's own news to leave out.</returns>
        public List<ScreenText> CheckQuestPage(ScreenSnapshot snapshot)
        {
            var hud = this.inGame ? snapshot.Find(this.HudLayer) : null;
            var tracker = hud == null
                ? new List<ScreenText>()
                : hud.Texts.Where(text => text.Name is GameLayout.InGameUi.QuestTitleName or GameLayout.InGameUi.QuestLineName &&
                                          (this.IsQuestTrackerText?.Invoke(text.Address) ?? false)).ToList();
            var legend = hud?.Texts.FirstOrDefault(text => text.Name == GameLayout.InGameUi.QuestLegendName)?.Text ?? string.Empty;
            if (this.questPage.News(QuestsOf(tracker), this.InMenu || this.AreaUnnamed, legend) is { Length: > 0 } page)
            {
                // Held whole, pages turned one after another queued up behind each other (user 2026-11-07); the cursor's
                // move cuts it, as it held a dialogue's options back for 12 s (user 2026-11-11).
                var turned = this.questPage.Turned;
                this.Speak("quest page", Announcements.QuestPage(page), turned || SpeechPriority.Interrupts(SpeechKind.Quest), turned ? Hold.Floor : Hold.Page);
            }

            return tracker;
        }

        /// <summary>
        ///     Each tab of the shop is a page of its own: its actions are said on entering it, though the bar showed the same
        ///     before (the guild page's "Seleccionar, A. Cerrar, B" were never said, live 2026-11-10).
        /// </summary>
        private void SayShopPageActions(bool inShop)
        {
            if (inShop)
            {
                this.shopActionsDueAt = Environment.TickCount64 + ShopPageSettleMs;
            }
        }

        /// <summary>
        ///     A shop tab's line cuts in up to 300 ms after its texts and the bar under it is redrawn later still (the guild
        ///     tab still showed the shop's "Filtrar/buscar", live 2026-11-10): what follows the tab waits this long.
        /// </summary>
        private const long ShopPageSettleMs = 600;

        private void SayDueShopActions(ScreenSnapshot snapshot, long now)
        {
            if (this.shopActionsDueAt == 0 || now < this.shopActionsDueAt)
            {
                return;
            }

            this.shopActionsDueAt = 0;
            var actions = snapshot.Layers.Where(layer => layer.IsHintBar).SelectMany(layer => layer.Texts).ToList();
            if (actions.Count > 0)
            {
                // Said here, they are not news again when the bar next changes.
                this.legendsSaid.Clear();
                actions.ForEach(action => this.legendsSaid.Add(Describe(new[] { action })));
                this.Speak("shop page actions", Describe(actions), false);
            }
        }

        /// <summary>
        ///     The favours window opens with the tribute of the last one opened and corrects it up to 630 ms later ("1.727" for
        ///     1.265, play log 2026-11-20): its first value waits twice that.
        /// </summary>
        private const long TributeSettleMs = 1200;

        /// <summary>What a favour leaves is said at once, after the item.</summary>
        private const long TributeChangeMs = 200;

        /// <summary>The favours window's own lines, held until its tribute is right; dropped when the cursor moves on.</summary>
        private (IReadOnlyList<ScreenText> Lines, long At)? heldFavours;

        /// <summary>
        ///     The favours window coming on top, before its cursor or after it: its long explanation of reservations waits for
        ///     the tribute, which goes first.
        /// </summary>
        private bool HoldsFavours(ScreenChange change)
        {
            if (change.Kind is not (ScreenChangeKind.LayerOpened or ScreenChangeKind.TopLayerChanged) || !change.Texts.Any(this.IsTribute))
            {
                return false;
            }

            var chrome = this.Chrome(change.Texts);
            var lines = PairNumbers(change.Texts).Where(text => !text.IsControl && !IsNumber(text.Text) && !IsTierNumeral(text.Text) && !chrome.Contains(text.Address)).ToList();
            if (lines.Count > SmallBoxTexts)
            {
                return false;
            }

            if (this.favoursDue)
            {
                this.favoursDue = false; // a popup closing over the window brings it on top again: said once an opening
                this.heldFavours = (lines, Environment.TickCount64);
            }

            return true;
        }

        /// <summary>The favours window's explanation is still due since every panel closed.</summary>
        private bool favoursDue = true;

        /// <summary>The favours window's tribute as it reads now, null while the window is not shown.</summary>
        private string? tributeShown;

        /// <summary>The HUD as last seen: it counts the area's tribute, right while the window's label is still the last window's.</summary>
        private ScreenLayer? lastHud;

        /// <summary>Every panel closed: the next focus opens one.</summary>
        private void PanelsClosed() => (this.goldDue, this.openingDue, this.favoursDue) = (true, true, true);

        /// <summary>
        ///     Whether the focus of an opening favours window still waits for a tribute to trust: the number the HUD showed,
        ///     or one that has had the time to be corrected (<see cref="TributeSettleMs"/>).
        /// </summary>
        private bool AwaitsTribute(DateTime since)
        {
            if (this.heldFavours is not { } held)
            {
                return this.deferredFavours && (DateTime.UtcNow - since).TotalMilliseconds < TributeSettleMs;
            }

            return Environment.TickCount64 - held.At < TributeSettleMs &&
                   !(this.tributeShown is { } shown && this.lastHud?.Texts.Any(text => HasDigit(text.Text) && Digits(text.Text) == Digits(shown)) == true);

            static string Digits(string text) => new(text.Where(char.IsDigit).ToArray());
        }

        private static bool HasDigit(string text) => text.Any(char.IsDigit);

        /// <summary>The tribute left in the favours window; only a counter is looked up.</summary>
        private bool IsTribute(ScreenText text) =>
            text.Name.Length == 0 && !text.IsControl && HasDigit(text.Text) && text.Text.Any(char.IsLetter) && this.IsTributeLabel?.Invoke(text.Address) == true;

        /// <summary>
        ///     Says the account's points when the shop shows them and when a purchase changes them, after what is being said;
        ///     the guild's, on its tab, by their own name; a ritual's tribute, as its window writes it.
        /// </summary>
        public void CheckShopBalance(ScreenSnapshot snapshot)
        {
            string? shown = null;
            string? guild = null;
            string? tribute = null;
            foreach (var layer in this.inGame ? snapshot.Layers : Array.Empty<ScreenLayer>())
            {
                foreach (var text in layer.IsHintBar || layer.Address == this.HudLayer ? Array.Empty<ScreenText>() : layer.Texts)
                {
                    shown ??= text.Name == GameLayout.InGameUi.ShopBalanceName ? text.Text : null;
                    guild ??= text.Name == GameLayout.InGameUi.ShopGuildBalanceName ? text.Text : null;
                    tribute ??= this.IsTribute(text) ? text.Text : null;
                }
            }

            this.FavoursShown = tribute != null;
            this.tributeShown = tribute;
            this.lastHud = snapshot.Find(this.HudLayer) is { Texts.Count: > 0 } hud ? hud : this.lastHud;

            var now = Environment.TickCount64;
            if (this.shopBalance.Heard(shown, now) is { } points)
            {
                this.Speak("shop balance", Announcements.Capitalised(Announcements.ShopBalance(points)), false);
            }

            if (this.guildBalance.Heard(guild, now) is { } guildPoints)
            {
                this.Speak("guild balance", Announcements.Capitalised(Announcements.ShopGuildBalance(guildPoints)), false);
            }

            var withFocus = this.deferredFocus != null && (this.heldFavours != null || this.deferredFavours);
            var left = withFocus ? null : this.tributeBalance.Heard(tribute, now);
            if (this.heldFavours is { } held && tribute == null)
            {
                this.heldFavours = now - held.At < TributeSettleMs ? held : null; // closed, or its label read a moment later
            }
            else if (!withFocus && this.heldFavours is { } favours && (left != null || now - favours.At >= 2 * TributeSettleMs))
            {
                // Its title, the tribute (said already when the window was rebuilt at once), then what the window explains.
                this.heldFavours = null;
                var title = favours.Lines.Count > 1 ? 1 : 0;
                left = Announcements.Sentences(new[] { Describe(favours.Lines.Take(title)), left ?? tribute!, Describe(favours.Lines.Skip(title)) }.Where(part => part.Length > 0));
            }

            if (left != null)
            {
                this.Speak("tribute left", left, false);
            }

            this.SayDueShopActions(snapshot, now);
        }

        /// <summary>Says a clock falling on the HUD, cutting in: running out it can kill.</summary>
        public void CheckCountdown(ScreenSnapshot snapshot)
        {
            var clock = this.inGame ? snapshot.Find(this.HudLayer)?.Texts.FirstOrDefault(text => CountdownWatch.IsClock(text.Text))?.Text : null;
            if (this.countdown.Heard(clock) is { } left)
            {
                this.output.Urge(Announcements.SecondsLeft(left));
            }
        }
    }
}
