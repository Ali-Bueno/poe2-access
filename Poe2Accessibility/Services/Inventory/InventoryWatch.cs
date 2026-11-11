namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     What arrives in the main inventory while playing (D2R inventory arrival notices): "Recogido: Traje de
    ///     ermitaño" when an item is picked up, "Recogido: 3 Pergamino de sabiduría" when a stack grows. The
    ///     inventory is GameHelper's own reader (<c>ServerData.PlayerInventories[MainInventory1]</c>, internal, so
    ///     reached by reflection like the rest of the host's internals); the first reading of an area is silent.
    ///     Only while no panel is open: moving things around in the inventory is not news.
    /// </summary>
    public sealed partial class InventoryWatch
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

        /// <summary>How long a held item that does not read is tried again before it is said as an unknown one.</summary>
        private const int HandUnreadMs = 1000;
        private long handUnreadSince;

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

        /// <summary>A trade window is open: what leaves the inventory was handed over, not dropped or used.</summary>
        public Func<bool>? InTrade { get; set; }

        /// <summary>The engraving window's last choice and category (<see cref="ScreenNarrator.Engraving"/>).</summary>
        public Func<(string Choice, string Category)>? Engraving { get; set; }

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
                    if (this.reader.DescribeItemEntity(item.Address) is not { } name) // keys are inventory slots, the entity is the item's address
                    {
                        this.known[address] = before; // not readable yet: the next poll says it
                        continue;
                    }

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
                this.handUnreadSince = 0;
                this.held = item;
                this.handKnown = true;
                return;
            }

            if (item != IntPtr.Zero && entity != null)
            {
                // An item on its way out (a Y drop passes through the cursor) no longer reads: tried again, never said by its file.
                var name = this.reader.DescribeItemEntity(entity.Address);
                this.handUnreadSince = name != null ? 0 : this.handUnreadSince == 0 ? now : this.handUnreadSince;
                if (name == null && now - this.handUnreadSince < HandUnreadMs)
                {
                    return;
                }

                this.handUnreadSince = 0;
                this.handEmptyDue = 0;
                // Held only past the readout of the cell it left (now empty); the next D-pad move cuts it.
                this.output.Urge(Announcements.InHand(name ?? Announcements.UnknownItem), InHandHoldMs); // the name carries the stack
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
                        this.Reread(item);
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

            var salvaging = !playing && enabled && this.TickSalvage(now);
            if (!this.movesBaseline && enabled && !salvaging)
            {
                foreach (var (entity, from) in this.where)
                {
                    if (!located.ContainsKey(entity))
                    {
                        var name = this.names.Last(entity); // its entity is gone: as last read while it was there
                        this.departures.Add((from, name));
                        if (this.titled.GetValueOrDefault(entity, string.Empty) is { Length: > 0 } title && this.byBase.GetValueOrDefault(entity) is { } plain)
                        {
                            this.batchTitles.Add((name, plain, title));
                        }

                        this.NoteGem(entity, name);
                        (this.lastMoveAt, this.movedInPanel) = (now, this.movedInPanel || !playing);
                    }
                }

                foreach (var (entity, to) in located)
                {
                    if (!this.where.ContainsKey(entity))
                    {
                        this.arrivals.Add((to, this.NameOf(entity)));
                        this.arrivalEntities.Add(entity);
                        this.NoteGem(entity, this.NameOf(entity));
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
                this.arrivalEntities.Clear();
                this.batchTitles.Clear();
                this.departures.Clear();
                this.batchGems.Clear();
                this.batchUncutSupports.Clear();
            }

            this.names.KeepOnly(located.ContainsKey);
            foreach (var gone in this.titled.Keys.Where(entity => !located.ContainsKey(entity)).ToList())
            {
                this.titled.Remove(gone);
                this.byBase.Remove(gone);
            }

            foreach (var gone in this.gems.Keys.Where(entity => !located.ContainsKey(entity)).ToList())
            {
                this.gems.Remove(gone);
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
            // The skills panel takes and gives back gems only: anything else that leaves beside it was dropped or used.
            Func<string, bool>? sideTakes = side == Announcements.SkillGemPanel ? this.batchGems.Contains : null;
            var inTrade = this.InTrade?.Invoke() == true;
            this.NameArrivalsByTitle();
            var byBase = this.batchTitles.GroupBy(gone => gone.Name).ToDictionary(group => group.Key, group => group.First().ByBase);
            this.arrivalEntities.Clear();
            this.batchTitles.Clear();
            var moves = ItemMoves.Read(this.arrivals, this.departures, Announcements.MoveSlotName,this.movedInPanel, goldChange, side, this.handFrom, sideTakes, inTrade);
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
            var uncutSupports = this.batchUncutSupports.ToList();
            this.batchGems.Clear();
            this.batchUncutSupports.Clear();
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
                    case ItemMoveKind.DroppedOrUsed when inTrade:
                        lines.Add(Announcements.HandedOver(names));
                        break;
                    case ItemMoveKind.DroppedOrUsed:
                        foreach (var move in group)
                        {
                            this.drops.Add((move.Name, byBase.GetValueOrDefault(move.Name, string.Empty), Environment.TickCount64 + DropWaitMs, this.groundBefore));
                        }

                        break;
                    default:
                        lines.AddRange(group.Select(move => move.Kind switch
                        {
                            ItemMoveKind.Equipped => Announcements.Equipped(move.Name, move.Where),
                            ItemMoveKind.Unequipped => Announcements.Unequipped(move.Name),
                            // An uncut support gem gone into the skills was engraved and socketed into the engraving
                            // window's category, the skill it was chosen for (user 2026-10-31: where did it go?).
                            ItemMoveKind.MovedTo when move.Where == Announcements.SkillGemPanel && uncutSupports.Contains(move.Name) &&
                                                      this.Engraving?.Invoke() is { Choice.Length: > 0, Category.Length: > 0 } engraving =>
                                Announcements.SupportEngraved(engraving.Choice, engraving.Category),
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
                if (ground.Except(drop.GroundBefore).Any(id => this.GroundItemTitle?.Invoke(id) is { } onGround && ItemMoves.IsDropOf(onGround, drop.Name, drop.ByBase)))
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
            InventoryName.Gloves1, InventoryName.Boots1, InventoryName.Belt1, InventoryName.Flask1,
        };

        /// <summary>A move is read once the inventories have not changed for this long.</summary>
        private const int SettleMs = 300;

        /// <summary>"No longer holding" waits this long for a move batch that may say "Equipped" instead.</summary>
        private const int HandEmptyWaitMs = 800;

        private long handEmptyDue;

        private readonly Dictionary<InventoryName, Inventory> readers = new();
        private readonly ItemReadings names = new(() => Announcements.UnknownItem);
        private readonly List<(InventoryName To, string Name)> arrivals = new();
        private readonly List<(InventoryName From, string Name)> departures = new();
        private readonly HashSet<string> batchGems = new();
        private readonly HashSet<string> batchUncutSupports = new();

        /// <summary>The uncut support gem's base type (BaseItemTypes, dat-rows 2026-10-31): engraving one sockets the support at once.</summary>
        private const string UncutSupportBase = "/Gems/SupportGemUncut";
        private readonly Dictionary<IntPtr, bool> gems = new();
        private readonly List<(string Name, string ByBase, long Due, IReadOnlyCollection<uint> GroundBefore)> drops = new();
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

        /// <summary>Remembers a moved item that is a gem, by name, for the batch (its entity may be gone when the batch settles).</summary>
        private void NoteGem(IntPtr entity, string name)
        {
            if (this.gems.TryGetValue(entity, out var gem) ? gem : this.reader.IsGem(entity))
            {
                this.batchGems.Add(name);
                if (this.reader.ItemBaseId(entity).EndsWith(UncutSupportBase, StringComparison.Ordinal))
                {
                    this.batchUncutSupports.Add(name);
                }
            }
        }

        /// <summary>
        ///     Reads an item in reach again when the stack or rarity the host shows is not what its name was read with: a
        ///     stack used up to 7 was sold as "…, 10", an item made magic in place moved without its rarity.
        /// </summary>
        private void Reread(Item item)
        {
            var count = item.TryGetComponent<Stack>(out var stack) ? stack.Count : 0;
            var rarity = item.TryGetComponent<Mods>(out var mods) ? (int)mods.Rarity : 0;
            var title = this.reader.TitleOf(item.Address);
            if (this.names.IsStale(item.Address, count, rarity) || this.titled.GetValueOrDefault(item.Address, string.Empty) != title)
            {
                // By the name its tooltip gave it once the cursor has been on it; by its base until then.
                this.names.Read(item.Address, this.reader.DescribeItemEntity(item.Address, title), count, rarity);
                this.titled[item.Address] = title;
                if (title.Length > 0)
                {
                    this.byBase[item.Address] = this.reader.DescribeItemEntity(item.Address) ?? string.Empty;
                }

                this.gems[item.Address] = this.reader.IsGem(item.Address);
            }
        }

        /// <summary>The tooltip title each item's name was read with (empty: by its base).</summary>
        private readonly Dictionary<IntPtr, string> titled = new();

        /// <summary>What a titled item is called by its base alone, as its new entity will be read.</summary>
        private readonly Dictionary<IntPtr, string> byBase = new();

        /// <summary>The batch's departed items that had a title: their name, their name by base, and the title.</summary>
        private readonly List<(string Name, string ByBase, string Title)> batchTitles = new();

        /// <summary>The batch's arrivals' entities, in the order of <see cref="arrivals"/>.</summary>
        private readonly List<IntPtr> arrivalEntities = new();

        /// <summary>
        ///     An item that changed inventory is a new entity, read by its base: it takes the title of the one departure of
        ///     the batch with that base name ("Equipado: Escudo torre reforzado, raro" for Égida de glifo).
        /// </summary>
        private void NameArrivalsByTitle()
        {
            for (var index = 0; index < this.arrivals.Count && index < this.arrivalEntities.Count; index++)
            {
                var (to, name) = this.arrivals[index];
                var found = this.batchTitles.Where(gone => gone.ByBase == name).Select(gone => gone.Title).Distinct().ToList();
                if (found.Count == 1 && this.reader.DescribeItemEntity(this.arrivalEntities[index], found[0]) is { } named)
                {
                    this.reader.RememberTitle(this.arrivalEntities[index], found[0]);
                    this.arrivals[index] = (to, named);
                }
            }
        }

        /// <summary>An item's name as last read while it was there; one never read (the bench's) is read now.</summary>
        private string NameOf(IntPtr entity) =>
            this.names.Has(entity) ? this.names.Last(entity) : this.names.Read(entity, this.reader.DescribeItemEntity(entity));

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
