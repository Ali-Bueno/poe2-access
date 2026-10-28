namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.RemoteEnums;

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
    ///     item counting from the panel it was picked up in. Pure.
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
            string handFrom = "")
        {
            bool IsEquipment(InventoryName name) => slotName(name) != null;
            var left = new List<(InventoryName From, string Name)>(departures);
            var moves = new List<ItemMove>();
            foreach (var (to, name) in arrivals)
            {
                if (slotName(to) is { } slot)
                {
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
                        if (handFrom.Length > 0 && inPanel)
                        {
                            moves.Add(new ItemMove(ItemMoveKind.TakenFrom, placed == Announcements.UnknownItem ? name : placed, handFrom));
                        }
                    }
                    else if (Take(source => source == InventoryName.MainInventory1) == null && inPanel)
                    {
                        moves.Add(goldChange < 0 ? new ItemMove(ItemMoveKind.Bought, name)
                            : sidePanel.Length > 0 ? new ItemMove(ItemMoveKind.TakenFrom, name, sidePanel)
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
                        : sidePanel.Length > 0 ? new ItemMove(ItemMoveKind.MovedTo, name, sidePanel)
                        : new ItemMove(ItemMoveKind.DroppedOrUsed, name));
                }
            }

            return moves;

            string? Take(Func<InventoryName, bool> source)
            {
                var index = left.FindIndex(departure => source(departure.From));
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
