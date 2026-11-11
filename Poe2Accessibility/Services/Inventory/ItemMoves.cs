namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
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

        /// <summary>Gone with no panel to take it: dropped if it shows on the ground soon, else used up.</summary>
        DroppedOrUsed,
    }

    /// <summary>One settled item move: its kind, the item, and the slot or panel it concerns.</summary>
    public sealed record ItemMove(ItemMoveKind Kind, string Name, string Where = "");

    /// <summary>
    ///     Reads one settled batch of inventory departures and arrivals (the game makes a new item entity on every
    ///     move) as what the player did, the way a sighted player sees it: equipped, unequipped, sold, bought, put in
    ///     the stash or taken out, dropped. Not "left the inventory" (user 2026-10-28). Gold that rose with a departure
    ///     is a sale (Y on a merchant sells at once), gold that fell with an arrival a purchase; otherwise the panel open
    ///     beside the inventory names where it went or came from (the stash, the skills: the game's own title), the hand's
    ///     item counting from the panel it was picked up in, unless that panel cannot hold it (the skills hold only gems:
    ///     an orb used or an item dropped beside them was "moved to Skills", user 2026-10-31). In a trade what arrives was
    ///     received and what leaves was handed over, both said: an arrival there is no item of the inventory in a new
    ///     place unless it carries the same name. Pure.
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
            bool inTrade = false)
        {
            bool Beside(string name) => sidePanel.Length > 0 && (sideTakes?.Invoke(name) ?? true);
            bool IsEquipment(InventoryName name) => slotName(name) != null;
            var left = new List<(InventoryName From, string Name)>(departures);
            var moves = new List<ItemMove>();
            foreach (var (to, name) in arrivals)
            {
                if (slotName(to) is { } slot)
                {
                    // A flask drunk is a new entity in its own slot; in play nothing else can arrive where one left, and
                    // with a panel open one of its name brought from the inventory or the hand is another flask.
                    if (Take(source => source == to, null, departed => !inPanel || (Title(departed) == Title(name) && !Brought(name))) != null)
                    {
                        continue;
                    }

                    moves.Add(new ItemMove(ItemMoveKind.Equipped, name, slot));
                    Take(source => source != InventoryName.Cursor1 && !IsEquipment(source));
                }
                else if (to == InventoryName.MainInventory1)
                {
                    if (Take(IsEquipment) is { } unequipped)
                    {
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
                    else if ((TakeSame(name) ?? Rearranged()) == null && inPanel)
                    {
                        moves.Add(goldChange < 0 ? new ItemMove(ItemMoveKind.Bought, name)
                            : !inTrade && Beside(name) ? new ItemMove(ItemMoveKind.TakenFrom, name, sidePanel)
                            : new ItemMove(ItemMoveKind.Obtained, name));
                    }
                }
            }

            // What the hand or an equipment slot took is said there; what left the inventory or the hand for nowhere
            // tracked went to a merchant, a panel, the ground, or was used up.
            var taken = arrivals.Any(arrival => arrival.To == InventoryName.Cursor1 || IsEquipment(arrival.To));
            if (!taken && inPanel)
            {
                foreach (var (from, name) in left.Where(departure => departure.From is InventoryName.MainInventory1 or InventoryName.Cursor1))
                {
                    if (from == InventoryName.Cursor1 && handFrom.Length > 0 && handFrom == sidePanel && goldChange == 0)
                    {
                        continue; // set down in the panel it was picked up in: rearranging the stash
                    }

                    moves.Add(goldChange > 0 ? new ItemMove(ItemMoveKind.Sold, name)
                        : !inTrade && Beside(name) ? new ItemMove(ItemMoveKind.MovedTo, name, sidePanel)
                        : new ItemMove(ItemMoveKind.DroppedOrUsed, name));
                }
            }

            return moves;

            // An arrival with another name is the inventory's own item in a new place when its departure never read, and
            // outside a trade whatever its name (an item changed in place). In a trade it is what the other side gave:
            // paired with what was given for it, neither was said (one item for one: silence).
            string? Rearranged() =>
                inTrade ? Take(source => source == InventoryName.MainInventory1, Announcements.UnknownItem) : Take(source => source == InventoryName.MainInventory1);

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

        /// <summary>An item's name without its stack or rarity ("Orbe de aumento, 3" → "Orbe de aumento"), to find it on the ground.</summary>
        public static string Title(string name) => name.Split(',')[0].Trim();
    }
}
