namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The Trial of the Sekhemas' map: a room's place, as the tutorial explains the floor (user 2026-11-07).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The counts beside the Trial's honour line on the HUD, in their order: each counter holds an icon and its number.</summary>
        public IReadOnlyList<string> TrialCounters(IntPtr honourLine)
        {
            var counts = new List<string>();
            if (!this.elements.TryReadElement(honourLine, out var line) || !this.elements.TryReadElement(line.Raw.ParentPtr, out var row))
            {
                return counts;
            }

            foreach (var address in this.elements.ReadChildren(row).TakeWhile(address => address != honourLine))
            {
                if (this.elements.TryReadElement(address, out var counter) && this.elements.ReadChildren(counter) is { Length: 2 } parts &&
                    this.elements.TryReadText(parts[1], out var count) && count.Length > 0)
                {
                    counts.Add(count);
                }
            }

            return counts;
        }

        /// <summary>
        ///     Which of the Trial's counters a number is, by the icon before it: its place in
        ///     <see cref="GameLayout.Image.TrialCounterArtMarkers"/>, negative for any other text. The map window shows them
        ///     while it hides the HUD's.
        /// </summary>
        public int TrialCounterKind(IntPtr number)
        {
            if (!this.elements.TryReadElement(number, out var text) || !this.elements.TryReadElement(text.Raw.ParentPtr, out var pair) ||
                this.elements.ReadChildren(pair) is not { Length: 2 } parts || parts[1] != number ||
                !this.elements.TryReadElement(parts[0], out var icon) || this.elements.ReadTextureKey(icon) is not { } art)
            {
                return -1;
            }

            return Array.FindIndex(GameLayout.Image.TrialCounterArtMarkers, marker => art.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     "columna 2 de 8, sala 3 de 3" and how the room stands, or empty for any other button. Each column of the map is a
        ///     child of one element, its rooms buttons; only a room the player can enter has the interact button in its tooltip.
        /// </summary>
        private string TrialRoomPlace(in UiElementReader.ElementInfo room)
        {
            var ancestors = this.elements.ReadAncestors(room, 2);
            if (ancestors.Count < 2)
            {
                return string.Empty;
            }

            var columns = this.elements.ReadChildren(ancestors[1]);
            var rooms = columns.Select(column => this.elements.TryReadElement(column, out var info) ? this.elements.ReadChildren(info) : Array.Empty<IntPtr>()).ToList();
            if (!TrialMap.IsMap(rooms.Select(column => column.Length).ToList()))
            {
                return string.Empty;
            }

            var address = room.Address;
            var column = Array.IndexOf(columns, ancestors[0].Address);
            var row = column < 0 ? -1 : Array.IndexOf(rooms[column], address);
            if (row < 0)
            {
                return string.Empty;
            }

            var firstOpen = rooms.FindIndex(inColumn => inColumn.Any(this.TrialRoomIsOpen));
            var sizes = rooms.Select(inColumn => inColumn.Length).ToList();
            if (this.ReadTrialFloor(this.elements.ReadAncestors(room, GameLayout.TrialFloor.HolderHops), sizes) is { Walk.Count: > 0 } floor)
            {
                // The links the game draws between the rooms, and the rooms taken: where the player is and what can still be reached.
                var open = firstOpen == floor.Walk.Count
                    ? Enumerable.Range(0, sizes[firstOpen]).Where(each => this.TrialRoomIsOpen(rooms[firstOpen][each])).ToList()
                    : new List<int>();
                var view = TrialMap.View(floor.Links, floor.Walk, column, row, open);
                return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, sizes[column], view.Standing, view.Through, view.LeadsTo);
            }

            var standing = TrialMap.Standing(column, this.TrialRoomIsOpen(address), firstOpen, this.TrialRoomIsMarked(address));
            return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, rooms[column].Length, standing);
        }

        /// <summary>
        ///     The floor's links and the rooms taken, read from the floor an ancestor of the rooms points to; null unless the
        ///     links fit the map on screen (<see cref="TrialMap.Fits"/>).
        /// </summary>
        private (IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links, IReadOnlyList<int> Walk)? ReadTrialFloor(
            IReadOnlyList<UiElementReader.ElementInfo> ancestors, IReadOnlyList<int> rooms)
        {
            foreach (var holder in ancestors.Skip(2)) // past the column and the columns' container
            {
                if (this.elements.TryReadPointer(holder.Address + GameLayout.TrialFloor.HolderOffset, out var floor) && this.ReadTrialLinks(floor, rooms) is { } links)
                {
                    var taken = this.elements.ReadBytesAt(floor + GameLayout.TrialFloor.PathOffset, Math.Min(rooms.Count, GameLayout.TrialFloor.PathColumns));
                    var walk = TrialMap.Walk(links, taken, GameLayout.TrialFloor.NoRoom);
                    this.NoteTrialFloor($"Trial floor: columns {string.Join(",", rooms)}, links {string.Join(" | ", links.Select(column => string.Join(" ", column.Select(ways => string.Join(",", ways)))))}, " +
                                        $"taken {Convert.ToHexString(taken)}, walk {string.Join(",", walk)}");
                    return (links, walk);
                }
            }

            this.NoteTrialFloor($"Trial floor: no links fit the map of columns {string.Join(",", rooms)} ({ancestors.Count} ancestors)");
            return null;
        }

        /// <summary>Told what the floor read as, once a reading: the plugin's log, to check the links against the map heard.</summary>
        public Action<string>? TrialFloorNote { get; set; }

        private string lastTrialFloorNote = string.Empty;

        private void NoteTrialFloor(string note)
        {
            if (note != this.lastTrialFloorNote)
            {
                this.lastTrialFloorNote = note;
                this.TrialFloorNote?.Invoke(note);
            }
        }

        private IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? ReadTrialLinks(IntPtr floor, IReadOnlyList<int> rooms)
        {
            if (!this.TryReadGameVector(floor + GameLayout.TrialFloor.ColumnsOffset, GameLayout.TrialFloor.ColumnSize, rooms.Count, out var columns))
            {
                return null;
            }

            var links = new List<IReadOnlyList<IReadOnlyList<int>>>();
            for (var column = 0; column < rooms.Count; column++)
            {
                if (!this.TryReadGameVector(columns + (column * GameLayout.TrialFloor.ColumnSize), GameLayout.TrialFloor.RoomSize, rooms[column], out var first))
                {
                    return null;
                }

                var ways = new List<IReadOnlyList<int>>();
                for (var room = 0; room < rooms[column]; room++)
                {
                    var at = first + (room * GameLayout.TrialFloor.RoomSize);
                    // A room that leads nowhere (the boss's) may hold no pointer at all: only the span is asked of them.
                    if (!this.elements.TryReadWord(at, out var begin) || !this.elements.TryReadWord(at + IntPtr.Size, out var end) ||
                        (long)end - (long)begin is < 0 or > GameLayout.TrialFloor.MaxWays)
                    {
                        return null;
                    }

                    var rows = this.elements.ReadBytesAt(begin, (int)((long)end - (long)begin));
                    if (rows.Length != (int)((long)end - (long)begin))
                    {
                        return null;
                    }

                    ways.Add(rows.Select(each => (int)each).ToList());
                }

                links.Add(ways);
            }

            // The boss's room leads nowhere; the links are those of the columns before it.
            if (links[^1].Any(ways => ways.Count > 0))
            {
                return null;
            }

            links.RemoveAt(links.Count - 1);
            return TrialMap.Fits(rooms, links) ? links : null;
        }

        /// <summary>A game vector's first element when it holds exactly <paramref name="count"/> of <paramref name="size"/> bytes each.</summary>
        private bool TryReadGameVector(IntPtr vector, int size, int count, out IntPtr first) =>
            this.elements.TryReadPointer(vector, out first) && this.elements.TryReadPointer(vector + IntPtr.Size, out var end) &&
            (long)end - (long)first == (long)size * count;

        /// <summary>
        ///     The afflictions and boons a run carries, as catalog keys, from the slots beside the Trial's counters (the HUD's
        ///     row or the map window's); null when no slot is found there. The HUD draws one icon a kind and names none.
        /// </summary>
        /// <param name="beside">The honour line or a counter's number.</param>
        public IReadOnlyList<string>? TrialEffects(IntPtr beside)
        {
            if (!this.elements.TryReadElement(beside, out var from))
            {
                return null;
            }

            foreach (var row in this.elements.ReadAncestors(from, 2))
            {
                var slots = this.elements.ReadChildren(row)
                    .SelectMany(child => this.elements.TryReadElement(child, out var holder) ? this.elements.ReadChildren(holder) : Array.Empty<IntPtr>())
                    .Where(this.IsTrialEffectSlot).ToList();
                if (slots.Count > 0)
                {
                    return slots.SelectMany(this.TrialEffectsOf).Distinct().ToList();
                }
            }

            return null;
        }

        private bool IsTrialEffectSlot(IntPtr slot) =>
            this.elements.TryReadElement(slot, out var info) && this.elements.ReadChildren(info) is { Length: > 0 } parts &&
            this.elements.TryReadElement(parts[0], out var icon) && this.elements.ReadTextureKey(icon) is { } art &&
            art.Contains(GameLayout.Image.TrialEffectSlotArtMarker, StringComparison.OrdinalIgnoreCase);

        /// <summary>The effects of one slot: each key's row names itself by its Id, which the catalog must hold.</summary>
        private IEnumerable<string> TrialEffectsOf(IntPtr slot)
        {
            var vector = slot + GameLayout.TrialFloor.SlotEffectsOffset;
            if (!this.elements.TryReadPointer(vector, out var first) || !this.elements.TryReadPointer(vector + IntPtr.Size, out var end) ||
                (long)end - (long)first is < 0 or > GameLayout.TrialFloor.MaxEffects * GameLayout.TrialFloor.KeySize)
            {
                yield break; // an empty slot's vector is null: nothing carried of its kind
            }

            for (var key = first; (long)key < (long)end; key += GameLayout.TrialFloor.KeySize)
            {
                if (this.elements.TryReadPointer(key, out var effect) && this.elements.TryReadPointer(effect, out var id) &&
                    this.elements.ReadUtf16BufferAt(id, GameLayout.TrialFloor.EffectIdChars) is { } name && this.Trials.KeyOfEffect(name) is { } known)
                {
                    yield return known;
                }
            }
        }

        /// <summary>Whether the game draws the player's glow under the room: an image among the button's children.</summary>
        private bool TrialRoomIsMarked(IntPtr room) =>
            this.elements.TryReadElement(room, out var info) &&
            this.elements.ReadChildren(info).Any(child =>
                this.elements.TryReadElement(child, out var mark) && this.elements.ReadTextureKey(mark) is { } art &&
                art.Contains(GameLayout.Image.TrialCurrentRoomArtMarker, StringComparison.OrdinalIgnoreCase));

        private bool TrialRoomIsOpen(IntPtr room) =>
            this.elements.TryReadElement(room, out var info) &&
            this.TooltipLines(info).Any(line => line.EndsWith($", {ControllerGlyphs.Interact}", StringComparison.Ordinal));
    }
}
