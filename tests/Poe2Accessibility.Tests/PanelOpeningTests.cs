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

    /// <summary>What a panel says as it opens, its layer reported before its cursor: the order the inventory came in (2026-11-24).</summary>
    public static class PanelOpeningTests
    {
        /// <summary>Past the moment a page waits for its focus (300 ms).</summary>
        private const int SettledMs = 350;

        /// <summary>Past the moment a favours window's tribute waits to be right (1200 ms).</summary>
        private const int TributeSettledMs = 1000;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheInventoryOpensWithItsCursorOnly), TheInventoryOpensWithItsCursorOnly),
            (nameof(APanelToMoveThroughLeavesItsRowsToTheCursor), APanelToMoveThroughLeavesItsRowsToTheCursor),
            (nameof(AFreeCursorsPageIsStillRead), AFreeCursorsPageIsStillRead),
            (nameof(TheInventorysGoldIsSaidOnceAnOpening), TheInventorysGoldIsSaidOnceAnOpening),
            (nameof(TheTributeGoesBeforeEverythingInTheFavours), TheTributeGoesBeforeEverythingInTheFavours),
            (nameof(AnAtlasPopupIsKnownSaidForItsLanding), AnAtlasPopupIsKnownSaidForItsLanding),
        };

        /// <summary>The Atlas node's popup, read as it appears, is what its landing line leaves out (the owner, 2026-11-28).</summary>
        private static void AnAtlasPopupIsKnownSaidForItsLanding()
        {
            Localization.Set("en", new());
            using var output = new Recorder();
            var narrator = new ScreenNarrator(output, Log(), _ => string.Empty);
            narrator.Reset(true);
            var hop = DateTime.UtcNow;
            var popup = Layer(17, 0, false, Text(1, "Oasis"), Text(2, "Biome: Desert"), Text(3, "Hidden amidst the sun-bleached wastes."));
            narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.TextsAppeared, popup, popup.Texts), Snapshot(popup), null, IntPtr.Zero);
            Equal("Oasis. Biome: Desert. Hidden amidst the sun-bleached wastes.", string.Join(" | ", output.Said), "the popup, read as it appears");
            Equal(true, narrator.SaidSince("Oasis", hop) && narrator.SaidSince("Biome: Desert", hop), "its title and biome line were said this hop");
            Equal(false, narrator.SaidSince("Oasis", DateTime.UtcNow.AddSeconds(1)), "not for a hop after it");
            Equal(false, narrator.SaidSince("Biome: Forest", hop), "nor a biome it did not say");

            var landing = new AtlasLandingWords("Oasis", "Accessible", "Biome: Desert", "Desert", string.Empty, "Links: down left, Hidden Grotto");
            Equal("Accessible. Links: down left, Hidden Grotto", landing.Spoken(text => narrator.SaidSince(text, hop)), "the landing goes on with the state");
            Equal("Oasis. Accessible. Biome: Desert. Links: down left, Hidden Grotto", landing.Text, "the whole line, with nothing heard");
            Equal("Accessible. Biome: Desert. Links: down left, Hidden Grotto", landing.Spoken(text => text == "Oasis"),
                "a biome line the last node shared never read as new: the landing says it");
            Equal(string.Empty, AtlasLandingWords.None.Spoken(_ => false), "no node, nothing");
        }

        private static void TheInventoryOpensWithItsCursorOnly()
        {
            var said = Open(Cell("Inventario"), "Inventario", "35, 1", "IV", "1, 1", "9", "7", "XI", "1", "X", "X", "XI, III", "+25, Oro", "Oro");
            Equal("Inventario. 112.462 oro. Wisdom, columna 1, fila 1", said, "spoken");
            Equal("Inventario", Open(null, "Inventario", "35, 1", "IV", "7", "XI", "XI, III", "+25, Oro", "Oro"), "spoken");
        }

        private static void APanelToMoveThroughLeavesItsRowsToTheCursor()
        {
            var said = Open(Cell("Inventario"), "Habilidades", "Skill one", "Skill two", "Skill three", "Skill four", "Skill five");
            Equal("Habilidades. Inventario. 112.462 oro. Wisdom, columna 1, fila 1", said, "spoken");
        }

        private static void AFreeCursorsPageIsStillRead()
        {
            var said = Open(null, "Pasivas", "11", "DEX", "Points left", "One", "Two", "Three", "Four");
            Equal("Pasivas. DEX 11. Points left. One. Two. Three. Four", said, "spoken");
        }

        private static void TheInventorysGoldIsSaidOnceAnOpening()
        {
            using var output = new Recorder();
            var narrator = new ScreenNarrator(output, Log(), _ => string.Empty) { GoldOf = Gold };
            narrator.Reset(true);
            narrator.OnFocusChanged(Cell("Inventario"));
            narrator.OnFocusChanged(Cell("Habilidades"));
            narrator.OnFocusChanged(Cell("Inventario"));
            Equal("Inventario. 112.462 oro. Wisdom, columna 1, fila 1 | Habilidades. Wisdom, columna 1, fila 1 | Inventario. Wisdom, columna 1, fila 1",
                string.Join(" | ", output.Said), "spoken");
        }

        private static void TheTributeGoesBeforeEverythingInTheFavours()
        {
            const string Whole = "1.155 de tributo. Favores. Wisdom, columna 1, fila 1 | Modo de reservas";
            Equal(Whole, Favours("1.155 Tributo", cursorFirst: true, settledMs: 0), "cursor first");
            Equal(Whole, Favours("1.155 Tributo", cursorFirst: false, settledMs: 0), "window first");
            Equal(string.Empty, Favours("990 Tributo", cursorFirst: true, settledMs: 0), "the last window's tribute is not said");
            Equal(Whole, Favours("990 Tributo", cursorFirst: true, settledMs: TributeSettledMs), "a tribute the HUD never shows");
        }

        /// <summary>The favours window opens with the cursor in it, before or after its layer; what is said by then.</summary>
        private static string Favours(string onHud, bool cursorFirst, int settledMs)
        {
            var hud = Layer(12, 0, false, Text(50, onHud));
            var layer = Layer(17, 1, true, Text(1, "Favores"), Text(2, "1.155 de tributo"), Text(3, "Modo de reservas"));
            using var output = new Recorder();
            var narrator = new ScreenNarrator(output, Log(), _ => string.Empty)
            {
                IsTributeLabel = address => address == (IntPtr)2, InFavoursWindow = _ => true, FocusLayer = () => layer.Address, HudLayer = hud.Address,
            };
            narrator.Reset(true);
            narrator.CheckShopBalance(Snapshot(hud));
            var cell = Cell("Favores");
            var screen = Snapshot(layer);
            if (cursorFirst)
            {
                narrator.OnFocusChanged(cell);
            }

            narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.TopLayerChanged, layer, layer.Texts), screen,
                cursorFirst ? cell : null, cursorFirst ? layer.Address : IntPtr.Zero);
            if (!cursorFirst)
            {
                narrator.OnFocusChanged(cell);
            }

            narrator.CheckShopBalance(screen);
            Thread.Sleep(SettledMs + settledMs);
            narrator.Tick();
            narrator.CheckShopBalance(screen);
            return string.Join(" | ", output.Said);
        }

        private static string Gold(string panel) => panel == "Inventario" ? "112.462 oro" : string.Empty;

        private static PluginLog Log() => new(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);

        /// <summary>The panel's layer comes on top with no focus known, then the focus (none: a free cursor), then time passes.</summary>
        private static string Open(AccessibleUiNode? focus, string title, params string[] shown)
        {
            var gold = Text(900, "112.462 oro");
            var texts = shown.Select((text, at) => Text(100 + at, text)).Append(gold)
                .Append(new ScreenText((IntPtr)901, title, "label_tab_title", false, true)).ToArray();
            var layer = Layer(17, 0, true, texts);
            using var output = new Recorder();
            var narrator = new ScreenNarrator(output, Log(), _ => string.Empty) { GoldCounter = () => gold.Address, GoldOf = Gold };
            narrator.Reset(true);
            narrator.OnScreenChange(new ScreenChange(ScreenChangeKind.TopLayerChanged, layer, texts), Snapshot(layer), null, IntPtr.Zero);
            if (focus != null)
            {
                narrator.OnFocusChanged(focus);
            }

            Thread.Sleep(SettledMs);
            narrator.Tick();
            return string.Join(" | ", output.Said);
        }

        private static AccessibleUiNode Cell(string panel) => new()
        {
            Address = (IntPtr)500, Role = UiRole.GridCell, Text = "Wisdom", Value = "columna 1, fila 1", Context = panel, IsVisible = true,
        };

        private sealed class Recorder : IAccessibilityOutputService
        {
            public List<string> Said { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Said.Add(text);

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
