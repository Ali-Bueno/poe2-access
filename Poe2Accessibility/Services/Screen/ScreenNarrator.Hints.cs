namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Hint bars of <see cref="ScreenNarrator"/>: pre-game button hints and in-game legends, settling, stale and cut-off bars.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>Pre-game button hints: the last spoken, and a new set waiting to settle.</summary>
        private string lastHints = string.Empty;

        /// <summary>The previous screen's hints and when that screen closed: the bar can show them for a moment longer.</summary>
        private (string Text, DateTime Since) staleHints = (string.Empty, DateTime.MinValue);
        private readonly HashSet<string> spokenHints = new();
        private (string Text, DateTime Since) pendingHints = (string.Empty, DateTime.MinValue);

        /// <summary>The previous screen's hint bar seen this soon after a state change is the game redrawing late.</summary>
        private const int StaleHintsMs = 2000;

        /// <summary>A new set of pre-game hints must stay this long before it is read (screens rebuild in steps).</summary>
        private const int HintsSettleMs = 400;

        /// <summary>
        ///     A screen's own text that interrupts its button bar this soon after the bar began cut it off: the login
        ///     screen's "Conectarse…" silenced "Presiona para jugar, A" 50 ms in (2026-10-24). The bar is said again after.
        /// </summary>
        private const int HintsCutMs = 1500;

        /// <summary>The button bar last said, while an interrupting screen text could still cut it.</summary>
        private (string Text, DateTime At)? cuttableHints;

        /// <summary>The panel whose button legends were said (<see cref="legendsSaid"/>), each once per opening.</summary>
        private IntPtr legendsLayer;

        private readonly HashSet<string> legendsSaid = new(StringComparer.Ordinal);

        /// <summary>Legends of a panel without focus, said once its title and content have been.</summary>
        private (string Text, DateTime At) panelLegends = (string.Empty, DateTime.MinValue);

        /// <summary>The controller legends of the hint bar, said after the focus that waits for them.</summary>
        private void OnHintBarChange(ScreenChange change, ScreenSnapshot snapshot, IntPtr focusLayer)
        {
            if (this.inGame && focusLayer == IntPtr.Zero && change.Kind == ScreenChangeKind.TextsAppeared &&
                (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < FocusTextMergeMs)
            {
                // What the map cursor's place offers ("Instance Manager, A" on a town): after the place.
                this.Speak("action of panel without focus", Describe(change.Texts), false);
            }
            else if (this.inGame && change.Kind == ScreenChangeKind.TextsAppeared &&
                     (DateTime.UtcNow - this.valueChangedAt).TotalMilliseconds < FocusTextMergeMs)
            {
                // The control changed in place and offers other actions: A on a bound skill picks it up, its button
                // reads "empty" and the bar "Place, A. Cancel, B" (skill bind screen, 2026-10-24).
                this.Speak("actions after value change", Describe(change.Texts), false);
            }
            else if (this.inGame && focusLayer != IntPtr.Zero && change.Kind is ScreenChangeKind.LayerOpened or ScreenChangeKind.TextsAppeared)
            {
                // What a panel's buttons do, each once per opening and after the focus: the inventory said "Equip, X"
                // from the item but never "Drop, Y" from this bar (user, 2026-10-24).
                if (focusLayer != this.legendsLayer)
                {
                    this.legendsLayer = focusLayer;
                    this.legendsSaid.Clear();
                }

                var fresh = change.Texts.Where(text => this.legendsSaid.Add(Describe(new[] { text }))).ToList();
                if (fresh.Count > 0 && this.deferredFocus != null)
                {
                    this.hintAfterFocus = Announcements.Sentences(new[] { this.hintAfterFocus, Describe(fresh) });
                }
                else if (fresh.Count > 0)
                {
                    this.Speak("panel actions", Describe(fresh), false);
                }
            }
            else if (this.inGame && focusLayer == IntPtr.Zero && snapshot.TopInteractive is { } panel && panel.Address != this.HudLayer &&
                     change.Kind is ScreenChangeKind.LayerOpened or ScreenChangeKind.TextsAppeared)
            {
                // A panel nothing focuses (the world map: "Show quests, X"): its legends once per opening, after the
                // title and the place it shows, which come a moment later (Tick).
                if (panel.Address != this.legendsLayer)
                {
                    this.legendsLayer = panel.Address;
                    this.legendsSaid.Clear();
                }

                var fresh = change.Texts.Where(text => this.legendsSaid.Add(Describe(new[] { text }))).ToList();
                if (fresh.Count > 0)
                {
                    this.panelLegends = (Announcements.Sentences(new[] { this.panelLegends.Text, Describe(fresh) }), DateTime.UtcNow);
                }
            }
        }

        /// <summary>Speaks the hint bar (controller legends) on request.</summary>
        public void SpeakHints(ScreenSnapshot snapshot)
        {
            var hints = snapshot.Layers.SelectMany(layer => layer.Texts).Where(ScreenWatcher.IsLegend).ToList();
            this.Speak("hints", hints.Count == 0 ? Announcements.NoHints : Describe(hints), true);
        }
    }
}
