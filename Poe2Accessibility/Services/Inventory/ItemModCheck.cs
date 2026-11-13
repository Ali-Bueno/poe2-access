namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>An item's lines made from memory set against its tooltip's.</summary>
    /// <param name="Equal">Certain lines the tooltip shows letter for letter.</param>
    /// <param name="MemoryOnly">Certain lines the tooltip does not show.</param>
    /// <param name="TooltipOnly">Tooltip lines that read as a modifier and no line made from memory equals.</param>
    /// <param name="Uncertain">The lines in doubt, each with whether the tooltip shows it as written.</param>
    public sealed record ItemModComparison(int Equal, IReadOnlyList<string> MemoryOnly, IReadOnlyList<string> TooltipOnly, IReadOnlyList<(ItemModLine Line, bool Shown)> Uncertain)
    {
        public bool Differs => this.MemoryOnly.Count + this.TooltipOnly.Count > 0;

        /// <summary>"5 lines equal, 0 differ, 1 uncertain".</summary>
        public string Counts => $"{this.Equal} lines equal, {this.MemoryOnly.Count + this.TooltipOnly.Count} differ, {this.Uncertain.Count} uncertain";

        /// <summary>What differs and what is in doubt, for the log; empty when neither.</summary>
        public string Details
        {
            get
            {
                var parts = new List<string>();
                if (this.MemoryOnly.Count > 0)
                {
                    parts.Add("memory only: " + string.Join(" | ", this.MemoryOnly));
                }

                if (this.TooltipOnly.Count > 0)
                {
                    parts.Add("tooltip only: " + string.Join(" | ", this.TooltipOnly));
                }

                if (this.Uncertain.Count > 0)
                {
                    parts.Add("uncertain: " + string.Join(" | ", this.Uncertain.Select(doubt =>
                        $"{(doubt.Line.Text.Length > 0 ? $"\"{doubt.Line.Text}\" " : string.Empty)}({doubt.Line.From}){(doubt.Shown ? " as the tooltip shows it" : string.Empty)}")));
                }

                return string.Join("; ", parts);
            }
        }
    }

    /// <summary>The comparison that tells whether the lines made from memory can stand in for a tooltip (docs/research/item-mods.md, "Live check"). Pure.</summary>
    public static class ItemModCheck
    {
        /// <summary>What the tooltip reader puts between the lines of one text (<c>Announcements.JoinLines</c>): the belt's two implicit lines came as one.</summary>
        private static readonly string[] Joints = { ". ", " " };

        /// <summary>The range the game writes after a rolled value while its advanced descriptions are on: "+15(10-19)".</summary>
        private static readonly Regex RolledRange = new(@"(?<=\d)\(-?\d+(?:[.,]\d+)?[-–]-?\d+(?:[.,]\d+)?\)", RegexOptions.Compiled);

        public static ItemModComparison Compare(IReadOnlyList<ItemModLine> memory, IReadOnlyList<string> tooltip, IModTexts texts)
        {
            var shown = new bool[memory.Count];
            var tooltipOnly = new List<string>();
            foreach (var line in tooltip.Select(line => RolledRange.Replace(line, string.Empty)))
            {
                if (Parts(line, memory, shown) is { } parts)
                {
                    parts.ForEach(part => shown[part] = true);
                }
                else if (texts.Parse(line) != null)
                {
                    tooltipOnly.Add(line);
                }
            }

            var certain = Enumerable.Range(0, memory.Count).Where(at => memory[at].Certain).ToList();
            return new ItemModComparison(certain.Count(at => shown[at]), certain.Where(at => !shown[at]).Select(at => memory[at].Text).ToList(), tooltipOnly,
                Enumerable.Range(0, memory.Count).Where(at => !memory[at].Certain).Select(at => (memory[at], shown[at])).ToList());
        }

        /// <summary>The lines made from memory that a tooltip line is, one or several joined; null when it is not made of them.</summary>
        private static List<int>? Parts(string line, IReadOnlyList<ItemModLine> memory, bool[] taken)
        {
            // A line not yet matched first: two lists may write the same line.
            foreach (var at in Enumerable.Range(0, memory.Count).Where(at => memory[at].Text.Length > 0).OrderBy(at => taken[at]))
            {
                var text = memory[at].Text;
                if (line == text)
                {
                    return new List<int> { at };
                }

                if (!line.StartsWith(text, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var joint in Joints.Where(joint => line.AsSpan(text.Length).StartsWith(joint, StringComparison.Ordinal)))
                {
                    if (Parts(line[(text.Length + joint.Length)..], memory, taken) is { } rest)
                    {
                        rest.Insert(0, at);
                        return rest;
                    }
                }
            }

            return null;
        }
    }
}
