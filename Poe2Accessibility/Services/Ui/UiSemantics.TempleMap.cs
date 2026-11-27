namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The Temple Console's map of Atziri's Temple: each cell's row, column and neighbours (<see cref="TempleGrid"/>).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>How long a read of the map's cells is kept: the cursor moves cell to cell, the cells stay the same elements.</summary>
        private const int TempleCellsKeptMs = 1500;

        private (IntPtr Holder, long At, Dictionary<(int Row, int Column), IntPtr> Cells)? templeCells;

        /// <summary>
        ///     The place of a cell of the Temple Console's map, with what stands beside it; null for any other element. The cells
        ///     are named "(row, column)" by the game and sit 81 to a container, Atziri's Chamber alone in another beside it
        ///     (dump 2026-11-27 11:50): the list position "43 de 81" said nothing of where a room is.
        /// </summary>
        private string? TempleCellPlace(in UiElementReader.ElementInfo cell)
        {
            if (TempleGrid.Parse(cell.Name) is not { } at)
            {
                return null;
            }

            var ancestors = this.elements.ReadAncestors(cell, 2);
            if (ancestors.Count < 2 || this.TempleCells(ancestors[1]) is not { } cells || !cells.ContainsKey(at))
            {
                return null;
            }

            var rooms = this.TempleRooms(ancestors[1].Address, cells);
            var neighbours = TempleGrid.Neighbours(at, place => rooms.TryGetValue(place, out var room) ? room.Title : null);
            var cut = rooms.ContainsKey(at) && !TempleGrid.Reached(rooms.ToDictionary(each => each.Key, each => each.Value.Path)).Contains(at);
            return Announcements.TempleCellPlace(at.Row, at.Column, neighbours, cut, TempleRun.Here == at, TempleRun.Done.Contains(at));
        }

        private (IntPtr Holder, long At, Dictionary<(int Row, int Column), (string Title, bool Path)> Rooms)? templeRooms;

        /// <summary>
        ///     What each cell holds: its tooltip's title, and whether it is a path. A path's tooltip is its title alone ("Camino",
        ///     "Vestíbulo"; L3 on a path card: "Sin detalles"), a room's goes on with its effect ("Guarnición nivel 1. …"),
        ///     which says it in any language. Kept as long as the cells are.
        /// </summary>
        private Dictionary<(int Row, int Column), (string Title, bool Path)> TempleRooms(IntPtr holder, Dictionary<(int Row, int Column), IntPtr> cells)
        {
            var now = Environment.TickCount64;
            if (this.templeRooms is { } kept && kept.Holder == holder && now - kept.At < TempleCellsKeptMs)
            {
                return kept.Rooms;
            }

            var rooms = new Dictionary<(int Row, int Column), (string Title, bool Path)>();
            foreach (var (place, address) in cells)
            {
                if (this.elements.TryReadElement(address, out var info) && this.TooltipLines(info) is { Count: > 0 } lines)
                {
                    rooms[place] = (lines[0], lines.Count == 1);
                }
            }

            this.templeRooms = (holder, now, rooms);
            TempleRun.Publish(rooms);
            return rooms;
        }

        /// <summary>The last map holder found: a card of the console sits three levels under it, a cell two.</summary>
        private IntPtr templeHolder;

        /// <summary>Whether a card was held at the last read of a cell.</summary>
        private bool templePlacing;

        /// <summary>The cell the held card was last told on, and what was told: the narrator reads a cell again as the hint bar changes.</summary>
        private (IntPtr Cell, string News, long At) templeCardTold;

        /// <summary>How long the held card stays said on a cell read again (the hint bar's "Colocar pieza [A]" re-reads it ~170 ms later).</summary>
        private const int TempleCardToldMs = 1500;

        /// <summary>The children a card of the Room Cards list has (frame, two marks, caption; dump 2026-11-27).</summary>
        private const int TempleCardParts = 4;

        /// <summary>
        ///     The card held and how many cells take it, or null when none is held. Compared with the console idle (dumps
        ///     2026-11-27 11:50 and 12:19, Camino held): the held card's first child, a frame, is drawn (on no other card, the
        ///     focused one included), the list slides left, and the cells the card can go on, and only those, take the navigable
        ///     bit (7 free cells, each beside a path).
        /// </summary>
        private (string Card, int Spots)? TempleHeld(in UiElementReader.ElementInfo holder, Dictionary<(int Row, int Column), IntPtr> cells)
        {
            foreach (var groupAddress in this.elements.ReadChildren(holder))
            {
                if (!this.elements.TryReadElement(groupAddress, out var group))
                {
                    continue;
                }

                foreach (var listAddress in this.elements.ReadChildren(group))
                {
                    if (!this.elements.TryReadElement(listAddress, out var list))
                    {
                        continue;
                    }

                    foreach (var cardAddress in this.elements.ReadChildren(list))
                    {
                        if (this.elements.TryReadElement(cardAddress, out var card) && TempleGrid.Parse(card.Name) == null &&
                            this.elements.ReadChildren(card) is { Length: TempleCardParts } parts &&
                            this.elements.TryReadElement(parts[0], out var frame) && frame.IsVisibleFlag &&
                            this.FindDescendantText(card, 1, null) is { Length: > 0 } name)
                        {
                            var spots = 0;
                            foreach (var address in cells.Values)
                            {
                                if (this.elements.TryReadElement(address, out var each) && each.HasFlagBit(GameLayout.Element.NavigableFlagBit) &&
                                    this.TempleRoomAt(address) == null)
                                {
                                    spots++;
                                }
                            }

                            return (name, spots);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     The card held, on the first cell read after A on it (and on that cell read again soon after); null otherwise. A on
        ///     a card only moved the cursor to the map and queued "Cancelar, B" (user 2026-11-27: "no dijo que tenía la carta
        ///     seleccionada", "tampoco me dice cuando estoy sujetando una sala o no").
        /// </summary>
        private string? TempleHeldCardNews(in UiElementReader.ElementInfo cell)
        {
            var now = Environment.TickCount64;
            var held = this.templeCells is { } kept && this.elements.TryReadElement(kept.Holder, out var holder) ? this.TempleHeld(holder, kept.Cells) : null;
            string? news = null;
            if (held is { } card && !this.templePlacing)
            {
                news = Announcements.TemplePlacing(card.Card, card.Spots);
                this.templeCardTold = (cell.Address, news, now);
            }
            else if (held != null && this.templeCardTold.Cell == cell.Address && now - this.templeCardTold.At < TempleCardToldMs)
            {
                news = this.templeCardTold.News;
            }

            this.templePlacing = held != null;
            return news;
        }

        /// <summary>
        ///     ", en la mano" for the card held, when the cursor goes back to the Room Cards list with it; empty for any other button.
        /// </summary>
        private string TempleCardHeldMark(in UiElementReader.ElementInfo button, string label)
        {
            var inHub = (this.AreaId?.Invoke() ?? string.Empty).StartsWith(TempleHubArea, StringComparison.Ordinal);
            if (label.Length == 0 || (this.templeHolder == IntPtr.Zero && !inHub))
            {
                return string.Empty;
            }

            // The console opens on a card, before any cell is read, and a console opened again is new elements: in the Vaal
            // Ruins the card's holder is looked at for the map.
            var ancestors = this.elements.ReadAncestors(button, 3);
            if (ancestors.Count < 3 || (ancestors[2].Address != this.templeHolder && (!inHub || this.TempleCells(ancestors[2]) == null)) ||
                this.templeCells is not { } kept || kept.Holder != ancestors[2].Address)
            {
                return string.Empty;
            }

            var holder = ancestors[2];
            var held = this.TempleHeld(holder, kept.Cells);
            this.templePlacing = held != null;
            var marks = new List<string>();
            if (held != null && this.elements.ReadChildren(button) is { Length: TempleCardParts } parts &&
                this.elements.TryReadElement(parts[0], out var frame) && frame.IsVisibleFlag)
            {
                marks.Add(Announcements.TempleCardInHand);
            }

            // The cards left, when the list is first read and each time it shrinks or grows: the list position "1 de 4" said it
            // only in passing (user 2026-11-27: "tampoco me dice cuántas cartas tengo restantes").
            var left = this.elements.TryReadElement(button.Raw.ParentPtr, out var list)
                ? this.elements.ReadChildren(list).Count(card => this.elements.TryReadElement(card, out var each) && each.IsVisibleFlag)
                : 0;
            if (left > 0 && (this.templeCardsLeft.Holder != this.templeHolder || this.templeCardsLeft.Count != left))
            {
                marks.Add(Announcements.TempleCardsLeft(left));
            }

            this.templeCardsLeft = (this.templeHolder, left);
            return string.Join(", ", marks);
        }

        /// <summary>The Vaal Ruins, where the Temple Console stands (WorldAreas IncursionHub; log 2026-11-27 "Ruinas vaal (IncursionHub)").</summary>
        private const string TempleHubArea = "IncursionHub";

        /// <summary>How many cards the Room Cards list held when last read, for the console it was read in.</summary>
        private (IntPtr Holder, int Count) templeCardsLeft;

        /// <summary>The room or path a cell holds, by its tooltip's title (an empty cell has no tooltip); null when empty.</summary>
        private string? TempleRoomAt(IntPtr address) =>
            this.elements.TryReadElement(address, out var info) && this.TooltipLines(info) is { Count: > 0 } lines ? lines[0] : null;

        /// <summary>Every coordinate-named cell under the map's holder, when one of its children holds the grid.</summary>
        private Dictionary<(int Row, int Column), IntPtr>? TempleCells(in UiElementReader.ElementInfo holder)
        {
            var now = Environment.TickCount64;
            if (this.templeCells is { } kept && kept.Holder == holder.Address && now - kept.At < TempleCellsKeptMs)
            {
                return kept.Cells;
            }

            var cells = new Dictionary<(int Row, int Column), IntPtr>();
            var grid = false;
            foreach (var groupAddress in this.elements.ReadChildren(holder))
            {
                if (!this.elements.TryReadElement(groupAddress, out var group))
                {
                    continue;
                }

                var named = 0;
                foreach (var cellAddress in this.elements.ReadChildren(group))
                {
                    if (this.elements.TryReadElement(cellAddress, out var cellInfo) && TempleGrid.Parse(cellInfo.Name) is { } place)
                    {
                        cells[place] = cellAddress;
                        named++;
                    }
                }

                grid |= named >= TempleGrid.MinCells;
            }

            if (!grid)
            {
                return null;
            }

            this.templeCells = (holder.Address, now, cells);
            this.templeHolder = holder.Address;
            return cells;
        }
    }
}
