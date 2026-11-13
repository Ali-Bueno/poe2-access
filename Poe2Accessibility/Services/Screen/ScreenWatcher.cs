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
    public sealed partial class ScreenWatcher : IDisposable
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

        /// <summary>The parts of tab controls met in the layer being walked, by their control, and one entry per end button already given its legend.</summary>
        private readonly List<int> walkTabBars = new();
        private readonly List<int> walkTabBarEnds = new();
        private readonly Stack<(UiElementReader.ElementInfo Info, int Parent, int Index, int Control)> walkStack = new();

        /// <summary>Where each ancestor of a snapshot is, emptied for the next: a table per snapshot was megabytes with the passive tree open.</summary>
        private readonly PluginUiElementReflection.RectCache rects = new();
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

        /// <summary>When a snapshot that ran out of its element budget may next be logged (Environment.TickCount64): the first one at once.</summary>
        private long cutShortReportAt;
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
                    // Taken before the reset flag is read: a Reset landing between the two would otherwise stamp this capture with
                    // the new generation while the differ still holds the old screen, and the stale pass would go through.
                    var started = Volatile.Read(ref this.generation);
                    if (this.resetRequested)
                    {
                        this.resetRequested = false;
                        this.differ.Reset();
                        this.textureKeys.Clear();
                    }

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
                    if (started != Volatile.Read(ref this.generation))
                    {
                        continue; // a reset landed while the pass was applied: Current stays the reset's empty snapshot and nothing is queued
                    }

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
            var rects = this.rects; // one snapshot: each ancestor placed once
            rects.Clear();
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

            if (budget <= 0 && Environment.TickCount64 >= this.cutShortReportAt)
            {
                // Layers after the last one read are missing from this snapshot: a popup or the hint bar there goes unread.
                this.cutShortReportAt = Environment.TickCount64 + SlowReportIntervalMs;
                this.log.Warn($"Screen snapshot cut short at {MaxNodes} elements: {layers.Count} layers read of {children.Length} under the root, the last one whole or in part");
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
    }
}
