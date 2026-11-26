namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>Pure: the encounter tracker's labels as lines, a label's buttons joined to what they do.</summary>
    public static class EncounterLines
    {
        /// <summary>A button picture pairs with a label at most this many levels up from it to their shared parent (the screen reader's rule).</summary>
        public const int GlyphPairHops = 3;

        private const string Plus = "+";

        /// <summary>
        ///     One visible element of the tracker in walk order: its parent's place in the list (-1 under the column), its depth,
        ///     its text and tagged text (empty for a picture) and the button its picture shows (empty when none).
        /// </summary>
        public readonly record struct Node(int Parent, int Depth, string Text, string Tagged, string Glyph);

        /// <summary>
        ///     The tracker's lines from its elements: a button picture goes with the label nearest it in the tree, and two pictures
        ///     with a bare "+" between them are one chord ("Colocar explosivos, LT más cruceta arriba").
        /// </summary>
        public static List<string> Build(IReadOnlyList<Node> nodes)
        {
            var labels = new List<int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Text.Trim().Length > 0 && !IsBarePlus(nodes[i]))
                {
                    labels.Add(i);
                }
            }

            var buttons = new Dictionary<int, (string Spoken, int LastGlyph)>();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Glyph.Length == 0 || Nearest(nodes, labels, i) is not { } label)
                {
                    continue;
                }

                if (buttons.TryGetValue(label, out var known))
                {
                    var joiner = PlusBetween(nodes, known.LastGlyph, i) ? Announcements.ButtonCombination : Announcements.ButtonSeparator;
                    buttons[label] = (known.Spoken + joiner + nodes[i].Glyph, i);
                }
                else
                {
                    buttons[label] = (nodes[i].Glyph, i);
                }
            }

            var lines = new List<string>();
            foreach (var label in labels)
            {
                Add(lines, nodes[label].Text, nodes[label].Tagged, buttons.TryGetValue(label, out var pictured) ? pictured.Spoken : string.Empty);
            }

            return lines;
        }

        /// <summary>
        ///     Adds a label to the lines; one holding only buttons ("LT + D-pad up" under "Regresar al barco") goes with the line before
        ///     it. <paramref name="pictured"/> is the buttons drawn as pictures beside it.
        /// </summary>
        public static void Add(List<string> lines, string text, string tagged, string pictured = "")
        {
            var markup = ControllerGlyphs.OnlyLeadingButtons(tagged) ? ControllerGlyphs.FromMarkup(tagged) : string.Empty;
            var button = markup.Length == 0 ? pictured : pictured.Length == 0 ? markup : markup + Announcements.ButtonSeparator + pictured;
            var label = markup.Length > 0 ? text.Trim().Trim('+').Trim() : text.Trim();
            if (button.Length > 0 && label.Length == 0)
            {
                label = lines.Count > 0 ? lines[^1] : string.Empty;
            }

            if (label.Length == 0)
            {
                return;
            }

            var line = Announcements.WithButton(label, button);
            var known = lines.FindIndex(said => said == label || said == line || said.StartsWith(label + ", ", StringComparison.Ordinal));
            if (known < 0)
            {
                lines.Add(line);
            }
            else if (button.Length > 0 && lines[known] == label)
            {
                lines[known] = line;
            }
        }

        /// <summary>A "+" with no buttons of its own: the joiner drawn between two button pictures.</summary>
        private static bool IsBarePlus(Node node) => node.Text.Trim() == Plus && ControllerGlyphs.FromMarkup(node.Tagged).Length == 0;

        private static bool PlusBetween(IReadOnlyList<Node> nodes, int from, int to)
        {
            for (var i = from + 1; i < to; i++)
            {
                if (IsBarePlus(nodes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The label fewest levels away in the tree, then nearest in walk order; null past <see cref="GlyphPairHops"/>.</summary>
        private static int? Nearest(IReadOnlyList<Node> nodes, List<int> labels, int glyph)
        {
            int? best = null;
            var (bestHops, bestGap) = (int.MaxValue, int.MaxValue);
            foreach (var label in labels)
            {
                var hops = nodes[glyph].Depth - CommonDepth(nodes, glyph, label);
                var gap = Math.Abs(label - glyph);
                if (hops <= GlyphPairHops && (hops < bestHops || (hops == bestHops && gap < bestGap)))
                {
                    (best, bestHops, bestGap) = (label, hops, gap);
                }
            }

            return best;
        }

        /// <summary>The depth of the deepest element both sit under; -1 when that is the column itself.</summary>
        private static int CommonDepth(IReadOnlyList<Node> nodes, int a, int b)
        {
            var above = new HashSet<int>();
            for (var at = nodes[a].Parent; at >= 0; at = nodes[at].Parent)
            {
                above.Add(at);
            }

            for (var at = nodes[b].Parent; at >= 0; at = nodes[at].Parent)
            {
                if (above.Contains(at))
                {
                    return nodes[at].Depth;
                }
            }

            return -1;
        }
    }
}
