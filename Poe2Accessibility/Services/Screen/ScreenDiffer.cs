namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Turns consecutive screen snapshots into changes worth speaking. A layer or text must be seen in two
    ///     snapshots in a row before it counts (screens build over several frames), and a text whose change
    ///     only touches digits is a live counter (timer, queue position), never news.
    /// </summary>
    public sealed class ScreenDiffer
    {
        private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);

        /// <summary>
        ///     A number and the unit word after it, whose ending follows the number ("1 segundo" / "0 segundos", "1 Sekunde" /
        ///     "2 Sekunden"): a clock passing 1 is still a tick (the Instance Manager's time left, 2026-11-08).
        /// </summary>
        private static readonly Regex CountedWord = new(@"\d+(\s+\p{L}+?)(?:s|n)?\b", RegexOptions.Compiled);

        /// <summary>A number written with separators ("7.491", "1,000", "58,2"): one number, so gold passing a thousand is still a tick.</summary>
        private static readonly Regex Grouped = new(@"\d+(?:[.,]\d+)+", RegexOptions.Compiled);

        /// <summary>A zoom factor ("x1,4", the passive tree's LT/RT): news when it changes, unlike a counter.</summary>
        public static readonly Regex ZoomFactor = new(@"^x\d+([.,]\d+)?$", RegexOptions.Compiled);

        /// <summary>Snapshots after texts went away within which arrivals in the same layer count as a scroll.</summary>
        private const int ScrollSnapshots = 3;

        /// <summary>A lone text shown again may be a notice repeated; from this many together a part of the layer was built again.</summary>
        private const int RebuiltTogether = 2;

        /// <summary>
        ///     Built again means the new element came as the old one went: a snapshot without either between them is a notice
        ///     closed and shown again, which is news.
        /// </summary>
        private const int RebuiltWithin = 1;

        /// <summary>Per layer and text, the element that showed it last and the snapshot it was last seen in.</summary>
        private readonly Dictionary<(IntPtr Layer, string Text), (IntPtr Address, long Seen)> shownBy = new();

        /// <summary>New elements showing what another element of their layer showed until it went away.</summary>
        private readonly HashSet<IntPtr> rebuilt = new();

        private readonly HashSet<IntPtr> stableLayers = new();
        private readonly HashSet<IntPtr> baseLayers = new();
        private readonly Dictionary<IntPtr, string> stableTexts = new();

        /// <summary>The stable texts that are a control shown unavailable ("guardar X" with nothing to save).</summary>
        private readonly HashSet<IntPtr> stableOff = new();

        /// <summary>A control that cannot be used now: in a panel, it turning usable or back is news though its text stays.</summary>
        private static bool IsOff(ScreenText text) => text.IsControl && !text.IsEnabled;

        /// <summary>Per layer, the snapshot number in which some of its texts last went away.</summary>
        private readonly Dictionary<IntPtr, long> vanishedAt = new();
        private long applied;
        private ScreenSnapshot previous = ScreenSnapshot.Empty;
        private IntPtr stableTop;
        private bool hasBaseline;

        /// <summary>
        ///     In game the layers confirmed in the baseline (HUD, minimap) become base layers that never own input;
        ///     pre-game the baseline's top layer can be a popup (terms window) and must stay eligible.
        /// </summary>
        public bool FreezeBaseLayers { get; set; }

        /// <summary>
        ///     A layer whose texts are news even in the silent baseline (the tutorial box: a new character arrives with
        ///     "L to move, R to aim" already on screen). Zero for none.
        /// </summary>
        public IntPtr ArrivalLayer { get; set; }

        /// <summary>Texts of the last Apply whose digits alone changed in a window (not the HUD), apart from its changes: they are no news by themselves.</summary>
        public IReadOnlyList<ScreenChange> Ticks => this.ticks;

        private readonly List<ScreenChange> ticks = new();

        /// <summary>The last snapshot passed to Apply, with base layers marked.</summary>
        public ScreenSnapshot Last { get; private set; } = ScreenSnapshot.Empty;

        /// <summary>Forget everything; the next confirmed snapshot becomes a silent baseline (new game state).</summary>
        public void Reset()
        {
            this.baseLayers.Clear();
            this.stableLayers.Clear();
            this.stableTexts.Clear();
            this.stableOff.Clear();
            this.vanishedAt.Clear();
            this.shownBy.Clear();
            this.rebuilt.Clear();
            this.previous = ScreenSnapshot.Empty;
            this.stableTop = IntPtr.Zero;
            this.hasBaseline = false;
        }

        /// <summary>The same text with only its numbers changed: a counter ticking, not a new line.</summary>
        public static bool SameBesideNumbers(string a, string b) => Ticked(a) == Ticked(b);

        private static string Ticked(string text) => Digits.Replace(CountedWord.Replace(text, "#$1"), "#");

        /// <summary>A counter ticking in a text nobody rests on: as <see cref="SameBesideNumbers"/>, with a number's separators part of it.</summary>
        public static bool IsTick(string a, string b) => Shape(a) == Shape(b);

        /// <summary>A text without its numbers ("Maná, #/#"): what two readings of one counter share.</summary>
        public static string Shape(string text) => Ticked(Grouped.Replace(text, "0"));

        public IReadOnlyList<ScreenChange> Apply(ScreenSnapshot current)
        {
            current = current.WithBase(this.baseLayers);
            var changes = new List<ScreenChange>();
            this.ticks.Clear();
            var previousLayers = this.previous.Layers.ToDictionary(layer => layer.Address);
            var previousTexts = this.previous.Layers.SelectMany(layer => layer.Texts).GroupBy(text => text.Address)
                .ToDictionary(group => group.Key, group => group.First().Text);
            var previousOff = this.previous.Layers.SelectMany(layer => layer.Texts).Where(IsOff).Select(text => text.Address).ToHashSet();
            this.applied++;
            foreach (var layer in current.Layers)
            {
                if (!previousLayers.TryGetValue(layer.Address, out var previousLayer))
                {
                    continue;
                }

                if (previousLayer.Texts.Any(old => layer.Texts.All(text => text.Address != old.Address)))
                {
                    this.vanishedAt[layer.Address] = this.applied;
                }

                this.NoteRebuilt(layer);
                var opened = this.stableLayers.Add(layer.Address);
                var appeared = new List<ScreenText>();
                var again = new List<ScreenText>();
                var ticked = new List<ScreenText>();
                var tickedBefore = new List<string>();
                foreach (var text in layer.Texts)
                {
                    var confirmed = previousTexts.TryGetValue(text.Address, out var before) && before == text.Text;
                    var known = this.stableTexts.GetValueOrDefault(text.Address);
                    var off = IsOff(text);
                    var flipped = !layer.IsBase && known == text.Text && previousOff.Contains(text.Address) == off && this.stableOff.Contains(text.Address) != off;
                    if (!confirmed || (known == text.Text && !flipped))
                    {
                        continue;
                    }

                    var counterTick = !flipped && known != null && IsTick(known, text.Text) && !ZoomFactor.IsMatch(text.Text);
                    this.stableTexts[text.Address] = text.Text;
                    _ = off ? this.stableOff.Add(text.Address) : this.stableOff.Remove(text.Address);
                    if (!opened && counterTick && !layer.IsBase)
                    {
                        // Told apart for the narrator, which says only a price the player's own press changed (NumberChanges).
                        ticked.Add(text);
                        tickedBefore.Add(known!);
                    }

                    if (!opened && !counterTick)
                    {
                        appeared.Add(text);
                        if (known == null && this.rebuilt.Remove(text.Address))
                        {
                            again.Add(text);
                        }
                    }
                }

                if (again.Count >= RebuiltTogether && again.Count == appeared.Count)
                {
                    // A tab strip the game builds again at every sale shows nothing new (docs/menu-speech-rules.md);
                    // with a new text among them they may be what it is read with, and all are told as before.
                    appeared.Clear();
                }

                if (!this.hasBaseline)
                {
                    continue;
                }

                if (ticked.Count > 0)
                {
                    this.ticks.Add(new ScreenChange(ScreenChangeKind.NumbersChanged, layer, ticked) { Before = tickedBefore });
                }

                if (opened && !layer.IsBase)
                {
                    changes.Add(new ScreenChange(ScreenChangeKind.LayerOpened, layer, layer.Texts));
                }
                else if (appeared.Count > 0)
                {
                    // A text needs two snapshots to count, so a scroll's arrivals come a snapshot or two after its departures.
                    var scrolled = this.vanishedAt.TryGetValue(layer.Address, out var gone) && this.applied - gone <= ScrollSnapshots;
                    changes.Add(new ScreenChange(ScreenChangeKind.TextsAppeared, layer, appeared) { Scrolled = scrolled });
                }
            }

            var baselineNow = !this.hasBaseline && current.Layers.Count > 0 && current.Layers.All(layer => this.stableLayers.Contains(layer.Address));
            if (baselineNow && this.ArrivalLayer != IntPtr.Zero && current.Layers.FirstOrDefault(layer => layer.Address == this.ArrivalLayer) is { Texts.Count: > 0 } arrival)
            {
                changes.Add(new ScreenChange(ScreenChangeKind.TextsAppeared, arrival, arrival.Texts));
            }
            if (baselineNow && this.FreezeBaseLayers)
            {
                this.baseLayers.UnionWith(current.Layers.Select(layer => layer.Address));
                current = current.WithBase(this.baseLayers);
            }

            var top = current.TopInteractive;
            if (top != null && top.Address == this.previous.TopInteractive?.Address && top.Address != this.stableTop)
            {
                this.stableTop = top.Address;
                changes.Add(new ScreenChange(ScreenChangeKind.TopLayerChanged, top, top.Texts));
            }

            this.Forget(current);
            this.hasBaseline |= baselineNow;
            this.previous = current;
            this.Last = current;
            return changes;
        }

        /// <summary>Marks the layer's new elements that show the text of one just gone from it.</summary>
        private void NoteRebuilt(ScreenLayer layer)
        {
            var showing = new Dictionary<IntPtr, string>(layer.Texts.Count);
            foreach (var text in layer.Texts)
            {
                showing[text.Address] = text.Text;
            }

            foreach (var text in layer.Texts)
            {
                var key = (layer.Address, text.Text);
                if (this.shownBy.TryGetValue(key, out var shown) && shown.Address != text.Address)
                {
                    if (showing.GetValueOrDefault(shown.Address) == text.Text)
                    {
                        continue; // two elements with one text: the first keeps it
                    }

                    if (!showing.ContainsKey(shown.Address) && this.applied - shown.Seen <= RebuiltWithin && !this.stableTexts.ContainsKey(text.Address))
                    {
                        this.rebuilt.Add(text.Address);
                    }
                }

                this.shownBy[key] = (text.Address, this.applied);
            }
        }

        /// <summary>Layers and texts absent from two snapshots in a row are forgotten, so a reappearance is news again.</summary>
        private void Forget(ScreenSnapshot current)
        {
            foreach (var key in this.shownBy.Where(entry => this.applied - entry.Value.Seen > ScrollSnapshots).Select(entry => entry.Key).ToList())
            {
                this.shownBy.Remove(key);
            }

            var visibleLayers = current.Layers.Select(layer => layer.Address).Concat(this.previous.Layers.Select(layer => layer.Address)).ToHashSet();
            this.stableLayers.RemoveWhere(address => !visibleLayers.Contains(address));
            var visibleTexts = current.Layers.Concat(this.previous.Layers).SelectMany(layer => layer.Texts).Select(text => text.Address).ToHashSet();
            this.rebuilt.RemoveWhere(address => !visibleTexts.Contains(address));
            foreach (var address in this.stableTexts.Keys.Where(address => !visibleTexts.Contains(address)).ToList())
            {
                this.stableTexts.Remove(address);
                this.stableOff.Remove(address);
            }
        }
    }
}
