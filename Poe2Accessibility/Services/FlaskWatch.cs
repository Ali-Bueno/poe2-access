namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The flasks on the belt (D2R belt readout): the uses each has left from its charges, for the vitals readout, and
    ///     a warning when one runs dry during play, once until it can be used again.
    /// </summary>
    public sealed class FlaskWatch
    {
        private const int PollMs = 500;

        private readonly InventoryReader inventory;
        private readonly IAccessibilityOutputService output;
        private readonly HashSet<IntPtr> dry = new();
        private long lastPoll;
        private bool baseline = true;

        public FlaskWatch(InventoryReader inventory, IAccessibilityOutputService output)
        {
            this.inventory = inventory;
            this.output = output;
        }

        /// <summary>The flasks in belt order with the uses they have left (charges over charges per use).</summary>
        public IReadOnlyList<(IntPtr Address, string Name, int Uses)> Flasks()
        {
            // A flask fills a column of the belt's cells (5x2 in 0.5.5e): each is read once.
            var belt = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject.FlaskInventory;
            var flasks = new List<(IntPtr, string, int)>();
            var seen = new HashSet<IntPtr>();
            for (var x = 0; x < belt.TotalBoxes.X; x++)
            {
                for (var y = 0; y < belt.TotalBoxes.Y; y++)
                {
                    var item = belt[y, x];
                    if (seen.Add(item.Address) && Read(item, this.inventory) is { } flask)
                    {
                        flasks.Add(flask);
                    }
                }
            }

            return flasks;
        }

        /// <summary>Per frame: a flask that has just run dry is said (silent on the first read of an area).</summary>
        public void Tick(bool playing, bool enabled)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var flasks = this.Flasks();
            foreach (var (address, name, uses) in flasks)
            {
                if (uses > 0)
                {
                    this.dry.Remove(address);
                }
                else if (this.dry.Add(address) && !this.baseline && enabled)
                {
                    this.output.Say(SpeechKind.LifeAndMana, Announcements.FlaskEmpty(name));
                }
            }

            // The belt can read empty right after an area loads: the silent first read waits for flasks.
            this.baseline &= flasks.Count == 0;
        }

        /// <summary>A new area: the first read is the baseline again.</summary>
        public void Reset()
        {
            this.dry.Clear();
            this.baseline = true;
        }

        /// <summary>Research (dev command "flasks"): every belt cell with what it holds.</summary>
        public string Diagnose()
        {
            var belt = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject.FlaskInventory;
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

        private static (IntPtr Address, string Name, int Uses)? Read(Item item, InventoryReader inventory)
        {
            if (item.Address == IntPtr.Zero || !item.TryGetComponent<Charges>(out var charges) || charges.Current < 0 ||
                inventory.ItemBaseName(item.Address) is not { Length: > 0 } name)
            {
                return null;
            }

            return (item.Address, name, charges.PerUseCharge > 0 ? charges.Current / charges.PerUseCharge : charges.Current);
        }
    }
}
