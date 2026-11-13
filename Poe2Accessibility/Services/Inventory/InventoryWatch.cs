namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
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
