namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>The points left to spend on the passive tree and on the ascendancy's, for <see cref="UiSemantics"/>: read from the tree's own labels.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The tree's own layout of its points left, and the keyword the ascendancy's label is written with ("[AscendancyPoints|…]:").</summary>
        private const string PointsLayoutName = "top_points_layout_gmp";

        private const string AscendancyPointsKey = "AscendancyPoints";

        /// <summary>The points' labels are looked for again this often while they are not found.</summary>
        private const int PointLabelsMs = 1000;

        private (IntPtr Canvas, IntPtr Skill, IntPtr AscendancyRow, IntPtr Ascendancy, long At) pointLabels;

        /// <summary>
        ///     The points left to spend on the class's tree and on the ascendancy's, as the tree's labels show them
        ///     (docs/research/passive-tree.md#the-ascendancys-tree); the ascendancy's are none while its row is not shown.
        ///     Null where the tree has no such labels (the altar's preview) or they do not read.
        /// </summary>
        private (int? Skill, int? Ascendancy) PointsLeft(PassiveGraph graph)
        {
            var now = Environment.TickCount64;
            var labels = this.pointLabels;
            if (labels.Canvas != graph.Canvas || (labels.Skill == IntPtr.Zero && now - labels.At >= PointLabelsMs))
            {
                this.pointLabels = labels = this.FindPointLabels(graph.Canvas, now);
            }

            if (labels.Skill == IntPtr.Zero || !this.elements.TryReadText(labels.Skill, out var skillText) || !int.TryParse(skillText.Trim(), out var skill))
            {
                this.pointLabels = labels with { Skill = IntPtr.Zero }; // labels made again by the game are found again
                return (null, null);
            }

            var ascendancy = this.elements.TryReadElement(labels.AscendancyRow, out var row) && row.IsVisibleFlag &&
                             this.elements.TryReadText(labels.Ascendancy, out var text) && int.TryParse(text.Trim(), out var read) ? read : 0;
            return (skill, ascendancy);
        }

        /// <summary>The value labels of the points layout beside the canvas: the first row's is the tree's, the ascendancy's is known by its label's keyword.</summary>
        private (IntPtr Canvas, IntPtr Skill, IntPtr AscendancyRow, IntPtr Ascendancy, long At) FindPointLabels(IntPtr canvasAddress, long now)
        {
            var found = (Canvas: canvasAddress, Skill: IntPtr.Zero, AscendancyRow: IntPtr.Zero, Ascendancy: IntPtr.Zero, At: now);
            if (!this.elements.TryReadElement(canvasAddress, out var canvas) || !this.elements.TryReadElement(canvas.Raw.ParentPtr, out var panel))
            {
                return found;
            }

            var layout = this.elements.ReadChildren(panel).Where(part => part != canvasAddress)
                .Select(part => this.elements.FindNamed(part, PointsLayoutName, 1)).FirstOrDefault(part => part != null);
            if (layout is not { } rows)
            {
                return found;
            }

            foreach (var (rowAddress, index) in this.elements.ReadChildren(rows).Select((row, index) => (row, index)))
            {
                if (!this.elements.TryReadElement(rowAddress, out var row) || this.elements.ReadChildren(row) is not { Length: >= 2 } parts ||
                    !this.elements.TryReadText(parts[^1], out var value) || !int.TryParse(value.Trim(), out _))
                {
                    continue; // a weapon set's row keeps its two counts a level deeper
                }

                if (this.elements.TryReadText(parts[^2], out _, out var tagged) && tagged.Contains(AscendancyPointsKey, StringComparison.Ordinal))
                {
                    (found.AscendancyRow, found.Ascendancy) = (rowAddress, parts[^1]);
                }
                else if (index == 0)
                {
                    found.Skill = parts[^1];
                }
            }

            return found;
        }
    }
}
