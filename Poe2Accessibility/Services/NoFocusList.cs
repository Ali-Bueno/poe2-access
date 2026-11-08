namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     A list a popup navigates without giving the focus (<see cref="GameLayout.HighlightList"/>): once the game focuses
    ///     nothing, the lists under the layer of the last focus are watched, and the row of the one whose index moves with
    ///     the stick stands for the focus (its control is described as any focused control).
    /// </summary>
    public sealed class NoFocusList
    {
        /// <summary>Lists are looked for again after this while none has moved (a popup still building), then less and less often.</summary>
        private const int FirstRescanMs = 500;

        private const int MaxRescanMs = 4000;

        /// <summary>The index fields are read this often; a stick step takes longer.</summary>
        private const int PollMs = 60;

        /// <summary>Visible elements a search reads at most (the options layer shows about 1000).</summary>
        private const int MaxScan = 4000;

        /// <summary>Fewest rows that make a list.</summary>
        private const int MinRows = 3;

        /// <summary>Row → cell → wrapper → control: how deep a row holds its control.</summary>
        private const int ControlDepth = 3;

        /// <summary>Rows → list frame → popup: the index two levels up; one more leaves room for a wrapper.</summary>
        private const int OwnerHops = 3;

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly MovingIndexWatch watch = new();
        private List<(IntPtr Owner, IntPtr Rows)> lists = new();
        private IntPtr root;
        private IntPtr row;
        private long polledAt;
        private long scanAt;
        private int rescanMs = FirstRescanMs;

        public NoFocusList(HostMemoryReader reader, UiElementReader elements)
        {
            this.reader = reader;
            this.elements = elements;
        }

        /// <summary>The game focused something: the next loss of focus starts afresh.</summary>
        public void Reset()
        {
            this.root = IntPtr.Zero;
            this.row = IntPtr.Zero;
            this.lists.Clear();
            this.watch.Reset();
            this.scanAt = 0;
            this.rescanMs = FirstRescanMs;
        }

        /// <summary>Per tick while the game focuses nothing: the control of the highlighted row under <paramref name="layer"/>, or zero.</summary>
        public IntPtr Row(IntPtr layer)
        {
            if (layer != this.root)
            {
                this.Reset();
                this.root = layer;
            }

            var now = Environment.TickCount64;
            if (layer == IntPtr.Zero || now - this.polledAt < PollMs)
            {
                return this.row;
            }

            this.polledAt = now;
            this.row = IntPtr.Zero;
            if (!this.elements.TryReadElement(layer, out var layerInfo) || !this.elements.IsVisible(layerInfo))
            {
                return this.row;
            }

            var readings = new List<MovingIndexWatch.Reading>(this.lists.Count);
            var offset = GameLayout.HighlightList.RowIndexOffset + (this.elements.Controls?.TextShift ?? 0);
            foreach (var (owner, rows) in this.lists)
            {
                var count = this.elements.TryReadElement(rows, out var rowsInfo) ? this.elements.ReadChildren(rowsInfo).Length : 0;
                readings.Add(new MovingIndexWatch.Reading(owner, rows, this.reader.TryRead<int>(owner + offset, out var index) ? index : -1, count));
            }

            if (this.watch.Observe(readings) is { } moving)
            {
                this.row = this.ControlOfRow(moving.Rows, moving.Row);
            }
            else if (now >= this.scanAt)
            {
                this.lists = this.FindLists(layer);
                this.scanAt = now + this.rescanMs;
                this.rescanMs = Math.Min(this.rescanMs * 2, MaxRescanMs);
            }

            return this.row;
        }

        /// <summary>Visible lists under <paramref name="layer"/>: a rows element and each ancestor that may hold its index.</summary>
        private List<(IntPtr Owner, IntPtr Rows)> FindLists(IntPtr layer)
        {
            var found = new List<(IntPtr, IntPtr)>();
            var queue = new Queue<IntPtr>();
            queue.Enqueue(layer);
            var budget = MaxScan;
            while (queue.Count > 0 && budget-- > 0)
            {
                if (!this.elements.TryReadElement(queue.Dequeue(), out var info) || !info.IsVisibleFlag)
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                if (children.Length >= MinRows && this.AllRowsHoldControls(children))
                {
                    var owner = info;
                    for (var hop = 0; hop < OwnerHops && owner.Raw.ParentPtr != IntPtr.Zero && this.elements.TryReadElement(owner.Raw.ParentPtr, out owner); hop++)
                    {
                        found.Add((owner.Address, info.Address));
                    }

                    continue; // a row's insides are no list of their own
                }

                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }

            return found;
        }

        private bool AllRowsHoldControls(IntPtr[] children)
        {
            foreach (var child in children)
            {
                if (!this.elements.TryReadElement(child, out var info) || info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) ||
                    (info.IsVisibleFlag && this.ControlIn(info) == IntPtr.Zero))
                {
                    return false;
                }
            }

            return true;
        }

        private IntPtr ControlOfRow(IntPtr rows, int index)
        {
            if (!this.elements.TryReadElement(rows, out var rowsInfo))
            {
                return IntPtr.Zero;
            }

            var children = this.elements.ReadChildren(rowsInfo);
            return index < children.Length && this.elements.TryReadElement(children[index], out var rowInfo) && this.elements.IsVisible(rowInfo)
                ? this.ControlIn(rowInfo)
                : IntPtr.Zero;
        }

        /// <summary>The row's first interactive descendant, breadth first.</summary>
        private IntPtr ControlIn(in UiElementReader.ElementInfo row)
        {
            var queue = new Queue<(UiElementReader.ElementInfo Info, int Depth)>();
            queue.Enqueue((row, 0));
            while (queue.Count > 0)
            {
                var (info, depth) = queue.Dequeue();
                if (depth > 0 && info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                {
                    return info.Address;
                }

                if (depth < ControlDepth)
                {
                    foreach (var child in this.elements.ReadChildren(info))
                    {
                        if (this.elements.TryReadElement(child, out var childInfo))
                        {
                            queue.Enqueue((childInfo, depth + 1));
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }
    }
}
