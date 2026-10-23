namespace Poe2Accessibility.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     One visible text of a layer, in tree (reading) order; control texts carry the control's enabled state,
    ///     and <paramref name="Button"/> names the controller button shown next to it (hint bars, popups).
    /// </summary>
    public sealed record ScreenText(IntPtr Address, string Text, string Name, bool IsControl, bool IsEnabled, string Button = "");

    /// <summary>
    ///     A visible top-level child of the UI root: a screen, panel, popup or the controller hint bar.
    ///     <paramref name="IsBase"/> marks in-game layers present on arrival (HUD, minimap), which never own input.
    /// </summary>
    public sealed record ScreenLayer(IntPtr Address, int Index, IReadOnlyList<ScreenText> Texts, bool HasControls, bool IsHintBar, bool IsBase = false);

    /// <summary>The visible layers of the UI root at one moment, in root-child (drawing) order.</summary>
    public sealed record ScreenSnapshot(IntPtr Root, IReadOnlyList<ScreenLayer> Layers)
    {
        public static readonly ScreenSnapshot Empty = new(IntPtr.Zero, Array.Empty<ScreenLayer>());

        /// <summary>The game moves an opened popup above the screen it covers, so the highest layer with controls owns input.</summary>
        public ScreenLayer? TopInteractive => this.Layers.Where(layer => layer.HasControls && !layer.IsHintBar && !layer.IsBase).MaxBy(layer => layer.Index);

        public ScreenSnapshot WithBase(IReadOnlySet<IntPtr> baseLayers) => baseLayers.Count == 0
            ? this
            : this with { Layers = this.Layers.Select(layer => layer with { IsBase = baseLayers.Contains(layer.Address) }).ToList() };

        public ScreenLayer? HintBar => this.Layers.FirstOrDefault(layer => layer.IsHintBar);

        public ScreenLayer? Find(IntPtr address) => this.Layers.FirstOrDefault(layer => layer.Address == address);
    }

    public enum ScreenChangeKind
    {
        /// <summary>A layer became visible (popup, panel, new screen part).</summary>
        LayerOpened,

        /// <summary>Texts appeared or changed inside a layer that was already visible.</summary>
        TextsAppeared,

        /// <summary>Another layer now owns input (popup opened or closed, new screen); also reported for the first screen after a reset.</summary>
        TopLayerChanged,
    }

    public sealed record ScreenChange(ScreenChangeKind Kind, ScreenLayer Layer, IReadOnlyList<ScreenText> Texts);
}
