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
        ///     Says the account's points when the shop shows them and when a purchase changes them, after what is being said;
        ///     the guild's, on its tab, by their own name.
        /// </summary>
        public void CheckShopBalance(ScreenSnapshot snapshot)
        {
            string? shown = null;
            string? guild = null;
            foreach (var layer in this.inGame ? snapshot.Layers : Array.Empty<ScreenLayer>())
            {
                foreach (var text in layer.IsHintBar || layer.Address == this.HudLayer ? Array.Empty<ScreenText>() : layer.Texts)
                {
                    shown ??= text.Name == GameLayout.InGameUi.ShopBalanceName ? text.Text : null;
                    guild ??= text.Name == GameLayout.InGameUi.ShopGuildBalanceName ? text.Text : null;
                }
            }

            var now = Environment.TickCount64;
            if (this.shopBalance.Heard(shown, now) is { } points)
            {
                this.Speak("shop balance", Announcements.Capitalised(Announcements.ShopBalance(points)), false);
            }

            if (this.guildBalance.Heard(guild, now) is { } guildPoints)
            {
                this.Speak("guild balance", Announcements.Capitalised(Announcements.ShopGuildBalance(guildPoints)), false);
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
