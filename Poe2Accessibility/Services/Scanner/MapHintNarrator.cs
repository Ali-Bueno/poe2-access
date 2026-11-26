namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The cues GGG placed on the maps (docs/research/map-hints.md): walking past one it is said once, with the side it goes
    ///     when the mod knows where it leads, what a sighted player reads off the ground. That side is the mod's own working out
    ///     by the walk (<see cref="EntityCatalog.SideToward"/>): the game's files hold no direction.
    /// </summary>
    public sealed partial class MapHintNarrator
    {
        /// <summary>A second of running is 37 cells, well inside <see cref="PassingCells"/>.</summary>
        private const int PollMs = 1000;

        /// <summary>The area's name goes first, as for discoveries (DiscoveryNarrator).</summary>
        private const int ArrivalMs = 2500;

        /// <summary>What the player sees walking by: the ground the mod counts as seen.</summary>
        private const float PassingCells = ExplorationMap.LookCells;

        /// <summary>A campaign ritual's altar, known for the whole map (Freythorn's four stood on its rooms' altars, 2026-11-23).</summary>
        private const string AltarPath = "Leagues/Ritual/RitualRuneObject";

        /// <summary>The altar's state: 2 the fight, 3 finished, 4 its reward taken (the altar's .ot).</summary>
        private const string AltarState = "current_state";

        /// <summary>A tile with this many closing models this near is a dead end: the three rooms that end a road hold five to nine.</summary>
        private const float BlockedCells = 160f;
        private const int BlockedCount = 3;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly MapHintTable table;
        private readonly HintTrails trails;
        private readonly Func<string> areaId;
        private readonly PluginLog log;
        private readonly MapHintMemory memory = new();
        private IReadOnlyList<MapHintRow> rows = Array.Empty<MapHintRow>();

        /// <summary>Tiles and doodads of the rows, known at arrival.</summary>
        private ConcurrentBag<(MapHintRow Row, Vector2 Cell)> spots = new();

        /// <summary>What closes a goal's tile that is no way on (<see cref="MapHintRow.Blocked"/>).</summary>
        private ConcurrentBag<Vector2> blockers = new();

        /// <summary>The ritual altars, which the client lists for the whole map at arrival.</summary>
        private ConcurrentBag<Vector2> altars = new();
        private RitualProgress rituals = new();
        private Task? scan;
        private string instance = string.Empty;
        private string area = string.Empty;
        private long arrivedAt;
        private long lastPoll;
        private bool scanned;
        private bool trailsAdded;

        public MapHintNarrator(EntityCatalog catalog, IAccessibilityOutputService output, MapHintTable table, HintTrails trails, Func<string> areaId, PluginLog log)
        {
            this.trails = trails;
            this.catalog = catalog;
            this.output = output;
            this.table = table;
            this.areaId = areaId;
            this.log = log;
        }

        /// <summary>Say which way a cue goes, not only that it is there.</summary>
        public bool SaySide { get; set; } = true;

        /// <summary>Per frame while playing out of towns.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var map = Core.States.InGameStateObject.CurrentAreaInstance;
            if (map.AreaHash != this.instance || this.areaId() != this.area)
            {
                (this.instance, this.area, this.rows) = (map.AreaHash, this.areaId(), this.table.For(this.areaId()));
                (this.spots, this.blockers, this.altars, this.rituals) = (new(), new(), new(), new());
                (this.scanned, this.trailsAdded, this.scan, this.arrivedAt) = (false, false, null, now);
                this.trails.Reset(this.instance);
                this.pointersUsed.Clear();
            }

            if (now - this.arrivedAt < ArrivalMs)
            {
                return;
            }

            if (!this.scanned)
            {
                this.scanned = true;
                this.FindSpots(map);
            }

            if (!this.trailsAdded && this.scan is not { IsCompleted: false })
            {
                this.trailsAdded = true;
                foreach (var row in this.rows.Where(row => row.Line && row.After.Length == 0))
                {
                    var along = this.spots.Where(spot => spot.Row == row).Select(spot => spot.Cell).ToList();
                    if (along.Count > 0)
                    {
                        this.trails.Add(row, along, () => this.Goal(row));
                    }
                }
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var lines = new List<string>();
            foreach (var (row, cell) in this.spots.Concat(this.AwakeSpots(map)))
            {
                if (!row.Line && Vector2.Distance(here, cell) <= PassingCells && this.memory.Note($"{this.instance}|{row.Hint}", here))
                {
                    lines.Add(this.Line(row));
                }
            }

            var entries = this.catalog.Entries();
            foreach (var sign in entries.Where(entry => entry.Kind == MinimapMarks.SignKind))
            {
                var cell = new Vector2(sign.WorldX, sign.WorldY) / map.WorldToGridConvertor;
                if (Vector2.Distance(here, cell) <= PassingCells && this.memory.Note($"{this.instance}|sign|{sign.Id}", cell))
                {
                    lines.Add(this.SignLine(sign, entries));
                }
            }

            this.WatchRituals(map, here);
            this.WatchWayfinders(map, here);
            if (lines.Count > 0)
            {
                this.output.Say(SpeechKind.Places, string.Join(". ", lines), urgent: true); // through a fight, as discoveries
            }
        }

        /// <summary>What the dev command "maphints" logs: each cue found here and the side it would say from where the player stands.</summary>
        public IEnumerable<string> Diagnose()
        {
            yield return $"Map hints in {this.area}: {this.rows.Count} rows, {this.spots.Count} spots, {this.altars.Count} altars, {this.blockers.Count} blockers";
            foreach (var line in this.trails.Diagnose())
            {
                yield return line;
            }

            foreach (var group in this.spots.GroupBy(spot => spot.Row).Where(group => !group.Key.Line))
            {
                var first = group.First().Cell;
                yield return FormattableString.Invariant($"Hint {group.Key.Hint}: {group.Count()} spots, first at {first.X:0},{first.Y:0}: \"{Announcements.MapHint(group.Key.Hint, this.Side(group.Key))}\"");
            }
        }

        /// <summary>Tiles from the area's layout, then, off the frame, doodads: the client lists them for the whole map at arrival.</summary>
        private void FindSpots(AreaInstance map)
        {
            var (spots, blockers, altars, rows) = (this.spots, this.blockers, this.altars, this.rows);
            foreach (var row in rows.Where(row => row.Tiles.Count > 0))
            {
                foreach (var cell in map.TgtTilesLocations.Where(tile => row.Tiles.Any(part => tile.Key.Contains(part, StringComparison.OrdinalIgnoreCase))).SelectMany(tile => tile.Value))
                {
                    spots.Add((row, cell));
                }
            }

            if (rows.All(row => row.Match.Length == 0 && row.Blocked.Length == 0 && row.After.Length == 0))
            {
                return;
            }

            var (area, log) = (this.area, this.log);
            this.scan = Task.Run(() =>
            {
                try
                {
                    map.ScanSleepingEntities(_ => true, (_, entity) =>
                    {
                        var seen = entity.TryGetComponent<Animated>(out var animated) ? $"{entity.Path} {animated.ModelPath}" : entity.Path;
                        if (!entity.TryGetComponent<Render>(out var place))
                        {
                            return;
                        }

                        var cell = new Vector2(place.GridPosition.X, place.GridPosition.Y);
                        if (MapHintTable.Matching(rows, seen) is { } row)
                        {
                            spots.Add((row, cell));
                        }

                        if (rows.Any(row => row.Blocked.Length > 0 && seen.Contains(row.Blocked, StringComparison.OrdinalIgnoreCase)))
                        {
                            blockers.Add(cell);
                        }

                        if (seen.Contains(AltarPath, StringComparison.Ordinal))
                        {
                            altars.Add(cell);
                        }
                    });
                    log.Info($"Map hints in {area}: {spots.Count} spots, {altars.Count} altars, {blockers.Count} blockers");
                }
                catch (Exception ex)
                {
                    log.Warn($"Map hints: scan failed in {area}: {ex.Message}"); // the cues of this area stay silent, the rest of the mod runs on
                }
            });
        }

        /// <summary>Objects are entities the client loads near the player.</summary>
        private IEnumerable<(MapHintRow Row, Vector2 Cell)> AwakeSpots(AreaInstance map)
        {
            if (this.rows.All(row => row.Match.Length == 0))
            {
                yield break;
            }

            foreach (var entity in map.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<Render>(out var place))
                {
                    continue;
                }

                var path = entity.Path ?? string.Empty;
                var cell = new Vector2(place.GridPosition.X, place.GridPosition.Y);
                if (MapHintTable.Matching(this.rows, path) is { } row)
                {
                    yield return (row, cell);
                }
            }
        }

        private string Line(MapHintRow row)
        {
            var line = this.WithWay(Announcements.MapHint(row.Hint, this.Side(row)), this.SaySide ? this.Goal(row) : null);
            return row.Reveals && this.catalog.HintGoal(row.Goal) is { } goal && this.catalog.Reveal(goal)
                ? $"{line}. {Announcements.Discovered(goal)}"
                : line;
        }

        private string SignLine(ScanEntry sign, IReadOnlyList<ScanEntry> entries)
        {
            var row = this.rows.FirstOrDefault(row => row.Sign.Length > 0 && Announcements.Sign(this.catalog.Localized(row.Sign)) == sign.Name);
            var named = row == null ? entries.Concat(this.catalog.AreaLandmarks()).FirstOrDefault(entry => entry.Category == ScanCategory.Exits && Announcements.Sign(entry.Name) == sign.Name) : null;
            var goal = row != null ? this.Goal(row) : named;
            var side = this.SaySide && goal != null ? this.catalog.SideToward(goal) : string.Empty;
            return this.WithWay(Announcements.WithSide(sign.Name, side), this.SaySide ? goal : null);
        }

        /// <summary>A cue's line with the walk to where it leads, unless that is in view already.</summary>
        private string WithWay(string line, ScanEntry? goal)
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            return goal == null || toWorld <= 0 || this.catalog.IsSeen(new Vector2(goal.WorldX, goal.WorldY) / toWorld) ? line : $"{line}. {this.catalog.Describe(goal)}";
        }

        /// <summary>The side a cue goes, empty when it is not to be said or nothing says where it leads.</summary>
        private string Side(MapHintRow row) => this.SaySide && this.Goal(row) is { } goal ? this.catalog.SideToward(goal) : string.Empty;

        /// <summary>The place a cue leads to; null when the map does not tell.</summary>
        private ScanEntry? Goal(MapHintRow row)
        {
            if (row.Goal.Length == 0)
            {
                return null;
            }

            return row.Goal == MapHintTable.RitualGoal ? this.NextAltar() ?? this.catalog.HintGoal(MapHintTable.BossGoal)
                : this.catalog.HintGoal(row.Goal) ?? (row.GoalTile.Length > 0 ? this.OpenTile(row) : null);
        }

        /// <summary>The one tile of the goal's that nothing closes; null when none or several are open.</summary>
        private ScanEntry? OpenTile(MapHintRow row)
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            var open = this.catalog.HintTiles(row.GoalTile)
                .Where(tile => this.blockers.Count(blocker => Vector2.Distance(blocker, new Vector2(tile.WorldX, tile.WorldY) / toWorld) <= BlockedCells) < BlockedCount).ToList();
            return open.Count == 1 ? open[0] with { Name = this.catalog.Localized(row.Goal) } : null;
        }

        /// <summary>The nearest altar not finished, the boss's left out; null without one.</summary>
        private ScanEntry? NextAltar()
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var boss = this.catalog.HintGoal(MapHintTable.BossGoal);
            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            return this.rituals.Next(this.altars, here, boss == null ? null : new Vector2(boss.WorldX, boss.WorldY) / toWorld) is { } cell
                ? new ScanEntry(0, ScanCategory.Encounters, Announcements.RitualAltarName, cell.X * toWorld, cell.Y * toWorld, player.WorldPosition.Z)
                : null;
        }

        /// <summary>A ritual just finished sends its swarm on: a trail from here to the next one.</summary>
        private void WatchRituals(AreaInstance map, Vector2 here)
        {
            var rows = this.rows.Where(row => row.After.Length > 0).ToList();
            if (rows.Count == 0)
            {
                return;
            }

            foreach (var entity in map.AwakeEntities.Values)
            {
                if (!entity.IsValid || rows.FirstOrDefault(row => entity.Path?.Contains(row.After, StringComparison.Ordinal) == true) is not { } row ||
                    !entity.TryGetComponent<Render>(out var place) || !entity.TryGetComponent<StateMachine>(out var machine) ||
                    machine.States.FirstOrDefault(state => state.Name == AltarState) is not { } state)
                {
                    continue;
                }

                if (this.rituals.Finished(entity.Id, state.Value, new Vector2(place.GridPosition.X, place.GridPosition.Y)) && this.Goal(row) is { } goal)
                {
                    this.log.Info($"Ritual finished: the swarm goes to {goal.Name}");
                    this.trails.Add(row, Array.Empty<Vector2>(), () => goal);
                }
            }
        }
    }
}
