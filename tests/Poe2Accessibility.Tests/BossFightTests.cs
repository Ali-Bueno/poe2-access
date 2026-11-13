namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The census's mechanics in the plugin (docs/research/boss-mechanics.md §14): the catalog's kinds, a boss's signals, the fight's places and what it puts on the player.</summary>
    public static class BossFightTests
    {
        private const string Titan = "Metadata/Monsters/TitanBoss/TitanBoss";
        private const string King = "Metadata/Monsters/PerennialKingStage2/PerennialKing";
        private const string Stalagmite = "Metadata/Monsters/TitanBoss/BrainStalagmite";
        private const string Asala = "Metadata/NPC/Four_Act2/MarakethMatriarchDreadnought";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheCatalogReadsTheCensusKinds), TheCatalogReadsTheCensusKinds),
            (nameof(ABuffTextIsSpokenWithoutMarkup), ABuffTextIsSpokenWithoutMarkup),
            (nameof(AnArenaStateIsSaidOnceAsItRises), AnArenaStateIsSaidOnceAsItRises),
            (nameof(AnEmpoweringBuffIsSaidAsItBeginsAndEnds), AnEmpoweringBuffIsSaidAsItBeginsAndEnds),
            (nameof(ACounterIsSaidByPartsOfItsTopOrNowAndThen), ACounterIsSaidByPartsOfItsTopOrNowAndThen),
            (nameof(AnElementIsNamedWhereTheFilesNameIt), AnElementIsNamedWhereTheFilesNameIt),
            (nameof(TheShelterHoldsBetweenItsRenewals), TheShelterHoldsBetweenItsRenewals),
            (nameof(AFightsBuffsOnThePlayerAreSaidAsTheyBegin), AFightsBuffsOnThePlayerAreSaidAsTheyBegin),
            (nameof(ACountIsSaidOnceItHoldsStill), ACountIsSaidOnceItHoldsStill),
            (nameof(ACoverIsStoodBehindAsSeenFromTheBoss), ACoverIsStoodBehindAsSeenFromTheBoss),
            (nameof(TheMostPressingPlaceLeads), TheMostPressingPlaceLeads),
            (nameof(AThingToUseIsOfferedOnceAndForAWhile), AThingToUseIsOfferedOnceAndForAWhile),
            (nameof(PlaceLinesNameThePlaceAndTheWay), PlaceLinesNameThePlaceAndTheWay),
            (nameof(TheCellOfAnUndrawnEntityIsFoundFromDrawnOnes), TheCellOfAnUndrawnEntityIsFoundFromDrawnOnes),
        };

        private static BossMechanicsCatalog Sample()
        {
            var catalog = new BossMechanicsCatalog();
            catalog.Add(new[]
            {
                $"boss|{Titan}|Zalmarath, the Colossus",
                $"daemon|{Titan}|Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget",
                $"cover|{Titan}|{Stalagmite}",
                $"use|{Titan}|Metadata/Terrain/Lever",
                $"target|{Titan}|Metadata/Monsters/TitanBoss/Wisp",
                $"arena|{King}|sandstorm|sandstorm",
                $"arena|{King}|bracket",
                $"empowered|{King}|perennial_king_lightning_tornado",
                $"shelter|{King}|asala_sand_shield",
                $"refuge|{King}|{Asala}|sandstorm_defence",
                $"clone|{King}|Metadata/Monsters/Clone|chosen_one",
                $"progress|{King}|collected|8",
                $"progress|{King}|timer",
                $"element|{King}|infuse=1|fire",
                $"element|{King}|element|-",
                $"stack|vomit_chunk_stew|{King}",
                $"grab|{King}|GACrowbellGrab",
                $"grabbed|{King}|imprisoned_buff",
                $"zone|{King}|zarokh_slow",
                $"unavoidable|{King}|GAPerennialKingSwordSlam",
                "text|en|perennial_king_lightning_tornado|Charged Tornado|The [Tornado]es are empowering <red>{the King}",
                "text|es|perennial_king_lightning_tornado|Tornado cargado|Los tornados fortalecen al Rey",
                "text|en|zarokh_slow|Zarokh's Influence|Time slows by {0}%",
            });
            return catalog;
        }

        private static void TheCatalogReadsTheCensusKinds()
        {
            var catalog = Sample();
            Equal(true, catalog.IsDaemon(Titan, "metadata/monsters/invisiblefire/mdtitanbosslasertarget"), "a helper of its boss, whatever the path's case");
            Equal(true, catalog.IsDaemon("Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget") && !catalog.IsDaemon(King, "Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget"), "not another boss's");
            Equal("sandstorm", catalog.ArenaKind(King, "sandstorm"), "an arena state's kind");
            Equal("change", catalog.ArenaKind(King, "bracket"), "a kind the line leaves out");
            Equal(true, catalog.ArenaKind(Titan, "sandstorm") == null, "only its own boss's state");
            Equal(true, catalog.IsEmpowering(King, "perennial_king_lightning_tornado") && catalog.IsShelter("asala_sand_shield"), "buffs of the fight");
            Equal(true, catalog.IsStack("vomit_chunk_stew") && catalog.IsZone("zarokh_slow") && catalog.IsGrabbed("imprisoned_buff"), "buffs on the player");
            Equal(true, catalog.IsGrab(King, "GACrowbellGrab") && catalog.IsUnavoidable("GAPerennialKingSwordSlam"), "skills");
            Equal((true, (int?)8), catalog.Progress(King, "collected"), "a counter with its top");
            Equal((true, (int?)null), catalog.Progress(King, "timer"), "a counter without one");
            Equal("fire", catalog.Element(King, "infuse", 1), "a value's element");
            Equal(true, catalog.Element(King, "infuse", 2) == null, "a value the files do not name");
            Equal(BossMechanicsCatalog.UnknownElement, catalog.Element(King, "element", 3), "an element state not spelt out");
            Equal(BossPlaceKind.Cover, catalog.PlaceRules(Stalagmite.ToLowerInvariant()).Single().Kind, "a cover by its path");
            Equal("sandstorm_defence", catalog.PlaceRules(Asala).Single().State, "a refuge and its state");
            Equal(5, catalog.Places, "five places");
            Equal(0, catalog.PlaceRules("Metadata/Chests/Urn").Count, "any other entity is no place");
        }

        private static void ABuffTextIsSpokenWithoutMarkup()
        {
            var catalog = Sample();
            Equal(("Charged Tornado", "The Tornadoes are empowering the King"), catalog.BuffText("perennial_king_lightning_tornado", "en")!.Value, "links and tags leave their words");
            Equal("Tornado cargado", catalog.BuffText("perennial_king_lightning_tornado", "es")!.Value.Name, "the player's language");
            Equal("Charged Tornado", catalog.BuffText("perennial_king_lightning_tornado", "fr")!.Value.Name, "English where the language has none");
            Equal("Time slows by %", catalog.BuffText("zarokh_slow", "en")!.Value.Description, "a value the table leaves to the game is dropped");
            Equal(true, catalog.BuffText("unknown", "en") == null, "no text");
            Equal("You are stunned", BossTexts.Plain("You are [Stun|stunned]"), "a link shows its second part");
            Equal("Name. Text", BossTexts.Line("Name", "Text"), "name and description");
            Equal("Name", BossTexts.Line("Name", "Name"), "a description that repeats the name");
        }

        private static void AnArenaStateIsSaidOnceAsItRises()
        {
            var (catalog, tracker) = (Sample(), new BossSignalTracker());
            var none = Array.Empty<string>();
            Equal(0, tracker.Update(catalog, King, new[] { ("sandstorm", 0L) }, none, true, 0).Count, "where the fight finds it");
            Equal(0, new BossSignalTracker().Update(catalog, King, new[] { ("sandstorm", 1L) }, none, true, 0).Count, "set when first read: not news");
            var risen = tracker.Update(catalog, King, new[] { ("sandstorm", 1L) }, none, true, 150);
            Equal(new BossSignal(BossSignalKind.Arena, "sandstorm"), risen.Single(), "the storm begins");
            tracker.Update(catalog, King, new[] { ("sandstorm", 0L) }, none, true, 300);
            Equal(0, tracker.Update(catalog, King, new[] { ("sandstorm", 1L) }, none, true, 450).Count, "a flicker is the same change");
            tracker.Update(catalog, King, new[] { ("sandstorm", 0L) }, none, true, 600);
            Equal(1, tracker.Update(catalog, King, new[] { ("sandstorm", 1L) }, none, true, 150 + BossSignalTracker.ArenaGapMs).Count, "again later");

            var idle = new BossSignalTracker();
            idle.Update(catalog, King, new[] { ("sandstorm", 0L) }, none, false, 0);
            Equal(0, idle.Update(catalog, King, new[] { ("sandstorm", 1L) }, none, false, 150).Count, "nothing before the fight is on");
        }

        private static void AnEmpoweringBuffIsSaidAsItBeginsAndEnds()
        {
            var (catalog, tracker) = (Sample(), new BossSignalTracker());
            var states = Array.Empty<(string, long)>();
            var buffed = new[] { "perennial_king_lightning_tornado", "other" };
            Equal(BossSignalKind.Empowered, tracker.Update(catalog, King, states, buffed, true, 0).Single().Kind, "said as it begins, there from the start or not");
            Equal(0, tracker.Update(catalog, King, states, buffed, true, 150).Count, "once");
            Equal(0, tracker.Update(catalog, King, states, Array.Empty<string>(), true, 300).Count, "a renewal's gap is no end");
            Equal(0, tracker.Update(catalog, King, states, buffed, true, 450).Count, "still the same buff");
            Equal(BossSignalKind.EmpowerEnded, tracker.Update(catalog, King, states, Array.Empty<string>(), true, 450 + BossSignalTracker.BuffHoldMs).Single().Kind, "ended");
            Equal(0, new BossSignalTracker().Update(catalog, Titan, states, buffed, true, 0).Count, "another boss's buff");
        }

        private static void ACounterIsSaidByPartsOfItsTopOrNowAndThen()
        {
            var (catalog, tracker) = (Sample(), new BossSignalTracker());
            var none = Array.Empty<string>();
            IReadOnlyList<BossSignal> At(long collected, long timer, long now) =>
                tracker.Update(catalog, King, new[] { ("collected", collected), ("timer", timer) }, none, true, now);

            Equal(0, At(0, 5, 0).Count, "where the fight finds them");
            Equal(0, At(1, 5, 150).Count, "one of eight: not yet a quarter");
            Equal(new BossSignal(BossSignalKind.Progress, "collected", 2, 8), At(2, 5, 300).Single(), "a quarter");
            Equal(0, At(3, 5, 450).Count, "between quarters");
            Equal(8L, At(8, 5, 600).Single().Value, "the top");
            Equal(6L, At(8, 6, 750).Single().Value, "a counter without a top: its value");
            Equal(0, At(8, 7, 900).Count, "not again so soon");
            Equal(9L, At(8, 9, 750 + BossSignalTracker.ProgressGapMs).Single().Value, "its latest value once the gap has passed");
        }

        private static void AnElementIsNamedWhereTheFilesNameIt()
        {
            var (catalog, tracker) = (Sample(), new BossSignalTracker());
            var none = Array.Empty<string>();
            tracker.Update(catalog, King, new[] { ("infuse", 0L), ("element", 0L) }, none, true, 0);
            Equal(new BossSignal(BossSignalKind.Element, "fire", 1), tracker.Update(catalog, King, new[] { ("infuse", 1L), ("element", 0L) }, none, true, 150).Single(), "fire");
            Equal(0, tracker.Update(catalog, King, new[] { ("infuse", 2L), ("element", 0L) }, none, true, 300).Count, "a value with no element");
            Equal(BossMechanicsCatalog.UnknownElement, tracker.Update(catalog, King, new[] { ("infuse", 2L), ("element", 2L) }, none, true, 450).Single().Key, "it changes, to what is not written");
            Equal("Element: fire", Announcements.BossElement("fire"), "the element's line");
            Equal("The element changes", Announcements.BossElement(BossMechanicsCatalog.UnknownElement), "the unknown's line");
        }

        private static void TheShelterHoldsBetweenItsRenewals()
        {
            var (catalog, buffs) = (Sample(), new BossPlayerBuffs());
            var shield = new[] { ("asala_sand_shield", 1) };
            var none = Array.Empty<(string, int)>();
            Equal(0, buffs.Update(catalog, none, true, 0).Count, "outside");
            Equal(PlayerFightNewsKind.Sheltered, buffs.Update(catalog, shield, true, 150).Single().Kind, "inside");
            Equal(0, buffs.Update(catalog, none, true, 300).Count, "between two renewals");
            Equal(true, buffs.Sheltered, "still inside");
            Equal(0, buffs.Update(catalog, shield, true, 450).Count, "renewed");
            Equal(PlayerFightNewsKind.ShelterLost, buffs.Update(catalog, none, true, 450 + BossPlayerBuffs.ShelterHoldMs).Single().Kind, "out while it is needed");
            buffs.Update(catalog, shield, true, 2000);
            Equal(0, buffs.Update(catalog, none, false, 2000 + BossPlayerBuffs.ShelterHoldMs).Count, "out once it is over: nothing to say");
        }

        private static void AFightsBuffsOnThePlayerAreSaidAsTheyBegin()
        {
            var (catalog, buffs) = (Sample(), new BossPlayerBuffs());
            var none = Array.Empty<(string, int)>();
            Equal(0, buffs.Update(catalog, new[] { ("zarokh_slow", 1) }, false, 0).Count, "on the player when the fight starts: not news");
            Equal(0, buffs.Update(catalog, new[] { ("zarokh_slow", 1) }, false, 150).Count, "still on");
            buffs.Update(catalog, none, false, 300);
            Equal(0, buffs.Update(catalog, new[] { ("zarokh_slow", 1) }, false, 450).Count, "back at once: the same one");
            buffs.Update(catalog, none, false, 600);
            Equal(new PlayerFightNews(PlayerFightNewsKind.Zone, "zarokh_slow"), buffs.Update(catalog, new[] { ("zarokh_slow", 1) }, false, 450 + BossPlayerBuffs.AgainMs).Single(), "again later");
            Equal(new PlayerFightNews(PlayerFightNewsKind.Grabbed, "imprisoned_buff"), buffs.Update(catalog, new[] { ("imprisoned_buff", 1) }, false, 5000).Single(), "held");
            Equal(0, buffs.Update(catalog, new[] { ("some_other_buff", 1) }, false, 5150).Count, "any other buff");
        }

        private static void ACountIsSaidOnceItHoldsStill()
        {
            var (catalog, buffs) = (Sample(), new BossPlayerBuffs());
            var none = Array.Empty<(string, int)>();
            buffs.Update(catalog, none, false, 0);
            Equal(0, buffs.Update(catalog, new[] { ("vomit_chunk_stew", 1) }, false, 150).Count, "just gained");
            Equal(0, buffs.Update(catalog, new[] { ("vomit_chunk_stew", 2) }, false, 300).Count, "still rising");
            var said = buffs.Update(catalog, new[] { ("vomit_chunk_stew", 2) }, false, 300 + BossPlayerBuffs.CountQuietMs).Single();
            Equal(new PlayerFightNews(PlayerFightNewsKind.Stack, "vomit_chunk_stew", 2), said, "said once it holds");
            Equal(0, buffs.Update(catalog, new[] { ("vomit_chunk_stew", 2) }, false, 2000).Count, "once");
            Equal("Explosive Retch, 2", Announcements.BossStacks("Explosive Retch", 2), "its line");
            Equal("2 stacks", Announcements.BossStacks(string.Empty, 2), "a buff the game does not name");
        }

        private static BossPlaceSight Sight(uint id, BossPlaceKind kind, float x, float y, bool active = true, string? path = null) =>
            new(id, kind, path ?? kind.ToString(), string.Empty, new Vector2(x, y), 20f, active);

        private static void ACoverIsStoodBehindAsSeenFromTheBoss()
        {
            var cover = Sight(1, BossPlaceKind.Cover, 500, 0);
            Equal(new Vector2(540, 0), BossPlaces.Target(cover, Vector2.Zero, 10f), "its radius and a body's width beyond it, away from the boss");
            Equal(new Vector2(500, 0), BossPlaces.Target(cover, new Vector2(500, 0), 10f), "a boss standing on it: the cover itself");
            Equal(new Vector2(500, 0), BossPlaces.Target(Sight(2, BossPlaceKind.Use, 500, 0), Vector2.Zero, 10f), "any other place: where it stands");

            var places = new BossPlaces();
            var news = places.Update(new[] { cover }, Vector2.Zero, new Vector2(0, 300), 10f, 40f, false, 0);
            Equal(new BossPlaceNews(BossPlaceKind.Cover, string.Empty, new Vector2(540, 0)), news.Single(), "said with the place to stand");
            Equal(new Vector2(540, 0), places.Lead!.Value.At, "the beat leads behind it");
            Equal(0, places.Update(new[] { cover }, Vector2.Zero, new Vector2(530, 0), 10f, 40f, false, 150).Count, "once");
            Equal(true, places.Lead == null, "behind it: no more beat");
            places.Update(new[] { cover }, Vector2.Zero, new Vector2(0, 300), 10f, 40f, false, 300);
            Equal(true, places.Lead == null, "found once, it is not led to again");
            Equal(1, places.Update(new[] { Sight(3, BossPlaceKind.Cover, 0, 500) }, Vector2.Zero, new Vector2(0, 300), 10f, 40f, false, 450).Count, "another cover is news");
        }

        private static void TheMostPressingPlaceLeads()
        {
            var places = new BossPlaces();
            var player = new Vector2(0, 0);
            var sights = new[]
            {
                Sight(1, BossPlaceKind.Use, 100, 0), Sight(2, BossPlaceKind.Clone, 800, 0), Sight(3, BossPlaceKind.Refuge, 0, 900),
                Sight(4, BossPlaceKind.Refuge, 0, 400, active: false),
            };
            var news = places.Update(sights, new Vector2(300, 300), player, 10f, 40f, false, 0);
            Equal("Refuge,Clone,Use", string.Join(",", news.Select(line => line.Kind)), "each said, the most pressing first; a refuge that is not up is none");
            Equal(new Vector2(0, 900), places.Lead!.Value.At, "the shelter first");
            places.Update(sights, new Vector2(300, 300), player, 10f, 40f, true, 150);
            Equal(new Vector2(800, 0), places.Lead!.Value.At, "sheltered: the next kind");
            Equal(0, places.Update(sights, new Vector2(300, 300), player, 10f, 40f, false, 300).Count, "nothing is said twice");
            places.Update(sights.Take(2).ToList(), new Vector2(300, 300), player, 10f, 40f, false, 450);
            Equal(1, places.Update(sights, new Vector2(300, 300), player, 10f, 40f, false, 600).Count, "a shelter that comes up again is said again");
        }

        private static void AThingToUseIsOfferedOnceAndForAWhile()
        {
            var places = new BossPlaces();
            var levers = new[] { Sight(1, BossPlaceKind.Use, 900, 0, path: "lever"), Sight(2, BossPlaceKind.Use, 300, 0, path: "lever") };
            var news = places.Update(levers, Vector2.Zero, Vector2.Zero, 10f, 40f, false, 0);
            Equal(new Vector2(300, 0), news.Single().To, "the nearest of its kind speaks for all of them");
            Equal(new Vector2(300, 0), places.Lead!.Value.At, "and is led to");
            Equal(0, places.Update(levers, Vector2.Zero, new Vector2(280, 0), 10f, 40f, false, 150).Count, "once");
            Equal(new Vector2(900, 0), places.Lead!.Value.At, "one reached: the next of them");
            places.Update(levers, Vector2.Zero, Vector2.Zero, 10f, 40f, false, BossPlaces.OfferMs);
            Equal(true, places.Lead == null, "the offer ends");
            Equal(0, places.Update(new[] { Sight(3, BossPlaceKind.Use, 100, 100, path: "lever") }, Vector2.Zero, Vector2.Zero, 10f, 40f, false, BossPlaces.OfferMs + 150).Count,
                "another of the same kind later is no news");
        }

        /// <summary>A Positioned component's bytes with a cell at 0x1F4 and, before it, a number two of the three samples share.</summary>
        private static (byte[] Bytes, int X, int Y) Positioned(int x, int y, int shared, int drawnX, int drawnY)
        {
            var bytes = new byte[PositionedGridSearch.SearchBytes];
            BitConverter.GetBytes(shared).CopyTo(bytes, 0x40);
            BitConverter.GetBytes(shared).CopyTo(bytes, 0x44);
            BitConverter.GetBytes(x).CopyTo(bytes, 0x1F4);
            BitConverter.GetBytes(y).CopyTo(bytes, 0x1F8);
            return (bytes, drawnX, drawnY);
        }

        private static void TheCellOfAnUndrawnEntityIsFoundFromDrawnOnes()
        {
            var samples = new[] { Positioned(120, 340, 120, 120, 340), Positioned(500, 77, 500, 501, 77), Positioned(42, 900, 7, 42, 899) };
            Equal(0x1F4, PositionedGridSearch.Find(samples), "the one pair that is every sample's cell, a cell off allowed");
            Equal(true, PositionedGridSearch.Find(samples.Take(2).ToList()) == null, "two cells prove nothing");
            Equal(true, PositionedGridSearch.Find(new[] { samples[0], samples[0], samples[0] }) == null, "nor one cell three times");
            var moved = new[] { samples[0], samples[1], Positioned(42, 900, 7, 300, 300) };
            Equal(true, PositionedGridSearch.Find(moved) == null, "no pair fits them all: not found");
        }

        private static void PlaceLinesNameThePlaceAndTheWay()
        {
            Equal("Shelter at Asala: follow the heartbeat, up", Announcements.BossPlace(BossPlaceKind.Refuge, "Asala", 2, true), "a named shelter");
            Equal("Cover, left", Announcements.BossPlace(BossPlaceKind.Cover, string.Empty, 4, false), "without the heartbeat");
            Equal("Lever, can be used: follow the heartbeat, right", Announcements.BossPlace(BossPlaceKind.Use, "Lever", 0, true), "a thing to use");
            Equal("Something to break, down", Announcements.BossPlace(BossPlaceKind.Break, string.Empty, 6, false), "a nameless thing to break");
            Equal("The boss: Charged Tornado. The tornadoes are empowering the King", Announcements.BossEmpowered(null, "Charged Tornado. The tornadoes are empowering the King"), "the game's words");
            Equal("Jamanra is empowered", Announcements.BossEmpowered("Jamanra", string.Empty), "no words: that it is");
            Equal("Progress 25 percent", Announcements.BossProgress(2, 8), "a share of the top");
            Equal("Progress 6", Announcements.BossProgress(6, null), "a value");
            Equal("Sandstorm", Announcements.BossArena("sandstorm"), "an arena's kind");
            Equal("The arena changes", Announcements.BossArena("retreat"), "any other kind");
        }
    }
}
