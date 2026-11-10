namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Services;

    /// <summary>The Build Planner's gear: the file's pieces, the one for a level, and an item set against it in the game's language.</summary>
    public static class BuildGearTests
    {
        // Entries of "Early Campaign - [0.5.5] Twister Gemling.build" as exported (yarkey's build, 2026-11-10).
        private const string Early =
            "{\"name\":\"Early\",\"passives\":[\"projectiles18\"],\"inventory_slots\":[" +
            "{\"additional_text\": \"War Spear\\n1. 11% increased Attack Speed\\n2. 65% increased Physical Damage\\n3. Adds 6 to 11 Physical Damage\", \"inventory_id\": \"Weapon1\", \"level_interval\": [21, 100], \"slot_x\": 0, \"slot_y\": 0}," +
            "{\"additional_text\": \"Shielded Helm\\n1. +40 to maximum Life\\n2. +16% to Fire Resistance\\n3. +16% to Lightning Resistance\", \"inventory_id\": \"Helm1\", \"level_interval\": [33, 100], \"slot_x\": 0, \"slot_y\": 0}," +
            "{\"additional_text\": \"Sapphire Ring\\n1. +16% to Fire Resistance\\n2. Adds 6 to 10 Fire damage to Attacks\", \"inventory_id\": \"Ring1\", \"level_interval\": [12, 100], \"slot_x\": 0, \"slot_y\": 0}," +
            "{\"additional_text\": \"Colossal Life Flask\\n1. 24% of Recovery applied Instantly\", \"inventory_id\": \"Flask1\", \"level_interval\": [30, 100], \"slot_x\": 0, \"slot_y\": 0}," +
            "{\"additional_text\": \"Gargantuan Mana Flask\\n1. 24% of Recovery applied Instantly\", \"inventory_id\": \"Flask1\", \"level_interval\": [40, 100], \"slot_x\": 1, \"slot_y\": 0}," +
            "{\"additional_text\": \"Stone Charm\", \"inventory_id\": \"Charm1\", \"level_interval\": [8, 100], \"slot_x\": 0, \"slot_y\": 0}]}";

        // Entries of "Late Endgame - [0.5.5] Twister Gemling L.build": uniques carry a name, a note in brackets and no levels.
        private const string Late =
            "{\"name\":\"Late\",\"passives\":[\"projectiles18\"],\"inventory_slots\":[" +
            "{\"inventory_id\": \"BodyArmour1\", \"slot_x\": 0, \"slot_y\": 0, \"unique_name\": \"Morior Invictus (global defences)\"}," +
            "{\"additional_text\": \"Ancestral Tiara\\n1. 31% increased Rarity of Items found\\n2. +41% to Fire Resistance\\n3. +41% to Cold Resistance\\n4. +71 to maximum Energy Shield\\n5. 92% increased Energy Shield\", \"inventory_id\": \"Helm1\", \"level_interval\": [80, 100], \"slot_x\": 0, \"slot_y\": 0}," +
            "{\"inventory_id\": \"Belt1\", \"slot_x\": 0, \"slot_y\": 0, \"unique_name\": \"Headhunter\"}]}";

        // Made for the test: one slot with two pieces over the levels, which one file of the seven does not hold.
        private const string Staged =
            "{\"passives\":[],\"inventory_slots\":[" +
            "{\"additional_text\":\"Iron Greaves\",\"inventory_id\":\"Boots1\",\"level_interval\":[1,32],\"slot_x\":0}," +
            "{\"additional_text\":\"Stacked Sabatons\",\"inventory_id\":\"Boots1\",\"level_interval\":[33,100],\"slot_x\":0}]}";

        // The worn helmet's tooltip as the mod reads it (play log 2026-11-10 07:32:48).
        private static readonly string[] WornHelm =
        {
            "Halo de cólera", "Gran yelmo de soldado", "Casco", "Armadura: 88", "Requiere: Nivel 12, 19 Fue", "Armadura aumentada un 10%",
            "+29 a la vida máxima", "+7 al maná máximo", "+10 a la inteligencia", "+9% a la resistencia al fuego", "Equipado",
        };

        private static readonly GearItem Worn = new("Metadata/Items/Armours/Helmets/FourHelmetStr3", "Gran yelmo de soldado", "Halo de cólera", false, WornHelm);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheFilesGearIsRead), TheFilesGearIsRead),
            (nameof(ThePieceForALevelIsTheLatestThatApplies), ThePieceForALevelIsTheLatestThatApplies),
            (nameof(AnItemGoesInTheSlotsOfItsFolder), AnItemGoesInTheSlotsOfItsFolder),
            (nameof(AWantedLineIsFoundInTheGamesLanguage), AWantedLineIsFoundInTheGamesLanguage),
            (nameof(TwoNumbersCompareByTheFirst), TwoNumbersCompareByTheFirst),
            (nameof(TheHelmetSlotSaysWhatTheBuildWants), TheHelmetSlotSaysWhatTheBuildWants),
            (nameof(AnEmptySlotAndALaterPieceSayWhatIsAsked), AnEmptySlotAndALaterPieceSayWhatIsAsked),
            (nameof(AShopItemIsCountedAgainstTheWornOne), AShopItemIsCountedAgainstTheWornOne),
            (nameof(WithoutCatalogsNothingIsClaimed), WithoutCatalogsNothingIsClaimed),
            (nameof(AUniqueIsToldByItsOwnName), AUniqueIsToldByItsOwnName),
            (nameof(TheSummaryCountsBasesAndTheModifiersRead), TheSummaryCountsBasesAndTheModifiersRead),
        };

        private static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        /// <summary>Runs with the game in Spanish, as the owner plays it.</summary>
        private static void InSpanish(Action test)
        {
            Localization.Set("es", new());
            try
            {
                test();
            }
            finally
            {
                Localization.Set("en", new());
            }
        }

        private static BuildGearEntry Piece(string file, string slot, int level, int x = 0) => BuildGear.EntryFor(BuildPlan.Parse(file)!, slot, x, level)!;

        private static GearFit Fit(BuildGearEntry entry, GearItem? item) => BuildGear.Fit(entry, item, new Bases(), new Texts(), "es");

        private static void TheFilesGearIsRead()
        {
            var gear = BuildPlan.Parse(Early)!.Gear;
            Expect(6, gear.Count, "pieces");
            Expect("War Spear", gear[0].Base, "base");
            Expect("11% increased Attack Speed|65% increased Physical Damage|Adds 6 to 11 Physical Damage", string.Join('|', gear[0].Wanted), "wanted lines without their numbering");
            Expect((21, 100), (gear[0].FromLevel, gear[0].ToLevel), "levels");
            Expect(("Flask1", 1, "Gargantuan Mana Flask"), (gear[4].Slot, gear[4].SlotX, gear[4].Base), "the mana flask's column");
            Expect(0, gear[5].Wanted.Count, "a charm without modifiers");

            var late = BuildPlan.Parse(Late)!.Gear;
            Expect(("Morior Invictus (global defences)", "Morior Invictus", true), (late[0].Unique, late[0].UniqueName, late[0].IsUnique), "a unique with the author's note");
            Expect((1, 100), (late[0].FromLevel, late[0].ToLevel), "a unique gives no levels");
            Expect(5, late[1].Wanted.Count, "five wanted lines");
            Expect(0, BuildPlan.Parse("{\"passives\":[]}")!.Gear.Count, "a file without gear");
        }

        private static void ThePieceForALevelIsTheLatestThatApplies()
        {
            var plan = BuildPlan.Parse(Staged)!;
            Expect("Iron Greaves", BuildGear.EntryFor(plan, "Boots1", 0, 20)!.Base, "the piece of its levels");
            Expect("Stacked Sabatons", BuildGear.EntryFor(plan, "Boots1", 0, 33)!.Base, "the next from its first level");
            Expect("Shielded Helm", Piece(Early, "Helm1", 12).Base, "a piece still to come is the slot's");
            Expect(true, BuildGear.EntryFor(plan, "Helm1", 0, 20) == null, "no piece for the slot");
            Expect("Colossal Life Flask", Piece(Early, "Flask1", 40).Base, "life flask at column 0");
            Expect("Gargantuan Mana Flask", Piece(Early, "Flask1", 40, 1).Base, "mana flask at column 1");
        }

        private static void AnItemGoesInTheSlotsOfItsFolder()
        {
            string Slots(string id) => string.Join(' ', BuildGear.SlotsFor(id).Select(slot => $"{slot.Slot}:{slot.X}"));
            Expect("Helm1:0", Slots("Metadata/Items/Armours/Helmets/FourHelmetStrInt2"), "helmet");
            Expect("Ring1:0 Ring2:0", Slots("Metadata/Items/Rings/FourRing3"), "ring");
            Expect("Weapon1:0 Weapon2:0", Slots("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear4"), "spear");
            Expect("Offhand1:0 Offhand2:0", Slots("Metadata/Items/Armours/Shields/FourShieldDex2"), "buckler");
            Expect("Flask1:0", Slots("Metadata/Items/Flasks/FourFlaskLife3"), "life flask");
            Expect("Flask1:1", Slots("Metadata/Items/Flasks/FourFlaskMana3"), "mana flask");
            Expect(string.Empty, Slots("Metadata/Items/Flasks/FourCharm2"), "a charm's slot is not told");
            Expect(string.Empty, Slots("Metadata/Items/Currency/CurrencyIdentification"), "no gear");
        }

        private static void AWantedLineIsFoundInTheGamesLanguage()
        {
            var fit = Fit(Piece(Early, "Helm1", 40), Worn);
            Expect(BaseFit.Other, fit.Base, "another helmet base");
            Expect("Below Below Missing", string.Join(' ', fit.Mods.Select(mod => mod.Fit)), "life and fire with less, no lightning");
            Expect("+29 a la vida máxima", fit.Mods[0].Held, "the item's own line");
            Expect(2, fit.Has, "lines had");
            Expect(2, fit.Shortfall, "another base and one line missing");

            var better = Worn with { BaseId = "Metadata/Items/Armours/Helmets/FourHelmetStr5", BaseName = "Yelmo blindado", Lines = new[] { "+52 a la vida máxima", "+16% a la resistencia al rayo" } };
            fit = Fit(Piece(Early, "Helm1", 40), better);
            Expect(BaseFit.Same, fit.Base, "the base by its id");
            Expect("Met Missing Met", string.Join(' ', fit.Mods.Select(mod => mod.Fit)), "more life than asked, the same lightning");
            Expect(BaseFit.Same, Fit(Piece(Early, "Helm1", 40), better with { BaseId = string.Empty }).Base, "the base by its name in the game's language");
            Expect("Unknown Unknown Unknown", string.Join(' ', Fit(Piece(Early, "Helm1", 40), better with { Lines = null }).Mods.Select(mod => mod.Fit)), "no tooltip read, nothing compared");
        }

        private static void TwoNumbersCompareByTheFirst() => InSpanish(() =>
        {
            var spear = new GearItem("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear4", "Lanza de guerra", "Lanza de guerra dentada", false,
                new[] { "Lanza de guerra dentada", "Agrega de 4 a 9 de daño físico", "Velocidad de ataque aumentada un 11%" });
            var fit = Fit(Piece(Early, "Weapon1", 30), spear);
            Expect("Met Unknown Below", string.Join(' ', fit.Mods.Select(mod => mod.Fit)), "attack speed met, a line the texts lack, less damage");
            Expect("Velocidad de ataque aumentada un 11%, cumple|Sin comparar: 65% increased Physical Damage|Agrega de 4 a 9 de daño físico, la build pide 6 a 11",
                string.Join('|', Announcements.GearMods(fit, new Texts())), "said");
            fit = Fit(Piece(Early, "Weapon1", 30), spear with { Lines = new[] { "Agrega de 7 a 9 de daño físico" } });
            Expect(ModFit.Met, fit.Mods[2].Fit, "a higher first number meets it");
        });

        private static void TheHelmetSlotSaysWhatTheBuildWants() => InSpanish(() =>
        {
            var lines = Announcements.BuildGearSlot(Fit(Piece(Early, "Helm1", 40), Worn), 40, new Bases(), new Texts()).ToList();
            Expect("Build: Yelmo blindado, armadura, otra base|+29 a la vida máxima, la build pide 40|+9% a la resistencia al fuego, la build pide 16|Falta +16% a la resistencia al rayo",
                string.Join('|', lines), "the helmet slot");

            var evasive = Worn with { BaseId = "Metadata/Items/Armours/Helmets/FourHelmetDex2" };
            Expect("Build: Yelmo blindado, armadura, desde el nivel 33, otra base, la tuya da evasión",
                Announcements.BuildGearSlot(Fit(Piece(Early, "Helm1", 20), evasive), 20, new Bases(), new Texts()).First(), "a piece to come against an evasion helmet");
        });

        private static void AnEmptySlotAndALaterPieceSayWhatIsAsked() => InSpanish(() =>
        {
            var lines = Announcements.BuildGearSlot(Fit(Piece(Early, "Helm1", 40), null), 40, new Bases(), new Texts()).ToList();
            Expect("Build: Yelmo blindado, armadura|Pide: +40 a la vida máxima; +16% a la resistencia al fuego; +16% a la resistencia al rayo", string.Join('|', lines), "an empty slot");
            Expect("Build: Stone Charm", Announcements.BuildGearSlot(Fit(Piece(Early, "Charm1", 40), null), 40, new Bases(), new Texts()).Single(), "a base the files lack, no modifiers");
        });

        private static void AShopItemIsCountedAgainstTheWornOne() => InSpanish(() =>
        {
            var entry = Piece(Early, "Helm1", 40);
            var shop = new GearItem("Metadata/Items/Armours/Helmets/FourHelmetStr5", "Yelmo blindado", "Yelmo blindado del zorro", false,
                new[] { "Yelmo blindado del zorro", "+41 a la vida máxima", "+12% a la resistencia al fuego", "+18% a la resistencia al rayo" });
            Expect("Build, Casco: 3 de 3 modificadores, misma base, más que el puesto, que tiene 2", Announcements.BuildGearItem(Fit(entry, shop), Fit(entry, Worn), 40), "more than the worn one");
            Expect("Build, Casco: 2 de 3 modificadores, otra base, igual que el puesto", Announcements.BuildGearItem(Fit(entry, Worn), Fit(entry, Worn), 40), "the same count");
            Expect("Build, Casco: 3 de 3 modificadores, misma base, nada puesto", Announcements.BuildGearItem(Fit(entry, shop), null, 40), "nothing worn");
            Expect("Build, Casco: 3 de 3 modificadores, misma base", Announcements.BuildGearItem(Fit(entry, shop), Fit(entry, Worn with { Lines = null }), 40), "the worn one's lines not read: no comparison");
            Expect("Build, Casco: 3 de 3 modificadores, misma base, desde el nivel 33, igual que el puesto",
                Announcements.BuildGearItem(Fit(entry, shop), Fit(entry, shop), 20), "a piece to come names its level");
            Expect("Build, Casco: 2 de 3 modificadores, otra base, menos que el puesto, que tiene 3",
                Announcements.BuildGearItem(Fit(entry, Worn), Fit(entry, shop), 40), "fewer than the worn one");
            Expect("Build, Frasco de maná: otra base, nada puesto",
                Announcements.BuildGearItem(Fit(Piece(Early, "Flask1", 40, 1), new GearItem("Metadata/Items/Flasks/FourFlaskMana2", "Frasco de maná mediano", "Frasco de maná mediano", false, null)), null, 40),
                "a flask by what it holds, its lines not read");
            var spear = new GearItem("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear4", "Lanza de guerra", "Lanza de guerra", false, new[] { "Velocidad de ataque aumentada un 5%" });
            Expect("Build, Mano principal: 1 de 2 modificadores, 1 sin comparar, misma base, nada puesto",
                Announcements.BuildGearItem(Fit(Piece(Early, "Weapon1", 40), spear), null, 40), "a line the texts lack is left out of the count");
        });

        private static void WithoutCatalogsNothingIsClaimed() => InSpanish(() =>
        {
            var entry = Piece(Early, "Helm1", 40);
            var fit = BuildGear.Fit(entry, Worn, BuildGear.Bases, BuildGear.Texts, "es");
            Expect(BaseFit.Unknown, fit.Base, "a base the files do not name");
            Expect("Unknown Unknown Unknown", string.Join(' ', fit.Mods.Select(mod => mod.Fit)), "no line is met or missing");
            Expect("Build: Shielded Helm|Sin comparar: +40 to maximum Life|Sin comparar: +16% to Fire Resistance|Sin comparar: +16% to Lightning Resistance",
                string.Join('|', Announcements.BuildGearSlot(fit, 40, BuildGear.Bases, BuildGear.Texts)), "the author's own words");
            Expect(string.Empty, Announcements.BuildGearItem(fit, null, 40), "nothing is said of an item nothing could be compared on");

            // The game in the author's language: the same words around another number are the same line.
            var english = new GearItem(string.Empty, "Shielded Helm", "Shielded Helm of the Fox", false, new[] { "+21 to maximum Life", "+16% to Fire Resistance" });
            fit = BuildGear.Fit(entry, english, BuildGear.Bases, BuildGear.Texts, "en");
            Expect(BaseFit.Same, fit.Base, "the base by the author's name");
            Expect("Below Met Unknown", string.Join(' ', fit.Mods.Select(mod => mod.Fit)), "found by its words; what is not found is not called missing");
        });

        private static void AUniqueIsToldByItsOwnName() => InSpanish(() =>
        {
            var belt = Piece(Late, "Belt1", 70);
            var rare = new GearItem("Metadata/Items/Belts/FourBelt3", "Cinturón de placas", "Lazo de pesar", false, new[] { "Lazo de pesar" });
            Expect("Build: único Headhunter, no es este", Announcements.BuildGearSlot(Fit(belt, rare), 70, new Bases(), new Texts()).Single(), "a rare is not the unique");
            Expect("Build: único Headhunter, es este", Announcements.BuildGearSlot(Fit(belt, rare with { IsUnique = true, Title = "Headhunter" }), 70, new Bases(), new Texts()).Single(), "by its name");
            Expect("Build: único Headhunter", Announcements.BuildGearSlot(Fit(belt, rare with { IsUnique = true, Title = "Cazacabezas" }), 70, new Bases(), new Texts()).Single(),
                "a unique named in another language is not judged");
            Expect("Build, Cinturón: único Headhunter, no es este", Announcements.BuildGearItem(Fit(belt, rare), Fit(belt, rare), 70), "on an item");
            Expect(string.Empty, Announcements.BuildGearItem(Fit(belt, rare with { IsUnique = true, Title = "Cazacabezas" }), null, 70), "nothing said of a unique not judged");
        });

        private static void TheSummaryCountsBasesAndTheModifiersRead() => InSpanish(() =>
        {
            var plan = BuildPlan.Parse(Early)!;
            var ring = new GearItem("Metadata/Items/Rings/FourRing2", "Anillo de zafiro", "Anillo de zafiro", false, null);
            var fits = new[]
            {
                Fit(BuildGear.EntryFor(plan, "Weapon1", 0, 40)!, null),
                Fit(BuildGear.EntryFor(plan, "Helm1", 0, 40)!, Worn),
                Fit(BuildGear.EntryFor(plan, "Ring1", 0, 40)!, ring),
            };
            var summary = BuildGear.Summary(fits);
            Expect((3, 1, 1, 2, 3), (summary.Slots, summary.OnBase, summary.PiecesRead, summary.Has, summary.Wanted), "slots, on base, pieces read, lines had, lines wanted");
            Expect("Build: 1 de 3 piezas con su base, 2 de 3 modificadores, leídos en 1 de 3 piezas|Más lejos de la build: Mano principal",
                string.Join('|', Announcements.BuildGearSummary(summary)), "said on entering the inventory");
            Expect("Build: 1 de 1 piezas con su base", string.Join('|', Announcements.BuildGearSummary(BuildGear.Summary(new[] { fits[2] }))), "bases alone while no tooltip was read");
        });

        /// <summary>The bases the tests name, as the game's files would give them.</summary>
        private sealed class Bases : IItemBases
        {
            private static readonly ItemBase[] All =
            {
                Base("Metadata/Items/Armours/Helmets/FourHelmetStr5", "Helmet", "Shielded Helm", "Yelmo blindado", 100, 0, 0),
                Base("Metadata/Items/Armours/Helmets/FourHelmetStr3", "Helmet", "Soldier Greathelm", "Gran yelmo de soldado", 60, 0, 0),
                Base("Metadata/Items/Armours/Helmets/FourHelmetDex2", "Helmet", "Felt Cap", "Gorra de fieltro", 0, 40, 0),
                Base("Metadata/Items/Rings/FourRing2", "Ring", "Sapphire Ring", "Anillo de zafiro", 0, 0, 0),
                Base("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear4", "Spear", "War Spear", "Lanza de guerra", 0, 0, 0),
                Base("Metadata/Items/Flasks/FourFlaskMana5", "ManaFlask", "Gargantuan Mana Flask", "Frasco de maná descomunal", 0, 0, 0),
                Base("Metadata/Items/Flasks/FourFlaskMana2", "ManaFlask", "Medium Mana Flask", "Frasco de maná mediano", 0, 0, 0),
            };

            public ItemBase? ByName(string name) => All.FirstOrDefault(item => item.Names.Values.Contains(name, StringComparer.OrdinalIgnoreCase));

            public ItemBase? ById(string id) => All.FirstOrDefault(item => item.Id.EndsWith(id[(id.LastIndexOf('/') + 1)..], StringComparison.OrdinalIgnoreCase) && id.Length > 0);

            private static ItemBase Base(string id, string itemClass, string english, string spanish, int armour, int evasion, int energyShield) =>
                new(id, itemClass, new Dictionary<string, string> { ["en"] = english, ["es"] = spanish }, armour, evasion, energyShield, 0);
        }

        /// <summary>A few of the game's modifier texts in two languages; "increased Physical Damage" is left out, as a line the files' texts would not tell.</summary>
        private sealed class Texts : IModTexts
        {
            private static readonly (string Template, string English, string Spanish)[] All =
            {
                ("life", "+{0} to maximum Life", "+{0} a la vida máxima"),
                ("fire", "+{0}% to Fire Resistance", "+{0}% a la resistencia al fuego"),
                ("lightning", "+{0}% to Lightning Resistance", "+{0}% a la resistencia al rayo"),
                ("physical", "Adds {0} to {1} Physical Damage", "Agrega de {0} a {1} de daño físico"),
                ("attack speed", "{0}% increased Attack Speed", "Velocidad de ataque aumentada un {0}%"),
                ("recovery", "{0}% of Recovery applied Instantly", "El {0}% de la recuperación se aplica al instante"),
                ("fire damage", "Adds {0} to {1} Fire damage to Attacks", "Agrega de {0} a {1} de daño de fuego a los ataques"),
            };

            public ModLine? Parse(string text)
            {
                foreach (var (template, english, spanish) in All)
                {
                    foreach (var format in new[] { english, spanish })
                    {
                        var match = Regex.Match(text, "^" + Regex.Replace(Regex.Escape(format), @"\\\{\d\}", @"(-?\d+(?:\.\d+)?)") + "$");
                        if (match.Success)
                        {
                            return new ModLine(template, match.Groups.Values.Skip(1).Select(group => double.Parse(group.Value, CultureInfo.InvariantCulture)).ToList());
                        }
                    }
                }

                return null;
            }

            public string Say(ModLine line, string language)
            {
                var (_, english, spanish) = All.First(text => text.Template == line.Template);
                return string.Format(CultureInfo.InvariantCulture, language == "es" ? spanish : english, line.Numbers.Cast<object>().ToArray());
            }
        }
    }
}
