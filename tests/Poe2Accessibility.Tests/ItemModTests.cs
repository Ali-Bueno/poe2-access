namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>An item's modifier lines made from its modifiers in memory, without its tooltip (docs/research/item-mods.md).</summary>
    public static class ItemModTests
    {
        private const string Evidence = "item-mods-2026-11-10.txt";

        private const string Stats =
            "{\"hidden\":[\"level\"],\"mods\":{" +
            "\"IncreasedLife3\":\"base_maximum_life 30 39\",\"IncreasedMana1\":\"base_maximum_mana 10 14\"," +
            "\"LocalIncreasedArmourAndLife2\":\"local_physical_damage_reduction_rating_+% 14 20 base_maximum_life 11 19\"," +
            "\"AddedCold3\":\"minimum_added_cold_damage 5 6 maximum_added_cold_damage 9 11\"," +
            "\"ReducedRequirements1\":\"local_attribute_requirements_+% -15 -15\"," +
            "\"LifeRegeneration1\":\"base_life_regeneration_rate_per_minute 60 400\"," +
            "\"Level1\":\"level 1 1\",\"Undescribed1\":\"stat_without_text 1 5\",\"Twice1\":\"stat_described_twice 1 5\"," +
            "\"Passive1\":\"named_passive 1 9000\",\"Armour9\":\"flat_armour 500 2000\",\"FireResist1\":\"base_fire_damage_resistance_% -30 30\"," +
            "\"Leech1\":\"life_leech_permyriad 100 900\",\"Duration1\":\"duration_ms 500 9000\",\"Tenth1\":\"tenths 1 90\",\"Wave1\":\"waves 1 90\",\"English1\":\"only_english 1 9\"}," +
            "\"unproven\":{\"SixStats1\":\"base_maximum_mana stat_in_a_later_column\"}}";

        private const string Texts =
            "{\"source\":\"test\",\"texts\":[" +
            "{\"stats\":\"local_physical_damage_reduction_rating_+%\",\"texts\":{\"en\":[\"1|# \\\"{0}% increased [Armour]\\\"\",\"#|-1 \\\"{0}% reduced [Armour]\\\" negate 1\"],\"es\":[\"1|# \\\"[Armour|Armadura] aumentada un {0}%\\\"\",\"#|-1 \\\"[Armour|Armadura] reducida un {0}%\\\" negate 1\"]}}," +
            "{\"stats\":\"base_maximum_life\",\"texts\":{\"en\":[\"# \\\"{0:+d} to maximum Life\\\"\"],\"es\":[\"# \\\"{0:+d} a la vida máxima\\\"\"]}}," +
            "{\"stats\":\"base_maximum_mana\",\"texts\":{\"en\":[\"# \\\"{0:+d} to maximum Mana\\\"\"],\"es\":[\"# \\\"{0:+d} al maná máximo\\\"\"]}}," +
            "{\"stats\":\"minimum_added_cold_damage maximum_added_cold_damage\",\"texts\":{\"en\":[\"# # \\\"Adds {0} to {1} [Cold] Damage\\\"\"],\"es\":[\"# # \\\"Agrega de {0} a {1} de daño de [Cold|hielo]\\\"\"]}}," +
            "{\"stats\":\"local_attribute_requirements_+%\",\"texts\":{\"en\":[\"1|# \\\"{0}% increased [Attributes|Attribute] Requirements\\\"\",\"#|-1 \\\"{0}% reduced [Attributes|Attribute] Requirements\\\" negate 1\"]," +
            "\"es\":[\"1|# \\\"Requisitos de [Attributes|atributos] aumentados un {0}%\\\"\",\"#|-1 \\\"Requisitos de [Attributes|atributos] reducidos un {0}%\\\" negate 1\"]}}," +
            "{\"stats\":\"base_life_regeneration_rate_per_minute\",\"texts\":{\"en\":[\"# \\\"{0} Life Regeneration per second\\\" per_minute_to_per_second 1\"],\"es\":[\"# \\\"Regeneras {0} de vida por segundo\\\" per_minute_to_per_second 1\"]," +
            "\"fr\":[\"# \\\"{0} de Régénération de Vie par seconde\\\" per_minute_to_per_second 1\"]}}," +
            "{\"stats\":\"stat_described_twice\",\"texts\":{\"en\":[\"# \\\"{0} once\\\"\"]}}," +
            "{\"stats\":\"stat_described_twice\",\"texts\":{\"en\":[\"# \\\"{0} again\\\"\"]}}," +
            "{\"stats\":\"named_passive\",\"texts\":{\"en\":[\"# \\\"Allocates {0}\\\" passive_hash 1\"]}}," +
            "{\"stats\":\"flat_armour\",\"texts\":{\"en\":[\"table_only # \\\"Listed {0}\\\"\",\"# \\\"{0:+d} to [Armour]\\\"\"],\"es\":[\"# \\\"{0:+d} a la [Armour|armadura]\\\"\"]}}," +
            "{\"stats\":\"base_fire_damage_resistance_%\",\"texts\":{\"en\":[\"# \\\"{0:+d}% to [Resistances|Fire Resistance]\\\"\"]}}," +
            "{\"stats\":\"life_leech_permyriad\",\"texts\":{\"en\":[\"# \\\"Leeches {0}% of Damage as Life\\\" divide_by_one_hundred 1\"]}}," +
            "{\"stats\":\"duration_ms\",\"texts\":{\"en\":[\"# \\\"Lasts {0} seconds\\\" milliseconds_to_seconds_2dp_if_required 1\",\"# \\\"never reached\\\"\"]}}," +
            "{\"stats\":\"tenths\",\"texts\":{\"en\":[\"# \\\"{0} metres\\\" divide_by_ten_1dp 1\"]}}," +
            "{\"stats\":\"waves\",\"texts\":{\"en\":[\"# \\\"{0} waves\\\" divide_by_twenty_then_double_0dp 1\"]}}," +
            "{\"stats\":\"only_english\",\"texts\":{\"en\":[\"2|# \\\"{0} to Spirit\\\"\"]}}]}";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheCatalogGivesAModifiersStatsInTheirOrder), TheCatalogGivesAModifiersStatsInTheirOrder),
            (nameof(AStatIsSummedAcrossModifiersThenWritten), AStatIsSummedAcrossModifiersThenWritten),
            (nameof(EachListIsWrittenApart), EachListIsWrittenApart),
            (nameof(AModifierThatIsNotKnownMakesItsLinesUncertain), AModifierThatIsNotKnownMakesItsLinesUncertain),
            (nameof(AStatWithoutOneLineIsUncertainOrSilent), AStatWithoutOneLineIsUncertainOrSilent),
            (nameof(ANumberWrittenAWayNeverSeenIsUncertain), ANumberWrittenAWayNeverSeenIsUncertain),
            (nameof(MemoryIsSetAgainstTheTooltipLineByLine), MemoryIsSetAgainstTheTooltipLineByLine),
            (nameof(TheComparedItemsReadAsTheirTooltips), TheComparedItemsReadAsTheirTooltips),
        };

        internal static ItemModLists Lists(IEnumerable<ItemMod>? @implicit = null, IEnumerable<ItemMod>? @explicit = null, IEnumerable<ItemMod>? enchant = null, IEnumerable<ItemMod>? other = null) =>
            new((@implicit ?? Array.Empty<ItemMod>()).ToList(), (@explicit ?? Array.Empty<ItemMod>()).ToList(), (enchant ?? Array.Empty<ItemMod>()).ToList(), (other ?? Array.Empty<ItemMod>()).ToList());

        internal static ItemMod Mod(string id, params int[] values) => new(id, values);

        private static (ModStatCatalog Stats, ModTextCatalog Texts) Catalogs() => (ModStatCatalog.Parse(Stats), ModTextCatalog.FromJson(Texts));

        /// <summary>The lines as "text" when certain and "?text" when not, one after another.</summary>
        private static string Written(ItemModLists mods, string language = "es")
        {
            var (stats, texts) = Catalogs();
            return string.Join(" | ", ItemModLines.Render(mods, language, stats, texts).Select(line => (line.Certain ? string.Empty : "?") + line.Text));
        }

        private static string Explicit(string language, params ItemMod[] mods) => Written(Lists(@explicit: mods), language);

        private static void TheCatalogGivesAModifiersStatsInTheirOrder()
        {
            var catalog = ModStatCatalog.Parse(Stats);
            Equal(new ModStat("base_maximum_life", 11, 19), catalog.StatsOf("LocalIncreasedArmourAndLife2")![1], "each stat with what it rolls");
            Equal(new ModStat("local_attribute_requirements_+%", -15, -15), catalog.StatsOf("ReducedRequirements1")![0], "below zero too");
            Equal("base_maximum_mana,stat_in_a_later_column", string.Join(',', catalog.UnprovenStatsOf("SixStats1") ?? Array.Empty<string>()), "a modifier whose values are not told apart keeps its stats");
            Equal(null, catalog.StatsOf("SixStats1"), "and gives no order for them");
            Equal(null, catalog.StatsOf("Nothing1"), "a modifier the files lack");
            Equal(null, catalog.UnprovenStatsOf("Nothing1"), "is not one of those either");
            Expect((true, false), (catalog.IsHidden("level"), catalog.IsHidden("base_maximum_life")), "the stats the description file hides");
            Equal(null, ModStatCatalog.Parse("{\"mods\":{\"Broken1\":\"base_maximum_life 30\"}}").StatsOf("Broken1"), "an entry that does not read gives nothing");
            Equal(0, ModStatCatalog.Parse("{}").Count, "an empty catalog knows none");
        }

        private static void AStatIsSummedAcrossModifiersThenWritten()
        {
            Equal("Armadura aumentada un 16% | +47 a la vida máxima", Explicit("es", Mod("IncreasedLife3", 32), Mod("LocalIncreasedArmourAndLife2", 16, 15)),
                "life from two modifiers is one line, as the tooltip adds it up");
            Equal("16% increased Armour | +47 to maximum Life", Explicit("en", Mod("IncreasedLife3", 32), Mod("LocalIncreasedArmourAndLife2", 16, 15)), "in another language");
            Equal("Agrega de 6 a 10 de daño de hielo", Explicit("es", Mod("AddedCold3", 6, 10)), "two values that are one line's two numbers");
            Equal("Requisitos de atributos reducidos un 15%", Explicit("es", Mod("ReducedRequirements1", -15)), "a negative value takes the line its condition fits, negated");
            Equal("Regeneras 1.7 de vida por segundo", Explicit("es", Mod("LifeRegeneration1", 104)), "stored a minute, shown a second: 104");
            Equal("Regeneras 5.9 de vida por segundo", Explicit("es", Mod("LifeRegeneration1", 352)), "and 352");
            Equal("Regeneras 3.3 de vida por segundo", Explicit("es", Mod("LifeRegeneration1", 201)), "201 is 3.35, a half: the tooltip showed 3.3, as single precision holds it (a unique ring, 2026-11-10)");
            Equal("?Regeneras 2.3 de vida por segundo", Explicit("es", Mod("LifeRegeneration1", 135)), "2.25 is a half in single precision too: not known");
            Equal("+10 al maná máximo | Regeneras 1.7 de vida por segundo", Explicit("es", Mod("LifeRegeneration1", 104), Mod("IncreasedMana1", 10)), "in the description file's order, whatever the list's");
            Equal(string.Empty, Written(Lists()), "an item without modifiers has no line, and that is certain");
        }

        private static void EachListIsWrittenApart()
        {
            Equal("+32 a la vida máxima | Armadura aumentada un 16% | +15 a la vida máxima",
                Written(Lists(@implicit: new[] { Mod("IncreasedLife3", 32) }, @explicit: new[] { Mod("LocalIncreasedArmourAndLife2", 16, 15) })),
                "the implicit list's lines, then the explicit one's: a stat is not summed across them");
            Equal("?+10 al maná máximo", Written(Lists(enchant: new[] { Mod("IncreasedMana1", 10) })), "the enchant list was never seen with a modifier: written, never certain");
            Equal("?+10 al maná máximo", Written(Lists(other: new[] { Mod("IncreasedMana1", 10) })), "nor the other lists");
        }

        private static void AModifierThatIsNotKnownMakesItsLinesUncertain()
        {
            Equal("? | ?+32 a la vida máxima", Explicit("es", Mod("Nothing1", 5), Mod("IncreasedLife3", 32)), "a modifier the files lack may add to any line of its list");
            Equal("+32 a la vida máxima | ?", Written(Lists(@implicit: new[] { Mod("IncreasedLife3", 32) }, @explicit: new[] { Mod("Nothing1", 5) })), "but not to another list's");
            Equal("? | +32 a la vida máxima | ?+10 al maná máximo", Explicit("es", Mod("SixStats1", 1, 2, 3), Mod("IncreasedLife3", 32), Mod("IncreasedMana1", 10)),
                "values not told apart: only the lines of that modifier's stats");
            Equal("? | ?+32 a la vida máxima", Explicit("es", Mod("LocalIncreasedArmourAndLife2", 16), Mod("IncreasedLife3", 32)), "fewer values than stats: none is given to a stat");
            Equal("?+40 a la vida máxima", Explicit("es", Mod("IncreasedLife3", 40)), "a value the modifier cannot roll may be another stat's");
            var (stats, texts) = Catalogs();
            Equal("Nothing1: not in the game files", ItemModLines.Render(Lists(@explicit: new[] { Mod("Nothing1", 5) }), "es", stats, texts)[0].From, "the log says why");
        }

        private static void AStatWithoutOneLineIsUncertainOrSilent()
        {
            Equal(string.Empty, Explicit("es", Mod("Level1", 1)), "a stat the file says has no description: no line, and nothing in doubt");
            Equal("?", Explicit("es", Mod("Undescribed1", 3)), "a stat the file does not mention: whether the tooltip shows a line is not known");
            Equal("?", Explicit("en", Mod("Twice1", 3)), "a stat in two blocks: which one is written is not known");
            Equal("?", Explicit("en", Mod("Passive1", 77)), "a line that shows a name looked up, not a number");
            Equal("?+600 to Armour", Explicit("en", Mod("Armour9", 600)), "a line after one that cannot be read: the first may have been the one");
            Equal("+600 a la armadura", Explicit("es", Mod("Armour9", 600)), "the same block in a language where every line reads");
            Equal("?", Explicit("fr", Mod("Leech1", 250)), "a language the block has no text in");
            Equal("?", Explicit("en", Mod("English1", 1)), "a value no line's condition fits");
        }

        private static void ANumberWrittenAWayNeverSeenIsUncertain()
        {
            Equal("?2 Life Regeneration per second", Explicit("en", Mod("LifeRegeneration1", 120)), "a whole number where a decimal was always seen: 2 or 2.0");
            Equal("?2.3 Life Regeneration per second", Explicit("en", Mod("LifeRegeneration1", 135)), "a half: rounded up or to even");
            Equal("?1.7 de Régénération de Vie par seconde", Explicit("fr", Mod("LifeRegeneration1", 104)), "a fraction in a language whose decimal mark was not seen");
            Equal("?+1500 a la armadura", Explicit("es", Mod("Armour9", 1500)), "from a thousand on the digits may be grouped");
            Equal("?-10% to Fire Resistance", Explicit("en", Mod("FireResist1", -10)), "a number shown below zero: which minus sign is not known");
            Equal("+10% to Fire Resistance", Explicit("en", Mod("FireResist1", 10)), "above zero it is certain");
            Equal("Leeches 2.5% of Damage as Life", Explicit("en", Mod("Leech1", 250)), "one decimal exactly where no handler states the decimals: seen written as it is (+1.4% on a spear, 2026-11-10)");
            Equal("Leeches 2.55% of Damage as Life", Explicit("en", Mod("Leech1", 255)), "two decimals, all a number of hundredths can have (+1.12% on a staff's tooltip)");
            Equal("Leeches 3% of Damage as Life", Explicit("en", Mod("Leech1", 300)), "a whole number under the same handler");
            Equal("Lasts 1.5 seconds", Explicit("en", Mod("Duration1", 1500)), "decimals as required: the last zero dropped");
            Equal("Lasts 0.75 seconds", Explicit("en", Mod("Duration1", 750)), "both when both are needed");
            Equal("Lasts 2 seconds", Explicit("en", Mod("Duration1", 2000)), "none for a whole number");
            Equal("2.5 metres", Explicit("en", Mod("Tenth1", 25)), "decimals always shown, the last not zero");
            Equal("?2 metres", Explicit("en", Mod("Tenth1", 20)), "always shown and zero: 2 or 2.0");
            Equal("?3 waves", Explicit("en", Mod("Wave1", 30)), "arithmetic whose rounding between its two steps is not settled");
        }

        private static void MemoryIsSetAgainstTheTooltipLineByLine()
        {
            var (stats, texts) = Catalogs();
            var memory = ItemModLines.Render(Lists(new[] { Mod("IncreasedMana1", 10), Mod("IncreasedLife3", 32) }, new[] { Mod("AddedCold3", 6, 10), Mod("Nothing1", 1) }), "es", stats, texts);
            var same = ItemModCheck.Compare(memory, new[] { "Guantes", "Armadura: 44", "+32 a la vida máxima. +10 al maná máximo", "Agrega de 6 a 10 de daño de hielo" }, texts);
            Expect((2, 0, 0, 2), (same.Equal, same.MemoryOnly.Count, same.TooltipOnly.Count, same.Uncertain.Count), "two lines the tooltip shows as one text are both there; the name and the armour are no modifier");
            Equal("2 lines equal, 0 differ, 2 uncertain", same.Counts, "as the log counts them");
            Equal(false, same.Differs, "lines in doubt are no difference");
            Equal("uncertain: (Nothing1: not in the game files) | \"Agrega de 6 a 10 de daño de hielo\" (minimum_added_cold_damage maximum_added_cold_damage) as the tooltip shows it", same.Details,
                "a line in doubt that reads as the tooltip's is said so: what would make it certain is seen");

            var other = ItemModCheck.Compare(memory, new[] { "+33 a la vida máxima", "+10 al maná máximo", "Regeneras 1.7 de vida por segundo" }, texts);
            Expect((1, "+32 a la vida máxima", "+33 a la vida máxima | Regeneras 1.7 de vida por segundo"), (other.Equal, string.Join(" | ", other.MemoryOnly), string.Join(" | ", other.TooltipOnly)),
                "a line memory alone holds, and the tooltip's modifier lines memory gave no line for");
            Expect((true, "1 lines equal, 3 differ, 2 uncertain"), (other.Differs, other.Counts), "counted as differing");
            Equal(true, other.Details.StartsWith("memory only: +32 a la vida máxima; tooltip only: +33 a la vida máxima | Regeneras 1.7 de vida por segundo; uncertain: ", StringComparison.Ordinal), "each side named");
        }

        /// <summary>
        ///     The items of the evidence file, as the dev command "itemmods" logs them: each one's modifiers as the host read
        ///     them ("Id 16/15", a value that is no number left out) and its tooltip's lines.
        /// </summary>
        private static IEnumerable<(string Item, ItemModLists Mods, string[] Tooltip)> EvidenceItems(string file)
        {
            var list = new Regex(@"(implicit|explicit|enchant|other)\[\d+\] (.*?)(?= \| (?:explicit|enchant|other)\[|$)");
            foreach (var line in File.ReadAllLines(file).Where(line => line.Contains(" rarity=", StringComparison.Ordinal)))
            {
                var parts = line.Split(" || tooltip: ");
                var lists = list.Matches(parts[0]).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(entry => entry.Split(' ')).Select(words => Mod(words[0], words[1].Split('/').Where(value => int.TryParse(value, out _)).Select(int.Parse).ToArray())).ToList());
                yield return (parts[0][..parts[0].IndexOf(' ')], Lists(lists.GetValueOrDefault("implicit"), lists.GetValueOrDefault("explicit"), lists.GetValueOrDefault("enchant"), lists.GetValueOrDefault("other")), parts[1].Split(" | "));
            }
        }

        private static string? EvidenceFile()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = Path.Combine(folder.FullName, "docs", "research", "evidence", Evidence);
                if (File.Exists(file))
                {
                    return file;
                }
            }

            return null;
        }

        /// <summary>
        ///     The seven items with modifiers set against their Spanish tooltips (0.5.5f, 2026-11-10), from the evidence file
        ///     and the shipped catalogs: every line made from memory is certain and is a line of the tooltip letter for letter,
        ///     in the tooltip's order, and there are as many as the tooltip has of modifiers.
        /// </summary>
        private static void TheComparedItemsReadAsTheirTooltips()
        {
            if (EvidenceFile() is not { } evidence || FileCatalogTests.Shipped("mod-stats.json") is not { } statFile || FileCatalogTests.Shipped("mod-texts.json") is not { } textFile)
            {
                return; // away from the repository
            }

            var (stats, texts) = (ModStatCatalog.Parse(File.ReadAllText(statFile)), ModTextCatalog.FromJson(File.ReadAllText(textFile)));
            var modifierLines = new Dictionary<string, int>
            {
                ["Metadata/Items/Weapons/OneHandWeapons/Sceptres/FourSceptre4"] = 5, ["Metadata/Items/Flasks/FourFlaskLife4"] = 2, ["Metadata/Items/Flasks/FourCharm6"] = 2,
                ["Metadata/Items/Flasks/FourFlaskMana3"] = 2, ["Metadata/Items/Armours/Boots/FourBootsStrDex1"] = 4, ["Metadata/Items/Belts/FourBelt1"] = 5,
                ["Metadata/Items/Armours/Gloves/FourGlovesStr2"] = 5,
            };
            var seen = new HashSet<string>();
            foreach (var (item, mods, tooltip) in EvidenceItems(evidence))
            {
                seen.Add(item);
                var lines = ItemModLines.Render(mods, "es", stats, texts);
                Equal(string.Empty, string.Join(" | ", lines.Where(line => !line.Certain).Select(line => $"{line.Text} ({line.From})")), $"{item}: no line in doubt");
                Equal(modifierLines[item], lines.Count, $"{item}: as many lines as its tooltip has of modifiers");
                var check = ItemModCheck.Compare(lines, tooltip, texts);
                Equal(string.Empty, string.Join(" | ", check.MemoryOnly), $"{item}: every line is one of its tooltip's");
                Equal(lines.Count, check.Equal, $"{item}: all of them");
                var places = lines.Select(line => Array.FindIndex(tooltip, shown => shown.Contains(line.Text, StringComparison.Ordinal))).ToList();
                Equal(true, places.SequenceEqual(places.Order()), $"{item}: in the tooltip's order");
            }

            Equal(modifierLines.Count, seen.Count, "the seven items with a Mods component");
            var gloves = EvidenceItems(evidence).First(item => item.Item.EndsWith("FourGlovesStr2", StringComparison.Ordinal));
            Equal(true, ItemModLines.Render(gloves.Mods, "es", stats, texts).Any(line => line.Text == "+47 a la vida máxima"), "the gloves' life is 32 and 15 from two modifiers, one line of 47");
            Equal("16% increased Armour | +47 to maximum Life | +33 to maximum Mana | 15% increased Critical Damage Bonus | Gain 2 Life per Enemy Hit with Attacks",
                string.Join(" | ", ItemModLines.Render(gloves.Mods, "en", stats, texts).Where(line => line.Certain).Select(line => line.Text)), "the same item in English");
        }
    }
}
