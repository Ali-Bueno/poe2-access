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

        /// <summary>The guide's end this near (cells) is reached: the end of the walk, within what the player sees.</summary>
        private const float GuideEndCells = ExplorationMap.LookCells;

        private static readonly Regex PointerPath = new(@"/Objects/Wayfinder\d+Pointer$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly MapHintRow WayfinderRow = new() { Hint = "wayfinder", Line = true };

        /// <summary>The value of <see cref="Guides"/> while a character follows the seals' guide in an area.</summary>
        private const string GuideOn = "on";

        private const string GuideOff = "off";

        private readonly HashSet<uint> pointersUsed = new();
        private readonly HashSet<uint> pointersUnused = new();
        private bool guideResumed;
        private bool guideTrailOn;

        /// <summary>
        ///     Per character and area, whether the seals' guide is on: the compass stays on the player in a new instance or after a
        ///     restart of the mod, when the seal used may be far out of the game's reach (file `config/wayfinders.txt`).
        /// </summary>
        public PassiveResume? Guides { get; set; }

        /// <summary>The followed character's name, for <see cref="Guides"/>.</summary>
        public Func<string>? Character { get; set; }

        private string GuideKey => $"{this.Character?.Invoke() ?? string.Empty}|{this.area}";

        /// <summary>
        ///     A seal seen turning used sends a trail to the next seal still to use, else to the row's goal; so does the first seal
        ///     seen already used when no trail is on in this area (one used before the mod saw it), and a guide on from before.
        /// </summary>
        private void WatchWayfinders(AreaInstance map)
        {
            if (!this.guideResumed)
            {
                this.guideResumed = true;
                if (this.Guides?.Get(this.GuideKey) == GuideOn)
                {
                    this.log.Info("Wayfinder guide on from before: a trail to the next");
                    this.AddWayfinderTrail();
                }
            }

            foreach (var (id, cell, used) in Pointers(map).ToList())
            {
                if (!used)
                {
                    this.pointersUnused.Add(id);
                }
                else if (this.pointersUsed.Add(id) && (this.pointersUnused.Remove(id) || !this.guideTrailOn))
                {
                    this.log.Info($"Wayfinder used at cell {cell.X:0},{cell.Y:0}: a trail to the next");
                    this.AddWayfinderTrail();
                }
            }
        }

        private void AddWayfinderTrail()
        {
            var row = this.rows.FirstOrDefault(each => each.Hint == WayfinderRow.Hint) ?? WayfinderRow;
            this.trails.Add(row, Array.Empty<Vector2>(), () => this.NextWayfinder(row));
            this.Guides?.Set(this.GuideKey, GuideOn);
            this.guideTrailOn = true;
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

            var last = this.Goal(row) ?? this.catalog.QuestExit();
            if (last != null && Vector2.Distance(here, new Vector2(last.WorldX, last.WorldY) / toWorld) <= GuideEndCells)
            {
                this.Guides?.Set(this.GuideKey, GuideOff); // the guide's end reached: nothing to follow on a later visit
            }

            return last;
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
