namespace Poe2Accessibility
{
    using System;
    using System.Linq;

    /// <summary>The focused element and its tooltip: role, state, position, description, keyword links and combo changes.</summary>
    public static partial class Announcements
    {
        /// <summary>Menu rule: name, then state/value, then role. Value-bearing controls read "label, value, role".</summary>
        /// <param name="fullTooltip">Also read a long tooltip (item mods, gem stats) after the position.</param>
        public static string Focused(Models.AccessibleUiNode node, bool fullTooltip = false)
        {
            if (node.Role == Models.UiRole.Link)
            {
                return Link(node);
            }

            var role = node.Role switch
            {
                Models.UiRole.Button => T("button", "botón"),
                Models.UiRole.ComboBox => T("combo box", "lista desplegable"),
                Models.UiRole.TextField => T("edit field", "campo de texto"),
                Models.UiRole.CheckBox => T("check box", "casilla de verificación"),
                Models.UiRole.Slider => T("slider", "control deslizante"),
                Models.UiRole.Tab => T("tab", "pestaña"),
                Models.UiRole.Link => T("link", "enlace"),
                _ => string.Empty,
            };
            var parts = new System.Collections.Generic.List<string>(3);
            if (node.Text.Length > 0)
            {
                parts.Add(Capitalised(node.Text));
            }

            if (node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            if (role.Length > 0 && !node.IsHovered)
            {
                parts.Add(role);
            }

            if (node.OpensWithA)
            {
                parts.Add(TypeWithA);
            }

            if (node.IsCollapsible || (node.Role == Models.UiRole.ComboBox && node.IsExpanded))
            {
                parts.Add(node.IsExpanded ? Expanded : Collapsed);
            }

            if (!node.IsEnabled)
            {
                parts.Add(UnavailableState);
            }

            var tooltip = Unsaid(node.Tooltip, node.Text);

            // Name, state, place in the list, then what it means (D2R order): the position never trails a description.
            if (node.Position.Length > 0)
            {
                if (parts.Count == 0 && tooltip.Count == 0)
                {
                    parts.Add(Capitalised(Empty)); // a row with nothing in it (an unused skill slot) was heard as a bare "5 de 13"
                }

                parts.Add(node.Position);
            }

            if ((node.IsHovered || node.Role == Models.UiRole.Link) && tooltip.Count > 0)
            {
                parts.Add(Sentences(tooltip));
            }
            else if (IsShortTooltip(tooltip))
            {
                parts.Add(Sentences(tooltip));
            }

            if (fullTooltip && !node.IsHovered && tooltip.Count > 0 && !IsShortTooltip(tooltip))
            {
                parts.Add(Sentences(tooltip));
            }

            return parts.Count == 0 ? UnnamedElement : Clauses(parts);
        }

        public static string UnnamedElement => T("Unnamed element", "Elemento sin nombre");

        /// <summary>The icon button that empties a search box (the game's own hint for it: "Borrar búsqueda").</summary>
        public static string ClearSearch => T("Clear search", "Borrar búsqueda");

        /// <summary>The shoulder buttons drawn at the two ends of a tab bar (Options): what each does, not the tab beside it.</summary>
        public static string PreviousTab => T("Previous tab", "Pestaña anterior");

        public static string NextTab => T("Next tab", "Pestaña siguiente");

        /// <summary>A collapsible row shown open (a Challenges category after A).</summary>
        public static string Expanded => T("expanded", "expandido");

        public static string Collapsed => T("collapsed", "contraído");

        /// <summary>A hint whose button is held, not pressed: "Comprobar precio, mantener Y".</summary>
        public static string HoldButton(string button) => T($"hold {button}", $"mantener {button}");

        /// <summary>
        ///     A tooltip without its title when the label already said it: a Skills row "NIVEL: 1, Rayo atronador, DPS: 12,5"
        ///     read the skill's name again as the tooltip's first line (2026-10-25).
        /// </summary>
        private static System.Collections.Generic.IReadOnlyList<string> Unsaid(System.Collections.Generic.IReadOnlyList<string> tooltip, string said)
        {
            if (tooltip.Count == 0 || said.Length == 0)
            {
                return tooltip;
            }

            var title = tooltip[0].Trim();
            var clauses = said.Split(", ");
            return clauses.Any(clause => clause.Trim().Equals(title, StringComparison.OrdinalIgnoreCase)) ? tooltip.Skip(1).ToList() : tooltip;
        }

        /// <summary>"Evasion Rating, 7. Evasion, link. Evasion Rating grants a chance …": the line, the keyword, its definition.</summary>
        /// <summary>A keyword link: the text holding it (unless just said for the previous link), the keyword, its definition.</summary>
        public static string Link(Models.AccessibleUiNode node, bool withRow = true)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (withRow && node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            parts.Add(T($"{node.Text}, link", $"{node.Text}, enlace"));
            parts.AddRange(node.Tooltip);
            return Sentences(parts);
        }

        public static string ListPosition(int oneBasedIndex, int count) => T($"{oneBasedIndex} of {count}", $"{oneBasedIndex} de {count}");

        /// <summary>A combo opened in place: the D-pad now steps its value.</summary>
        public static string ComboOpened(string valueAndPosition) => T($"Choosing: {valueAndPosition}", $"Eligiendo: {valueAndPosition}");

        /// <summary>A combo closed on its value.</summary>
        public static string ComboClosed(string value) => T($"Chosen: {value}", $"Elegido: {value}");

        /// <summary>A changed value with its place among the choices: "Adaptativo (experimental), 3 de 4".</summary>
        public static string ValueAt(string value, string position) => position.Length == 0 ? value : $"{value}, {position}";

        /// <summary>One table row: "Maximum Life, 42"; a caption ending in ':' runs into its value ("LVL: 1").</summary>
        public static string LabelValue(string label, string value) => label.EndsWith(':') ? $"{label} {value}" : $"{label}, {value}";

        /// <summary>Tooltips up to this size are read with the focus (D2R: name, state, tooltip, position); longer ones on request.</summary>
        private const int MaxAutoTooltipLines = 2;

        private const int MaxAutoTooltipChars = 200;

        public static string NoDetails => T("No details.", "Sin detalles.");

        public static bool IsShortTooltip(System.Collections.Generic.IReadOnlyList<string> tooltip) =>
            tooltip.Count is > 0 and <= MaxAutoTooltipLines && tooltip.Sum(line => line.Length) <= MaxAutoTooltipChars;

        /// <summary>The focused element's full tooltip (skill gems, rich descriptions).</summary>
        public static string Details(Models.AccessibleUiNode node) =>
            (node.Details ?? node.Tooltip) is { Count: > 0 } lines ? Sentences(lines) : NoDetails;

        /// <summary>Entering a panel: its title before the focused item.</summary>
        public static string InContext(string context, string focused) =>
            context.Length == 0 ? focused : focused.Length == 0 ? context : Sentences(new[] { context, focused });
    }
}
