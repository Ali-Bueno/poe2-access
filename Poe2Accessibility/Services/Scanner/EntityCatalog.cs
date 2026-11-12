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

    /// <summary>
    ///     The scanner's view of the area: GameHelper's awake entities sorted into <see cref="ScanCategory"/>,
    ///     named as the game names them, nearest first. Only what a sighted player has seen (the minimap shows no
    ///     more): enemies while on screen, everything else once it has been on screen or on explored ground, and the
    ///     area's named tiles once their ground is explored. Exits, waypoints and NPCs then stay listed at their last
    ///     known place (the game unloads far entities), for as long as the area instance lives (<see cref="AreaMemory"/>).
    /// </summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>Landmark ids live above entity ids so the two never collide.</summary>
        private const uint LandmarkIdBase = 0x80000000;

        /// <summary>Live walkable grids kept for routing, one per SampleMs: a cell must be walkable in all of them.</summary>
        private const int MaxSamples = 4;

        private const int SampleMs = 500;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly InventoryReader inventory;
        private readonly LandmarkTable landmarks;
        private readonly Func<string> areaId;
        private readonly PluginLog log;
        private readonly Dictionary<uint, ScanEntry> remembered = new();
        /// <summary>Entities and named tiles the player has seen in this area (see <see cref="Sighted"/>).</summary>
        private readonly HashSet<uint> sighted = new();
        /// <summary>
        ///     Title of each ground item seen in this area ("Pergamino de sabiduría") → its spoken name ("Pergamino de
        ///     sabiduría, 3"; empty for gold, which its pick-up says), kept past the pick-up: its HUD tooltip can come later.
        /// </summary>
        private readonly Dictionary<string, string> itemTitles = new(StringComparer.Ordinal);
        private readonly ExplorationMap explored = new();

        /// <summary>What the objective names, seen or not, as of the last <see cref="Entries"/>: the lost hint's goal.</summary>
        private IReadOnlyList<ScanEntry> questTargets = Array.Empty<ScanEntry>();
        /// <summary>The recent live walkable grids, the barrier memory and the grid combined from them (see WalkableGrid).</summary>
        private readonly GridSamples grid = new(MaxSamples, SampleMs);
        /// <summary>Closed barriers seen in this area by entity id (grid position), kept past the network bubble.</summary>
        private readonly Dictionary<uint, Vector2> closedBarriers = new();
        private readonly object samplesLock = new();

        /// <summary>Where each sample of the live grid is read (<see cref="ReadLiveGrid"/>); never a sample itself.</summary>
        private byte[] liveGrid = Array.Empty<byte>();

        private long savedAt;

        /// <summary>The area instance being read (AreaInstance.AreaHash), its map (<see cref="AreaMemory.Layout"/>) and its name.</summary>
        private string instance = string.Empty;
        private ulong layout;

        /// <summary>Its exits' frame (<see cref="AreaMemory.Frame"/>): another instance with the same one is the same map.</summary>
        private ulong frame;
        private string areaName = string.Empty;

        /// <summary>The tile table the layout was last computed from: a new one is a reloaded area.</summary>
        private object? layoutTiles;

        public EntityCatalog(HostMemoryReader memory, UiElementReader elements, InventoryReader inventory, LandmarkTable landmarks, Func<string> areaId, PluginLog log)
        {
            this.QuestTexts = () => Array.Empty<string>();
            this.log = log;
            this.memory = memory;
            this.elements = elements;
            this.inventory = inventory;
            this.landmarks = landmarks;
            this.areaId = areaId;
        }

        /// <summary>Checks the entity name field once enough named things are awake (set by the plugin).</summary>
        public LayoutProbe? Probe { get; set; }

        /// <summary>Finds the in-play flag and the quest marker again after a game update (set by the plugin).</summary>
        public EntityFields? Fields { get; set; }

        /// <summary>The quest tracker's texts on screen ("Find Clearfell", "Slay the Bloated Miller"), for the Quest category.</summary>
        public Func<IReadOnlyList<string>> QuestTexts { get; set; }

        /// <summary>
        ///     Whether the HUD counts the Death Crystals left: the room asks for them even when the page of the tracker on
        ///     screen shows other quests than the Trial's (set by the plugin).
        /// </summary>
        public Func<bool> CrystalsCounted { get; set; } = () => false;

        /// <summary>The area's named places (<see cref="PlaceNarrator.Entries"/>), set by the plugin.</summary>
        public Func<float, IEnumerable<ScanEntry>>? PlaceEntries { get; set; }

        /// <summary>The ways that opened and are still to be walked to, for the player's world position (<see cref="OpenedWays.Update"/>).</summary>
        public Func<float, float, IReadOnlyList<ScanEntry>>? OpenedWayEntries { get; set; }

        /// <summary>The current area's name in the client language (its name in an objective is not a destination).</summary>
        public Func<string> AreaName { get; set; } = () => string.Empty;

        /// <summary>How much ground has been seen in this area: it grows while the player finds new ground.</summary>
        public int SeenBlocks => this.explored.SeenBlocks;

        /// <summary>
        ///     The spoken name of the ground item whose tooltip holds these texts (empty for gold), read now from the item
        ///     the game targets or the one in view (<see cref="GroundTooltip"/>); null when none does.
        /// </summary>
        public string? ItemOfTooltip(IEnumerable<string> texts)
        {
            var shown = texts.ToList();
            if (!shown.Any(this.itemTitles.ContainsKey))
            {
                return null; // no title of an item seen here: the ground is not read for every text the HUD shows
            }

            GroundTooltip.Item? target = null;
            var inView = new List<GroundTooltip.Item>();
            foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (this.GroundItemOf(entity) is { } item)
                {
                    inView.Add(item);
                    target = this.IsTargeted(entity) ? item : target;
                }
            }

            return GroundTooltip.Name(shown, target, inView, title => this.itemTitles.TryGetValue(title, out var spoken) ? spoken : null);
        }

        /// <summary>A thing in the world, not a named place of the area's tiles.</summary>
        public static bool IsEntity(ScanEntry entry) => entry.Id < LandmarkIdBase;
    }
}
