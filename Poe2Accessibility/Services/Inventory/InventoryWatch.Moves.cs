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
            var grown = new List<(IntPtr Entity, string Before)>();
            foreach (var name in Tracked)
            {
                if (this.Reader(inventories, name) is { } reader)
                {
                    foreach (var (_, item) in reader.Items)
                    {
                        located[item.Address] = name;
                        var before = this.names.Has(item.Address) ? this.names.Last(item.Address) : null;
                        this.Reread(item);
                        if (!playing && name == InventoryName.MainInventory1 && before != null && ItemMoves.Growth(before, this.names.Last(item.Address)) > 0)
                        {
                            grown.Add((item.Address, before));
                        }
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
                this.ClearBatch(); // what a batch that came to nothing left behind
            }

            var inPanel = !playing && enabled;
            var side = inPanel ? this.SidePanel?.Invoke() ?? string.Empty : string.Empty;
            var salvaging = inPanel && this.TickSalvage(now, side);
            if (!this.movesBaseline && enabled && !salvaging)
            {
                var moved = false;
                foreach (var (entity, from) in this.where)
                {
                    if (located.ContainsKey(entity))
                    {
                        continue;
                    }

                    moved = true;
                    if (from == InventoryName.Cursor1 && this.arrivalEntities.IndexOf(entity) is >= 0 and var passed)
                    {
                        // Come to the hand and gone before the batch settled: it only passed through (a Y drop).
                        this.arrivals.RemoveAt(passed);
                        this.arrivalEntities.RemoveAt(passed);
                        this.batchHandPassed = true;
                        continue;
                    }

                    var name = this.names.Last(entity); // its entity is gone: as last read while it was there
                    this.departures.Add((from, name));
                    if (this.seen.TryGetValue(entity, out var item) && item.Title.Length > 0)
                    {
                        this.batchTitles.Add((name, item.ByBase, item.Title, from));
                    }

                    this.NoteKind(entity, name);
                }

                foreach (var (entity, to) in located)
                {
                    if (!this.where.ContainsKey(entity))
                    {
                        moved = true;
                        this.arrivals.Add((to, this.NameOf(entity)));
                        this.arrivalEntities.Add(entity);
                        this.NoteKind(entity, this.NameOf(entity));
                    }
                }

                // A stack that grew in place reads as the game's other way: the old stack gone, the larger one come.
                foreach (var (entity, before) in grown.Where(stack => this.where.ContainsKey(stack.Entity)))
                {
                    moved = true;
                    this.departures.Add((InventoryName.MainInventory1, before));
                    this.arrivals.Add((InventoryName.MainInventory1, this.names.Last(entity)));
                    this.arrivalEntities.Add(entity);
                }

                if (moved)
                {
                    this.NoteMoved(now, !playing, side);
                }

                if ((this.arrivals.Count > 0 || this.departures.Count > 0) && now - this.lastMoveAt >= SettleMs)
                {
                    this.SpeakMoves();
                }
            }
            else
            {
                this.ClearBatch();
            }

            this.names.KeepOnly(located.ContainsKey);
            foreach (var gone in this.seen.Keys.Where(entity => !located.ContainsKey(entity)).ToList())
            {
                this.seen.Remove(gone);
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
            // The panel as it was when the moves showed: closed since, its moves were "used" (a fast hand).
            var favours = this.batchInFavours || this.InFavours?.Invoke() == true;
            var side = !this.movedInPanel || favours ? string.Empty
                : this.batchSide.Length > 0 ? this.batchSide
                : this.SidePanel?.Invoke() ?? string.Empty;
            // The skills panel takes and gives back gems only: anything else that leaves beside it was dropped or used.
            Func<string, bool>? sideTakes = side == Announcements.SkillGemPanel ? this.batchGems.Contains : null;
            var inTrade = this.batchInTrade || this.InTrade?.Invoke() == true;
            this.NameArrivalsByTitle();
            var byBase = this.batchTitles.GroupBy(gone => gone.Name).ToDictionary(group => group.Key, group => group.First().ByBase);
            var moves = ItemMoves.Read(this.arrivals, this.departures, Announcements.MoveSlotName, this.movedInPanel, goldChange, side, this.handFrom, sideTakes, inTrade,
                this.batchQuestItems.Contains, this.batchHandPassed);
            this.LogBatch(moves, goldChange, side, inTrade);
            if (this.arrivals.Any(arrival => arrival.To == InventoryName.Cursor1))
            {
                // Picked up with nothing tracked leaving but what the hand held: from the panel beside (a stash tab), said when set down.
                this.handFrom = this.departures.All(departure => departure.From == InventoryName.Cursor1) ? side : string.Empty;
            }
            else if (this.departures.Any(departure => departure.From == InventoryName.Cursor1))
            {
                this.handFrom = string.Empty;
            }

            var uncutSupports = this.batchUncutSupports.ToList();
            this.ClearBatch();
            if (moves.Count > 0)
            {
                this.handEmptyDue = 0; // where the hand's item went is said instead
            }

            var lines = new List<string>();
            // One sold and another bought in the batch: the gold that moved is neither's price, so none is said.
            var bothWays = moves.Any(move => move.Kind == ItemMoveKind.Sold) && moves.Any(move => move.Kind == ItemMoveKind.Bought);
            foreach (var group in moves.GroupBy(move => (move.Kind, move.Where)))
            {
                var names = string.Join(", ", group.Select(move => move.Name));
                switch (group.Key.Kind)
                {
                    case ItemMoveKind.Sold:
                        lines.Add(bothWays ? Announcements.Sold(names) : Announcements.Sold(names, goldChange));
                        break;
                    case ItemMoveKind.Bought:
                        lines.Add(bothWays ? Announcements.Bought(names) : Announcements.Bought(names, -goldChange));
                        break;
                    case ItemMoveKind.HandedOver:
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
                            ItemMoveKind.Socketed => Announcements.Socketed(move.Name),
                            ItemMoveKind.Unsocketed => Announcements.Unsocketed(move.Name),
                            _ => Announcements.Obtained(move.Name),
                        }));
                        break;
                }
            }

            this.SayMoves(lines);
        }

        /// <summary>A move just seen: when, and what was open beside the inventory then.</summary>
        private void NoteMoved(long now, bool inPanel, string side)
        {
            (this.lastMoveAt, this.movedInPanel) = (now, this.movedInPanel || inPanel);
            if (inPanel)
            {
                this.batchSide = this.batchSide.Length > 0 ? this.batchSide : side;
                this.batchInTrade |= this.InTrade?.Invoke() == true;
                this.batchInFavours |= this.InFavours?.Invoke() == true;
            }
        }

        private void ClearBatch()
        {
            this.arrivals.Clear();
            this.arrivalEntities.Clear();
            this.departures.Clear();
            this.batchTitles.Clear();
            this.batchGems.Clear();
            this.batchUncutSupports.Clear();
            this.batchQuestItems.Clear();
            (this.movedInPanel, this.batchSide, this.batchInTrade, this.batchInFavours, this.batchHandPassed) = (false, string.Empty, false, false, false);
        }

        /// <summary>One log line per batch said or seen beside a panel: what came, what left and how it was read.</summary>
        private void LogBatch(List<ItemMove> moves, long goldChange, string side, bool inTrade)
        {
            if (moves.Count == 0 && !this.movedInPanel)
            {
                return; // a flask drunk in play
            }

            static string Listed(IEnumerable<(InventoryName Where, string Name)> items) => string.Join("; ", items.Select(item => $"{item.Where} {item.Name}"));
            var read = moves.Count == 0 ? "nothing said" : string.Join("; ", moves.Select(move => $"{move.Kind} {move.Name}"));
            this.log.Info($"Item moves: in [{Listed(this.arrivals)}], out [{Listed(this.departures)}], gold {goldChange:+0;-0;0}, beside \"{side}\"{(inTrade ? ", trade" : string.Empty)}: {read}");
        }
    }
}
