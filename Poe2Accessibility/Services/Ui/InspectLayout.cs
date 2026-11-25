namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>One element of an Inspect Mode tooltip as read: its name, its plain text (+0x490), its tagged text (+0x360), its children.</summary>
    public sealed record InspectNode(string Name, string Text, string Tagged, IReadOnlyList<InspectNode> Children);

    /// <summary>
    ///     An Inspect Mode tooltip's rows, by the element names the game gives them in every language
    ///     (docs/research/ui-layers.md, "Item Inspect Mode"): the title in child 0, the rows under `layout`. A modifier group
    ///     gives one row a line, said with its tier and the group's label; the price, the buttons and a worn tooltip's
    ///     "Equipped" line are no rows. Pure.
    /// </summary>
    public static class InspectLayout
    {
        /// <summary>The element holding a tooltip's rows.</summary>
        public const string LayoutName = "layout";

        /// <summary>The end of a modifier group's name: implicit-, prefix- and suffix-mods-with-reminders (advanced info on).</summary>
        private const string ModGroupSuffix = "-mods-with-reminders";

        /// <summary>The modifier lines with advanced info off, and "Unidentified" either way: one text a line.</summary>
        private const string PlainModsName = "explicit-stats";

        /// <summary>Rows that are no line of the item: its price or sell value, and its buttons.</summary>
        private static readonly HashSet<string> NoRows = new(StringComparer.Ordinal) { "cost", "gamepad_popup_button_legend_layout" };

        /// <summary>Whether a row element is a modifier group, one line a child and the group's label last.</summary>
        public static bool IsModGroup(string name) => name.EndsWith(ModGroupSuffix, StringComparison.Ordinal);

        /// <summary>
        ///     A tooltip's title (name and base joined) and lines, the title row first; null when the node holds no
        ///     <see cref="LayoutName"/>. On a worn tooltip the last text row is the game's "Equipped" line and is left out.
        /// </summary>
        public static (string Title, IReadOnlyList<InspectLine> Lines)? Read(InspectNode tooltip, bool worn)
        {
            var layout = tooltip.Children.FirstOrDefault(child => child.Name == LayoutName);
            if (layout == null)
            {
                return null;
            }

            var header = tooltip.Children[0] == layout ? string.Empty : TextOf(tooltip.Children[0], keepBreaks: true);
            var title = string.Join(", ", header.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            var lines = new List<InspectLine> { new(title, title) };
            var lastText = -1;
            foreach (var row in layout.Children)
            {
                if (NoRows.Contains(row.Name))
                {
                    continue;
                }

                if (IsModGroup(row.Name))
                {
                    lines.AddRange(GroupLines(row));
                }
                else if (row.Name == PlainModsName)
                {
                    lines.AddRange(row.Children.Select(child => TextOf(child)).Where(text => text.Length > 0).Select(text => new InspectLine(text, text)));
                }
                else if (TextOf(row) is { Length: > 0 } text)
                {
                    var unmet = UnmetRequirements.Of(row.Tagged);
                    lines.Add(new InspectLine(unmet.Count > 0 ? $"{text}. {Announcements.Unmet(string.Join(", ", unmet))}" : text, text));
                    lastText = lines.Count - 1;
                }
            }

            if (worn && lastText == lines.Count - 1 && lastText > 0)
            {
                lines.RemoveAt(lastText);
            }

            return (title, lines);
        }

        /// <summary>
        ///     The slot of each worn tooltip, in the tooltips' order: the worn item whose base name the title holds (a magic
        ///     item's name is made around its base, a rare's or a unique's title shows it on its second line, and memory gives
        ///     the base name for sure where it does not the magic name). Two alike take the slots in order. Null for a tooltip
        ///     no worn item fits.
        /// </summary>
        public static IReadOnlyList<string?> MatchSlots(IReadOnlyList<string> tooltipTitles, IReadOnlyList<(string Slot, string BaseName, string Title)> worn)
        {
            // First the title a tooltip of the worn item gave it this session (two rings of one base told apart), then the base.
            var taken = new bool[worn.Count];
            var slots = new string?[tooltipTitles.Count];
            foreach (var byTitle in new[] { true, false })
            {
                for (var tooltip = 0; tooltip < tooltipTitles.Count; tooltip++)
                {
                    var title = tooltipTitles[tooltip];
                    var index = slots[tooltip] != null ? -1 : Enumerable.Range(0, worn.Count).FirstOrDefault(
                        i => !taken[i] && (byTitle
                            ? worn[i].Title.Length > 0 && title.StartsWith(worn[i].Title, StringComparison.Ordinal)
                            : worn[i].BaseName.Length > 0 && title.Contains(worn[i].BaseName, StringComparison.Ordinal)),
                        -1);
                    if (index >= 0)
                    {
                        (taken[index], slots[tooltip]) = (true, worn[index].Slot);
                    }
                }
            }

            return slots;
        }

        /// <summary>A modifier group's lines: each line element but the last, whose text is the label ("Prefix", "Unique").</summary>
        private static IEnumerable<InspectLine> GroupLines(InspectNode group)
        {
            if (group.Children.Count < 2)
            {
                yield break;
            }

            var label = TextOf(group.Children[^1]);
            foreach (var line in group.Children.Take(group.Children.Count - 1))
            {
                var text = line.Children.Count > 0 ? TextOf(line.Children[0]) : TextOf(line);
                if (text.Length == 0)
                {
                    continue;
                }

                var tier = line.Children.Count > 1 ? TextOf(line.Children[1]) : string.Empty;
                yield return new InspectLine(string.Join(", ", new[] { text, tier, label }.Where(part => part.Length > 0)), text);
            }
        }

        /// <summary>An element's text, else its descendants' texts joined (a story line one level down); empty for none.</summary>
        private static string TextOf(InspectNode node, bool keepBreaks = false)
        {
            var text = node.Text.Trim();
            if (text.Length > 0)
            {
                return keepBreaks ? text : text.Replace('\n', ' ');
            }

            return string.Join(", ", node.Children.Select(child => TextOf(child, keepBreaks)).Where(part => part.Length > 0));
        }
    }
}
