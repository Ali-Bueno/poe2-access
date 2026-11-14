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

    /// <summary>Dodge timing, build files, passive-tree paths, object sounds, settings menu and news delivery.</summary>
    public static class PassiveAndMenuTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(DodgeTimingIsLearntFromBlows), DodgeTimingIsLearntFromBlows),
            (nameof(ProjectileWarningsAddTheFlight), ProjectileWarningsAddTheFlight),
            (nameof(BuildFilesAreReadWithTheirNotes), BuildFilesAreReadWithTheirNotes),
            (nameof(MinionsAreCountedByKind), MinionsAreCountedByKind),
            (nameof(PassiveStepsCountThePointsAlongTheLinks), PassiveStepsCountThePointsAlongTheLinks),
            (nameof(ANodeThatHoldsNoOtherCanBeRefunded), ANodeThatHoldsNoOtherCanBeRefunded),
            (nameof(PassiveChoicesAreReadWithTheirNodes), PassiveChoicesAreReadWithTheirNodes),
            (nameof(PassiveZoneNamesTheSideAndRing), PassiveZoneNamesTheSideAndRing),
            (nameof(PassiveFinderWalksItsLists), PassiveFinderWalksItsLists),
            (nameof(TheTreeRemembersWhereEachCharacterLeftOff), TheTreeRemembersWhereEachCharacterLeftOff),
            (nameof(APassiveTrailTakesTheFewestPointsAlongTheBuild), APassiveTrailTakesTheFewestPointsAlongTheBuild),
            (nameof(ObjectSoundsDieAwayPastTheirRange), ObjectSoundsDieAwayPastTheirRange),
            (nameof(SettingsMenuOpensGroupsAndGoesBack), SettingsMenuOpensGroupsAndGoesBack),
            (nameof(NewsFollowItsDeliveryLevel), NewsFollowItsDeliveryLevel),
            (nameof(StashOpenedOnInventoryNamesTheInventory), StashOpenedOnInventoryNamesTheInventory),
            (nameof(WallTonesSwellNearWalls), WallTonesSwellNearWalls),
        };

        private static void DodgeTimingIsLearntFromBlows()
        {
            var timings = new DodgeTimings();
            var key = DodgeTimings.Key("Metadata/Monsters/MudGolem/MudGolemWet1@3", 13);
            Equal("Metadata/Monsters/MudGolem/MudGolemWet1|13", key, "the kind without its level");
            Equal(true, timings.Delay(key) == null, "unknown until it hits");
            Equal(1000L, DodgeTimings.WarnAt(1000, null, 500), "unknown: at the wind-up");
            foreach (var delay in new[] { 1170, 1150, 3900, 1180, 1160 })
            {
                timings.Learn(key, delay);
            }

            Equal(1170, timings.Delay(key) ?? 0, "the median ignores a blow from another monster");
            Equal(1670L, DodgeTimings.WarnAt(1000, timings.Delay(key), 500), "half a second before the blow");
            Equal(1000L, DodgeTimings.WarnAt(1000, 300, 500), "a blow quicker than the lead: at once");
            timings.Learn(key, 9000);
            Equal(1170, timings.Delay(key) ?? 0, "a blow long after the start is not that attack's");
            var copy = DodgeTimings.FromLines(timings.ToLines());
            Equal(timings.Delay(key), copy.Delay(key), "saved and loaded");
            var seeded = DodgeTimings.FromLines(new[] { $"{key}=900,900,900" }.Concat(new[] { $"{key}=1200,1210,1190" }));
            Equal(1190, seeded.Delay(key) ?? 0, "the player's own blows outweigh the shipped ones");
            var filed = new DodgeTimings();
            filed.AddHitTimes(new[] { "# comment", $"{key}=1169/2333", "Metadata/Monsters/X|1=20/100", "broken line" });
            Equal(1169, filed.Delay(key) ?? 0, "before any blow, the game files' hit time");
            Equal(2333, filed.Swing(key) ?? 0, "and how long one swing lasts");
            Equal(true, filed.Delay("Metadata/Monsters/X|1") == null, "a hit sooner than any blow is not believed");
            filed.Learn(key, 1150);
            Equal(1150, filed.Delay(key) ?? 0, "a blow felt outweighs the files");
            Equal(true, filed.ProjectileSpeed(key) == null, "a line without a speed fires no projectile");
        }

        private static void ProjectileWarningsAddTheFlight()
        {
            var timings = new DodgeTimings();
            const string bolt = "Metadata/Monsters/Caster|31";
            const string boss = "Metadata/Monsters/Boss|9";
            timings.AddHitTimes(new[] { $"{bolt}=800/1500/1000", $"{boss}=8646/9466", "Metadata/Monsters/Boss|10=5000/0", "Metadata/Monsters/Caster|32=800/1500/fast" });
            Equal(800, timings.Delay(bolt) ?? 0, "the files time the release");
            Equal(1000, timings.ProjectileSpeed(bolt) ?? 0, "and the projectile's speed");
            Equal(8646, timings.Delay(boss) ?? 0, "a long boss attack is believed within its own swing");
            Equal(true, timings.Delay("Metadata/Monsters/Boss|10") == null, "but not past the longest blow learnt without a swing");
            Equal(true, timings.Delay("Metadata/Monsters/Caster|32") == null, "a broken speed drops the line");

            var travel = DodgeTimings.TravelMs(600f, timings.ProjectileSpeed(bolt));
            Equal(600, travel, "600 units at 1000 a second");
            Equal(0, DodgeTimings.TravelMs(600f, null), "no projectile, no flight");
            Equal(0, DodgeTimings.TravelMs(-20f, 1000), "bodies touching, no flight");
            Equal(1900L, DodgeTimings.WarnAt(1000, timings.Delay(bolt) + travel, 500), "half a second before the projectile arrives");
            Equal(1000L, DodgeTimings.WarnAt(1000, timings.Delay("Metadata/Monsters/Unknown|31") + travel, 500), "an untimed release still warns at the wind-up");

            Equal(true, DodgeTimings.CanStillHit(0, 1800, 600, 2500), "the animation still runs");
            Equal(true, DodgeTimings.CanStillHit(2000, 1800, 600, 2400), "released, the monster turned away, the projectile flies on");
            Equal(false, DodgeTimings.CanStillHit(1500, 1800, 600, 1600), "interrupted before the release: nothing comes");
            Equal(false, DodgeTimings.CanStillHit(2000, 1800, 600, 2700), "landed long ago");
            Equal(false, DodgeTimings.CanStillHit(2000, 1800, 0, 2100), "a blow beside the monster ends with its animation");
            Equal(false, DodgeTimings.CanStillHit(2000, 1800 + 1500, 600, 2100), "a later swing that ended before its release fired nothing");
        }

        private static void BuildFilesAreReadWithTheirNotes()
        {
            // The GGG docs' example shape: plain ids and objects with a weapon set and a note.
            var plan = BuildPlan.Parse("{\"name\":\"Titan\",\"passives\":[\"melee17\",{\"id\":\"strength17\",\"additional_text\":\"<m>{<red>{Strength +5 is recommended}}\"},{\"id\":\"melee25\",\"weapon_set\":2}],\"skills\":[]}");
            Equal("Titan 3", $"{plan?.Name} {plan?.Passives.Count}", "name and passives");
            Equal("strength17 1 0", $"{plan?.Passives[1].Id} {plan?.Passives[1].Order} {plan?.Passives[1].WeaponSet}", "an object entry keeps its place");
            Equal(2, plan?.Passives[2].WeaponSet ?? 0, "weapon set");
            Equal(true, BuildPlan.Parse("not json") == null && BuildPlan.Parse("{\"name\":\"x\"}") == null, "no build file");
            Equal("Strength +5 is recommended", BuildNotes.Plain("<m>{<red>{Strength +5 is recommended}}"), "markup out");
            var words = new[] { "Strength", "Dexterity", "Intelligence", "Fuerza", "Destreza", "Inteligencia" };
            Equal(true, BuildNotes.ColourOnly("<m>{<red>{Strength +5 is recommended}}", words) == null, "a note naming the attribute needs nothing added");
            Equal(BuildNotes.Attribute.Strength, BuildNotes.ColourOnly("<red>{Attribute is recommended}", words), "red alone means strength");
            Equal(BuildNotes.Attribute.Dexterity, BuildNotes.ColourOnly("<green>{+5}", words), "green dexterity");
            Equal(true, BuildNotes.ColourOnly("<grey>{Point #1}", words) == null, "other colours say nothing");
            Equal("True True True False", string.Join(" ", new[] { "strength89", "intelligence36_", "attributes65", "melee17" }.Select(BuildNotes.IsAttributeNode)), "attribute nodes by id");
        }

        private static void MinionsAreCountedByKind()
        {
            Equal(string.Empty, MinionGroups.Describe(Array.Empty<(string, int)>()), "none: nothing said");
            var said = MinionGroups.Describe(new[] { ("Arquero", 100), ("Lobo", 45), ("Arquero", 60), ("Arquero", 80) });
            Equal(true, said.EndsWith("3 Arquero, 80 percent; Lobo, 45 percent") || said.EndsWith("3 Arquero, 80 por ciento; Lobo, 45 por ciento"), $"the most first, life averaged: {said}");
        }

        private static void ANodeThatHoldsNoOtherCanBeRefunded()
        {
            // Start 0 - 1 - 2 - 3, 2 - 4, and 5 hanging from the second root 6; 7 is not taken.
            var links = new[] { new[] { 1 }, new[] { 0, 2 }, new[] { 1, 3, 4 }, new[] { 2, 7 }, new[] { 2 }, new[] { 6 }, new[] { 5 }, new[] { 3 } };
            var taken = new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 };
            var roots = new[] { 0, 6 };
            Equal(0, PassiveMap.Held(links, taken, roots, 3), "the end of a line holds none");
            Equal(0, PassiveMap.Held(links, taken, roots, 4), "nor does the other end");
            Equal(2, PassiveMap.Held(links, taken, roots, 2), "the fork holds both ends");
            Equal(3, PassiveMap.Held(links, taken, roots, 1), "and the node before it the fork too");
            Equal(0, PassiveMap.Held(links, taken, roots, 5), "a node of the other tree hangs from its own root");
            Equal(0, PassiveMap.Held(links, taken, roots, 0), "a root is not given back");
            Equal(0, PassiveMap.Held(links, taken, roots, 7), "nor a node not taken");

            // A ring: 0 - 1 - 2 - 3 - 0. Either way round still joins the rest.
            var ring = new[] { new[] { 1, 3 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2, 0 } };
            Equal(0, PassiveMap.Held(ring, new HashSet<int> { 0, 1, 2, 3 }, new[] { 0 }, 1), "a node of a ring holds none");
        }

        private static void PassiveChoicesAreReadWithTheirNodes()
        {
            // The owner's eight attribute nodes as the state listed them (2026-11-14): node id low, the row chosen high.
            var nodes = new HashSet<int> { 16725, 21387, 27373 };
            var variants = new HashSet<int> { 14927, 26297 };
            var read = PassiveChoices.Read(new[] { 0x3A4F4155u, 0x66B9538Bu, 0x3A4F6AEDu }, nodes, variants);
            Equal(3, read!.Count, "three nodes with a choice");
            Equal(14927, read[16725], "strength21 was given Dexterity's row");
            Equal(26297, read[21387], "strength67 Strength's");
            Equal(null, PassiveChoices.Read(new[] { 0x3A4F4155u, 0x00010002u }, nodes, variants), "an entry that names no node and no choice: another list");
            Equal(0, PassiveChoices.Read(Array.Empty<uint>(), nodes, variants)!.Count, "no choice made");
            Equal("14927x2 26297x1", string.Join(' ', PassiveChoices.Counts(read.Values).Select(count => $"{count.Variant}x{count.Nodes}")), "the most taken first");
        }

        private static void PassiveStepsCountThePointsAlongTheLinks()
        {
            // 0 - 1 - 2 - 3, with 4 linked to nothing.
            var links = new[] { new[] { 1 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2 }, Array.Empty<int>() };
            var steps = PassiveMap.Steps(links, new[] { 0 });
            Equal(3, steps[3], "three links from what is taken");
            Equal(0, steps[0], "taken costs nothing");
            Equal(PassiveMap.NoPath, steps[4], "no link leads there");
            Equal(1, PassiveMap.Steps(links, new[] { 0, 2 })[3], "the nearest taken node counts");
        }

        private static void PassiveZoneNamesTheSideAndRing()
        {
            var positions = new[] { new Vector2(-100, 0), new Vector2(100, 0), new Vector2(0, -100), new Vector2(0, 100), new Vector2(10, 0), new Vector2(50, -50) };
            Equal((0, 2), PassiveMap.Zone(positions, 1), "the far right edge: right, outer");
            Equal((2, 2), PassiveMap.Zone(positions, 2), "screen y grows down: the top is up");
            Equal((0, 0), PassiveMap.Zone(positions, 4), "beside the centre: inner");
            Equal((1, 2), PassiveMap.Zone(positions, 5), "up right, at 0.7 of the radius: outer");
        }

        private static void PassiveFinderWalksItsLists()
        {
            var (keystone, nearNotable, farNotable, reachable) = ((IntPtr)1, (IntPtr)2, (IntPtr)3, (IntPtr)4);
            IReadOnlyList<IntPtr> Members(PassiveCategory category) => category switch
            {
                PassiveCategory.Keystones => new[] { keystone },
                PassiveCategory.Notables => new[] { nearNotable, farNotable },
                PassiveCategory.Reachable => new[] { reachable },
                _ => Array.Empty<IntPtr>(),
            };

            var finder = new PassiveFinder();
            finder.ChangeCategory(1, Members);
            Equal(PassiveCategory.Keystones, finder.Category, "down: the first list");
            Equal(keystone, finder.Target, "the list's nearest is chosen at once");
            finder.ChangeCategory(1, Members);
            finder.ChangeEntry(1, Members(finder.Category));
            Equal(farNotable, finder.Target, "right: the next entry");
            finder.ChangeEntry(1, Members(finder.Category));
            Equal(nearNotable, finder.Target, "and round");
            finder.ChangeEntry(-1, new[] { farNotable, nearNotable });
            Equal(farNotable, finder.Target, "the list re-sorted under the choice: it moves from where the choice is now");
            finder.ChangeCategory(1, Members);
            Equal(PassiveCategory.Reachable, finder.Category, "an empty list is passed over");
            finder.ChangeCategory(1, Members);
            Equal(PassiveCategory.Position, finder.Category, "the lists wrap to the position");
            Equal(IntPtr.Zero, finder.Target, "which lets the target go");

            finder.ChangeCategory(1, Members);
            Equal(false, finder.Arrive(reachable, true), "another node");
            Equal(keystone, finder.Target, "keeps the guide");
            Equal(true, finder.Arrive(keystone, false), "landing on a target taken");
            Equal(IntPtr.Zero, finder.Target, "ends the guide");
            Equal(false, finder.Arrive(keystone, false), "once");

            finder.ChangeCategory(-1, Members);
            finder.ChangeCategory(1, Members);
            Equal(true, finder.Arrive(keystone, true), "landing on a target not taken");
            Equal((keystone, true), (finder.Target, finder.Taking), "leads on to the nodes to take for it");
            Equal(false, finder.Arrive(keystone, true), "said once");
            finder.ChangeEntry(1, Members(finder.Category));
            Equal(false, finder.Taking, "another choice is looked at first");
            finder.Drop();
            Equal((IntPtr.Zero, false), (finder.Target, finder.Taking), "taken: the guide ends");
        }

        private static void TheTreeRemembersWhereEachCharacterLeftOff()
        {
            var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"passive-resume-{Guid.NewGuid():N}.txt");
            try
            {
                var book = new PassiveResume(file);
                Equal(true, book.Get("yarkey") == null, "nothing yet");
                book.Set("yarkey", "attributes20");
                book.Set("malgdhler", "spell_criticals7");
                book.Set("yarkey", "jewel_slot1970");
                var again = new PassiveResume(file);
                Equal("jewel_slot1970", again.Get("yarkey"), "the last place, after a restart");
                Equal("spell_criticals7", again.Get("malgdhler"), "each character its own");
            }
            finally
            {
                System.IO.File.Delete(file);
            }

            var finder = new PassiveFinder();
            var place = (IntPtr)7;
            finder.Return(place);
            Equal((place, true, PassiveCategory.Position), (finder.Target, finder.Returning, finder.Category), "led back from the start");
            Equal(true, finder.Arrive(place, true), "landing there");
            Equal((IntPtr.Zero, false, false), (finder.Target, finder.Returning, finder.Taking), "ends the guide: nothing to take there");
        }

        private static void APassiveTrailTakesTheFewestPointsAlongTheBuild()
        {
            // 0 (taken) - 1 - 2 - 5 (target), and 0 - 3 - 4 - 5: two trails of three points; 6 hangs off 5, 7 is linked to nothing.
            var links = new[] { new[] { 1, 3 }, new[] { 0, 2 }, new[] { 1, 5 }, new[] { 0, 4 }, new[] { 3, 5 }, new[] { 2, 4, 6 }, new[] { 5 }, Array.Empty<int>() };
            int Plain(int node) => PassiveMap.TrailCost(false, false, false);
            Equal(3, PassiveMap.Trail(links, new[] { 0 }, 5, Plain)!.Count, "the fewest points");
            Equal("1,2,5", string.Join(",", PassiveMap.Trail(links, new[] { 0 }, 5, node => PassiveMap.TrailCost(false, node == 2, false))!), "a notable on one of two trails as long");
            Equal("3,4,5", string.Join(",", PassiveMap.Trail(links, new[] { 0 }, 5, node => PassiveMap.TrailCost(node is 3 or 4, node == 2, false))!), "the build's nodes before a notable");
            Equal("2,5,6", string.Join(",", PassiveMap.Trail(links, new[] { 0, 1 }, 6, Plain)!), "from the nearest taken node, in the order they can be taken");
            Equal(0, PassiveMap.Trail(links, new[] { 0, 1 }, 1, Plain)!.Count, "taken: nothing to take");
            Equal(true, PassiveMap.Trail(links, new[] { 0 }, 7, Plain) == null, "no link leads there");

            // 0 - 1 - 2 (target): one point off the build, or 0 - 3 - 4 - 5 - 2 along it.
            var detour = new[] { new[] { 1, 3 }, new[] { 0, 2 }, new[] { 1, 5 }, new[] { 0, 4 }, new[] { 3, 5 }, new[] { 4, 2 } };
            Equal("3,4,5,2", string.Join(",", PassiveMap.Trail(detour, new[] { 0 }, 2, node => PassiveMap.TrailCost(node is 2 or 3 or 4 or 5, false, true))!), "a build's node is reached along the build");
            Equal("1,2", string.Join(",", PassiveMap.Trail(detour, new[] { 0 }, 2, node => PassiveMap.TrailCost(node is 3 or 4 or 5, false, false))!), "another node by the fewest points");
        }

        private static void ObjectSoundsDieAwayPastTheirRange()
        {
            const float Range = 1300f;
            const float Far = 0.3f;
            Equal(1f, ProximityFade.Gain(0f, Range, Far), "on it");
            Equal(Far, ProximityFade.Gain(Range, Range, Far), "the edge of the range keeps its level");
            Equal(0f, ProximityFade.Gain(Range + ProximityFade.TailWorld, Range, Far), "silent where it stops");
            var previous = 2f;
            for (var distance = 0f; distance <= Range + ProximityFade.TailWorld; distance += 10f)
            {
                var gain = ProximityFade.Gain(distance, Range, Far);
                Equal(true, gain < previous, $"falls all the way ({distance})");
                Equal(true, previous - gain < 0.02f || previous == 2f, $"with no step ({distance})");
                previous = gain;
            }
        }

        private static void SettingsMenuOpensGroupsAndGoesBack()
        {
            static SettingsCursor.Node Menu(int settings, params SettingsCursor.Node[] submenus) =>
                new(Enumerable.Repeat(SettingsCursor.Node.Setting, settings).Concat(submenus).ToArray());

            // Top level: a group of 5, one of 12, and Sound: one volume, then a submenu of 3 and one of 2.
            var cursor = new SettingsCursor(Menu(0, Menu(5), Menu(12), Menu(1, Menu(3), Menu(2))));
            Equal(false, cursor.InGroup, "opens on the groups");
            Equal((1, 3), cursor.Place, "the top level lists the groups");
            cursor.Move(1);
            Equal(true, cursor.Enter(), "a group opens");
            Equal((true, 1, 0), (cursor.InGroup, cursor.Path[0], cursor.Path[1]), "a group opens on its first entry");
            Equal((1, 12), cursor.Place, "within a group, the place is the group's");
            Equal(false, cursor.Enter(), "a setting is no submenu");
            cursor.Move(-1);
            Equal(11, cursor.Path[1], "a group wraps");
            Equal(true, cursor.Back(), "back");
            Equal((false, 1), (cursor.InGroup, cursor.Path[0]), "back lands on the group left");
            Equal(false, cursor.Back(), "nothing above the top level");
            cursor.Move(-2);
            Equal(2, cursor.Path[0], "the groups wrap");
            cursor.Move(-1);
            cursor.Enter();
            Equal(11, cursor.Path[1], "a group opens on the entry it was left on");
            cursor.Back();
            cursor.Move(1);
            cursor.Enter();
            Equal(false, cursor.OnSubmenu, "Sound opens on its volume");
            cursor.Move(2);
            Equal(true, cursor.OnSubmenu, "then its submenus");
            cursor.Enter();
            Equal((3, 0, 2), (cursor.Path.Count, cursor.Place.Position - 1, cursor.Place.Count), "a nested submenu opens a level deeper");
            cursor.Move(1);
            cursor.Back();
            Equal((2, 2), (cursor.Path.Count, cursor.Path[1]), "back one level, on the submenu left");
            cursor.Back();
            cursor.Enter();
            Equal(2, cursor.Path[1], "each submenu reopens where it was left");
            cursor.Enter();
            Equal(1, cursor.Path[2], "nested ones too");
        }

        private static void NewsFollowItsDeliveryLevel()
        {
            var output = new RecordingOutput();
            SpeechPriority.Chosen = null;
            output.Say(SpeechKind.StatusBegins, "a");
            output.Say(SpeechKind.GroundItems, "b");
            output.Say(SpeechKind.LifeAndMana, "c", urgent: true);
            output.Say(SpeechKind.LifeAndMana, "d");
            SpeechPriority.Chosen = _ => SpeechPriority.CutsIn;
            output.Say(SpeechKind.GroundItems, "e");
            SpeechPriority.Chosen = _ => "nonsense";
            output.Say(SpeechKind.Quest, "f");
            SpeechPriority.Chosen = null;
            output.Say(SpeechKind.Charges, "g");
            output.Say(SpeechKind.Combo, "h");
            Equal("urge a|b False|urge c|mention d|e True|f False|g False|h False", string.Join("|", output.Calls), "each kind at its level, urgent news holds, a waiting life warning keeps its turn, defaults for unknown values, charges and combo wait");
        }

        private sealed class RecordingOutput : IAccessibilityOutputService
        {
            public List<string> Calls { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Calls.Add($"{text} {interrupt}");

            public void Urge(string text, int holdMs = 1500, bool yieldsToMoves = false) => this.Calls.Add($"urge {text}");

            public void Keep(string text, int holdMs, bool interrupt = false, bool yieldsToMoves = false) => this.Calls.Add($"keep {text} {interrupt}");

            public void Mention(string text) => this.Calls.Add($"mention {text}");

            public void Repeat()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }

        private static void StashOpenedOnInventoryNamesTheInventory()
        {
            // Live 2026-11-08: the stash opened with the cursor on the inventory's first item and the stash's selected
            // tab "1" was said as its place ("pestaña 1. Orbe de transmutación, columna 1, fila 1"), where a helmet lay.
            Equal("Inventario", FocusDetails.PagePlace("Inventario", "1"), "an inventory item beside the stash is said in the inventory");
            Equal(Announcements.TabTitle("1"), FocusDetails.PagePlace("Alijo. " + Announcements.TabTitle("1"), "1"), "a stash item keeps its tab");
            Equal(Announcements.TabTitle("1"), FocusDetails.PagePlace(string.Empty, "1"), "a page focus with no part of its own (options) takes the tab");
            Equal("Gráficos", FocusDetails.PagePlace(string.Empty, "Gráficos"), "a named tab keeps its name");

            // Live 2026-11-10: a merchant opened on the inventory's side said "Inventario. Orbe de caos…" and never its tab.
            Equal("Armas", FocusDetails.PageBeside("Inventario", "Armas", string.Empty), "a merchant's first tab is named beside the inventory");
            Equal("Alijo, " + Announcements.TabTitle("1"), FocusDetails.PageBeside("Inventario", "1", "Alijo"), "a numbered tab with its panel's title");
            Equal(string.Empty, FocusDetails.PageBeside("Alijo. " + Announcements.TabTitle("1"), "1", "Alijo"), "a focus on the page itself is placed by its tab alone");
            Equal(string.Empty, FocusDetails.PageBeside(string.Empty, "Gráficos", string.Empty), "as is one with no part of its own");
            Equal("Armas", FocusDetails.PageBeside("Inventario", "Armas", "Inventario"), "the inventory is not the panel beside itself");
        }

        private static void WallTonesSwellNearWalls()
        {
            Equal(0f, RadarSynth.WallToneGain(20f, 20f), "open to the range: silent");
            Equal(1f, RadarSynth.WallToneGain(0f, 20f), "touching the wall: full");
            Equal(true, RadarSynth.WallToneGain(2f, 20f) > RadarSynth.WallToneGain(10f, 20f), "closer is louder");
            var loop = RadarSynth.WallTone(RadarSynth.Direction.Up, 44100);
            Equal(true, MathF.Abs(loop[0] - loop[^1]) < 0.08f, "the loop has no seam");
            static float Rms(float[] samples) => MathF.Sqrt(samples.Sum(s => s * s) / samples.Length);
            Equal(true, MathF.Abs(Rms(loop) - 0.04f) < 0.001f, "a touching wall sits at -28 dBFS RMS (D2R)");
            var behind = Rms(RadarSynth.WallTone(RadarSynth.Direction.Down, 44100)) / Rms(loop);
            Equal(true, MathF.Abs(behind - 1.413f) < 0.01f, "the band behind is lifted 3 dB");
        }
    }
}
