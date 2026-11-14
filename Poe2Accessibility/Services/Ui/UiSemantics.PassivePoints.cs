namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What the passive tree's window shows beside its canvas, for <see cref="UiSemantics"/>: the points left to spend
    ///     (the tree's, each weapon set's, the ascendancy's), the gold shown while passives are refunded, and the bar of
    ///     changes not yet confirmed (docs/research/passive-tree.md#the-trees-window).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The tree's own layout of its points left, and the keyword the ascendancy's label is written with ("[AscendancyPoints|…]:").</summary>
        private const string PointsLayoutName = "top_points_layout_gmp";

        private const string AscendancyPointsKey = "AscendancyPoints";

        /// <summary>The glyph of a button in a label's tagged text ("&lt;&lt;xbox_button_x;0&gt;&gt; Apply changes").</summary>
        private const string GlyphOpen = "<<";

        /// <summary>The points' labels are looked for again this often while they are not found.</summary>
        private const int PointLabelsMs = 1000;

        private TreeLabels pointLabels = TreeLabels.None;
        private (string Label, string Cost) changesShown = (string.Empty, string.Empty);
        private (IntPtr Record, IReadOnlyList<string> Lines) treeNews = (IntPtr.Zero, Array.Empty<string>());

        /// <summary>
        ///     The points left to spend on the class's tree, with each weapon set's points and on the ascendancy's, as the
        ///     tree's labels show them; a row not shown (the ascendancy's before ascending, a third weapon set) has none.
        ///     Null where the tree has no such labels (the altar's preview) or they do not read.
        /// </summary>
        private TreePoints? PointsLeft(PassiveGraph graph)
        {
            var labels = this.LabelsOf(graph);
            if (labels.Skill == IntPtr.Zero || !this.elements.TryReadText(labels.Skill, out var skillText) || !int.TryParse(skillText.Trim(), out var skill))
            {
                this.pointLabels = labels with { Skill = IntPtr.Zero }; // labels made again by the game are found again
                return null;
            }

            int Shown(IntPtr row, IntPtr value) =>
                this.elements.TryReadElement(row, out var info) && info.IsVisibleFlag && this.elements.TryReadText(value, out var text) && int.TryParse(text.Trim(), out var read) ? read : 0;
            return new TreePoints(skill, Shown(labels.AscendancyRow, labels.Ascendancy), labels.Sets.Select(set => Shown(set.Row, set.Value)).ToList());
        }

        /// <summary>
        ///     Whether the tree is open to refund passives (the Hooded One's "Refund Passives"): it then shows the player's
        ///     gold after the ascendancy's points, a row hidden on every other opening (39 openings logged on 2026-11-14,
        ///     the one refund among them).
        /// </summary>
        private bool Refunding(PassiveGraph graph) =>
            this.LabelsOf(graph) is { GoldRow: var row } && row != IntPtr.Zero && this.elements.TryReadElement(row, out var info) && info.IsVisibleFlag;

        /// <summary>
        ///     What changed in the tree's bar of unconfirmed changes since it was last read, as lines for the node under
        ///     the cursor: the count as the game words it and, when the changes cost gold, the gold before and after; once
        ///     the bar is gone and the gold is what it announced, that the changes were applied. The game's own line is
        ///     cut off by the node read again as its choice closes (three changes went unsaid, 2026-11-14). The lines stay
        ///     with the node while the cursor does.
        /// </summary>
        private IReadOnlyList<string> TreeNews(PassiveGraph graph, IntPtr record)
        {
            var labels = this.LabelsOf(graph);
            var shown = this.ChangesBar(labels);
            if (shown != this.changesShown)
            {
                var before = this.changesShown;
                this.changesShown = shown;
                var lines = new List<string>();
                if (shown.Label.Length > 0)
                {
                    lines.Add(shown.Label);
                    if (TreePoints.SplitCost(shown.Cost) is (var from, var to))
                    {
                        lines.Add(Announcements.PassiveChangeCost(from, to));
                    }
                }
                else if (TreePoints.SplitCost(before.Cost) is (_, var left) && this.elements.TryReadText(labels.Gold, out var gold) && gold.Trim() == left)
                {
                    lines.Add(Announcements.PassiveChangesApplied(left));
                }

                this.treeNews = (record, lines);
            }
            else if (this.treeNews.Record != record)
            {
                this.treeNews = (IntPtr.Zero, Array.Empty<string>());
            }

            return this.treeNews.Lines;
        }

        /// <summary>
        ///     What the tree's chooser of points shows while it is up ("Asignar (Principal)": held A on a node chooses the
        ///     type of points, the tree's own or a weapon set's), as one line; empty while it is hidden. The game draws it
        ///     over the tree with no other text, and nothing of it was ever read (not once in the play logs).
        /// </summary>
        private string PointsChoice(PassiveGraph graph)
        {
            var labels = this.LabelsOf(graph);
            return labels.Chooser != IntPtr.Zero && this.elements.TryReadElement(labels.Chooser, out var part) && part.IsVisibleFlag &&
                   this.elements.TryReadElement(labels.ChooserBox, out var box) && box.IsVisibleFlag &&
                   this.elements.TryReadText(labels.ChooserLabel, out var text)
                ? string.Join(' ', text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                : string.Empty;
        }

        /// <summary>The bar's count and cost while it is shown; empty texts while it is not.</summary>
        private (string Label, string Cost) ChangesBar(TreeLabels labels)
        {
            if (labels.ChangesBar == IntPtr.Zero || !this.elements.TryReadElement(labels.ChangesBar, out var bar) || !bar.IsVisibleFlag ||
                !this.elements.TryReadText(labels.Changes, out var label) || label.Trim().Length == 0)
            {
                return (string.Empty, string.Empty);
            }

            var cost = this.elements.TryReadElement(labels.CostRow, out var row) && row.IsVisibleFlag && this.elements.TryReadText(labels.Cost, out var text) ? text.Trim() : string.Empty;
            return (label.Trim(), cost);
        }

        private TreeLabels LabelsOf(PassiveGraph graph)
        {
            var now = Environment.TickCount64;
            var labels = this.pointLabels;
            if (labels.Canvas != graph.Canvas || (labels.Skill == IntPtr.Zero && now - labels.At >= PointLabelsMs))
            {
                this.pointLabels = labels = this.FindTreeLabels(graph.Canvas, now);
            }

            return labels;
        }

        /// <summary>
        ///     The labels beside the canvas. In the points layout the first row's value is the tree's; a row whose value
        ///     holds two counts is a weapon set's, in the sets' order; the ascendancy's is known by its label's keyword, and
        ///     the row after it holds the gold. The bar of changes is the one part of the window made of a count, a row
        ///     with the cost and a label with a button's glyph.
        /// </summary>
        private TreeLabels FindTreeLabels(IntPtr canvasAddress, long now)
        {
            var found = TreeLabels.None with { Canvas = canvasAddress, At = now };
            if (!this.elements.TryReadElement(canvasAddress, out var canvas) || !this.elements.TryReadElement(canvas.Raw.ParentPtr, out var panel))
            {
                return found;
            }

            var parts = this.elements.ReadChildren(panel).Where(part => part != canvasAddress).ToList();
            found = this.FindChooser(parts, this.FindChangesBar(parts, found));
            if (parts.Select(part => this.elements.FindNamed(part, PointsLayoutName, 1)).FirstOrDefault(part => part != null) is not { } rows)
            {
                return found;
            }

            var sets = new List<(IntPtr Row, IntPtr Value)>();
            var afterAscendancy = false;
            foreach (var (rowAddress, index) in this.elements.ReadChildren(rows).Select((row, index) => (row, index)))
            {
                if (!this.elements.TryReadElement(rowAddress, out var row) || this.elements.ReadChildren(row) is not { Length: >= 2 } cells)
                {
                    afterAscendancy = false;
                    continue;
                }

                var isGold = afterAscendancy;
                afterAscendancy = false;
                // A weapon set's row keeps its two counts a level deeper, "4" and "(4)": the first is the set's points left.
                if (this.elements.TryReadElement(cells[^1], out var pair) && this.elements.ReadChildren(pair) is { Length: 2 } counts)
                {
                    if (this.elements.TryReadText(counts[0], out var first) && int.TryParse(first.Trim(), out _))
                    {
                        sets.Add((rowAddress, counts[0]));
                    }

                    continue;
                }

                if (!this.elements.TryReadText(cells[^1], out var value) || value.Trim().Length == 0)
                {
                    continue;
                }

                if (this.elements.TryReadText(cells[^2], out _, out var tagged) && tagged.Contains(AscendancyPointsKey, StringComparison.Ordinal))
                {
                    (found, afterAscendancy) = (found with { AscendancyRow = rowAddress, Ascendancy = cells[^1] }, true);
                }
                else if (index == 0 && int.TryParse(value.Trim(), out _))
                {
                    found = found with { Skill = cells[^1] };
                }
                else if (isGold && cells.Length == 2 && value.Any(char.IsDigit))
                {
                    found = found with { GoldRow = rowAddress, Gold = cells[^1] };
                }
            }

            return found with { Sets = sets };
        }

        private TreeLabels FindChangesBar(IReadOnlyList<IntPtr> parts, TreeLabels found)
        {
            foreach (var part in parts)
            {
                if (!this.elements.TryReadElement(part, out var outer) || this.elements.ReadChildren(outer) is not { Length: 1 } only ||
                    !this.elements.TryReadElement(only[0], out var bar) || this.elements.ReadChildren(bar) is not { Length: 3 } cells ||
                    !this.elements.TryReadText(cells[0], out var count) || !count.Any(char.IsDigit) ||
                    !this.elements.TryReadElement(cells[1], out var costRow) || this.elements.ReadChildren(costRow) is not { Length: 2 } cost ||
                    !this.elements.TryReadText(cells[2], out _, out var apply) || !apply.Contains(GlyphOpen, StringComparison.Ordinal))
                {
                    continue;
                }

                return found with { ChangesBar = part, Changes = cells[0], CostRow = cells[1], Cost = cost[1] };
            }

            return found;
        }

        /// <summary>
        ///     The chooser of points: the one part of the window, with no name, whose only child's only child is a label,
        ///     both hidden while it is not up (its label read "Asignar / (Principal)" on a closed tree, 2026-11-14).
        /// </summary>
        private TreeLabels FindChooser(IReadOnlyList<IntPtr> parts, TreeLabels found)
        {
            foreach (var part in parts)
            {
                if (this.elements.TryReadElement(part, out var outer) && outer.Name.Length == 0 && this.elements.ReadChildren(outer) is { Length: 1 } only &&
                    this.elements.TryReadElement(only[0], out var box) && this.elements.ReadChildren(box) is { Length: 1 } inner &&
                    this.elements.TryReadElement(inner[0], out var label) && this.elements.ReadChildren(label).Length == 0 &&
                    this.elements.TryReadText(inner[0], out var text) && text.Trim().Length > 0)
                {
                    return found with { Chooser = part, ChooserBox = only[0], ChooserLabel = inner[0] };
                }
            }

            return found;
        }

        /// <summary>The window's labels by address; zero for one not found.</summary>
        private sealed record TreeLabels(
            IntPtr Canvas, IntPtr Skill, IntPtr AscendancyRow, IntPtr Ascendancy, IReadOnlyList<(IntPtr Row, IntPtr Value)> Sets,
            IntPtr GoldRow, IntPtr Gold, IntPtr ChangesBar, IntPtr Changes, IntPtr CostRow, IntPtr Cost, long At)
        {
            /// <summary>Gets the chooser of points, its box and its label (<see cref="FindChooser"/>).</summary>
            public IntPtr Chooser { get; init; }

            public IntPtr ChooserBox { get; init; }

            public IntPtr ChooserLabel { get; init; }

            public static TreeLabels None { get; } = new(
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Array.Empty<(IntPtr Row, IntPtr Value)>(),
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0);
        }
    }
}
