namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using GameReader.RemoteEnums;

    /// <summary>What happened to an item, told by where it went and what else changed with it.</summary>
    public enum ItemMoveKind
    {
        Equipped,
        Unequipped,
        Sold,
        Bought,
        MovedTo,
        TakenFrom,
        Obtained,

        /// <summary>Put in a jewel socket of the passive tree (PassiveJewels1), or taken out of one.</summary>
        Socketed,
        Unsocketed,

        /// <summary>Gone with no panel to take it: dropped if it shows on the ground soon, else used up.</summary>
        DroppedOrUsed,

        /// <summary>Given to whoever opened the panel beside: an item no panel keeps (a quest item turned in).</summary>
        HandedOver,
    }

    /// <summary>One settled item move: its kind, the item, and the slot or panel it concerns.</summary>
    public sealed record ItemMove(ItemMoveKind Kind, string Name, string Where = "");

    /// <summary>
    ///     Reads one settled batch of inventory departures and arrivals (the game makes a new item entity on every
    ///     move) as what the player did, the way a sighted player sees it: equipped, unequipped, sold, bought, put in
    ///     the stash or taken out, socketed in the passive tree or taken from it, dropped. Not "left the inventory" (user 2026-10-28). Gold that rose with a departure
    ///     is a sale (Y on a merchant sells at once), gold that fell with an arrival a purchase; otherwise the panel open
    ///     beside the inventory names where it went or came from (the stash, the skills: the game's own title), the hand's
    ///     item counting from the panel it was picked up in, unless that panel cannot hold it (the skills hold only gems:
    ///     an orb used or an item dropped beside them was "moved to Skills", user 2026-10-31). In a trade what arrives was
    ///     received and what leaves was handed over, both said: an arrival there is no item of the inventory in a new
    ///     place unless it carries the same name. A stack that grew gained what it grew by, counted by all that came and
    ///     left under its name. A batch
    ///     may hold several deeds done in quick succession: what the hand or a slot took is paired with its own departure,
    ///     so the others are still said; one sold and another bought at once are a sale and a purchase, whichever way the
    ///     gold went in all. Pure.
    /// </summary>
    public static class ItemMoves
    {
        public static List<ItemMove> Read(
            IReadOnlyList<(InventoryName To, string Name)> arrivals,
            IReadOnlyList<(InventoryName From, string Name)> departures,
            Func<InventoryName, string?> slotName,
            bool inPanel,
            long goldChange,
            string sidePanel,
            string handFrom = "",
            Func<string, bool>? sideTakes = null,
            bool inTrade = false,
            Func<string, bool>? noPanelKeeps = null,
            bool handPassed = false)
        {
            bool Beside(string name) => sidePanel.Length > 0 && (sideTakes?.Invoke(name) ?? true);
            bool IsEquipment(InventoryName name) => slotName(name) != null;
            var left = new List<(InventoryName From, string Name)>(departures);
            var moves = new List<ItemMove>();
            // Gold moved beside a panel: its keeper traded, so what comes under a new name was bought, not the
            // inventory's own item changed in place.
            var goldMoved = goldChange != 0 && inPanel && !inTrade && sidePanel.Length > 0;
            var gained = new List<int>();
            var lost = new List<int>();
            var grown = new HashSet<string>(StringComparer.Ordinal);
            // The hand carried something in this batch (handPassed: come and gone before it settled): an item of the
            // bag come again under its own name may then be one moved by hand, not one made anew where it lay.
            var handDealt = handPassed || arrivals.Any(arrival => arrival.To == InventoryName.Cursor1) || departures.Any(departure => departure.From == InventoryName.Cursor1);
            var madeAnew = false;

            // An item made anew where it is worn (a flask drunk, an orb used on it) is no move, settled before another
            // arrival can take its departure for an unequip. In play nothing else can arrive where one left; with a panel
            // open one of its name brought from the inventory or the hand is another item.
            var remade = new bool[arrivals.Count];
            for (var index = 0; index < arrivals.Count; index++)
            {
                var (to, name) = arrivals[index];
                remade[index] = IsEquipment(to) &&
                                Take(source => source == to, null, departed => !inPanel || (Title(departed) == Title(name) && !Brought(name))) != null;
                // A flask is made anew by being drunk, with nothing used on it.
                madeAnew |= remade[index] && to != InventoryName.Flask1;
            }

            // What a slot or the hand took is paired with the departure of its own name before the inventory's arrivals
            // take theirs: a batch may hold another deed, and that one is still said. Not when one of its name arrives in
            // the inventory too (the stack it was split from): that departure is the stack's.
            var paired = new bool[arrivals.Count];
            for (var index = 0; index < arrivals.Count; index++)
            {
                var (to, name) = arrivals[index];
                paired[index] = !remade[index] && IsEquipment(to) && !StaysInTheBag(name) && TakeNamed(name, source => !IsEquipment(source));
            }

            for (var index = 0; index < arrivals.Count; index++)
            {
                var (to, name) = arrivals[index];
                paired[index] |= to == InventoryName.Cursor1 && !StaysInTheBag(name) && TakeNamed(name, FromOutsideTheHand);
            }

            for (var index = 0; index < arrivals.Count; index++)
            {
                var (to, name) = arrivals[index];
                if (remade[index])
                {
                    continue;
                }

                if (slotName(to) is { } slot)
                {
                    moves.Add(new ItemMove(ItemMoveKind.Equipped, name, slot));
                }
                else if (to == InventoryName.PassiveJewels1)
                {
                    // Not the stash beside: a jewel put in the tree from the bag or the hand, named as it was read there.
                    var socketed = Take(source => source is InventoryName.MainInventory1 or InventoryName.Cursor1);
                    moves.Add(new ItemMove(ItemMoveKind.Socketed, socketed is null || socketed == Announcements.UnknownItem ? name : socketed));
                }
                else if (to == InventoryName.MainInventory1)
                {
                    if (Take(source => source == InventoryName.PassiveJewels1) is { } unsocketed)
                    {
                        moves.Add(new ItemMove(ItemMoveKind.Unsocketed, unsocketed == Announcements.UnknownItem ? name : unsocketed));
                    }
                    else if (Take(IsEquipment, null, departed => Title(departed) == Title(name) || !StaysInTheBag(departed)) is { } unequipped)
                    {
                        // Its own name's departure, or one no other arrival of the bag is named like: that arrival is
                        // its owner (one bought and put on at once took the old weapon's, which was then "bought").
                        // The arrival is the same item in a new entity, readable when the departed one never was.
                        moves.Add(new ItemMove(ItemMoveKind.Unequipped, unequipped == Announcements.UnknownItem ? name : unequipped));
                    }
                    else if (Take(source => source == InventoryName.Cursor1) is { } placed)
                    {
                        // The hand set it down: news only when the hand took it from the panel beside (the stash).
                        // Set down on a stack of its kind, that stack is a new entity too: the old one was not used up
                        // ("Usado: Orbe de transmutación, 5" on putting one orb back, live 2026-11-10).
                        TakeSame(name);
                        if (handFrom.Length > 0 && inPanel)
                        {
                            moves.Add(new ItemMove(ItemMoveKind.TakenFrom, placed == Announcements.UnknownItem ? name : placed, handFrom));
                        }
                    }
                    else if (TakeSame(name) != null)
                    {
                        // A stack that grew gained what it grew by, said once for its name, whatever else the batch holds.
                        if (!inTrade && grown.Add(Title(name)) && NetGrowth(name) is { } more)
                        {
                            if (more >= 0)
                            {
                                // Every stack of its name that left is in that count: two made one are no stack gone.
                                left.RemoveAll(departure => departure.From == InventoryName.MainInventory1 && Title(departure.Name) == Title(name));
                            }

                            // No more and no fewer, with no hand to have moved it: the item was made anew where it lay.
                            madeAnew |= more == 0 && !handDealt;
                            if (more > 0 && inPanel)
                            {
                                gained.Add(moves.Count);
                                moves.Add(Gained(more > 1 ? $"{Title(name)}, {more}" : Title(name)));
                            }
                        }
                    }
                    else if (grown.Contains(Title(name)))
                    {
                        // Counted with the stack of its name: what did not fit it began another.
                    }
                    else if (Rearranged() != null)
                    {
                        // The bag's own item under another name: changed where it lay, unless the hand moved it.
                        madeAnew |= !handDealt;
                    }
                    else if (inPanel)
                    {
                        gained.Add(moves.Count);
                        moves.Add(Gained(name));
                    }
                }
            }

            // A slot's or the hand's item whose departure has another name takes the one never read, else the only one
            // left; with several left none can be told from it, and none of them is said.
            var unsure = false;
            foreach (var hand in new[] { false, true }) // the slots first: what they leave is the hand's
            {
                for (var index = 0; index < arrivals.Count; index++)
                {
                    var to = arrivals[index].To;
                    if (!remade[index] && !paired[index] && (hand ? to == InventoryName.Cursor1 : IsEquipment(to)))
                    {
                        unsure |= !TakeUnnamed(hand ? FromOutsideTheHand : source => !IsEquipment(source));
                    }
                }
            }

            // What left the inventory or the hand for nowhere tracked went to a merchant, a panel, the ground, or was used up.
            if (!unsure && inPanel)
            {
                // An item was made anew where it was and one thing left with it, no gold moving and no hand carrying:
                // that one was used on it, whatever is open beside (the last orb of its stack, used beside the stash,
                // was "moved to the stash").
                var loose = left.Where(departure => departure.From is InventoryName.MainInventory1 or InventoryName.Cursor1).ToList();
                var usedUp = madeAnew && !handDealt && !inTrade && goldChange == 0 && loose.Count == 1;
                foreach (var (from, name) in loose)
                {
                    if (from == InventoryName.Cursor1 && handFrom.Length > 0 && handFrom == sidePanel && goldChange == 0)
                    {
                        continue; // set down in the panel it was picked up in: rearranging the stash
                    }

                    if (usedUp)
                    {
                        moves.Add(new ItemMove(ItemMoveKind.DroppedOrUsed, name));
                        continue;
                    }

                    var kept = goldChange > 0 || (!inTrade && Beside(name));
                    var handedOver = kept && noPanelKeeps?.Invoke(name) == true;
                    if (!handedOver)
                    {
                        lost.Add(moves.Count);
                    }

                    moves.Add(handedOver ? new ItemMove(ItemMoveKind.HandedOver, name)
                        : goldChange > 0 ? new ItemMove(ItemMoveKind.Sold, name)
                        : kept ? new ItemMove(ItemMoveKind.MovedTo, name, sidePanel)
                        : new ItemMove(ItemMoveKind.DroppedOrUsed, name));
                }
            }

            // One sold and another bought in the same batch, more spent than earned: gold fell, so something was bought,
            // and what left beside the keeper was sold; the gold that moved is the one's price less the other's. With
            // more earned than spent the sale stands as read and what came is "taken from" the keeper: gold that rose
            // does not say a thing was bought (a keeper may pay in more than gold).
            if (goldMoved && goldChange < 0 && gained.Count > 0 && lost.Count > 0)
            {
                foreach (var index in gained)
                {
                    moves[index] = new ItemMove(ItemMoveKind.Bought, moves[index].Name);
                }

                foreach (var index in lost)
                {
                    moves[index] = new ItemMove(ItemMoveKind.Sold, moves[index].Name);
                }
            }

            return moves;

            static bool FromOutsideTheHand(InventoryName source) => source != InventoryName.Cursor1;

            bool StaysInTheBag(string name) => arrivals.Any(arrival => arrival.To == InventoryName.MainInventory1 && Title(arrival.Name) == Title(name));

            // Takes the departure of the arrival's own name; false when there is none.
            bool TakeNamed(string arrival, Func<InventoryName, bool> from)
            {
                var index = left.FindIndex(departure => from(departure.From) && Title(departure.Name) == Title(arrival));
                if (index >= 0)
                {
                    left.RemoveAt(index);
                }

                return index >= 0;
            }

            // Takes the departure that never read, else the only one there is; false when several are left.
            bool TakeUnnamed(Func<InventoryName, bool> from)
            {
                var index = left.FindIndex(departure => from(departure.From) && departure.Name == Announcements.UnknownItem);
                if (index < 0)
                {
                    index = left.FindIndex(departure => from(departure.From));
                    if (index != left.FindLastIndex(departure => from(departure.From)))
                    {
                        return false;
                    }
                }

                if (index >= 0)
                {
                    left.RemoveAt(index);
                }

                return true;
            }

            ItemMove Gained(string name) => goldChange < 0 ? new ItemMove(ItemMoveKind.Bought, name)
                : !inTrade && Beside(name) ? new ItemMove(ItemMoveKind.TakenFrom, name, sidePanel)
                : new ItemMove(ItemMoveKind.Obtained, name);

            // An arrival with another name is the inventory's own item in a new place when its departure never read, and
            // outside a trade whatever its name (an item changed in place). In a trade it is what the other side gave:
            // paired with what was given for it, neither was said (one item for one: silence). The same where gold
            // moved beside a panel: one sold and another bought at once said nothing.
            // Never a departure whose own name arrives too: that one is its stack, larger or smaller (bought with
            // artifacts, the item took the stack they were paid from and the stack was said in its place).
            string? Rearranged() => inTrade || goldMoved
                ? Take(source => source == InventoryName.MainInventory1, Announcements.UnknownItem)
                : Take(source => source == InventoryName.MainInventory1, null, departed => !StaysInTheBag(departed));

            // How many more of an item the inventory holds after the batch, by all that came and left under its name:
            // a stack bought into twice came and left twice. Not told when the hand or a slot dealt in it too (a split).
            int? NetGrowth(string name)
            {
                var title = Title(name);
                bool Named((InventoryName Where, string Name) item) => Title(item.Name) == title;
                var came = arrivals.Where(Named).ToList();
                var went = departures.Where(Named).ToList();
                return came.Any(item => item.To != InventoryName.MainInventory1) || went.Any(item => item.From != InventoryName.MainInventory1) ? null
                    : came.Sum(item => Count(item.Name)) - went.Sum(item => Count(item.Name));
            }

            // Whether an item of this name, or one never read, left the inventory or the hand in this batch.
            bool Brought(string arrival) => left.Any(departure =>
                !IsEquipment(departure.From) && (departure.Name == Announcements.UnknownItem || Title(departure.Name) == Title(arrival)));

            // A stack that grew is a new entity of the same item, whatever else left with it (a trade).
            string? TakeSame(string arrival)
            {
                var index = left.FindIndex(departure => departure.From == InventoryName.MainInventory1 && Title(departure.Name) == Title(arrival));
                if (index < 0)
                {
                    return null;
                }

                var same = left[index].Name;
                left.RemoveAt(index);
                return same;
            }

            string? Take(Func<InventoryName, bool> source, string? named = null, Func<string, bool>? fits = null)
            {
                var index = left.FindIndex(departure => source(departure.From) && (named == null || departure.Name == named) && (fits?.Invoke(departure.Name) ?? true));
                if (index < 0)
                {
                    return null;
                }

                var name = left[index].Name;
                left.RemoveAt(index);
                return name;
            }
        }

        /// <summary>Whether a ground item is the one dropped: named alike, or as the base a titled item's new entity is read by.</summary>
        public static bool IsDropOf(string onGround, string dropped, string byBase) =>
            Title(onGround) is var title && (title == Title(dropped) || (byBase.Length > 0 && title == Title(byBase)));

        /// <summary>How many more of the same item the second line counts; zero for another item or a stack no larger.</summary>
        public static int Growth(string before, string after) =>
            Title(before) == Title(after) ? Math.Max(0, Count(after) - Count(before)) : 0;

        /// <summary>The stack an item's line says (<see cref="Announcements.Item"/>); one that says none is one item.</summary>
        private static int Count(string name)
        {
            foreach (var part in name.Split(',').Skip(1))
            {
                if (int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                {
                    return count;
                }
            }

            return 1;
        }

        /// <summary>An item's name without its stack or rarity ("Orbe de aumento, 3" → "Orbe de aumento"), to find it on the ground.</summary>
        public static string Title(string name) => name.Split(',')[0].Trim();
    }
}
