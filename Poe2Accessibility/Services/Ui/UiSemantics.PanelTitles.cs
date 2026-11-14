namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Panel titles and notes of <see cref="UiSemantics"/>: the title an ancestor panel carries and the lines a panel shows besides the focus.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The world map's title label lies two levels under "WorldScreen" (<see cref="GameLayout.InGameUi.WorldMapTitleName"/>).</summary>
        private const int WorldMapTitleDepth = 2;

        private const int MaxPanelTitleLength = 40;

        /// <summary>Most lines a panel shows outside the focused part to be read as its notes; more is a panel of labels.</summary>
        private const int MaxPanelNotes = 3;

        /// <summary>Depth and elements of a panel looked through for its notes (Skills: the spirit line sits 2 below the panel).</summary>
        private const int PanelNoteDepth = 6;

        private const int MaxPanelNoteNodes = 600;

        /// <summary>A label and its control, alone in their holder (a check box with its caption beside it).</summary>
        private const int ControlCaptionPair = 2;

        /// <summary>The notes of the last panel, kept a moment: the focus is described every few steps within one panel.</summary>
        private const int PanelNotesKeepMs = 1000;

        private (IntPtr Panel, IReadOnlyList<string> Notes, long At) panelNotes = (IntPtr.Zero, Array.Empty<string>(), 0);

        /// <summary>Whether an ancestor panel carries this title (a social page under the Challenges overlay does not).</summary>
        public bool IsInPanel(IntPtr address, string title)
        {
            if (!this.elements.TryReadElement(address, out var info))
            {
                return false;
            }

            var shift = this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                if (this.elements.ReadWStringStructAt(ancestor.Address + GameLayout.Element.PanelTitleOffset + shift)?.Trim() == title)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Title of the nearest ancestor panel (GameLayout.Element.PanelTitleOffset), judged only on a focus inside the
        ///     inventory's panels, each of which has one: the field holds where an ancestor names the panel (a shift found by
        ///     search waits for a second focus to agree).
        /// </summary>
        private string PanelTitle(in UiElementReader.ElementInfo info)
        {
            var chain = this.elements.ReadAncestors(info, PanelTitleHops);
            if (chain.Any(ancestor => ancestor.Name == GameLayout.InGameUi.ShopPageName))
            {
                // The shop's pages have no title: judged there, the search took a child's name ("background") for one (live 2026-11-10).
                return string.Empty;
            }

            var map = chain.FirstOrDefault(ancestor => ancestor.Name == WorldScreenName);
            if (map.Address != IntPtr.Zero && this.ShownText(map, GameLayout.InGameUi.WorldMapTitleName, WorldMapTitleDepth) is { Length: > 0 } shownTitle)
            {
                // The map names itself in a label; its panel is titled "Misiones" whatever it shows (the Teleport map's act tabs).
                return shownTitle;
            }

            var inPanels = chain.Any(ancestor => ancestor.Name == GameLayout.InGameUi.PanelsParentName);
            // Inside the side panels only the panels themselves carry titles: the root above them holds "HUD" within the
            // search's reach, which the untitled trade market adopted as every panel's title (2026-10-28).
            var ancestors = (inPanels ? chain.TakeWhile(ancestor => ancestor.Name != GameLayout.InGameUi.PanelsParentName) : chain)
                .Select(ancestor => ancestor.Address).ToList();
            if (ancestors.Count == 0)
            {
                return string.Empty;
            }

            var shift = inPanels
                ? this.elements.Controls?.Shift(ControlGroup.PanelTitle, info.Address, at => ancestors.Any(ancestor => this.TitleAt(ancestor, at).Length > 0)) ?? 0
                : this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            var title = ancestors.Select(ancestor => this.TitleAt(ancestor, shift)).FirstOrDefault(title => title.Length > 0) ?? string.Empty;
            // An untitled panel beside the inventory (the group under panels_parent holds it): the object it was opened at.
            return title.Length == 0 && inPanels && ancestors.Count >= SidePanelHops ? this.OpenedAt(ancestors[^SidePanelHops]) : title;
        }

        /// <summary>From panels_parent down to a side panel: its group, then the panel.</summary>
        private const int SidePanelHops = 2;

        /// <summary>
        ///     The few lines a titled panel shows besides what the cursor lands on, in screen order (Skills: "0 de
        ///     espíritu" over the list, the gem capacity under it; Character: the class over the stat tiles). Texts inside
        ///     navigable or interactive elements are read when focused, and a part of the panel holding more than a
        ///     handful (the stat list) is a list, not a note; empty when none are left.
        /// </summary>
        private IReadOnlyList<string> PanelNotes(in UiElementReader.ElementInfo info)
        {
            var shift = this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                var title = this.TitleAt(ancestor.Address, shift);
                if (title.Length > 0)
                {
                    return this.NotesOf(ancestor, title);
                }
            }

            return Array.Empty<string>();
        }

        private IReadOnlyList<string> NotesOf(in UiElementReader.ElementInfo panel, string title)
        {
            var now = Environment.TickCount64;
            if (this.panelNotes.Panel == panel.Address && now - this.panelNotes.At < PanelNotesKeepMs)
            {
                return this.panelNotes.Notes;
            }

            var lines = new List<(float Y, float X, string Text)>();
            var budget = MaxPanelNoteNodes;
            foreach (var childAddress in this.elements.ReadChildren(panel))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                var part = new List<(float Y, float X, string Text)>();
                this.CollectNotes(child, 0, 0, PanelNoteDepth, ref budget, part);
                if (part.Count <= MaxPanelNotes)
                {
                    lines.AddRange(part);
                }
            }

            var texts = lines.Where(line => line.Text != title).OrderBy(line => line.Y).ThenBy(line => line.X).Select(line => line.Text).Distinct().ToList();
            IReadOnlyList<string> notes = texts.Count <= MaxPanelNotes ? texts : Array.Empty<string>();
            this.panelNotes = (panel.Address, notes, now);
            return notes;
        }

        /// <summary>Texts of <paramref name="node"/> and below, placed relative to the panel, short of what the focus lands on.</summary>
        private void CollectNotes(in UiElementReader.ElementInfo node, float x, float y, int depth, ref int budget, List<(float Y, float X, string Text)> found)
        {
            if (budget-- <= 0 || node.HasFlagBit(GameLayout.Element.NavigableFlagBit) || node.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                return;
            }

            var children = this.elements.ReadChildren(node);
            if (children.Length == ControlCaptionPair && children.Any(this.IsFocusable))
            {
                return; // a caption beside its control (Friends' "Mostrar desconectados" and its check box)
            }

            var nodeX = x + node.Raw.RelativePosition.X;
            var nodeY = y + node.Raw.RelativePosition.Y;
            if (this.elements.TryReadSpokenText(node.Address, out var text) && text.Any(char.IsLetter))
            {
                // A bare number is a value drawn beside its tile ("11" over DES), read with the tile.
                found.Add((nodeY, nodeX, text));
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var childAddress in children)
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    this.CollectNotes(child, nodeX, nodeY, depth - 1, ref budget, found);
                }
            }
        }

        /// <summary>Whether the controller cursor lands on the element (in game: navigable; before it: interactive).</summary>
        private bool IsFocusable(IntPtr address) =>
            this.elements.TryReadElement(address, out var element) &&
            (element.HasFlagBit(GameLayout.Element.NavigableFlagBit) || element.HasFlagBit(GameLayout.Element.InteractiveFlagBit));

        /// <summary>
        ///     Font faces the UI keeps in the same kind of field (UiTreeReader's font column): the shop's items_root holds
        ///     "Fontin" where a panel keeps its title, said before the first item (2026-10-29).
        /// </summary>
        private static readonly string[] FontFaces = { "Fontin", "Fontin Smallcaps", "FontinSmallcaps", "OptimusPrincepsSemiBold" };

        /// <summary>A panel title read with the field moved by a shift: a short name with letters, no path, no layout id, no font.</summary>
        private string TitleAt(IntPtr element, int shift) =>
            this.elements.ReadWStringStructAt(element + GameLayout.Element.PanelTitleOffset + shift)?.Trim() is { Length: > 0 and <= MaxPanelTitleLength } title &&
            !title.Contains('/') && !title.Contains('_') && title.Any(char.IsLetter) &&
            !FontFaces.Contains(title, StringComparer.OrdinalIgnoreCase) ? title : string.Empty; // "bar_area": a layout id, not a title
    }
}
