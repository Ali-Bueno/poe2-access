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
            (nameof(ACategoryReachedByR1ReadsItsChoicesDescription), ACategoryReachedByR1ReadsItsChoicesDescription),
            (nameof(TheWindowsPartsAreToldByTheDumpsPaths), TheWindowsPartsAreToldByTheDumpsPaths),
            (nameof(ADescriptionLeftDrawnIsReadOnReturn), ADescriptionLeftDrawnIsReadOnReturn),
            (nameof(ASkillGemsDescriptionKeepsTheSmallPageRule), ASkillGemsDescriptionKeepsTheSmallPageRule),
            (nameof(ASkillsTabReadsTheSupportableSkills), ASkillsTabReadsTheSupportableSkills),
            (nameof(TheMessageBackBySquareIsSaidOnTheNextCategory), TheMessageBackBySquareIsSaidOnTheNextCategory),
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
        ///     A category reached with R1 reads its focused choice's description, as the D-pad does (Barrage's Cooldown Recovery
        ///     came without it, tester 2026-11-26).
        /// </summary>
        private static void ACategoryReachedByR1ReadsItsChoicesDescription()
        {
            var window = new Window();
            // The description as the log of 22:01:49 has it: more lines than a small page, which the tab once dropped whole.
            var description = new[]
            {
                "Category: Cooldown Recovery", "Cost Multiplier: 130%", "Support Requirements: +5 Dex (25)", "Supports skills with Cooldowns.",
                "Supported Skills have 25% increased cooldown recovery rate", "Place into a Skill's Support Gem socket.",
            };
            window.Choose("Barrage", "Cooldown Recovery I", new[] { "III" }, description, Array.Empty<string>());
            Equal("Barrage. Cooldown Recovery I, 1 of 2. " + string.Join(". ", description.Select(line => line.TrimEnd('.'))) + ".", window.Said.LastOrDefault(), "last spoken");
        }

        /// <summary>
        ///     Back on a category whose choice's description the game left drawn (Barrage, Frost Bomb without choices, Barrage
        ///     again): nothing of it appears again, and it is still read (tester 2026-11-27 00:10:35, "Barrage needs this support" alone).
        /// </summary>
        private static void ADescriptionLeftDrawnIsReadOnReturn()
        {
            var window = new Window();
            var description = new[]
            {
                "Category: Cooldown Recovery", "Cost Multiplier: 130%", "Supports skills with Cooldowns.", "Supported Skills have 25% increased cooldown recovery rate",
                "Place into a Skill's Support Gem socket.",
            };
            window.Choose("Barrage", "Cooldown Recovery I", new[] { "III" }, description, Array.Empty<string>());
            window.Empty("Frost Bomb");
            window.Return("Barrage", "Cooldown Recovery I", new[] { "III" }, description);
            Equal("Barrage. Cooldown Recovery I, 1 of 2. " + string.Join(". ", description.Select(line => line.TrimEnd('.'))) + ".", window.Said.LastOrDefault(), "last spoken");
        }

        /// <summary>The uncut skill gem window (nothing of it a support's description) keeps the small page's lines after its choice.</summary>
        private static void ASkillGemsDescriptionKeepsTheSmallPageRule()
        {
            var window = new Window();
            window.Choose("Bow", "Escape Shot, Level 3", "Twister");
            Equal("Bow. Escape Shot, Level 3, 1 of 2. Twister", window.Said.LastOrDefault(), "last spoken");
        }

        /// <summary>
        ///     The game's "Supportable Skills" list ends the description on a skill's tab too, and is read there, by R1 or by the
        ///     D-pad (the tester, 2026-11-27: what the window shows is heard).
        /// </summary>
        private static void ASkillsTabReadsTheSupportableSkills()
        {
            var window = new Window();
            var list = new[] { "Supportable Skills", "Lightning Rod, Herald of Thunder" };
            window.Choose("Lightning Rod", "Elemental Armament I", new[] { "I" }, new[] { "Category: Elemental Armament" }, list);
            Equal("Lightning Rod. Elemental Armament I, 1 of 2. Category: Elemental Armament. Supportable Skills. Lightning Rod, Herald of Thunder", window.Said.LastOrDefault(), "by R1");
            window.Move("Lightning Attunement", new[] { "Category: Lightning Attunement" }, list);
            Equal("Lightning Attunement, 1 of 2. Category: Lightning Attunement. Supportable Skills. Lightning Rod, Herald of Thunder", window.Said.LastOrDefault(), "by the D-pad");
        }

        /// <summary>
        ///     The window's parts by their ancestors' control ids, from the parent up, as the dump of 2026-11-26 23:26 has them:
        ///     contents_frame > item_popup > unnamed > bottom_layout > the unnamed "Supportable Skills" block.
        /// </summary>
        private static void TheWindowsPartsAreToldByTheDumpsPaths()
        {
            var popup = new[] { "", "item_popup", "contents_frame" };
            string[] Up(params string[] below) => below.Concat(popup).ToArray();
            EngravePart Part(string[] chain, bool support = true) => EngraveWindowParts.Classify(chain, support);
            Equal(EngravePart.Description, Part(Up("", "bottom_layout")), "the Supportable Skills heading");
            Equal(EngravePart.Description, Part(Up("", "", "", "bottom_layout")), "a skill in the Supportable Skills grid");
            Equal(EngravePart.Description, Part(Up("description_layout", "bottom_layout")), "the description");
            Equal(EngravePart.Description, Part(Up("bottom_layout")), "the reminder");
            Equal(EngravePart.Description, Part(Up("current_tab_content", "stat_container_layout", "bottom_layout")), "a stat line");
            Equal(EngravePart.Description, Part(Up("title_background")), "the title");
            Equal(EngravePart.Other, Part(new[] { "category_name_anchor", "contents_frame" }), "the tab's label");
            Equal(EngravePart.Other, Part(Up("description_layout", "bottom_layout"), support: false), "a skill gem's description (the uncut skill gem window)");
            Equal(EngravePart.Other, Part(new[] { "", "item_popup", "inventory" }), "a popup of the same name elsewhere");
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
        ///     Square back from the recommended gems shows the message with no category switch, and the next category without
        ///     choices keeps it drawn: said there too (the mod restarted on the recommended gems, tester 2026-11-26).
        /// </summary>
        private static void TheMessageBackBySquareIsSaidOnTheNextCategory()
        {
            var window = new Window();
            window.Choose("Stormcaller Arrow", "Shock", "Lightning Attunement, Shock");
            window.BackToPlan();
            window.Empty("Escape Shot");
            Equal($"Escape Shot. {NoEntries}", window.Said.LastOrDefault(), "last spoken");
        }

        /// <summary>
        ///     The game can rename the focused choice in place for the next category (the log's "Value -> … Cooldown Recovery
        ///     I"): no focus move, so the tab key's wait ran out with the focus held and named the stash's tab, and the choice
        ///     was said alone, then again with its category.
        /// </summary>
        private static void AChoiceRenamedInPlaceDoesNotNameTheStashsTab()
        {
            var window = new Window();
            window.Choose("Herald of Thunder", "Elemental Armament I", "Concentrated Area, Elemental Armament I", "I");
            window.Renamed("Barrage", "Cooldown Recovery I", "III");
            // Neither the stash's tab nor the renamed choice alone before its category.
            Equal("Herald of Thunder. Elemental Armament I, 1 of 2 | Barrage. Cooldown Recovery I, 1 of 2", string.Join(" | ", window.Said), "spoken");
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
                    EngravePartOf = text => this.parts.GetValueOrDefault(text, EngravePart.Other),
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

            /// <summary>What each text of the window is, as the semantics would tell (UiSemantics.EngravePartOf).</summary>
            private readonly Dictionary<IntPtr, EngravePart> parts = new();

            /// <summary>The next address for a description or list text: a choice's description is drawn anew for each choice.</summary>
            private long nextPart = 5900;

            /// <summary>R1 onto a category with choices whose focused one shows its description, maybe ending with the game's skill list.</summary>
            public void Choose(string category, string choice, string[] page, string[] description, string[] skillList)
            {
                this.Press();
                this.focusLayer = (IntPtr)EngraveLayer;
                var node = Choice(choice);
                this.narrator.OnFocusChanged(node);
                this.Settle(100);
                this.Show(node, Page(category, page).Concat(this.Described(description, skillList)).ToArray());
                this.Settle(1100);
            }

            /// <summary>The D-pad to another choice of the same category: its description replaces the last one's.</summary>
            public void Move(string choice, string[] description, string[] skillList)
            {
                var node = Choice(choice, 5981);
                this.narrator.OnFocusChanged(node);
                this.Settle(100);
                this.Show(node, this.shown.Where(text => !this.parts.ContainsKey(text.Address)).Concat(this.Described(description, skillList)).ToArray());
                this.Settle(1100);
            }

            /// <summary>
            ///     R1 back onto a category whose choice's description the game left drawn: the layer shows it, and only the title
            ///     and the page appear (the description's elements hold the same text as when last confirmed).
            /// </summary>
            public void Return(string category, string choice, string[] page, string[] description)
            {
                this.Press();
                this.focusLayer = (IntPtr)EngraveLayer;
                var node = Choice(choice);
                this.narrator.OnFocusChanged(node);
                this.Settle(100);
                var appeared = Page(category, page);
                var drawn = this.parts.Keys.Select(address => (address, line: this.lines[address])).Where(part => description.Contains(part.line)).Select(part => Text((long)part.address, part.line));
                var layer = Layer(EngraveLayer, 58, true, appeared.Concat(drawn).ToArray());
                this.shown = layer.Texts.ToArray();
                this.narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.TextsAppeared, layer, appeared), Snapshot(this.hud, this.stash, layer), node, this.focusLayer);
                this.Settle(1100);
            }

            /// <summary>The text each part's address held (<see cref="Described"/>).</summary>
            private readonly Dictionary<IntPtr, string> lines = new();

            private IEnumerable<ScreenText> Described(string[] description, string[] skillList)
            {
                // The "Supportable Skills" list is the description's own end.
                foreach (var line in description.Concat(skillList))
                {
                    var text = Text(this.nextPart++, line);
                    this.parts[text.Address] = EngravePart.Description;
                    this.lines[text.Address] = line;
                    yield return text;
                }
            }

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
                this.Settle(100);
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

            /// <summary>Square from the recommended gems back to the build's, in a category it has none for: the message alone appears.</summary>
            public void BackToPlan()
            {
                this.focusLayer = IntPtr.Zero;
                this.Show(null, this.shown.Where(text => text.Name == "category_name_label").Append(Text(5850, NoEntries)).ToArray());
                this.Settle(1100);
            }

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

            private static AccessibleUiNode Choice(string text, long address = 5980) => new()
            {
                Address = (IntPtr)address, Name = "new_option", Role = UiRole.Panel, Text = text, Position = "1 of 2", IsVisible = true,
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
