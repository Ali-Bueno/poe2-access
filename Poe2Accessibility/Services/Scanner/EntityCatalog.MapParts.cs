namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>The scanner's view of the whole map: the part the player is in, how each part stands, and a glance at it all in words.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>A glance names this many unexplored stretches, the first of the scanner's own order.</summary>
        private const int GlanceStretches = 3;

        /// <summary>The map's frame of an area instance and grid size: working it out sorts every block of the grid.</summary>
        private (string Instance, int Cells, MapFrame? Frame) mapFrame = (string.Empty, 0, null);

        /// <summary>The map's frame and the player's cell on it; null outside gameplay or without ground. Cheap once the frame is known.</summary>
        public (MapFrame Frame, Vector2 Cell)? PlaceOnMap()
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player) || !this.TryLiveGrid(out var grid, out var row))
            {
                return null;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (this.mapFrame.Instance != area.AreaHash || this.mapFrame.Cells != grid.Length)
            {
                var (alongX, alongY) = ScreenProjection.Axes(player.WorldPosition);
                this.mapFrame = (area.AreaHash, grid.Length, MapCorner.Frame(grid, row, alongX, alongY));
            }

            this.Unexplored(area, player); // keeps the reachable ground fresh for MapParts
            return this.mapFrame.Frame is { } frame ? (frame, new Vector2(player.GridPosition.X, player.GridPosition.Y)) : null;
        }

        /// <summary>
        ///     The area is a floor of rooms apart, each its own ground (a Trial of the Sekhemas floor): thirds of the whole
        ///     floor are no parts of the room the player is in (set by the plugin).
        /// </summary>
        public Func<bool> RoomsApart { get; set; } = () => false;

        /// <summary>How the parts of the map stand (<see cref="ExplorationMap.Parts"/>): it reads every block, so on a part entered or a glance asked.</summary>
        public IReadOnlyList<MapPart> MapParts(MapFrame frame) => this.RoomsApart() ? Array.Empty<MapPart>() : this.explored.Parts(frame);

        /// <summary>Whether an entity stands on the ground the player can walk to (<see cref="ExplorationMap.OnPlayersGround"/>); null while not known.</summary>
        private bool? OnPlayersGround(Render render) =>
            Seats.Followed.TryGetComponent<Render>(out var player)
                ? this.explored.OnPlayersGround(new Vector2(render.GridPosition.X, render.GridPosition.Y), new Vector2(player.GridPosition.X, player.GridPosition.Y))
                : null;

        /// <summary>
        ///     What a look at the map tells, in words: how much is explored, the part the player is in, the parts with ground
        ///     left, and the nearest unexplored stretches with their way. Null outside gameplay.
        /// </summary>
        public string? MapGlance()
        {
            if (this.PlaceOnMap() is not { } place || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var stretches = this.Unexplored(Core.States.InGameStateObject.CurrentAreaInstance, player)
                .Take(GlanceStretches).Select(entry => this.Describe(entry)).ToList();
            var here = place.Frame.Part(place.Cell, ExplorationMap.SeenRadiusCells);
            return Announcements.MapGlance(this.ExploredPercent(), here, this.MapParts(place.Frame), stretches);
        }
    }
}
