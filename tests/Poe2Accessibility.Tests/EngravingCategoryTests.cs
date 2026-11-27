namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;
    using static ScreenFixtures;

    /// <summary>
    ///     The support engraving window's categories (R1/L1), opened from a gem in the stash, replayed from a tester's log
    ///     (2026-11-26 22:01-22:07), and the stash's own LB/RB beside it; the pad driving the game behind another window.
    /// </summary>
    public static class EngravingCategoryTests
    {
        private const long EngraveLayer = 58;
        private const long StashLayer = 17;
        private const long HudLayer = 12;
        private const string NoEntries = "Selected Build Plan does not contain any entries for this category. Press Square to show recommended gems.";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EachCategoryIsSaidWithoutTheStashsTab), EachCategoryIsSaidWithoutTheStashsTab),
            (nameof(ACategoryWithANewButtonStillCutsIn), ACategoryWithANewButtonStillCutsIn),
            (nameof(AChoiceRenamedInPlaceDoesNotNameTheStashsTab), AChoiceRenamedInPlaceDoesNotNameTheStashsTab),
            (nameof(TheStashsOwnTabSwitchIsStillSaid), TheStashsOwnTabSwitchIsStillSaid),
            (nameof(OnlyAPressBehindTheWindowDrivesTheGame), OnlyAPressBehindTheWindowDrivesTheGame),
        };

        /// <summary>
        ///     Every category names itself, and one the build has nothing for says the game's message even when the last
        ///     category's message is still drawn ("Escape Shot" was said alone); the stash's "tab 1" never cuts in.
        /// </summary>
        private static void EachCategoryIsSaidWithoutTheStashsTab()
        {
            var window = new Window();
            window.Choose("Herald of Thunder", "Elemental Armament I", "Concentrated Area, Elemental Armament I", "I");
            window.Empty("Bow Shot");
            window.Choose("Strength Gems", "Elemental Armament I", "Prolonged Duration I, Elemental Armament I", "I");
            window.Empty("Contagion");
            window.Empty("Escape Shot");
            window.Empty("Stormcaller Arrow");
            Equal(
                string.Join(" | ", new[]
                {
                    "Herald of Thunder. Elemental Armament I, 1 of 2",
                    $"Bow Shot. {NoEntries}",
                    "Strength Gems. Elemental Armament I, 1 of 2",
                    $"Contagion. {NoEntries}",
                    $"Escape Shot. {NoEntries}",
                    $"Stormcaller Arrow. {NoEntries}",
                }),
                string.Join(" | ", window.Said),
                "spoken");
        }

        /// <summary>
        ///     Back from an attribute category to a skill without choices the hint bar changes in the same frame ("Show All"
        ///     to "Show Recommended"): said after the category, which still cuts the last one off (it was queued behind it).
        /// </summary>
        private static void ACategoryWithANewButtonStillCutsIn()
        {
            var window = new Window();
            window.Choose("Strength Gems", "Elemental Armament I", "Prolonged Duration I, Elemental Armament I", "I");
            window.Empty("Bow Shot", "Show Recommended");
            var line = $"Bow Shot. {NoEntries} Show Recommended, Square";
            Equal(line, window.Said.LastOrDefault(), "last spoken");
            Equal(true, window.Interrupted(line), "cuts in");
        }

        /// <summary>
        ///     The game can rename the focused choice in place for the next category (the log's "Value -> … Cooldown Recovery
        ///     I"): no focus move, so the tab key's wait ran out with the focus held and named the stash's tab.
        /// </summary>
        private static void AChoiceRenamedInPlaceDoesNotNameTheStashsTab()
        {
            var window = new Window();
            window.Choose("Herald of Thunder", "Elemental Armament I", "Concentrated Area, Elemental Armament I", "I");
            window.Renamed("Barrage", "Cooldown Recovery I", "III");
            Equal(false, window.Said.Contains("tab 1"), "the stash's tab said");
            Equal("Barrage. Cooldown Recovery I, 1 of 2", window.Said.LastOrDefault(), "last spoken");
        }

        /// <summary>
        ///     LB/RB on the stash with the cursor left in the inventory beside it still say the new tab (2026-10-28), also
        ///     back to a tab the cursor in the stash's own grid named as it switched.
        /// </summary>
        private static void TheStashsOwnTabSwitchIsStillSaid()
        {
            var window = new Window();
            window.SwitchStash("2");
            Equal("tab 2", string.Join(" | ", window.Said), "the cursor beside");
            window = new Window();
            window.SwitchStashUnderCursor("2");
            window.SwitchStash("1");
            Equal("Stash. tab 2. Empty, column 12, row 10 | tab 1", string.Join(" | ", window.Said), "the cursor in the stash, then beside");
        }

        /// <summary>Behind another window only a press made there drives the game; play the window was taken from does not.</summary>
        private static void OnlyAPressBehindTheWindowDrivesTheGame()
        {
            const long Behind = 10_000;
            var pad = new PadActivity();
            Equal(false, pad.Driving(Behind, Behind + 100), "an untouched pad");
            pad.Observe(0x200, false, Behind - 50);
            Equal(false, pad.Driving(Behind, Behind + 100), "a press just before the window went behind (a notification mid-fight)");
            pad.Observe(0x200, false, Behind + 500);
            Equal(false, pad.Driving(Behind, Behind + 600), "that button still held");
            pad.Observe(0, false, Behind + 700);
            Equal(false, pad.Driving(Behind, Behind + 800), "that button let go");
            pad.Observe(0x1000, false, Behind + 1000);
            Equal(true, pad.Driving(Behind, Behind + 1000 + PadActivity.DrivesMs - 1), "R1 pressed behind the window, a moment later");
            Equal(false, pad.Driving(Behind, Behind + 1000 + PadActivity.DrivesMs), "R1 pressed behind the window, long ago");
            pad.Observe(0x1000, true, Behind + 5000);
            Equal(true, pad.Driving(Behind, Behind + 5000), "a stick pushed behind the window");
        }

        /// <summary>
        ///     The engraving window as the narrator sees it, over the stash it was opened from: the stash said its tab "1" as
        ///     it opened with the cursor in the inventory, so its layer is the one LB/RB would switch.
        /// </summary>
        private sealed class Window
        {
            private readonly Recorder output = new();
            private readonly ScreenNarrator narrator;
            private readonly ScreenLayer stash = Layer(StashLayer, 17, true, Text(1700, "Buy Stash"), Text(1702, "7,035 gold"));
            private readonly ScreenLayer hud = Layer(HudLayer, 12, true, Text(1200, "HUD"));
            private IntPtr focusLayer;
            private ScreenText[] shown = Array.Empty<ScreenText>();
            private string stashTab = "1";

            /// <summary>When LB/RB (or A) was last pressed: the core's confirm clock.</summary>
            private long pressedAt = long.MinValue / 2;

            public Window()
            {
                var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
                this.narrator = new ScreenNarrator(this.output, log, layer => layer == (IntPtr)StashLayer ? this.stashTab : string.Empty)
                {
                    FocusLayer = () => this.focusLayer, HudLayer = (IntPtr)HudLayer, SinceConfirm = () => Environment.TickCount64 - this.pressedAt,
                };
                this.narrator.Reset(true);
                var cell = new AccessibleUiNode { Address = (IntPtr)1701, Role = UiRole.GridCell, Text = "empty", Value = "column 1, row 1", Context = "Inventory", IsVisible = true };
                this.focusLayer = (IntPtr)StashLayer;
                this.narrator.OnFocusChanged(cell);
                this.narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.LayerOpened, this.stash, this.stash.Texts), Snapshot(this.hud, this.stash), cell, this.focusLayer);
                this.Settle(400);
                this.output.Said.Clear();
            }

            public List<string> Said => this.output.Said;

            /// <summary>R1 onto a category with choices: the focus lands on one, the page's texts well within its 300 ms wait.</summary>
            public void Choose(string category, string choice, params string[] page)
            {
                this.Press();
                this.focusLayer = (IntPtr)EngraveLayer;
                var node = Choice(choice);
                this.narrator.OnFocusChanged(node);
                this.Settle(100);
                this.Show(node, Page(category, page));
                this.Settle(1100);
            }

            /// <summary>R1 onto a category with choices whose first the game writes into the focused choice: a value change.</summary>
            public void Renamed(string category, string choice, params string[] page)
            {
                this.Press();
                var node = Choice(choice);
                this.Settle(50);
                this.narrator.OnValueChanged(node);
                this.Settle(170);
                this.Show(node, Page(category, page));
                this.Settle(1100);
            }

            /// <summary>R1 onto a category without choices: no focus, the title and the game's message; maybe a new button with them.</summary>
            public void Empty(string category, string? button = null)
            {
                this.Press();
                this.focusLayer = IntPtr.Zero;
                this.Settle(200);
                this.Show(null, new[] { Title(category), Text(5850, NoEntries) });
                if (button != null)
                {
                    // The hint bar, in the same frame: skills offer "Show Recommended", the attribute categories "Show All".
                    var hint = new ScreenText((IntPtr)6100, button, string.Empty, false, true, "Square");
                    var bar = new ScreenLayer((IntPtr)61, 61, new[] { hint }, false, true);
                    this.narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.TextsAppeared, bar, new[] { hint }), Snapshot(this.hud, this.stash, bar), null, this.focusLayer);
                }

                this.Settle(1100);
            }

            public bool Interrupted(string line) => this.output.Interrupting.Contains(line);

            /// <summary>RB on the stash itself, the cursor left in the inventory: the tab changes, nothing else does.</summary>
            public void SwitchStash(string tab)
            {
                this.Press();
                this.stashTab = tab;
                this.Settle(1100);
            }

            /// <summary>RB on the stash with the cursor in its grid: the cursor lands again, naming the tab itself.</summary>
            public void SwitchStashUnderCursor(string tab)
            {
                this.Press();
                this.stashTab = tab;
                this.Settle(100);
                this.narrator.OnFocusChanged(new AccessibleUiNode
                {
                    Address = (IntPtr)1703, Role = UiRole.GridCell, Text = "empty", Value = "column 12, row 10", Context = $"Stash. tab {tab}", IsVisible = true,
                });
                this.Settle(1100);
            }

            /// <summary>R1 as the core reports it: the tab key (while something holds the focus) and the confirm clock.</summary>
            private void Press()
            {
                this.narrator.OnTabKey();
                this.pressedAt = Environment.TickCount64;
            }

            private void Show(AccessibleUiNode? focus, ScreenText[] texts)
            {
                var appeared = texts.Where(text => !this.shown.Contains(text)).ToList();
                this.shown = texts;
                var layer = Layer(EngraveLayer, 58, true, texts);
                this.narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.TextsAppeared, layer, appeared), Snapshot(this.hud, this.stash, layer), focus, this.focusLayer);
            }

            private void Settle(int ms)
            {
                for (var waited = 0; waited < ms; waited += 50)
                {
                    Thread.Sleep(50);
                    this.narrator.Tick();
                }
            }

            private static ScreenText[] Page(string category, string[] texts) => texts.Select((text, at) => Text(5800 + at, text)).Append(Title(category)).ToArray();

            private static ScreenText Title(string category) => new((IntPtr)5890, category, "category_name_label", false, true);

            private static AccessibleUiNode Choice(string text) => new()
            {
                Address = (IntPtr)5980, Name = "new_option", Role = UiRole.Panel, Text = text, Position = "1 of 2", IsVisible = true,
            };
        }

        private sealed class Recorder : IAccessibilityOutputService
        {
            public List<string> Said { get; } = new();

            /// <summary>The lines said cutting in on what was being said.</summary>
            public HashSet<string> Interrupting { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false)
            {
                this.Said.Add(text);
                if (interrupt)
                {
                    this.Interrupting.Add(text);
                }
            }

            public void Urge(string text, int holdMs = 1500, bool yieldsToMoves = false) => this.Said.Add(text);

            public void Repeat()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
