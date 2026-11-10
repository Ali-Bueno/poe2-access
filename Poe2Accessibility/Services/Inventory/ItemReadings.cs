namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Pure: what each item in reach was last read as ("Pergamino de sabiduría, 7"). A reading holds the item's stack and
    ///     rarity, which change in place, so it is taken again whenever either differs from what was read; the last named
    ///     reading answers once the item is gone. An unnamed reading is never kept.
    /// </summary>
    public sealed class ItemReadings
    {
        /// <summary>A stack or rarity nothing was read with: any real one differs from it.</summary>
        private const int NotRead = int.MinValue;

        private readonly Dictionary<IntPtr, (string Text, int Count, int Rarity)> readings = new();
        private readonly Func<string> unknown;

        /// <param name="unknown">What an item without a name is said as (the reader's own word for it).</param>
        public ItemReadings(Func<string> unknown) => this.unknown = unknown;

        /// <summary>Whether the item has no named reading, or one taken with another stack or rarity.</summary>
        public bool IsStale(IntPtr item, int count, int rarity) =>
            !this.readings.TryGetValue(item, out var read) || read.Count != count || read.Rarity != rarity;

        /// <summary>
        ///     Takes a reading of an item that is there, made while it had this stack and rarity; one that did not read or
        ///     names nothing leaves the last named reading to answer.
        /// </summary>
        public string Read(IntPtr item, string? reading, int count = NotRead, int rarity = NotRead)
        {
            if (reading != null && this.IsNamed(reading))
            {
                this.readings[item] = (reading, count, rarity);
                return reading;
            }

            return this.readings.TryGetValue(item, out var last) ? last.Text : reading ?? this.unknown();
        }

        /// <summary>The last named reading of an item, there or gone; the unknown item's word when it never had one.</summary>
        public string Last(IntPtr item) => this.readings.TryGetValue(item, out var read) ? read.Text : this.unknown();

        public bool Has(IntPtr item) => this.readings.ContainsKey(item);

        /// <summary>Forgets every item that is not among <paramref name="present"/>.</summary>
        public void KeepOnly(Func<IntPtr, bool> present)
        {
            foreach (var gone in this.readings.Keys.Where(item => !present(item)).ToList())
            {
                this.readings.Remove(gone);
            }
        }

        private bool IsNamed(string reading)
        {
            var unnamed = this.unknown();
            return reading.Length > 0 && reading != unnamed && !reading.StartsWith(unnamed + ",", StringComparison.Ordinal);
        }
    }
}
