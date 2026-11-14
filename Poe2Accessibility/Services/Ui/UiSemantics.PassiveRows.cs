namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Natives;
    using GameReader;
    using GameReader.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>The passive tree's node rows: a node's state, allocated list and tree state, and its dat row read and compared with the game files.</summary>
    public sealed partial class UiSemantics
    {
        private PassiveState StateOf(PassiveGraph graph, PassiveNodeRow node, HashSet<int> allocated)
        {
            if (node.TakenIn(allocated) || graph.IsOwnStart(node.Record))
            {
                return PassiveState.Allocated; // the start is the character's own: an ascendancy's is in no allocated list
            }

            // Reachable: linked to an allocated node, or to a start of the character's own.
            return graph.Links.TryGetValue(node.Record, out var linked) &&
                   linked.Any(other => graph.IsOwnStart(other) || (this.PassiveRow(other) is { } row && this.RowTaken(row, allocated)))
                ? PassiveState.Reachable
                : PassiveState.Locked;
        }

        /// <summary>The push halfway between two octants, the straight direction first ("between left and up left"; Spanish "y izquierda" would want "e").</summary>
        private static string Between(int half)
        {
            var (first, second) = half % 2 == 0 ? (half, (half + 1) % 8) : ((half + 1) % 8, half);
            return Announcements.BetweenDirections(Announcements.StickDirections[first], Announcements.StickDirections[second]);
        }

        private static string StateWord(PassiveState state) => state switch
        {
            PassiveState.Allocated => Announcements.PassiveAllocated,
            PassiveState.Reachable => Announcements.PassiveReachable,
            _ => Announcements.PassiveLocked,
        };

        /// <summary>The graph ids in the tree's allocated list (re-read on every describe: allocating changes it).</summary>
        private HashSet<int> AllocatedIds(IntPtr state)
        {
            var now = Environment.TickCount64;
            if (this.TreeFields.AllocatedAt(state, GameLayout.PassiveTree.AllocatedOffset) is { } list)
            {
                this.lastAllocated = (state, now, list.ToHashSet());
                return this.lastAllocated.Ids;
            }

            // One failed read is not "nothing allocated": what was just read of this tree stands for a moment.
            return this.lastAllocated.State == state && now - this.lastAllocated.At < AllocatedKeptMs ? this.lastAllocated.Ids : new HashSet<int>();
        }

        /// <summary>The allocated graph ids of the whole tree: the class's list and, when it keeps one of its own, the ascendancy's.</summary>
        private HashSet<int> AllocatedIn(PassiveGraph graph)
        {
            var ids = this.AllocatedIds(graph.State);
            return graph.InnerState != IntPtr.Zero && graph.InnerState != graph.State &&
                   this.TreeFields.AllocatedAt(graph.InnerState, GameLayout.PassiveTree.AllocatedOffset) is { } inner
                ? ids.Concat(inner).ToHashSet()
                : ids;
        }

        /// <summary>The choice made on each allocated node that has one, by the node's graph id: the class's tree's and the ascendancy's.</summary>
        private IReadOnlyDictionary<int, int> ChoicesIn(PassiveGraph graph)
        {
            var choices = this.TreeFields.Choices(graph.State);
            if (graph.InnerState == IntPtr.Zero || graph.InnerState == graph.State || this.TreeFields.Choices(graph.InnerState) is not { Count: > 0 } inner)
            {
                return choices;
            }

            var all = new Dictionary<int, int>(choices);
            foreach (var (node, variant) in inner)
            {
                all[node] = variant;
            }

            return all;
        }

        /// <summary>What a node was given, as the game names it ("Strength"); empty for a node with no choice made.</summary>
        private static string ChoiceName(PassiveNodeRow node, IReadOnlyDictionary<int, int> choices) =>
            ChoiceOf(node, choices) is { } variant && PassiveTreeCatalog.Shared.VariantOf(variant) is { } known
                ? ShownName(PassiveTreeCatalog.Shared.Name(known.Id, Localization.Language) ?? string.Empty)
                : string.Empty;

        private static int? ChoiceOf(PassiveNodeRow node, IReadOnlyDictionary<int, int> choices) =>
            choices.TryGetValue(node.GraphId, out var variant) || (node.ShownGraphId >= 0 && choices.TryGetValue(node.ShownGraphId, out variant)) ? variant : null;

        /// <summary>A node's kind as it is said: an ascendancy's is named as such, its tree being drawn inside the class's.</summary>
        private static string KindWords(PassiveClass kind, string id) => BuildPlan.IsAscendancyNode(id)
            ? Announcements.PassiveNode(Announcements.PassiveKind(kind), Announcements.OfAscendancy, string.Empty)
            : Announcements.PassiveKind(kind);

        private const int AllocatedKeptMs = 2000;
        private (IntPtr State, long At, HashSet<int> Ids) lastAllocated = (IntPtr.Zero, 0, new HashSet<int>());

        /// <summary>
        ///     The canvas's tree state, judged where it holds the passive skill graph; zero on any other element. When no
        ///     canvas field leads to it, the state's own graph file field is looked for (one change at a time), on an element
        ///     shaped like the canvas only.
        /// </summary>
        private IntPtr TreeState(IntPtr canvas)
        {
            var shift = this.elements.Controls?.Shift(ControlGroup.PassiveCanvas, canvas, at => this.TreeStateAt(canvas, at) != IntPtr.Zero) ?? 0;
            if (this.TreeStateAt(canvas, shift) is var state && state != IntPtr.Zero)
            {
                return state;
            }

            return this.elements.TryReadElement(canvas, out var info) && this.elements.ReadChildren(info).Length >= MinCanvasGroups &&
                   this.reader.TryRead<IntPtr>(canvas + GameLayout.PassiveTree.CanvasStateOffset + shift, out var candidate) && candidate != IntPtr.Zero &&
                   this.TreeFields.GraphFile(candidate, true) != IntPtr.Zero
                ? candidate
                : IntPtr.Zero;
        }

        /// <summary>A row's graph id: the game files' by the row's Id, else the row's own column (a node newer than the catalog).</summary>
        private int? RowGraphId(IntPtr row) =>
            this.RowId(row) is { } id && PassiveTreeCatalog.Shared.ById(id) is { } node ? node.GraphId
            : this.reader.TryRead<ushort>(row + GameLayout.PassiveTree.RowGraphIdOffset, out var read) ? read : null;

        /// <summary>
        ///     A node record's graph id and that of the row its class sees in its place (null when it sees its own), for the
        ///     allocated list's check. Called off the frame thread: it takes the player's path as last noted there and no cache.
        /// </summary>
        private (int? Own, int? Shown) GraphIdsOf(IntPtr record)
        {
            if (this.PassiveRow(record) is not { } row)
            {
                return (null, null);
            }

            var own = this.RowGraphId(row);
            var path = this.judgedPath;
            if (this.Overrides is not { Count: > 0 } overrides || path.Length == 0 || this.RowId(row) is not { } id || overrides.Find(path, id) is not { } swap)
            {
                return (own, null);
            }

            var other = row + (nint)overrides.Step(swap);
            return (own, this.RowId(other) == swap.ShownId && this.reader.TryRead<ushort>(other + GameLayout.PassiveTree.RowGraphIdOffset, out var shown) ? shown : null);
        }

        /// <summary>The player's path when the allocated list was last judged, for <see cref="GraphIdsOf"/>'s worker thread.</summary>
        private volatile string judgedPath = string.Empty;

        private IntPtr TreeStateAt(IntPtr canvas, int shift) =>
            this.reader.TryRead<IntPtr>(canvas + GameLayout.PassiveTree.CanvasStateOffset + shift, out var state) && state != IntPtr.Zero &&
            this.TreeFields.GraphFile(state, false) != IntPtr.Zero
                ? state
                : IntPtr.Zero;

        /// <summary>A node element's graph record, judged where it leads to a PassiveSkills row.</summary>
        private IntPtr? NodeRecord(IntPtr node)
        {
            var shift = this.elements.Controls?.Shift(ControlGroup.PassiveNode, node, at => this.NodeRecordAt(node, at) is { } record && this.PassiveRow(record) != null) ?? 0;
            return this.NodeRecordAt(node, shift);
        }

        private IntPtr? NodeRecordAt(IntPtr node, int shift) =>
            this.reader.TryRead<IntPtr>(node + GameLayout.PassiveTree.NodeRecordOffset + shift, out var record) && record != IntPtr.Zero ? record : null;

        /// <summary>Where a node record keeps its PassiveSkills key: at its start in 0.5.5f, found again by the table its file names.</summary>
        private static readonly DatKeySite PassiveSkillKey = new("passive node key", GameLayout.PassiveTree.RecordSkillKeyOffset, GameLayout.PassiveTree.SkillTable);

        /// <summary>The PassiveSkills row of a node record, trusted only when its key names the table.</summary>
        private IntPtr? PassiveRow(IntPtr record) => PassiveSkillKey.Row(this.reader, this.elements, record);

        /// <summary>
        ///     A node as the game shows it. What a node is cannot change while the game runs, so its name, kind, graph id and
        ///     class start come from the game's files by the row's Id (<see cref="PassiveTreeCatalog"/>), the row's columns
        ///     backing them up and checked against them. The row is read as before for a node the files lack (one a patch
        ///     added) and for the row the player's class sees in its place (<see cref="ShownRow"/>), which gives name and kind.
        ///     A catalog can be older than the game (a patch under a running host, a regeneration that failed): once the rows
        ///     were seen to agree with the files on most nodes, a node whose row differs is said from its row, as the skills'
        ///     names are (<see cref="PassiveTreeFields.RowsDecide"/>).
        /// </summary>
        private PassiveNodeRow ReadPassiveRow(IntPtr record, IntPtr row)
        {
            var shownRow = this.ShownRow(row);
            var catalog = PassiveTreeCatalog.Shared;
            var known = this.RowId(row) is { } id ? catalog.ById(id) : null;
            if (known != null && shownRow == row)
            {
                if (!this.CompareWithRow(known, row))
                {
                    // A node the files know but leave unnamed (a catalog short of names) keeps the name its row holds.
                    var named = KeywordLink.Replace(catalog.Name(known.Id, Localization.Language) is { Length: > 0 } fromFiles ? fromFiles : this.RowName(row), ShownText);
                    var gated = this.reader.TryRead<long>(row + GameLayout.PassiveTree.RowNoLinkedStepOffset, out var gate) && gate != 0;
                    return new PassiveNodeRow(record, known.GraphId, named, KindWords(known.Class, known.Id), known.IconOnly, known.IsStart || BuildPlan.IsAscendancyStart(known.Id), known.Class, gated) { Id = known.Id };
                }

                known = null; // the game's row says otherwise and the rows read right: the node is read from it, as one the files lack
            }

            var bytes = this.elements.ReadBlock(row, GameLayout.PassiveTree.RowBytes);
            if (bytes.Length < GameLayout.PassiveTree.RowBytes)
            {
                return new PassiveNodeRow(record, -1, string.Empty, string.Empty, false, false, PassiveClass.Other);
            }

            var shown = shownRow == row ? bytes : this.elements.ReadBlock(shownRow, GameLayout.PassiveTree.RowBytes);
            if (shown.Length < GameLayout.PassiveTree.RowBytes)
            {
                shown = bytes;
            }

            var name = KeywordLink.Replace(this.elements.ReadUtf16BufferAt((IntPtr)BitConverter.ToInt64(shown, GameLayout.PassiveTree.RowNameOffset), MaxPassiveNameChars) ?? string.Empty, ShownText);
            var isStart = known?.IsStart ?? BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowCharactersOffset) > 0;
            var iconOnly = shown[GameLayout.PassiveTree.RowIconOnlyOffset] == 1;
            var kind = PassiveTreeCatalog.ClassOf(
                shown[GameLayout.PassiveTree.RowKeystoneOffset] == 1, shown[GameLayout.PassiveTree.RowNotableOffset] == 1,
                shown[GameLayout.PassiveTree.RowJewelSocketOffset] == 1, iconOnly, isStart);
            var noLinkedStep = BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowNoLinkedStepOffset) != 0;
            var shownId = this.RowId(shownRow) ?? string.Empty;
            return new PassiveNodeRow(record, known?.GraphId ?? BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset), name, KindWords(kind, shownId), iconOnly, isStart, kind, noLinkedStep)
            {
                ShownGraphId = BitConverter.ToUInt16(shown, GameLayout.PassiveTree.RowGraphIdOffset),
                Id = shownId,
            };
        }

        /// <summary>The name a row itself holds, markup as the game keeps it; empty when it has none or cannot be read.</summary>
        private string RowName(IntPtr row) =>
            this.reader.TryRead<IntPtr>(row + GameLayout.PassiveTree.RowNameOffset, out var name) && name != IntPtr.Zero
                ? this.elements.ReadUtf16BufferAt(name, MaxPassiveNameChars) ?? string.Empty
                : string.Empty;

        /// <summary>A catalog name as the tooltip's title shows it: keyword links as their text.</summary>
        private static string ShownName(string name) => KeywordLink.Replace(name, ShownText);

        /// <summary>
        ///     The cross-check of the two sources: the row's own columns must say what the files say of the node (its name in
        ///     whichever language the game runs in, its flags, graph id and class start). The first nodes read give the
        ///     verdict on the rows; after it, true when this node's row differs and the rows are the ones to believe.
        /// </summary>
        private bool CompareWithRow(PassiveTreeCatalog.Node known, IntPtr row)
        {
            var wanted = this.TreeFields.WantsNode(known.Id);
            if ((!wanted && !this.TreeFields.RowsDecide) || this.elements.ReadBlock(row, GameLayout.PassiveTree.RowBytes) is not { } bytes || bytes.Length < GameLayout.PassiveTree.RowBytes)
            {
                return false; // a row not readable now compares nothing
            }

            var differences = new List<string>();
            var name = this.elements.ReadUtf16BufferAt((IntPtr)BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowNameOffset), MaxPassiveNameChars) ?? string.Empty;
            if (!PassiveTreeCatalog.Shared.IsNameOf(known.Id, name))
            {
                differences.Add($"the row's name reads '{name}'");
            }

            if ((bytes[GameLayout.PassiveTree.RowKeystoneOffset] == 1) != known.Keystone || (bytes[GameLayout.PassiveTree.RowNotableOffset] == 1) != known.Notable ||
                (bytes[GameLayout.PassiveTree.RowJewelSocketOffset] == 1) != known.JewelSocket || (bytes[GameLayout.PassiveTree.RowIconOnlyOffset] == 1) != known.IconOnly)
            {
                differences.Add("the row's kind flags differ");
            }

            if (BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset) != known.GraphId)
            {
                differences.Add($"the row's graph id reads {BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset)}, the files' {known.GraphId}");
            }

            if ((BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowCharactersOffset) > 0) != known.IsStart)
            {
                differences.Add("the row's class list differs");
            }

            var difference = differences.Count == 0 ? null : string.Join(", ", differences);
            if (wanted)
            {
                this.TreeFields.NoteNode(known.Id, difference);
                return false; // the nodes that give the verdict are said from the files, as before it
            }

            // A name that is no text is a row that did not read, not the game's word.
            if (difference == null || !SkillNameCheck.IsText(name))
            {
                return false;
            }

            this.TreeFields.NoteOwnRow(known.Id, difference);
            return true;
        }

        /// <summary>
        ///     The PassiveSkills row the game shows for a node's own row: the player's class's override of it when it has one
        ///     (<see cref="Overrides"/>), found by stepping through the table and trusted only when the Id there is the expected
        ///     one; else the row itself. Remembered per row for the character it was found for.
        /// </summary>
        private IntPtr ShownRow(IntPtr row)
        {
            var path = this.PlayerPath?.Invoke() ?? string.Empty;
            if (this.Overrides is not { Count: > 0 } overrides || path.Length == 0)
            {
                return row;
            }

            if (!string.Equals(path, this.shownRowsFor, StringComparison.OrdinalIgnoreCase))
            {
                this.shownRows.Clear();
                this.shownRowsFor = path;
            }

            if (this.shownRows.TryGetValue(row, out var known))
            {
                return known;
            }

            var shown = row;
            if (this.RowId(row) is { } id && overrides.Find(path, id) is { } swap)
            {
                var other = row + (nint)overrides.Step(swap);
                if (this.RowId(other) == swap.ShownId)
                {
                    shown = other;
                }
                else
                {
                    this.TreeFields.Log?.Invoke($"Passive override of {id} for {path}: no {swap.ShownId} {overrides.Step(swap)} bytes on; its own row is read");
                }
            }

            this.shownRows[row] = shown;
            return shown;
        }

        private string? RowId(IntPtr row) =>
            this.reader.TryRead<IntPtr>(row, out var id) && id != IntPtr.Zero ? this.elements.ReadUtf16BufferAt(id, MaxPassiveNameChars) : null;

        /// <summary>Whether a node's row, or the one its class sees in its place, is in the allocated list (which of the two the game keeps there is not measured).</summary>
        private bool RowTaken(IntPtr row, HashSet<int> allocated) =>
            (this.RowGraphId(row) is { } id && allocated.Contains(id)) ||
            (this.ShownRow(row) is var shown && shown != row && this.reader.TryRead<ushort>(shown + GameLayout.PassiveTree.RowGraphIdOffset, out var shownId) && allocated.Contains(shownId));
    }
}
