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

        /// <summary>
        ///     A host Inventory just made or moved reads empty, or the old inventory's items, until its update coroutine
        ///     has run: what the readers show this long after is not news (a false "Picked up" when player 2's inventory
        ///     began to be watched, 2026-10-25).
        /// </summary>
        private const int ReaderSettleMs = 1000;

        private long readersSettleUntil;


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
        /// <summary>Whose inventories are watched, local player 1 or 2 (<see cref="LocalInventories"/>).</summary>
        public Func<int> Seat { get; init; } = () => 1;

        /// <summary>The gold carried, null while unknown: rising with a departure is a sale, falling with an arrival a purchase.</summary>
        public Func<long?>? Gold { get; set; }

        /// <summary>The title of the panel open beside the inventory (stash, skills, merchant), empty when none.</summary>
        public Func<string>? SidePanel { get; set; }

        /// <summary>Ids of the items on the ground, and the title of one: an item gone from the inventory that lands there was dropped.</summary>
        public Func<IReadOnlyCollection<uint>>? GroundItemIds { get; set; }

        public Func<uint, string?>? GroundItemTitle { get; set; }

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
            this.baseline |= now < this.readersSettleUntil;
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

            if (item == this.held || now < this.readersSettleUntil)
            {
                this.held = item;
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
        ///     for <see cref="SettleMs"/>, and said as what the player did (<see cref="ItemMoves"/>): "Equipado: Ballesta
        ///     improvisada, Mano principal", "Vendido: …, por 19 de oro", "Movido a Alijo: …"; an item gone with only the
        ///     inventory open is "Tirado" once it shows on the ground, else "Usado". Pick-ups during play are Tick's.
        /// </summary>
        public void TickMoves(bool playing, bool enabled)
        {
            var now = Environment.TickCount64;
            if (now - this.lastMovesPoll < PollMs)
            {
                return;
            }

            this.lastMovesPoll = now;
            if (this.reader.Locals?.Of(this.Seat()) is not { } inventories)
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

            this.movesBaseline |= now < this.readersSettleUntil;
            this.TickDrops(now);
            // What a batch is measured against: the gold and the ground two polls before its first change, since the
            // gold counter and a dropped item can show a poll ahead of the inventory. Only with a panel open, where
            // moves are said.
            this.recent.Enqueue((this.Gold?.Invoke(), playing ? Array.Empty<uint>() : this.GroundItemIds?.Invoke() ?? Array.Empty<uint>()));
            while (this.recent.Count > RecentPolls)
            {
                this.recent.Dequeue();
            }

            if (this.arrivals.Count == 0 && this.departures.Count == 0)
            {
                (this.goldBefore, this.groundBefore) = this.recent.Peek();
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
            if (!Seats.Of(this.Seat()).TryGetComponent<Stats>(out var stats) ||
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

        /// <summary>One settled batch of departures and arrivals, each said as the deed it was and held against the focus that follows.</summary>
        private void SpeakMoves()
        {
            var goldChange = this.Gold?.Invoke() is { } now && this.goldBefore is { } before ? now - before : 0;
            var side = this.movedInPanel ? this.SidePanel?.Invoke() ?? string.Empty : string.Empty;
            var moves = ItemMoves.Read(this.arrivals, this.departures, Announcements.SlotName, this.movedInPanel, goldChange, side, this.handFrom);
            if (this.arrivals.Any(arrival => arrival.To == InventoryName.Cursor1))
            {
                // Picked up with nothing tracked leaving: from the panel beside (a stash tab), said when set down.
                this.handFrom = this.departures.Count == 0 ? side : string.Empty;
            }
            else if (this.departures.Any(departure => departure.From == InventoryName.Cursor1))
            {
                this.handFrom = string.Empty;
            }

            this.arrivals.Clear();
            this.departures.Clear();
            this.movedInPanel = false;
            if (moves.Count > 0)
            {
                this.handEmptyDue = 0; // where the hand's item went is said instead
            }

            var lines = new List<string>();
            foreach (var group in moves.GroupBy(move => (move.Kind, move.Where)))
            {
                var names = string.Join(", ", group.Select(move => move.Name));
                switch (group.Key.Kind)
                {
                    case ItemMoveKind.Sold:
                        lines.Add(Announcements.Sold(names, goldChange));
                        break;
                    case ItemMoveKind.Bought:
                        lines.Add(Announcements.Bought(names, -goldChange));
                        break;
                    case ItemMoveKind.DroppedOrUsed:
                        foreach (var move in group)
                        {
                            this.drops.Add((move.Name, Environment.TickCount64 + DropWaitMs, this.groundBefore));
                        }

                        break;
                    default:
                        lines.AddRange(group.Select(move => move.Kind switch
                        {
                            ItemMoveKind.Equipped => Announcements.Equipped(move.Name, move.Where),
                            ItemMoveKind.Unequipped => Announcements.Unequipped(move.Name),
                            ItemMoveKind.MovedTo => Announcements.MovedTo(move.Name, move.Where),
                            ItemMoveKind.TakenFrom => Announcements.TakenFrom(move.Name, move.Where),
                            _ => Announcements.Obtained(move.Name),
                        }));
                        break;
                }
            }

            this.SayMoves(lines);
        }

        /// <summary>
        ///     Items gone with only the inventory open: dropped once a new ground item of that name shows (Y "Tirar"), used
        ///     up (a currency's last orb, a rune socketed) when none has by the deadline.
        /// </summary>
        private void TickDrops(long now)
        {
            if (this.drops.Count == 0)
            {
                return;
            }

            var ground = this.GroundItemIds?.Invoke() ?? Array.Empty<uint>();
            var lines = new List<string>();
            foreach (var drop in this.drops.ToList())
            {
                var title = ItemMoves.Title(drop.Name);
                if (ground.Except(drop.GroundBefore).Any(id => this.GroundItemTitle?.Invoke(id) is { } onGround && ItemMoves.Title(onGround) == title))
                {
                    lines.Add(Announcements.Dropped(drop.Name));
                    this.drops.Remove(drop);
                }
                else if (now >= drop.Due)
                {
                    lines.Add(Announcements.UsedUp(drop.Name));
                    this.drops.Remove(drop);
                }
            }

            this.SayMoves(lines);
        }

        /// <summary>A move is what the player is waiting on: kept against the focus update it causes.</summary>
        private void SayMoves(List<string> lines)
        {
            if (lines.Count > 0)
            {
                var text = string.Join(". ", lines);
                this.output.Keep(text, SpeechFloor.MessageHoldMs(text), true, yieldsToMoves: true);
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
        private readonly List<(string Name, long Due, IReadOnlyCollection<uint> GroundBefore)> drops = new();
        private readonly Queue<(long? Gold, IReadOnlyCollection<uint> Ground)> recent = new();
        private long? goldBefore;

        /// <summary>The panel the hand's item was picked up in, empty when it came from the inventory or equipment.</summary>
        private string handFrom = string.Empty;
        private IReadOnlyCollection<uint> groundBefore = Array.Empty<uint>();

        /// <summary>Polls kept for <see cref="recent"/>: this one and the two before it.</summary>
        private const int RecentPolls = 3;

        /// <summary>A dropped item shows on the ground within this time of leaving the inventory; past it, it was used up.</summary>
        private const int DropWaitMs = 1200;
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
                this.Settle();
            }
            else if (reader.Address != address)
            {
                reader.Address = address;
                this.movesBaseline = true;
                this.Settle();
            }

            return reader;
        }

        private bool TryCursor(out IReadOnlyDictionary<IntPtr, Item> items)
        {
            items = new Dictionary<IntPtr, Item>();
            if (this.reader.Locals?.Of(this.Seat()) is not { } inventories ||
                !inventories.TryGetValue(InventoryName.Cursor1, out var address) || address == IntPtr.Zero)
            {
                return false;
            }

            if (this.cursor == null)
            {
                this.cursor = (Inventory)Activator.CreateInstance(typeof(Inventory), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { address, "Poe2Accessibility cursor" }, null)!;
                this.log.Info($"Watching the cursor inventory at 0x{address.ToInt64():X}");
                this.Settle();
            }
            else if (this.cursor.Address != address)
            {
                this.cursor.Address = address;
                this.Settle();
                this.held = IntPtr.Zero;
                this.handKnown = false;
            }

            items = this.cursor.Items;
            return true;
        }

        private void Settle() => this.readersSettleUntil = Environment.TickCount64 + ReaderSettleMs;

        private bool TryInventory(out IReadOnlyDictionary<IntPtr, Item> items)
        {
            items = new Dictionary<IntPtr, Item>();
            if (this.reader.Locals?.Of(this.Seat()) is not { } inventories ||
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
                this.Settle();
            }
            else if (this.inventory.Address != address)
            {
                this.inventory.Address = address;
                this.Reset();
                this.Settle();
            }

            items = this.inventory.Items;
            return true;
        }
    }
}
