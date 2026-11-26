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
            (nameof(ALitStoneLeadsUntilItIsUsed), ALitStoneLeadsUntilItIsUsed),
            (nameof(AClonesWayNamesWhereItStops), AClonesWayNamesWhereItStops),
            (nameof(TheSafeSpotIsSaidUntilThePlayerIsInIt), TheSafeSpotIsSaidUntilThePlayerIsInIt),
            (nameof(TheThrowsTargetIsTheNearestWithItsHelperAtWork), TheThrowsTargetIsTheNearestWithItsHelperAtWork),
            (nameof(AMazesLinesAreWalkedThroughTheirOpenings), AMazesLinesAreWalkedThroughTheirOpenings),
            (nameof(AMazesRingsAreLeftRoundToTheirOpenings), AMazesRingsAreLeftRoundToTheirOpenings),
            (nameof(AMazesRoundIsToldByItsEffects), AMazesRoundIsToldByItsEffects),
            (nameof(TheWayOutOfAMazeIsSaidAsItTurns), TheWayOutOfAMazeIsSaidAsItTurns),
            (nameof(TheShelterEndsOnceItStaysDown), TheShelterEndsOnceItStaysDown),
            (nameof(AThingToUseIsToldOfOnceAndNeverLedTo), AThingToUseIsToldOfOnceAndNeverLedTo),
            (nameof(PlaceLinesNameThePlaceAndTheWay), PlaceLinesNameThePlaceAndTheWay),
            (nameof(TheCellOfAnUndrawnEntityIsFoundFromDrawnOnes), TheCellOfAnUndrawnEntityIsFoundFromDrawnOnes),
            (nameof(AHelperWarnsAsItsDangerReachesThePlayer), AHelperWarnsAsItsDangerReachesThePlayer),
            (nameof(TheShippedCatalogHoldsTheFightsThePlayLogMet), TheShippedCatalogHoldsTheFightsThePlayLogMet),
        };

        private static BossMechanicsCatalog Sample()
        {
            var catalog = new BossMechanicsCatalog();
            catalog.Add(new[]
            {
                $"boss|{Titan}|Zalmarath, the Colossus",
                $"daemon|{Titan}|Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget|GSTitanBossLaserImpact, GSTitanBossLaserEnd",
                $"cover|{Titan}|{Stalagmite}",
                $"use|{Titan}|Metadata/Terrain/Lever",
                $"target|{Titan}|Metadata/Monsters/TitanBoss/Wisp",
                $"arena|{King}|sandstorm|sandstorm",
                $"arena|{King}|bracket",
                $"empowered|{King}|perennial_king_lightning_tornado",
                $"consume|{King}|Metadata/Monsters/SkeletonSnake/SkeletonSnakeSerpentCasterMinion",
                $"shelter|{King}|asala_sand_shield",
                $"refuge|{King}|{Asala}|sandstorm_defence",
                $"clone|{King}|Metadata/Monsters/Clone|chosen_one",
                $"clonepath|{King}|Metadata/Monsters/ClonePath",
                $"areas|{King}|GAOne,GSTwo",
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
            Equal("GSTitanBossLaserImpact,GSTitanBossLaserEnd", string.Join(",", catalog.DaemonSkills(Titan, "Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget")), "the helper's own skills");
            Equal(0, catalog.DaemonSkills(King, "Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget").Count, "none for another boss");
            Equal(true, catalog.FeedsBoss(King, "Metadata/Monsters/SkeletonSnake/SkeletonSnakeSerpentCasterMinion") && !catalog.FeedsBoss(Titan, "Metadata/Monsters/SkeletonSnake/SkeletonSnakeSerpentCasterMinion"), "adds that empower their boss");
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

            // A burn that goes on counting changed the arena once, as it began.
            var burn = new BossMechanicsCatalog();
            burn.Add(new[] { $"arena|{King}|room_burning_progress|burn", $"progress|{King}|room_burning_progress" });
            var burning = new BossSignalTracker();
            burning.Update(burn, King, new[] { ("room_burning_progress", 0L) }, none, true, 0);
            Equal("Arena,Progress", string.Join(",", burning.Update(burn, King, new[] { ("room_burning_progress", 5L) }, none, true, 150).Select(signal => signal.Kind)), "it begins");
            var later = burning.Update(burn, King, new[] { ("room_burning_progress", 60L) }, none, true, 150 + BossSignalTracker.ArenaGapMs);
            Equal(BossSignalKind.Progress, later.Single().Kind, "counting on: its count, not the arena again");

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
            tracker.Update(catalog, King, new[] { ("infuse", 0L), ("element", 2L) }, none, true, 600);
            Equal(0, tracker.Update(catalog, King, new[] { ("infuse", 1L), ("element", 2L) }, none, true, 750).Count, "flipping back at once is the same change");
            tracker.Update(catalog, King, new[] { ("infuse", 0L), ("element", 2L) }, none, true, 900);
            Equal(1, tracker.Update(catalog, King, new[] { ("infuse", 1L), ("element", 2L) }, none, true, 150 + BossSignalTracker.ArenaGapMs).Count, "again later");
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

        private static BossPlaceSight Sight(uint id, BossPlaceKind kind, float x, float y, bool active = true, string name = "", Vector2 boss = default) =>
            new(id, kind, kind.ToString(), name, new Vector2(x, y), 20f, active, boss);

        private static void ACoverIsStoodBehindAsSeenFromTheBoss()
        {
            var cover = Sight(1, BossPlaceKind.Cover, 500, 0);
            Equal(new Vector2(540, 0), BossPlaces.Target(cover, 10f), "its radius and a body's width beyond it, away from its boss");
            Equal(new Vector2(500, 0), BossPlaces.Target(Sight(1, BossPlaceKind.Cover, 500, 0, boss: new Vector2(500, 0)), 10f), "a boss standing on it: the cover itself");
            Equal(new Vector2(460, 0), BossPlaces.Target(Sight(1, BossPlaceKind.Cover, 500, 0, boss: new Vector2(900, 0)), 10f), "the other side for a boss on the other side");
            Equal(new Vector2(500, 0), BossPlaces.Target(Sight(2, BossPlaceKind.Use, 500, 0), 10f), "any other place: where it stands");

            var places = new BossPlaces();
            var news = places.Update(new[] { cover }, new Vector2(0, 300), 10f, false);
            Equal(new BossPlaceNews(BossPlaceKind.Cover, string.Empty, new Vector2(540, 0)), news.Single(), "said with the place to stand");
            Equal(new Vector2(540, 0), places.Lead!.Value.At, "the beat leads behind it");
            Equal(0, places.Update(new[] { cover }, new Vector2(530, 0), 10f, false).Count, "once");
            Equal(true, places.Lead == null, "behind it: no more beat");
            places.Update(new[] { cover }, new Vector2(0, 300), 10f, false);
            Equal(true, places.Lead == null, "found once, it is not led to again");
            Equal(1, places.Update(new[] { Sight(3, BossPlaceKind.Cover, 0, 500) }, new Vector2(0, 300), 10f, false).Count, "another cover is news");
        }

        private static void TheMostPressingPlaceLeads()
        {
            var places = new BossPlaces();
            var player = new Vector2(0, 0);
            var sights = new[]
            {
                Sight(1, BossPlaceKind.Use, 100, 0, name: "Lever"), Sight(2, BossPlaceKind.Clone, 800, 0), Sight(3, BossPlaceKind.Refuge, 0, 900),
                Sight(4, BossPlaceKind.Refuge, 0, 400, active: false),
            };
            var news = places.Update(sights, player, 10f, false);
            Equal("Refuge,Clone,Use", string.Join(",", news.Select(line => line.Kind)), "each said, the most pressing first; a refuge that is not up is none");
            Equal("True,True,False", string.Join(",", news.Select(line => line.Pressing)), "the shelter and the clone press, the lever does not");
            Equal(new Vector2(0, 900), places.Lead!.Value.At, "the shelter first");
            Equal(BossPlaceKind.Refuge, places.LeadKind, "with the shelter's own sound");
            places.Update(sights, player, 10f, true);
            Equal(new Vector2(800, 0), places.Lead!.Value.At, "sheltered: the next kind");
            Equal(BossPlaceKind.Clone, places.LeadKind, "and the heartbeat again");
            Equal(0, places.Update(sights, player, 10f, false).Count, "nothing is said twice");
            places.Update(sights.Take(2).ToList(), player, 10f, false);
            Equal(1, places.Update(sights, player, 10f, false).Count, "a shelter that comes up again is said again");
        }

        private static void ALitStoneLeadsUntilItIsUsed()
        {
            const string Siora = "Metadata/Monsters/ReaperBoss/EzomyteReaperBoss";
            const string Rock = "Metadata/Terrain/Gallows/Act4_Interlude/Part1/P1_2/Objects/RuneRock_03";
            var catalog = new BossMechanicsCatalog();
            catalog.Add(new[] { $"boss|{Siora}|Siora, Blade of the Mists", $"activate|{Siora}|{Rock}|active=1", $"activate|{Siora}|Metadata/Bad|active" });
            Equal("Activate:active=1", string.Join(";", catalog.PlaceRules(Rock).Select(rule => $"{rule.Kind}:{rule.State}")), "the rock and the state that lights it");
            Equal(0, catalog.PlaceRules("Metadata/Bad").Count, "a state with no value is no rule");

            // Her rocks stand at active=2 once used; the one lit in her darkness is at 1, and using it plays activate (4), then 2.
            Equal(true, BossPlaces.StateIs(new[] { ("lightstrike", 0L), ("active", 1L) }, "active=1"), "lit");
            Equal(false, BossPlaces.StateIs(new[] { ("active", 2L) }, "active=1"), "used already");
            Equal(false, BossPlaces.StateIs(new[] { ("active", 4L) }, "active=1"), "being used");
            Equal(true, BossPlaces.StateIs(Array.Empty<(string, long)>(), "active=0"), "a state not listed is 0");
            Equal(false, BossPlaces.StateIs(new[] { ("active", 1L) }, "active"), "a rule with no value holds nothing");

            var places = new BossPlaces();
            var lit = new[] { Sight(9, BossPlaceKind.Activate, 0, 600, name: "Megalito rúnico") };
            var news = places.Update(lit, Vector2.Zero, 10f, true);
            Equal("Activate:True:Megalito rúnico", string.Join(";", news.Select(line => $"{line.Kind}:{line.Pressing}:{line.Name}")), "said at once, as a place to go to, by the game's name");
            Equal(new Vector2(0, 600), places.Lead!.Value.At, "led to, though no shelter buff is on the player");
            Equal(BossPlaceKind.Activate, places.LeadKind, "with the shelter's sound");
            places.Update(lit, new Vector2(0, 590), 10f, true);
            Equal(BossPlaceKind.Activate, places.LeadKind, "still led to when there: it must be used, not stood by");
            places.Update(new[] { Sight(9, BossPlaceKind.Activate, 0, 600, active: false) }, Vector2.Zero, 10f, false);
            Equal(null, places.LeadKind, "used: the lead ends");
        }

        private static void AClonesWayNamesWhereItStops()
        {
            const string Stem = "Metadata/Monsters/ClonePath";
            var catalog = Sample();
            Equal((Stem, ClonePathRole.Start), catalog.ClonePath(Stem + "Start"), "a way's start");
            Equal((Stem, ClonePathRole.Way), catalog.ClonePath(Stem), "its markers");
            Equal((Stem, ClonePathRole.End), catalog.ClonePath(Stem + "End"), "its end");
            Equal("GAOne,GSTwo", string.Join(",", catalog.AreaSkills(King)), "the boss's area skills");

            // Two ways from one start that part later, the markers in no order.
            ClonePathMarker Marker(uint id, ClonePathRole role, float x, float y) => new(id, Stem, role, new Vector2(x, y));
            var markers = new[]
            {
                Marker(13, ClonePathRole.End, 600, 0), Marker(10, ClonePathRole.Start, 0, 0), Marker(11, ClonePathRole.Way, 200, 0), Marker(12, ClonePathRole.Way, 400, 0),
                Marker(20, ClonePathRole.Start, 0, 0), Marker(21, ClonePathRole.Way, 200, 0), Marker(22, ClonePathRole.Way, 200, 200), Marker(23, ClonePathRole.End, 200, 600),
                Marker(30, ClonePathRole.Start, 0, 0),
            };
            var paths = new ClonePaths();
            void Seen(float x, float y, long now) => paths.Update(markers, new[] { (7u, new Vector2(x, y)) }, now);
            Seen(0, 0, 1000);
            Equal(2, paths.Count, "a start without an end is no way");
            Seen(150, 5, 1150);
            Equal(true, paths.EndOf(7) == null, "on the stretch the ways share: not known yet");
            Seen(205, 150, 1300);
            Equal(new Vector2(200, 600), paths.EndOf(7)!.Value, "where they part, the way that fits names the end");
            Equal(true, paths.EndOf(8) == null, "a clone never seen");
            Seen(205, 155, 1600);
            Equal(300L, paths.StoppedMs(7, 1600), "standing still since it last moved");
            Seen(400, 400, 1750);
            Equal(0L, paths.StoppedMs(7, 1750), "moving again");
            Equal(true, paths.EndOf(7) == null, "off every way: none is guessed");
            paths.Update(markers, Array.Empty<(uint, Vector2)>(), 1900);
            Equal(0L, paths.StoppedMs(7, 5000), "a clone that is gone is forgotten");

            // The game takes a way away as its clone sets off on it (Yama, 2026-11-25).
            paths.Update(markers.Skip(4).ToList(), new[] { (8u, Vector2.Zero) }, 2000);
            Equal(new Vector2(600, 0), paths.EndOf(8)!.Value, "the way that went as the clone came is its own, before it takes a step");
            paths.Update(markers.Skip(8).ToList(), new[] { (8u, new Vector2(100, 0)), (9u, Vector2.Zero) }, 2500);
            Equal(new Vector2(200, 600), paths.EndOf(9)!.Value, "the next clone's is the next that went");
            Equal(new Vector2(600, 0), paths.EndOf(8)!.Value, "and the first keeps its own");
            Equal("2/0", $"{paths.Count}/{paths.Laid}", "both are remembered, none is laid");
            Equal(true, paths.Walked(8) && !paths.Walked(9), "which have set off");
            paths.Update(Array.Empty<ClonePathMarker>(), Array.Empty<(uint, Vector2)>(), 9000);
            Equal(0, paths.Count, "a round that is over is forgotten");
        }

        private static void TheThrowsTargetIsTheNearestWithItsHelperAtWork()
        {
            // Torvian's arena, 2026-11-25: the scaffoldings and the priest by each.
            var (agility, attack, defence) = (new Vector2(28234, 19364), new Vector2(29114, 19592), new Vector2(29408, 18060));
            var targets = new[] { agility, attack, defence };
            (Vector2, bool)[] Priests(bool agilityBusy, bool attackBusy, bool defenceBusy) => new[]
            {
                (new Vector2(28212, 19408), agilityBusy), (new Vector2(29103, 19625), attackBusy), (new Vector2(29495, 18071), defenceBusy),
            };
            var throws = new BossThrows();
            var player = new Vector2(29000, 19100);
            Equal(true, throws.Pick(Priests(false, false, false), targets, player) == null, "no priest at work: nothing to hit");
            Equal(defence, throws.Pick(Priests(false, false, true), targets, player)!.Value, "the one at work, however far");
            Equal(attack, throws.Pick(Priests(true, true, true), targets, player)!.Value, "the nearest of those at work");
            Equal(attack, throws.Pick(Priests(true, true, true), targets, new Vector2(28650, 19450))!.Value, "kept while the other is hardly nearer");
            Equal(agility, throws.Pick(Priests(true, true, true), targets, new Vector2(28400, 19400))!.Value, "another that is clearly nearer");
            Equal(attack, throws.Pick(Priests(false, true, true), targets, new Vector2(28400, 19400))!.Value, "its priest stopped: the next");

            var boss = new Vector2(29114, 18900);
            Equal(true, BossThrows.LinedUp(boss, new Vector2(29114, 19200), attack), "between the boss and it");
            Equal(false, BossThrows.LinedUp(boss, new Vector2(29114, 18600), attack), "on the boss's other side");
            Equal(false, BossThrows.LinedUp(boss, new Vector2(29250, 19200), attack), "off the line");
            Equal(false, BossThrows.LinedUp(boss, new Vector2(29114, 20100), attack), "beyond it");
            Equal(false, BossThrows.LinedUp(boss, boss, attack), "upon the boss no line");
        }

        /// <summary>Benedictus's lines as the log of 2026-11-25 placed them; the fourth, past the log's cap, with one opening unmarked.</summary>
        private static List<MazeRock> Lines()
        {
            var rocks = new List<MazeRock>();
            void Wall(float x, float right, float left, params float[] plain)
            {
                rocks.Add(new MazeRock(new Vector2(x, right), false, true));
                rocks.Add(new MazeRock(new Vector2(x, left), true, false));
                rocks.AddRange(plain.Select(y => new MazeRock(new Vector2(x, y), false, false)));
            }

            Wall(23810, 21745, 21364, 22125, 21940, 21179, 20984, 20799);
            Wall(23505, 21560, 21179, 22125, 21940, 21745, 20984, 20799);
            Wall(23147, 21940, 21560, 22125, 21364, 21179, 20984, 20799);
            rocks.Add(new MazeRock(new Vector2(23516, 22125), false, false)); // a rock a cell off its wall
            rocks.AddRange(new[] { 22125f, 21940f, 21745f, 21560f, 21179f, 20984f, 20799f }.Select(y => new MazeRock(new Vector2(22897, y), false, false)));
            return rocks;
        }

        private static void AMazesLinesAreWalkedThroughTheirOpenings()
        {
            var rocks = Lines();
            var (forward, goal) = (new Vector2(-1, 0), new Vector2(22800, 21364));
            var player = new Vector2(24071, 21364);
            var openings = new List<float>();
            var walls = new List<int>();
            for (var step = 0; step < 400 && player.X > goal.X + 1; step++)
            {
                var next = MazeWay.Lines(rocks, player, forward, goal);
                if (next.Wall >= 0 && (walls.Count == 0 || walls[^1] != next.Wall))
                {
                    walls.Add(next.Wall);
                    openings.Add(next.At.Y);
                }

                var way = next.At - player;
                var before = player;
                player += way.Length() <= 10f ? way : Vector2.Normalize(way) * 10f;
                Equal(false, rocks.Any(rock => Vector2.Distance(rock.At, player) < 80f && Vector2.Distance(rock.At, player) < Vector2.Distance(rock.At, before)), $"walks into no rock at {player.X:0},{player.Y:0}");
                if (way.Length() < 0.5f)
                {
                    break;
                }
            }

            Equal("0,1,2,3", string.Join(",", walls), "every wall in turn");
            Equal("21554,21370,21750,21370", string.Join(",", openings.Select(y => MathF.Round(y))), "through the middle of each opening, the last its widest gap");
            Equal(true, player.X <= goal.X + 1, "out beyond the blast");
        }

        private static void AMazesRingsAreLeftRoundToTheirOpenings()
        {
            var centre = new Vector2(23342, 21364);
            var rocks = new List<MazeRock>();
            void Ring(float radius, int count, int open)
            {
                for (var k = 0; k < count; k++)
                {
                    if (k != open)
                    {
                        var (sin, cos) = MathF.SinCos(2f * MathF.PI * k / count);
                        rocks.Add(new MazeRock(centre + (new Vector2(cos, sin) * radius), k == (open + 1) % count, k == (open + count - 1) % count));
                    }
                }
            }

            Ring(400, 13, 3);
            Ring(650, 21, 14);
            var player = centre;
            var walls = new List<int>();
            for (var step = 0; step < 600 && MazeWay.Rings(rocks, player, centre) is { } next; step++)
            {
                if (walls.Count == 0 || walls[^1] != next.Wall)
                {
                    walls.Add(next.Wall);
                }

                var way = next.At - player;
                if (way.Length() < 0.5f)
                {
                    break;
                }

                var before = player;
                player += way.Length() <= 10f ? way : Vector2.Normalize(way) * 10f;
                Equal(false, rocks.Any(rock => Vector2.Distance(rock.At, player) < 70f && Vector2.Distance(rock.At, player) < Vector2.Distance(rock.At, before)), $"walks into no rock at {player.X - centre.X:0},{player.Y - centre.Y:0}");
            }

            Equal("0,1", string.Join(",", walls), "one ring, then the other");
            Equal(true, Vector2.Distance(player, centre) > 700, "past the last");
            Equal(true, MazeWay.Rings(Array.Empty<MazeRock>(), centre, centre) == null, "no rocks: no rings to leave");
        }

        private static void AMazesRoundIsToldByItsEffects()
        {
            const string Ao = "Metadata/Effects/Spells/monsters_effects/Act4_FOUR/TwilightOrderOverlord/ao/";
            var middle = new Vector2(23342, 21364);
            var round = new MazeRound();
            Equal(MazePhase.None, round.Phase(0), "no round yet");
            Equal(false, round.Drawn(Ao + "snake_attack_long.ao", middle, middle, 0), "another effect");
            Equal(true, round.Drawn(Ao + MazeRound.HoleEffect, middle, middle, 1000), "the first blast");
            Equal(MazePhase.Hole, round.Phase(6100), "until it has fallen: the hole");
            Equal(MazePhase.Rings, round.Phase(6400), "then out, round the middle out of rings");
            round.Drawn(Ao + MazeRound.RingEffect, Vector2.Zero, middle, 7400);
            Equal(middle, round.Centre, "an effect not placed yet keeps the centre");
            Equal(MazePhase.Rings, round.Phase(12500), "until the second has fallen");
            Equal(MazePhase.None, round.Phase(12800), "over");
            round.Drawn(Ao + MazeRound.HoleEffect, new Vector2(24071, 21364), middle, 49010);
            Equal(MazePhase.Lines, round.Phase(54411), "drawn at the arena's end: lines");
            round.Drawn(Ao + MazeRound.LineEffect, new Vector2(24505, 21408), middle, 55443);
            Equal(MazePhase.Lines, round.Phase(60400), "until it falls");
            Equal(MazePhase.None, round.Phase(60800), "over");
            round.Drawn(Ao + MazeRound.HoleEffect, middle, middle, 70000);
            Equal(MazePhase.None, round.Phase(79500), "with no second blast the round ends by itself");
        }

        private static void TheWayOutOfAMazeIsSaidAsItTurns()
        {
            static int Octant(Vector2 offset) => MathF.Abs(offset.X) >= MathF.Abs(offset.Y) ? (offset.X >= 0 ? 0 : 4) : (offset.Y >= 0 ? 2 : 6);
            var guide = new MazeExitGuide();
            Equal(new MazeExitNews(MazeExitNewsKind.First, 0, 3), guide.Update(new MazeStep(new Vector2(250, 0), 0), false, Vector2.Zero, Octant, 1000), "at the start");
            Equal(true, guide.Update(new MazeStep(new Vector2(250, 0), 0), false, new Vector2(50, 0), Octant, 1100) == null, "the same way is not said again");
            Equal(true, guide.Update(new MazeStep(new Vector2(0, 250), 0), false, new Vector2(50, 0), Octant, 1200) == null, "a turn too soon");
            Equal(new MazeExitNews(MazeExitNewsKind.Way, 2, 3), guide.Update(new MazeStep(new Vector2(0, 250), 0), false, Vector2.Zero, Octant, 1800), "the turn");
            Equal(MazeExitNewsKind.Way, guide.Update(new MazeStep(new Vector2(0, 250), 1), false, Vector2.Zero, Octant, 1900)!.Value.Kind, "the next wall at once");
            Equal(MazeExitNewsKind.Out, guide.Update(new MazeStep(Vector2.Zero, -1), true, Vector2.Zero, Octant, 2000)!.Value.Kind, "out");
            Equal(true, guide.Update(new MazeStep(Vector2.Zero, -1), true, Vector2.Zero, Octant, 2100) == null, "once");
        }

        private static void TheSafeSpotIsSaidUntilThePlayerIsInIt()
        {
            const float Hole = 228f;
            var guide = new SafeSpotGuide();
            static int Octant(Vector2 offset) => MathF.Abs(offset.X) >= MathF.Abs(offset.Y) ? (offset.X >= 0 ? 0 : 4) : (offset.Y >= 0 ? 2 : 6);
            SafeSpotNews? At(Vector2 spot, bool settled, Vector2 player, long now) => guide.Update(1, spot, settled, player, 20f, Hole, Octant, now);
            var safe = Hole - 20f - AttackAim.MarginWorld;

            Equal(new SafeSpotNews(SafeSpotNewsKind.First, 0, (int)MathF.Ceiling((600 - safe) / 100)), At(new Vector2(600, 0), false, Vector2.Zero, 1000), "marked: the way and the steps to the hole's edge");
            Equal(true, At(new Vector2(0, 600), false, Vector2.Zero, 2500) == null, "still walking: its turns are not said");
            Equal(SafeSpotNewsKind.Way, At(new Vector2(0, 600), true, Vector2.Zero, 2650)!.Value.Kind, "settled: the way again at once");
            Equal(true, At(new Vector2(0, 600), true, new Vector2(0, 100), 5000) == null, "the same way is not repeated: the beat tells it nears");
            Equal(new SafeSpotNews(SafeSpotNewsKind.Way, 4, (int)MathF.Ceiling((480 - safe) / 100)), At(new Vector2(0, 600), true, new Vector2(480, 600), 6000), "another way is");
            Equal(SafeSpotNewsKind.Inside, At(new Vector2(0, 600), true, new Vector2(safe - 1, 600), 6150)!.Value.Kind, "the whole body in the hole");
            Equal(true, At(new Vector2(0, 600), true, new Vector2(0, 600), 8000) == null, "once");
            Equal(SafeSpotNewsKind.Out, At(new Vector2(0, 600), true, new Vector2(300, 600), 8150)!.Value.Kind, "out of it: the way back, at once");

            // A run through it at 600 world units a second (Yama, 2026-11-25: 570 to 680).
            var run = new SafeSpotGuide();
            var kinds = new List<(SafeSpotNewsKind Kind, float At)>();
            for (var ms = 0; ms <= 1500; ms += 16)
            {
                var x = -600f + (0.6f * ms);
                if (run.Update(5, Vector2.Zero, true, new Vector2(x, 0), 20f, Hole, Octant, 1000 + ms) is { } word)
                {
                    kinds.Add((word.Kind, x));
                }
            }

            Equal("First,Inside,Out", string.Join(",", kinds.Select(kind => kind.Kind)), "running: the way, to stop, that they ran past");
            Equal(true, kinds[1].At < -safe && kinds[1].At > -safe - 170, "to stop is said before the edge, by the pace");
            Equal(true, kinds[2].At > safe && kinds[2].At < safe + 20, "past it is said at once");
            Equal(SafeSpotNewsKind.First, guide.Update(2, Vector2.Zero, false, new Vector2(300, 0), 20f, Hole, Octant, 8300)!.Value.Kind, "another clone starts anew");
            Equal(true, guide.Update(3, Vector2.Zero, true, Vector2.Zero, 20f, null, Octant, 8400) == null, "no hole in the files: being near promises nothing");
            Equal(true, SafeSpotGuide.LeadRadius(Hole, 20f) + 20f <= safe, "the lead's end is wholly inside");
            Equal("Safe spot: follow the heartbeat, up, 5 steps", Announcements.BossClone(true, 2, 5, true), "its first line");
            Equal("The true one, up, 5 steps", Announcements.BossClone(false, 2, 5, false), "without a hole or the heartbeat");
        }

        private static void TheShelterEndsOnceItStaysDown()
        {
            var end = new RefugeEnd();
            Equal(false, end.Update(false, 0), "never up: nothing ends");
            Equal(false, end.Update(true, 1000), "up");
            Equal(false, end.Update(false, 1300), "one read down is not the end");
            Equal(false, end.Update(true, 1500), "up again");
            Equal(false, end.Update(false, 1800), "down a moment");
            Equal(true, end.Update(false, 1500 + RefugeEnd.GoneMs), "down long enough: the player can move");
            Equal(false, end.Update(false, 5000), "said once");
            end.Update(true, 6000);
            end.Reset();
            Equal(false, end.Update(false, 9000), "a reset (the fight over) says nothing");
            Equal("Danger over, move freely", Announcements.BossRefugeOver(), "its line");
            Equal("Sheltered, stay until the all-clear", Announcements.BossSheltered(true), "in the shelter, told to stay");
            var chord = CueSynth.Refuge(44100);
            Equal((int)(0.3f * 44100), chord.Length, "the shelter's chord lasts 0.3 s, short enough to repeat");
            Equal(true, Math.Abs(chord.Max(v => Math.Abs(v)) - 0.85f) < 0.001f, "and loud enough to cut through a fight");
        }

        private static void AThingToUseIsToldOfOnceAndNeverLedTo()
        {
            var places = new BossPlaces();
            var levers = new[] { Sight(1, BossPlaceKind.Use, 900, 0, name: "Lever"), Sight(2, BossPlaceKind.Use, 300, 0, name: "Lever"), Sight(3, BossPlaceKind.Break, 50, 0) };
            var news = places.Update(levers, Vector2.Zero, 10f, false);
            Equal(new BossPlaceNews(BossPlaceKind.Use, "Lever", new Vector2(300, 0)), news.Single(), "the nearest of its name speaks for all of them; a nameless thing tells nothing");
            Equal(false, news.Single().Pressing, "told of, not a place to run to");
            Equal(true, places.Lead == null, "never led to: the files do not say whether using it helps");
            Equal(0, places.Update(levers, Vector2.Zero, 10f, false).Count, "once a fight");
            Equal(1, places.Update(new[] { Sight(4, BossPlaceKind.Break, 100, 100, name: "Lever") }, Vector2.Zero, 10f, false).Count, "the same name to break is another thing");
            Equal("Lever, can be used, right", Announcements.BossPlace(BossPlaceKind.Use, "Lever", 0, false), "its line");
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

        private static void AHelperWarnsAsItsDangerReachesThePlayer()
        {
            var warnings = new BossHelperWarnings();
            Equal(0, warnings.Update(new[] { (7u, false) }, 0).Count, "a storm far off");
            Equal(7u, warnings.Update(new[] { (7u, true) }, 300).Single(), "upon the player");
            Equal(0, warnings.Update(new[] { (7u, true) }, 600).Count, "once while it stays");
            warnings.Update(new[] { (7u, false) }, 900);
            Equal(0, warnings.Update(new[] { (7u, true) }, 1200).Count, "the edge of its area is the same danger");
            warnings.Update(new[] { (7u, false) }, 1500);
            Equal(1, warnings.Update(new[] { (7u, true) }, 1200 + BossHelperWarnings.RearmMs).Count, "back after the player was clear of it");
            Equal(2, warnings.Update(new[] { (8u, true), (9u, true) }, 5000).Count, "each helper its own warning");
            warnings.Update(Array.Empty<(uint, bool)>(), 5300);
            Equal(1, warnings.Update(new[] { (8u, true) }, 5600).Count, "one that left and came again is new");

            var (helper, player) = (new Vector2(100, 100), new Vector2(100, 400));
            Equal(helper, BossHelperWarnings.AimOf(new AttackArea(AttackAreas.Circle, 10f, 0f, 0f, 0f, 0), helper, player), "an area around the helper lies where it stands");
            Equal(player, BossHelperWarnings.AimOf(new AttackArea(AttackAreas.Rectangle, 10f, 4f, 0f, 0f, 0), helper, player), "a line points at the player it walks toward");
        }

        /// <summary>The catalog as shipped, against what the play log of 2026-11-12 saw in Zalmarath's and Jamanra's arenas (docs/research/boss-mechanics.md §16).</summary>
        private static void TheShippedCatalogHoldsTheFightsThePlayLogMet()
        {
            var directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", BossMechanicsCatalog.FileName)))
            {
                directory = System.IO.Path.GetDirectoryName(directory) ?? throw new System.IO.FileNotFoundException("boss-mechanics.txt is not under any folder above the tests");
            }

            var catalog = BossMechanicsCatalog.Load(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog"));
            Equal(BossPlaceKind.Cover, catalog.PlaceRules(Stalagmite).First(rule => rule.Boss == Titan).Kind, "Zalmarath's sword is a cover");
            Equal(true, catalog.DaemonSkills(Titan, "Metadata/Monsters/InvisibleFire/MDTitanBossLaserTarget").Count > 0, "his laser target strikes for him, with its own skill");
            Equal(true, catalog.IsDaemon("Metadata/Monsters/PerennialKingHumanBoss", "Metadata/Monsters/InvisibleFire/MDPerennialKingObsidianStorm_"), "Jamanra's obsidian storm");
            var asala = catalog.PlaceRules("Metadata/Monsters/NPC/MarakethMatriarchDreadnought_");
            Equal((BossPlaceKind.Refuge, "sandstorm_defence"), (asala[0].Kind, asala[0].State), "Asala shelters while her state is set");
            Equal(true, catalog.IsShelter("asala_sand_shield"), "her shield on the player");
            Equal(true, catalog.BuffText("guard_boss_utility_buff", "es") is { Name.Length: > 0, Description.Length: > 0 }, "Torvian's protection in the game's Spanish");
            Equal(false, catalog.BuffText("guard_boss_utility_buff", "es")!.Value.Description.Contains('['), "without the game's links");
            Equal(true, catalog.IsWall("Metadata/Monsters/WifeMonster/WifeMonster", "Metadata/Monsters/WifeMonster/WifeMonsterBoneWall"), "Asinia's walls");
            const string Yama = "Metadata/Monsters/YamaBoss/YamaBoss";
            Equal(ClonePathRole.End, catalog.ClonePath("Metadata/Monsters/YamaBoss/objects/YamaFutureClonePathEnd")!.Value.Role, "where Yama's clones stop");
            Equal(true, catalog.AreaSkills(Yama).Contains("GSYamaTimeFreezeSlam"), "his blow with the hole");
            Equal(true, catalog.Places > 200 && catalog.Daemons > 40, "every fight's places and helpers");

            // A monster the reader ignores is never typed: Jamanra's King's Attendants were listed as objects to use (issue #7).
            var ignored = new GameReader.Settings.State().MonstersPathsToIgnore;
            Equal(true, catalog.IsAdd(King, "Metadata/Monsters/PerennialKingStage2/PerennialKingCorpseFriend1"), "the King's Attendants are Jamanra's adds");
            Equal(string.Empty, string.Join(", ", catalog.AddPaths.Where(add => ignored.Any(prefix => add.StartsWith(prefix, StringComparison.Ordinal)))),
                "no boss's add is a monster the reader ignores");
        }

        private static void PlaceLinesNameThePlaceAndTheWay()
        {
            Equal("Shelter at Asala: follow the heartbeat, up", Announcements.BossPlace(BossPlaceKind.Refuge, "Asala", 2, true), "a named shelter");
            Equal("Cover, left", Announcements.BossPlace(BossPlaceKind.Cover, string.Empty, 4, false), "without the heartbeat");
            Equal("Lever, can be used: follow the heartbeat, right", Announcements.BossPlace(BossPlaceKind.Use, "Lever", 0, true), "a thing to use");
            Equal("Something to break, down", Announcements.BossPlace(BossPlaceKind.Break, string.Empty, 6, false), "a nameless thing to break");
            Equal("The boss: Charged Tornado. The tornadoes are empowering the King", Announcements.BossEmpowered(null, "Charged Tornado. The tornadoes are empowering the King"), "the game's words");
            Equal("Jamanra is empowered", Announcements.BossEmpowered("Jamanra", string.Empty), "no words: that it is");
            Equal("The boss is no longer empowered", Announcements.BossEmpowerEnded(null), "its end, for a buff the game does not name");
            Equal("Progress 25 percent", Announcements.BossProgress(2, 8), "a share of the top");
            Equal("Progress 6", Announcements.BossProgress(6, null), "a value");
            Equal("Sandstorm", Announcements.BossArena("sandstorm"), "an arena's kind");
            Equal("The boss retreats", Announcements.BossArena("retreat"), "a retreat");
            Equal("The arena changes", Announcements.BossArena("change"), "any other kind");
        }
    }
}
