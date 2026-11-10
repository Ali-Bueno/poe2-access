namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Says what a stash's or a merchant's search field lights ("Resaltar objetos"): the game dims the rest, which only
    ///     eyes can tell (docs/research/ui-layers.md, search highlight).
    /// </summary>
    public sealed class SearchHighlightWatch
    {
        private const long PollMs = 300;

        /// <summary>Grids kept in view at once: a stash page and the inventory beside it, with room for a merchant's.</summary>
        private const int MaxGrids = 4;

        /// <summary>How far a field's grid is looked for: up to the window that holds both, then down to the page shown.</summary>
        private const int MaxClimb = 6;
        private const int MaxDepth = 8;
        private const int MaxNodes = 2000;

        private readonly UiElementReader elements;
        private readonly InventoryReader inventory;
        private readonly Action<string> speak;
        private readonly SearchHighlightNews news = new();
        private readonly List<IntPtr> grids = new();
        private IntPtr searchedFrom;
        private long lastPoll;

        public SearchHighlightWatch(UiElementReader elements, InventoryReader inventory, Action<string> speak)
        {
            this.elements = elements;
            this.inventory = inventory;
            this.speak = speak;
        }

        /// <summary>Per frame in game, with the focused control (null: none).</summary>
        public void Tick(AccessibleUiNode? focus)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (focus is { Role: UiRole.GridCell })
            {
                this.Keep(new[] { focus.Address });
            }

            if (focus is { Role: UiRole.TextField } && focus.Address != this.searchedFrom)
            {
                // The cursor may never have been in the grid the field searches (it opens on the inventory's side).
                this.searchedFrom = focus.Address;
                this.Keep(this.GridsBeside(focus.Address));
            }

            // A page turned (another tab of the stash or the merchant): the grid shown in its place is the searched one now.
            var hidden = this.grids.Where(grid => !this.elements.TryReadElement(grid, out var info) || !this.elements.IsVisible(info)).ToList();
            this.grids.RemoveAll(hidden.Contains);
            foreach (var grid in hidden)
            {
                this.Keep(this.GridsBeside(grid));
            }

            if (this.grids.Count == 0)
            {
                this.news.Forget();
                this.searchedFrom = IntPtr.Zero;
                return;
            }

            if (focus is { Role: UiRole.TextField })
            {
                return; // while the search is typed its echo is what is heard
            }


            var searching = false;
            var matches = new List<(string Name, int Column, int Row)>();
            foreach (var grid in this.grids)
            {
                foreach (var (item, column, row) in this.inventory.ItemsOf(grid))
                {
                    var highlight = this.inventory.HighlightOf(item);
                    searching |= highlight != ItemHighlight.None;
                    if (highlight == ItemHighlight.Match)
                    {
                        matches.Add((this.inventory.DescribeItem(item) ?? Announcements.UnknownItem, column, row));
                    }
                }
            }

            if (this.news.Line(searching ? matches : null) is { } line)
            {
                this.speak(line);
            }
        }

        private void Keep(IEnumerable<IntPtr> found)
        {
            foreach (var grid in found.Where(grid => !this.grids.Contains(grid)))
            {
                this.grids.Insert(0, grid);
            }

            if (this.grids.Count > MaxGrids)
            {
                this.grids.RemoveRange(MaxGrids, this.grids.Count - MaxGrids);
            }
        }

        /// <summary>The grids with items in the nearest part of the window that holds any, climbing from the search field.</summary>
        private List<IntPtr> GridsBeside(IntPtr field)
        {
            var found = new List<IntPtr>();
            var budget = MaxNodes;
            var at = field;
            for (var up = 0; up < MaxClimb && found.Count == 0 && this.elements.TryReadElement(at, out var info); up++)
            {
                this.GridsUnder(info, MaxDepth, ref budget, found);
                at = info.Raw.ParentPtr;
            }

            return found;
        }

        private void GridsUnder(in UiElementReader.ElementInfo info, int depth, ref int budget, List<IntPtr> found)
        {
            if (budget-- <= 0 || !info.IsVisibleFlag)
            {
                return;
            }

            if (this.inventory.ItemsOf(info.Address).Count > 0)
            {
                if (this.elements.IsVisible(info))
                {
                    found.Add(info.Address); // a page not shown keeps its own flag: only what is drawn counts
                }

                return;
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var child in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(child, out var childInfo))
                {
                    this.GridsUnder(childInfo, depth - 1, ref budget, found);
                }
            }
        }
    }
}
