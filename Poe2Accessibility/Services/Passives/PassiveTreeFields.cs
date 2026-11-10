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
    ///     graph's nodes). Each is looked for near where it was measured, then through its whole object
    ///     (<see cref="PassiveGraphSearch"/>); the state itself is found by the canvas, the file in a found state.
    /// </summary>
    public sealed class PassiveTreeFields
    {
        private const int Radius = 0x100;
        private const int Step = 8;

        // The whole objects searched once nothing fits nearby, each several times as far as its farthest field measured:
        // the state (the allocated list at 0x1A8), the graph file (its content at 0x28) and the content (the edge list at 0x58).
        private const int WideStateBytes = 0x800;
        private const int WideFileBytes = 0x200;
        private const int WideContentBytes = 0x400;

        /// <summary>The state is asked for its file on every candidate of the canvas's search: its whole-object search runs this often at most.</summary>
        private const int WideFileEveryMs = 10_000;

        /// <summary>A connected tree of n drawn groups has at least n − 1 links; half the groups is a floor no tree misses, whatever its size.</summary>
        private const int GroupsPerEdgeFloor = 2;

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
        private readonly Func<IntPtr, (int? Own, int? Shown)> graphIdsOf;
        private readonly int measuredFile = GameLayout.PassiveTree.StateGraphFileOffset;
        private readonly int measuredContent = GameLayout.PassiveTree.GraphContentOffset;
        private readonly int measuredEdges = GameLayout.PassiveTree.EdgeListOffset;
        private readonly int measuredAllocated = GameLayout.PassiveTree.AllocatedOffset;
        private readonly (int In, int Out) measuredRecordEdges = (GameLayout.PassiveTree.RecordInEdgesOffset, GameLayout.PassiveTree.RecordOutEdgesOffset);
        private bool fileKnown;
        private bool edgesKnown;
        private bool allocatedKnown;
        private Task<HashSet<int>>? graphIds;

        /// <param name="graphIdsOf">A record's graph id and that of the row its class sees in its place; called off the frame thread.</param>
        public PassiveTreeFields(HostMemoryReader reader, UiElementReader elements, Func<IntPtr, bool> isNodeRecord, Func<IntPtr, (int? Own, int? Shown)> graphIdsOf)
        {
            this.reader = reader;
            this.elements = elements;
            this.isNodeRecord = isNodeRecord;
            this.graphIdsOf = graphIdsOf;
        }

        public CompatibilityReport? Report { get; set; }

        public Action<string>? Log { get; set; }

        /// <summary>Every graph id the game's files give the tree (<see cref="PassiveTreeCatalog"/>); empty without the catalog.</summary>
        public Func<IReadOnlySet<int>>? FileIds { get; set; }

        /// <summary>Nodes compared before the files and the rows in memory are said to agree or not: a screenful of the tree.</summary>
        private const int NodesCompared = 64;

        private readonly CatalogCrossCheck nodeCheck = new(NodesCompared);
        private bool linksCompared;
        private bool edgesFromFiles;

        /// <summary>Whether a node of this Id is still to be compared with its row in memory (<see cref="NoteNode"/>).</summary>
        public bool WantsNode(string passiveId) => this.nodeCheck.Wants(passiveId);

        /// <summary>
        ///     Gets a value indicating whether a node whose row differs from the files is said from its row: once enough nodes
        ///     were compared and most agree. The rows are then seen to read right, so the one that differs is the game's own
        ///     word on a node the catalog has from an older build; while most differ it is the rows' columns that moved, and
        ///     the files are said (the rule of the skills' names, <see cref="SkillNameCheck"/>).
        /// </summary>
        public bool RowsDecide { get; private set; }

        /// <summary>Nodes said from their row against the files, logged once each (a patch changes a few).</summary>
        private readonly HashSet<string> ownRows = new(StringComparer.Ordinal);

        private const int OwnRowsLogged = 32;

        /// <summary>A node said from its row because it differs from the files there (<see cref="RowsDecide"/>).</summary>
        public void NoteOwnRow(string passiveId, string difference)
        {
            if (this.ownRows.Count < OwnRowsLogged && this.ownRows.Add(passiveId))
            {
                this.Log?.Invoke($"Passive tree catalog: {passiveId} differs from the game files ({difference}); the game's row is said");
            }
        }

        /// <summary>
        ///     One node read from the files and from its row; <paramref name="difference"/> null when they agree. Logged once
        ///     when enough were compared; when most differ the row's columns are not where they are read (or the catalog is
        ///     another build's), said as any column nothing judged is.
        /// </summary>
        public void NoteNode(string passiveId, string? difference)
        {
            if (this.nodeCheck.Note(passiveId, difference) is not { } verdict)
            {
                return;
            }

            this.RowsDecide = verdict.RowsDecide;
            if (verdict.Disagreeing == 0)
            {
                this.Log?.Invoke($"Passive tree catalog: the game files and the rows in memory agree on {verdict.Compared} nodes");
                return;
            }

            this.Log?.Invoke($"Passive tree catalog: the game files and the rows in memory DISAGREE on {verdict.Disagreeing} of {verdict.Compared} nodes (first {verdict.First}); " +
                             (verdict.MostDisagree ? "the files' are said" : "a node whose row differs is said from its row"));
            if (verdict.MostDisagree)
            {
                this.Report?.Missing(DatColumns.UnjudgedPart);
            }
        }

        /// <summary>The files' links set beside the graph's edge list in memory, once per game process: a log line, nothing said.</summary>
        public void NoteLinks(int compared, int differing, string first)
        {
            if (this.linksCompared || compared == 0)
            {
                return;
            }

            this.linksCompared = true;
            this.Log?.Invoke(differing == 0
                ? $"Passive tree catalog: the game files' links and the graph in memory agree on {compared} nodes"
                : $"Passive tree catalog: the game files' links and the graph in memory DISAGREE on {differing} of {compared} nodes (first {first}); the files' are used");
        }

        /// <summary>The state's graph file, or zero; a state whose file is not where measured is searched once for it.</summary>
        public IntPtr GraphFile(IntPtr state, bool search)
        {
            if (this.IsGraphFile(state, GameLayout.PassiveTree.StateGraphFileOffset) is { } file)
            {
                this.Settle(ref this.fileKnown, "passive tree graph", this.measuredFile, GameLayout.PassiveTree.StateGraphFileOffset);
                return file;
            }

            if (this.fileKnown || !search)
            {
                return IntPtr.Zero;
            }

            // Far from where measured the name alone is not enough: the file must lead to an edge list too.
            var now = Environment.TickCount64;
            var wide = now >= this.nextWideFileAt;
            this.nextWideFileAt = wide ? now + WideFileEveryMs : this.nextWideFileAt;
            if (PassiveGraphSearch.File(
                    at => this.reader.TryRead<IntPtr>(state + at, out var file) ? file.ToInt64() : 0,
                    file => this.NamesGraph(new IntPtr(file)),
                    file => this.FindEdges(new IntPtr(file), 1) != null,
                    this.measuredFile, Radius, Step, wide ? WideStateBytes : 0) is not { } found)
            {
                return IntPtr.Zero;
            }

            GameLayout.PassiveTree.StateGraphFileOffset = found;
            this.Settle(ref this.fileKnown, "passive tree graph", this.measuredFile, found);
            return this.IsGraphFile(state, found) ?? IntPtr.Zero;
        }

        /// <summary>
        ///     The graph's edge list (edge pointers, each {from record, to record}); null when it cannot be found. The list must hold
        ///     at least half as many edges as the canvas draws groups (<paramref name="drawnGroups"/>): the floor scales with the tree.
        ///     With <paramref name="filesStandIn"/> the game files' links are read in its place, so a list not found is logged, not said.
        /// </summary>
        public StdVector? Edges(IntPtr graphFile, int drawnGroups, bool filesStandIn = false)
        {
            var minEdges = Math.Max(1, drawnGroups / GroupsPerEdgeFloor);
            if (this.EdgesAt(graphFile, GameLayout.PassiveTree.GraphContentOffset, GameLayout.PassiveTree.EdgeListOffset, minEdges) is { } edges)
            {
                this.Settle(ref this.edgesKnown, "passive tree links", this.measuredEdges, GameLayout.PassiveTree.EdgeListOffset);
                this.graphEdges = edges;
                return edges;
            }

            if (this.edgesKnown || Environment.TickCount64 < this.edgesRetryAt)
            {
                return null;
            }

            if (this.FindEdges(graphFile, minEdges) is (var content, var list))
            {
                (GameLayout.PassiveTree.GraphContentOffset, GameLayout.PassiveTree.EdgeListOffset) = (content, list);
                this.Settle(ref this.edgesKnown, "passive tree links", this.measuredEdges, list);
                return this.EdgesAt(graphFile, content, list, minEdges);
            }

            // Not repeated on every hover, and not given up at once: a search made before the tree's rows were readable
            // must not silence the tree for the session.
            this.edgesRetryAt = Environment.TickCount64 + EdgesRetryMs;
            if (++this.edgesFailures < EdgesFailuresToSay)
            {
                return null;
            }

            this.edgesKnown = true;
            this.Log?.Invoke($"Passive tree links where measured: {this.DescribeEdges(graphFile, GameLayout.PassiveTree.GraphContentOffset, GameLayout.PassiveTree.EdgeListOffset)}");
            if (filesStandIn)
            {
                // The tree still reads: only the edge list's order, the stick step's second source, is gone.
                this.edgesFromFiles = true;
                this.Log?.Invoke("Passive tree links: not found in memory; the game files' links are read, and the stick's order comes from each node's own lists");
                return null;
            }

            this.Report?.Missing("passive tree links");
            return null;
        }

        private const int EdgesRetryMs = 10_000;
        private const int EdgesFailuresToSay = 3;
        private long edgesRetryAt;
        private int edgesFailures;

        /// <summary>
        ///     Judges the allocated list once the graph's node ids are known (gathered off the frame thread from its
        ///     records): kept where measured while it names the graph's nodes (<see cref="AllocatedListCheck"/>); moved only
        ///     when it names almost none and another list of the state names nothing else; said lost on the second such reading.
        /// </summary>
        public void JudgeAllocated(IntPtr state, IReadOnlyCollection<IntPtr> records)
        {
            this.JudgeRecordEdges(records);
            if (this.allocatedKnown || records.Count == 0 || Environment.TickCount64 < this.allocatedRetryAt)
            {
                return;
            }

            if (this.graphIds == null)
            {
                // Both ids of each record: a node its class sees another in place of may stand in the list under either.
                var snapshot = records.ToList();
                this.graphIds = Task.Run(() => snapshot.Select(this.graphIdsOf).SelectMany(ids => new[] { ids.Own, ids.Shown }).Where(id => id != null).Select(id => id!.Value).ToHashSet());
                return;
            }

            if (!this.graphIds.IsCompletedSuccessfully)
            {
                this.graphIds = this.graphIds.IsCompleted ? null : this.graphIds;
                return;
            }

            IReadOnlySet<int> ids = this.graphIds.Result;
            if (this.FileIds?.Invoke() is { Count: > 0 } fromFiles)
            {
                // The files give every node of the graph, read or not; the rows add the ids of the nodes shown in others' place.
                ids = AllocatedListCheck.AllIds(fromFiles, ids);
            }
            else if (!AllocatedListCheck.IdsUsable(ids.Count, records.Count))
            {
                // The rows were not readable when the ids were gathered: gathered again, nothing judged on a partial set.
                this.graphIds = null;
                this.allocatedRetryAt = Environment.TickCount64 + AllocatedRetryMs;
                return;
            }

            var verdict = AllocatedListCheck.Judge(this.AllocatedAt(state, GameLayout.PassiveTree.AllocatedOffset), ids);
            if (verdict == AllocatedVerdict.Kept)
            {
                this.Settle(ref this.allocatedKnown, "passive tree allocations", this.measuredAllocated, GameLayout.PassiveTree.AllocatedOffset);
                return;
            }

            if (verdict == AllocatedVerdict.Unjudged)
            {
                return; // nothing allocated (a new character), or too few known ids to tell: judged again on a later node
            }

            // The state's fields move together more often than apart: as far as the graph file moved is tried first.
            var together = this.measuredAllocated + (GameLayout.PassiveTree.StateGraphFileOffset - this.measuredFile);
            if (PassiveGraphSearch.Allocated(
                    at => this.AllocatedAt(state, at),
                    at => this.reader.TryRead<IntPtr>(state + at, out var first) ? first.ToInt64() : 0,
                    ids, this.measuredAllocated, together, Radius, Step, WideStateBytes) is { } found)
            {
                GameLayout.PassiveTree.AllocatedOffset = found;
                this.Settle(ref this.allocatedKnown, "passive tree allocations", this.measuredAllocated, found);
                return;
            }

            this.allocatedRetryAt = Environment.TickCount64 + AllocatedRetryMs;
            if (++this.allocatedFailures >= AllocatedFailuresToSay)
            {
                this.allocatedKnown = true;
                this.Report?.Missing("passive tree allocations");
            }
        }

        /// <summary>
        ///     Judges, off the frame thread, where a node record keeps its own edge vectors against the graph file's edge list
        ///     (<see cref="RecordEdgesSearch"/>): kept or moved when the two agree. When nothing fits it is only logged, as the
        ///     stick order then comes from the file's list and nothing read is wrong.
        /// </summary>
        private void JudgeRecordEdges(IReadOnlyCollection<IntPtr> records)
        {
            if (this.recordEdgesKnown || this.graphEdges is not { } edges || records.Count == 0)
            {
                return;
            }

            if (this.recordEdges == null)
            {
                var snapshot = records.Take(MaxRecordSamples).ToList();
                this.recordEdges = Task.Run(() => this.FindRecordEdges(edges, snapshot));
                return;
            }

            if (!this.recordEdges.IsCompleted)
            {
                return;
            }

            this.recordEdgesKnown = true; // judged once per game process: the graph does not change while it runs
            var found = this.recordEdges.IsCompletedSuccessfully ? this.recordEdges.Result : null;
            if (found is not { } place)
            {
                this.Log?.Invoke("Passive node links: the records' own edge lists were not judged or fit nowhere; the stick order comes from the graph file's list");
                return;
            }

            (GameLayout.PassiveTree.RecordInEdgesOffset, GameLayout.PassiveTree.RecordOutEdgesOffset) = place;
            var known = false;
            var moved = place.In != this.measuredRecordEdges.In ? (this.measuredRecordEdges.In, place.In) : (this.measuredRecordEdges.Out, place.Out);
            this.Settle(ref known, "passive node links", moved.Item1, moved.Item2);
        }

        /// <summary>For a worker thread: counts each record's edges in the graph's list, then looks for the vectors that hold as many.</summary>
        private (int In, int Out)? FindRecordEdges(StdVector edges, IReadOnlyList<IntPtr> records)
        {
            var count = (int)Math.Min((edges.Last.ToInt64() - edges.First.ToInt64()) / IntPtr.Size, MaxEdges);
            var (into, from) = (new Dictionary<long, int>(), new Dictionary<long, int>());
            foreach (var edge in this.reader.ReadArray<IntPtr>(edges.First, count))
            {
                if (this.reader.ReadArray<long>(edge, 2) is { Length: 2 } ends)
                {
                    from[ends[0]] = from.GetValueOrDefault(ends[0]) + 1;
                    into[ends[1]] = into.GetValueOrDefault(ends[1]) + 1;
                }
            }

            var samples = records
                .Select(record => new RecordEdgesSearch.Sample(record.ToInt64(), this.reader.ReadBytes(record, RecordBytes), into.GetValueOrDefault(record.ToInt64()), from.GetValueOrDefault(record.ToInt64())))
                .Where(sample => sample.Block.Length > 0)
                .ToList();
            return RecordEdgesSearch.Find(samples, this.measuredRecordEdges, Step, (address, pointers) => this.reader.ReadArray<long>(new IntPtr(address), pointers));
        }

        /// <summary>Records the edge vectors are judged on, and the bytes of each that are searched (the vectors were measured at 0x38 and 0x50).</summary>
        private const int MaxRecordSamples = 64;
        private const int RecordBytes = 0x100;
        private StdVector? graphEdges;
        private Task<(int In, int Out)?>? recordEdges;
        private bool recordEdgesKnown;

        private const int AllocatedRetryMs = 10_000;
        private const int AllocatedFailuresToSay = 2;
        private long allocatedRetryAt;
        private long nextWideFileAt;
        private int allocatedFailures;

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
            (GameLayout.PassiveTree.RecordInEdgesOffset, GameLayout.PassiveTree.RecordOutEdgesOffset) = this.measuredRecordEdges;
            (this.graphEdges, this.recordEdges, this.recordEdgesKnown) = (null, null, false);
            (this.edgesRetryAt, this.edgesFailures, this.allocatedRetryAt, this.allocatedFailures, this.nextWideFileAt) = (0, 0, 0, 0, 0);
            this.nodeCheck.Reset();
            this.RowsDecide = false;
            this.ownRows.Clear();
            (this.linksCompared, this.edgesFromFiles) = (false, false);
        }

        /// <summary>Research (dev command "layout"): which of the tree's fields have been judged.</summary>
        public string SelfTest() =>
            $"passive tree graph {(this.fileKnown ? "ok" : "not judged yet")}; links {(this.edgesFromFiles ? "from the game files (not found in memory)" : this.edgesKnown ? "ok" : "not judged yet")}; allocations {(this.allocatedKnown ? "ok" : "not judged yet (needs an allocated node)")}; " +
            $"catalog nodes {(this.nodeCheck.Done ? "compared" : "not compared yet")}; " +
            $"node links {(this.recordEdgesKnown ? $"judged (+0x{GameLayout.PassiveTree.RecordInEdgesOffset:X}, +0x{GameLayout.PassiveTree.RecordOutEdgesOffset:X})" : "not judged yet")}";

        private IntPtr? IsGraphFile(IntPtr state, int offset) =>
            this.reader.TryRead<IntPtr>(state + offset, out var file) && this.NamesGraph(file) ? file : null;

        private bool NamesGraph(IntPtr file) =>
            file != IntPtr.Zero && this.reader.IsValidAddress(file) &&
            this.elements.ReadWStringStructAt(file + GameLayout.SkillSlot.FileNameOffset) == GameLayout.PassiveTree.GraphFileName;

        /// <summary>Where the file keeps its content and the content its edge list (<see cref="PassiveGraphSearch.Edges"/>); null when nowhere.</summary>
        private (int Content, int List)? FindEdges(IntPtr graphFile, int minEdges) =>
            PassiveGraphSearch.Edges(
                (content, list) => this.EdgesAt(graphFile, content, list, minEdges) is { } edges ? edges.First.ToInt64() : 0,
                (this.measuredContent, this.measuredEdges), Radius, Step, (WideFileBytes, WideContentBytes));

        private StdVector? EdgesAt(IntPtr graphFile, int contentOffset, int edgesOffset, int minEdges)
        {
            if (!this.reader.TryRead<IntPtr>(graphFile + contentOffset, out var content) || content == IntPtr.Zero || !this.reader.IsValidAddress(content) ||
                !this.reader.TryRead<StdVector>(content + edgesOffset, out var vector))
            {
                return null;
            }

            var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
            if (bytes % IntPtr.Size != 0 || bytes / IntPtr.Size < minEdges || bytes / IntPtr.Size > MaxEdges || !this.reader.IsValidAddress(vector.First))
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
