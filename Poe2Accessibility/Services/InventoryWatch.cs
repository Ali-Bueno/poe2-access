namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     What arrives in the main inventory while playing (D2R inventory arrival notices): "Recogido: Traje de
    ///     ermitaño" when an item is picked up, "Recogido: 3 Pergamino de sabiduría" when a stack grows. The
    ///     inventory is GameHelper's own reader (<c>ServerData.PlayerInventories[MainInventory1]</c>, internal, so
    ///     reached by reflection like the rest of the host's internals); the first reading of an area is silent.
    ///     Only while no panel is open: moving things around in the inventory is not news.
    /// </summary>
    public sealed class InventoryWatch
    {
        private const int PollMs = 250;

        private static readonly PropertyInfo? PlayerInventoriesProperty =
            typeof(ServerData).GetProperty("PlayerInventories", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly InventoryReader reader;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly Dictionary<IntPtr, int> known = new();
        private const int HandPollMs = 100;

        /// <summary>The emptied cell is read about 100 ms after the pick-up (2026-10-24); a hold of 1.5 s queued the moves after it.</summary>
        private const int InHandHoldMs = 400;

        private Inventory? inventory;
        private bool baseline = true;
        private long lastPoll;

        /// <summary>The item on the cursor (D2R "item in hand"): GameHelper's reader of the Cursor1 inventory.</summary>
        private Inventory? cursor;
        private IntPtr held;
        private bool handKnown;
        private long lastHandPoll;

        public InventoryWatch(InventoryReader reader, IAccessibilityOutputService output, PluginLog log)
        {
            this.reader = reader;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area: the inventory is read again silently.</summary>
        public void Reset()
        {
            this.known.Clear();
            this.baseline = true;
            this.movesBaseline = true;
            this.weaponSet = -1;
        }

        /// <summary>Per frame; <paramref name="playing"/> false while a panel is open (the baseline still follows).</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs || !this.TryInventory(out var items))
            {
                return;
            }

            this.lastPoll = now;
            var present = new HashSet<IntPtr>();
            foreach (var (address, item) in items)
            {
                present.Add(address);
                var count = item.TryGetComponent<Stack>(out var stack) && stack.Count > 0 ? stack.Count : 1;
                var before = this.known.TryGetValue(address, out var had) ? had : 0;
                this.known[address] = count;
                if (!this.baseline && playing && count > before)
                {
                    var name = this.reader.DescribeItemEntity(item.Address) ?? item.Path; // keys are inventory slots, the entity is the item's address
                    this.output.Say(SpeechKind.GroundItems, Announcements.PickedUp(name, before == 0 ? 0 : count - before));
                }
            }

            foreach (var gone in new List<IntPtr>(this.known.Keys))
            {
                if (!present.Contains(gone))
                {
                    this.known.Remove(gone);
                }
            }

            this.baseline = false;
        }

        /// <summary>
        ///     Per frame in game, panels open or not: "In hand: Scroll of Wisdom, 3. Choose a cell…" when the cursor takes
        ///     an item, "You no longer hold an item" when it lets go (where it went is not guessed). An unreadable
        ///     cursor never counts as a release.
        /// </summary>
        public void TickHand(bool enabled)
        {
            var now = Environment.TickCount64;
            if (!enabled || now - this.lastHandPoll < HandPollMs)
            {
                return;
            }

            this.lastHandPoll = now;
            if (this.handEmptyDue != 0 && now >= this.handEmptyDue)
            {
                this.handEmptyDue = 0;
                this.output.Speak(Announcements.HandEmpty, true);
            }

            if (!this.TryCursor(out var items))
            {
                this.handKnown = false;
                return;
            }

            var item = IntPtr.Zero;
            Item? entity = null;
            foreach (var (address, candidate) in items)
            {
                (item, entity) = (address, candidate);
                break;
            }

            if (item == this.held)
            {
                this.handKnown = true;
                return;
            }

            if (item != IntPtr.Zero && entity != null)
            {
                this.handEmptyDue = 0;
                // Held only past the readout of the cell it left (now empty); the next D-pad move cuts it.
                this.output.Urge(Announcements.InHand(this.reader.DescribeItemEntity(entity.Address) ?? entity.Path), InHandHoldMs); // the name carries the stack
            }
            else if (this.handKnown)
            {
                // Where it went is not guessed here; an equipment slot taking it is said by TickMoves ("Equipado: …")
                // instead, so the release waits for that batch.
                this.handEmptyDue = now + HandEmptyWaitMs;
            }

            this.held = item;
            this.handKnown = true;
        }

        /// <summary>
        ///     Per frame in game (D2R arrival notices). The game makes a new item entity whenever an item changes
        ///     inventory, so a move is a departure and an arrival read together once the inventories have been still
        ///     for <see cref="SettleMs"/> (which also lets the focus update the move causes be said first): an arrival
        ///     in an equipment slot is "Equipado: Ballesta improvisada, Mano principal"; one in the main inventory is
        ///     "Desequipado: …" beside an equipment departure, a placement beside a cursor departure (the hand's), and
        ///     with a panel open otherwise "Nuevo en el inventario: …" (bought, withdrawn); a main-inventory departure
        ///     with nothing taking it (hand, equipment) and a panel open is "… salió del inventario" (sold, stored).
        ///     Pick-ups during play are Tick's.
        /// </summary>
        public void TickMoves(bool playing, bool enabled)
        {
            var now = Environment.TickCount64;
            if (now - this.lastMovesPoll < PollMs)
            {
                return;
            }

            this.lastMovesPoll = now;
            var server = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject;
            if (PlayerInventoriesProperty?.GetValue(server) is not Dictionary<InventoryName, IntPtr> inventories)
            {
                return;
            }

            var located = new Dictionary<IntPtr, InventoryName>();
            foreach (var name in Tracked)
            {
                if (this.Reader(inventories, name) is { } reader)
                {
                    foreach (var (_, item) in reader.Items)
                    {
                        located[item.Address] = name;
                    }
                }
            }

            if (!this.movesBaseline && enabled)
            {
                foreach (var (entity, from) in this.where)
                {
                    if (!located.ContainsKey(entity))
                    {
                        this.departures.Add((from, this.NameOf(entity)));
                        (this.lastMoveAt, this.movedInPanel) = (now, this.movedInPanel || !playing);
                    }
                }

                foreach (var (entity, to) in located)
                {
                    if (!this.where.ContainsKey(entity))
                    {
                        this.arrivals.Add((to, this.NameOf(entity)));
                        (this.lastMoveAt, this.movedInPanel) = (now, this.movedInPanel || !playing);
                    }
                }

                if ((this.arrivals.Count > 0 || this.departures.Count > 0) && now - this.lastMoveAt >= SettleMs)
                {
                    this.SpeakMoves();
                }
            }
            else
            {
                this.arrivals.Clear();
                this.departures.Clear();
            }

            foreach (var gone in new List<IntPtr>(this.names.Keys))
            {
                if (!located.ContainsKey(gone))
                {
                    this.names.Remove(gone);
                }
            }

            foreach (var entity in located.Keys)
            {
                this.NameOf(entity); // read while it is there: a swapped-out item's entity is gone ("Unequipped: unknown item")
            }

            this.TickWeaponSet(located, enabled);
            this.where = located;
            this.movesBaseline = false;
        }

        /// <summary>The weapon set in use (Stats component, GameHelper's CurrentWeaponIndex: 0 or 1); -1 until read.</summary>
        private int weaponSet = -1;

        /// <summary>
        ///     D2R weapon swap: RT in the inventory changes the set in use with nothing on screen to read but the slots;
        ///     the new set is said with what it holds ("Set de armas 2, vacío", 2026-10-24).
        /// </summary>
        private void TickWeaponSet(Dictionary<IntPtr, InventoryName> located, bool enabled)
        {
            if (!Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Stats>(out var stats) ||
                stats.CurrentWeaponIndex is not (0 or 1))
            {
                return; // a moved offset reads anything else
            }

            var previous = this.weaponSet;
            this.weaponSet = stats.CurrentWeaponIndex;
            if (previous < 0 || previous == this.weaponSet || !enabled)
            {
                return;
            }

            var (main, off) = this.weaponSet == 0 ? (InventoryName.Weapon1, InventoryName.Offhand1) : (InventoryName.Weapon2, InventoryName.Offhand2);
            var held = located.Where(pair => pair.Value == main || pair.Value == off)
                .OrderBy(pair => pair.Value == main ? 0 : 1)
                .Select(pair => this.NameOf(pair.Key))
                .Distinct()
                .ToList();
            this.output.Speak(Announcements.WeaponSet(this.weaponSet + 1, held), true);
        }

        /// <summary>One settled batch of departures and arrivals, said in that order of importance.</summary>
        private void SpeakMoves()
        {
            static bool IsEquipment(InventoryName name) => Announcements.SlotName(name) != null;
            var departures = new List<(InventoryName From, string Name)>(this.departures);
            var taken = this.arrivals.Any(arrival => arrival.To == InventoryName.Cursor1 || IsEquipment(arrival.To));
            foreach (var (to, name) in this.arrivals)
            {
                if (Announcements.SlotName(to) is { } slot)
                {
                    this.handEmptyDue = 0; // the hand let go into this slot: "Equipped" says it all
                    this.output.Speak(Announcements.Equipped(name, slot), true);
                    Take(source => source != InventoryName.Cursor1 && !IsEquipment(source));
                }
                else if (to == InventoryName.MainInventory1)
                {
                    if (Take(IsEquipment) is { } unequipped)
                    {
                        // The arrival is the same item in a new entity, readable when the departed one never was.
                        this.output.Speak(Announcements.Unequipped(unequipped == Announcements.UnknownItem ? name : unequipped), true);
                    }
                    else if (Take(source => source is InventoryName.Cursor1 or InventoryName.MainInventory1) == null && this.movedInPanel)
                    {
                        this.output.Speak(Announcements.NewInInventory(name), true);
                    }
                }
            }

            if (!taken && this.movedInPanel)
            {
                foreach (var (from, name) in departures)
                {
                    if (from == InventoryName.MainInventory1)
                    {
                        this.output.Speak(Announcements.LeftInventory(name), true);
                    }
                }
            }

            this.arrivals.Clear();
            this.departures.Clear();
            this.movedInPanel = false;

            string? Take(Func<InventoryName, bool> source)
            {
                var index = departures.FindIndex(departure => source(departure.From));
                if (index < 0)
                {
                    return null;
                }

                var name = departures[index].Name;
                departures.RemoveAt(index);
                return name;
            }
        }

        /// <summary>Main inventory, cursor and the equipment slots: where an item can be moved by the player.</summary>
        private static readonly InventoryName[] Tracked =
        {
            InventoryName.MainInventory1, InventoryName.Cursor1, InventoryName.BodyArmour1, InventoryName.Weapon1, InventoryName.Offhand1,
            InventoryName.Weapon2, InventoryName.Offhand2, InventoryName.Helm1, InventoryName.Amulet1, InventoryName.Ring1, InventoryName.Ring2,
            InventoryName.Gloves1, InventoryName.Boots1, InventoryName.Belt1,
        };

        /// <summary>A move is read once the inventories have not changed for this long.</summary>
        private const int SettleMs = 300;

        /// <summary>"No longer holding" waits this long for a move batch that may say "Equipped" instead.</summary>
        private const int HandEmptyWaitMs = 800;

        private long handEmptyDue;

        private readonly Dictionary<InventoryName, Inventory> readers = new();
        private readonly Dictionary<IntPtr, string> names = new();
        private readonly List<(InventoryName To, string Name)> arrivals = new();
        private readonly List<(InventoryName From, string Name)> departures = new();
        private Dictionary<IntPtr, InventoryName> where = new();
        private bool movesBaseline = true;
        private bool movedInPanel;
        private long lastMovesPoll;
        private long lastMoveAt;

        /// <summary>An item's name, kept after it leaves (a sold item can no longer be read).</summary>
        private string NameOf(IntPtr entity)
        {
            if (this.names.TryGetValue(entity, out var name))
            {
                return name;
            }

            if (this.reader.DescribeItemEntity(entity) is not { } read)
            {
                return Announcements.UnknownItem; // not kept: the next poll may read it
            }

            this.names[entity] = read;
            return read;
        }

        /// <summary>
        ///     GameHelper's reader of one player inventory, made once (each registers an update coroutine) and moved
        ///     when the game rebuilds the inventory.
        /// </summary>
        private Inventory? Reader(Dictionary<InventoryName, IntPtr> inventories, InventoryName name)
        {
            if (!inventories.TryGetValue(name, out var address) || address == IntPtr.Zero)
            {
                return null;
            }

            if (!this.readers.TryGetValue(name, out var reader))
            {
                reader = (Inventory)Activator.CreateInstance(typeof(Inventory), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { address, $"Poe2Accessibility {name}" }, null)!;
                this.readers[name] = reader;
            }
            else if (reader.Address != address)
            {
                reader.Address = address;
                this.movesBaseline = true;
            }

            return reader;
        }

        private bool TryCursor(out IReadOnlyDictionary<IntPtr, Item> items)
        {
            items = new Dictionary<IntPtr, Item>();
            var server = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject;
            if (PlayerInventoriesProperty?.GetValue(server) is not Dictionary<InventoryName, IntPtr> inventories ||
                !inventories.TryGetValue(InventoryName.Cursor1, out var address) || address == IntPtr.Zero)
            {
                return false;
            }

            if (this.cursor == null)
            {
                this.cursor = (Inventory)Activator.CreateInstance(typeof(Inventory), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { address, "Poe2Accessibility cursor" }, null)!;
                this.log.Info($"Watching the cursor inventory at 0x{address.ToInt64():X}");
            }
            else if (this.cursor.Address != address)
            {
                this.cursor.Address = address;
                this.held = IntPtr.Zero;
                this.handKnown = false;
            }

            items = this.cursor.Items;
            return true;
        }

        private bool TryInventory(out IReadOnlyDictionary<IntPtr, Item> items)
        {
            items = new Dictionary<IntPtr, Item>();
            var server = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject;
            if (PlayerInventoriesProperty?.GetValue(server) is not Dictionary<InventoryName, IntPtr> inventories ||
                !inventories.TryGetValue(InventoryName.MainInventory1, out var address) || address == IntPtr.Zero)
            {
                return false;
            }

            if (this.inventory == null)
            {
                // One host Inventory for the plugin's lifetime: each one registers its own update coroutine.
                this.inventory = (Inventory)Activator.CreateInstance(typeof(Inventory), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { address, "Poe2Accessibility main inventory" }, null)!;
                this.log.Info($"Watching the main inventory at 0x{address.ToInt64():X}");
            }
            else if (this.inventory.Address != address)
            {
                this.inventory.Address = address;
                this.Reset();
            }

            items = this.inventory.Items;
            return true;
        }
    }
}
