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
            var standing = TrialMap.Standing(column, this.TrialRoomIsOpen(address), firstOpen, this.TrialRoomIsMarked(address));
            return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, rooms[column].Length, standing);
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
