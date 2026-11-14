namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;
    using static ScreenFixtures;

    /// <summary>Screen-change reports, loading and chat speech, L3 gestures, stick bins and the scanner browser.</summary>
    public static class ScreenAndInputTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(BaselineIsSilentExceptTopLayer), BaselineIsSilentExceptTopLayer),
            (nameof(PopupNeedsTwoSnapshots), PopupNeedsTwoSnapshots),
            (nameof(ChangedTextIsReportedOnce), ChangedTextIsReportedOnce),
            (nameof(CounterTicksAreIgnored), CounterTicksAreIgnored),
            (nameof(APadButtonTurningUsableIsNews), APadButtonTurningUsableIsNews),
            (nameof(AStripBuiltAgainIsNotNews), AStripBuiltAgainIsNotNews),
            (nameof(TheEndsOfATabBarSayWhatTheyDo), TheEndsOfATabBarSayWhatTheyDo),
            (nameof(ClosedPopupIsNewsAgain), ClosedPopupIsNewsAgain),
            (nameof(InGameBaseLayersNeverOwnInput), InGameBaseLayersNeverOwnInput),
            (nameof(TypingIsReportedAsTheEdit), TypingIsReportedAsTheEdit),
            (nameof(OnlyTheLoadingScreenSpeaksWhileAnAreaLoads), OnlyTheLoadingScreenSpeaksWhileAnAreaLoads),
            (nameof(DiabloLoopsStopAtTheirTopPitch), DiabloLoopsStopAtTheirTopPitch),
            (nameof(CreationNameDeadEndsAreSaid), CreationNameDeadEndsAreSaid),
            (nameof(ChatReportsOnlyAppendedLines), ChatReportsOnlyAppendedLines),
            (nameof(L3GesturesAreToldApart), L3GesturesAreToldApart),
            (nameof(StickDirectionsBinInWorldSpace), StickDirectionsBinInWorldSpace),
            (nameof(ScannerBrowsesNearestFirstAndFollowsTheEntity), ScannerBrowsesNearestFirstAndFollowsTheEntity),
            (nameof(RouteLegsKeepRealTurnsOnly), RouteLegsKeepRealTurnsOnly),
        };

        private static void BaselineIsSilentExceptTopLayer()
        {
            var differ = new ScreenDiffer();
            var screen = Snapshot(Layer(1, 0, true, Text(10, "Log In")), Layer(2, 1, true, Text(20, "Terms of use")));
            Equal(0, differ.Apply(screen).Count, "first snapshot");
            var changes = differ.Apply(screen);
            Equal(1, changes.Count, "second snapshot");
            Equal(ScreenChangeKind.TopLayerChanged, changes[0].Kind, "kind");
            Equal((IntPtr)2, changes[0].Layer.Address, "the popup above the screen owns input");
        }

        private static void PopupNeedsTwoSnapshots()
        {
            var differ = Settled(out var screen);
            var withPopup = Snapshot(screen.Layers.Append(Layer(3, 2, true, Text(30, "Account"))).ToArray());
            Equal(0, differ.Apply(withPopup).Count, "one sighting");
            var changes = differ.Apply(withPopup);
            Equal(true, changes.Any(change => change.Kind == ScreenChangeKind.LayerOpened && change.Layer.Address == (IntPtr)3), "opened");
            Equal(true, changes.Any(change => change.Kind == ScreenChangeKind.TopLayerChanged && change.Layer.Address == (IntPtr)3), "top");
        }

        private static void ChangedTextIsReportedOnce()
        {
            var differ = Settled(out _);
            var warrior = Snapshot(Layer(1, 0, true, Text(10, "Warrior"), Text(11, "I wanted very little")));
            differ.Apply(warrior);
            var changes = differ.Apply(warrior);
            Equal(1, changes.Count, "one change");
            Equal("Warrior|I wanted very little", string.Join("|", changes[0].Texts.Select(text => text.Text)), "texts");
            Equal(0, differ.Apply(warrior).Count, "not repeated");
        }

        private static void CounterTicksAreIgnored()
        {
            var differ = new ScreenDiffer();
            var before = Snapshot(Layer(1, 0, true, Text(10, "Ranger"), Text(12, "Time Left: 0H 0M 5S")));
            differ.Apply(before);
            differ.Apply(before);
            var after = Snapshot(Layer(1, 0, true, Text(10, "Ranger"), Text(12, "Time Left: 0H 0M 4S")));
            differ.Apply(after);
            Equal(0, differ.Apply(after).Count, "digit-only change");
            Equal(true, ScreenDiffer.SameBesideNumbers("11 minutos 2 segundos", "11 minutos 1 segundo"), "a clock passing 1 (Spanish)");
            Equal(true, ScreenDiffer.SameBesideNumbers("11 minutos 1 segundo", "11 minutos 0 segundos"), "and on to 0");
            Equal(true, ScreenDiffer.SameBesideNumbers("1 Sekunde", "2 Sekunden"), "German plural");
            Equal(true, ScreenDiffer.SameBesideNumbers("1 minute 5 seconds", "2 minutes 5 seconds"), "English plural");
            Equal(false, ScreenDiffer.SameBesideNumbers("3 rounds left", "3 lives left"), "another word is news");
            Equal(true, ScreenDiffer.IsTick("999 oro", "1.000 oro"), "a counter gaining its thousands separator");
            Equal(true, ScreenDiffer.IsTick("Gold 1,000", "Gold 5"), "or losing it");
            Equal(false, ScreenDiffer.IsTick("999 oro", "999 puntos"), "another word is still news");
        }

        /// <summary>A merchant's tab strip is built again at every sale and "Recomprar -2-" was said 63 times in a day (play log 2026-11-13).</summary>
        private static void AStripBuiltAgainIsNotNews()
        {
            ScreenSnapshot Shop(long first, params string[] extra) => Snapshot(Layer(1, 0, true,
                new[] { Text(5, "7.491 oro") }.Concat(new[] { "-2-", "Recomprar", "-1-" }.Concat(extra).Select((text, i) => Text(first + i, text))).ToArray()));
            var differ = new ScreenDiffer();
            differ.Apply(Shop(10));
            differ.Apply(Shop(10));
            Equal(0, differ.Apply(Shop(10)).Count, "the shop as it opened");
            Equal(0, differ.Apply(Shop(20)).Count, "one sighting of the strip's new elements");
            Equal(0, differ.Apply(Shop(20)).Count, "the same tabs on new elements are not news");
            differ.Apply(Shop(30, "Vendido"));
            Equal("-2-|Recomprar|-1-|Vendido", string.Join("|", differ.Apply(Shop(30, "Vendido")).SelectMany(change => change.Texts).Select(text => text.Text)), "with a new text among them all are told, as before");

            var notice = new ScreenDiffer();
            ScreenSnapshot Told(long address) => Snapshot(Layer(1, 0, true, Text(5, "7.491 oro"), Text(address, "No tienes oro suficiente")));
            notice.Apply(Told(10));
            notice.Apply(Told(10));
            notice.Apply(Told(10));
            notice.Apply(Told(20));
            Equal(1, notice.Apply(Told(20)).Count, "one notice shown again alone is said again");
        }

        /// <summary>The Options' "Guardar X" turns usable at the first change with its text as it was (heard 2026-11-10: only "Cancelar B").</summary>
        private static void APadButtonTurningUsableIsNews()
        {
            static ScreenSnapshot Options(bool canSave, bool skillReady = true) => Snapshot(
                Layer(1, 0, true, new ScreenText((IntPtr)10, "guardar X", "save", true, canSave)),
                new ScreenLayer((IntPtr)2, 1, new[] { new ScreenText((IntPtr)20, "Q", "skill", true, skillReady) }, true, false, true));
            var differ = new ScreenDiffer();
            differ.Apply(Options(false));
            differ.Apply(Options(false));
            Equal(0, differ.Apply(Options(false)).Count, "nothing to save: said with the panel, not again");
            Equal(0, differ.Apply(Options(true)).Count, "one sighting");
            var changes = differ.Apply(Options(true));
            Equal("guardar X", string.Join("|", changes.SelectMany(change => change.Texts).Select(text => text.Text)), "usable now");
            Equal(true, changes.SelectMany(change => change.Texts).All(text => text.IsEnabled), "and told as usable");
            Equal(0, differ.Apply(Options(true)).Count, "once");
            differ.Apply(Options(false));
            Equal(1, differ.Apply(Options(false)).Count, "saved: unavailable again is news too");
            differ.Apply(Options(false, false));
            Equal(0, differ.Apply(Options(false, false)).Count, "a control of the HUD (a skill on cooldown) is not followed");
        }

        /// <summary>The Options' bar: [LB] Gráficos … Entrada [RB]; "Entrada, RB" was said of it (2026-11-10).</summary>
        private static void TheEndsOfATabBarSayWhatTheyDo()
        {
            Equal(Announcements.PreviousTab, TabBar.EndLegend(0), "the first end button met is the previous tab's");
            Equal(Announcements.NextTab, TabBar.EndLegend(1), "the second the next tab's");
            Equal(false, Announcements.PreviousTab == Announcements.NextTab, "two legends");
            Equal(true, TabBar.IsPart("buttons_container", 0, false), "the bar, the control's first child");
            Equal(true, TabBar.IsPart("pages_container", 1, true), "then its pages");
            Equal(false, TabBar.IsPart("pages_container", 1, false), "pages with no bar before them");
            Equal(false, TabBar.IsPart("buttons_container", 3, false), "a row of buttons elsewhere in a dialog");
            Equal(false, TabBar.IsPart("label", 0, false), "any other element");
            Equal(true, TabBar.IsControl(2), "bar and pages under one element");
            Equal(false, TabBar.IsControl(1), "a row of buttons alone is no tab control");
        }

        private static void ClosedPopupIsNewsAgain()
        {
            var differ = Settled(out var screen);
            var withPopup = Snapshot(screen.Layers.Append(Layer(3, 2, false, Text(30, "Notice"))).ToArray());
            differ.Apply(withPopup);
            differ.Apply(withPopup);
            differ.Apply(screen);
            differ.Apply(screen);
            differ.Apply(withPopup);
            Equal(true, differ.Apply(withPopup).Any(change => change.Kind == ScreenChangeKind.LayerOpened), "reopened");
        }

        private static void InGameBaseLayersNeverOwnInput()
        {
            var differ = new ScreenDiffer { FreezeBaseLayers = true };
            var arrival = Snapshot(Layer(1, 0, true, Text(10, "HUD")), Layer(2, 5, true, Text(20, "Chat")));
            differ.Apply(arrival);
            Equal(0, differ.Apply(arrival).Count, "arrival is silent");
            var inventory = Snapshot(arrival.Layers.Append(Layer(3, 3, true, Text(30, "Inventory"))).ToArray());
            differ.Apply(inventory);
            var changes = differ.Apply(inventory);
            Equal(true, changes.Any(change => change.Kind == ScreenChangeKind.TopLayerChanged && change.Layer.Address == (IntPtr)3),
                "a panel opened later owns input even below a base layer");

            // The tutorial box already showing on arrival ("L to move, R to aim") is read; the rest stays silent.
            var tutorial = new ScreenDiffer { FreezeBaseLayers = true, ArrivalLayer = (IntPtr)4 };
            var start = Snapshot(Layer(1, 0, true, Text(10, "HUD")), Layer(4, 13, false, Text(40, "L to move")));
            tutorial.Apply(start);
            var read = tutorial.Apply(start);
            Equal("TextsAppeared 4 L to move", string.Join("|", read.Select(change => $"{change.Kind} {change.Layer.Address} {change.Texts[0].Text}")),
                "the arrival layer's texts are news in the baseline");
        }

        private static void TypingIsReportedAsTheEdit()
        {
            Equal(new TextEdit("i", string.Empty), TextEdit.Between("Yark", "Yarki"), "typed one");
            Equal(new TextEdit(string.Empty, "s"), TextEdit.Between("Yarkidius", "Yarkidiu"), "deleted one");
            Equal(new TextEdit("Yarkidius", string.Empty), TextEdit.Between(string.Empty, "Yarkidius"), "pasted into empty");
            Equal<TextEdit?>(null, TextEdit.Between("abc", "abd"), "replaced");
        }

        private static void OnlyTheLoadingScreenSpeaksWhileAnAreaLoads()
        {
            ScreenText Text(string name) => new(IntPtr.Zero, "text", name, false, true);
            Equal(true, Text("loading_screen_tip_label").IsLoadingScreen, "the tip");
            Equal(true, Text("loading_screen_area_name").IsLoadingScreen, "the area's name");
            Equal(false, Text("quest_title").IsLoadingScreen, "the HUD left in the tree");
            Equal(false, Text(string.Empty).IsLoadingScreen, "a chat line");
        }

        private static void DiabloLoopsStopAtTheirTopPitch()
        {
            Equal(1.12f, ItemSoundSet.LoopPitch(1.26f), "straight up stops at the top");
            Equal(1.05f, ItemSoundSet.LoopPitch(1.05f), "under it nothing changes");
            Equal(0.8f, ItemSoundSet.LoopPitch(0.8f), "down still sounds down");
        }

        private static void CreationNameDeadEndsAreSaid()
        {
            const int Max = 23;
            var typed = new CreationPresses(true, false, false);
            var enter = new CreationPresses(false, true, false);
            var a = new CreationPresses(false, false, true);
            var grey = new Func<bool>(() => true);
            var lit = new Func<bool>(() => false);
            var unread = new Func<bool>(() => throw new InvalidOperationException("Continue read for nothing"));
            CreationAdvice Typing(CreationNameGuide guide, long now, string name, CreationPresses presses, Func<bool> state) =>
                guide.Tick(now, CreationSpot.Typing, name, presses, state);

            var guide = new CreationNameGuide(Max);
            Equal(CreationAdvice.None, Typing(guide, 0, "ab", default, unread), "entering the field");
            Equal(CreationAdvice.None, Typing(guide, 10, "ab", typed, unread), "a key is judged later");
            Equal(CreationAdvice.None, Typing(guide, 100, "ab", default, unread), "too soon to judge");
            Equal(CreationAdvice.CharacterRefused, Typing(guide, 300, "ab", default, unread), "a digit changes nothing");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "ab", default, unread);
            Typing(guide, 10, "ab", typed, unread);
            Equal(CreationAdvice.None, Typing(guide, 40, "abc", default, unread), "a letter shows up");
            Equal(CreationAdvice.None, Typing(guide, 400, "abc", default, unread), "and nothing is said later");

            guide = new CreationNameGuide(Max);
            var full = new string('a', Max);
            Typing(guide, 0, full, default, unread);
            Typing(guide, 10, full, typed, unread);
            Equal(CreationAdvice.NameFull, Typing(guide, 300, full, default, unread), "a letter past the limit");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "ab", default, unread);
            Equal(CreationAdvice.NameTooShort, Typing(guide, 10, "ab", enter, grey), "Enter with Continue grey");
            Equal(CreationAdvice.None, Typing(guide, 20, "ab", enter, grey), "a held Enter says it once");
            Equal(CreationAdvice.NameTooShort, Typing(guide, 2000, "ab", enter, grey), "pressed again later");
            Equal(CreationAdvice.None, Typing(guide, 3000, "abc", enter, lit), "Enter with a good name creates");
            Equal(CreationAdvice.HowToFinish, Typing(guide, 4000, "abc", a, lit), "A in the field does not create");
            Equal(CreationAdvice.NameTooShort, Typing(guide, 6000, "ab", a, grey), "A in the field, name too short");

            guide = new CreationNameGuide(Max);
            Equal(CreationAdvice.None, Typing(guide, 0, "abc", a, unread), "the A that opened the field");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "ab", default, unread);
            Equal(CreationAdvice.NameTooShort, guide.Tick(10, CreationSpot.Outside, "ab", default, grey), "B with Continue grey");
            Equal(CreationAdvice.TypeOnTheName, guide.Tick(20, CreationSpot.Outside, "ab", typed, unread), "typing on the frame");
            Equal(CreationAdvice.None, guide.Tick(30, CreationSpot.Outside, "ab", typed, unread), "a held key says it once");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "abc", default, unread);
            Equal(CreationAdvice.None, guide.Tick(10, CreationSpot.Outside, "abc", default, lit), "B with a good name lands on Continue");

            guide = new CreationNameGuide(Max);
            Equal(CreationAdvice.None, guide.Tick(0, CreationSpot.Elsewhere, string.Empty, typed, unread), "typing on the class choice");
        }

        private static void ChatReportsOnlyAppendedLines()
        {
            var before = new[] { "a", "b", "c" };
            Equal("d", string.Join("|", ChatDiff.Appended(before, new[] { "a", "b", "c", "d" })), "appended");
            Equal("d|e", string.Join("|", ChatDiff.Appended(before, new[] { "c", "d", "e" })), "window slid");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "a", "b", "c" })), "rebuilt elements, same lines");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "x", "y", "a", "b", "c" })), "history expanded");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "b", "c" })), "list shrank");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "p", "q" })), "unrelated list is a new baseline");
            Equal("AFK|AFK", string.Join("|", ChatDiff.Appended(new[] { "AFK", "x" }, new[] { "AFK", "x", "AFK", "AFK" })), "repeated text counts");
        }

        private static void L3GesturesAreToldApart()
        {
            Equal("Tap", Play(Down(0), Up(100), Up(450)), "single tap after the double-tap window");
            Equal(true, L3Gestures.MovedFocus(1000, 1040) && !L3Gestures.MovedFocus(1000, 700), "a focus born of the press is the game's doing, an older one is read");
            Equal("DoubleTap", Play(Down(0), Up(100), Down(200), Up(300), Up(700)), "two quick taps");
            Equal("Hold", Play(Down(0), Down(600), Up(900), Up(1300)), "held past the threshold");
            Equal("FlickUp|FlickRight", Play(Down(0), Stick(100, 0f, 0.9f), Stick(150, 0f, 0f), Stick(200, 0.9f, 0f), Stick(900, 0.9f, 0f), Up(1000), Up(1400)),
                "flicks while held, no hold or tap");
            Equal("Tap", Play(Down(0), Stick(100, 0f, 0.5f), Up(200), Up(600)), "half deflection is no flick");
        }

        private static void StickDirectionsBinInWorldSpace()
        {
            // Isometric diamond: world +X goes right-down on screen, +Y left-down (y down).
            var x = new System.Numerics.Vector2(1f, 0.5f);
            var y = new System.Numerics.Vector2(-1f, 0.5f);
            Equal(0, StickDirection.Octant(x, y, 1f, -1f), "screen right");
            Equal(2, StickDirection.Octant(x, y, -1f, -1f), "screen up");
            Equal(6, StickDirection.Octant(x, y, 1f, 1f), "screen down");
            Equal(7, StickDirection.Octant(x, y, 1f, 0f), "world +X is down-right");
            // 30 degrees above the stick's right in world space: up-right, although the squash draws it flatter.
            Equal(1, StickDirection.Octant(x, y, 0.299f, -1.115f), "binned in world space");
        }

        private static void ScannerBrowsesNearestFirstAndFollowsTheEntity()
        {
            var exit = new ScanEntry(1, ScanCategory.Exits, "Clearfell", 0, 0, 0);
            var near = new ScanEntry(2, ScanCategory.Npcs, "Renly", 0, 0, 0);
            var far = new ScanEntry(3, ScanCategory.Npcs, "Una", 0, 0, 0);
            var browser = new ScanBrowser();
            var list = new[] { exit, near, far };
            Equal("Clearfell True", Said(browser.ChangeEntry(list, 1, 0)), "first flick opens the first category");
            Equal("Renly True", Said(browser.ChangeCategory(list, 1, 100)), "next category, nearest entry");
            Equal("Una False", Said(browser.ChangeEntry(list, 1, 200)), "next entry");
            Equal("Renly False", Said(browser.ChangeEntry(list, 1, 300)), "wraps");
            Equal("Una False", Said(browser.ChangeEntry(list, -1, 400)), "wraps backwards");
            Equal("Renly False", Said(browser.ChangeEntry(new[] { exit, far, near }, 1, 500)), "follows Una after a re-sort");
            Equal("Una False", Said(browser.ChangeEntry(new[] { exit, far, near }, 1, 20000)), "stale: nearest again");
            Equal("Clearfell True", Said(browser.ChangeCategory(list, 1, 20100)), "categories wrap");
            Equal("None False", Said(browser.ChangeCategory(Array.Empty<ScanEntry>(), 1, 20200)), "nothing around");
            browser.ChangeCategory(list, 1, 30000);
            browser.ChangeEntry(list, 1, 30100);
            Equal("Una", browser.Current(list, 90000)?.Name, "the choice stays however long the walk");
            Equal(null, browser.Current(new[] { exit, near }, 90100)?.Name, "gone once its entity is");
        }

        private static void RouteLegsKeepRealTurnsOnly()
        {
            // A* staircase: a slanted run east (two east, one north-east, repeated), a 4-cell jog west, then a run north.
            var route = new List<System.Numerics.Vector2> { new(0, 0) };
            void Walk(int dx, int dy, int times)
            {
                for (var i = 0; i < times; i++)
                {
                    route.Add(route[^1] + new System.Numerics.Vector2(dx, dy));
                }
            }

            for (var i = 0; i < 10; i++)
            {
                Walk(1, 0, 2);
                Walk(1, 1, 1);
            }

            Walk(-1, 0, 4);
            Walk(0, 1, 40);
            var corners = RoutePlanner.Corners(route, 3f);
            Equal(4, corners.Count, "staircase gone, the jog's two corners kept");
            static int Octant(System.Numerics.Vector2 delta) =>
                (int)Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4) + 8) % 8;
            var legs = RoutePlanner.Legs(corners, Octant, 5f);
            Equal("0:32 2:40", string.Join(" ", legs.Select(leg => $"{leg.Direction}:{Math.Round(leg.Length)}")), "the jog is dropped");
            Equal(1, RoutePlanner.Legs(RoutePlanner.Corners(route.Take(3).ToList(), 3f), Octant, 5f).Count, "a short route keeps its way");
        }

        private static string Said(ScanAnswer answer) => $"{answer.Entry?.Name ?? "None"} {answer.CategoryChanged}";

        private static (long At, bool L3, float X, float Y) Down(long at) => (at, true, 0f, 0f);

        private static (long At, bool L3, float X, float Y) Up(long at) => (at, false, 0f, 0f);

        private static (long At, bool L3, float X, float Y) Stick(long at, float x, float y) => (at, true, x, y);

        private static string Play(params (long At, bool L3, float X, float Y)[] samples)
        {
            var gestures = new L3Gestures();
            var seen = new List<string>();
            foreach (var sample in samples)
            {
                seen.AddRange(gestures.Update(sample.At, sample.L3, sample.X, sample.Y).Select(gesture => gesture.ToString()));
            }

            return string.Join("|", seen);
        }
    }
}
