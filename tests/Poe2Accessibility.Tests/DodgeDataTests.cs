namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The dodge warning's kinds from the game's data, the name fallback behind them, and the per-patch coverage report.</summary>
    public static class DodgeDataTests
    {
        private const string Golem = "Metadata/Monsters/MudGolem/MudGolemWetEncased1";
        private const string Miller = "Metadata/Monsters/SwollenMiller/SwollenMillerBoss";

        private static readonly string[] HitTimes =
        {
            "# comment",
            $"{Golem}|1=661/1500;melee",
            $"{Golem}|13=1169/2333;melee,noblock",
            $"{Golem}|40=-;calm",
            $"{Golem}|50=900/2000",
            $"{Miller}|135=-;ranged,noblock,noparry",
            $"{Miller}|153=-;calm",
            $"{Miller}|33=2042/3600/1800;ranged",
        };

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(KindsComeFromTheFileFlags), KindsComeFromTheFileFlags),
            (nameof(TheBasicAttackOfABowOrCrossbowMonsterIsAShot), TheBasicAttackOfABowOrCrossbowMonsterIsAShot),
            (nameof(ACalmActionOfTheDataIsNoThreatWhateverItsName), ACalmActionOfTheDataIsNoThreatWhateverItsName),
            (nameof(AnAttackTheDataIsMuteAboutHasNoKind), AnAttackTheDataIsMuteAboutHasNoKind),
            (nameof(AnAnimationWithoutANameStaysQuiet), AnAnimationWithoutANameStaysQuiet),
            (nameof(CoverageCountsEachMonstersAttacks), CoverageCountsEachMonstersAttacks),
            (nameof(BossesAreMarkedInTheirPath), BossesAreMarkedInTheirPath),
            (nameof(TheNewsReportSaysWhatCoversEachKey), TheNewsReportSaysWhatCoversEachKey),
            (nameof(TheNewsSummaryCountsTheUnclassified), TheNewsSummaryCountsTheUnclassified),
            (nameof(NotesClassifyMonstersBuffsAreasIconsAndScreens), NotesClassifyMonstersBuffsAreasIconsAndScreens),
            (nameof(ALeapIsLearntFromWhereItCarriesItsUser), ALeapIsLearntFromWhereItCarriesItsUser),
        };

        /// <summary>
        ///     The urchins' leap (Clearfell, 2026-11-08): from 327 units away it landed 110 from the place it aimed at, 74 from the
        ///     player; seen twice, it is a leap, kept with the timings and read back by older lines as before.
        /// </summary>
        private static void ALeapIsLearntFromWhereItCarriesItsUser()
        {
            const string leap = "Metadata/Monsters/Urchins/MeleeUrchin1|106";
            var start = new System.Numerics.Vector2(0, 0);
            var landed = new System.Numerics.Vector2(400, 0);
            var aimed = new System.Numerics.Vector2(470, 30);
            Equal(true, DodgeTimings.MovedToward(start, landed, aimed), "carried most of the way toward its aim");
            Equal(false, DodgeTimings.MovedToward(start, new System.Numerics.Vector2(40, 0), aimed), "a step is no leap");
            Equal(false, DodgeTimings.MovedToward(start, new System.Numerics.Vector2(-300, 0), aimed), "knocked back, away from its aim");
            Equal(false, DodgeTimings.MovedToward(start, new System.Numerics.Vector2(0, 300), aimed), "sideways is no leap");

            var timings = new DodgeTimings();
            Equal(false, timings.LearnMove(leap, start, landed, aimed), "once could be a knock or a pull");
            Equal(false, timings.Moves(leap), "not yet a leap");
            Equal(true, timings.LearnMove(leap, start, landed, aimed), "twice: a leap");
            Equal(true, timings.Moves(leap), "judged from its landing");
            Equal(false, timings.Moves("Metadata/Monsters/Urchins/MeleeUrchin1|1"), "its other attacks are not");

            timings.Learn(leap, 300);
            var lines = timings.ToLines().ToList();
            Equal(leap + "=300,moves", string.Join("|", lines.Where(line => line.StartsWith(leap, StringComparison.Ordinal))), "kept with its delays");
            var read = DodgeTimings.FromLines(lines);
            Equal(true, read.Moves(leap), "read back");
            Equal(300, read.Delay(leap), "its delay read back beside it");
            Equal(true, DodgeTimings.FromLines(new[] { "Metadata/Monsters/Urchins/MeleeUrchin1|1=moves" }).Moves("Metadata/Monsters/Urchins/MeleeUrchin1|1"), "a leap with no delay yet");
        }

        private static DodgeTimings Timings()
        {
            var timings = new DodgeTimings();
            timings.AddHitTimes(HitTimes);
            return timings;
        }

        private static void KindsComeFromTheFileFlags()
        {
            var timings = Timings();
            Equal(DodgeThreat.Melee, timings.Threat($"{Golem}|1"), "the weapon blow");
            Equal(DodgeThreat.Melee, timings.Threat($"{Golem}|13"), "a kind beside a block flag");
            Equal(false, timings.CanBlock($"{Golem}|13"), "the flag still read");
            Equal(DodgeThreat.Ranged, timings.Threat($"{Miller}|135"), "an untimed charge keeps its kind");
            Equal(DodgeThreat.Ranged, timings.Threat($"{Miller}|33"), "a projectile with its speed");
            Equal(1800, timings.ProjectileSpeed($"{Miller}|33"), "the speed still read");
            Equal(1169, timings.Delay($"{Golem}|13"), "the hit time still read");
            Equal(6, timings.FileKinds, "every kind token counted");
        }

        private static void ACalmActionOfTheDataIsNoThreatWhateverItsName()
        {
            var timings = Timings();
            Equal(DodgeThreat.None, timings.Threat($"{Golem}|40"), "a summon's contact is no blow");
            Equal(null, timings.Delay($"{Golem}|40"), "and it has no time");
            Equal(DodgeThreat.None, timings.Threat($"{Miller}|153"), "the Miller's zombie summon");
        }

        private static void AnAttackTheDataIsMuteAboutHasNoKind()
        {
            var timings = Timings();
            Equal(null, timings.Threat($"{Golem}|50"), "timed by the files, its kind left to the name");
            Equal(900, timings.Delay($"{Golem}|50"), "the time stays");
            Equal(null, timings.Threat($"{Golem}|99"), "an attack the file lacks");
        }

        private static void AnAnimationWithoutANameStaysQuiet()
        {
            Equal(DodgeThreat.None, DodgeThreats.Of(99, _ => null), "an id the table lacks: a false warning costs more than a missed one");
            Equal(DodgeThreat.Melee, DodgeThreats.Of(13, _ => "GroundSlam"), "a named slam");
        }

        private static void CoverageCountsEachMonstersAttacks()
        {
            var coverage = DodgeCoverage.Parse(HitTimes);
            Equal(2, coverage.Monsters, "two monsters");
            var golem = coverage.Of($"{Golem}@3")!;
            Equal(4, golem.Attacks, "the golem's lines");
            Equal(2, golem.Kinded, "two kinds from the data");
            Equal(3, golem.Timed, "three with a hit time");
            Equal(1, golem.Calm, "one calm");
            Equal(1, golem.ByName, "one left to the name");
            Equal("4 attacks: 3 timed, 2 kinds from the data, 1 calm, 1 from the name", golem.ToString(), "the news line");
            Equal(null, coverage.Of("Metadata/Monsters/Nobody"), "a monster the file lacks");
        }

        private static void BossesAreMarkedInTheirPath()
        {
            Equal(true, DodgeCoverage.IsBoss(Miller), "the Miller");
            Equal(false, DodgeCoverage.IsBoss(Golem), "a golem");
        }

        private static GameNewsNotes Notes() => new(
            DodgeCoverage.Parse(HitTimes),
            new HashSet<string>(StringComparer.Ordinal) { "G1_1" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "metadata/ui/ingamestate/tradewindow.ui" },
            id => id == "mud_patch_zone");

        private static void TheNewsReportSaysWhatCoversEachKey()
        {
            var news = GameNews.Compare(
                new[] { "monstervarieties\tMetadata/Monsters/Old", "buffdefinitions\told_buff", "worldareas\tG1_1" },
                new[] { $"monstervarieties\t{Miller}", "monstervarieties\tMetadata/Monsters/Old", "monstervarieties\tMetadata/Monsters/New/NewOne", "buffdefinitions\told_buff", "buffdefinitions\tmud_patch_zone", "buffdefinitions\tweird_aura", "worldareas\tG1_1", "worldareas\tG1_9" });
            var lines = GameNews.Report(news, Notes().Of).ToList();
            Equal($"monstervarieties: 2 new, 0 gone; 1 unclassified", lines[0], "the monsters' line");
            Equal($"  + {Miller} (boss; named by the game; dodge: 3 attacks: 1 timed, 2 kinds from the data, 1 calm)", lines[1], "a boss with data");
            Equal("  + Metadata/Monsters/New/NewOne (named by the game; no attack data)", lines[2], "a monster without attack data");
            Equal("buffdefinitions: 2 new, 0 gone; 1 unclassified", lines[3], "the buffs' line");
            Equal("  + mud_patch_zone (harmful ground)", lines[4], "a hazard");
            Equal("  + weird_aura (no rule names it; said by the game's name only)", lines[5], "an unknown buff");
            Equal("worldareas: 1 new, 0 gone; 1 unclassified", lines[6], "the areas' line");
            Equal("  + G1_9 (no name in area-names)", lines[7], "an area without a name");
        }

        private static void TheNewsSummaryCountsTheUnclassified()
        {
            var news = GameNews.Compare(
                new[] { "worldareas\tG1_1", "chests\tOld" },
                new[] { "worldareas\tG1_1", "chests\tOld", "chests\tNewChest", "worldareas\tG1_2" });
            var summary = GameNews.Summary(news, (table, key) => table == "worldareas" ? new NewsNote("named", true) : null);
            Equal("worldareas 1 0, chests 1 1", string.Join(", ", summary.Select(item => $"{item.Table} {item.Added} {item.Unclassified}")), "areas covered, chests not");
        }

        private static void NotesClassifyMonstersBuffsAreasIconsAndScreens()
        {
            var notes = Notes();
            Equal(true, notes.Of("buffdefinitions", "additional_ignite")?.Covered, "an ailment rule");
            Equal(false, notes.Of("buffdefinitions", "some_new_thing")?.Covered, "a buff no rule names");
            Equal(true, notes.Of("worldareas", "G1_1")?.Covered, "a named area");
            Equal(true, notes.Of("minimapicons", "RitualRune")?.Covered, "a ritual altar icon");
            Equal(true, notes.Of("screens", "metadata/ui/ingamestate/tradewindow.ui")?.Covered, "a catalogued screen");
            Equal(false, notes.Of("screens", "metadata/ui/ingamestate/brandnew.ui")?.Covered, "a screen the catalog lacks");
            Equal(true, notes.Of("animation", "SpellBuff")?.Text.StartsWith("the name reads as None", StringComparison.Ordinal), "an animation's name reading");
            Equal(null, notes.Of("chests", "Metadata/Chests/New"), "a table nothing classifies");
        }

        /// <summary>
        ///     The shipped hit times: a Decrepit Mercenary's basic attack is its crossbow's bolt (it hit from 400-585 units in Ogham
        ///     Farmlands, 2026-11-08, while the melee reach gate held 14 of 24 shots back); an urchin's stays a blow.
        /// </summary>
        private static void TheBasicAttackOfABowOrCrossbowMonsterIsAShot()
        {
            if (ShippedHitTimes() is not { } file)
            {
                return; // away from the repository
            }

            var timings = DodgeTimings.Load(file, string.Empty, string.Empty);
            Equal(DodgeThreat.Ranged, timings.Threat("Metadata/Monsters/Mercenary/Infected/InfectedMercenaryCrossbow|1"), "the crossbow mercenary's basic attack");
            Equal(DodgeThreat.Ranged, timings.Threat("Metadata/Monsters/Skeletons/Basic/GraveSkeletonBow|1"), "an Act 1 skeleton archer's basic attack");
            Equal(DodgeThreat.Melee, timings.Threat("Metadata/Monsters/Urchins/MeleeUrchin1|1"), "an urchin's basic attack");
        }

        private static string? ShippedHitTimes()
        {
            for (var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = System.IO.Path.Combine(folder.FullName, "Poe2Accessibility", "catalog", "hit-times.txt");
                if (System.IO.File.Exists(file))
                {
                    return file;
                }
            }

            return null;
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
            }
        }
    }
}
