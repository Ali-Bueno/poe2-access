namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameOffsets.Natives;

    /// <summary>The marks the minimap's edge arrow can point to (<see cref="MinimapPointer"/>), from the last listing.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>Boss rooms not yet slain and quest or landmark icons not spent, with their arrow's reach in grid cells.</summary>
        private IReadOnlyList<(ScanEntry Entry, float ReachCells)> pointing = Array.Empty<(ScanEntry, float)>();

        /// <summary>Every mark the arrow could point to now, in grid cells; <see cref="MinimapPointer.Pick"/> chooses.</summary>
        public IReadOnlyList<MinimapPointer.Mark> PointerMarks()
        {
            this.Entries();
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (toWorld <= 0 || this.pointing.Count == 0)
            {
                return Array.Empty<MinimapPointer.Mark>();
            }

            var quest = this.questTargets.Select(target => target.Id).ToHashSet();
            return this.pointing.Select(point => new MinimapPointer.Mark(
                point.Entry.Id,
                new Vector2(point.Entry.WorldX, point.Entry.WorldY) / toWorld,
                point.ReachCells,
                quest.Contains(point.Entry.Id) || point.Entry.QuestMarked || point.Entry.Kind == MinimapMarks.QuestObjectKind,
                ScreenProjection.InView(new StdTuple3D<float> { X = point.Entry.WorldX, Y = point.Entry.WorldY, Z = point.Entry.WorldZ }))).ToList();
        }
    }
}
