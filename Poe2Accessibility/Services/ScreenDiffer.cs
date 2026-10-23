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

        private readonly HashSet<IntPtr> stableLayers = new();
        private readonly HashSet<IntPtr> baseLayers = new();
        private readonly Dictionary<IntPtr, string> stableTexts = new();
        private ScreenSnapshot previous = ScreenSnapshot.Empty;
        private IntPtr stableTop;
        private bool hasBaseline;

        /// <summary>
        ///     In game the layers confirmed in the baseline (HUD, minimap) become base layers that never own input;
        ///     pre-game the baseline's top layer can be a popup (terms window) and must stay eligible.
        /// </summary>
        public bool FreezeBaseLayers { get; set; }

        /// <summary>The last snapshot passed to Apply, with base layers marked.</summary>
        public ScreenSnapshot Last { get; private set; } = ScreenSnapshot.Empty;

        /// <summary>Forget everything; the next confirmed snapshot becomes a silent baseline (new game state).</summary>
        public void Reset()
        {
            this.baseLayers.Clear();
            this.stableLayers.Clear();
            this.stableTexts.Clear();
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
            foreach (var layer in current.Layers)
            {
                if (!previousLayers.ContainsKey(layer.Address))
                {
                    continue;
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
                    changes.Add(new ScreenChange(ScreenChangeKind.TextsAppeared, layer, appeared));
                }
            }

            var baselineNow = !this.hasBaseline && current.Layers.Count > 0 && current.Layers.All(layer => this.stableLayers.Contains(layer.Address));
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
