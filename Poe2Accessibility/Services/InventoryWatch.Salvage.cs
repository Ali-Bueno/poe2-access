namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Beside the salvage bench the main inventory is compared as a whole once it settles: the salvaged item goes and
    ///     its shards or quality currency come, often onto a stack that only grows, which item moves cannot tell.
    /// </summary>
    public sealed partial class InventoryWatch
    {
        private IReadOnlyList<(string Name, int Count)>? salvageBefore;
        private string salvageSignature = string.Empty;
        private string salvageSaid = string.Empty;
        private long salvageChangedAt;

        /// <summary>Per moves poll with a panel open; true while the salvage bench is beside the inventory (moves are not said then).</summary>
        private bool TickSalvage(long now)
        {
            if (this.SidePanel?.Invoke() != Announcements.SalvageBenchPanel ||
                !this.readers.TryGetValue(InventoryName.MainInventory1, out var main))
            {
                this.salvageBefore = null;
                return false;
            }

            var contents = main.Items.Values
                .Select(item => (Name: this.NameOf(item.Address), Count: item.TryGetComponent<Stack>(out var stack) && stack.Count > 0 ? stack.Count : 1))
                .ToList();
            if (contents.Any(item => item.Name == Announcements.UnknownItem))
            {
                return true; // read again next poll: an unread name would sound like an item gone and another come
            }

            var signature = string.Join("\n", contents.Select(item => $"{item.Name}|{item.Count}").OrderBy(line => line, StringComparer.Ordinal));
            if (this.salvageBefore == null || now < this.readersSettleUntil)
            {
                if (this.salvageBefore == null)
                {
                    this.log.Info($"Salvage bench open: {contents.Count} items in the main inventory");
                }

                (this.salvageBefore, this.salvageSignature, this.salvageSaid) = (contents, signature, signature);
                return true;
            }

            if (signature != this.salvageSignature)
            {
                (this.salvageSignature, this.salvageChangedAt) = (signature, now);
            }
            else if (signature != this.salvageSaid && now - this.salvageChangedAt >= SettleMs)
            {
                var outcome = SalvageOutcome.Read(this.salvageBefore, contents);
                (this.salvageBefore, this.salvageSaid) = (contents, signature);
                if (!outcome.IsEmpty)
                {
                    this.SayMoves(new List<string> { Announcements.Salvaged(outcome) });
                }
            }

            return true;
        }
    }
}
