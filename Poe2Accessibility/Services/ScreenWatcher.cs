namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using GameHelper.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Snapshots the visible UI tree on a background task and queues the changes for the render thread.
    ///     Only visible subtrees are walked, so a snapshot costs a few hundred element reads.
    /// </summary>
    public sealed class ScreenWatcher : IDisposable
    {
        /// <summary>Two snapshots confirm a change, so speech follows a screen change by about twice this.</summary>
        private const int SnapshotIntervalMs = 150;

        private const int MaxNodes = 6000;

        private const int SlowSnapshotMs = 60;
        private const int SlowReportIntervalMs = 60000;
        private const int MaxDepth = 48;

        /// <summary>A text this far below a control belongs to it (button caption, check box label).</summary>
        private const int ControlCaptionHops = 3;

        /// <summary>The controller hint bar labels each entry with an element of this name (dumps 2026-10-23).</summary>
        private const string HintEntryName = "entry_label";

        /// <summary>A glyph labels a text sharing an ancestor at most this far up (hint entry: icon and label meet two levels up).</summary>
        private const int GlyphPairHops = 3;

        /// <summary>A stat row: label and value cells sit two levels under the row (character panel, 2026-10-23).</summary>
        private const int RowHops = 2;

        /// <summary>Layout units two texts of one table row may differ vertically.</summary>
        private const float SameLineTolerance = 8;

        /// <summary>Control-id prefixes never read: the couch co-op "P2, Connect a Controller" prompt shows whenever a second pad is plugged in.</summary>
        /// <remarks>World and map overlays draw labels over the game world (ground items, monsters); they are not UI to read.</remarks>
        private static readonly string[] IgnoredControlPrefixes = { "couch_coop", "world_", "map_overlay" };

        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly ScreenDiffer differ = new();
        private readonly ConcurrentQueue<ScreenChange> changes = new();
        private readonly CancellationTokenSource stop = new();
        private readonly Dictionary<IntPtr, string?> textureKeys = new();
        private readonly Stopwatch sinceSlowReport = Stopwatch.StartNew();
        private volatile bool resetRequested;
        private IntPtr root;
        private IntPtr ignoredElement;

        public ScreenWatcher(UiElementReader elements, PluginLog log)
        {
            this.elements = elements;
            this.log = log;
            Task.Run(this.RunAsync);
        }

        /// <summary>Latest snapshot (render thread reads it to know which layer owns input).</summary>
        public ScreenSnapshot Current { get; private set; } = ScreenSnapshot.Empty;

        /// <summary>Root of the current screen; zero pauses the watcher.</summary>
        public void SetRoot(IntPtr address) => this.root = address;

        /// <summary>New game state: the next screen becomes a silent baseline; in game its layers become base layers.</summary>
        public void Reset(bool inGame)
        {
            this.differ.FreezeBaseLayers = inGame;
            this.resetRequested = true;
        }

        /// <summary>The layer holding this element is never read (the chat panel: its own feature, never automatic).</summary>
        public void SetIgnoredElement(IntPtr element) => this.ignoredElement = element;

        public bool TryDequeue(out ScreenChange change) => this.changes.TryDequeue(out change!);

        public void Dispose() => this.stop.Cancel();

        private async Task RunAsync()
        {
            while (!this.stop.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(SnapshotIntervalMs, this.stop.Token).ConfigureAwait(false);
                    if (this.resetRequested)
                    {
                        this.resetRequested = false;
                        this.differ.Reset();
                        this.textureKeys.Clear();
                    }

                    var timer = Stopwatch.StartNew();
                    var captured = this.Capture(this.root);
                    this.ReportSlowSnapshot(timer.ElapsedMilliseconds, captured);
                    var applied = this.differ.Apply(captured);
                    this.Current = this.differ.Last;
                    foreach (var change in applied)
                    {
                        this.changes.Enqueue(change);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    this.log.Error("Screen snapshot failed", ex);
                }
            }
        }

        private static bool IsIgnored(string controlName) =>
            controlName.Length > 0 && Array.Exists(IgnoredControlPrefixes, prefix => controlName.StartsWith(prefix, StringComparison.Ordinal));

        /// <summary>A snapshot slower than SlowSnapshotMs is logged at most once per SlowReportIntervalMs (cost of walking big screens).</summary>
        private void ReportSlowSnapshot(long elapsedMs, ScreenSnapshot snapshot)
        {
            if (elapsedMs > SlowSnapshotMs && this.sinceSlowReport.ElapsedMilliseconds > SlowReportIntervalMs)
            {
                this.sinceSlowReport.Restart();
                this.log.Info($"Screen snapshot took {elapsedMs} ms ({snapshot.Layers.Count} layers, {snapshot.Layers.Sum(layer => layer.Texts.Count)} texts)");
            }
        }

        private ScreenSnapshot Capture(IntPtr rootAddress)
        {
            if (rootAddress == IntPtr.Zero || !this.elements.TryReadElement(rootAddress, out var rootInfo))
            {
                return ScreenSnapshot.Empty;
            }

            var layers = new List<ScreenLayer>();
            var ignoredLayer = this.LayerOf(this.ignoredElement, rootAddress);
            var children = this.elements.ReadChildren(rootInfo);
            var budget = MaxNodes;
            var screen = PluginUiElementReflection.TryGetAbsoluteRect(rootAddress, out var screenPosition, out var screenSize)
                ? new RectangleF(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y)
                : RectangleF.Empty;
            for (var index = 0; index < children.Length && budget > 0; index++)
            {
                if (children[index] != ignoredLayer && this.elements.TryReadElement(children[index], out var layerInfo) &&
                    layerInfo.IsVisibleFlag && !IsIgnored(layerInfo.Name))
                {
                    layers.Add(this.CaptureLayer(layerInfo, index, screen, ref budget));
                }
            }

            return new ScreenSnapshot(rootAddress, layers);
        }

        /// <summary>The child of <paramref name="rootAddress"/> that contains <paramref name="element"/>; zero when unrelated.</summary>
        private IntPtr LayerOf(IntPtr element, IntPtr rootAddress)
        {
            if (element == IntPtr.Zero || !this.elements.TryReadElement(element, out var info))
            {
                return IntPtr.Zero;
            }

            foreach (var ancestor in this.elements.ReadAncestors(info, MaxDepth))
            {
                if (ancestor.Raw.ParentPtr == rootAddress)
                {
                    return ancestor.Address;
                }
            }

            return info.Raw.ParentPtr == rootAddress ? element : IntPtr.Zero;
        }

        private ScreenLayer CaptureLayer(UiElementReader.ElementInfo layerInfo, int index, RectangleF screen, ref int budget)
        {
            var nodes = new List<WalkNode>();
            var texts = new List<(int Node, ScreenText Text)>();
            var glyphs = new List<(int Node, string Button)>();
            var units = new List<(int Node, string Unit)>();
            var hasControls = false;
            bool isHintBar;
            var stack = new Stack<(UiElementReader.ElementInfo Info, int Parent, int Control)>();
            stack.Push((layerInfo, -1, -1));
            while (stack.Count > 0 && budget-- > 0)
            {
                var (info, parent, control) = stack.Pop();
                var above = parent >= 0 ? nodes[parent] : new WalkNode(-1, -1, 0, 0, -1, true);
                var self = nodes.Count;
                var enabled = above.Enabled;
                if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                {
                    hasControls = true;
                    control = self;
                    enabled = info.HasFlagBit(GameLayout.Element.EnabledFlagBit);
                }

                var position = info.Raw.RelativePosition;
                nodes.Add(new WalkNode(parent, above.Depth + 1, above.X + position.X, above.Y + position.Y, control, enabled));
                var children = this.elements.ReadChildren(info);
                var hasText = this.elements.TryReadText(info.Address, out var text, out var tagged);
                if (hasText && ControllerGlyphs.ForOtherPlayer(tagged))
                {
                    continue; // a second controller's prompt and its children ("P2")
                }

                var markupButton = hasText ? ControllerGlyphs.FromMarkup(tagged) : string.Empty;
                if (hasText && text.Length > 0 && IsOffScreen(info.Address, screen))
                {
                    continue; // a panel slid out of view keeps its visible flag (the world map's quest list)
                }

                if (hasText && text.Length > 0)
                {
                    // Glyphs inside a sentence are read in place; a lone trailing glyph ("continue {A}") the same way.
                    var spoken = Announcements.Flatten(markupButton.Length > 0 ? ControllerGlyphs.Render(tagged) : text);
                    var isCaption = control >= 0 && nodes[self].Depth - nodes[control].Depth <= ControlCaptionHops;
                    texts.Add((self, new ScreenText(info.Address, spoken, info.Name, isCaption, !isCaption || enabled)));
                }
                else if (markupButton.Length > 0)
                {
                    // A text holding only glyph markup is an icon (panel legends): it labels its neighbour.
                    glyphs.Add((self, markupButton));
                }
                else if (this.TextureKeyOf(info) is { } key)
                {
                    if (ControllerGlyphs.FromTextureKey(key) is { } glyph)
                    {
                        glyphs.Add((self, glyph));
                    }
                    else if (CurrencyIcons.FromTextureKey(key) is { } unit)
                    {
                        units.Add((self, unit));
                    }
                }

                if (nodes[self].Depth >= MaxDepth)
                {
                    continue;
                }

                for (var i = children.Length - 1; i >= 0; i--)
                {
                    if (this.elements.TryReadElement(children[i], out var child) && child.IsVisibleFlag && !IsIgnored(child.Name))
                    {
                        stack.Push((child, self, control));
                    }
                }
            }

            PairGlyphs(nodes, texts, glyphs);
            PairUnits(nodes, texts, units);
            MergeRows(nodes, texts);
            // A legend layer holds nothing but legends; a menu layer may carry its own hint bar (game menu).
            isHintBar = texts.Count > 0 && texts.TrueForAll(text => IsLegend(text.Text));
            return new ScreenLayer(layerInfo.Address, index, texts.Select(text => text.Text).ToList(), hasControls, isHintBar);
        }

        private static bool IsOffScreen(IntPtr element, RectangleF screen) =>
            !screen.IsEmpty && PluginUiElementReflection.TryGetAbsoluteRect(element, out var position, out var size) &&
            !screen.IntersectsWith(new RectangleF(position.X, position.Y, size.X, size.Y));

        /// <summary>A hint-bar entry label or a text paired with its button glyph.</summary>
        public static bool IsLegend(ScreenText text) => text.Name == HintEntryName || text.Button.Length > 0;

        /// <summary>Art key of an image element; texture keys are cached (shared resources).</summary>
        private string? TextureKeyOf(in UiElementReader.ElementInfo info)
        {
            if (info.Texture == IntPtr.Zero)
            {
                return null;
            }

            if (!this.textureKeys.TryGetValue(info.Texture, out var key))
            {
                key = this.elements.ReadTextureKey(info.Texture);
                this.textureKeys[info.Texture] = key;
            }

            return key;
        }

        /// <summary>
        ///     Each glyph labels the text closest in the tree (deepest shared ancestor, at most GlyphPairHops up),
        ///     then closest on screen: a legend's icon and label are siblings even when another text is nearer.
        /// </summary>
        private static void PairGlyphs(List<WalkNode> nodes, List<(int Node, ScreenText Text)> texts, List<(int Node, string Button)> glyphs)
        {
            foreach (var (glyphNode, button) in glyphs)
            {
                var best = NearestText(nodes, texts, glyphNode);
                if (best >= 0)
                {
                    var text = texts[best].Text;
                    var joined = text.Button.Length == 0 ? button : text.Button + Announcements.ButtonSeparator + button;
                    texts[best] = (texts[best].Node, text with { Button = joined });
                }
            }
        }

        /// <summary>A currency icon names the amount beside it: the inventory's gold counter reads "0 gold".</summary>
        private static void PairUnits(List<WalkNode> nodes, List<(int Node, ScreenText Text)> texts, List<(int Node, string Unit)> units)
        {
            foreach (var (iconNode, unit) in units)
            {
                var best = NearestText(nodes, texts, iconNode);
                if (best >= 0 && IsAmount(texts[best].Text.Text))
                {
                    var text = texts[best].Text;
                    texts[best] = (texts[best].Node, text with { Text = Announcements.Amount(text.Text, unit, false) });
                }
            }
        }

        private static bool IsAmount(string text) => text.Length > 0 && text.All(c => char.IsDigit(c) || c is ',' or '.' or ' ');

        /// <summary>The text closest to an image in the tree (deepest shared ancestor, at most GlyphPairHops up), then on screen.</summary>
        private static int NearestText(List<WalkNode> nodes, List<(int Node, ScreenText Text)> texts, int imageNode)
        {
            var best = -1;
            var bestHops = int.MaxValue;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < texts.Count; i++)
            {
                var textNode = texts[i].Node;
                var hops = nodes[imageNode].Depth - CommonAncestorDepth(nodes, imageNode, textNode);
                if (hops > GlyphPairHops)
                {
                    continue;
                }

                var dx = nodes[imageNode].X - nodes[textNode].X;
                var dy = nodes[imageNode].Y - nodes[textNode].Y;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (hops < bestHops || (hops == bestHops && distance < bestDistance))
                {
                    bestHops = hops;
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        ///     Table rows ("Maximum Life" … "42"): consecutive texts on the same line whose row container is at most
        ///     RowHops above both become one text, read "label, value".
        /// </summary>
        private static void MergeRows(List<WalkNode> nodes, List<(int Node, ScreenText Text)> texts)
        {
            for (var i = texts.Count - 1; i > 0; i--)
            {
                var (labelNode, label) = texts[i - 1];
                var (valueNode, value) = texts[i];
                var shared = CommonAncestorDepth(nodes, labelNode, valueNode);
                if (shared < 0 || nodes[labelNode].Depth - shared > RowHops || nodes[valueNode].Depth - shared > RowHops ||
                    Math.Abs(nodes[labelNode].Y - nodes[valueNode].Y) > SameLineTolerance || label.IsControl || value.IsControl ||
                    label.Button.Length > 0 || value.Button.Length > 0)
                {
                    continue;
                }

                texts[i - 1] = (labelNode, label with { Text = Announcements.LabelValue(label.Text, value.Text) });
                texts.RemoveAt(i);
            }
        }

        private static int CommonAncestorDepth(List<WalkNode> nodes, int a, int b)
        {
            while (a != b)
            {
                if (nodes[a].Depth >= nodes[b].Depth)
                {
                    a = nodes[a].Parent;
                }
                else
                {
                    b = nodes[b].Parent;
                }

                if (a < 0 || b < 0)
                {
                    return -1;
                }
            }

            return nodes[a].Depth;
        }

        /// <summary>A visited element: parent index, depth, position summed from the layer, owning control and its enabled state.</summary>
        private readonly record struct WalkNode(int Parent, int Depth, float X, float Y, int Control, bool Enabled);
    }
}
