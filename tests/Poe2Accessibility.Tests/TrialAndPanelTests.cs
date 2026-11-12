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

    /// <summary>Camera view, trial maps and traps, the guide, honour, panel-over-game and moved-field trust.</summary>
    public static class TrialAndPanelTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(CameraViewIsASixteenByNineScreenAroundThePlayer), CameraViewIsASixteenByNineScreenAroundThePlayer),
            (nameof(CameraViewDrawsFromTheMatrixWhateverTheWindow), CameraViewDrawsFromTheMatrixWhateverTheWindow),
            (nameof(TrialHudCountsWaterAndKeys), TrialHudCountsWaterAndKeys),
            (nameof(TrialTrapsAreToldByTheirFiles), TrialTrapsAreToldByTheirFiles),
            (nameof(SheetWidgetsAreToldByTheirTooltipsKeys), SheetWidgetsAreToldByTheirTooltipsKeys),
            (nameof(TrialMapIsColumnsFromEntranceToBoss), TrialMapIsColumnsFromEntranceToBoss),
            (nameof(TheGuideGoesFromCrystalToCrystal), TheGuideGoesFromCrystalToCrystal),
            (nameof(HonourIsSaidAsItFalls), HonourIsSaidAsItFalls),
            (nameof(TakingTheFloorCutsAKeptLine), TakingTheFloorCutsAKeptLine),
            (nameof(QuestPageWaitsForTheMenuToClose), QuestPageWaitsForTheMenuToClose),
            (nameof(WorldIsBackOnlyOnceNothingCoversIt), WorldIsBackOnlyOnceNothingCoversIt),
            (nameof(PanelOverTheGameIsTheTopLayerThatIsNotTheHud), PanelOverTheGameIsTheTopLayerThatIsNotTheHud),
            (nameof(NearbyThingsWaitForTheCooldown), NearbyThingsWaitForTheCooldown),
            (nameof(StickClickWhileHoldingL3), StickClickWhileHoldingL3),
            (nameof(MovedFieldsAreFoundNearestFirst), MovedFieldsAreFoundNearestFirst),
            (nameof(TownsAndHideoutsAreToldByTheirRow), TownsAndHideoutsAreToldByTheirRow),
            (nameof(ALayoutLostTooEarlyIsJudgedAgain), ALayoutLostTooEarlyIsJudgedAgain),
            (nameof(MovedControlFieldsAreTrustedOnceTwoControlsAgree), MovedControlFieldsAreTrustedOnceTwoControlsAgree),
            (nameof(TheHoveredPinIsTheKeyThatChangesWithTheCursor), TheHoveredPinIsTheKeyThatChangesWithTheCursor),
        };

        private static void CameraViewIsASixteenByNineScreenAroundThePlayer()
        {
            // World +X draws right and up, +Y left and up (2560x1440: 85.3 px across and 63.1 px up per 100 units of either).
            Equal(true, CameraView.Offset(100, 0).X > 0 && CameraView.Offset(100, 0).Y < 0, "world +X: right and up");
            Equal(true, CameraView.Offset(0, 100).X < 0 && CameraView.Offset(0, 100).Y < 0, "world +Y: left and up");
            Equal(true, CameraView.InView(0, 0), "the player");
            Equal(true, CameraView.InView(600, -600), "0.71 heights right: on screen");
            Equal(false, CameraView.InView(800, -800), "0.95 heights right: past a 16:9 edge at 0.89");
            Equal(true, CameraView.InView(-600, -600), "below, 0.47 from the centre");
            Equal(false, CameraView.InView(-700, -700), "lower, 0.56 from the centre");
            Equal(true, CameraView.InView(480, 480), "above, 0.42 heights: the player stands high, so less is seen above");
            Equal(false, CameraView.InView(530, 530), "higher, 0.46: past the top edge at 0.44");
            Equal(1f, MathF.Round(CameraView.HalfScreens(750.6f, -750.6f).X, 2), "a 16:9 screen's right edge is one half screen");
            Equal(false, CameraView.IsUsable(System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero), "no window: nothing drawn");
            Equal(false, CameraView.IsUsable(new System.Numerics.Vector2(float.NaN, 1), new System.Numerics.Vector2(1, 1)), "a camera not drawn yet");
            Equal(true, CameraView.IsUsable(CameraView.Offset(100, 0), CameraView.Offset(0, 100)), "the measured camera itself");
        }

        private static void CameraViewDrawsFromTheMatrixWhateverTheWindow()
        {
            // A camera like the game's: above and behind the player, looking down at it; the game widens the view with the
            // window (the same vertical angle), so its matrix differs per aspect while the place drawn per height must not.
            var target = new System.Numerics.Vector3(5000, 3000, 0);
            var eye = target + new System.Numerics.Vector3(-700, -700, 1200);
            var view = System.Numerics.Matrix4x4.CreateLookAt(eye, target, System.Numerics.Vector3.UnitZ);
            System.Numerics.Matrix4x4 Camera(float aspect) => view * System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(0.6f, aspect, 10f, 20000f);
            var point = target + new System.Numerics.Vector3(300, -200, 0);
            var wide = CameraView.Draw(Camera(16f / 9f), point);
            var square = CameraView.Draw(Camera(5f / 4f), point);
            var ultra = CameraView.Draw(Camera(21f / 9f), point);
            Equal(true, wide is { } w && square is { } s && ultra is { } u && System.Numerics.Vector2.Distance(w, s) < 1e-5f && System.Numerics.Vector2.Distance(w, u) < 1e-5f,
                  "the same place at 16:9, 5:4 and 21:9");
            Equal(true, CameraView.Draw(Camera(16f / 9f), target) is { } centre && centre.Length() < 1e-5f, "what the camera looks at: the centre");

            // What GameHelper's WorldToScreen gives on a 1280x1024 window, in heights from its centre: the same numbers.
            System.Numerics.Vector2 OnWindow(System.Numerics.Matrix4x4 camera, float width, float height)
            {
                var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(point, 1f), camera);
                var (x, y) = ((clip.X / clip.W + 1f) * width / 2f, (1f - (clip.Y / clip.W)) * height / 2f);
                return new System.Numerics.Vector2(x - (width / 2f), y - (height / 2f)) / height;
            }

            Equal(true, square is { } s2 && System.Numerics.Vector2.Distance(s2, OnWindow(Camera(5f / 4f), 1280, 1024)) < 1e-5f, "as a 5:4 window draws it");

            // A view shifted off its centre (as a camera keeping the player high on the screen could be) keeps its aspect.
            var (near, half) = (10f, 10f * MathF.Tan(0.3f));
            var shifted = view * System.Numerics.Matrix4x4.CreatePerspectiveOffCenter(-half * 16f / 9f, half * 16f / 9f, -half * 0.9f, half * 1.1f, near, 20000f);
            Equal(true, CameraView.Draw(shifted, point) is { } off && System.Numerics.Vector2.Distance(off, OnWindow(shifted, 2560, 1440)) < 1e-5f, "an off-centre view");

            Equal(false, CameraView.Draw(Camera(16f / 9f), eye + (eye - target)).HasValue, "behind the camera: not drawn");
            Equal(false, CameraView.Draw(System.Numerics.Matrix4x4.Identity, point).HasValue, "GameHelper's placeholder before the first frame draws nothing");
            Equal(true, wide is { } w2 && CameraView.InView(w2), "a place near the player is in view");
            Equal(false, CameraView.Draw(Camera(16f / 9f), target + new System.Numerics.Vector3(4000, -4000, 0)) is { } far && CameraView.InView(far), "far off: out of view");
        }

        private static void TrialHudCountsWaterAndKeys()
        {
            Equal(new TrialCounters(39, 7, 1, 0, 0), TrialHud.Parse(new[] { "39(+7)", "1", "0", "0" }), "the HUD of 2026-11-07");
            Equal(new TrialCounters(46, 0, 0, 0, 2), TrialHud.Parse(new[] { "46", "0", "0", "2" }), "water without what is to come");
            Equal(null, TrialHud.Parse(new[] { "39(+7)", "1" }), "not all the counters");
            Equal(null, TrialHud.Parse(new[] { "x", "1", "0", "0" }), "no number");
            Equal((474, 513), TrialHud.HonourOf("Honor: 474/513"), "the honour line");
            Equal(null, TrialHud.HonourOf("Honor"), "with no amount");

            var watch = new TrialCounterWatch();
            Equal(default, watch.Heard(new TrialCounters(39, 0, 0, 0, 0), 0), "the first reading is no news");
            Equal(default, watch.Heard(new TrialCounters(41, 0, 0, 0, 0), 100), "water still falling into the purse");
            Equal(default, watch.Heard(new TrialCounters(46, 0, 0, 0, 0), 800), "more of it");
            Equal(46, watch.Heard(new TrialCounters(46, 0, 0, 0, 0), 2400).Water, "said once it settles");
            Equal(null, watch.Heard(new TrialCounters(46, 0, 0, 0, 0), 4000).Water, "and only once");
            Equal(1, watch.Heard(new TrialCounters(46, 0, 1, 0, 0), 4100).Bronze, "a key at once");
            Equal(default, watch.Heard(new TrialCounters(30, 0, 1, 0, 0), 4200), "water spent is no news");
            Equal(null, watch.Heard(new TrialCounters(30, 0, 1, 0, 0), 6000).Water, "nor once settled");
            Equal(31, watch.Heard(new TrialCounters(31, 0, 1, 0, 0), 6100) is var _ ? watch.Heard(new TrialCounters(31, 0, 1, 0, 0), 7700).Water : null, "the next gain counts from there");
        }

        private static void TrialTrapsAreToldByTheirFiles()
        {
            const string Hazards = "Metadata/Monsters/MarakethSanctumTrial/Hazards/";
            Equal(TrialTrap.Spikes, TrialTraps.KindOf(Hazards + "GroundSpike"), "a floor's spikes");
            Equal(TrialTrap.Laser, TrialTraps.KindOf(Hazards + "LaserCrossBeamTurret@22"), "whatever its level");
            Equal(null, TrialTraps.KindOf(Hazards + "GroundSpikeSpawner"), "what spawns them is no trap");
            Equal(null, TrialTraps.KindOf(Hazards + "HourglassLethal"), "the Death Crystal is its own object");
            Equal(true, TrialWater.IsDrop("Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote"), "the sacred water a monster drops");
            Equal(true, TrialWater.IsDrop("Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote@22"), "whatever its level");
            Equal(false, TrialWater.IsDrop("Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMoteSpawner"), "and nothing merely named after it");
            Equal(null, TrialTraps.KindOf("Metadata/Monsters/Wolves/Wolf"), "a monster");
            Equal(false, TrialTraps.IsSeenBeforeItStrikes(TrialTrap.Spikes), "spikes lie hidden");
            Equal(true, TrialTraps.IsSeenBeforeItStrikes(TrialTrap.Laser), "a turret stands in the open");
            Equal(true, TrialTraps.IsFiring(Hazards + "GroundSpike", new[] { ("rumble", 1L), ("spike", 0L) }), "the ground rumbling warns of them");
            Equal(false, TrialTraps.IsFiring(Hazards + "GroundSpike", new[] { ("rumble", 0L), ("spike", 0L) }), "at rest");
            Equal(false, TrialTraps.IsFiring(Hazards + "LaserCrossBeamTurret", new[] { ("active", 1L), ("fire", 0L) }), "armed is not firing");
            Equal(true, TrialTraps.IsFiring(Hazards + "LaserCrossBeamTurret", new[] { ("fire", 1L) }), "firing");
        }

        private static void SheetWidgetsAreToldByTheirTooltipsKeys()
        {
            Equal("chaos_damage_resistance_%", SheetStats.FromMarkup("<valuedefault>{Chaos Resistance}\nYou take this much less [Chaos] damage.\nMaximum [Resistances|Resistance] is capped at 75%. The number in brackets is the [UncappedResist|Uncapped] value.")?.Stat, "a resistance in English");
            Equal("uncapped_fire_damage_resistance_%", SheetStats.FromMarkup("<valuedefault>{Resistencia al fuego}\nRecibes esta cantidad menos de daño de [Fire|fuego].\n[Resistances|Resistencia] máxima [UncappedResist|sin tope]")?.Uncapped, "the keys stay English in Spanish");
            Equal(BuildNotes.Attribute.Strength, SheetStats.FromMarkup("1 [Strength|Strength] = +2 Life\n5 [Strength|Strength] = +1 Strength [SupportGem|Support Gem] Capacity")?.Attribute, "an attribute");
            Equal("intelligence", SheetStats.FromMarkup("1 [Intelligence] = +2 Mana\n5 [Intelligence|Intelligence] = +1 Intelligence [SupportGem|Support Gem] Capacity")?.Stat, "a key without its shown half");
            Equal(null, SheetStats.FromMarkup("<valuedefault>{Estimated Armour Mitigation}\n[Armour] reduces the [Physical|Physical Damage] you take"), "any other tooltip");
            Equal(null, SheetStats.FromMarkup("+10 to [Strength]\n+20% to [Fire] [Resistances|Resistance]"), "an item's lines are no sheet widget");
            Equal(null, SheetStats.FromMarkup(string.Empty), "no tooltip");
        }

        private static void TrialMapIsColumnsFromEntranceToBoss()
        {
            Equal(true, TrialMap.IsMap(new[] { 1, 3, 4, 3, 5, 3, 6, 1 }), "the floor seen on 2026-11-07");
            Equal(false, TrialMap.IsMap(new[] { 3, 3, 3, 3 }), "a grid of buttons has no entrance");
            Equal(false, TrialMap.IsMap(new[] { 1, 2, 1 }), "too few columns");
            Equal(false, TrialMap.IsMap(new[] { 1, 0, 3, 1 }), "and every column holds rooms");
            Equal(false, TrialMap.IsMap(Enumerable.Repeat(1, 31).ToArray()), "no choice of rooms: the Cosmetics panel's 31 slots (2026-11-10)");
            // The user's floor of 2026-11-12 as the game drew it: columns of 1, 3, 3, 4, 5, 4, 6 and 1 rooms.
            var floor = new[] { 1, 3, 3, 4, 5, 4, 6, 1 };
            var links = new IReadOnlyList<int>[][]
            {
                new IReadOnlyList<int>[] { new[] { 0, 1, 2 } },
                new IReadOnlyList<int>[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2 } },
                new IReadOnlyList<int>[] { new[] { 0, 1, 2 }, new[] { 2 }, new[] { 2, 3 } },
                new IReadOnlyList<int>[] { new[] { 0, 1, 2 }, new[] { 2 }, new[] { 2, 3, 4 }, new[] { 4 } },
                new IReadOnlyList<int>[] { new[] { 0, 1 }, new[] { 1 }, new[] { 1, 2 }, new[] { 2 }, new[] { 2, 3 } },
                new IReadOnlyList<int>[] { new[] { 0, 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 }, new[] { 4, 5 } },
                new IReadOnlyList<int>[] { new[] { 0 }, new[] { 0 }, new[] { 0 }, new[] { 0 }, new[] { 0 }, new[] { 0 } },
            };
            Equal(true, TrialMap.Fits(floor, links), "links that fit the map on screen");
            Equal(false, TrialMap.Fits(new[] { 1, 3, 4, 3, 5, 3, 6, 1 }, links), "another floor's are not this map's");
            Equal(false, TrialMap.Fits(floor, links.Take(6).ToArray()), "nor a column short");
            Equal("4", string.Join(",", TrialMap.WaysTo(links, 4, new[] { 4 }, 6, 5)), "the one room to enter leads on to the last of the sixes");
            Equal(string.Empty, string.Join(",", TrialMap.WaysTo(links, 4, new[] { 4 }, 6, 0)), "and by no way to the first");
            Equal("0,1", string.Join(",", TrialMap.WaysTo(links, 1, new[] { 0, 1, 2 }, 2, 1)), "two of the three first rooms lead to the middle of the next");
            Equal("0", string.Join(",", TrialMap.WaysTo(links, 1, new[] { 0, 1, 2 }, 3, 1)), "and one alone to a room two columns on");
            Equal("4", string.Join(",", TrialMap.WaysTo(links, 4, new[] { 4 }, 4, 4)), "a room leads to itself");

            // The rooms taken as the game keeps them, a byte a column, and what the map then says of each room.
            const byte NoRoom = 0xFF;
            var walk = TrialMap.Walk(links, new byte[] { 0, 1, 2, 3, NoRoom, NoRoom, NoRoom, NoRoom }, NoRoom);
            Equal("0,1,2,3", string.Join(",", walk), "the walk of 2026-11-12");
            Equal(0, TrialMap.Walk(links, new byte[] { 0, 1, 0, 3, NoRoom, NoRoom, NoRoom, NoRoom }, NoRoom).Count, "rooms no link joins are no walk");
            Equal(0, TrialMap.Walk(links, new byte[] { 0, 1, NoRoom, 3, NoRoom, NoRoom, NoRoom, NoRoom }, NoRoom).Count, "nor a walk with a gap");
            Equal(0, TrialMap.Walk(links, new byte[] { NoRoom, NoRoom, NoRoom, NoRoom, NoRoom, NoRoom, NoRoom, NoRoom }, NoRoom).Count, "nor none at all");
            var open = new[] { 4 };
            string View(int column, int room) =>
                TrialMap.View(links, walk.Count - 1, walk[^1], column, room, open) is var view ? $"{view.Standing} through {string.Join(",", view.Through)} to {string.Join(",", view.LeadsTo)}" : string.Empty;
            Equal("Current through  to 4", View(3, 3), "the player's room, and the one room it leads to");
            Equal("Here through  to ", View(3, 0), "another room of its column");
            Equal("Behind through  to ", View(2, 2), "a room taken before");
            Equal("Open through  to 2,3", View(4, 4), "the room to enter, and where it leads");
            Equal("Beside through  to ", View(4, 0), "a room beside it no way leads to");
            Equal("Reachable through  to 3,4", View(5, 2), "a room further on the gold lines reach: one room to enter, so no choice to tell");
            Equal("Beside through  to ", View(5, 0), "and one the red lines alone reach");
            Equal("Reachable through  to ", View(6, 5), "the last rooms lead to the boss alone");
            Equal("Reachable through  to ", View(7, 0), "the boss");
            var start = TrialMap.Walk(links, new byte[] { 0, NoRoom, NoRoom, NoRoom, NoRoom, NoRoom, NoRoom, NoRoom }, NoRoom);
            var choice = TrialMap.View(links, start.Count - 1, start[^1], 3, 1, new[] { 0, 1, 2 });
            Equal("Reachable 0", $"{choice.Standing} {string.Join(",", choice.Through)}", "at the entrance, a room two columns on is reached through the first room alone");
            Equal(TrialRoomStanding.Open, TrialMap.Standing(1, 0, true, 1), "the room offers to enter");
            Equal(TrialRoomStanding.Here, TrialMap.Standing(0, 0, false, 1), "the column before the first to enter is the player's");
            Equal(TrialRoomStanding.Current, TrialMap.Standing(2, 1, false, 3, (2, 1)), "the room with the game's mark is where the player stands");
            Equal(TrialRoomStanding.Here, TrialMap.Standing(2, 0, false, 3, (2, 1)), "another room of its column");
            Equal(TrialRoomStanding.Behind, TrialMap.Standing(1, 0, false, 3), "before it, left behind");
            Equal(TrialRoomStanding.Open, TrialMap.Standing(1, 0, true, 1, (0, 0)), "a room that offers to enter says so");
            Equal(TrialRoomStanding.Ahead, TrialMap.Standing(2, 0, false, 1), "after it, not reachable");
            Equal(TrialRoomStanding.Beside, TrialMap.Standing(1, 1, false, 1), "a room beside the open ones that does not offer has no way from the player's");
            // The user's map of 2026-11-12, opened in the room before the boss with that room done: no room offers to enter in
            // its tooltip, the boss's has none. Every room was "not reachable yet", the boss's too, the one room to enter.
            Equal(TrialRoomStanding.Next, TrialMap.Standing(7, 0, false, -1, (6, 3)), "the boss's room, the column after the player's: not called out of reach");
            Equal(TrialRoomStanding.Current, TrialMap.Standing(6, 3, false, -1, (6, 3)), "the player's room by the game's mark alone");
            Equal(TrialRoomStanding.Here, TrialMap.Standing(6, 5, false, -1, (6, 3)), "its column");
            Equal(TrialRoomStanding.Behind, TrialMap.Standing(5, 2, false, -1, (6, 3)), "the rooms before");
            Equal(TrialRoomStanding.Ahead, TrialMap.Standing(7, 0, false, -1, (3, 3)), "a room two columns on cannot be entered yet");
            Equal(TrialRoomStanding.Unknown, TrialMap.Standing(0, 0, false, -1), "with nothing open and no room marked nothing is told");
        }

        private static void TheGuideGoesFromCrystalToCrystal()
        {
            var guide = new TimedGuide();
            Equal((null, false), guide.Look(Array.Empty<uint>(), null), "no crystal, nothing to do");
            Equal((7u, false), guide.Look(new uint[] { 7, 8, 9 }, null), "the nearest first");
            Equal((null, false), guide.Look(new uint[] { 8, 7, 9 }, 7), "while it stands the guide stays, whichever is now nearer");
            Equal((null, false), guide.Look(new uint[] { 7, 8, 9 }, null), "arrived or released, not again until it is disabled");
            Equal((8u, false), guide.Look(new uint[] { 8, 9 }, null), "disabled: the next");
            Equal((9u, false), guide.Look(new uint[] { 9 }, 8), "and straight on from the one gone");
            Equal((null, false), guide.Look(Array.Empty<uint>(), 9), "the room done: a page turned hides the objective too, so not at once");
            Equal((null, false), guide.Look(Array.Empty<uint>(), 9), "nor on the second look");
            Equal((null, true), guide.Look(Array.Empty<uint>(), 9), "then the guide lets go of the last crystal");
            Equal((null, false), guide.Look(Array.Empty<uint>(), null), "once");
            var paged = new TimedGuide();
            paged.Look(new uint[] { 7 }, null);
            paged.Look(Array.Empty<uint>(), 7);
            Equal((null, false), paged.Look(new uint[] { 7 }, 7), "the objective back in a moment: the guide stays");
            paged.Look(Array.Empty<uint>(), 7);
            paged.Look(Array.Empty<uint>(), 7);
            Equal((null, true), paged.Look(Array.Empty<uint>(), 7), "and the looks count from there");
            var walked = new TimedGuide();
            Equal((7u, false), walked.Look(new uint[] { 7, 8 }, null), "the nearest in a straight line");
            walked.Aim(8);
            Equal((null, false), walked.Look(new uint[] { 7, 8 }, 8), "the one chosen for its route is the one that stands");
            Equal((7u, false), walked.Look(new uint[] { 7 }, 8), "until it is disabled");
            var other = new TimedGuide();
            Equal((null, false), other.Look(new uint[] { 7 }, 42), "the player's own target is left alone");
            Equal((7u, false), other.Look(new uint[] { 7 }, null), "until released");
            other.Look(Array.Empty<uint>(), 42);
            other.Look(Array.Empty<uint>(), 42);
            Equal((null, false), other.Look(Array.Empty<uint>(), 42), "and it is not let go when the room ends");
            var next = new TimedGuide();
            next.Look(new uint[] { 7 }, null);
            next.Reset();
            Equal((7u, false), next.Look(new uint[] { 7 }, null), "in the next room the same id is another crystal: it is aimed at anew");
        }

        private static void HonourIsSaidAsItFalls()
        {
            var watch = new HonourWatch();
            Equal(null, watch.Heard("Honor: 474/513"), "the first reading is no fall");
            Equal(null, watch.Heard("Honor: 470/513"), "91%: no mark passed");
            Equal(77, watch.Heard("Honor: 400/513"), "past 90 and 80, said once");
            Equal(null, watch.Heard("Honor: 370/513"), "not again above the next");
            Equal(24, watch.Heard("Honor: 125/513"), "marks passed at one blow say the amount once");
            Equal(null, watch.Rose, "a fall is no rise");
            Equal(null, watch.Heard("Honor: 400/513"), "a fountain raises it");
            Equal(77, watch.Rose, "and what it rose to is said (user 2026-11-12: a shrine restored honour with nothing said)");
            Equal(null, watch.Heard("Honor: 410/513"), "two points more, a relic's kill");
            Equal(null, watch.Rose, "are not worth a line");
            Equal(null, watch.Heard("Honor: 430/513"), "until they add up");
            Equal(83, watch.Rose, "to a shrine's worth");
            Equal(49, watch.Heard("Honor: 255/513"), "and the marks count again");
            Equal(null, watch.Rose, "with no rise in a fall");
            Equal(null, watch.Heard("Honor"), "a line with no amount says nothing");
            Equal(9, watch.Heard("Honor: 50/513"), "and leaves the last reading");
            Equal(0, watch.Heard("Honor: 0/513"), "running out is said");
            Equal(null, watch.Heard("Honor: 0/513"), "once, and the hub's stale line after the failed run stays silent");
            Equal(null, watch.Heard("Honor: 513/513"), "the next run starts whole");
            Equal(null, watch.Rose, "which is no honour restored");
            Equal(58, watch.Heard("Honor: 300/513"), "a run left with honour to spare");
            watch.Reset();
            Equal(null, watch.Heard("Honor: 513/513"), "and the next one, in another area, starts whole");
            Equal(null, watch.Rose, "which is no honour restored either");
            Equal(87, watch.Heard("Honor: 450/513"), "and its marks count from there");

            var effects = new TrialEffectNews();
            Equal(0, effects.Gained(new[] { "AfflictionA", "BoonB" }).Count, "what a run carries at the first reading is no news");
            Equal("BoonC", string.Join("|", effects.Gained(new[] { "AfflictionA", "BoonB", "BoonC" })), "a boon gained is");
            Equal(0, effects.Gained(new[] { "AfflictionA" }).Count, "a slot that reads empty for a moment");
            Equal(0, effects.Gained(new[] { "AfflictionA", "BoonB", "BoonC" }).Count, "gains nothing when it reads again");
            for (var reading = 0; reading < TrialEffectNews.GoneAfter; reading++)
            {
                effects.Gained(new[] { "AfflictionA" });
            }

            Equal("BoonB", string.Join("|", effects.Gained(new[] { "AfflictionA", "BoonB" })), "one truly lost and gained again is news");
        }

        private static void TakingTheFloorCutsAKeptLine()
        {
            var now = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
            var floor = new SpeechFloor();
            floor.Keep("Misión: una página larga", false, 5000, now);
            Equal(true, floor.Held(now.AddSeconds(1)), "a kept line holds");
            Equal(false, floor.Admit("Oro recogido", false, now.AddSeconds(1)), "and what comes waits");
            floor.Take("Misión: la página siguiente", now.AddSeconds(2));
            Equal(false, floor.Held(now.AddSeconds(2)), "the player's turn takes the floor");
            Equal(0, floor.Release(now.AddSeconds(2)).Count, "and what waited is stale");
        }

        private static void QuestPageWaitsForTheMenuToClose()
        {
            var page = new QuestTrackerPage();
            page.News(new[] { "Noticias graves. Habla con Leitis" });
            Equal(string.Empty, page.News(new[] { "El lobo rabioso de Ogham. Encuentra al Conde Geonor" }, waits: true), "accepted at the NPC: not over its options");
            Equal(true, page.Waiting, "it waits");
            Equal(string.Empty, page.News(new[] { "El lobo rabioso de Ogham. Entra en la mansión" }, waits: true), "changed again in the same conversation");
            Equal("El lobo rabioso de Ogham. Entra en la mansión", page.News(new[] { "El lobo rabioso de Ogham. Entra en la mansión" }), "the menu closed: the latest page, whole");
            Equal(false, page.Waiting, "said");
            Equal(string.Empty, page.News(new[] { "El lobo rabioso de Ogham. Entra en la mansión" }, waits: true), "the same page in a menu is no news");
            Equal(false, page.Waiting, "and nothing waits");
        }

        private static void WorldIsBackOnlyOnceNothingCoversIt()
        {
            var gate = new WorldGate();
            Equal(true, gate.InFront(false, 1000), "playing from the start");
            Equal(false, gate.InFront(true, 2000), "a panel opened: quiet at once");
            Equal(false, gate.InFront(false, 2100), "between two panels, before the snapshots show the next one");
            Equal(false, gate.InFront(true, 2200), "the confirmation box shows");
            Equal(false, gate.InFront(false, 2200 + WorldGate.ReturnMs - 1), "the box answered, a moment later");
            Equal(true, gate.InFront(false, 2200 + WorldGate.ReturnMs), "the world again");
        }

        private static void PanelOverTheGameIsTheTopLayerThatIsNotTheHud()
        {
            IntPtr hud = 12, tutorial = 30, panel = 57;
            ScreenLayer Layer(IntPtr address, bool controls = true, bool hints = false, bool isBase = false) =>
                new(address, (int)address, Array.Empty<ScreenText>(), controls, hints, isBase);
            ScreenLayer? Over(params ScreenLayer[] layers) => new ScreenSnapshot(1, layers).PanelOver(hud, tutorial);
            Equal(null, Over(Layer(hud))?.Address, "playing: the HUD on top");
            Equal(null, Over(Layer(hud, isBase: true))?.Address, "playing: the HUD taken as a base layer");
            Equal(null, Over(Layer(hud), Layer(tutorial))?.Address, "the tutorial box covers nothing");
            Equal(null, Over(Layer(hud), Layer(panel, controls: false))?.Address, "a box to read without controls (radial menu) is not a panel");
            Equal(null, Over(Layer(hud), Layer(panel, hints: true))?.Address, "nor a hint bar");
            Equal(panel, Over(Layer(hud), Layer(panel))?.Address, "a panel, focus or not");
            Equal(null, new ScreenSnapshot(1, new[] { Layer(panel) }).PanelOver(IntPtr.Zero, IntPtr.Zero)?.Address, "HUD not found: never silence the game on a guess");
        }

        private static void NearbyThingsWaitForTheCooldown()
        {
            var tracker = new ProximityTracker(200, 300, 60000);
            var checkpoint = new ScanEntry(1, ScanCategory.Waypoints, "Checkpoint", 0, 0, 0);
            string At(float x, long now) => string.Join(", ", tracker.Update(x, 0, new[] { checkpoint }, now).Select(entry => entry.Name));
            Equal("Checkpoint", At(100, 0), "first approach");
            Equal(string.Empty, At(500, 5000), "stepped away while fighting");
            Equal(string.Empty, At(100, 9000), "back within the minute: quiet");
            Equal(string.Empty, At(500, 30000), "away again");
            Equal("Checkpoint", At(100, 70000), "after the cooldown it is news again");

            var items = new ProximityTracker(200, 300, 60000, entry => entry.Category != ScanCategory.Items);
            var bow = new ScanEntry(2, ScanCategory.Items, "Arco corto", 0, 0, 0);
            string ItemAt(float x, long now) => string.Join(", ", items.Update(x, 0, new[] { bow }, now).Select(entry => entry.Name));
            Equal("Arco corto", ItemAt(100, 0), "an item walked up to");
            Equal(string.Empty, ItemAt(500, 3000), "walked away");
            Equal("Arco corto", ItemAt(100, 6000), "walked back: named again, no cooldown for items");
        }

        private static void StickClickWhileHoldingL3()
        {
            var gestures = new L3Gestures();
            var seen = new List<PadGesture>();
            seen.AddRange(gestures.Update(0, true, 0, 0));
            seen.AddRange(gestures.Update(100, true, 0, 0, true));
            seen.AddRange(gestures.Update(200, true, 0, 0, true));
            seen.AddRange(gestures.Update(700, true, 0, 0));
            seen.AddRange(gestures.Update(800, false, 0, 0));
            seen.AddRange(gestures.Update(1300, false, 0, 0));
            Equal("StickClick", string.Join(",", seen), "one click, no hold or tap after it");
        }

        private static void MovedFieldsAreFoundNearestFirst()
        {
            Equal("152,160,144,168,136", string.Join(",", OffsetSearch.Outward(0x98, 0x10, 8)), "where measured first, then one step either side");
            Equal("8,16,0,24", string.Join(",", OffsetSearch.Outward(8, 0x10, 8, 0)), "never below the floor");
            Equal(0x98, OffsetSearch.First(0x98, 0x100, 8, offset => offset == 0x98 || offset == 0xA0) ?? -1, "kept where measured when it still fits");
            Equal(0xA0, OffsetSearch.First(0x98, 0x100, 8, offset => offset is 0xA0 or 0x40) ?? -1, "the nearest fit wins");
            Equal(-8, OffsetSearch.First(0, 0x100, 8, shift => shift == -8, -0x100) ?? 1, "a shift may be negative");
            Equal(true, OffsetSearch.First(0x98, 0x20, 8, offset => offset == 0x200) == null, "nothing within reach: lost");
        }

        private static void TownsAndHideoutsAreToldByTheirRow()
        {
            Equal(true, AreaKind.IsTownOrHideout("G1_town", true), "the encampment: its row's town flag");
            Equal(false, AreaKind.IsTownOrHideout("G1_2", false), "Clearfell");
            Equal(true, AreaKind.IsTownOrHideout("HideoutFelled", false), "a hideout by its Id");
            Equal(false, AreaKind.IsTownOrHideout("MapHideoutFelled", false), "a hideout's map is a map");
        }

        private static void ALayoutLostTooEarlyIsJudgedAgain()
        {
            // 13:27:15 cold start: the PreGameState root had no children yet; the login's root a second later read every field.
            Equal(true, LayoutVerdict.Settled(false, 0, 0), "found at once: kept");
            Equal(false, LayoutVerdict.Settled(true, 1000, 1000), "lost on the first root: judged again on the next");
            Equal(true, LayoutVerdict.Settled(false, 1000, 2000), "found on the login's root: kept, no warning");
            Equal(false, LayoutVerdict.Settled(true, 1000, 1000 + LayoutVerdict.LostWaitMs - 1), "still waiting");
            Equal(true, LayoutVerdict.Settled(true, 1000, 1000 + LayoutVerdict.LostWaitMs), "lost for good: reported");
        }

        private static void MovedControlFieldsAreTrustedOnceTwoControlsAgree()
        {
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            var controls = new ControlFields(new CompatibilityReport(log), () => 0);
            static bool Moved(int shift) => shift == 0x10;
            Equal(0x10, controls.Shift(ControlGroup.Slider, 1, Moved), "a moved slider is read where it fits");
            Equal(0x10, controls.Shift(ControlGroup.Slider, 1, Moved), "the same slider again");
            Equal(0x10, controls.Shift(ControlGroup.Slider, 2, Moved), "a second slider agrees");
            Equal(0x10, controls.Shift(ControlGroup.Slider, 3, _ => false), "trusted once two agreed, even on an unreadable one");
            Equal(0, controls.Current(ControlGroup.ComboBox), "another group is not moved by it");
            var followed = new ControlFields(new CompatibilityReport(log), () => 8);
            Equal(8, followed.Shift(ControlGroup.ComboBox, 1, shift => shift is 0 or 8), "tried first as far as the label text moved");
            Equal(8, followed.Current(ControlGroup.Inventory), "an unjudged group follows the label text");
        }

        private static void TheHoveredPinIsTheKeyThatChangesWithTheCursor()
        {
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);

            // Keys into MapPins on the canvas, by shift from where the hovered one was measured: the current area's 0x20 before it.
            static Func<int, long> Canvas(int hoveredAt, long hoveredRow) => shift => shift == hoveredAt ? hoveredRow : shift == hoveredAt - 0x20 ? 0x5000 : 0;
            var measured = new ControlFields(new CompatibilityReport(log), () => 0);
            Equal(0, measured.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(0, 0x6000)), "read where measured until judged");
            Equal(0, measured.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(0, 0x6100)), "the cursor moves to another pin");
            Equal(true, measured.IsKnown(ControlGroup.HoveredPin), "proved where measured on the first change");

            var moved = new ControlFields(new CompatibilityReport(log), () => 0);
            var rows = new long[] { 0x6000, 0x6100, 0, 0x6200, 0x6300 };
            var shifts = rows.Select(row => moved.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(8, row))).ToList();
            Equal("0,0,0,0,8", string.Join(",", shifts), "after a patch, the key that changed twice from pin to pin (to and from no pin does not count)");
            Equal(8, moved.Current(ControlGroup.HoveredPin), "kept");

            var still = new ControlFields(new CompatibilityReport(log), () => 0);
            foreach (var row in rows)
            {
                still.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(0x40, 0x6000));
            }

            Equal(false, still.IsKnown(ControlGroup.HoveredPin), "a cursor resting on one pin proves nothing, nor the current area's key");
        }
    }
}
