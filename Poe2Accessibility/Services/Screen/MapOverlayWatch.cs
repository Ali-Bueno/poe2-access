namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The large map laid over the world (D-pad down in play) shown or hidden: the game says so only with a hint in
    ///     its top-right corner ("Mantén presionado cruceta abajo para interactuar con el mapa"), visible only while the
    ///     map is open (2026-10-24), under the area, league and realm ("Campamento de Sierraclara", "Liga Forbidden
    ///     Rites", "Reino San Pablo (BR)", 2026-10-25). Said with those lines and the hint when it opens, and once more
    ///     when it closes.
    /// </summary>
    public sealed class MapOverlayWatch
    {
        private const int PollMs = 250;

        /// <summary>Overlay → info stats → info layout → hint.</summary>
        private const int HintDepth = 4;

        private const string HintName = "gamepad_move_map_hint";

        private readonly UiElementReader elements;
        private readonly Action<string> speak;
        private IntPtr overlay;
        private IntPtr hint;
        private bool? shown;
        private long lastPoll;

        public MapOverlayWatch(UiElementReader elements, Action<string> speak)
        {
            this.elements = elements;
            this.speak = speak;
        }

        /// <summary>Per frame with the map overlay layer (zero outside the game).</summary>
        public void Tick(IntPtr mapOverlay)
        {
            if (mapOverlay != this.overlay)
            {
                this.overlay = mapOverlay;
                this.hint = IntPtr.Zero;
                this.shown = null;
            }

            var now = Environment.TickCount64;
            if (this.overlay == IntPtr.Zero || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (this.hint == IntPtr.Zero)
            {
                this.hint = this.FindHint();
            }

            if (this.hint == IntPtr.Zero || !this.elements.TryReadElement(this.hint, out var info))
            {
                this.hint = IntPtr.Zero;
                return;
            }

            var visible = this.elements.IsVisible(info);
            if (this.shown is { } was && was != visible)
            {
                var text = visible && this.elements.TryReadText(this.hint, out _, out var tagged) ? ControllerGlyphs.Render(tagged) : string.Empty;
                this.speak(visible ? Announcements.MapShown(Announcements.Sentences(this.InfoLines(info).Append(text).Where(line => line.Length > 0))) : Announcements.MapHidden);
            }

            this.shown = visible;
        }

        /// <summary>The shown lines beside the hint (area, league, realm), in their order.</summary>
        private IEnumerable<string> InfoLines(in UiElementReader.ElementInfo hint)
        {
            var lines = new List<string>();
            if (this.elements.TryReadElement(hint.Raw.ParentPtr, out var info))
            {
                foreach (var address in this.elements.ReadChildren(info))
                {
                    if (address != hint.Address && this.elements.TryReadElement(address, out var line) && line.IsVisibleFlag &&
                        this.elements.TryReadSpokenText(address, out var text) && text.Length > 0)
                    {
                        lines.Add(text);
                    }
                }
            }

            return lines;
        }

        private IntPtr FindHint()
        {
            if (!this.elements.TryReadElement(this.overlay, out var root))
            {
                return IntPtr.Zero;
            }

            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                foreach (var address in this.elements.ReadChildren(node))
                {
                    if (!this.elements.TryReadElement(address, out var child))
                    {
                        continue;
                    }

                    if (child.Name == HintName)
                    {
                        return address;
                    }

                    if (depth + 1 < HintDepth)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return IntPtr.Zero;
        }
    }
}
