namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using GameReader.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>Pairing of the walked nodes of <see cref="ScreenWatcher"/>: glyphs, units and weapon-set icons with their texts, and merging rows.</summary>
    public sealed partial class ScreenWatcher
    {
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
