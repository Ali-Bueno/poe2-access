namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of the Build Planner's gear: what the build wants in a slot set against an item (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>A slot as the build files name it: the equipment slots' own names, the flasks by what they hold; the id itself for one without a name.</summary>
        public static string GearSlotName(string slot, int x) =>
            slot == BuildGear.FlaskSlot ? (x == BuildGear.ManaFlaskX ? T("Mana flask", "Frasco de maná") : T("Life flask", "Frasco de vida"))
            : System.Enum.TryParse<GameReader.RemoteEnums.InventoryName>(slot, out var inventory) && SlotName(inventory) is { } name ? name
            : slot;

        /// <summary>"armour", "evasion, energy shield": what a base's own values give; empty for none.</summary>
        public static string DefenceKind(Defence defence)
        {
            var kinds = new List<string>();
            if (defence.HasFlag(Defence.Armour))
            {
                kinds.Add(T("armour", "armadura"));
            }

            if (defence.HasFlag(Defence.Evasion))
            {
                kinds.Add(T("evasion", "evasión"));
            }

            if (defence.HasFlag(Defence.EnergyShield))
            {
                kinds.Add(T("energy shield", "escudo de energía"));
            }

            return string.Join(", ", kinds);
        }

        /// <summary>
        ///     On an equipment slot: "Build: Shielded Helm, armour, from level 33, another base", then each wanted modifier
        ///     against the worn item's own line, or the list of them while there is nothing to compare.
        /// </summary>
        /// <param name="lacksSaid">The missing modifiers were said before the item (<see cref="BuildGearLacks"/>): not again here.</param>
        public static IEnumerable<string> BuildGearSlot(GearFit fit, int level, IItemBases bases, IModTexts texts, bool lacksSaid = false)
        {
            var parts = new List<string> { GearWanted(fit.Entry, bases) };
            if (fit.Entry.FromLevel > level)
            {
                parts.Add(GearFromLevel(fit.Entry.FromLevel));
            }

            parts.AddRange(GearBaseFit(fit, bases));
            var wanted = string.Join(", ", parts);
            yield return T($"Build: {wanted}", $"Build: {wanted}");
            if (fit.Mods.Count == 0)
            {
                yield break;
            }

            if (!fit.ModsRead)
            {
                yield return T("Asks for: ", "Pide: ") + string.Join("; ", fit.Mods.Select(mod => GearWantedMod(mod, texts)));
                yield break;
            }

            // One line of the item can answer two wanted ones ("to all Elemental Resistances" for cold and lightning): said once.
            foreach (var line in fit.Mods.Where(mod => !(lacksSaid && mod.Fit == ModFit.Missing)).Select(mod => GearModLine(mod, texts)).Distinct())
            {
                yield return line;
            }
        }

        /// <summary>
        ///     Right after a worn piece's name: how many of the wanted modifiers it has and which it lacks, so what to look
        ///     for is heard without waiting for the whole item (user 2026-11-10: the build's lines "all come at the end").
        ///     Empty when the item's lines were not read or none of the wanted ones could be looked for.
        /// </summary>
        public static string BuildGearLacks(GearFit fit, IModTexts texts)
        {
            if (!fit.ModsRead || fit.Compared == 0)
            {
                return string.Empty;
            }

            var (has, all) = (fit.Has, fit.Compared);
            var parts = new List<string> { T($"{has} of {all} modifiers", $"{has} de {all} modificadores") };
            parts.AddRange(fit.Mods.Where(mod => mod.Fit == ModFit.Missing).Select(mod => GearModLine(mod, texts)));
            var said = string.Join(". ", parts);
            return T($"Build: {said}", $"Build: {said}");
        }

        /// <summary>
        ///     On an item that could go in a slot: "Build, helmet: 2 of 3 modifiers, another base, more than the worn one,
        ///     which has 1"; empty when nothing of it can be told.
        /// </summary>
        /// <param name="worn">The slot's worn piece against the same build entry; null when the slot is empty.</param>
        public static string BuildGearItem(GearFit fit, GearFit? worn, int level)
        {
            var parts = new List<string>();
            if (fit.Entry.IsUnique)
            {
                parts.Add(GearWanted(fit.Entry, BuildGear.Bases));
            }

            // Only the modifiers that could be looked for are counted: one not compared is neither had nor missing.
            var counted = fit.ModsRead && fit.Compared > 0;
            if (counted)
            {
                var (has, all, rest) = (fit.Has, fit.Compared, fit.Mods.Count - fit.Compared);
                parts.Add(T($"{has} of {all} modifiers", $"{has} de {all} modificadores"));
                if (rest > 0)
                {
                    parts.Add(T($"{rest} not compared", $"{rest} sin comparar"));
                }
            }

            parts.AddRange(GearBaseFit(fit, null));
            if (parts.Count == 0 || (fit.Entry.IsUnique && fit.Base == BaseFit.Unknown))
            {
                return string.Empty;
            }

            if (fit.Entry.FromLevel > level)
            {
                parts.Add(GearFromLevel(fit.Entry.FromLevel));
            }

            if (worn == null)
            {
                parts.Add(T("nothing worn there", "nada puesto"));
            }
            else if (counted && worn.ModsRead && worn.Compared == fit.Compared)
            {
                var theirs = worn.Has;
                parts.Add(fit.Has > theirs ? T($"more than the worn one, which has {theirs}", $"más que el puesto, que tiene {theirs}")
                    : fit.Has < theirs ? T($"fewer than the worn one, which has {theirs}", $"menos que el puesto, que tiene {theirs}")
                    : T("same as the worn one", "igual que el puesto"));
            }

            var slot = GearSlotName(fit.Entry.Slot, fit.Entry.SlotX);
            var said = string.Join(", ", parts);
            return T($"Build, {slot}: {said}", $"Build, {slot}: {said}");
        }

        /// <summary>Each wanted modifier against the item's own line: met, with less than asked, missing, or not comparable (the author's words).</summary>
        public static IEnumerable<string> GearMods(GearFit fit, IModTexts texts) => fit.Mods.Select(mod => GearModLine(mod, texts)).Distinct();

        private static string GearModLine(ModStatus mod, IModTexts texts)
        {
            var (held, asked, wanted, words) = (mod.Held, GearNumbers(mod), GearWantedMod(mod, texts), mod.Wanted);
            return mod.Fit switch
            {
                ModFit.Met => T($"{held}, met", $"{held}, cumple"),
                ModFit.Below => T($"{held}, the build asks for {asked}", $"{held}, la build pide {asked}"),
                ModFit.Missing => T($"Missing {wanted}", $"Falta {wanted}"),
                _ => T($"Not compared: {words}", $"Sin comparar: {words}"),
            };
        }

        /// <summary>On entering the inventory: "Build: 3 of 12 pieces on its base, 5 of 14 modifiers, read on 6 of 12 pieces", then the slot furthest off.</summary>
        public static IEnumerable<string> BuildGearSummary(GearSummary summary)
        {
            var (onBase, slots, has, wanted, read) = (summary.OnBase, summary.Slots, summary.Has, summary.Wanted, summary.PiecesRead);
            var count = T($"Build: {onBase} of {slots} pieces on its base", $"Build: {onBase} de {slots} piezas con su base");
            yield return read == 0 || wanted == 0 ? count
                : count + ", " + T($"{has} of {wanted} modifiers, read on {read} of {slots} pieces", $"{has} de {wanted} modificadores, leídos en {read} de {slots} piezas");
            if (summary.Furthest is { } furthest)
            {
                var slot = GearSlotName(furthest.Entry.Slot, furthest.Entry.SlotX);
                yield return T($"Furthest from the build: {slot}", $"Más lejos de la build: {slot}");
            }
        }

        /// <summary>The piece the build wants: the base by its name in the game's language with its defences, or the author's own words; a unique by the author's name.</summary>
        private static string GearWanted(BuildGearEntry entry, IItemBases bases)
        {
            if (entry.IsUnique)
            {
                var unique = entry.Unique;
                return T($"unique {unique}", $"único {unique}");
            }

            var known = bases.ByName(entry.Base);
            var kind = DefenceKind(BuildGear.DefenceOf(known));
            var name = known?.Name(Localization.Language) ?? entry.Base;
            return kind.Length > 0 ? $"{name}, {kind}" : name;
        }

        private static string GearFromLevel(int level) => T($"from level {level}", $"desde el nivel {level}");

        /// <summary>Whether the item is the wanted base (with its own defences when they differ) or the wanted unique; nothing when unknown.</summary>
        private static IEnumerable<string> GearBaseFit(GearFit fit, IItemBases? bases)
        {
            if (fit.Item == null || fit.Base == BaseFit.Unknown)
            {
                yield break;
            }

            if (fit.Entry.IsUnique)
            {
                yield return fit.Base == BaseFit.Same ? T("it is this one", "es este") : T("it is not this one", "no es este");
                yield break;
            }

            yield return fit.Base == BaseFit.Same ? T("same base", "misma base") : T("another base", "otra base");
            if (fit.Base == BaseFit.Other && bases != null && BuildGear.DefenceOf(bases.ById(fit.Item.BaseId)) is var own && own != Defence.None &&
                own != BuildGear.DefenceOf(bases.ByName(fit.Entry.Base)))
            {
                var kind = DefenceKind(own);
                yield return T($"yours gives {kind}", $"la tuya da {kind}");
            }
        }

        /// <summary>A wanted modifier as the game says it, else in the author's words.</summary>
        private static string GearWantedMod(ModStatus mod, IModTexts texts) =>
            mod.Line != null && texts.Say(mod.Line, Localization.Language) is { Length: > 0 } said ? said : mod.Wanted;

        /// <summary>The wanted line's numbers: "40", "6 to 11".</summary>
        private static string GearNumbers(ModStatus mod)
        {
            var numbers = (mod.Line?.Numbers ?? BuildGear.NumbersOf(mod.Wanted)).Select(number => number.ToString("0.##", CultureInfo.InvariantCulture)).ToList();
            if (numbers.Count != 2)
            {
                return string.Join(", ", numbers);
            }

            var (low, high) = (numbers[0], numbers[1]);
            return T($"{low} to {high}", $"{low} a {high}");
        }
    }
}
