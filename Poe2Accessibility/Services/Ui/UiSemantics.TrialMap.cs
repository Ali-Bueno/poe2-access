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
            if (this.ReadTrialFloor(this.elements.ReadAncestors(room, GameLayout.TrialFloor.HolderHops), sizes) is { } floor)
            {
                // Where the player is: the last of the rooms the game keeps as taken or, when those are not where they
                // were measured, the room of the player's column the game draws its glow under.
                (int Column, int Room)? at = floor.Walk.Count > 0 ? (floor.Walk.Count - 1, floor.Walk[^1]) : this.TrialMarkedRoom(rooms, firstOpen);
                if (at is { } own)
                {
                    // The links the game draws between the rooms: what can still be reached, through which room, and where each leads.
                    var open = firstOpen == own.Column + 1
                        ? Enumerable.Range(0, sizes[firstOpen]).Where(each => this.TrialRoomIsOpen(rooms[firstOpen][each])).ToList()
                        : new List<int>();
                    var view = TrialMap.View(floor.Links, own.Column, own.Room, column, row, open);
                    return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, sizes[column], view.Standing, view.Through, view.LeadsTo);
                }
            }

            var standing = TrialMap.Standing(column, this.TrialRoomIsOpen(address), firstOpen, this.TrialRoomIsMarked(address));
            return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, rooms[column].Length, standing);
        }

        /// <summary>The one room the game marks in the column before the first to enter: where the player stands, by the screen alone.</summary>
        private (int Column, int Room)? TrialMarkedRoom(IReadOnlyList<IntPtr[]> rooms, int firstOpen)
        {
            if (firstOpen < 1)
            {
                return null;
            }

            var marked = Enumerable.Range(0, rooms[firstOpen - 1].Length).Where(each => this.TrialRoomIsMarked(rooms[firstOpen - 1][each])).ToList();
            return marked.Count == 1 ? (firstOpen - 1, marked[0]) : null;
        }

        /// <summary>Where the floor's links stand: as measured until a search by shape finds them elsewhere (a patch moved them).</summary>
        private TrialFloorShape trialFloorShape = new(
            GameLayout.TrialFloor.HolderOffset, GameLayout.TrialFloor.ColumnsOffset, GameLayout.TrialFloor.ColumnSize,
            GameLayout.TrialFloor.RoomsOffset, GameLayout.TrialFloor.RoomSize, GameLayout.TrialFloor.WaysOffset);

        /// <summary>The map last searched by shape (its columns' container and its rooms): a search that finds nothing is not run again on it.</summary>
        private (IntPtr Container, string Rooms) trialFloorSearched;

        /// <summary>
        ///     The floor's links and the rooms taken, read from the floor an ancestor of the rooms points to; null unless the
        ///     links fit the map on screen (<see cref="TrialMap.Fits"/>). The rooms taken are empty when they are no walk
        ///     along the links.
        /// </summary>
        private (IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links, IReadOnlyList<int> Walk)? ReadTrialFloor(
            IReadOnlyList<UiElementReader.ElementInfo> ancestors, IReadOnlyList<int> rooms)
        {
            var holders = ancestors.Skip(2).Select(holder => (long)holder.Address).ToList(); // past the column and the columns' container
            byte[] Read(long address, int count) => this.elements.ReadBytesAt((IntPtr)address, count);
            foreach (var holder in holders)
            {
                if (this.elements.TryReadPointer((IntPtr)(holder + this.trialFloorShape.HolderOffset), out var floor) &&
                    TrialFloorLocator.ReadLinks(Read, (long)floor, this.trialFloorShape, rooms) is { } links)
                {
                    return this.TrialFloorRead((long)floor, links, rooms);
                }
            }

            // Not where it was measured: a patch may have moved it. Searched by shape, once a map.
            var map = (ancestors.Count > 1 ? ancestors[1].Address : IntPtr.Zero, string.Join(",", rooms));
            if (map != this.trialFloorSearched)
            {
                this.trialFloorSearched = map;
                if (TrialFloorLocator.Find(Read, holders, this.trialFloorShape, rooms) is { } found)
                {
                    this.trialFloorShape = found.Shape;
                    this.TrialFloorNote?.Invoke(
                        $"Trial floor: found by shape, GameLayout.TrialFloor is out of date: holder +0x{found.Shape.HolderOffset:X}, " +
                        $"columns +0x{found.Shape.ColumnsOffset:X} of 0x{found.Shape.ColumnSize:X} bytes, rooms +0x{found.Shape.RoomsOffset:X} " +
                        $"of 0x{found.Shape.RoomSize:X} bytes, ways +0x{found.Shape.WaysOffset:X}");
                    return this.TrialFloorRead(found.Floor, found.Links, rooms);
                }
            }

            this.NoteTrialFloor($"Trial floor: no links fit the map of columns {string.Join(",", rooms)} ({ancestors.Count} ancestors)");
            return null;
        }

        private (IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links, IReadOnlyList<int> Walk) TrialFloorRead(
            long floor, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links, IReadOnlyList<int> rooms)
        {
            var taken = this.elements.ReadBytesAt(
                (IntPtr)(floor + this.trialFloorShape.ColumnsOffset + GameLayout.TrialFloor.PathAfterColumns), Math.Min(rooms.Count, GameLayout.TrialFloor.PathColumns));
            var walk = TrialMap.Walk(links, taken, GameLayout.TrialFloor.NoRoom);
            this.NoteTrialFloor($"Trial floor: columns {string.Join(",", rooms)}, links {string.Join(" | ", links.Select(column => string.Join(" ", column.Select(ways => string.Join(",", ways)))))}, " +
                                $"taken {Convert.ToHexString(taken)}, walk {(walk.Count > 0 ? string.Join(",", walk) : "none: the player's room is the one the map marks")}");
            return (links, walk);
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
                    var effects = slots.SelectMany(this.TrialEffectsOf).Distinct().ToList();
                    return effects.Count == 0 && this.FindTrialEffects(slots) ? slots.SelectMany(this.TrialEffectsOf).Distinct().ToList() : effects;
                }
            }

            return null;
        }

        private bool IsTrialEffectSlot(IntPtr slot) =>
            this.elements.TryReadElement(slot, out var info) && this.elements.ReadChildren(info) is { Length: > 0 } parts &&
            this.elements.TryReadElement(parts[0], out var icon) && this.elements.ReadTextureKey(icon) is { } art &&
            art.Contains(GameLayout.Image.TrialEffectSlotArtMarker, StringComparison.OrdinalIgnoreCase);

        /// <summary>Where a slot keeps what it carries: as measured until a search finds it elsewhere (a patch moved it).</summary>
        private int trialEffectsOffset = GameLayout.TrialFloor.SlotEffectsOffset;

        /// <summary>The slots already searched for it, each once.</summary>
        private readonly HashSet<IntPtr> trialSlotsSearched = new();

        /// <summary>The effects of one slot: each key's row names itself by its Id, which the catalog must hold.</summary>
        private IEnumerable<string> TrialEffectsOf(IntPtr slot) => this.TrialEffectKeys(slot, this.trialEffectsOffset).OfType<string>();

        /// <summary>The catalog's key of each effect a vector on a slot holds, null for a key that names none the catalog knows.</summary>
        private IEnumerable<string?> TrialEffectKeys(IntPtr slot, int offset)
        {
            var vector = slot + offset;
            if (!this.elements.TryReadPointer(vector, out var first) || !this.elements.TryReadPointer(vector + IntPtr.Size, out var end) ||
                (long)end - (long)first is < 0 or > GameLayout.TrialFloor.MaxEffects * GameLayout.TrialFloor.KeySize)
            {
                yield break; // an empty slot's vector is null: nothing carried of its kind
            }

            for (var key = first; (long)key < (long)end; key += GameLayout.TrialFloor.KeySize)
            {
                yield return this.elements.TryReadPointer(key, out var effect) && this.elements.TryReadPointer(effect, out var id) &&
                             this.elements.ReadUtf16BufferAt(id, GameLayout.TrialFloor.EffectIdChars) is { } name
                    ? this.Trials.KeyOfEffect(name)
                    : null;
            }
        }

        /// <summary>
        ///     Finds where the slots keep what they carry when none reads as carrying anything although the game draws one
        ///     (it draws a slot only while it holds something): the one vector on that slot whose every key names an effect
        ///     of the game's tables. True when found elsewhere than it was.
        /// </summary>
        private bool FindTrialEffects(IReadOnlyList<IntPtr> slots)
        {
            foreach (var slot in slots)
            {
                if (this.trialSlotsSearched.Contains(slot) || !this.elements.TryReadElement(slot, out var info) || !info.IsVisibleFlag)
                {
                    continue;
                }

                this.trialSlotsSearched.Add(slot);
                var body = TrialFloorLocator.Window((address, count) => this.elements.ReadBytesAt((IntPtr)address, count), (long)slot, GameLayout.TrialFloor.SlotWindow);
                for (var offset = 0; offset + (2 * IntPtr.Size) <= body.Length; offset += IntPtr.Size)
                {
                    var span = BitConverter.ToInt64(body, offset + IntPtr.Size) - BitConverter.ToInt64(body, offset);
                    if (span <= 0 || span % GameLayout.TrialFloor.KeySize != 0 || span > GameLayout.TrialFloor.MaxEffects * GameLayout.TrialFloor.KeySize)
                    {
                        continue;
                    }

                    var keys = this.TrialEffectKeys(slot, offset).ToList();
                    if (keys.Count > 0 && keys.All(key => key != null))
                    {
                        this.TrialFloorNote?.Invoke($"Trial effects: found by their Ids, GameLayout.TrialFloor.SlotEffectsOffset is out of date: +0x{offset:X} ({string.Join(", ", keys)})");
                        this.trialEffectsOffset = offset;
                        return true;
                    }
                }
            }

            return false;
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
