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
    ///     The flasks on the belt (D2R belt readout): the uses each has left from its charges, for the vitals readout and
    ///     after each drink, and a warning when one runs dry during play, once until it can be used again.
    /// </summary>
    public sealed class FlaskWatch
    {
        private const int PollMs = 500;

        private readonly InventoryReader inventory;
        private readonly IAccessibilityOutputService output;
        private readonly FlaskDrinks drinks = new();
        private long lastPoll;
        private bool baseline = true;

        private Inventory? secondBelt;

        public FlaskWatch(InventoryReader inventory, IAccessibilityOutputService output)
        {
            this.inventory = inventory;
            this.output = output;
        }

        /// <summary>Whose belt is watched, local player 1 or 2 (<see cref="LocalInventories"/>).</summary>
        public Func<int> Seat { get; init; } = () => 1;

        /// <summary>The flasks in belt order, by their column, with the uses they have left (charges over charges per use).</summary>
        public IReadOnlyList<(int Slot, string Name, int Uses)> Flasks()
        {
            // A flask fills a column of the belt's cells (5x2 in 0.5.5e): each is read once.
            var flasks = new List<(int, string, int)>();
            if (this.Belt() is not { } belt)
            {
                return flasks;
            }

            var seen = new HashSet<IntPtr>();
            for (var x = 0; x < belt.TotalBoxes.X; x++)
            {
                for (var y = 0; y < belt.TotalBoxes.Y; y++)
                {
                    var item = belt[y, x];
                    if (seen.Add(item.Address) && Read(item, this.inventory) is { } flask)
                    {
                        flasks.Add((x, flask.Name, flask.Uses));
                    }
                }
            }

            return flasks;
        }

        /// <summary>Per frame: a flask drunk says the uses it has left, one run dry says so (silent on the first read of an area).</summary>
        public void Tick(bool playing, bool enabled)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var flasks = this.Flasks();
            foreach (var (slot, name, uses) in flasks)
            {
                if (this.drinks.Update(slot, name, uses) && !this.baseline && enabled)
                {
                    this.output.Say(SpeechKind.LifeAndMana, Announcements.Flask(name, uses));
                }
            }

            // The belt can read empty right after an area loads: the silent first read waits for flasks.
            this.baseline &= flasks.Count == 0;
        }

        /// <summary>A new area: the first read is the baseline again.</summary>
        public void Reset()
        {
            this.drinks.Clear();
            this.baseline = true;
        }

        /// <summary>Research (dev command "flasks"): every belt cell with what it holds.</summary>
        public string Diagnose()
        {
            if (this.Belt() is not { } belt)
            {
                return "Flasks: no belt";
            }

            var cells = new List<string>();
            for (var x = 0; x < belt.TotalBoxes.X; x++)
            {
                for (var y = 0; y < belt.TotalBoxes.Y; y++)
                {
                    var item = belt[y, x];
                    var charges = item.TryGetComponent<Charges>(out var c) ? $"{c.Current}/{c.PerUseCharge}" : "no charges";
                    cells.Add($"({x},{y}) 0x{item.Address.ToInt64():X} {item.Path} {this.inventory.ItemBaseName(item.Address)} {charges}");
                }
            }

            return $"Flasks: {belt.TotalBoxes.X}x{belt.TotalBoxes.Y} at 0x{belt.Address.ToInt64():X}, {belt.Items.Count} items; {string.Join("; ", cells)}";
        }

        /// <summary>The watched player's belt: GameHelper's for player 1, one made here (once) for player 2.</summary>
        private Inventory? Belt()
        {
            if (this.Seat() != 2)
            {
                return Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject.FlaskInventory;
            }

            if (this.inventory.Locals?.Of(2) is not { } inventories || !inventories.TryGetValue(InventoryName.Flask1, out var address))
            {
                return null;
            }

            if (this.secondBelt == null)
            {
                this.secondBelt = (Inventory)Activator.CreateInstance(typeof(Inventory), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { address, "Poe2Accessibility player 2 flasks" }, null)!;
            }
            else if (this.secondBelt.Address != address)
            {
                this.secondBelt.Address = address;
            }

            return this.secondBelt;
        }

        private static (string Name, int Uses)? Read(Item item, InventoryReader inventory)
        {
            if (item.Address == IntPtr.Zero || !item.TryGetComponent<Charges>(out var charges) || charges.Current < 0 ||
                inventory.ItemBaseName(item.Address) is not { Length: > 0 } name)
            {
                return null;
            }

            return (name, charges.PerUseCharge > 0 ? charges.Current / charges.PerUseCharge : charges.Current);
        }
    }
}
