namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The class resources derived from the game's tables and how their changes are told (docs/research/class-mechanics.md).</summary>
    public static class ClassMechanicTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ResourcesPairCurrentWithItsMaxima), ResourcesPairCurrentWithItsMaxima),
            (nameof(LimitsPairActiveWithAllowed), LimitsPairActiveWithAllowed),
            (nameof(HiddenBuffNamesAreNotShown), HiddenBuffNamesAreNotShown),
            (nameof(CatalogParsesAndFallsBackToEnglish), CatalogParsesAndFallsBackToEnglish),
            (nameof(CountedResourcesSettleBeforeTheyAreSaid), CountedResourcesSettleBeforeTheyAreSaid),
            (nameof(OneBuffFromTwoSkillsIsOneCount), OneBuffFromTwoSkillsIsOneCount),
            (nameof(PoolsTellOnlyTheirEdges), PoolsTellOnlyTheirEdges),
            (nameof(TheFirstReadingIsOnlyRemembered), TheFirstReadingIsOnlyRemembered),
            (nameof(AMissingResourceEnds), AMissingResourceEnds),
            (nameof(UnknownNamesBecomeWords), UnknownNamesBecomeWords),
            (nameof(ChargeWatchCoversItsOwnStats), ChargeWatchCoversItsOwnStats),
            (nameof(ASkillLevelsMaximumIsFoundByItsStatName), ASkillLevelsMaximumIsFoundByItsStatName),
        };

        private static readonly string[] Stats =
        {
            "level", "current_rage", "maximum_rage", "virtual_maximum_rage", "current_infernal_flame", "maximum_infernal_flame",
            "current_demon_form_stacks", "max_demon_form_stacks", "current_focus_lost", "number_of_active_totems", "number_of_totems_allowed",
            "base_number_of_totems_allowed", "number_of_active_skeletons", "number_of_skeletons_allowed", "number_of_active_relics", "current_",
        };

        private static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        private static void ASkillLevelsMaximumIsFoundByItsStatName()
        {
            // Rapid Shot's level 5 row (GrantedEffectStatSetsPerLevel 8689): one additional stat, the maximum Heat, 30.
            var names = ClassMechanicCatalog.MaximumNames("number_of_crossbow_heat_stacks").ToList();
            Expect("maximum_number_of_crossbow_heat_stacks", names[0], "the extractor's pairing prefix first");
            Expect<int?>(30, ClassMechanicCatalog.StatValue(new[] { "maximum_number_of_crossbow_heat_stacks" }, new[] { 30 }, names), "Rapid Shot at level 5");
            Expect<int?>(45, ClassMechanicCatalog.StatValue(new[] { "base_skill_effect_duration", null, "maximum_number_of_crossbow_heat_stacks" }, new[] { 6600, 3, 45 }, names),
                "paired by place, an unread name skipped");
            Expect<int?>(null, ClassMechanicCatalog.StatValue(new[] { "maximum_number_of_crossbow_heat_stacks" }, new[] { 30, 1 }, names), "lists that do not pair give nothing");
            Expect<int?>(null, ClassMechanicCatalog.StatValue(new[] { "maximum_rage" }, new[] { 30 }, names), "another resource's maximum is not this one");
        }

        private static void ResourcesPairCurrentWithItsMaxima()
        {
            var resources = ClassMechanics.Resources(Stats);
            var rage = resources.Single(resource => resource.Name == "rage");
            Expect(2, rage.Current, "key = row + 1");
            Expect(3, rage.Maximum, "maximum_rage");
            Expect(4, rage.VirtualMaximum, "virtual_maximum_rage");
            var demon = resources.Single(resource => resource.Name == "demon_form_stacks");
            Expect(8, demon.Maximum, "max_ partner");
            Expect(0, demon.VirtualMaximum, "no virtual maximum");
            var focus = resources.Single(resource => resource.Name == "focus_lost");
            Expect(0, focus.Maximum, "a lone current is kept with no maximum");
            Expect(false, resources.Any(resource => resource.Name.Length == 0), "a bare 'current_' is not a resource");
        }

        private static void LimitsPairActiveWithAllowed()
        {
            var limits = ClassMechanics.Limits(Stats);
            Expect(2, limits.Count, "totems and skeletons; relics have no allowance");
            Expect("totems", limits[0].Name, "table order");
            Expect(10, limits[0].Active, "number_of_active_totems key");
            Expect(11, limits[0].Allowed, "number_of_totems_allowed beats base_");
        }

        private static void HiddenBuffNamesAreNotShown()
        {
            Expect(true, ClassMechanics.IsShownName("Jade"), "a plain name");
            Expect(false, ClassMechanics.IsShownName("[Not Displayed] Tempest Bell"), "not displayed");
            Expect(false, ClassMechanics.IsShownName(" [DNT-UNUSED] Donut"), "do not translate");
            Expect(false, ClassMechanics.IsShownName(" "), "blank");
        }

        private const string Json = """
            {"stats":[{"name":"infernal_flame","current":5,"maximum":6,"virtualMaximum":0},{"name":"","current":1,"maximum":0,"virtualMaximum":0}],
             "limits":[{"name":"totems","active":7,"allowed":8}],
             "buffs":{"jade_stacks":{"type":1,"names":{"en":"Jade","es":"Jade"}},"tailwind":{"type":1,"names":{"en":"Tailwind"}},"flask_effect_life":{"type":4,"names":{"en":"Healing"}}},
             "skills":{"WarBannerPlayer":["Attack","Banner"],"ExplosiveGrenadePlayer":["Grenade"]}}
            """;

        private static void CatalogParsesAndFallsBackToEnglish()
        {
            var catalog = ClassMechanicCatalog.Parse(Json);
            Expect(1, catalog.Resources.Count, "a nameless stat is dropped");
            Expect("infernal_flame", catalog.Resources[0].Name, "the resource");
            Expect(1, catalog.Limits.Count, "the limit");
            Expect("Jade", catalog.BuffName("jade_stacks_79", "es"), "the instance suffix is cut, Spanish found");
            Expect("Tailwind", catalog.BuffName("tailwind", "es"), "English when the language has none");
            Expect(string.Empty, catalog.BuffName("unknown", "en"), "an unknown buff has no name");
            Expect(-1, catalog.BuffType("unknown"), "an unknown buff has no type");
            Expect(ClassMechanicCatalog.FlaskBuffType, catalog.BuffType("flask_effect_life"), "the flask type");
            Expect(true, catalog.SkillHasType("WarBannerPlayer", "Banner"), "a banner skill");
            Expect(false, catalog.SkillHasType("WarBannerPlayer", "Grenade"), "not a grenade");
            Expect(0, catalog.SkillTypes("DodgeRollPlayer").Count, "an unlisted skill has no types");
            Expect(0, ClassMechanicCatalog.Load(System.IO.Path.Join(System.IO.Path.GetTempPath(), "no-such-class-mechanics.json")).Resources.Count, "a missing catalog is empty");
        }

        private static ResourceTracker.Reading Counted(string key, int count, int max = 0) => new(key, count, max, ResourceMode.Counted);

        private static void OneBuffFromTwoSkillsIsOneCount()
        {
            var tracker = new ResourceTracker();
            tracker.Update(new[] { Counted("buff:jade", 0) }, 0);
            var news = new List<ResourceTracker.News>();
            for (var now = 3000L; now <= 3000 + ResourceTracker.SettleMs + 500; now += 100)
            {
                // Two instances of one buff (two skills' gems) share the key: their readings alternated and never settled.
                news.AddRange(tracker.Update(new[] { Counted("buff:jade", 3), Counted("buff:jade", 1) }, now));
            }

            Expect(1, news.Count, "said once, settled");
            Expect(4, news[0].Count, "the stacks added up");
        }

        private static void CountedResourcesSettleBeforeTheyAreSaid()
        {
            var tracker = new ResourceTracker();
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 0) }, 0).Count, "baseline");
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 1) }, 100).Count, "within the grace: still the baseline");
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 1) }, 100 + ResourceTracker.SettleMs).Count, "a baseline reading is never said");
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 3) }, 3000).Count, "three held, not settled");
            var news = tracker.Update(new[] { Counted("buff:jade", 3) }, 3000 + ResourceTracker.SettleMs);
            Expect(1, news.Count, "said once settled");
            Expect(ResourceChange.Count, news[0].Change, "a count");
            Expect(3, news[0].Count, "the count held");
            news = tracker.Update(new[] { Counted("buff:jade", 3), Counted("buff:unbound", 1) }, 5000);
            Expect(0, news.Count, "a new buff waits to settle");
            news = tracker.Update(new[] { Counted("buff:jade", 3), Counted("buff:unbound", 1) }, 5000 + ResourceTracker.SettleMs);
            Expect(ResourceChange.Begun, news.Single().Change, "one from nothing begins");
            Expect(true, tracker.IsHeld("buff:unbound"), "held");
            Expect(0, tracker.Update(Array.Empty<ResourceTracker.Reading>(), 6000).Count, "gone, not settled");
            news = tracker.Update(Array.Empty<ResourceTracker.Reading>(), 6000 + ResourceTracker.SettleMs);
            Expect(2, news.Count, "both ended");
            Expect(true, news.All(item => item.Change == ResourceChange.Ended), "ended");
            Expect(0, tracker.Held.Count(), "nothing held");
        }

        private static void PoolsTellOnlyTheirEdges()
        {
            var tracker = new ResourceTracker();
            ResourceTracker.Reading Pool(int count) => new("stat:infernal_flame", count, 400, ResourceTracker.ModeFor(400));
            Expect(ResourceMode.Edges, ResourceTracker.ModeFor(400), "a large maximum is a pool");
            Expect(ResourceMode.Counted, ResourceTracker.ModeFor(ResourceTracker.CountedMax), "a small one is counted");
            tracker.Update(new[] { Pool(0) }, 0);
            Expect(0, tracker.Update(new[] { Pool(120) }, 3000).Count, "rising is not news");
            Expect(0, tracker.Update(new[] { Pool(120) }, 3000 + ResourceTracker.SettleMs).Count, "nor settled");
            var news = tracker.Update(new[] { Pool(400) }, 3700);
            Expect(ResourceChange.Full, news.Single().Change, "full at once");
            Expect(0, tracker.Update(new[] { Pool(400) }, 3800).Count, "staying full is not news");
            Expect(0, tracker.Update(new[] { Pool(10) }, 3900).Count, "falling is not news");
            Expect(ResourceChange.Empty, tracker.Update(new[] { Pool(0) }, 4000).Single().Change, "empty at once");
        }

        private static void TheFirstReadingIsOnlyRemembered()
        {
            var tracker = new ResourceTracker();
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 7), new ResourceTracker.Reading("limit:totems", 3, 3, ResourceMode.Edges) }, 0).Count, "nothing on entering");
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 7), new ResourceTracker.Reading("limit:totems", 3, 3, ResourceMode.Edges) }, 5000).Count, "nothing while unchanged");
            Expect(2, tracker.Held.Count(), "both remembered");
            tracker.Reset();
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 2) }, 6000).Count, "a reset makes the next reading the baseline");
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 5) }, 6000 + ResourceTracker.SettleMs).Count, "and the grace after it");
            Expect(0, tracker.Update(new[] { Counted("buff:jade", 5) }, 6000 + ResourceTracker.BaselineGraceMs + ResourceTracker.SettleMs).Count, "the grace's last reading is the baseline");
        }

        private static void AMissingResourceEnds()
        {
            var tracker = new ResourceTracker();
            tracker.Update(new[] { new ResourceTracker.Reading("limit:totems", 1, 3, ResourceMode.Edges) }, 0);
            Expect(ResourceChange.Full, tracker.Update(new[] { new ResourceTracker.Reading("limit:totems", 3, 3, ResourceMode.Edges) }, 3000).Single().Change, "all totems placed");
            Expect(ResourceChange.Empty, tracker.Update(Array.Empty<ResourceTracker.Reading>(), 3100).Single().Change, "a limit no longer read is empty");
        }

        private static void UnknownNamesBecomeWords()
        {
            Localization.Detect("en");
            Expect("Infernal Flame", Announcements.ResourceName("infernal_flame"), "a known stat");
            Expect("Blood scythe charges", Announcements.ResourceName("blood_scythe_charges"), "an unknown stat as words");
            Expect("Replicate minions", Announcements.LimitName("replicate_minions"), "an unknown limit as words");
            Expect("Jade: 7", Announcements.ResourceCount("Jade", 7, 0), "a count without a maximum");
            Localization.Detect("es");
            Expect("Tótems", Announcements.LimitName("totems"), "a known limit");
            Expect("Ofrendas", Announcements.LimitName("bone_offerings"), "offerings of any kind");
            Expect("Calor 3 de 5", Announcements.ResourceCount("Calor", 3, 5), "a count with its maximum");
            Localization.Detect("en");
        }

        private static void ChargeWatchCoversItsOwnStats()
        {
            Expect(true, PlayerCharges.CoversStat("rage"), "Rage is the charge watch's");
            Expect(true, PlayerCharges.CoversStat("power_charges"), "so are the charges, as buffs");
            Expect(false, PlayerCharges.CoversStat("infernal_flame"), "the rest is the resource watch's");
        }
    }
}
