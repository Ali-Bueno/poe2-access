namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     Mektul's lava (docs/research/boss-mechanics.md#mektuls-lava): the extractor's reading of the arena's clip and room, the
    ///     tongue's geometry, the room's fit to the world against the play log of 2026-11-19, and when the warnings speak.
    /// </summary>
    public static class BossFloodTests
    {
        private const string Mektul = "Metadata/Monsters/VaalForgeMaster/VaalForgemaster";

        // The play log of 2026-11-19, The Molten Vault: the lava's object, the sluice lever and the lift's way down.
        private static readonly Vector2 LavaAt = new(12614, 21462);
        private static readonly Vector2 LeverAt = new(11636, 19690);
        private static readonly Vector2 LiftAt = new(11369, 18326);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ARoomLinePlacesTurnsAndScalesItsObject), ARoomLinePlacesTurnsAndScalesItsObject),
            (nameof(AClipWhoseBonesTravelTogetherIsAFlood), AClipWhoseBonesTravelTogetherIsAFlood),
            (nameof(TheShippedLineFitsTheRoomTheLogMet), TheShippedLineFitsTheRoomTheLogMet),
            (nameof(TheTongueHoldsWhatIsBehindItsFrontAndNotBesideIt), TheTongueHoldsWhatIsBehindItsFrontAndNotBesideIt),
            (nameof(WarningsSpeakOncePerCloserBandAndOnStepping), WarningsSpeakOncePerCloserBandAndOnStepping),
        };

        private static void ARoomLinePlacesTurnsAndScalesItsObject()
        {
            var placed = BossFloods.Placements(string.Join('\n',
                "1652 286 0 -1.5708 0 -0 -0.707107 0.707107 0 0 1 475 1 \"Metadata/Effects/arena_lava.ao\" \"Metadata/Monsters/VaalForgeMaster/Lava/LavaVisuals\" 0",
                "1487 287 1 16168.5 3125 -1.57081 0 -0 -0.70711 0.707104 1 0 0 1 \"Metadata/Terrain/Marker.ao\" \"Metadata/Monsters/VaalForgeMaster/Lava/SunAura2\" 0",
                "\"Metadata/Terrain/black_inside_wall.gt\""));
            Equal(2, placed.Count, "two objects, the tile line skipped");
            Near(1652f * 250f / 23f, placed[0].At.X, "a cell's place in world units", 0.5f);
            Near(-1.5708f, placed[0].Rotation, "its turn");
            Equal("Metadata/Effects/arena_lava.ao", placed[0].Ao, "its art");
            Near(16168.5f, placed[1].At.X, "an exact place is taken as written");
            Near(-1.57081f, placed[1].Rotation, "after the exact place");
            var turned = BossFloods.Turn(new Vector2(-166, -1709), -1.5708f);
            Near(-1709f, turned.X, "a quarter turn back takes the model's -Y to the room's -X", 1f);
            Near(166f, turned.Y, "and its -X to the room's +Y", 1f);
        }

        private static void AClipWhoseBonesTravelTogetherIsAFlood()
        {
            var clip = BossFloods.Clip(string.Join('\n', "version 5", "1", "\"base_animation\"", "\tonce", "\t2000", "\t0", "\t0", "\t3 3",
                "\t\t\"front\" xyz 0 0 0 0 -1500 0 0 -3000 0", "\t\t\"left\" xyz 10 0 0 10 -1500 0 50 -2900 0", "\t\t\"right\" xyz -10 0 0 -10 -1500 0 -50 -2900 0"));
            Equal(true, clip != null, "the clip is read");
            var (ms, bones) = clip!.Value;
            Equal(2000, ms, "its length");
            Equal(3, bones.Count, "its bones");
            var lava = new BossFloods.Placement(Vector2.Zero, 0f, 1f, "lava.ao", "Metadata/Test/Lava");
            var room = new[] { lava, new BossFloods.Placement(new Vector2(-1000, -500), 0f, 1f, "lever.ao", "Metadata/Test/Objects/Lever") };
            Equal("Metadata/Test/Lava|2000|0:0;100:50|0:0;0:-1500;0:-3000|Metadata/Test/Objects/Lever@-1000:-500", BossFloods.Flood(lava, ms, bones, room), "the line");
            var still = bones.Select(bone => new BossFloods.Bone(bone.Name, bone.Frames.Select(frame => frame / 10f).ToList())).ToList();
            Equal<string?>(null, BossFloods.Flood(lava, ms, still, room), "a front going a room's tenth is no flood");
        }

        private static void TheShippedLineFitsTheRoomTheLogMet()
        {
            var rule = Shipped();
            Equal(true, rule != null, "Mektul's arena floods");
            var lever = rule!.Anchors.First(anchor => anchor.Path.EndsWith("VaalForgeMasterSluceLever", StringComparison.Ordinal));
            var fit = RoomFit.Of(lever.Room, LeverAt - LavaAt);
            Equal(new RoomFit(0, 1, 1, 0), fit, "the room lies mirrored across its diagonal");
            var lift = rule.Anchors.First(anchor => anchor.Path.EndsWith("BossLiftTransitionDown", StringComparison.Ordinal));
            Equal(true, Vector2.Distance(LavaAt + fit!.Value.ToWorld(lift.Room), LiftAt) < 20f, "the lift lands where the log saw it");
            var front = LavaAt + fit.Value.ToWorld(FloodGeometry.Outline(rule, 0f)[0]);
            Equal(true, Vector2.Distance(front, new Vector2(12780, 19753)) < 5f, $"the pool waits at the dam (12755,19810), front {front}");
            Equal(300f, rule.ClipSeconds, "five minutes to the hall's end");
        }

        private static void TheTongueHoldsWhatIsBehindItsFrontAndNotBesideIt()
        {
            var rule = Shipped()!;
            var axis = Vector2.Normalize(rule.Track[^1] - rule.Track[0]);
            var side = new Vector2(-axis.Y, axis.X);
            var outline = FloodGeometry.Outline(rule, 150f);
            var tip = outline[0];
            Near(Vector2.Distance(rule.Track[150], tip), 0f, "the front is the track's sample at 150 s", 1f);
            var ahead = FloodGeometry.Measure(outline, tip + (axis * 500f));
            Equal(false, ahead.Inside, "ahead of the front is dry");
            Near(500f, ahead.Distance, "half a room ahead", 1f);
            Equal(true, FloodGeometry.Measure(outline, tip - (axis * 1000f)).Inside, "behind it is lava");
            // Beside the track where the front passed 50 s earlier, far behind the tongue's narrowing.
            var beside = FloodGeometry.Measure(outline, rule.Track[100] + (side * 600f));
            Equal(false, beside.Inside, "beside the tongue is dry");
            Near(600f - rule.Outline.Max(point => point.Y), beside.Distance, "its edge, the widest the tongue gets", 5f);
            Equal(true, FloodGeometry.Measure(outline, rule.Track[100] + (side * 300f)).Inside, "inside its width is lava");
        }

        private static void WarningsSpeakOncePerCloserBandAndOnStepping()
        {
            var warnings = new FloodWarnings();
            var heard = new[] { (false, 30), (false, 10), (false, 9), (false, 5), (false, 2), (false, 1), (true, 0), (true, 0), (false, 1), (false, 5), (false, 2) }
                .Select(reading => warnings.Update(reading.Item1, reading.Item2)).ToList();
            FloodNewsKind? near = FloodNewsKind.Near;
            FloodNewsKind? inside = FloodNewsKind.Inside;
            Equal(string.Join(',', new[] { null, near, null, near, near, null, inside, null, null, null, near }), string.Join(',', heard), "bands nearing, stepping in, clear again");
        }

        private static FloodRule? Shipped()
        {
            var directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", BossMechanicsCatalog.FileName)))
            {
                directory = System.IO.Path.GetDirectoryName(directory) ?? throw new System.IO.FileNotFoundException("boss-mechanics.txt is not under any folder above the tests");
            }

            return BossMechanicsCatalog.Load(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog")).Flood(Mektul);
        }
    }
}
