namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The telegraph extractor's pure parsing: effect-driven shapes, geometry shape names and catalog lines.</summary>
    public static class TelegraphTests
    {
        private const string BloodDash = "version 3\nextends \"Metadata/Parent\"\n\nAnimationController\n{\n\tmetadata = \"x.amd\"\n}\n\nEffectDrivenEvent\n{\n\tanimation = \"animate\"\n" +
            "\t\tshape = \"rectangle\"\n\t\t\ttime = 0.010\n\t\t\twave_time = 0.330\n\t\t\twave_interval = 0.033\n\t\t\torientation_to_effect = 90.000\n\t\t\tlength = 120\n\t\t\twidth = 13\n" +
            "\t\tshape = \"rectangle\"\n\t\t\ttime = 0.010\n\t\t\torientation_to_effect = -90.000\n\t\t\tlength = 120\n\t\t\twidth = 13\n}\n\nclient\n{\n\tRender {}\n}\n";

        private const string Cascade = "version 3\n\nEffectDrivenEvent\n{\n\tanimation = \"animate\"\n\t\tshape = \"circle\"\n\t\t\ttime = 0.750\n\t\t\tlength = 10\n\t\t\twidth = 10\n" +
            "\tanimation = \"second\"\n\t\tshape = \"Cone\"\n\t\t\ttime = 1.5\n\t\t\tangle = 60\n}\n";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EachShapeLineStartsAShapeWithItsSettings), EachShapeLineStartsAShapeWithItsSettings),
            (nameof(ShapesBelongToTheAnimationNamedBeforeThem), ShapesBelongToTheAnimationNamedBeforeThem),
            (nameof(AFileWithoutTheBlockHasNoShapes), AFileWithoutTheBlockHasNoShapes),
            (nameof(TheFirstHitIsTheEarliestShapeInMs), TheFirstHitIsTheEarliestShapeInMs),
            (nameof(AShapeIsWrittenAsItsKindTimeAndSize), AShapeIsWrittenAsItsKindTimeAndSize),
            (nameof(APlacedOrSweepingShapeSaysWhereItLiesAndHowLongItSweeps), APlacedOrSweepingShapeSaysWhereItLiesAndHowLongItSweeps),
            (nameof(ASpawnedMonstersSettingsAreNotTheShapes), ASpawnedMonstersSettingsAreNotTheShapes),
            (nameof(TheTwinWavesAreLeftSideways), TheTwinWavesAreLeftSideways),
            (nameof(TheTripleWavesLeaveAGapBetweenThem), TheTripleWavesLeaveAGapBetweenThem),
            (nameof(GeometryShapesAreNamedByTheTablesValues), GeometryShapesAreNamedByTheTablesValues),
            (nameof(GeometryPairsLeaveZerosOutAndReadBack), GeometryPairsLeaveZerosOutAndReadBack),
            (nameof(OnlyMechanismKindsGetALine), OnlyMechanismKindsGetALine),
        };

        private static void EachShapeLineStartsAShapeWithItsSettings()
        {
            var shapes = TelegraphShapes.ParseEffectDrivenEvents(BloodDash);
            Expect(2, shapes.Count, "two rectangles");
            Expect("rectangle", shapes[0].Shape, "kind");
            Expect(0.010, TelegraphShapes.Number(shapes[0], "time"), "time of the first");
            Expect(120.0, TelegraphShapes.Number(shapes[0], "length"), "length");
            Expect(13.0, TelegraphShapes.Number(shapes[1], "width"), "width of the second");
            Expect(-90.0, TelegraphShapes.Number(shapes[1], "orientation_to_effect"), "the second's own orientation");
            Expect(null, TelegraphShapes.Number(shapes[1], "wave_time"), "a setting of the first is not the second's");
        }

        private static void ShapesBelongToTheAnimationNamedBeforeThem()
        {
            var shapes = TelegraphShapes.ParseEffectDrivenEvents(Cascade);
            Expect(2, shapes.Count, "two shapes");
            Expect("animate", shapes[0].Animation, "first animation");
            Expect("second", shapes[1].Animation, "second animation");
            Expect("cone", shapes[1].Shape, "shape names are lower case");
            Expect(60.0, TelegraphShapes.Number(shapes[1], "angle"), "angle");
        }

        private static void AFileWithoutTheBlockHasNoShapes()
        {
            Expect(0, TelegraphShapes.ParseEffectDrivenEvents("version 3\n\nAnimationController\n{\n\tshape = \"circle\"\n}\n").Count, "a shape outside the block");
            Expect(0, TelegraphShapes.ParseEffectDrivenEvents(string.Empty).Count, "empty file");
            Expect(null, TelegraphShapes.FirstHitMs(Array.Empty<EffectShape>()), "no hit");
        }

        private static void TheFirstHitIsTheEarliestShapeInMs()
        {
            Expect(750, TelegraphShapes.FirstHitMs(TelegraphShapes.ParseEffectDrivenEvents(Cascade)), "750 ms, not the cone's 1500");
            Expect(10, TelegraphShapes.FirstHitMs(TelegraphShapes.ParseEffectDrivenEvents(BloodDash)), "10 ms");
        }

        private static void AShapeIsWrittenAsItsKindTimeAndSize()
        {
            var shapes = TelegraphShapes.ParseEffectDrivenEvents(Cascade);
            Expect("circle:750:10:10:", TelegraphShapes.Format(shapes[0]), "a circle");
            Expect("cone:1500:::60", TelegraphShapes.Format(shapes[1]), "a cone with only an angle");
        }

        private static void APlacedOrSweepingShapeSaysWhereItLiesAndHowLongItSweeps()
        {
            var shapes = TelegraphShapes.ParseEffectDrivenEvents(BloodDash);
            Expect("rectangle:10:120:13::::90:330", TelegraphShapes.Format(shapes[0]), "turned and sweeping");
            Expect("rectangle:10:120:13::::-90:", TelegraphShapes.Format(shapes[1]), "turned only");
        }

        private static void ASpawnedMonstersSettingsAreNotTheShapes()
        {
            var shapes = TelegraphShapes.ParseEffectDrivenEvents(
                "version 3\n\nEffectDrivenEvent\n{\n\tanimation = \"once\"\n\t\tshape = \"circle\"\n\t\t\ttime = 1\n\t\t\tlength = 5\n\t\tmonster = \"M\"\n\t\t\ttime = 2\n\t\t\toffset_y = 250\n}\n");
            Expect("circle:1000:5::", TelegraphShapes.Format(shapes[0]), "the shape's own time and no offset");
        }

        private static IReadOnlyList<EffectZone> Zones(string shapes, System.Numerics.Vector2 at, float rotation)
        {
            var areas = new EffectAreas();
            areas.Add(new[] { "# header", "X/Wave.ao|effect|" + shapes });
            return areas.Of("x/wave.ao")!.Select(shape => new EffectZone(shape, at, rotation)).ToList();
        }

        private static void TheTwinWavesAreLeftSideways()
        {
            // Diamora's L_twin_wave_crash_01: two waves 37 cells wide from 68 cells on each side along the effect's x axis.
            var at = new System.Numerics.Vector2(10000f, 6000f);
            var zones = Zones("rectangle:1550:68:37::-68::90:1550,rectangle:1550:68:37::68::-90:1550,rectangle:3100:60:38::-30::90:", at, MathF.PI / 2f);
            var along = new System.Numerics.Vector2(0f, 1f); // a quarter turn: the x axis lies along the world's y
            var across = new System.Numerics.Vector2(1f, 0f);
            Expect(true, zones.Take(2).All(zone => zone.Catches(at, 40f)), "both waves meet at the middle");
            Expect(true, zones.Any(zone => zone.Catches(at + (along * 600f), 40f)), "600 along is still in a wave");
            Expect(false, zones.Any(zone => zone.Catches(at + (across * 260f), 40f)), "260 sideways is out");
            Expect(3100, zones[0].StrikesIn(at, 0), "the middle is struck at the end of the sweep");
            var fronts = zones.Take(2).Select(zone => zone.Front(at, 2325)!.Value).ToList();
            Expect(true, fronts.All(front => MathF.Abs(front.Ahead - (34f * 250f / 23f)) < 1f), "halfway through the sweep each front is half its way off");
            Expect(true, MathF.Abs(System.Numerics.Vector2.Distance(fronts[0].At, fronts[1].At) - (68f * 250f / 23f)) < 1f, "one on each side");
            Expect(true, zones[2].Front(at, 0) == null, "the crash does not sweep");
        }

        private static void TheTripleWavesLeaveAGapBetweenThem()
        {
            // L_triplet_wave_crash_01: from -x and from (37, 60) and (37, -60): in the model, half a turn from the shapes, from +x.
            var at = System.Numerics.Vector2.Zero;
            var zones = Zones("rectangle:1550:68:37::-68::90:1550,rectangle:1550:68:37::37:60:210:1550,rectangle:1550:68:37::37:-60:330:1550", at, 0f);
            Expect(true, zones[0].Catches(new System.Numerics.Vector2(600f, 0f), 0f), "the first comes from the world's +x");
            Expect(true, zones[1].Catches(new System.Numerics.Vector2(-370f, -600f), 0f), "the second from between -x and -y");
            Expect(false, zones.Any(zone => zone.Catches(new System.Numerics.Vector2(-500f, 0f), 40f)), "the gap opposite the first wave");
        }

        private static void GeometryShapesAreNamedByTheTablesValues()
        {
            Expect("circle", TelegraphShapes.GeometryShape(1), "1");
            Expect("cone", TelegraphShapes.GeometryShape(2), "2");
            Expect("rectangle", TelegraphShapes.GeometryShape(3), "3");
            Expect("donut", TelegraphShapes.GeometryShape(4), "4");
            Expect("triangle", TelegraphShapes.GeometryShape(5), "5");
            Expect(null, TelegraphShapes.GeometryShape(0), "0");
            Expect(null, TelegraphShapes.GeometryShape(6), "a value the table never used");
        }

        private static void GeometryPairsLeaveZerosOutAndReadBack()
        {
            var field = TelegraphShapes.PairsOf(("length", 92), ("width", 8), ("angle", 0), ("delay", 33));
            Expect("length=92;width=8;delay=33", field, "zeros left out");
            var pairs = TelegraphShapes.Pairs("shape=rectangle;" + field);
            Expect("rectangle", pairs["shape"], "shape");
            Expect("33", pairs["delay"], "delay");
            Expect(false, pairs.ContainsKey("angle"), "angle absent");
            Expect(5, TelegraphShapes.Fields("GABaronWolfDashDangerRectangle|geometry|shape=rectangle;width=15|a.ao,b.ao|").Length, "five fields, the last empty");
        }

        private static void OnlyMechanismKindsGetALine()
        {
            Expect("geometry", TelegraphShapes.KindOf("geometry_attack"), "geometry attack");
            Expect("geometry", TelegraphShapes.KindOf("geometry_spell_channelled"), "channelled spell");
            Expect("trigger", TelegraphShapes.KindOf("geometry_trigger_attack"), "trigger");
            Expect("effect", TelegraphShapes.KindOf("effect_driven_spell"), "effect driven");
            Expect("spawn", TelegraphShapes.KindOf("spawn_object_mtx_pale_boss"), "spawn");
            Expect("mortar", TelegraphShapes.KindOf("monster_mortar_attack"), "mortar");
            Expect("mortar", TelegraphShapes.KindOf("geometry_mortars_spell"), "geometry mortar");
            Expect(null, TelegraphShapes.KindOf("melee"), "melee is timed by hit-times, not drawn");
            Expect(null, TelegraphShapes.KindOf("geometry_projectiles_spell"), "geometry projectiles are projectiles");
        }
    }
}
