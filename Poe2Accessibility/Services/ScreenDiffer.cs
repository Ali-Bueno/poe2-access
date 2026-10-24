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

        /// <summary>Snapshots after texts went away within which arrivals in the same layer count as a scroll.</summary>
        private const int ScrollSnapshots = 3;

        private readonly HashSet<IntPtr> stableLayers = new();
        private readonly HashSet<IntPtr> baseLayers = new();
        private readonly Dictionary<IntPtr, string> stableTexts = new();

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

        /// <summary>The last snapshot passed to Apply, with base layers marked.</summary>
        public ScreenSnapshot Last { get; private set; } = ScreenSnapshot.Empty;

        /// <summary>Forget everything; the next confirmed snapshot becomes a silent baseline (new game state).</summary>
        public void Reset()
        {
            this.baseLayers.Clear();
            this.stableLayers.Clear();
            this.stableTexts.Clear();
            this.vanishedAt.Clear();
            this.previous = ScreenSnapshot.Empty;
            this.stableTop = IntPtr.Zero;
            this.hasBaseline = false;
        }

        public IReadOnlyList<ScreenChange> Apply(ScreenSnapshot current)
        {
            current = current.WithBase(this.baseLayers);
            var changes = new List<ScreenChange>();
            var previousLayers = this.previous.Layers.ToDictionary(layer => layer.Address);
            var previousTexts = this.previous.Layers.SelectMany(layer => layer.Texts).GroupBy(text => text.Address)
                .ToDictionary(group => group.Key, group => group.First().Text);
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

                var opened = this.stableLayers.Add(layer.Address);
                var appeared = new List<ScreenText>();
                foreach (var text in layer.Texts)
                {
                    var confirmed = previousTexts.TryGetValue(text.Address, out var before) && before == text.Text;
                    if (!confirmed || (this.stableTexts.TryGetValue(text.Address, out var known) && known == text.Text))
                    {
                        continue;
                    }

                    var counterTick = known != null && Digits.Replace(known, "#") == Digits.Replace(text.Text, "#");
                    this.stableTexts[text.Address] = text.Text;
                    if (!opened && !counterTick)
                    {
                        appeared.Add(text);
                    }
                }

                if (!this.hasBaseline)
                {
                    continue;
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

        /// <summary>Layers and texts absent from two snapshots in a row are forgotten, so a reappearance is news again.</summary>
        private void Forget(ScreenSnapshot current)
        {
            var visibleLayers = current.Layers.Select(layer => layer.Address).Concat(this.previous.Layers.Select(layer => layer.Address)).ToHashSet();
            this.stableLayers.RemoveWhere(address => !visibleLayers.Contains(address));
            var visibleTexts = current.Layers.Concat(this.previous.Layers).SelectMany(layer => layer.Texts).Select(text => text.Address).ToHashSet();
            foreach (var address in this.stableTexts.Keys.Where(address => !visibleTexts.Contains(address)).ToList())
            {
                this.stableTexts.Remove(address);
            }
        }
    }
}
