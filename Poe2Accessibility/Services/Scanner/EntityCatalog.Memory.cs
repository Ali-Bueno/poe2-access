namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>The scanner's area binding: an area instance's name, what it keeps between visits, its defeated bosses and the places inherited from another instance.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>
        ///     Exits and checkpoints brought from another instance of the same map (<see cref="AreaMemory.Inherit"/>): ids
        ///     above the mod's places, below the death place, until the game's own entity there is seen.
        /// </summary>
        private const uint InheritedIdBase = 0xB0000000;

        /// <summary>The game's entity this near an inherited place (world units) is that place in this instance.</summary>
        private const float InheritedMatchWorld = 400f;

        /// <summary>An inherited place the player stands this near with no such entity awake is not in this instance.</summary>
        private const float InheritedGoneWorld = 500f;

        /// <summary>The explored ground is written this often while playing, so a host restart loses little.</summary>
        private const int SaveExploredMs = 15000;

        /// <summary>An area's name this soon after its instance was bound unnamed belongs to that instance (its loading screen lagged).</summary>
        private const long LateNameMs = 15000;

        /// <summary>An area entered before the game had its instance loaded; that instance takes the name.</summary>
        private string? pendingName;
        private long unnamedAt = long.MinValue / 2;

        /// <summary>The area this instance was first entered from: the exit back there is not the way on.</summary>
        private string cameFrom = string.Empty;

        /// <summary>What each area instance left held, to restore it on return (set by the plugin).</summary>
        public AreaMemory? Memory { get; set; }

        /// <summary>
        ///     The game entered the area <paramref name="name"/> (its loading screen): bound to the instance the game holds
        ///     once that instance is the new one, which may come later than the name.
        /// </summary>
        public void AreaEntered(string name)
        {
            if (this.Rebind(name))
            {
                return;
            }

            if (Environment.TickCount64 - this.unnamedAt < LateNameMs)
            {
                this.areaName = name;
                this.unnamedAt = long.MinValue / 2;
            }
            else
            {
                this.pendingName = name;
            }
        }

        /// <summary>
        ///     Binds the scanner to the instance the game holds when it is another (hash or map): what the old one held is
        ///     kept, and what was known of the new one returns if its map is the same; true when it switched.
        /// </summary>
        private bool Rebind(string? name)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var hash = area.AreaHash;
            var tiles = area.TgtTilesLocations;
            if (hash.Length == 0 || (hash == this.instance && ReferenceEquals(tiles, this.layoutTiles)))
            {
                return false;
            }

            var layout = AreaMemory.Layout(tiles, area.TerrainMetadata.BytesPerRow, area.GridWalkableData.Length);
            this.layoutTiles = tiles;
            if (hash == this.instance && layout == this.layout)
            {
                return false;
            }

            if (this.Memory != null && this.instance.Length > 0)
            {
                this.Memory.Keep(this.instance, this.Snapshot());
            }

            var leaving = this.areaName;
            this.Clear();
            name ??= this.pendingName;
            var frame = AreaMemory.Frame(tiles, area.TerrainMetadata.BytesPerRow, area.GridWalkableData.Length);
            var known = this.Memory?.Recall(hash, layout);
            var inherited = known == null ? this.Memory?.Inherit(frame, hash) : null;
            if (inherited is { } same)
            {
                // The same campaign map in a new instance (Clearfell: exits and checkpoints at the same cells, other
                // tiles varied; testers heard its exits forgotten, 2026-11-02): the exits and checkpoints seen there, at
                // their places, until the game's own are seen again. Not the explored ground: Beira's arena stood
                // elsewhere in the next instance and was announced "on the map" before the player saw it.
                var index = 0u;
                foreach (var entry in AreaMemory.Framed(same.Snapshot))
                {
                    var id = InheritedIdBase + index++;
                    this.remembered[id] = entry with { Id = id };
                }
            }

            if (known != null)
            {
                this.explored.Load(known.Explored);
                foreach (var (id, entry) in known.Remembered)
                {
                    this.remembered[id] = entry;
                }

                this.sighted.UnionWith(known.Sighted);
                lock (this.samplesLock)
                {
                    foreach (var (id, at) in known.ClosedBarriers)
                    {
                        this.closedBarriers[id] = at;
                    }
                }
            }

            this.cameFrom = known?.CameFrom ?? leaving;
            (this.instance, this.layout, this.frame, this.areaName) = (hash, layout, frame, name ?? leaving);
            this.unnamedAt = name == null ? Environment.TickCount64 : long.MinValue / 2;
            this.pendingName = null;
            this.savedAt = Environment.TickCount64;
            this.log.Info(known != null
                ? $"Instance {hash} ({this.areaName}): same map, {known.Remembered.Count} places and its explored ground restored"
                : this.remembered.Count > 0 || inherited != null
                    ? $"Instance {hash} ({this.areaName}): new instance of the map of {inherited?.Instance} (frame {frame:X16}), its {this.remembered.Count} exits and checkpoints kept"
                    : $"Instance {hash} ({this.areaName}): new map {layout:X16}, frame {frame:X16}, unexplored");
            return true;
        }

        /// <summary>A copy of what is known of this instance, for <see cref="AreaMemory"/>.</summary>
        private AreaMemory.Snapshot Snapshot()
        {
            lock (this.samplesLock)
            {
                return new AreaMemory.Snapshot(this.layout, this.explored.Save(), this.cameFrom, new(this.remembered), new(this.sighted), new(this.closedBarriers), this.frame);
            }
        }

        /// <summary>Names of the bosses whose defeat was seen in this area instance (<see cref="BossDefeated"/>).</summary>
        private readonly HashSet<string> defeatedBosses = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     A boss's defeat was seen in this area (the boss watch calls this): its room among the area's tiles is said as
        ///     defeated from now on, without the radar table's reward note, until the instance is left.
        /// </summary>
        public void BossDefeated(string name)
        {
            lock (this.defeatedBosses)
            {
                if (name.Length > 0)
                {
                    this.defeatedBosses.Add(name);
                }
            }
        }

        private List<string> DefeatedBosses()
        {
            lock (this.defeatedBosses)
            {
                return this.defeatedBosses.ToList();
            }
        }

        private void Clear()
        {
            lock (this.defeatedBosses)
            {
                this.defeatedBosses.Clear();
            }

            this.remembered.Clear();
            this.sighted.Clear();
            this.itemTitles.Clear();
            this.explored.Reset();
            this.questTargets = Array.Empty<ScanEntry>();
            lock (this.samplesLock)
            {
                this.grid.Reset();
                this.closedBarriers.Clear();
            }
            RoutePlanner.ReleaseWorkspaces();
            this.unexplored = Array.Empty<ScanEntry>();
            this.unexploredAt = long.MinValue / 2;
            this.frontierSearch = null; // what it finds is another area's: the map it read is no longer this one
            this.frontierFor = null;
        }

        /// <summary>
        ///     An exit or checkpoint inherited from another instance of this map stays until the game's own entity of that
        ///     name is listed beside it, or the player stands by its place and the game has none there.
        /// </summary>
        private static bool KeepInherited(ScanEntry inherited, Vector2 here, IEnumerable<ScanEntry> listed, IEnumerable<ScanEntry> unseen)
        {
            var at = new Vector2(inherited.WorldX, inherited.WorldY);
            bool Same(ScanEntry entry) => IsEntity(entry) && entry.Category == inherited.Category &&
                                          string.Equals(entry.Name, inherited.Name, StringComparison.OrdinalIgnoreCase) &&
                                          Vector2.Distance(at, new Vector2(entry.WorldX, entry.WorldY)) <= InheritedMatchWorld;
            return !listed.Any(Same) && (unseen.Any(Same) || Vector2.Distance(here, at) > InheritedGoneWorld);
        }
    }
}
