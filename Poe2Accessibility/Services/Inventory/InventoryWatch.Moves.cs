namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;

    /// <summary>The item moves of <see cref="InventoryWatch"/>: the weapon set, the moves between inventories and how they are said.</summary>
    public sealed partial class InventoryWatch
    {
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
    }
}
