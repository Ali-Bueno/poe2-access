namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;
    using DatTableBuilder = DatColumnSearchTests.DatTableBuilder;

    /// <summary>The item bases and the modifier texts of the game's files (docs/research/file-catalogs.md), for the Build Planner's gear.</summary>
    public static class GearCatalogTests
    {
        private const string Bases =
            "{\"bases\":{" +
            "\"Metadata/Items/Armours/Helmets/FourHelmetStrDex5\":{\"class\":\"Helmet\",\"level\":33,\"armour\":97,\"evasion\":87,\"es\":0}," +
            "\"Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5\":{\"class\":\"Spear\",\"level\":21}," +
            "\"Metadata/Items/Armours/BodyArmours/FourBodyA\":{\"class\":\"Body Armour\",\"level\":45,\"armour\":0,\"evasion\":176,\"es\":57}," +
            "\"Metadata/Items/Armours/BodyArmours/FourBodyB\":{\"class\":\"Body Armour\",\"level\":51,\"armour\":0,\"evasion\":197,\"es\":63}," +
            "\"Metadata/Items/Armours/BodyArmours/FourBodyC\":{\"class\":\"Body Armour\",\"level\":10,\"armour\":50,\"evasion\":0,\"es\":0}," +
            "\"Metadata/Items/Armours/BodyArmours/FourBodyD\":{\"class\":\"Body Armour\",\"level\":10,\"armour\":50,\"evasion\":0,\"es\":0}," +
            "\"Metadata/Items/Rings/FourRing4\":{\"class\":\"Ring\",\"level\":12}," +
            "\"Metadata/Items/Amulets/FourRing4\":{\"class\":\"Amulet\",\"level\":12}}," +
            "\"names\":{\"en\":{\"Metadata/Items/Armours/Helmets/FourHelmetStrDex5\":\"Shielded Helm\",\"Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5\":\"War Spear\"," +
            "\"Metadata/Items/Armours/BodyArmours/FourBodyA\":\"Ascetic Garb\",\"Metadata/Items/Armours/BodyArmours/FourBodyB\":\"Ascetic Garb\"," +
            "\"Metadata/Items/Armours/BodyArmours/FourBodyC\":\"Fur Plate\",\"Metadata/Items/Armours/BodyArmours/FourBodyD\":\"Fur Plate\"," +
            "\"Metadata/Items/Rings/FourRing4\":\"Sapphire\",\"Metadata/Items/Amulets/FourRing4\":\"Sapphire\"}," +
            "\"es\":{\"Metadata/Items/Armours/Helmets/FourHelmetStrDex5\":\"Yelmo blindado\",\"Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5\":\"Lanza de guerra\"}," +
            "\"th\":{\"Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5\":\"<size:37>{Hok}\"}}}";

        private const string Texts =
            "{\"source\":\"test\",\"texts\":[" +
            "{\"stats\":\"base_fire_damage_resistance_%\",\"texts\":{\"en\":[\"# \\\"{0:+d}% to [Resistances|Fire Resistance]\\\"\"],\"es\":[\"# \\\"{0:+d}% a la resistencia al [Resistances|fuego]\\\"\"]}}," +
            "{\"stats\":\"local_minimum_added_physical_damage local_maximum_added_physical_damage\",\"texts\":{\"en\":[\"# # \\\"Adds {0} to {1} [Physical] Damage\\\"\"],\"es\":[\"# # \\\"Agrega de {0} a {1} de daño [Physical|físico]\\\"\"]}}," +
            "{\"stats\":\"global_minimum_added_physical_damage global_maximum_added_physical_damage\",\"texts\":{\"en\":[\"# # \\\"Adds {0} to {1} [Physical] Damage\\\"\"],\"es\":[\"# # \\\"Agrega de {0} a {1} de daño [Physical|físico]\\\"\"]}}," +
            "{\"stats\":\"base_maximum_life\",\"texts\":{\"en\":[\"# \\\"{0:+d} to maximum Life\\\"\"],\"es\":[\"# \\\"{0:+d} a la vida máxima\\\"\"]}}," +
            "{\"stats\":\"slow_potency_+%\",\"texts\":{\"en\":[\"1|# \\\"{0}% increased Slowing Potency\\\"\",\"#|-1 \\\"{0}% reduced Slowing Potency\\\" negate 1\"]," +
            "\"es\":[\"1|# \\\"Potencia de ralentización aumentada un {0}%\\\"\",\"#|-1 \\\"Potencia de ralentización reducida un {0}%\\\" negate 1\"]}}," +
            "{\"stats\":\"base_life_regeneration_rate_per_minute\",\"texts\":{\"en\":[\"# \\\"{0} Life Regeneration per second\\\" per_minute_to_per_second 1\"],\"es\":[\"# \\\"Regeneras {0} de vida por segundo\\\" per_minute_to_per_second 1\"]}}," +
            "{\"stats\":\"charges\",\"texts\":{\"en\":[\"1 \\\"Grants {0} Charge\\\"\",\"2|# \\\"Grants {0} Charges\\\"\"],\"es\":[\"1 \\\"Otorga {0} carga\\\"\",\"2|# \\\"Otorga {0} cargas\\\"\"]}}," +
            "{\"stats\":\"cannot_be_frozen\",\"texts\":{\"en\":[\"1|# \\\"Cannot be [Freeze|Frozen]\\\"\"],\"es\":[\"1|# \\\"No puedes ser [Freeze|congelado]\\\"\"]}}," +
            "{\"stats\":\"glory_for_seconds\",\"texts\":{\"en\":[\"# \\\"{0}% more for 6 seconds\\\"\"],\"es\":[\"# \\\"{0}% más durante 6 segundos\\\"\"]}}," +
            "{\"stats\":\"ambiguous_a\",\"texts\":{\"en\":[\"# \\\"{0}% faster\\\"\"],\"es\":[\"# \\\"{0}% igual\\\"\"]}}," +
            "{\"stats\":\"ambiguous_b\",\"texts\":{\"en\":[\"# \\\"{0}% quicker\\\"\"],\"es\":[\"# \\\"{0}% igual\\\"\"]}}," +
            "{\"stats\":\"named_passive\",\"texts\":{\"en\":[\"# \\\"Allocates {0}\\\" passive_hash 1\",\"table_only # \\\"Listed {0}\\\"\"]}}," +
            "{\"stats\":\"only_english\",\"texts\":{\"en\":[\"# \\\"{0} to Spirit\\\"\"]}}]}";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ItemBaseColumnsAreFoundByTheKnownBases), ItemBaseColumnsAreFoundByTheKnownBases),
            (nameof(ABaseIsFoundByItsNameInAnyLanguage), ABaseIsFoundByItsNameInAnyLanguage),
            (nameof(ANameOfDifferentBasesIsNoBase), ANameOfDifferentBasesIsNoBase),
            (nameof(AStatDescriptionFileIsReadByItsBlocks), AStatDescriptionFileIsReadByItsBlocks),
            (nameof(AModifierLineIsToldInAnyLanguage), AModifierLineIsToldInAnyLanguage),
            (nameof(ARangeShownOnAnItemGivesTheRolledNumber), ARangeShownOnAnItemGivesTheRolledNumber),
            (nameof(ConditionsAndHandlersAreAppliedAsTheFileDefinesThem), ConditionsAndHandlersAreAppliedAsTheFileDefinesThem),
            (nameof(ALineThatFitsNothingOrTwoLinesIsNotTold), ALineThatFitsNothingOrTwoLinesIsNotTold),
            (nameof(MissingCatalogsAnswerNothing), MissingCatalogsAnswerNothing),
            (nameof(TheShippedBasesHoldTheBuildsBases), TheShippedBasesHoldTheBuildsBases),
            (nameof(TheShippedTextsTellTheBuildsLines), TheShippedTextsTellTheBuildsLines),
        };

        /// <summary>The three tables with the four known bases; ArmourTypes' numbers moved by <paramref name="shift"/>.</summary>
        private static ItemBaseColumns.Tables Tables(int shift = 0, int helmetArmour = ItemBaseColumns.KnownHelmetArmour)
        {
            var classes = new DatTableBuilder(0x10);
            foreach (var id in new[] { "Helmet", ItemBaseColumns.KnownSpearClass, "Buckler", "Gloves" })
            {
                classes.Row(w => w.Text(0x0, id));
            }

            var items = new DatTableBuilder(0x38);
            var armours = new DatTableBuilder(0x28 + shift);
            var rows = new[]
            {
                (ItemBaseColumns.KnownHelmet, 0, ItemBaseColumns.KnownHelmetLevel, (int?)helmetArmour, 0, 0),
                (ItemBaseColumns.KnownSpear, 1, ItemBaseColumns.KnownSpearLevel, null, 0, 0),
                (ItemBaseColumns.KnownBuckler, 2, ItemBaseColumns.KnownBucklerLevel, 0, ItemBaseColumns.KnownBucklerEvasion, 0),
                (ItemBaseColumns.KnownGloves, 3, 16, 0, ItemBaseColumns.KnownGlovesEvasion, ItemBaseColumns.KnownGlovesEnergyShield),
            };
            for (var row = 0; row < rows.Length; row++)
            {
                var (id, itemClass, level, armour, evasion, energyShield) = rows[row];
                var item = row;
                items.Row(w => w.Text(0x0, id).WideKey(ItemBaseColumns.ClassOffset, itemClass).Int(ItemBaseColumns.LevelOffset, level));
                if (armour is { } value)
                {
                    armours.Row(w => w.WideKey(ItemBaseColumns.ArmourBaseOffset, item).Int(ItemBaseColumns.ArmourOffset + shift, value)
                        .Int(ItemBaseColumns.EvasionOffset + shift, evasion).Int(ItemBaseColumns.EnergyShieldOffset + shift, energyShield));
                }
            }

            return new ItemBaseColumns.Tables(items.Build(), classes.Build(), armours.Build());
        }

        private static void ItemBaseColumnsAreFoundByTheKnownBases()
        {
            var c = ItemBaseColumns.Find(Tables(), 0x20) ?? throw new Exception("not found");
            Expect((ItemBaseColumns.ClassOffset, ItemBaseColumns.LevelOffset), (c.ItemClass, c.Level), "BaseItemTypes");
            Expect((ItemBaseColumns.ArmourBaseOffset, ItemBaseColumns.ArmourOffset, ItemBaseColumns.EvasionOffset, ItemBaseColumns.EnergyShieldOffset), (c.ArmourBase, c.Armour, c.Evasion, c.EnergyShield), "ArmourTypes");
            const int Shift = 8;
            var moved = ItemBaseColumns.Find(Tables(Shift), 0x20) ?? throw new Exception("not found after a shift");
            Expect((ItemBaseColumns.ArmourOffset + Shift, ItemBaseColumns.EvasionOffset + Shift, ItemBaseColumns.EnergyShieldOffset + Shift), (moved.Armour, moved.Evasion, moved.EnergyShield),
                "each defence where the known bases hold theirs");
            Expect(true, ItemBaseColumns.Find(Tables(helmetArmour: 81), 0x20) == null, "a Soldier Greathelm that is not armour 80: the column is another, the step fails");
        }

        private static void ABaseIsFoundByItsNameInAnyLanguage()
        {
            var bases = ItemBaseCatalog.Parse(Bases);
            Equal(8, bases.Count, "every base");
            var helm = bases.ByName("Shielded Helm") ?? throw new Exception("no Shielded Helm");
            Equal("Metadata/Items/Armours/Helmets/FourHelmetStrDex5", helm.Id, "by its English name");
            Expect(("Helmet", 97, 87, 0, 33), (helm.ItemClass, helm.Armour, helm.Evasion, helm.EnergyShield, helm.RequiredLevel), "what the files say it is");
            Equal(helm, bases.ByName("  yelmo   BLINDADO "), "by its Spanish name, case and spacing ignored");
            Equal(helm, bases.ByName("ShieldedHelm"), "no spaces at all");
            Equal("Yelmo blindado", helm.Name("es"), "its name in a language");
            Equal("Shielded Helm", helm.Name("fr"), "English where the language has none");
            var spear = bases.ByName("War Spear") ?? throw new Exception("no War Spear");
            Expect(("Spear", 0, 0, 0, 21), (spear.ItemClass, spear.Armour, spear.Evasion, spear.EnergyShield, spear.RequiredLevel), "no armour piece: no defences");
            Equal(spear, bases.ByName("Hok"), "a name the files wrap in a size tag");
            Equal(spear, bases.ById("fourspear5"), "by the last part of its Id, case ignored");
            Equal(spear, bases.ById("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5"), "by its whole Id");
            Equal(null, bases.ById("FourRing4"), "a last part two bases share");
            Equal(null, bases.ByName("Wooden Club"), "an unknown name");
            Equal(null, bases.ById("Nothing"), "an unknown Id");
        }

        private static void ANameOfDifferentBasesIsNoBase()
        {
            var bases = ItemBaseCatalog.Parse(Bases);
            Equal(null, bases.ByName("Sapphire"), "bases of different classes");
            Equal(null, bases.ByName("Ascetic Garb"), "bases of one class the files give other defences: neither is the one meant");
            Equal("Metadata/Items/Armours/BodyArmours/FourBodyC", bases.ByName("Fur Plate")?.Id, "bases the files tell nothing apart of: the first in Id order");
        }

        private static void AStatDescriptionFileIsReadByItsBlocks()
        {
            var file = StatDescriptionFile.Parse(
                "include \"Data/StatDescriptions/other.csd\"\r\nno_description level\r\n\r\ndescription\r\n\t1 base_fire_damage_resistance_%\r\n\t1\r\n\t\t# \"{0:+d}% to [Resistances|Fire Resistance]\"\r\n" +
                "\tlang \"Spanish\"\t\r\n\t1\t\r\n\t\t# \"{0:+d}% a la resistencia al [Resistances|fuego]\"\r\n\r\ndescription\r\n\t2 min max\r\n\t2\r\n\t\t# 1|# \"Adds {0} to {1}\"\r\n\t\t# #|-1 \"Removes {0} to {1}\" negate 2\r\nstray words\r\n");
            Equal(2, file.Descriptions.Count, "two blocks");
            Equal("Data/StatDescriptions/other.csd", file.Includes.Single(), "the include, named");
            Equal("base_fire_damage_resistance_%", file.Descriptions[0].Stats.Single(), "the stat of the first");
            Equal("# \"{0:+d}% a la resistencia al [Resistances|fuego]\"", file.Descriptions[0].Texts["Spanish"].Single(), "a language's line as the file holds it, a tab after the language's name or not");
            Equal("min max", string.Join(' ', file.Descriptions[1].Stats), "two stats");
            Equal(2, file.Descriptions[1].Texts[StatDescriptionFile.English].Count, "its two English lines");
            Equal(1, file.Unplaced, "the one line that fits nowhere is counted");
        }

        private static void AModifierLineIsToldInAnyLanguage()
        {
            var texts = ModTextCatalog.FromJson(Texts);
            var english = texts.Parse("+16% to Fire Resistance") ?? throw new Exception("the English line is not told");
            var spanish = texts.Parse("+16% a la resistencia al fuego") ?? throw new Exception("the Spanish line is not told");
            Equal("{0:+d}% to Fire Resistance", english.Template, "a line's identity: its English wording, markup out");
            Equal(english.Template, spanish.Template, "the same line in both languages");
            Equal("16", string.Join(",", english.Numbers), "its number");
            Equal("16", string.Join(",", spanish.Numbers), "the same number");
            Equal("+16% a la resistencia al fuego", texts.Say(english, "es"), "said in Spanish as the game writes it");
            Equal("+16% to Fire Resistance", texts.Say(spanish, "en"), "and back");
            Equal("6,11", string.Join(",", texts.Parse("Adds 6 to 11 Physical Damage")!.Numbers), "two numbers; two blocks worded alike are one line");
            Equal("Agrega de 6 a 11 de daño físico", texts.Say(texts.Parse("adds 6  to 11 physical damage")!, "es"), "case and repeated spaces ignored");
            Equal("{0:+d}% to Fire Resistance", texts.Parse("<red>{+16% to [Resistances|Fire Resistance]}")?.Template, "the game's markup ignored");
            Equal("-5", string.Join(",", texts.Parse("-5% to Fire Resistance")!.Numbers), "a negative number");
            Equal("-5% a la resistencia al fuego", texts.Say(texts.Parse("-5% to Fire Resistance")!, "es"), "said with its sign");
            Equal(string.Empty, texts.Say(texts.Parse("+30 to Spirit")!, "es"), "a line with no text in the language");
            Equal(string.Empty, texts.Say(english, "xx"), "a language the files do not have");
            Equal(string.Empty, texts.Say(new ModLine("{0} to nothing", new double[] { 1 }), "es"), "a line the files do not have");
        }

        private static void ARangeShownOnAnItemGivesTheRolledNumber()
        {
            var texts = ModTextCatalog.FromJson(Texts);
            Equal("40", string.Join(",", texts.Parse("+40(30-40) a la vida máxima")!.Numbers), "the rolled number, its range dropped");
            Equal("{0:+d} to maximum Life", texts.Parse("+40(30-40) a la vida máxima")!.Template, "the life line");
            Equal("30", string.Join(",", texts.Parse("+(30-40) to maximum Life")!.Numbers), "a range alone: its lower end");
            Equal("7,15", string.Join(",", texts.Parse("Agrega de 7(5-8) a 15(12-16) de daño físico")!.Numbers), "two rolled numbers");
        }

        private static void ConditionsAndHandlersAreAppliedAsTheFileDefinesThem()
        {
            var texts = ModTextCatalog.FromJson(Texts);
            var reduced = texts.Parse("25% reduced Slowing Potency") ?? throw new Exception("reduced is not told");
            Equal("{0}% reduced Slowing Potency", reduced.Template, "the reduced line is its own");
            Equal("25", string.Join(",", reduced.Numbers), "with the number as written");
            Equal("Potencia de ralentización reducida un 25%", texts.Say(reduced, "es"), "said as the negative stat's line");
            Equal("Potencia de ralentización aumentada un 25%", texts.Say(texts.Parse("25% increased Slowing Potency")!, "es"), "the increased line");
            Equal("Regeneras 2.8 de vida por segundo", texts.Say(texts.Parse("2.8 Life Regeneration per second")!, "es"), "a stat a minute, shown a second");
            Equal("Otorga 1 carga", texts.Say(texts.Parse("Grants 1 Charge")!, "es"), "the singular line");
            Equal("Otorga 3 cargas", texts.Say(texts.Parse("Grants 3 Charges")!, "es"), "the plural line");
            Equal(null, texts.Parse("Grants 3 Charge"), "three is not the singular line's number");
            Equal("No puedes ser congelado", texts.Say(texts.Parse("Cannot be Frozen")!, "es"), "a line with no number");
            Equal("5", string.Join(",", texts.Parse("5% more for 6 seconds")!.Numbers), "a number the text writes out is no number of the line");
            Equal(null, texts.Parse("5% more for 7 seconds"), "and must be the text's");
        }

        private static void ALineThatFitsNothingOrTwoLinesIsNotTold()
        {
            var texts = ModTextCatalog.FromJson(Texts);
            Equal(null, texts.Parse("+16% to Wind Resistance"), "no such line");
            Equal(null, texts.Parse(string.Empty), "nothing");
            Equal(null, texts.Parse("5% igual"), "a Spanish line two different English lines share");
            Equal("{0}% faster", texts.Parse("5% faster")?.Template, "each of them is told in English");
            Equal(null, texts.Parse("Allocates 5"), "a line that shows a name for its number is not read");
            Equal(null, texts.Parse("Listed 5"), "nor one with a word before its text that is no condition");
        }

        private static void MissingCatalogsAnswerNothing()
        {
            var folder = Path.Join(Path.GetTempPath(), "poe2-gear-catalog-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var log = new PluginLog(folder, false);
                var broken = Path.Join(folder, "broken.json");
                File.WriteAllText(broken, "{\"bases\": [1, 2");
                ItemBaseCatalog.Load(log, Path.Join(folder, "no-such-file.json"));
                ItemBaseCatalog.Load(log, broken);
                ModTextCatalog.Load(log, Path.Join(folder, "no-such-file.json"));
                ModTextCatalog.Load(log, broken);
                Equal(null, ItemBaseCatalog.Parse("{}").ByName("War Spear"), "an empty catalog knows no base");
                Equal(null, ModTextCatalog.FromJson("{}").Parse("+16% to Fire Resistance"), "an empty catalog tells no line");
                Equal(string.Empty, ModTextCatalog.FromJson("{}").Say(new ModLine("{0:+d}% to Fire Resistance", new double[] { 16 }), "es"), "and says none");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>The bases the owner's build plans name, and what the files say the four armour pieces are.</summary>
        private static void TheShippedBasesHoldTheBuildsBases()
        {
            if (FileCatalogTests.Shipped("item-bases.json") is not { } file)
            {
                return; // away from the repository
            }

            var bases = ItemBaseCatalog.Parse(File.ReadAllText(file));
            foreach (var name in new[]
            {
                "War Spear", "Ridged Buckler", "Shielded Helm", "Explorer Armour", "Doubled Gauntlets", "Stacked Sabatons", "Bloodstone Amulet",
                "Plate Belt", "Sapphire Ring", "Topaz Ring", "Colossal Life Flask", "Gargantuan Mana Flask", "Stone Charm", "Thawing Charm",
            })
            {
                Expect(true, bases.ByName(name) != null, name);
            }

            (string, int, int, int) Defences(string name) => bases.ByName(name) is { } item ? (item.ItemClass, item.Armour, item.Evasion, item.EnergyShield) : default;
            Equal(("Helmet", 97, 87, 0), Defences("Shielded Helm"), "Shielded Helm");
            Equal(("Body Armour", 149, 132, 0), Defences("Explorer Armour"), "Explorer Armour");
            Equal(("Gloves", 49, 43, 0), Defences("Doubled Gauntlets"), "Doubled Gauntlets");
            Equal(("Boots", 73, 65, 0), Defences("Stacked Sabatons"), "Stacked Sabatons");
            Equal(string.Empty, string.Join(" | ", BuildBases.Where(name => bases.ByName(name) == null)), "every base of the build plans");
            Equal(bases.ByName("War Spear"), bases.ByName("Lanza de guerra"), "the Spanish name of the War Spear, as the game's tooltip shows it");
            Equal(bases.ByName("Ridged Buckler"), bases.ByName("Broquel corrugado"), "and of the Ridged Buckler");
            Equal(22, bases.ByName("Ridged Buckler")!.RequiredLevel, "the level its tooltip asks for");
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>English lines of the build plans, each with the Spanish line the game's tooltips showed for it (the play logs, 2026-11-10).</summary>
        private static void TheShippedTextsTellTheBuildsLines()
        {
            if (FileCatalogTests.Shipped("mod-texts.json") is not { } file)
            {
                return; // away from the repository
            }

            var texts = ModTextCatalog.FromJson(File.ReadAllText(file));
            foreach (var (english, spanish) in new[]
            {
                ("+16% to Fire Resistance", "+16% a la resistencia al fuego"),
                ("+40 to maximum Life", "+40 a la vida máxima"),
                ("Adds 6 to 11 Physical Damage", "Agrega de 6 a 11 de daño físico"),
                ("Adds 1 to 16 Lightning damage to Attacks", "Agrega de 1 a 16 de daño de rayo a los ataques"),
                ("65% increased Physical Damage", "Daño físico aumentado un 65%"),
                ("11% increased Attack Speed", "Velocidad de ataque aumentada un 11%"),
                ("20% increased Movement Speed", "Velocidad de movimiento aumentada un 20%"),
                ("+64 to Stun Threshold", "+64 al umbral de aturdimiento"),
                ("+21 to Dexterity", "+21 a la destreza"),
                ("33% increased Evasion Rating", "Evasión aumentada un 33%"),
                ("31% increased Rarity of Items found", "Rareza de los objetos encontrados aumentada un 31%"),
            })
            {
                var line = texts.Parse(english);
                Expect(true, line != null, english);
                Equal(spanish, texts.Say(line!, "es"), english);
                Equal(line!.Template, texts.Parse(spanish)?.Template, spanish);
                Equal(string.Join(",", line.Numbers), string.Join(",", texts.Parse(spanish)!.Numbers), spanish + ": the same numbers");
            }

            Equal("23", string.Join(",", texts.Parse("+23(20-30) al maná máximo")!.Numbers), "a tooltip's rolled number with its range");
            Equal("62", string.Join(",", texts.Parse("Daño físico aumentado un 62(50-64)%")!.Numbers), "a range before the percent sign");
            Equal("11", string.Join(",", texts.Parse("+(11-15)% a la resistencia al fuego")!.Numbers), "a range alone");
            Equal("Regeneras 2.8 de vida por segundo", texts.Say(texts.Parse("Regeneras 2.8 de vida por segundo")!, "es"), "a number shown a second, kept a minute");

            Equal(string.Empty, string.Join(" | ", BuildLines.Where(line => texts.Parse(line) == null)), "every modifier line of the build plans is told");
            Equal(string.Empty, string.Join(" | ", BuildLines.Where(line => texts.Say(texts.Parse(line)!, "es").Length == 0)), "and said in Spanish");
            Equal(string.Empty, string.Join(" | ", BuildLines.Where(line => !Same(texts.Say(texts.Parse(line)!, "en"), line))), "and said in English as it was written");
        }

        /// <summary>Every base the owner's build plans name (BuildPlanner/*.build, inventory_slots[].additional_text, first line; 2026-11-10).</summary>
        private static readonly string[] BuildBases =
        {
            "Ancestral Tiara", "Bloodstone Amulet", "Colossal Life Flask", "Crimson Amulet", "Daggerfoot Shoes", "Doubled Gauntlets", "Explorer Armour", "Flying Spear",
            "Fur Plate", "Furtive Wraps", "Gargantuan Mana Flask", "Gold Amulet", "Heavy Belt", "Iron Greaves", "Iron Ring", "Ironhead Spear", "Itinerant Jacket",
            "Medium Life Flask", "Medium Mana Flask", "Pariah Mask", "Plate Belt", "Prismatic Ring", "Rawhide Belt", "Ridged Buckler", "Riveted Mitts", "Sapphire Charm",
            "Sapphire Ring", "Secured Wraps", "Shielded Helm", "Sleek Jacket", "Soaring Spear", "Soldier Greathelm", "Stacked Sabatons", "Stone Charm", "Thawing Charm",
            "Topaz Ring", "Ultimate Life Flask", "Ultimate Mana Flask", "Unset Ring", "War Spear", "Wooden Buckler",
        };

        /// <summary>Every modifier line of those plans, its numbering off.</summary>
        private static readonly string[] BuildLines =
        {
            "+10 to maximum Life", "+100 to maximum Life", "+120 to Evasion Rating", "+13% to Cold and Chaos Resistances", "+13% to Fire and Chaos Resistances",
            "+13% to Lightning and Chaos Resistances", "+13% to all Elemental Resistances", "+15% to all Elemental Resistances", "+16 to maximum Energy Shield",
            "+16% to Chaos Resistance", "+16% to Cold Resistance", "+16% to Fire Resistance", "+16% to Lightning Resistance", "+20 to maximum Life", "+21 to Dexterity",
            "+22 to maximum Energy Shield", "+25 to maximum Energy Shield", "+26% to Cold Resistance", "+26% to Fire Resistance", "+26% to Lightning Resistance",
            "+28 to Dexterity", "+28 to Intelligence", "+3 to Level of all Projectile Skills", "+3% to Quality of all Skills", "+30 to maximum Life",
            "+327 to Accuracy Rating", "+36% to Cold Resistance", "+36% to Fire Resistance", "+36% to Lightning Resistance", "+37 to maximum Energy Shield",
            "+4 to Level of all Projectile Skills", "+4.4% to Critical Hit Chance", "+40 to maximum Life", "+41% to Cold Resistance", "+41% to Fire Resistance",
            "+41% to Lightning Resistance", "+42 to Evasion Rating", "+47 to Spirit", "+52 to maximum Energy Shield", "+54 to Spirit", "+6% to Cold Resistance",
            "+6% to Fire Resistance", "+6% to Lightning Resistance", "+64 to Stun Threshold", "+65 to Evasion Rating", "+71 to maximum Energy Shield",
            "+8 to all Attributes", "+80 to Evasion Rating", "10% increased Area of Effect for Attacks", "10% increased Movement Speed", "11% increased Attack Speed",
            "14% increased Attack Speed", "14% increased Cast Speed", "15% increased Rarity of Items found", "170% increased Physical Damage",
            "20% chance to gain Onslaught on Killing Hits with this Weapon", "20% increased Global Armour, Evasion and Energy Shield", "20% increased Movement Speed",
            "20% increased effect of Socketed Augment Items", "24% of Recovery applied Instantly", "25% increased Explicit Speed Modifier magnitudes",
            "25% reduced Slowing Potency of Debuffs on You", "30% increased Movement Speed", "30% increased Skill Effect Duration", "31% increased Attack Speed",
            "31% increased Rarity of Items found", "33% increased Evasion Rating", "40% increased Physical Damage", "5% increased Attack Speed",
            "5% increased Spirit Reservation Efficiency of Skills", "55% increased Charges", "65% increased Physical Damage", "68% increased Evasion and Energy Shield",
            "8% increased Attack Speed", "80% increased Energy Shield", "80% increased Evasion and Energy Shield", "92% increased Energy Shield",
            "92% increased Evasion and Energy Shield", "Adds 1 to 16 Lightning damage to Attacks", "Adds 1 to 202 Lightning Damage",
            "Adds 1 to 33 Lightning damage to Attacks", "Adds 1 to 4 Lightning damage to Attacks", "Adds 1 to 48 Lightning damage to Attacks",
            "Adds 1 to 60 Lightning damage to Attacks", "Adds 10 to 18 Physical Damage to Attacks", "Adds 11 to 18 Cold damage to Attacks",
            "Adds 11 to 21 Fire damage to Attacks", "Adds 16 to 25 Cold damage to Attacks", "Adds 21 to 32 Cold damage to Attacks",
            "Adds 25 to 37 Fire damage to Attacks", "Adds 26 to 44 Physical Damage", "Adds 6 to 10 Fire damage to Attacks", "Adds 6 to 11 Physical Damage",
            "Adds 7 to 12 Cold damage to Attacks", "Gain Deflection Rating equal to 18% of Evasion Rating",
            "Skills have 11% chance to not remove Charges but still count as consuming them",
            "Skills have 7% chance to not remove Charges but still count as consuming them",
        };
    }
}
