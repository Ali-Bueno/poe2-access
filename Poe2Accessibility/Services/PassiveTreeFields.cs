namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using GameOffsets.Natives;

    /// <summary>
    ///     Fields of the passive tree's state and graph objects a game update can move (docs/compatibility.md), each found
    ///     again by what it holds: the state's graph file (named Metadata/PassiveSkillGraph.psg), the file's content (an
    ///     edge list joining node records that lead to PassiveSkills rows) and the state's allocated list (graph ids of the
    ///     graph's nodes). One change at a time is assumed: the state is found by the canvas, the file in a found state.
    /// </summary>
    public sealed class PassiveTreeFields
    {
        private const int Radius = 0x100;
        private const int Step = 8;

        /// <summary>The tree has thousands of links; fewer than this is no edge list.</summary>
        private const int MinEdges = 100;

        private const int MaxEdges = 20000;

        /// <summary>
        ///     Edges sampled to judge a list: each reaches a node record, and some join two (the first edges start at
        ///     records with no PassiveSkills row, the class starts' roots: 6 of the first 8 in 0.5.5e).
        /// </summary>
        private const int SampledEdges = 8;

        private const int MaxAllocated = 2000;

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly Func<IntPtr, bool> isNodeRecord;
        private readonly Func<IntPtr, int?> graphIdOf;
        private readonly int measuredFile = GameLayout.PassiveTree.StateGraphFileOffset;
        private readonly int measuredContent = GameLayout.PassiveTree.GraphContentOffset;
        private readonly int measuredEdges = GameLayout.PassiveTree.EdgeListOffset;
        private readonly int measuredAllocated = GameLayout.PassiveTree.AllocatedOffset;
        private bool fileKnown;
        private bool edgesKnown;
        private bool allocatedKnown;
        private Task<HashSet<int>>? graphIds;

        public PassiveTreeFields(HostMemoryReader reader, UiElementReader elements, Func<IntPtr, bool> isNodeRecord, Func<IntPtr, int?> graphIdOf)
        {
            this.reader = reader;
            this.elements = elements;
            this.isNodeRecord = isNodeRecord;
            this.graphIdOf = graphIdOf;
        }

        public CompatibilityReport? Report { get; set; }

        public Action<string>? Log { get; set; }

        /// <summary>The state's graph file, or zero; a state whose file is not where measured is searched once for it.</summary>
        public IntPtr GraphFile(IntPtr state, bool search)
        {
            if (this.IsGraphFile(state, GameLayout.PassiveTree.StateGraphFileOffset) is { } file)
            {
                this.Settle(ref this.fileKnown, "passive tree graph", this.measuredFile, GameLayout.PassiveTree.StateGraphFileOffset);
                return file;
            }

            if (this.fileKnown || !search ||
                OffsetSearch.First(this.measuredFile, Radius, Step, at => this.IsGraphFile(state, at) != null, Step) is not { } found)
            {
                return IntPtr.Zero;
            }

            GameLayout.PassiveTree.StateGraphFileOffset = found;
            this.Settle(ref this.fileKnown, "passive tree graph", this.measuredFile, found);
            return this.IsGraphFile(state, found) ?? IntPtr.Zero;
        }

        /// <summary>The graph's edge list (edge pointers, each {from record, to record}); null when it cannot be found.</summary>
        public StdVector? Edges(IntPtr graphFile)
        {
            if (this.EdgesAt(graphFile, GameLayout.PassiveTree.GraphContentOffset, GameLayout.PassiveTree.EdgeListOffset) is { } edges)
            {
                this.Settle(ref this.edgesKnown, "passive tree links", this.measuredEdges, GameLayout.PassiveTree.EdgeListOffset);
                return edges;
            }

            if (this.edgesKnown)
            {
                return null;
            }

            foreach (var content in OffsetSearch.Outward(this.measuredContent, Radius, Step))
            {
                if (OffsetSearch.First(this.measuredEdges, Radius, Step, at => this.EdgesAt(graphFile, content, at) != null) is { } list)
                {
                    (GameLayout.PassiveTree.GraphContentOffset, GameLayout.PassiveTree.EdgeListOffset) = (content, list);
                    this.Settle(ref this.edgesKnown, "passive tree links", this.measuredEdges, list);
                    return this.EdgesAt(graphFile, content, list);
                }
            }

            this.edgesKnown = true; // searched once: not repeated on every hover
            this.Log?.Invoke($"Passive tree links where measured: {this.DescribeEdges(graphFile, GameLayout.PassiveTree.GraphContentOffset, GameLayout.PassiveTree.EdgeListOffset)}");
            this.Report?.Missing("passive tree links");
            return null;
        }

        /// <summary>
        ///     Judges the allocated list once the graph's node ids are known (gathered off the frame thread from its
        ///     records): where measured it holds only such ids; else the nearest list in the state that does is taken.
        /// </summary>
        public void JudgeAllocated(IntPtr state, IReadOnlyCollection<IntPtr> records)
        {
            if (this.allocatedKnown || records.Count == 0)
            {
                return;
            }

            if (this.graphIds == null)
            {
                var snapshot = records.ToList();
                this.graphIds = Task.Run(() => snapshot.Select(this.graphIdOf).Where(id => id != null).Select(id => id!.Value).ToHashSet());
                return;
            }

            if (!this.graphIds.IsCompletedSuccessfully)
            {
                this.graphIds = this.graphIds.IsCompleted ? null : this.graphIds;
                return;
            }

            var ids = this.graphIds.Result;
            var measured = this.AllocatedAt(state, GameLayout.PassiveTree.AllocatedOffset);
            if (measured is { Count: > 0 } && measured.All(ids.Contains))
            {
                this.Settle(ref this.allocatedKnown, "passive tree allocations", this.measuredAllocated, GameLayout.PassiveTree.AllocatedOffset);
                return;
            }

            if (OffsetSearch.First(this.measuredAllocated, Radius, Step, at => this.AllocatedAt(state, at) is { Count: > 0 } list && list.All(ids.Contains), Step) is { } found)
            {
                GameLayout.PassiveTree.AllocatedOffset = found;
                this.Settle(ref this.allocatedKnown, "passive tree allocations", this.measuredAllocated, found);
            }
            else if (measured is { Count: > 0 })
            {
                this.allocatedKnown = true;
                this.Report?.Missing("passive tree allocations");
            }

            // Nothing allocated anywhere (a new character): judged again on a later opening.
        }

        /// <summary>The graph ids in an allocated list at the offset of the state; null when no such list is there.</summary>
        public List<int>? AllocatedAt(IntPtr state, int offset)
        {
            if (!this.reader.TryRead<StdVector>(state + offset, out var vector))
            {
                return null;
            }

            var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
            if (bytes < 0 || bytes % GameLayout.PassiveTree.AllocatedEntrySize != 0 || bytes / GameLayout.PassiveTree.AllocatedEntrySize > MaxAllocated ||
                (bytes > 0 && !this.reader.IsValidAddress(vector.First)))
            {
                return null;
            }

            var count = (int)(bytes / GameLayout.PassiveTree.AllocatedEntrySize);
            return this.reader.ReadArray<uint>(vector.First, count).Select(entry => (int)(entry & 0xFFFF)).ToList();
        }

        /// <summary>A new game process: every field is judged again from where it was measured.</summary>
        public void Reset()
        {
            GameLayout.PassiveTree.StateGraphFileOffset = this.measuredFile;
            GameLayout.PassiveTree.GraphContentOffset = this.measuredContent;
            GameLayout.PassiveTree.EdgeListOffset = this.measuredEdges;
            GameLayout.PassiveTree.AllocatedOffset = this.measuredAllocated;
            (this.fileKnown, this.edgesKnown, this.allocatedKnown, this.graphIds) = (false, false, false, null);
        }

        /// <summary>Research (dev command "layout"): which of the tree's fields have been judged.</summary>
        public string SelfTest() =>
            $"passive tree graph {(this.fileKnown ? "ok" : "not judged yet")}; links {(this.edgesKnown ? "ok" : "not judged yet")}; allocations {(this.allocatedKnown ? "ok" : "not judged yet (needs an allocated node)")}";

        private IntPtr? IsGraphFile(IntPtr state, int offset) =>
            this.reader.TryRead<IntPtr>(state + offset, out var file) && file != IntPtr.Zero && this.reader.IsValidAddress(file) &&
            this.elements.ReadWStringStructAt(file + GameLayout.SkillSlot.FileNameOffset) == GameLayout.PassiveTree.GraphFileName
                ? file
                : null;

        private StdVector? EdgesAt(IntPtr graphFile, int contentOffset, int edgesOffset)
        {
            if (!this.reader.TryRead<IntPtr>(graphFile + contentOffset, out var content) || content == IntPtr.Zero || !this.reader.IsValidAddress(content) ||
                !this.reader.TryRead<StdVector>(content + edgesOffset, out var vector))
            {
                return null;
            }

            var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
            if (bytes % IntPtr.Size != 0 || bytes / IntPtr.Size is < MinEdges or > MaxEdges || !this.reader.IsValidAddress(vector.First))
            {
                return null;
            }

            var joined = 0;
            foreach (var edge in this.reader.ReadArray<IntPtr>(vector.First, SampledEdges))
            {
                var ends = this.reader.ReadArray<IntPtr>(edge, 2);
                var nodes = ends.Length < 2 ? 0 : (this.isNodeRecord(ends[0]) ? 1 : 0) + (this.isNodeRecord(ends[1]) ? 1 : 0);
                if (nodes == 0)
                {
                    return null;
                }

                joined += nodes == 2 ? 1 : 0;
            }

            return joined > 0 ? vector : null;
        }

        private string DescribeEdges(IntPtr graphFile, int contentOffset, int edgesOffset)
        {
            if (!this.reader.TryRead<IntPtr>(graphFile + contentOffset, out var content) || !this.reader.TryRead<StdVector>(content + edgesOffset, out var vector))
            {
                return "unreadable";
            }

            var edges = this.reader.ReadArray<IntPtr>(vector.First, SampledEdges);
            var valid = edges.Select(edge => this.reader.ReadArray<IntPtr>(edge, 2)).Select(ends => ends.Length == 2 ? $"{this.isNodeRecord(ends[0])}/{this.isNodeRecord(ends[1])}" : "short");
            return $"{(vector.Last.ToInt64() - vector.First.ToInt64()) / IntPtr.Size} entries; sampled {string.Join(" ", valid)}";
        }

        private void Settle(ref bool known, string part, int measured, int found)
        {
            if (known)
            {
                return;
            }

            known = true;
            if (found == measured)
            {
                this.Report?.Found(part);
            }
            else
            {
                this.Report?.Moved(part, measured, found);
            }
        }
    }
}
