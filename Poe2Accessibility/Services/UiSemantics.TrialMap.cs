namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>The Trial of the Sekhemas' map: a room's place, as the tutorial explains the floor (user 2026-11-07).</summary>
    public sealed partial class UiSemantics
    {
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
            var standing = TrialMap.Standing(column, this.TrialRoomIsOpen(address), firstOpen);
            return Announcements.TrialRoomPlace(column + 1, columns.Length, row + 1, rooms[column].Length, standing);
        }

        private bool TrialRoomIsOpen(IntPtr room) =>
            this.elements.TryReadElement(room, out var info) &&
            this.TooltipLines(info).Any(line => line.EndsWith($", {ControllerGlyphs.Interact}", StringComparison.Ordinal));
    }
}
