namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
            (nameof(GeometryShapesAreNamedByTheTablesValues), GeometryShapesAreNamedByTheTablesValues),
            (nameof(GeometryPairsLeaveZerosOutAndReadBack), GeometryPairsLeaveZerosOutAndReadBack),
            (nameof(OnlyMechanismKindsGetALine), OnlyMechanismKindsGetALine),
        };

        private static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

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
