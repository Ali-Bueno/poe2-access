namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What the arrow at the minimap's edge points to: a boss room or a quest mark within its icon's pointer reach and off
    ///     screen, the quest's first, then the nearest (docs/radar.md "The minimap's arrow"). Pure.
    /// </summary>
    public static class MinimapPointer
    {
        /// <param name="Id">The scanner entry's id.</param>
        /// <param name="Cell">Where it stands, in grid cells.</param>
        /// <param name="ReachCells">Its icon's pointer reach (MinimapIcons +0x12), in grid cells.</param>
        /// <param name="Quest">What the quest asks for, ahead of a boss room.</param>
        /// <param name="InView">On screen: the minimap draws the icon itself and the thing sounds of its own.</param>
        public readonly record struct Mark(uint Id, Vector2 Cell, float ReachCells, bool Quest, bool InView);

        /// <summary>The scanner kinds the arrow points to: boss rooms, the quest's objects and the campaign's landmarks.</summary>
        public static bool Points(string kind) => kind is MinimapMarks.BossRoomKind or MinimapMarks.QuestObjectKind or MinimapMarks.LandmarkKind;

        /// <summary>The mark the arrow points to from <paramref name="here"/> (grid cells); null for none.</summary>
        public static Mark? Pick(IEnumerable<Mark> marks, Vector2 here) =>
            marks.Where(mark => !mark.InView && mark.ReachCells > 0 && Vector2.Distance(here, mark.Cell) <= mark.ReachCells)
                .OrderBy(mark => mark.Quest ? 0 : 1)
                .ThenBy(mark => Vector2.Distance(here, mark.Cell))
                .Select(mark => (Mark?)mark)
                .FirstOrDefault();
    }
}
