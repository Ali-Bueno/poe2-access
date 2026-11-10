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
    using GameReader.Plugin;
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
        /// <remarks>
        ///     World and map overlays draw labels over the game world (ground items, monsters); they are not UI to read.
        ///     The HUD's "Game Paused" banner (paused_frame) shows for a moment as a panel closes; the menu that pauses
        ///     the game is announced itself.
        /// </remarks>
        private static readonly string[] IgnoredControlPrefixes = { "couch_coop", "world_", "map_overlay", "paused_frame" };

        /// <summary>A text this tall (layout units) may be a long one scrolled in a frame; only those are checked for it.</summary>
        private const float LongTextMinHeight = 2000;

        /// <summary>More blocks than a walk holds at once (its stack: the siblings waiting at each depth).</summary>
        private const int MaxBlocksKept = 1024;

        private readonly UiElementReader elements;
        private readonly PluginLog log;

        /// <summary>
        ///     The element blocks of the walk, read some 6 000 times a second: an element's block goes back once the walk is done
        ///     with the element, and nothing the snapshot keeps (addresses, names, texts) points into one. The watcher's task only.
        /// </summary>
        private readonly BlockPool blocks = new(MaxBlocksKept);

        /// <summary>The children of the element being walked: read, pushed and done with before the next element's.</summary>
        private IntPtr[] walkChildren = new IntPtr[64];

        private readonly List<WalkNode> walkNodes = new();
        private readonly List<(int Node, ScreenText Text)> walkTexts = new();
        private readonly List<(int Node, string Button)> walkGlyphs = new();
        private readonly List<(int Node, string Unit)> walkUnits = new();
        private readonly List<(int Node, int Set)> walkSetIcons = new();
        private readonly Stack<(UiElementReader.ElementInfo Info, int Parent, int Index, int Control)> walkStack = new();
        private readonly ScreenDiffer differ = new();
        private readonly ConcurrentQueue<(ScreenChange Change, int Generation)> changes = new();
        private readonly CancellationTokenSource stop = new();
        /// <summary>
        ///     A texture's key is read again this often: the game gives a freed texture's address to another, and one read
        ///     before it was loaded has no key yet.
        /// </summary>
        private const int TextureKeyKeepMs = 2000;

        private readonly Dictionary<IntPtr, (string? Key, long At)> textureKeys = new();
        private readonly Stopwatch sinceSlowReport = Stopwatch.StartNew();
        private readonly object snapshotCostsLock = new();
        private int snapshots;
        private double snapshotsMs;
        private double slowestSnapshotMs;
        private volatile bool resetRequested;

        /// <summary>Counts resets: a capture or a change from before the last one belongs to the old screen and is dropped.</summary>
        private int generation;
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

        /// <summary>A text too long to say at once on screen now (the terms of use), read by <see cref="LongTextReader"/>.</summary>
        public IntPtr LongText => this.longText;

        private volatile IntPtr longText;

        /// <summary>The long text found by the capture under way.</summary>
        private IntPtr capturedLongText;

        /// <summary>Root of the current screen; zero pauses the watcher.</summary>
        public void SetRoot(IntPtr address) => this.root = address;

        /// <summary>New game state: the next screen becomes a silent baseline; in game its layers become base layers.</summary>
        public void Reset(bool inGame)
        {
            this.differ.FreezeBaseLayers = inGame;
            Interlocked.Increment(ref this.generation);
            this.resetRequested = true;
            this.Current = ScreenSnapshot.Empty;
        }

        /// <summary>The layer read even when it is already on screen as a state begins (the in-game tutorial box).</summary>
        public void SetArrivalLayer(IntPtr layer) => this.differ.ArrivalLayer = layer;

        /// <summary>The layer holding this element is never read (the chat panel: its own feature, never automatic).</summary>
        public void SetIgnoredElement(IntPtr element) => this.ignoredElement = element;

        public bool TryDequeue(out ScreenChange change)
        {
            var current = Volatile.Read(ref this.generation);
            while (this.changes.TryDequeue(out var queued))
            {
                if (queued.Generation == current)
                {
                    change = queued.Change;
                    return true;
                }
            }

            change = null!;
            return false;
        }

        public void Dispose() => this.stop.Cancel();

        /// <summary>The snapshots taken since the last call (the dev command `costs`): how many, their average and slowest time.</summary>
        public string TakeSnapshotCosts()
        {
            lock (this.snapshotCostsLock)
            {
                var line = this.snapshots == 0
                    ? "Screen snapshots: none"
                    : $"Screen snapshots: {this.snapshots}, {this.snapshotsMs / this.snapshots:0.0} ms average, slowest {this.slowestSnapshotMs:0.0} ms";
                (this.snapshots, this.snapshotsMs, this.slowestSnapshotMs) = (0, 0, 0);
                return line;
            }
        }

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

                    var started = Volatile.Read(ref this.generation);
                    var began = Stopwatch.GetTimestamp();
                    var captured = this.Capture(this.root);
                    var took = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
                    this.CountSnapshot(took);
                    this.ReportSlowSnapshot((long)took, captured);
                    if (started != Volatile.Read(ref this.generation))
                    {
                        continue; // the screen changed state while it was captured: the next pass starts the new one
                    }

                    var applied = this.differ.Apply(captured);
                    this.Current = this.differ.Last;
                    foreach (var change in applied)
                    {
                        this.changes.Enqueue((change, started));
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

        private void CountSnapshot(double ms)
        {
            lock (this.snapshotCostsLock)
            {
                this.snapshots++;
                this.snapshotsMs += ms;
                this.slowestSnapshotMs = Math.Max(this.slowestSnapshotMs, ms);
            }
        }

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
            this.capturedLongText = IntPtr.Zero;
            var ignoredLayer = this.LayerOf(this.ignoredElement, rootAddress);
            var children = this.elements.ReadChildren(rootInfo);
            var budget = MaxNodes;
            var rects = new PluginUiElementReflection.RectCache(); // one snapshot: each ancestor placed once
            var screen = PluginUiElementReflection.TryGetAbsoluteRect(rootAddress, rects, out var screenPosition, out var screenSize)
                ? new RectangleF(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y)
                : RectangleF.Empty;
            for (var index = 0; index < children.Length && budget > 0; index++)
            {
                if (children[index] == ignoredLayer || !this.elements.TryReadElement(children[index], this.blocks, out var layerInfo))
                {
                    continue;
                }

                if (layerInfo.IsVisibleFlag && !IsIgnored(layerInfo.Name))
                {
                    layers.Add(this.CaptureLayer(layerInfo, index, screen, rects, ref budget)); // gives the layer's block back
                }
                else
                {
                    this.blocks.Return(layerInfo.Block);
                }
            }

            this.longText = this.capturedLongText;
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

        private ScreenLayer CaptureLayer(UiElementReader.ElementInfo layerInfo, int index, RectangleF screen, PluginUiElementReflection.RectCache rects, ref int budget)
        {
            // The walk's working lists serve every layer of every snapshot: the layer returned holds a copy of its texts.
            var (nodes, texts, glyphs, units, setIcons, stack) = (this.walkNodes, this.walkTexts, this.walkGlyphs, this.walkUnits, this.walkSetIcons, this.walkStack);
            nodes.Clear();
            texts.Clear();
            glyphs.Clear();
            units.Clear();
            setIcons.Clear();
            stack.Clear();
            var hasControls = false;
            bool isHintBar;
            stack.Push((layerInfo, -1, 0, -1));
            byte[]? walked = null;
            while (stack.Count > 0 && budget-- > 0)
            {
                // The element before this one is done with: its text was decoded into strings, its block reads no more.
                this.blocks.Return(walked);
                var (info, parent, childIndex, control) = stack.Pop();
                walked = info.Block;
                var above = parent >= 0 ? nodes[parent] : new WalkNode(-1, 0, -1, 0, 0, -1, true);
                var self = nodes.Count;
                var enabled = above.Enabled;
                // A control slid out of view does not make its layer one to use: the login screen's "Deal of the Day"
                // panel waits off screen with its "buy now!" button and hid the screen in front (fresh install, 2026-10-24).
                if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && !IsOffScreen(info.Address, screen, rects))
                {
                    hasControls = true;
                    control = self;
                    enabled = !FlagBits.EnabledTrusted || info.HasFlagBit(GameLayout.Element.EnabledFlagBit);
                }

                var position = info.Raw.RelativePosition;
                nodes.Add(new WalkNode(parent, childIndex, above.Depth + 1, above.X + position.X, above.Y + position.Y, control, enabled));
                var children = this.elements.ReadChildren(info, ref this.walkChildren);
                var hasText = this.elements.TryReadText(info, out var text, out var tagged);
                if (!hasText && this.capturedLongText == IntPtr.Zero && info.Raw.UnscaledSize.Y >= LongTextMinHeight && this.elements.IsLongText(info.Address))
                {
                    this.capturedLongText = info.Address; // read a paragraph at a time as it scrolls, not with the screen
                }
                if (hasText && ControllerGlyphs.ForOtherPlayer(tagged))
                {
                    continue; // a second controller's prompt and its children ("P2")
                }

                var markupButton = hasText ? ControllerGlyphs.FromMarkup(tagged) : string.Empty;
                // A chord's icons joined by a plus ("<<rt>>+<<lstick>>", stripped " + ") are an icon too: the label beside
                // them is the legend (it was read as content and repeated the inventory's focus, 2026-10-24).
                var glyphsOnly = markupButton.Length > 0 && text.Trim().Trim('+').Trim().Length == 0;
                if (hasText && text.Length > 0 && IsOffScreen(info.Address, screen, rects))
                {
                    continue; // a panel slid out of view keeps its visible flag (the world map's quest list)
                }

                if (hasText && markupButton.Length > 0 && tagged.TrimStart().StartsWith("<<", StringComparison.Ordinal) &&
                    ControllerGlyphs.OnlyLeadingButtons(tagged) && text.Trim().TrimStart('+').Trim() is { Length: > 0 } legendLabel)
                {
                    // A text that starts with its buttons is a legend ("<<rt>> + <<lstick>> Move" beside the character
                    // preview): label and buttons, read with the hints, not as content of the panel.
                    texts.Add((self, new ScreenText(info.Address, Announcements.Flatten(legendLabel), info.Name, false, true, markupButton)));
                }
                else if (hasText && text.Length > 0 && !glyphsOnly)
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
                        glyphs.Add((self, this.HasChildNamed(this.walkChildren, children, GameLayout.InGameUi.HoldProgressName) ? Announcements.HoldButton(glyph) : glyph));
                    }
                    else if (WeaponSetRows.FromTextureKey(key) is { } set)
                    {
                        setIcons.Add((self, set));
                    }
                    else if (CurrencyIcons.FromTextureKey(key) is { } unit)
                    {
                        units.Add((self, unit));
                    }
                    else if (QuestRewards.FromTextureKey(key) is { } reward && !IsOffScreen(info.Address, screen, rects))
                    {
                        // An icon that says what a quest gives ("Recompensa:" then a support gem and gold).
                        texts.Add((self, new ScreenText(info.Address, reward, info.Name, false, true)));
                    }
                }

                if (nodes[self].Depth >= MaxDepth)
                {
                    continue;
                }

                for (var i = children - 1; i >= 0; i--)
                {
                    if (!this.elements.TryReadElement(this.walkChildren[i], this.blocks, out var child))
                    {
                        continue;
                    }

                    if (child.IsVisibleFlag && !IsIgnored(child.Name))
                    {
                        stack.Push((child, self, i, control));
                    }
                    else
                    {
                        this.blocks.Return(child.Block);
                    }
                }
            }

            // What the budget left on the stack keeps its blocks: lost to the collector, never handed out twice.
            this.blocks.Return(walked);
            PairGlyphs(nodes, texts, glyphs);
            PairUnits(nodes, texts, units);
            PairWeaponSets(nodes, texts, setIcons);
            MergeRows(nodes, texts);
            // A legend layer holds nothing but legends; a menu layer may carry its own hint bar (game menu).
            isHintBar = texts.Count > 0 && texts.TrueForAll(text => IsLegend(text.Text));
            return new ScreenLayer(layerInfo.Address, index, texts.Select(text => text.Text).ToList(), hasControls, isHintBar, Name: layerInfo.Name);
        }

        private static bool IsOffScreen(IntPtr element, RectangleF screen, PluginUiElementReflection.RectCache rects) =>
            !screen.IsEmpty && PluginUiElementReflection.TryGetAbsoluteRect(element, rects, out var position, out var size) &&
            !screen.IntersectsWith(new RectangleF(position.X, position.Y, size.X, size.Y));

        /// <summary>A hint-bar entry label or a text paired with its button glyph.</summary>
        public static bool IsLegend(ScreenText text) => text.Name == HintEntryName || text.Button.Length > 0;

        /// <summary>Art key of an image element; texture keys are cached (shared resources).</summary>
        private bool HasChildNamed(IntPtr[] children, int count, string name)
        {
            for (var i = 0; i < count; i++)
            {
                if (this.elements.TryReadElement(children[i], this.blocks, out var info))
                {
                    this.blocks.Return(info.Block); // only its name is looked at
                    if (info.Name == name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string? TextureKeyOf(in UiElementReader.ElementInfo info)
        {
            if (info.Texture == IntPtr.Zero)
            {
                return null;
            }

            var now = Environment.TickCount64;
            if (!this.textureKeys.TryGetValue(info.Texture, out var known) || now - known.At >= TextureKeyKeepMs)
            {
                known = (this.elements.ReadTextureKey(info.Texture), now);
                this.textureKeys[info.Texture] = known;
            }

            return known.Key;
        }

        /// <summary>
        ///     Each glyph labels the text closest in the tree (deepest shared ancestor, at most GlyphPairHops up),
        ///     then closest on screen: a legend's icon and label are siblings even when another text is nearer.
        /// </summary>
        private static void PairGlyphs(List<WalkNode> nodes, List<(int Node, ScreenText Text)> texts, List<(int Node, string Button)> glyphs)
        {
            // A legend with two icons and a "+" between them ("LT + A: Start in town") is one chord: the "+" is not a
            // label of its own, and the two buttons are pressed together, not either one.
            var plus = texts.Where(item => item.Text.Text == PlusLabel).ToList();
            texts.RemoveAll(item => item.Text.Text == PlusLabel);
            foreach (var (glyphNode, button) in glyphs)
            {
                var best = NearestText(nodes, texts, glyphNode);
                if (best >= 0)
                {
                    var text = texts[best].Text;
                    var chord = plus.Count > 0 && NearestText(nodes, plus, texts[best].Node) >= 0;
                    var joined = text.Button.Length == 0 ? button : text.Button + (chord ? Announcements.ButtonCombination : Announcements.ButtonSeparator) + button;
                    texts[best] = (texts[best].Node, text with { Button = joined });
                }
            }
        }

        /// <summary>The visible "+" between a chord's two icons in a legend (hint entry plus_label).</summary>
        private const string PlusLabel = "+";

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

        /// <summary>A value drawn for one weapon set names the set (the spirit panel: "set de armas II: 30 de espíritu").</summary>
        private static void PairWeaponSets(List<WalkNode> nodes, List<(int Node, ScreenText Text)> texts, List<(int Node, int Set)> icons)
        {
            if (icons.Count == 0)
            {
                return;
            }

            var sets = WeaponSetRows.Pair(nodes.Select(node => node.Parent).ToList(), nodes.Select(node => node.Index).ToList(), icons, texts.Select(text => text.Node));
            for (var i = 0; i < texts.Count; i++)
            {
                if (sets.TryGetValue(texts[i].Node, out var set))
                {
                    texts[i] = (texts[i].Node, texts[i].Text with { Text = Announcements.WeaponSetValue(set, texts[i].Text.Text) });
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
        private readonly record struct WalkNode(int Parent, int Index, int Depth, float X, float Y, int Control, bool Enabled);
    }
}
