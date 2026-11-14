namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Trade listings, game-data steps, release tags, item moves, salvage and quest tracker pages.</summary>
    public static class ReleaseAndQuestTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TradeListingSaysItemPriceThenSeller), TradeListingSaysItemPriceThenSeller),
            (nameof(GameDataStepsRerunAlone), GameDataStepsRerunAlone),
            (nameof(GameDataStepsAreEachListedOnce), GameDataStepsAreEachListedOnce),
            (nameof(OldGameDataStampCarriesOver), OldGameDataStampCarriesOver),
            (nameof(GameContentIsFoundInEitherLayout), GameContentIsFoundInEitherLayout),
            (nameof(AClassSeesItsOwnPassives), AClassSeesItsOwnPassives),
            (nameof(KakaoClientIsAGameProcess), KakaoClientIsAGameProcess),
            (nameof(SteamGameIsStartedBeforeTheKakaoClient), SteamGameIsStartedBeforeTheKakaoClient),
            (nameof(ReleaseTagsOrderByDayThenNumber), ReleaseTagsOrderByDayThenNumber),
            (nameof(ReleaseManifestPinsThePackageToItsTag), ReleaseManifestPinsThePackageToItsTag),
            (nameof(ItemMovesAreSaidAsDeeds), ItemMovesAreSaidAsDeeds),
            (nameof(SalvageSaysWhatWentAndWhatCame), SalvageSaysWhatWentAndWhatCame),
            (nameof(QuestNamesItsTargets), QuestNamesItsTargets),
            (nameof(QuestTrackerPageIsSaidWhenItChanges), QuestTrackerPageIsSaidWhenItChanges),
            (nameof(QuestPageSaysOnlyTheQuestThatChanged), QuestPageSaysOnlyTheQuestThatChanged),
            (nameof(TurnedQuestPageIsToldFromTheObjectivesOwnChange), TurnedQuestPageIsToldFromTheObjectivesOwnChange),
            (nameof(AFallingClockIsSaidAtItsMarks), AFallingClockIsSaidAtItsMarks),
        };

        private static void TradeListingSaysItemPriceThenSeller()
        {
            var seller = new List<CardLine> { new("Mike_58#7693", false), new("Jugador desconectado", false), new("listado hace 1 meses", false) };
            var item = new List<CardLine>
            {
                new("Vision doble. Ballesta de diada", false), new("Ballesta", false), new("Carga una saeta adicional", false),
                new("Precio de salida:", true), new("1x", true), new("Orbe de aumento", true), new("DPS total: 88,80", false),
            };
            var card = CardText.Read(new List<IReadOnlyList<CardLine>> { seller, item }, 6);
            Equal("Vision doble. Ballesta de diada, Precio de salida: 1x, Orbe de aumento, Mike_58#7693, Jugador desconectado, listado hace 1 meses",
                  card?.Label, "the item, its price, then the seller's column with the status");
            Equal("Ballesta | Carga una saeta adicional | DPS total: 88,80", string.Join(" | ", card?.Details ?? new List<string>()), "the item's other lines follow, the price not again");
            Equal(null, CardText.Read(new List<IReadOnlyList<CardLine>> { seller, new List<CardLine> { new("Ballesta", false) } }, 6), "lines that fit a table row stay a row");
            var unpriced = CardText.Read(new List<IReadOnlyList<CardLine>> { seller, item.Where(line => !line.Price).ToList() }, 3);
            Equal("Vision doble. Ballesta de diada, Mike_58#7693, Jugador desconectado, listado hace 1 meses", unpriced?.Label, "without a price the longest column leads");
        }

        private static void GameDataStepsRerunAlone()
        {
            const string install = "index=10:20\nexe=30:40\nfmod=2.03.06\n";
            var stamp = Poe2Access.GameDataStamp.Parse(null).With(install, "sounds", 2).With(install, "places", 1);
            var read = Poe2Access.GameDataStamp.Parse(stamp.ToString().Replace("\n", "\r\n"));
            Equal(install, read.Install, "the install lines survive the file");
            Equal(true, read.IsCurrent(install, "sounds", 2), "a step done at its version is current");
            Equal(true, read.IsCurrent(install, "places", 1), "the other steps are kept");
            Equal(false, read.IsCurrent(install, "sounds", 3), "a raised version reruns that step");
            Equal(false, read.IsCurrent(install, "area names", 1), "a step never done runs");
            Equal(false, read.IsCurrent("index=11:20\nexe=30:40\nfmod=2.03.06\n", "places", 1), "a game patch reruns every step");
            Equal(1, read.With("index=11:20\n", "sounds", 2).Steps.Count, "a step done on a patched install forgets the others");
        }

        private static void GameDataStepsAreEachListedOnce()
        {
            // A step listed twice ran twice in one batch and the second run failed on the first's output (2026-11-08).
            var steps = Poe2Access.GameDataSteps.All;
            Equal(steps.Length, steps.Select(step => step.Name).Distinct().Count(), "each step name once");
            Equal(steps.Count(step => step.Output != null), steps.Where(step => step.Output != null).Select(step => step.Output).Distinct().Count(), "each output once");
            Equal(steps.Count(step => step.Command != null), steps.Where(step => step.Command != null).Select(step => step.Command).Distinct().Count(), "each command once");
        }

        private static void OldGameDataStampCarriesOver()
        {
            const string install = "index=10:20\nexe=30:40\nfmod=2.03.06\n";
            string[] steps = { "item sounds", "places", "sounds" };
            var current = Poe2Access.GameDataStamp.FromLegacy("format=7\r\n" + install.Replace("\n", "\r\n"), install, steps, "sounds");
            Equal(3, current.Steps.Count, "the last format vouches for every step");
            Equal(true, current.IsCurrent(install, "sounds", Poe2Access.GameDataStamp.LegacyVersion), "at the first version");
            var older = Poe2Access.GameDataStamp.FromLegacy("format=5\n" + install, install, steps, "sounds");
            Equal(false, older.Steps.ContainsKey("sounds"), "an earlier format reruns the sounds");
            Equal(true, older.IsCurrent(install, "places", 1), "and keeps the catalogs it covered");
            Equal(0, Poe2Access.GameDataStamp.FromLegacy("format=2\n" + install, install, steps, "sounds").Steps.Count, "a format before hit times and places covers nothing");
            Equal(0, Poe2Access.GameDataStamp.FromLegacy("format=7\nindex=9:20\nexe=30:40\nfmod=2.03.06\n", install, steps, "sounds").Steps.Count, "another install covers nothing");
            Equal(0, Poe2Access.GameDataStamp.FromLegacy(null, install, steps, "sounds").Steps.Count, "no old stamp covers nothing");
        }

        private static void GameContentIsFoundInEitherLayout()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poe2-install-" + Guid.NewGuid().ToString("N"));
            var ggpk = System.IO.Path.Combine(root, "Content.ggpk");
            var index = System.IO.Path.Combine(root, "Bundles2", "_.index.bin");
            try
            {
                System.IO.Directory.CreateDirectory(root);
                Equal(null, Poe2Access.GameInstall.ContentFile(root), "a folder without game content is no install");
                System.IO.File.WriteAllText(ggpk, string.Empty);
                Equal(ggpk, Poe2Access.GameInstall.ContentFile(root), "the Kakao Games client keeps everything in Content.ggpk");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(index)!);
                System.IO.File.WriteAllText(index, string.Empty);
                Equal(index, Poe2Access.GameInstall.ContentFile(root), "a Steam install's bundle index comes first");
            }
            finally
            {
                System.IO.Directory.Delete(root, true);
            }
        }

        /// <summary>The Druid's tree shows "Regeneración de vida" where the node record keeps "Daño crítico de hechizos" (2026-11-09).</summary>
        private static void AClassSeesItsOwnPassives()
        {
            var overrides = PassiveOverrides.Parse(new[]
            {
                "# character id\treplaced passive id\trow\tshown passive id\trow",
                "row-size\t487",
                "Metadata/Characters/StrInt/StrIntFourb\tspell_criticals7\t4709\tdruid_class_start22\t5699",
                "Metadata/Characters/Int/IntFour\tspells18\t2779\tspell_and_minion_damage1\t4935",
            });
            Equal(2, overrides.Count, "both overrides read");
            Equal("druid_class_start22", overrides.Find("Metadata/Characters/StrInt/StrIntFourb", "spell_criticals7")?.ShownId, "the Druid's node in its place");
            Equal("druid_class_start22", overrides.Find("metadata/characters/strint/strintfourb", "spell_criticals7")?.ShownId, "the player's path in any case");
            Equal((5699L - 4709L) * 487L, overrides.Step(overrides.Find("Metadata/Characters/StrInt/StrIntFourb", "spell_criticals7")!.Value), "rows apart times the row size");
            Equal(false, overrides.Find("Metadata/Characters/Dex/DexFour", "spell_criticals7").HasValue, "another class sees the node itself");
            Equal(0, PassiveOverrides.Parse(new[] { "Metadata/Characters/Int/IntFour\tspells18\t2779\tspell_and_minion_damage1\t4935" }).Count, "no row size: nothing swapped");
        }

        private static void KakaoClientIsAGameProcess()
        {
            Equal(true, Poe2Access.GameInstall.ProcessNames.Contains("PathOfExile_KG"), "the Korean (Kakao Games) client's process");
            Equal(true, Poe2Access.GameInstall.ProcessNames.Contains("PathOfExileSteam"), "the Steam client's process");
            Equal(true, Poe2Access.GameInstall.NeedsAdministrator("PathOfExile_KG", false, false), "the Korean client is read only by an administrator");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator("PathOfExile_KG", false, true), "an administrator launcher carries on");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator("PathOfExileSteam", false, false), "the Steam client needs no administrator");
            Equal(true, Poe2Access.GameInstall.NeedsAdministrator("PathOfExileSteam", false, false, gameReadDenied: true), "unless it runs as administrator (Steam run so)");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator("PathOfExileSteam", false, true, gameReadDenied: true), "denied to an administrator too: elevating again would not help");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator(null, false, false), "no game running needs no administrator");
            Equal(true, Poe2Access.GameInstall.NeedsAdministrator(null, true, false), "an administrator host left after the Korean client closed");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator(null, true, true), "an administrator launcher stops it itself");
        }

        private static void SteamGameIsStartedBeforeTheKakaoClient()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poe2-steam-" + Guid.NewGuid().ToString("N"));
            var steam = System.IO.Path.Combine(root, "Steam");
            var library = System.IO.Path.Combine(root, "Games");
            var kakao = System.IO.Path.Combine(root, "Kakaogames");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(steam, "steamapps"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(library, "steamapps"));
                Equal(false, Poe2Access.GameInstall.SteamHasGame(null), "no Steam has no game");
                Equal(false, Poe2Access.GameInstall.SteamHasGame(steam), "Steam without the game");

                // libraryfolders.vdf escapes the backslashes of each library's path.
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
                    "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steam.Replace("\\", "\\\\") + "\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" +
                    library.Replace("\\", "\\\\") + "\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"2694490\"\t\t\"1\"\n\t\t}\n\t}\n}\n");
                Equal(false, Poe2Access.GameInstall.SteamHasGame(steam), "a library listing the game but holding no manifest");
                System.IO.File.WriteAllText(System.IO.Path.Combine(library, "steamapps", "appmanifest_2694490.acf"), string.Empty);
                Equal(true, Poe2Access.GameInstall.SteamHasGame(steam), "the game in a second library");

                Equal(false, Poe2Access.GameInstall.StartsKakaoClient(true, kakao), "both installed: Steam starts the game");
                Equal(true, Poe2Access.GameInstall.StartsKakaoClient(false, kakao), "only the Korean client installed");
                Equal(false, Poe2Access.GameInstall.StartsKakaoClient(false, null), "neither found: Steam, as before");
            }
            finally
            {
                System.IO.Directory.Delete(root, true);
            }
        }

        private static void ReleaseTagsOrderByDayThenNumber()
        {
            Equal(new Version(2026, 10, 28, 1), Poe2Access.ReleaseTag.Parse("v2026.10.28"), "an unnumbered tag is the day's first release");
            Equal(true, Poe2Access.ReleaseTag.Parse("v2026.10.28-2") > Poe2Access.ReleaseTag.Parse("v2026.10.28"), "-2 follows the day's first");
            Equal(true, Poe2Access.ReleaseTag.Parse("v2026.10.29") > Poe2Access.ReleaseTag.Parse("v2026.10.28-3"), "a later day follows any number");
            Equal(true, Poe2Access.ReleaseTag.Parse("v2026.10.28-10") > Poe2Access.ReleaseTag.Parse("v2026.10.28-9"), "numbers compare as numbers");
            Equal<Version?>(null, Poe2Access.ReleaseTag.Parse("latest"), "not a dated tag");
            Equal<Version?>(null, Poe2Access.ReleaseTag.Parse("v2026.10"), "a tag without its day");
            Equal<Version?>(null, Poe2Access.ReleaseTag.Parse("v2026.10.28-0"), "releases are numbered from 2");
        }

        private static void ReleaseManifestPinsThePackageToItsTag()
        {
            var sha256 = new string('A', 64);
            var release = Poe2Access.ReleaseManifest.Parse($"{{\"tag\": \"v2026.10.30-2\", \"sha256\": \"{sha256}\", \"notes\": \" Changes \"}}");
            Equal("https://github.com/Ali-Bueno/poe2-access/releases/download/v2026.10.30-2/Poe2Access.zip", release.ZipUrl, "the package of that release, not of whatever is latest");
            Equal(new Version(2026, 10, 30, 2), release.Version, "the tag as a version");
            Equal(sha256, release.Sha256, "the checksum as written");
            Equal("Changes", release.Notes, "notes trimmed");

            string Refused(string json)
            {
                try
                {
                    Poe2Access.ReleaseManifest.Parse(json);
                    return "accepted";
                }
                catch (System.IO.InvalidDataException)
                {
                    return "refused";
                }
            }

            Equal("refused", Refused("{\"tag\": \"v2026.10.30\", \"sha256\": \"abc\"}"), "a short checksum");
            Equal("refused", Refused($"{{\"tag\": \"v2026.10.30\", \"sha256\": \"{new string('g', 64)}\"}}"), "a checksum that is not hex");
            Equal("refused", Refused($"{{\"tag\": \"latest\", \"sha256\": \"{sha256}\"}}"), "a tag that is not a date");
        }

        private static void ItemMovesAreSaidAsDeeds()
        {
            string Said(List<ItemMove> moves) => string.Join(" | ", moves.Select(move => $"{move.Kind} {move.Name} {move.Where}".Trim()));
            var main = InventoryName.MainInventory1;
            var none = new List<(InventoryName, string)>();
            Equal("Sold Brazales", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Brazales") }, Announcements.SlotName, true, 19, "Mercader")),
                  "gold rose with the departure: sold, whatever panel is open");
            Equal("Bought Lanza", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Lanza") }, none, Announcements.SlotName, true, -555, "")),
                  "gold fell with the arrival: bought");
            Equal("MovedTo Orbe Alijo", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Orbe") }, Announcements.SlotName, true, 0, "Alijo")),
                  "the hand let go into the stash");
            Equal("TakenFrom Orbe Alijo", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Orbe") }, none, Announcements.SlotName, true, 0, "Alijo")),
                  "came from the stash");
            Equal("DroppedOrUsed Arco", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Arco") }, Announcements.SlotName, true, 0, "")),
                  "gone with only the inventory open: dropped or used, decided on the ground");
            Equal("Equipped Casco Helmet", Said(ItemMoves.Read(new List<(InventoryName, string)> { (InventoryName.Helm1, "Casco") }, new List<(InventoryName, string)> { (main, "Casco") },
                                                              Announcements.SlotName, true, 0, "")),
                  "equipped from the inventory: only that");
            Equal(string.Empty, Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Orbe") }, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Orbe") },
                                                    Announcements.SlotName, true, 0, "Alijo")),
                  "put back from the hand: a placement, not news");
            Equal(string.Empty, Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Orbe, 6") },
                                                    new List<(InventoryName, string)> { (main, "Orbe, 5"), (InventoryName.Cursor1, "Orbe") }, Announcements.SlotName, true, 0, "")),
                  "put back on its stack: the stack that grew was not used up");
            Equal(string.Empty, Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Orbe") }, Announcements.SlotName, false, 0, "")),
                  "during play the pick-up watch speaks");
            Equal("TakenFrom Runa Alijo", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Runa") }, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Runa") },
                                                              Announcements.SlotName, true, 0, "Alijo", "Alijo")),
                  "the hand picked it up in the stash and set it down in the inventory");
            Equal(string.Empty, Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Runa") }, Announcements.SlotName, true, 0, "Alijo", "Alijo")),
                  "picked up and set down in the stash: rearranging, not news");
            Func<string, bool> gemsOnly = name => name == "Furia I";
            Equal("DroppedOrUsed Orbe | MovedTo Furia I Habilidades", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Orbe"), (main, "Furia I") },
                                                                                          Announcements.SlotName, true, 0, "Habilidades", "", gemsOnly)),
                  "beside the skills only a gem goes into them; an orb was used or dropped");
            Equal("Obtained Arco | TakenFrom Furia I Habilidades", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Arco"), (main, "Furia I") }, none,
                                                                                          Announcements.SlotName, true, 0, "Habilidades", "", gemsOnly)),
                  "and only a gem comes out of them");
            Equal("Orbe de aumento", ItemMoves.Title("Orbe de aumento, 3"), "the title without the stack");
        }

        private static void SalvageSaysWhatWentAndWhatCame()
        {
            string Said(SalvageOutcome outcome) =>
                string.Join(" | ", outcome.Salvaged) + " -> " + string.Join(" | ", outcome.Received.Select(item => $"{item.Title} {item.Count}"));
            var helmet = ("Corona con cuernos, raro", 1);
            var scrolls = ("Pergamino de sabiduría, 23", 23);
            Equal("Corona con cuernos, raro -> Esquirla del artesano 1",
                  Said(SalvageOutcome.Read(new[] { helmet, scrolls }, new[] { scrolls, ("Esquirla del artesano", 1) })),
                  "the item went and a new stack came");
            Equal("Corona con cuernos, raro -> Chatarra de armero 2",
                  Said(SalvageOutcome.Read(new[] { helmet, ("Chatarra de armero, 3", 3) }, new[] { ("Chatarra de armero, 5", 5) })),
                  "a stack that grew counts only what came");
            Equal(" -> ", Said(SalvageOutcome.Read(new[] { helmet, scrolls }, new[] { scrolls, helmet })), "moved within the inventory: nothing");
            Equal("Gorro de fieltro -> ", Said(SalvageOutcome.Read(new[] { ("Gorro de fieltro", 1), ("Gorro de fieltro, mágico", 1) },
                                                                    new[] { ("Gorro de fieltro, mágico", 1) })),
                  "of two items of one base, the one that went");
        }

        private static void QuestNamesItsTargets()
        {
            var entries = new[]
            {
                new ScanEntry(1, ScanCategory.Exits, "Campamento de Sierraclara", 0, 0, 0),
                new ScanEntry(2, ScanCategory.Enemies, "El molinero hinchado, único", 0, 0, 0),
                new ScanEntry(3, ScanCategory.Enemies, "Ahogado", 0, 0, 0),
                new ScanEntry(4, ScanCategory.Waypoints, "Punto de control", 0, 0, 0),
                new ScanEntry(5, ScanCategory.Npcs, "Renly", 0, 0, 0),
            };
            string Names(params string[] texts) => string.Join(" | ", QuestMatcher.Targets(entries, texts).Select(entry => entry.Name));
            Equal("Campamento de Sierraclara | El molinero hinchado, único", Names("Llegar a Sierraclara", "Busca al Molinero y encuentra refugio en Sierraclara"), "proper nouns of the objective");
            Equal(string.Empty, Names("Los ahogados observan entre las lodosas ramas"), "ordinary monsters never");
            Equal("Renly", Names("Talk to Renly"), "a name inside the sentence");
            Equal(string.Empty, Names("Renly"), "a label equal to the name is its own nameplate");
            Equal(true, QuestMatcher.Targets(entries, new[] { "Talk to Renly" }).All(entry => entry.Category == ScanCategory.Quest), "copies in the Quest category");
            Equal(string.Empty, string.Join(" | ", QuestMatcher.Targets(entries, new[] { "Suelo traicionero", "Registra Sierraclara para encontrar la entrada al Lodazal" }, "Sierraclara").Select(entry => entry.Name)),
                  "the area the player searches is not a destination");
            Equal("Renly, con misión", string.Join(" | ", QuestMatcher.Targets(new[] { new ScanEntry(6, ScanCategory.Npcs, "Renly, con misión", 0, 0, 0, "npc_quest") },
                                                                                new[] { "Habla con Renly" }).Select(entry => entry.Name)),
                  "an NPC named with its quest marker");
            var places = new[] { new ScanEntry(7, ScanCategory.Places, "entrada", 0, 0, 0), new ScanEntry(8, ScanCategory.Places, "tumba", 0, 0, 0) };
            Equal(string.Empty, string.Join(" | ", QuestMatcher.Targets(places, new[] { "Encuentra la entrada al Valle rojo en el Bosque singular", "Busca a Asinia en la Tumba de la consorte" })
                                                                   .Select(entry => entry.Name)),
                  "a place's common noun in the objective is not the target");
            var notes = new[]
            {
                new ScanEntry(9, ScanCategory.Exits, "Ogham Farmlands Or Deadend", 0, 0, 0, QuestMatcher.TileNote),
                new ScanEntry(10, ScanCategory.Exits, "Pueblo de Ogham", 0, 0, 0),
            };
            Equal("Pueblo de Ogham", string.Join(" | ", QuestMatcher.Targets(notes, new[] { "El tesoro salvaje de Ogham", "Encuentra la entrada del pueblo" })
                                                               .Select(entry => entry.Name)),
                  "a tile note shares a word with the quest, an area name may");
            Equal("Ogham Farmlands Or Deadend", string.Join(" | ", QuestMatcher.Targets(notes.Take(1).ToList(), new[] { "Find Ogham Farmlands Or Deadend" }).Select(entry => entry.Name)),
                  "a tile note named whole");
            var ramparts = new[]
            {
                new ScanEntry(14, ScanCategory.Exits, "Mansión de Ogham", 0, 0, 0),
                new ScanEntry(15, ScanCategory.Exits, "Camino de vuelta", 0, 0, 0, QuestMatcher.TileNote),
            };
            Equal("Mansión de Ogham", string.Join(" | ", QuestMatcher.Targets(ramparts, new[] { "El lobo rabioso de Ogham", "Infíltrate en la Mansión más allá del pueblo. Encuentra al Conde Geonor" },
                                                                               "Las murallas de la mansión", "Pueblo de Ogham").Select(entry => entry.Name)),
                  "the Manor from its ramparts, never the way back (user 2026-11-05)");
            var altars = new[]
            {
                new ScanEntry(11, ScanCategory.Encounters, "Altar de ritual", 0, 0, 0, "ritual"),
                new ScanEntry(12, ScanCategory.Encounters, "Altar de ritual del jefe", 0, 0, 0, "ritual"),
                new ScanEntry(13, ScanCategory.Encounters, "Altar de ritual, completado", 0, 0, 0, "ritual"),
            };
            Equal("Altar de ritual", string.Join(" | ", QuestMatcher.Targets(altars, new[] { "Altares siniestros", "Busca los altares de ritual y purifícalos" }).Select(entry => entry.Name)),
                  "the objective's plural names the altars still to do, not the boss's nor a done one (Freythorn, 2026-10-29)");
        }

        private static void QuestTrackerPageIsSaidWhenItChanges()
        {
            var page = new QuestTrackerPage();
            Equal("Altares siniestros. Busca Villafrey", page.News(new[] { "Altares siniestros. Busca Villafrey" }), "the first page");
            Equal(string.Empty, page.News(Array.Empty<string>()), "the tracker hidden by a panel");
            Equal(string.Empty, page.News(new[] { "Altares siniestros. Busca Villafrey" }), "back the same: no news");
            Equal("Altares siniestros. Busca los altares de ritual", page.News(new[] { "Altares siniestros. Busca los altares de ritual" }), "the objective changed on entering the area");
            Equal("El rastro de la corrupción. Viaja a Ogham", page.News(new[] { "El rastro de la corrupción. Viaja a Ogham" }), "another page");
            Equal("Altares siniestros. Busca los altares de ritual", page.News(new[] { "Altares siniestros. Busca los altares de ritual" }), "paged back to it");
        }

        private static void QuestPageSaysOnlyTheQuestThatChanged()
        {
            var page = new QuestTrackerPage();
            var (rising, trail) = ("Ascenso al poder. Encuentra un Altar de reliquias", "El rastro de la corrupción. Vuelve sobre los pasos de Orbala");
            Equal($"{rising}. {trail}", page.News(new[] { rising, trail }), "the first page, whole");
            var placed = "Ascenso al poder. Coloca tu barya en el Altar de reliquias";
            Equal(placed, page.News(new[] { placed, trail }), "one objective changed: only that quest");
            var city = "La ciudad de los siete ríos. Usa el Mapa del desierto";
            Equal(city, page.News(new[] { placed, city }), "paged on: the quest in hand opens every page, so only the other");
            Equal(string.Empty, page.News(new[] { placed }), "a quest gone leaves nothing new");
            var ivory = "Un robo de marfil. Viaja al Páramo";
            var crown = "Una corona de piedra. Encuentra la Gruta";
            Equal($"{ivory}. {crown}", page.News(new[] { ivory, crown }), "a page with nothing in common is said whole");
            Equal(string.Empty, page.News(new[] { ivory, crown }, waits: true), "the same page in a menu is no news");
            Equal(string.Empty, page.News(new[] { ivory, placed }, waits: true), "a change waits for the menu");
            Equal(placed, page.News(new[] { ivory, placed }), "and says only itself");
        }

        private static void TurnedQuestPageIsToldFromTheObjectivesOwnChange()
        {
            var page = new QuestTrackerPage();
            var (rising, trail, city) = ("Ascenso al poder. Busca el altar", "El rastro. Sigue a Orbala", "La ciudad. Viaja a Keth");
            page.News(new[] { rising, trail }, legend: "Página de misiones 1/5 LT + cruceta derecha");
            Equal(false, page.Turned, "the first page is no turn");
            Equal(city, page.News(new[] { rising, city }, legend: "Página de misiones 2/5 LT + cruceta derecha"), "the next page");
            Equal(true, page.Turned, "its number changed: the player turned it");
            var placed = "Ascenso al poder. Coloca tu barya";
            Equal(placed, page.News(new[] { placed, city }, legend: "Página de misiones 2/5 LT + cruceta derecha"), "an objective on the same page");
            Equal(false, page.Turned, "is no turn");
            Equal(string.Empty, page.News(new[] { placed, city }, legend: "Página de misiones 3/5 LT + cruceta derecha"), "the legend is read after the pages, a turn can show there first");
            Equal(false, page.Turned, "with nothing to say yet");
            Equal(trail, page.News(new[] { placed, trail }, legend: "Página de misiones 3/5 LT + cruceta derecha"), "the page follows");
            Equal(true, page.Turned, "and is that turn");
            Equal(string.Empty, page.News(new[] { placed, trail }, legend: "Página de misiones 3/6 LT + cruceta derecha"), "a new quest only raises the total");
            Equal(placed, page.News(new[] { placed }, legend: "Página de misiones 4/6 LT + cruceta derecha"), "a page turned to with nothing new is said whole: the player asked");
            Equal(true, page.Turned, "and turned");

            // A Trial's rooms: the Trial's quest takes the page's second place while a room asks something and leaves it when the room is done.
            var rooms = new QuestTrackerPage();
            var (exit, crystals) = ("La prueba de las sekhemas. Encuentra la salida", "La prueba de las sekhemas. Desactiva los cristales");
            const string six = "Página de misiones 1/6 LT + cruceta derecha", five = "Página de misiones 1/5 LT + cruceta derecha";
            rooms.News(new[] { rising, trail }, legend: five);
            Equal(exit, rooms.News(new[] { rising, exit }, legend: six), "the room's objective takes the other quest's place");
            Equal(string.Empty, rooms.News(new[] { rising, trail }, legend: five), "the room done, the quest it pushed off is back: no news");
            Equal(crystals, rooms.News(new[] { rising, crystals }, legend: six), "the next room's objective");
            Equal(string.Empty, rooms.News(new[] { rising, trail }, legend: five), "done too");
            Equal(exit, rooms.News(new[] { rising, exit }, legend: six), "a later room asks the first one's again: said again");
            var moved = "El rastro. Viaja a Keth";
            Equal(moved, rooms.News(new[] { rising, moved }, legend: five), "the quest behind changed meanwhile: news");
            Equal(city, rooms.News(new[] { rising, city }, legend: "Página de misiones 2/5 LT + cruceta derecha"), "a page the player turns to is said");
            Equal(moved, rooms.News(new[] { rising, moved }, legend: five), "and so is the one turned back to");
        }

        private static void AFallingClockIsSaidAtItsMarks()
        {
            var watch = new CountdownWatch();
            Equal(null, watch.Heard("00:20"), "a clock first seen may not be falling");
            Equal(19, watch.Heard("00:19"), "falling: said at once");
            Equal(null, watch.Heard("00:19"), "the same second is no news");
            Equal(null, watch.Heard("00:18"), "nor any second");
            Equal(null, watch.Heard("00:11"), "before a mark");
            Equal(10, watch.Heard("00:10"), "the mark of ten");
            Equal(4, watch.Heard("00:04"), "a mark jumped over is still said, with the time left");
            Equal(null, watch.Heard(null), "gone");
            Equal(null, watch.Heard("00:01"), "a clock that counts up");
            Equal(null, watch.Heard("00:02"), "is never said");
            Equal(true, CountdownWatch.IsClock("00:19") && CountdownWatch.IsClock("01:05"), "minutes and seconds");
            Equal(false, CountdownWatch.IsClock("9") || CountdownWatch.IsClock("1/5"), "a flask's charges and a page are no clock");
            Equal(false, CountdownWatch.IsClock("0:06"), "nor a buff's duration");
            Equal(true, MinimapMarks.IsQuestIcon("SanctumFountain"), "the Death Crystal is what the Trial's room wants");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.DeviceKind("MarakethSanctumRoomSelector"), "and a Trial's reward, under Leagues, sounds as a quest object");
            foreach (var reward in new[] { "MarakethSanctumFountain", "MarakethSanctumKey", "MarakethSanctumKeyConverterActive", "SanctumPact" })
            {
                Equal(MinimapMarks.QuestObjectKind, MinimapMarks.DeviceKind(reward), $"a reward that draws its own icon too: {reward}");
            }

            Equal("encounter", MinimapMarks.DeviceKind("MarakethSanctumKeyConverterInactive"), "the forge once spent keeps quiet");
        }
    }
}
