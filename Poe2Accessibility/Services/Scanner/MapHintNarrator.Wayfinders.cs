namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text.RegularExpressions;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The signal seals of El cruce de Khari (`…/Objects/Wayfinder{N}Pointer`): used, the game turns a compass on the player
    ///     toward the next seal, and past the last toward the quest's exit; the mod follows it as a trail (docs/research/map-hints.md).
    /// </summary>
    public sealed partial class MapHintNarrator
    {
        /// <summary>The seal's state: 1 once used, which also makes it untargetable (`Wayfinder{N}Pointer.ot`).</summary>
        private const string PointerState = "active";

        /// <summary>A seal used this near the player (cells) is the one just used: the trail starts from here.</summary>
        private const float UsedHereCells = ExplorationMap.LookCells;

        private static readonly Regex PointerPath = new(@"/Objects/Wayfinder\d+Pointer$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly MapHintRow WayfinderRow = new() { Hint = "wayfinder", Line = true };

        private readonly HashSet<uint> pointersUsed = new();

        /// <summary>A seal seen used beside the player sends a trail to the next seal still to use, else to the quest's exit.</summary>
        private void WatchWayfinders(AreaInstance map, Vector2 here)
        {
            // The first seal seen used starts one too, wherever it stands: the compass stays on the player after a restart.
            var first = this.pointersUsed.Count == 0;
            foreach (var (id, cell, used) in Pointers(map).ToList())
            {
                if (used && !this.pointersUsed.Contains(id) && (first || Vector2.Distance(here, cell) <= UsedHereCells))
                {
                    first = false;
                    this.pointersUsed.Add(id);
                    this.log.Info($"Wayfinder used at cell {cell.X:0},{cell.Y:0}: a trail to the next");
                    var row = this.rows.FirstOrDefault(each => each.Hint == WayfinderRow.Hint) ?? WayfinderRow;
                    this.trails.Add(row, Array.Empty<Vector2>(), () => this.NextWayfinder(row));
                }
            }
        }

        /// <summary>
        ///     The nearest seal still to use, by the game's name for it; else the row's goal (the room of the last, invisible
        ///     `Wayfinder4` holds El cruce de Khari's exit to the Pools of Khatal), else the exit the quest leads through.
        /// </summary>
        private ScanEntry? NextWayfinder(MapHintRow row)
        {
            var map = Core.States.InGameStateObject.CurrentAreaInstance;
            if (map.WorldToGridConvertor is not (> 0 and var toWorld) || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var entries = this.catalog.Entries();
            if (Pointers(map).Where(pointer => !pointer.Used).OrderBy(pointer => Vector2.Distance(here, pointer.Cell)).FirstOrDefault() is { Id: > 0 } next)
            {
                var name = entries.FirstOrDefault(entry => entry.Id == next.Id)?.Name ?? Announcements.WayfinderName;
                return new ScanEntry(next.Id, ScanCategory.Objects, name, next.Cell.X * toWorld, next.Cell.Y * toWorld, player.WorldPosition.Z);
            }

            return this.Goal(row) ?? this.catalog.QuestExit();
        }

        /// <summary>The awake seals with their place and whether they were used.</summary>
        private static IEnumerable<(uint Id, Vector2 Cell, bool Used)> Pointers(AreaInstance map)
        {
            foreach (var entity in map.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.Path is { } path && PointerPath.IsMatch(path) && entity.TryGetComponent<Render>(out var render) &&
                    entity.TryGetComponent<StateMachine>(out var machine))
                {
                    var used = machine.States.FirstOrDefault(state => state.Name == PointerState) is { Value: > 0 };
                    yield return (entity.Id, new Vector2(render.GridPosition.X, render.GridPosition.Y), used);
                }
            }
        }
    }
}
