namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
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
                this.output.Urge(Announcements.InHand(this.reader.DescribeItemEntity(entity.Address) ?? entity.Path)); // the name carries the stack
            }
            else if (this.handKnown)
            {
                this.output.Speak(Announcements.HandEmpty, false);
            }

            this.held = item;
            this.handKnown = true;
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
