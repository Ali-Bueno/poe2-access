namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The map's shape for the radar and the forks: corridors, forks, rooms, dead ends and narrow passages, worked out once
    ///     per area from the whole walkable grid (<see cref="MapTopology"/>, docs/research/map-topology.md).
    /// </summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>
        ///     A dead end is told only when a look shows it whole, as a sighted player sees it end on screen: its side no
        ///     longer than the screen is high (two reveal radii) and no larger than two reveal discs. Larger ones are walked.
        /// </summary>
        private const float SeenWholeExtentCells = 2 * ExplorationMap.SeenRadiusCells;

        private const int SeenWholeAreaCells = 2 * MapTopology.RevealDiscCells;

        /// <summary>A ray's way is judged this far along it (cells): far enough to be past a doorway, near enough to be the ray's.</summary>
        private const float RayProbeCells = 30f;

        /// <summary>The goals a dead end must not hold are counted again this often.</summary>
        private const int GoalsRefreshMs = 2000;

        /// <summary>Grids larger than this (cells) are not worked out: the build takes about 10 bytes a cell (a Trial floor).</summary>
        private const long MaxTopologyCells = 24L << 20;

        /// <summary>A narrow passage is underfoot this near its narrowest cell, past its half width (cells; a step and a half).</summary>
        private const float NarrowReachCells = 6f;

        /// <summary>The ways of the forks this near (cells, a look) tell which way the map runs around the player.</summary>
        private const float LayoutReachCells = ExplorationMap.SeenRadiusCells;

        private MapTopology? topology;
        private string topologyFor = string.Empty;
        private Task<MapTopology?>? topologyBuild;
        private CancellationTokenSource? topologyCancel;
        private long goalsAt = long.MinValue / 2;

        /// <summary>The current area's shape once worked out; null before, or for a grid too large.</summary>
        public MapTopology? Topology()
        {
            if (this.topologyBuild is { IsCompleted: true } done)
            {
                this.topologyBuild = null;
                if (done.IsCompletedSuccessfully && done.Result is { } built)
                {
                    this.topology = built;
                    this.goalsAt = long.MinValue / 2;
                }
                else if (done.IsFaulted)
                {
                    this.log.Info($"Map shape not worked out: {done.Exception?.GetBaseException().Message}");
                }
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (this.instance.Length == 0 || area.AreaHash != this.instance)
            {
                return null;
            }

            if (this.topologyFor != this.instance && this.topologyBuild == null && this.TryLiveGrid(out var grid, out var row))
            {
                this.topologyFor = this.instance;
                this.topology = null;
                this.topologyCancel?.Cancel();
                if ((long)grid.Length * 2 > MaxTopologyCells)
                {
                    this.log.Info($"Map shape not worked out: {grid.Length * 2} cells is too large a grid");
                    return null;
                }

                var cancel = this.topologyCancel = new CancellationTokenSource();
                var started = Environment.TickCount64;
                this.topologyBuild = Task.Run(() =>
                {
                    var built = MapTopology.Build(grid, row, grid.Length / row, cancel.Token);
                    this.log.Info($"Map shape worked out in {Environment.TickCount64 - started} ms: {built.Nodes.Count} places, {built.Edges.Count} ways, {built.Narrows.Count} narrow passages");
                    return (MapTopology?)built;
                });
            }

            if (this.topology is { } shape && Environment.TickCount64 - this.goalsAt >= GoalsRefreshMs)
            {
                this.goalsAt = Environment.TickCount64;
                shape.SetGoals(this.TopologyGoals(area));
            }

            return this.topology;
        }

        /// <summary>
        ///     Whether the way along a radar ray leads into a dead end a look shows whole (no exit, objective, boss room or shut
        ///     door in it). <paramref name="from"/> and <paramref name="axis"/> in grid cells.
        /// </summary>
        public bool DeadEndToward(Vector2 from, Vector2 axis)
        {
            if (this.Topology() is not { } shape || !this.TryLiveGrid(out var grid, out var row) || axis.LengthSquared() < 1e-6f)
            {
                return false;
            }

            // The farthest walkable cell along the ray within the probe: the ray's way, not a wall behind it.
            var direction = Vector2.Normalize(axis);
            Vector2? probe = null;
            for (var step = 4f; step <= RayProbeCells; step += 2f)
            {
                var cell = from + (direction * step);
                if (!Walkable(grid, row, cell))
                {
                    break;
                }

                probe = cell;
            }

            return probe is { } to && shape.DeadEndToward(from, to) is { } end && SeenWhole(end);
        }

        /// <summary>
        ///     Whether the ways near a cell run along the screen's diagonals: true when nearly all of them do (a corridor of a
        ///     grid-built map, Ogham Manor), false when a fair share runs straight, null when unclear or no fork is near.
        ///     An opening beside a corridor walked toward is one straight way among diagonal ones, so it does not turn the rays
        ///     (the owner, 2026-11-16).
        /// </summary>
        public bool? WaysRunDiagonal(Vector2 cell)
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (this.Topology() is not { } shape || toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var (diagonal, straight) = (0, 0);
            foreach (var node in shape.Nodes)
            {
                if (Vector2.Distance(node.Centre, cell) > LayoutReachCells)
                {
                    continue;
                }

                foreach (var way in shape.Ways(node.Id))
                {
                    if (ScreenProjection.Octant(player.WorldPosition, way.Direction * toWorld) % 2 == 1)
                    {
                        diagonal++;
                    }
                    else
                    {
                        straight++;
                    }
                }
            }

            return MapTopology.RunDiagonal(diagonal, straight);
        }

        /// <summary>A dead end a look shows whole.</summary>
        public static bool SeenWhole(DeadEnd end) => end.ExtentCells <= SeenWholeExtentCells && end.AreaCells <= SeenWholeAreaCells;

        /// <summary>
        ///     A way out of a place is explored once the next place along it and the first stretch of each of its ways were seen:
        ///     a look from the place already reveals 60 cells of every way, so the way's own first cell tells nothing.
        /// </summary>
        public bool WayExplored(MapTopology shape, int from, Way way)
        {
            if (!this.WasSeen(way.Ahead) || !this.WasSeen(shape.Nodes[way.To].Centre))
            {
                return false;
            }

            return shape.Ways(way.To).Where(next => next.To != from && !next.Nook).All(next => this.WasSeen(next.Ahead));
        }

        /// <summary>Whether a grid cell was seen (the explored ground).</summary>
        public bool WasSeen(Vector2 cell) => this.explored.IsSeen(cell);

        /// <summary>"narrow" where the player stands in a narrow passage between two parts of the map; null elsewhere.</summary>
        public string? NarrowAt(Vector2 cell) =>
            this.topology?.Narrows.Any(narrow => Vector2.Distance(narrow.Cell, cell) <= (narrow.WidthCells / 2) + NarrowReachCells) == true ? LandmarkAhead.Narrow : null;

        /// <summary>The narrow passages, as places to name ahead.</summary>
        public IEnumerable<(Vector2 Grid, string Kind)> NarrowPlaces() =>
            this.topology?.Narrows.Select(narrow => (narrow.Cell, LandmarkAhead.Narrow)) ?? Enumerable.Empty<(Vector2, string)>();

        /// <summary>Where a dead end must not be: exits, what the objective names, boss rooms not found, doors not opened.</summary>
        private IEnumerable<Vector2> TopologyGoals(AreaInstance area)
        {
            var toWorld = area.WorldToGridConvertor;
            if (toWorld <= 0)
            {
                return Array.Empty<Vector2>();
            }

            var goals = this.Entries().Concat(this.AreaLandmarks())
                .Where(entry => entry.Category == ScanCategory.Exits || (entry.Kind == MinimapMarks.BossRoomKind && !this.sighted.Contains(entry.Id)))
                .Concat(this.questTargets);
            if (Seats.Followed.TryGetComponent<Render>(out var player))
            {
                goals = goals.Concat(this.UnopenedDoors(area, player));
            }

            return goals.Select(entry => new Vector2(entry.WorldX, entry.WorldY) / toWorld).ToList();
        }

        private static bool Walkable(byte[] grid, int row, Vector2 cell)
        {
            var (x, y) = ((int)cell.X, (int)cell.Y);
            var index = (y * row) + (x >> 1);
            if (x < 0 || y < 0 || x >= row * 2 || index >= grid.Length)
            {
                return false;
            }

            return ((x & 1) == 0 ? grid[index] & 0xF : grid[index] >> 4) != 0;
        }
    }
}
