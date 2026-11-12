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
        ///     <see cref="TrialArt.CounterMarkers"/>, negative for any other text. The map window shows them
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

            return Array.FindIndex(TrialArt.CounterMarkers, marker => art.Contains(marker, StringComparison.OrdinalIgnoreCase));
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
            var floor = this.ReadTrialFloor(this.elements.ReadAncestors(room, GameLayout.TrialFloor.HolderHops), sizes);
            // Where the player is: the last of the rooms the game keeps as taken or, those unknown, the room the game draws its glow under.
            (int Column, int Room)? own = floor is { Walk.Count: > 0 } walked ? (walked.Walk.Count - 1, walked.Walk[^1]) : this.TrialMarkedRoom(rooms, firstOpen);
            if (floor is { } read && own is { } at)
            {
                // The links the game draws between the rooms: what can still be reached, through which room, and where each leads.
                var open = firstOpen == at.Column + 1
                    ? Enumerable.Range(0, sizes[firstOpen]).Where(each => this.TrialRoomIsOpen(rooms[firstOpen][each])).ToList()
                    : new List<int>();
                var view = TrialMap.View(read.Links, at.Column, at.Room, column, row, open);
                return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, sizes[column], view.Standing, view.Through, view.LeadsTo);
            }

            var standing = TrialMap.Standing(column, row, this.TrialRoomIsOpen(address), firstOpen, own);
            return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, rooms[column].Length, standing);
        }

        /// <summary>
        ///     The room the game draws its glow under: where the player stands, by the screen alone. The one room of the map
        ///     with it (one of 27 on the map measured 2026-11-12); with rooms to enter, it must stand in the column before them.
        /// </summary>
        private (int Column, int Room)? TrialMarkedRoom(IReadOnlyList<IntPtr[]> rooms, int firstOpen)
        {
            var marked = Enumerable.Range(0, rooms.Count)
                .SelectMany(column => Enumerable.Range(0, rooms[column].Length).Where(each => this.TrialRoomIsMarked(rooms[column][each])).Select(each => (Column: column, Room: each)))
                .Take(2).ToList();
            return marked.Count == 1 && (firstOpen < 0 || marked[0].Column == firstOpen - 1) ? marked[0] : null;
        }

        /// <summary>
        ///     Where the floor's links have stood, the last that read first: as measured, and wherever a search by shape found
        ///     them since. None is forgotten: the map opened to look at and the map opened to choose a room did not keep them
        ///     at the same place (user's run, 2026-11-12: found at holder +0x3B0, columns +0x198, then lost on the next map).
        /// </summary>
        private readonly List<TrialFloorShape> trialFloorShapes = new()
        {
            new(GameLayout.TrialFloor.HolderOffset, GameLayout.TrialFloor.ColumnsOffset, GameLayout.TrialFloor.ColumnSize,
                GameLayout.TrialFloor.RoomsOffset, GameLayout.TrialFloor.RoomSize, GameLayout.TrialFloor.WaysOffset),
        };

        // A search by shape costs about a thousand small reads: on a map where it found nothing, not on every step of the cursor.
        private const int TrialSearchPauseMs = 2000;

        /// <summary>The map last searched by shape (its columns' container and its rooms), and when.</summary>
        private ((IntPtr Container, string Rooms) Map, long At) trialFloorSearched;

        private static bool TrialSearchIsDue(ref ((IntPtr Container, string Rooms) Map, long At) last, (IntPtr Container, string Rooms) map)
        {
            var now = Environment.TickCount64;
            if (last.Map == map && now - last.At < TrialSearchPauseMs)
            {
                return false;
            }

            last = (map, now);
            return true;
        }

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
            for (var known = 0; known < this.trialFloorShapes.Count; known++)
            {
                var shape = this.trialFloorShapes[known];
                foreach (var holder in holders)
                {
                    if (this.elements.TryReadPointer((IntPtr)(holder + shape.HolderOffset), out var floor) &&
                        TrialFloorLocator.ReadLinks(Read, (long)floor, shape, rooms) is { } links)
                    {
                        this.trialFloorShapes.RemoveAt(known);
                        this.trialFloorShapes.Insert(0, shape);
                        this.CheckTrialLines(ancestors, rooms, links);
                        this.NoteTrialLinks(null);
                        return this.TrialFloorRead((long)floor, shape, links, rooms);
                    }
                }
            }

            // Not where it has stood: another window of the map, or a patch. The lines the map draws say the links too, and
            // which floor is the one shown; the floor is searched by shape.
            var map = (ancestors.Count > 1 ? ancestors[1].Address : IntPtr.Zero, string.Join(",", rooms));
            var drawn = this.ReadTrialLines(ancestors, rooms, map);
            if (TrialSearchIsDue(ref this.trialFloorSearched, map) &&
                TrialFloorLocator.Find(Read, holders, this.trialFloorShapes[0], rooms, drawn) is { } found)
            {
                this.trialFloorShapes.Remove(found.Shape);
                this.trialFloorShapes.Insert(0, found.Shape);
                this.TrialFloorNote?.Invoke(
                    $"Trial floor: found by shape: holder +0x{found.Shape.HolderOffset:X}, " +
                    $"columns +0x{found.Shape.ColumnsOffset:X} of 0x{found.Shape.ColumnSize:X} bytes, rooms +0x{found.Shape.RoomsOffset:X} " +
                    $"of 0x{found.Shape.RoomSize:X} bytes, ways +0x{found.Shape.WaysOffset:X} ({this.trialFloorShapes.Count} places known)");
                this.NoteTrialLinks(null);
                return this.TrialFloorRead(found.Floor, found.Shape, found.Links, rooms);
            }

            if (drawn is not null)
            {
                this.NoteTrialFloor($"Trial floor: not found, links from the lines the map draws: columns {string.Join(",", rooms)}, links {LinksText(drawn)}");
                this.NoteTrialLinks(null);
                return (drawn, Array.Empty<int>());
            }

            this.NoteTrialFloor($"Trial floor: no links fit the map of columns {string.Join(",", rooms)} ({ancestors.Count} ancestors, {this.trialFloorShapes.Count} places known, no lines found)");
            this.NoteTrialLinks(map.Item2);
            return null;
        }

        private static string LinksText(IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links) =>
            string.Join(" | ", links.Select(column => string.Join(" ", column.Select(ways => string.Join(",", ways)))));

        /// <summary>Where the canvas keeps the lines it draws: as measured until a search by shape finds them elsewhere.</summary>
        private (int Offset, int LineSize) trialLines = (GameLayout.TrialFloor.LinesOffset, GameLayout.TrialFloor.LineSize);

        /// <summary>The map last searched for its lines by shape, and when.</summary>
        private ((IntPtr Container, string Rooms) Map, long At) trialLinesSearched;

        /// <summary>
        ///     The links of the lines the map draws, from the canvas beside the columns (<see cref="TrialMapLines"/>): a
        ///     second source for the links, the very thing on screen. Null when no element near the rooms holds such lines.
        /// </summary>
        private IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? ReadTrialLines(
            IReadOnlyList<UiElementReader.ElementInfo> ancestors, IReadOnlyList<int> rooms, (IntPtr Container, string Rooms) map)
        {
            // The canvas is a child of the element that holds it and the columns' container; one level more in case a patch wraps it.
            var near = ancestors.Skip(1).Select(each => each.Address)
                .Concat(ancestors.Skip(2).Take(2).SelectMany(each => this.elements.ReadChildren(each))).Distinct().Select(each => (long)each).ToList();
            byte[] Read(long address, int count) => this.elements.ReadBytesAt((IntPtr)address, count);
            foreach (var element in near)
            {
                if (TrialMapLines.Read(Read, element, this.trialLines.Offset, this.trialLines.LineSize, rooms) is { } links)
                {
                    return links;
                }
            }

            if (!TrialSearchIsDue(ref this.trialLinesSearched, map))
            {
                return null;
            }

            foreach (var element in near)
            {
                if (TrialMapLines.Find(Read, element, rooms) is { } found)
                {
                    this.trialLines = (found.Offset, found.LineSize);
                    this.TrialFloorNote?.Invoke($"Trial lines: found by shape: lines +0x{found.Offset:X} of 0x{found.LineSize:X} bytes");
                    return found.Links;
                }
            }

            return null;
        }

        private string trialLinesChecked = string.Empty;

        /// <summary>Says in the log, once a floor, whether the lines the map draws say the links read from the floor: each proves the other.</summary>
        private void CheckTrialLines(IReadOnlyList<UiElementReader.ElementInfo> ancestors, IReadOnlyList<int> rooms, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links)
        {
            var text = LinksText(links);
            if (text == this.trialLinesChecked)
            {
                return;
            }

            this.trialLinesChecked = text;
            var drawn = this.ReadTrialLines(ancestors, rooms, (ancestors.Count > 1 ? ancestors[1].Address : IntPtr.Zero, string.Join(",", rooms)));
            this.TrialFloorNote?.Invoke(drawn is null
                ? "Trial lines: no lines found beside the map to check the floor's links against"
                : TrialFloorLocator.Same(links, drawn) ? "Trial lines: the lines the map draws say the floor's links" : $"Trial lines: the lines the map draws say other links than the floor: {LinksText(drawn)}");
        }

        private (IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links, IReadOnlyList<int> Walk) TrialFloorRead(
            long floor, TrialFloorShape shape, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links, IReadOnlyList<int> rooms)
        {
            var taken = this.elements.ReadBytesAt(
                (IntPtr)(floor + shape.ColumnsOffset + GameLayout.TrialFloor.PathAfterColumns), Math.Min(rooms.Count, GameLayout.TrialFloor.PathColumns));
            var walk = TrialMap.Walk(links, taken, GameLayout.TrialFloor.NoRoom);
            this.NoteTrialFloor($"Trial floor (holder +0x{shape.HolderOffset:X}, columns +0x{shape.ColumnsOffset:X}): columns {string.Join(",", rooms)}, links {LinksText(links)}, " +
                                $"taken {Convert.ToHexString(taken)}, walk {(walk.Count > 0 ? string.Join(",", walk) : "none: the player's room is the one the map marks")}");
            return (links, walk);
        }

        /// <summary>Told what the floor read as, once a reading: the plugin's log, to check the links against the map heard.</summary>
        public Action<string>? TrialFloorNote { get; set; }

        private string lastTrialFloorNote = string.Empty;

        /// <summary>The report's part for the map's links: neither the floor nor the lines drawn gave them.</summary>
        public const string TrialLinksPart = "trial map links";

        /// <summary>
        ///     A map still without links this long after it first had none is no window half built: the report says it, where
        ///     before only the log did and the map was read without its ways, in silence (sweep of 2026-11-12).
        /// </summary>
        private const int TrialLinksGraceMs = 5000;

        private (string Rooms, long Since)? trialMapWithoutLinks;

        /// <summary>The map's links were read (null), or the map of these rooms gave none.</summary>
        private void NoteTrialLinks(string? roomsWithout)
        {
            if (roomsWithout == null)
            {
                this.trialMapWithoutLinks = null;
                CompatibilityReport.Current?.Found(TrialLinksPart);
                return;
            }

            var now = Environment.TickCount64;
            if (this.trialMapWithoutLinks is not { } without || without.Rooms != roomsWithout)
            {
                this.trialMapWithoutLinks = (roomsWithout, now);
            }
            else if (now - without.Since >= TrialLinksGraceMs)
            {
                CompatibilityReport.Current?.Missing(TrialLinksPart);
            }
        }

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
            art.Contains(TrialArt.EffectSlotMarker, StringComparison.OrdinalIgnoreCase);

        /// <summary>Where a slot keeps what it carries: as measured until a search finds it elsewhere (a patch moved it).</summary>
        private int trialEffectsOffset = GameLayout.TrialFloor.SlotEffectsOffset;

        /// <summary>When each slot was last searched for it: again after a pause, since a slot read as the game rewrites it finds nothing.</summary>
        private readonly Dictionary<IntPtr, long> trialSlotsSearched = new();

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
            var now = Environment.TickCount64;
            foreach (var slot in slots)
            {
                if ((this.trialSlotsSearched.TryGetValue(slot, out var searched) && now - searched < TrialSearchPauseMs) ||
                    !this.elements.TryReadElement(slot, out var info) || !info.IsVisibleFlag)
                {
                    continue;
                }

                this.trialSlotsSearched[slot] = now;
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
                art.Contains(TrialArt.CurrentRoomMarker, StringComparison.OrdinalIgnoreCase));

        private bool TrialRoomIsOpen(IntPtr room) =>
            this.elements.TryReadElement(room, out var info) &&
            this.TooltipLines(info).Any(line => line.EndsWith($", {ControllerGlyphs.Interact}", StringComparison.Ordinal));
    }
}
