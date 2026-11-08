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
    public sealed class MapHintNarrator
    {
        /// <summary>A second of running is 37 cells, well inside <see cref="PassingCells"/>.</summary>
        private const int PollMs = 1000;

        /// <summary>The area's name goes first, as for discoveries (DiscoveryNarrator).</summary>
        private const int ArrivalMs = 2500;

        /// <summary>What the player sees walking by: the ground the mod counts as seen.</summary>
        private const float PassingCells = ExplorationMap.SeenRadiusCells;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly MapHintTable table;
        private readonly Func<string> areaId;
        private readonly PluginLog log;
        private readonly MapHintMemory memory = new();
        private IReadOnlyList<MapHintRow> rows = Array.Empty<MapHintRow>();

        /// <summary>Tiles and doodads of the rows, known at arrival.</summary>
        private ConcurrentBag<(MapHintRow Row, Vector2 Cell)> spots = new();

        /// <summary>Goals that are things, not places, where the client has shown them, by the row's goal.</summary>
        private ConcurrentDictionary<string, Vector2> goalThings = new(StringComparer.Ordinal);
        private string instance = string.Empty;
        private string area = string.Empty;
        private long arrivedAt;
        private long lastPoll;
        private bool scanned;

        public MapHintNarrator(EntityCatalog catalog, IAccessibilityOutputService output, MapHintTable table, Func<string> areaId, PluginLog log)
        {
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
                (this.spots, this.goalThings, this.scanned, this.arrivedAt) = (new(), new(StringComparer.Ordinal), false, now);
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

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var lines = new List<string>();
            foreach (var (row, cell) in this.spots.Concat(this.AwakeSpots(map)))
            {
                if (Vector2.Distance(here, cell) <= PassingCells && this.memory.Note($"{this.instance}|{row.Hint}", here))
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

            if (lines.Count > 0)
            {
                this.output.Say(SpeechKind.Places, string.Join(". ", lines), urgent: true); // through a fight, as discoveries
            }
        }

        /// <summary>What the dev command "maphints" logs: each cue found here and the side it would say from where the player stands.</summary>
        public IEnumerable<string> Diagnose()
        {
            yield return $"Map hints in {this.area}: {this.rows.Count} rows, {this.spots.Count} spots, goals seen {string.Join(", ", this.goalThings.Keys)}";
            foreach (var group in this.spots.GroupBy(spot => spot.Row))
            {
                var first = group.First().Cell;
                yield return FormattableString.Invariant($"Hint {group.Key.Hint}: {group.Count()} spots, first at {first.X:0},{first.Y:0}: \"{Announcements.MapHint(group.Key.Hint, this.Side(group.Key))}\"");
            }
        }

        /// <summary>Tiles from the area's layout, then, off the frame, doodads: the client lists them for the whole map at arrival.</summary>
        private void FindSpots(AreaInstance map)
        {
            var (spots, goals, rows) = (this.spots, this.goalThings, this.rows);
            foreach (var row in rows.Where(row => row.Tile.Length > 0))
            {
                foreach (var cell in map.TgtTilesLocations.Where(tile => tile.Key.Contains(row.Tile, StringComparison.OrdinalIgnoreCase)).SelectMany(tile => tile.Value))
                {
                    spots.Add((row, cell));
                }
            }

            if (rows.All(row => row.Match.Length == 0))
            {
                return;
            }

            var (area, log) = (this.area, this.log);
            Task.Run(() =>
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

                        NoteGoals(rows, goals, seen, cell);
                    });
                    log.Info($"Map hints in {area}: {spots.Count} spots, {goals.Count} goals seen");
                }
                catch (Exception ex)
                {
                    log.Warn($"Map hints: scan failed in {area}: {ex.Message}"); // the cues of this area stay silent, the rest of the mod runs on
                }
            });
        }

        /// <summary>A goal may be a thing: the Tree of Souls' roots are a quest object, known only near it (live, 2026-11-05).</summary>
        private static void NoteGoals(IEnumerable<MapHintRow> rows, ConcurrentDictionary<string, Vector2> goals, string seen, Vector2 cell)
        {
            foreach (var row in rows.Where(row => row.Goal.Length > 0 && seen.Contains(row.Goal, StringComparison.OrdinalIgnoreCase)))
            {
                goals[row.Goal] = cell;
            }
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
                NoteGoals(this.rows, this.goalThings, path, cell);
                if (MapHintTable.Matching(this.rows, path) is { } row)
                {
                    yield return (row, cell);
                }
            }
        }

        private string Line(MapHintRow row)
        {
            var line = Announcements.MapHint(row.Hint, this.Side(row));
            return row.Reveals && this.catalog.HintGoal(row.Goal) is { } goal && this.catalog.Reveal(goal)
                ? $"{line}. {Announcements.Discovered(goal)}"
                : line;
        }

        private string SignLine(ScanEntry sign, IReadOnlyList<ScanEntry> entries)
        {
            var row = this.rows.FirstOrDefault(row => row.Sign.Length > 0 && Announcements.Sign(this.catalog.Localized(row.Sign)) == sign.Name);
            var named = row == null ? entries.Concat(this.catalog.AreaLandmarks()).FirstOrDefault(entry => entry.Category == ScanCategory.Exits && Announcements.Sign(entry.Name) == sign.Name) : null;
            var goal = row != null ? this.catalog.HintGoal(row.Goal) : named;
            var side = this.SaySide && goal != null ? this.catalog.SideToward(goal) : string.Empty;
            return Announcements.WithSide(sign.Name, side);
        }

        /// <summary>The side a cue goes, empty when it is not to be said or nothing says where it leads.</summary>
        private string Side(MapHintRow row)
        {
            if (!this.SaySide || row.Goal.Length == 0)
            {
                return string.Empty;
            }

            return this.goalThings.TryGetValue(row.Goal, out var cell) ? this.catalog.SideToward(cell)
                : this.catalog.HintGoal(row.Goal) is { } goal ? this.catalog.SideToward(goal)
                : string.Empty;
        }
    }
}
