namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>The drops and arrivals of <see cref="InventoryWatch"/>: ground drops, gem notes, re-reads and naming an arrival by its tooltip title.</summary>
    public sealed partial class InventoryWatch
    {
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
    }
}
