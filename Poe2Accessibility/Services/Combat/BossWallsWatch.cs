namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The walls a boss raises to shut the player in (Kabala's bone cage; <see cref="BossMechanicsCatalog.IsWall"/>,
    ///     docs/research/boss-mechanics.md §13): once they stand, the way out is said with its direction, or that a wall must
    ///     be broken when there is none, and <see cref="Target"/> is the place the heartbeat leads to until the player is out.
    /// </summary>
    public sealed class BossWallsWatch
    {
        /// <summary>The walls rise one after another (200 ms apart in the files): their count and the way out hold this long (ms) before they are said.</summary>
        private const int SettleMs = 600;

        /// <summary>The ground is searched again this often (ms) while walled in.</summary>
        private const int SearchMs = 450;

        private readonly PluginLog log;
        private int count;
        private long countSince;
        private long lastSearch;
        private WallWay? found;
        private long foundSince;
        private WallWay? said;

        public BossWallsWatch(PluginLog log)
        {
            this.log = log;
        }

        /// <summary>Where to walk to (world): the opening, or the nearest wall to break; null when not walled in.</summary>
        public Vector2? Target { get; private set; }

        public void Reset()
        {
            this.count = 0;
            this.found = null;
            this.said = null;
            this.Target = null;
        }

        /// <summary>After a poll, with the walls in play (world): the line to say, when the way out is new.</summary>
        public string? Poll(IReadOnlyList<Vector2> walls, long now, bool heartbeat)
        {
            if (walls.Count != this.count)
            {
                this.count = walls.Count;
                this.countSince = now;
            }

            if (walls.Count < WallRing.MinWalls)
            {
                if (this.said != null)
                {
                    this.log.Info("Boss walls: gone");
                }

                this.found = null;
                this.said = null;
                this.Target = null;
                return null;
            }

            if (now - this.lastSearch < SearchMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            this.lastSearch = now;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var cell = area.WorldToGridConvertor;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var exit = WallRing.Read(area.GridWalkableData, area.TerrainMetadata.BytesPerRow, here / cell, walls.Select(wall => wall / cell).ToList());
            if (exit?.Way != this.found)
            {
                this.found = exit?.Way;
                this.foundSince = now;
            }

            if (this.said is WallWay.Open or WallWay.Closed)
            {
                // Said already: the beat follows the freshest way (the opening as the walls change, the wall nearest now).
                this.Target = exit is { Way: not WallWay.Out } way ? way.At * cell : null;
            }

            if (exit is not { } read || now - this.countSince < SettleMs || now - this.foundSince < SettleMs || read.Way == this.said)
            {
                return null;
            }

            var before = this.said;
            this.said = read.Way;
            this.log.Info($"Boss walls: {walls.Count} walls, {read.Way}, to {read.At.X * cell:0},{read.At.Y * cell:0}, {Vector2.Distance(read.At * cell, here):0} world units from the player");
            if (read.Way == WallWay.Out)
            {
                this.Target = null;
                return before == null ? null : Announcements.BossWallsLeft(); // never walled in: nothing to say
            }

            this.Target = read.At * cell;
            return Announcements.BossWalls(read.Way == WallWay.Open, ScreenProjection.Octant(player.WorldPosition, this.Target.Value - here), heartbeat);
        }
    }
}
