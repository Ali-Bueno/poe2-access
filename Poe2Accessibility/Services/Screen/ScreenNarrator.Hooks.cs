namespace Poe2Accessibility.Services
{
    using System;
    using Poe2Accessibility.Models;

    /// <summary>Providers and switches the plugin wires into <see cref="ScreenNarrator"/>: focus, panel and selector lookups, input timing and reading options.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>The layer holding the focus (FocusTracker), to notice a focus that moved into a popup.</summary>
        public Func<IntPtr>? FocusLayer { get; set; }

        /// <summary>The focused node described again (its panel may have been built since); null when gone.</summary>
        public Func<AccessibleUiNode, AccessibleUiNode?>? Refresh { get; set; }

        /// <summary>Whether a text lies inside the panel with the given title; null reads every text.</summary>
        public Func<IntPtr, string, bool>? InPanel { get; set; }

        /// <summary>Whether a text (first) belongs to another group of controls than the focus (second).</summary>
        public Func<IntPtr, IntPtr, bool>? InOtherControlGroup { get; set; }

        /// <summary>The title of the panel open beside the inventory (salvage bench, stash), empty when none.</summary>
        public Func<string>? SidePanel { get; set; }

        /// <summary>Whether a text lies in another row of the list the focus is a row of (text, focus).</summary>
        public Func<IntPtr, IntPtr, bool>? InSiblingRow { get; set; }

        /// <summary>Whether a text lies in another card of the shop's list than the focused card (text, focus).</summary>
        public Func<IntPtr, IntPtr, bool>? InAnotherShopCard { get; set; }

        /// <summary>Whether the microtransaction shop is the panel shown. Set by the plugin.</summary>
        public Func<bool>? ShopShown { get; set; }

        /// <summary>Read a screen's message with its first focus (delete character); off for the game menu.</summary>
        public bool ReadScreenMessages { get; set; } = true;

        /// <summary>An area is loading: the HUD and the chat can stay in the tree, so only the loading screen's own lines are news.</summary>
        public bool LoadingArea { get; set; }

        /// <summary>The screen's title as the state announcement already said it ("Menú del juego."); empty for none.</summary>
        public string SaidTitle { get; set; } = string.Empty;

        /// <summary>Milliseconds since the confirm button (A) was last pressed.</summary>
        public Func<long>? SinceConfirm { get; set; }

        /// <summary>Milliseconds since a direction (d-pad or a stick) was last held.</summary>
        public Func<long>? SinceDirection { get; set; }

        /// <summary>Read long tooltips with the focus (setting); short ones always are.</summary>
        public bool ReadTooltips { get; set; }

        /// <summary>"4 de 6" for a selector without focus in a layer (the class choice), else null.</summary>
        public Func<IntPtr, string?>? SelectorPosition { get; set; }

        /// <summary>The choice a selector without focus holds in a layer (the class id), else null.</summary>
        public Func<IntPtr, int?>? SelectorChoice { get; set; }
    }
}
