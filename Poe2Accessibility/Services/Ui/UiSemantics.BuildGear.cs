namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The Build Planner's gear for <see cref="UiSemantics"/>: what the active build wants in each equipment slot set
    ///     against the worn item and the item under the cursor (docs/research/ui-layers.md#build-planner-gear-055f-2026-11-10).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The unique rarity as <see cref="InventoryReader.ItemKind"/> names it.</summary>
        private const string UniqueKind = "unique";

        /// <summary>Tooltip lines of the worn items seen this session, by item entity: a worn item's lines are read from its tooltip alone.</summary>
        private readonly Dictionary<IntPtr, IReadOnlyList<string>> wornLines = new();

        private (long At, string Character, Dictionary<(string Slot, int X), IntPtr> Worn, HashSet<string> Slots)? wornGear;
        /// <summary>What was said last of the cursor's item and of the worn gear, kept while what it was made from stands (<see cref="GearState"/>).</summary>
        private (object? State, IntPtr Entity, IntPtr Grid, int Column, int Tooltip, (List<string> First, List<string> Last) Said) gearMemo;
        private (object? State, IReadOnlyList<string> Lines) gearSummary = (null, Array.Empty<string>());

        /// <summary>Counts the changes of the worn items and of their lines read.</summary>
        private int gearVersion;
        private (IntPtr Address, GearPlace Place, bool Show) gearFocus;

        /// <summary>Where the focus is, for the gear summary: the player's own inventory and slots, another grid (stash, a merchant's stock), or anything else.</summary>
        private enum GearPlace
        {
            Elsewhere,
            Own,
            OtherGrid,
        }

        private static bool IsEquipment(InventoryName name) => name == InventoryName.Flask1 || Announcements.SlotName(name) != null;

        /// <summary>The active build with gear and the character's level; null without one.</summary>
        private (BuildPlan Plan, int Level)? GearPlan() => this.ActivePlan() is { } active && active.Plan.Gear.Count > 0 ? active : null;

        /// <summary>The asking player's worn items by slot (the server inventories' own entities), and the equipment inventories the reader lists.</summary>
        private (Dictionary<(string Slot, int X), IntPtr> Worn, HashSet<string> Slots) WornGear()
        {
            var now = Environment.TickCount64;
            var character = AskingCharacter();
            if (this.wornGear is { } cached && cached.Character == character && now - cached.At < GemHoldingsMs)
            {
                return (cached.Worn, cached.Slots);
            }

            var worn = new Dictionary<(string Slot, int X), IntPtr>();
            var slots = new HashSet<string>();
            try
            {
                foreach (var (name, address) in this.inventory.Locals?.Of(Seats.Input)?.ToArray() ?? Array.Empty<KeyValuePair<InventoryName, IntPtr>>())
                {
                    if (!IsEquipment(name))
                    {
                        continue;
                    }

                    slots.Add(name.ToString());
                    foreach (var item in ServerInventory.Items(this.reader, address))
                    {
                        worn[(name.ToString(), name == InventoryName.Flask1 ? item.X : 0)] = item.Entity;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The host rebuilt its inventory list meanwhile: this character's gear as last read.
                return this.wornGear is { } last && last.Character == character ? (last.Worn, last.Slots) : (worn, slots);
            }

            foreach (var gone in this.wornLines.Keys.Where(entity => !worn.ContainsValue(entity)).ToList())
            {
                this.wornLines.Remove(gone);
            }

            if (this.wornGear is not { } before || before.Character != character || before.Worn.Count != worn.Count ||
                worn.Any(piece => before.Worn.GetValueOrDefault(piece.Key) != piece.Value))
            {
                this.gearVersion++;
            }

            this.wornGear = (now, character, worn, slots);
            return (worn, slots);
        }

        private GearItem? GearItemOf(IntPtr entity, IReadOnlyList<string>? tooltip) => entity == IntPtr.Zero ? null
            : new GearItem(this.inventory.ItemBaseId(entity), this.inventory.ItemBaseName(entity), tooltip is { Count: > 0 } ? tooltip[0] : string.Empty,
                this.inventory.ItemKind(entity).Kind == UniqueKind, tooltip);

        /// <summary>
        ///     What the build says of a grid cell: on an equipment slot, its piece against the worn item, said after the item's
        ///     own lines; on any other item that can be worn, a short line per slot before them and each modifier after.
        /// </summary>
        /// <param name="tooltip">The item's whole tooltip; nothing is said of an item until it is read.</param>
        private (List<string> First, List<string> Last) BuildGearLines(IntPtr grid, InventoryName? inventoryName, int column, IntPtr entity, IReadOnlyList<string> tooltip)
        {
            var none = (new List<string>(), new List<string>());
            if (this.GearPlan() is not { } active || (entity != IntPtr.Zero && tooltip.Count == 0))
            {
                return none;
            }

            this.WornGear();
            var text = tooltip.Aggregate(tooltip.Count, (hash, line) => HashCode.Combine(hash, line));
            if (Equals(this.gearMemo.State, this.GearState(active)) && this.gearMemo.Entity == entity && this.gearMemo.Grid == grid &&
                this.gearMemo.Column == column && this.gearMemo.Tooltip == text)
            {
                return this.gearMemo.Said;
            }

            var said = this.GearLinesNow(active, inventoryName, column, entity, tooltip);
            this.gearMemo = (this.GearState(active), entity, grid, column, text, said);
            return said;
        }

        /// <summary>What the gear's lines are made from besides the item itself: the build, the level, the worn items with their lines read, and the catalogs.</summary>
        private object GearState((BuildPlan Plan, int Level) active) => (active.Plan, active.Level, this.gearVersion, BuildGear.Bases, BuildGear.Texts);

        private (List<string> First, List<string> Last) GearLinesNow((BuildPlan Plan, int Level) active, InventoryName? inventoryName, int column, IntPtr entity, IReadOnlyList<string> tooltip)
        {
            var (first, last) = (new List<string>(), new List<string>());
            var (worn, _) = this.WornGear();
            GearFit Fit(BuildGearEntry entry, IntPtr item, IReadOnlyList<string>? lines) =>
                BuildGear.Fit(entry, this.GearItemOf(item, lines), BuildGear.Bases, BuildGear.Texts, Localization.Language);

            // The slot's own cell holds what the server holds there (read again after a swap); a stash tab named after a slot does not.
            var own = inventoryName is { } name && IsEquipment(name) ? (Slot: name.ToString(), X: name == InventoryName.Flask1 ? column : 0) : (Slot: string.Empty, X: 0);
            if (own.Slot.Length > 0 && worn.GetValueOrDefault(own) != entity)
            {
                this.wornGear = null;
                (worn, _) = this.WornGear();
            }

            if (own.Slot.Length > 0 && worn.GetValueOrDefault(own) == entity)
            {
                if (BuildGear.EntryFor(active.Plan, own.Slot, own.X, active.Level) is { } wanted)
                {
                    if (entity != IntPtr.Zero && !(this.wornLines.GetValueOrDefault(entity)?.SequenceEqual(tooltip) ?? false))
                    {
                        this.wornLines[entity] = tooltip.ToList();
                        this.gearVersion++;
                    }

                    last.AddRange(Announcements.BuildGearSlot(Fit(wanted, entity, entity == IntPtr.Zero ? null : tooltip), active.Level, BuildGear.Bases, BuildGear.Texts));
                }

                return (first, last);
            }

            if (entity == IntPtr.Zero)
            {
                return (first, last);
            }

            var slots = BuildGear.SlotsFor(this.inventory.ItemBaseId(entity))
                .Select(slot => (Slot: slot, Entry: BuildGear.EntryFor(active.Plan, slot.Slot, slot.X, active.Level))).Where(slot => slot.Entry != null).ToList();
            foreach (var (slot, entry) in slots)
            {
                var fit = Fit(entry!, entity, tooltip);
                var held = worn.GetValueOrDefault(slot);
                var line = Announcements.BuildGearItem(fit, held == IntPtr.Zero ? null : Fit(entry!, held, this.wornLines.GetValueOrDefault(held)), active.Level);
                if (line.Length == 0)
                {
                    continue;
                }

                first.Add(line);
                if (fit.Mods.Count > 0)
                {
                    // Two slots take it (rings, weapon sets): each one's modifiers under its name.
                    last.AddRange(slots.Count > 1 ? new[] { Announcements.GearSlotName(slot.Slot, slot.X) } : Array.Empty<string>());
                    last.AddRange(Announcements.GearMods(fit, BuildGear.Texts));
                }
            }

            return (first, last);
        }

        /// <summary>On the player's own inventory, entered from outside a stash or a shop: how the worn gear stands against the build.</summary>
        private AccessibleUiNode WithBuildGear(AccessibleUiNode node, bool hovered)
        {
            if (!this.InGame || hovered)
            {
                return node;
            }

            if (node.Address != this.gearFocus.Address)
            {
                var place = node.Role != UiRole.GridCell ? GearPlace.Elsewhere
                    : this.inventory.InventoryOf(node.Address) is { } name && (name == InventoryName.MainInventory1 || IsEquipment(name)) ? GearPlace.Own
                    : GearPlace.OtherGrid;
                // Coming back from the stash or a merchant's stock is no entering: said once per visit, not per crossing.
                var show = place == GearPlace.Own && (this.gearFocus.Place == GearPlace.Own ? this.gearFocus.Show : this.gearFocus.Place != GearPlace.OtherGrid);
                this.gearFocus = (node.Address, place, show);
            }

            if (!this.gearFocus.Show || this.GearPlan() is not { } active)
            {
                return node;
            }

            var notes = this.GearSummaryLines(active);
            return notes.Count == 0 ? node : node.With(node.Text, node.Tooltip, node.PanelNotes.Concat(notes).ToList());
        }

        /// <summary>The slots whose piece applies at this level, each against what is worn there; modifiers only where a tooltip was read.</summary>
        private IReadOnlyList<string> GearSummaryLines((BuildPlan Plan, int Level) active)
        {
            var (worn, known) = this.WornGear();
            if (Equals(this.gearSummary.State, this.GearState(active)))
            {
                return this.gearSummary.Lines;
            }
            var fits = new List<GearFit>();
            foreach (var slot in active.Plan.Gear.Select(entry => (entry.Slot, X: entry.SlotX)).Distinct())
            {
                // An inventory the reader does not list (charms) is not known to be empty.
                if (!known.Contains(slot.Slot) || BuildGear.EntryFor(active.Plan, slot.Slot, slot.X, active.Level) is not { } entry || entry.FromLevel > active.Level)
                {
                    continue;
                }

                var entity = worn.GetValueOrDefault(slot);
                fits.Add(BuildGear.Fit(entry, this.GearItemOf(entity, this.wornLines.GetValueOrDefault(entity)), BuildGear.Bases, BuildGear.Texts, Localization.Language));
            }

            IReadOnlyList<string> lines = fits.Count == 0 ? Array.Empty<string>() : Announcements.BuildGearSummary(BuildGear.Summary(fits)).ToList();
            this.gearSummary = (this.GearState(active), lines);
            return lines;
        }
    }
}
