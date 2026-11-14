namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>One piece of a build plan's gear: its slot, the base or the unique the author wants there, the modifiers wanted in his order and words, and the levels it applies to.</summary>
    public sealed record BuildGearEntry(string Slot, int SlotX, string Base, IReadOnlyList<string> Wanted, string Unique, int FromLevel, int ToLevel)
    {
        public bool IsUnique => this.Unique.Length > 0;

        /// <summary>The unique's name without the author's note in brackets ("Darkness Enthroned (body armour)").</summary>
        public string UniqueName => this.Unique.IndexOf(" (", StringComparison.Ordinal) is var note and > 0 ? this.Unique[..note] : this.Unique;
    }

    /// <summary>Whether an item is of the base (or is the unique) the build wants; unknown when neither the game's files nor the names can tell.</summary>
    public enum BaseFit
    {
        Unknown,
        Same,
        Other,
    }

    /// <summary>A wanted modifier on an item: there with the number or more, there with less, not there, or not comparable.</summary>
    public enum ModFit
    {
        Unknown,
        Met,
        Below,
        Missing,
    }

    /// <summary>The defences an item base gives.</summary>
    [Flags]
    public enum Defence
    {
        None = 0,
        Armour = 1,
        Evasion = 2,
        EnergyShield = 4,
    }

    /// <summary>One wanted modifier set against an item: the author's words, the game's line it is (null when unknown) and the item's own line that answers it.</summary>
    public sealed record ModStatus(string Wanted, ModLine? Line, ModFit Fit, string Held);

    /// <summary>
    ///     An item as the game shows it: its base id and name, its tooltip's title, and its lines: its tooltip's, or the
    ///     modifier lines made from its modifiers in memory (<see cref="ItemModLines"/>); null while neither is known.
    /// </summary>
    /// <param name="LinesPartial">Some line of the item could not be made: a modifier not found among <paramref name="Lines"/> is not known to be missing.</param>
    public sealed record GearItem(string BaseId, string BaseName, string Title, bool IsUnique, IReadOnlyList<string>? Lines, bool LinesPartial = false);

    /// <summary>A build piece set against an item; no item for an empty slot.</summary>
    public sealed record GearFit(BuildGearEntry Entry, GearItem? Item, BaseFit Base, IReadOnlyList<ModStatus> Mods)
    {
        /// <summary>Whether the item's modifier lines were there to compare.</summary>
        public bool ModsRead => this.Item?.Lines != null;

        /// <summary>The wanted modifiers the item has, whatever their number.</summary>
        public int Has => this.Mods.Count(mod => mod.Fit is ModFit.Met or ModFit.Below);

        /// <summary>The wanted modifiers that could be looked for on the item: only among these is one had or missing.</summary>
        public int Compared => this.Mods.Count(mod => mod.Fit != ModFit.Unknown);

        /// <summary>How far the slot is from the build: everything for an empty one, else a wrong base and each modifier known to be missing.</summary>
        public int Shortfall => this.Item == null ? 1 + this.Mods.Count
            : (this.Base == BaseFit.Other ? 1 : 0) + this.Mods.Count(mod => mod.Fit == ModFit.Missing);
    }

    /// <summary>The worn gear against the build: slots on the wanted base, modifiers had of those compared on the pieces whose lines were read, and the slot furthest off.</summary>
    public sealed record GearSummary(int Slots, int OnBase, int PiecesRead, int Has, int Wanted, GearFit? Furthest);

    /// <summary>The build's gear set against the items the game shows. Pure; the game files' names and texts come through <see cref="Bases"/> and <see cref="Texts"/>.</summary>
    public static class BuildGear
    {
        /// <summary>The file's last level when a piece gives no interval (a unique).</summary>
        private const int LastLevel = 100;

        /// <summary>The flask inventory holds life at column 0 and mana at 1 (Build Planner files, <c>slot_x</c>).</summary>
        public const string FlaskSlot = "Flask1";
        public const int ManaFlaskX = 1;

        private static readonly Regex Ordinal = new(@"^\s*\d+\.\s*", RegexOptions.Compiled);
        private static readonly Regex Number = new(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>The game's lines that give narrower ones too, as its English files write them (seen live 2026-11-10: a belt's "+9% to all Elemental Resistances" against a wanted cold and lightning resistance).</summary>
        private static readonly (string Wide, string[] Narrow)[] Wider =
        {
            ("+1% to all Elemental Resistances", new[] { "+1% to Fire Resistance", "+1% to Cold Resistance", "+1% to Lightning Resistance" }),
            ("+1 to all Attributes", new[] { "+1 to Strength", "+1 to Dexterity", "+1 to Intelligence" }),
        };

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IModTexts, Dictionary<string, string>> WiderByTexts = new();

        /// <summary>
        ///     Where an item goes by the folder of its id (item paths of worn and dropped items, play logs 2026-11-08..10):
        ///     the equipment inventories that take it, named as the build files and the game name them.
        /// </summary>
        private static readonly (string Folder, string[] Slots)[] Folders =
        {
            ("/Armours/Helmets/", new[] { "Helm1" }),
            ("/Armours/BodyArmours/", new[] { "BodyArmour1" }),
            ("/Armours/Gloves/", new[] { "Gloves1" }),
            ("/Armours/Boots/", new[] { "Boots1" }),
            ("/Armours/Shields/", new[] { "Offhand1", "Offhand2" }),
            ("/Weapons/", new[] { "Weapon1", "Weapon2" }),
            ("/Rings/", new[] { "Ring1", "Ring2" }),
            ("/Amulets/", new[] { "Amulet1" }),
            ("/Belts/", new[] { "Belt1" }),
        };

        /// <summary>Gets or sets the game files' item bases; none until the plugin loads them.</summary>
        public static IItemBases Bases { get; set; } = new NoCatalog();

        /// <summary>Gets or sets the game files' modifier texts; none until the plugin loads them.</summary>
        public static IModTexts Texts { get; set; } = new NoCatalog();

        /// <summary>The file's "inventory_slots": a base with numbered modifiers in "additional_text", or a "unique_name".</summary>
        public static IReadOnlyList<BuildGearEntry> Parse(JsonElement root)
        {
            var gear = new List<BuildGearEntry>();
            if (!root.TryGetProperty("inventory_slots", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return gear;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || Text(entry, "inventory_id") is not { Length: > 0 } slot)
                {
                    continue;
                }

                var lines = (Text(entry, "additional_text") ?? string.Empty).Split('\n').Select(BuildNotes.Plain).Where(line => line.Length > 0).ToList();
                var unique = BuildNotes.Plain(Text(entry, "unique_name") ?? string.Empty);
                if (lines.Count == 0 && unique.Length == 0)
                {
                    continue;
                }

                var (from, to) = (1, LastLevel);
                if (entry.TryGetProperty("level_interval", out var interval) && interval.ValueKind == JsonValueKind.Array && interval.GetArrayLength() == 2 &&
                    BuildPlan.Whole(interval[0], out var first) && BuildPlan.Whole(interval[1], out var last))
                {
                    (from, to) = (first, last);
                }

                var x = entry.TryGetProperty("slot_x", out var column) && BuildPlan.Whole(column, out var number) ? number : 0;
                gear.Add(new BuildGearEntry(slot, x, lines.Count > 0 ? lines[0] : string.Empty,
                    lines.Skip(1).Select(line => Ordinal.Replace(line, string.Empty)).Where(line => line.Length > 0).ToList(), unique, from, to));
            }

            return gear;
        }

        /// <summary>
        ///     The build's piece for a slot at a level: the latest one that applies, else the first to come; null when the
        ///     build has none there any more.
        /// </summary>
        public static BuildGearEntry? EntryFor(BuildPlan plan, string slot, int slotX, int level)
        {
            var open = plan.Gear.Where(entry => entry.Slot == slot && entry.SlotX == slotX && entry.ToLevel >= level).ToList();
            return open.Where(entry => entry.FromLevel <= level).OrderByDescending(entry => entry.FromLevel).FirstOrDefault()
                ?? open.OrderBy(entry => entry.FromLevel).FirstOrDefault();
        }

        /// <summary>
        ///     Ends the id of a gamble's random item ("…/OneHandSpears/FourSpearGamble", Finn's stock, memory 2026-11-10, 0.5.5f):
        ///     it has neither base nor modifiers until bought, so the build says nothing of it.
        /// </summary>
        private const string GambleSuffix = "Gamble";

        /// <summary>The slots an item would go in, by its id's folder; none for what is not worn or is not told apart (charms, a gamble's random item).</summary>
        public static IReadOnlyList<(string Slot, int X)> SlotsFor(string itemId)
        {
            if (itemId.EndsWith(GambleSuffix, StringComparison.Ordinal))
            {
                return Array.Empty<(string, int)>();
            }

            if (itemId.Contains("/Flasks/", StringComparison.OrdinalIgnoreCase))
            {
                return itemId.Contains("FlaskLife", StringComparison.OrdinalIgnoreCase) ? new[] { (FlaskSlot, 0) }
                    : itemId.Contains("FlaskMana", StringComparison.OrdinalIgnoreCase) ? new[] { (FlaskSlot, ManaFlaskX) }
                    : Array.Empty<(string, int)>();
            }

            foreach (var (folder, slots) in Folders)
            {
                if (itemId.Contains(folder, StringComparison.OrdinalIgnoreCase))
                {
                    return slots.Select(slot => (slot, 0)).ToList();
                }
            }

            return Array.Empty<(string, int)>();
        }

        /// <summary>The defences a base gives by its own values; none for an unknown base or what is no armour piece.</summary>
        public static Defence DefenceOf(ItemBase? item) => item == null ? Defence.None
            : (item.Armour > 0 ? Defence.Armour : Defence.None) | (item.Evasion > 0 ? Defence.Evasion : Defence.None) |
              (item.EnergyShield > 0 ? Defence.EnergyShield : Defence.None);

        /// <summary>A build piece set against an item (null for an empty slot): what cannot be compared stays unknown, never met or missing.</summary>
        public static GearFit Fit(BuildGearEntry entry, GearItem? item, IItemBases bases, IModTexts texts, string language)
        {
            var held = item?.Lines?.Select(line => (Text: line, Line: texts.Parse(line), Shape: Shape(line))).ToList();
            var mods = entry.Wanted.Select(wanted => Match(wanted, held, item?.LinesPartial ?? false, texts, language)).ToList();
            return new GearFit(entry, item, item == null ? BaseFit.Unknown : BaseFitOf(entry, item, bases, language), mods);
        }

        /// <summary>
        ///     What an item's lines made from memory give the comparison: the certain ones, partial when some line is in
        ///     doubt, and none at all (an item not read) when every line is.
        /// </summary>
        public static (IReadOnlyList<string>? Lines, bool Partial) LinesFromMemory(IReadOnlyList<ItemModLine> made)
        {
            var certain = made.Where(line => line.Certain).Select(line => line.Text).ToList();

            // A line outside the description file is in doubt and still answers no wanted modifier: both spears were
            // "not compared" for their skill grant until their slots were focused (live check 2026-11-10).
            var partial = made.Any(line => !line.Certain && !line.OutsideTexts);
            return (partial && certain.Count == 0 ? null : certain, partial);
        }

        /// <summary>
        ///     An item's lines for the slots that take it: one line each before the item and each slot's modifiers under its
        ///     name after it; two slots that say the same of it (both rings with nothing worn) are told as one.
        /// </summary>
        /// <param name="told">Per slot: its name, its line (which holds the name) and its modifiers' verdicts.</param>
        /// <param name="several">More than one slot takes the item.</param>
        /// <returns>The lines, and whether the last ones stand under slot names.</returns>
        public static (List<string> First, List<string> Last, bool Headed) SlotLines(IReadOnlyList<(string Slot, string Line, IReadOnlyList<string> Mods)> told, bool several)
        {
            if (told.Count == 2 && told[0].Slot.Length > 0 && told[0].Mods.SequenceEqual(told[1].Mods))
            {
                var at = told[0].Line.IndexOf(told[0].Slot, StringComparison.Ordinal);
                if (at >= 0 && at == told[1].Line.IndexOf(told[1].Slot, StringComparison.Ordinal) &&
                    told[0].Line.Remove(at, told[0].Slot.Length) == told[1].Line.Remove(at, told[1].Slot.Length))
                {
                    return (new List<string> { told[0].Line.Insert(at + told[0].Slot.Length, ", " + told[1].Slot) }, told[0].Mods.ToList(), false);
                }
            }

            var last = new List<string>();
            foreach (var slot in told.Where(slot => slot.Mods.Count > 0))
            {
                last.AddRange(several ? new[] { slot.Slot } : Array.Empty<string>());
                last.AddRange(slot.Mods);
            }

            return (told.Select(slot => slot.Line).ToList(), last, several && last.Count > 0);
        }

        /// <summary>
        ///     Each line of the item the build judges, said once: the verdict stands where the item shows the line ("+18 to
        ///     maximum Life, the build asks for 40"), and the verdicts of no line of its own are left to say after it.
        /// </summary>
        public static (List<string> Details, List<string> Left) JudgedInPlace(IReadOnlyList<string> details, IReadOnlyList<string> verdicts)
        {
            var lines = details.ToList();
            var left = new List<string>();
            foreach (var verdict in verdicts)
            {
                // The longest line the verdict opens with: "Adds 1, 2 fire, the build asks for 3" judges "Adds 1, 2 fire", not "Adds 1".
                var at = -1;
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line.Length > 0 && verdict.Length > line.Length && verdict[line.Length] == ',' && verdict.StartsWith(line, StringComparison.Ordinal) &&
                        (at < 0 || line.Length > lines[at].Length))
                    {
                        at = i;
                    }
                }

                if (at >= 0)
                {
                    lines[at] = verdict;
                }
                else if (!lines.Contains(verdict))
                {
                    left.Add(verdict);
                }
            }

            return (lines, left);
        }

        /// <summary>The worn pieces against the build; the furthest is the first in the build's order among the worst.</summary>
        public static GearSummary Summary(IReadOnlyList<GearFit> fits)
        {
            var read = fits.Where(fit => fit.ModsRead).ToList();
            return new GearSummary(fits.Count, fits.Count(fit => fit.Base == BaseFit.Same), read.Count, read.Sum(fit => fit.Has), read.Sum(fit => fit.Compared),
                fits.Where(fit => fit.Shortfall > 0).OrderByDescending(fit => fit.Shortfall).FirstOrDefault());
        }

        /// <summary>The numbers written in a line, in order ("Adds 6 to 11 Physical Damage" → 6, 11).</summary>
        public static IReadOnlyList<double> NumbersOf(string text) =>
            Number.Matches(text).Select(match => double.Parse(match.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture)).ToList();

        /// <summary>A line without its numbers, case and spacing: two lines of one shape are the same modifier in one language.</summary>
        public static string Shape(string text) => Spaces.Replace(Number.Replace(text, "#"), " ").Trim().TrimEnd('.').ToLowerInvariant();

        private static string? Text(JsonElement entry, string name) =>
            entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static bool SameText(string one, string other) =>
            one.Length > 0 && string.Equals(Spaces.Replace(one, " ").Trim(), Spaces.Replace(other, " ").Trim(), StringComparison.OrdinalIgnoreCase);

        private static BaseFit BaseFitOf(BuildGearEntry entry, GearItem item, IItemBases bases, string language)
        {
            if (entry.IsUnique)
            {
                // A unique's name in the game's language is not in the files read: only its own words can say it is this one.
                return !item.IsUnique ? BaseFit.Other : SameText(entry.UniqueName, item.Title) ? BaseFit.Same : BaseFit.Unknown;
            }

            if (bases.ByName(entry.Base) is not { } wanted)
            {
                return SameText(entry.Base, item.BaseName) ? BaseFit.Same : BaseFit.Unknown;
            }

            if ((item.BaseId.Length > 0 && SameText(BuildGems.KeyOf(wanted.Id), BuildGems.KeyOf(item.BaseId))) || SameText(wanted.Name(language), item.BaseName))
            {
                return BaseFit.Same;
            }

            return item.BaseName.Length > 0 || bases.ById(item.BaseId) != null ? BaseFit.Other : BaseFit.Unknown;
        }

        /// <summary>
        ///     The item's line that is the wanted modifier: by the game's texts, else by the same words around other numbers
        ///     (the wanted line as written, or as the game says it). A wanted line the texts do not know is never missing,
        ///     nor is any when the item's lines are <paramref name="partial"/>.
        /// </summary>
        private static ModStatus Match(string wanted, List<(string Text, ModLine? Line, string Shape)>? held, bool partial, IModTexts texts, string language)
        {
            var line = texts.Parse(wanted);
            if (held == null)
            {
                return new ModStatus(wanted, line, ModFit.Unknown, string.Empty);
            }

            var shapes = new List<string> { Shape(wanted) };
            if (line != null && texts.Say(line, language) is { Length: > 0 } said)
            {
                shapes.Add(Shape(said));
            }

            (string Text, IReadOnlyList<double> Numbers)? best = null;
            foreach (var candidate in held)
            {
                var numbers = line != null && candidate.Line?.Template == line.Template ? candidate.Line.Numbers
                    : shapes.Contains(candidate.Shape) ? NumbersOf(candidate.Text) : null;
                if (numbers != null && (best == null || First(numbers) > First(best.Value.Numbers)))
                {
                    best = (candidate.Text, numbers);
                }
            }

            // A line that gives the wanted one among others ("to all Elemental Resistances" for one resistance) adds to it.
            if (line != null && WiderOf(texts).TryGetValue(line.Template, out var wider))
            {
                foreach (var whole in held.Where(other => other.Line?.Template == wider && other.Line.Numbers.Count > 0))
                {
                    IReadOnlyList<double> sum = best is { } narrow ? new[] { First(narrow.Numbers) + whole.Line!.Numbers[0] } : whole.Line!.Numbers;
                    best = (best is { } before ? $"{before.Text}, {whole.Text}" : whole.Text, sum);
                }
            }

            if (best is not { } found)
            {
                return new ModStatus(wanted, line, line != null && !partial ? ModFit.Missing : ModFit.Unknown, string.Empty);
            }

            return new ModStatus(wanted, line, Meets(line?.Numbers ?? NumbersOf(wanted), found.Numbers) ? ModFit.Met : ModFit.Below, found.Text);
        }

        /// <summary>
        ///     Per narrow line's template, the template of the line that gives it too, both found by parsing the game's
        ///     English lines with the texts at hand: a pair the texts do not know is left out, so nothing is assumed.
        /// </summary>
        private static Dictionary<string, string> WiderOf(IModTexts texts) => WiderByTexts.GetValue(texts, known =>
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (wide, narrow) in Wider)
            {
                if (known.Parse(wide)?.Template is not { } template)
                {
                    continue;
                }

                foreach (var one in narrow)
                {
                    if (known.Parse(one)?.Template is { } part && part != template)
                    {
                        map[part] = template;
                    }
                }
            }

            return map;
        });

        private static double First(IReadOnlyList<double> numbers) => numbers.Count > 0 ? numbers[0] : 0;

        /// <summary>By the first number, as the author orders a line by it ("Adds 6 to 11"); a line without one is met by being there.</summary>
        private static bool Meets(IReadOnlyList<double> wanted, IReadOnlyList<double> held) =>
            wanted.Count == 0 || held.Count == 0 || (wanted[0] >= 0 ? held[0] >= wanted[0] : held[0] <= wanted[0]);

        /// <summary>No catalog loaded: every name and line is unknown.</summary>
        private sealed class NoCatalog : IItemBases, IModTexts
        {
            public ItemBase? ByName(string name) => null;

            public ItemBase? ById(string id) => null;

            public ModLine? Parse(string text) => null;

            public string Say(ModLine line, string language) => string.Empty;
        }
    }
}
