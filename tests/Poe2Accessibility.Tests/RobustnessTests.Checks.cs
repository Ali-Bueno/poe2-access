namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.Utils;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The second, independent checks of fields that had one protection or none: each from data the plugin already reads.</summary>
    public static partial class RobustnessTests
    {
        private static IEnumerable<(string Name, Action Test)> Checks() => new (string, Action)[]
        {
            (nameof(TheManagerIsThePointerEveryElementSharesAndThatHoldsTheirRoot), TheManagerIsThePointerEveryElementSharesAndThatHoldsTheirRoot),
            (nameof(AMovedLayerRootMustLeadBackToItsManager), AMovedLayerRootMustLeadBackToItsManager),
            (nameof(ARecordsEdgeVectorsAgreeWithTheGraphsList), ARecordsEdgeVectorsAgreeWithTheGraphsList),
            (nameof(ACooldownNamesOneOfTheActorsSkills), ACooldownNamesOneOfTheActorsSkills),
            (nameof(AFieldWithNoShapeKeepsItsRule), AFieldWithNoShapeKeepsItsRule),
            (nameof(ARuleFailsOnlyOnManyEntitiesInTwoAreas), ARuleFailsOnlyOnManyEntitiesInTwoAreas),
        };

        private static void TheManagerIsThePointerEveryElementSharesAndThatHoldsTheirRoot()
        {
            const int follower = 0x90;
            const long manager = 0x7100, vtable = 0x7200, parent = 0x7300;
            static bool HoldsRoot(long pointer) => pointer == manager;
            static byte[] Element(params (int At, long Pointer)[] fields)
            {
                var block = new byte[ElementBytes];
                foreach (var (at, pointer) in fields)
                {
                    BitConverter.TryWriteBytes(block.AsSpan(at), pointer);
                }

                return block;
            }

            int? Find(int managerAt, int nodes = 4, long odd = manager) => ElementFieldSearch.Manager(
                Enumerable.Range(0, nodes).Select(index => Element((0, vtable), (managerAt, index == nodes - 1 ? odd : manager), (0xB8, index == 0 ? 0 : parent))).ToList(),
                HoldsRoot, follower, Radius, Step);
            Equal((int?)follower, Find(follower), "where the control name's shift puts it");
            Equal((int?)(follower + 0x18), Find(follower + 0x18), "moved apart from the name: found on its own");
            Equal((int?)(follower + 0x400), Find(follower + 0x400), "anywhere in the element");
            Equal((int?)null, Find(follower, odd: 0x7400), "one element of another manager: not judged, the follower stands");
            Equal((int?)null, Find(follower, nodes: 2), "two elements are too few");

            // Every element shares its class's table at +0 and most their parent: neither object holds the root.
            static bool NothingHoldsRoot(long pointer) => false;
            var tree = Enumerable.Range(0, 4).Select(_ => Element((0, vtable), (follower, manager))).ToList();
            Equal((int?)null, ElementFieldSearch.Manager(tree, NothingHoldsRoot, follower, Radius, Step), "a pointer all share that does not lead back to the root is no manager");
        }

        private static void AMovedLayerRootMustLeadBackToItsManager()
        {
            const long root = 0x100, container = 0x200, layer = 0x300, manager = 0x900, stale = 0x400;
            var parents = new Dictionary<long, long> { [layer] = container, [container] = root, [root] = 0, [stale] = 0 };
            long ParentOf(long element) => parents.GetValueOrDefault(element);
            Equal(true, LayerSlots.LeadsBack(container, ParentOf, top => manager, held => root), "the container climbs to the root its manager holds");
            Equal(true, LayerSlots.LeadsBack(layer, ParentOf, top => manager, held => root), "from any depth");
            Equal(false, LayerSlots.LeadsBack(stale, ParentOf, top => manager, held => root), "the top of a tree the manager no longer shows");
            Equal(false, LayerSlots.LeadsBack(container, ParentOf, top => 0, held => root), "no manager on its top element");
            Equal(false, LayerSlots.LeadsBack(container, element => container, top => manager, held => root), "parents that never end");
        }

        private static void ARecordsEdgeVectorsAgreeWithTheGraphsList()
        {
            const int inAt = 0x38, outAt = 0x50, bytes = 0x100;
            const long heap = 0x10_0000;
            var memory = new Dictionary<long, long[]>();
            long[] Pointers(long address, int count) => memory.TryGetValue(address, out var found) && found.Length >= count ? found[..count] : Array.Empty<long>();

            // Ten records: record i has i % 3 + 1 edges ending at it and i % 2 + 1 starting at it, each edge (from, to).
            RecordEdgesSearch.Sample Record(int index, int into, int from, bool named = true)
            {
                var record = heap + (index * 0x1000);
                var block = new byte[bytes];
                var (ends, starts) = (index % 3 + 1, index % 2 + 1);
                void Vector(int at, int count, bool ending, long list)
                {
                    BitConverter.TryWriteBytes(block.AsSpan(at), list);
                    BitConverter.TryWriteBytes(block.AsSpan(at + 8), list + (count * 8L));
                    BitConverter.TryWriteBytes(block.AsSpan(at + 16), list + (count * 8L));
                    var edge = list + 0x400;
                    memory[list] = Enumerable.Repeat(edge, count).ToArray();
                    var self = named ? record : record + 8;
                    memory[edge] = ending ? new[] { 0x77L, self } : new[] { self, 0x77L };
                }

                Vector(into, ends, true, record + 0x100);
                Vector(from, starts, false, record + 0x200);
                return new RecordEdgesSearch.Sample(record, block, ends, starts);
            }

            List<RecordEdgesSearch.Sample> Records(int into, int from, bool named = true, int count = 10) =>
                Enumerable.Range(0, count).Select(index => Record(index, into, from, named)).ToList();
            Equal(((int, int)?)(inAt, outAt), RecordEdgesSearch.Find(Records(inAt, outAt), (inAt, outAt), Step, Pointers), "where measured: as many edges as the graph's list says");
            Equal(((int, int)?)(inAt + 0x10, outAt + 0x10), RecordEdgesSearch.Find(Records(inAt + 0x10, outAt + 0x10), (inAt, outAt), Step, Pointers), "both moved together");
            Equal(((int, int)?)(inAt, outAt + 0x60), RecordEdgesSearch.Find(Records(inAt, outAt + 0x60), (inAt, outAt), Step, Pointers), "one moved alone, far");
            Equal(((int, int)?)null, RecordEdgesSearch.Find(Records(inAt, outAt, named: false), (inAt, outAt), Step, Pointers), "vectors of the right length whose edges name another record: one fact");
            Equal(((int, int)?)null, RecordEdgesSearch.Find(Records(inAt, outAt, count: RecordEdgesSearch.MinRecords - 1), (inAt, outAt), Step, Pointers), "too few records to judge");
            Equal((bool?)false, RecordEdgesSearch.Holds(Records(inAt, outAt), outAt, true, Pointers), "the starting edges are not the ending ones");
        }

        private static void AFieldWithNoShapeKeepsItsRule()
        {
            Equal(true, StructShapes.IsFlag(0) && StructShapes.IsFlag(1), "a bool is 0 or 1");
            Equal(false, StructShapes.IsFlag(2) || StructShapes.IsFlag(0xC8), "a byte of a pointer or a count is no bool");
            Equal(true, StructShapes.IsRarity(0, 3) && StructShapes.IsRarity(3, 3), "normal to unique");
            Equal(false, StructShapes.IsRarity(4, 3) || StructShapes.IsRarity(-1, 3) || StructShapes.IsRarity(0x3F800000, 3), "a float or a count is no rarity");
            Equal(true, StructShapes.IsStackCount(1, 20) && StructShapes.IsStackCount(20, 20), "one item to the kind's maximum");
            Equal(false, StructShapes.IsStackCount(0, 20) || StructShapes.IsStackCount(21, 20), "an empty stack, or more than the kind allows");
            Equal(true, StructShapes.IsStackCount(5000, 0), "the maximum unread: only an empty stack is wrong");
            Equal(false, StructShapes.IsStackCount(-3, 0), "a negative count");
            Equal(true, StructShapes.IsCharges(0, 20) && StructShapes.IsCharges(60, 30), "an empty flask and a full one");
            Equal(false, StructShapes.IsCharges(-1, 20) || StructShapes.IsCharges(30, 0) || StructShapes.IsCharges(0x7FF61234, 20), "negative charges, a use that costs nothing, a pointer's half");
            Equal(true, StructShapes.IsBodySize(12f, 12f, 40f) && StructShapes.IsBodySize(0f, 0f, 0f), "a body, and a thing with none");
            Equal(false, StructShapes.IsBodySize(float.NaN, 1f, 1f) || StructShapes.IsBodySize(-4f, 1f, 1f) || StructShapes.IsBodySize(1e30f, 1f, 1f), "no number, a negative size, an absurd one");
        }

        private static void ARuleFailsOnlyOnManyEntitiesInTwoAreas()
        {
            const string rule = "invariant: chest flags";
            const long town = 1, forest = 2, cave = 3;
            var tally = new InvariantTally();
            Equal(InvariantVerdict.NotJudged, tally.Verdict(rule).Verdict, "nothing read");
            for (var chest = 0; chest < InvariantTally.MinEntities - 1; chest++)
            {
                tally.Add(rule, forest, 100 + chest, true);
            }

            Equal(InvariantVerdict.NotJudged, tally.Verdict(rule).Verdict, "four chests are too few");
            tally.Add(rule, forest, 100, true);
            Equal(InvariantVerdict.NotJudged, tally.Verdict(rule).Verdict, "the same chest in the same area counts once");
            tally.Add(rule, forest, 200, true);
            Equal((InvariantVerdict.Holds, 5, 5), tally.Verdict(rule), "five keep it");
            tally.Add(rule, forest, 201, false);
            Equal(InvariantVerdict.NotJudged, tally.Verdict(rule).Verdict, "one in six broken: neither kept nor lost");

            var moved = new InvariantTally();
            for (var chest = 0; chest < 8; chest++)
            {
                moved.Add(rule, forest, 100 + chest, false);
            }

            Equal(InvariantVerdict.NotJudged, moved.Verdict(rule).Verdict, "every chest of one area breaks it: one area is no verdict");
            moved.Add(rule, cave, 300, false);
            Equal((InvariantVerdict.Fails, 0, 9), moved.Verdict(rule), "a second area agrees: the field is not where it is read");
            moved.Clear();
            Equal(InvariantVerdict.NotJudged, moved.Verdict(rule).Verdict, "a new game process starts from nothing");

            // Two flasks are the same two entities in every area: each area's reading counts.
            var flasks = new InvariantTally();
            foreach (var area in new[] { town, forest, cave })
            {
                flasks.Add(rule, area, 7, false);
                flasks.Add(rule, area, 8, false);
            }

            Equal(InvariantVerdict.Fails, flasks.Verdict(rule).Verdict, "two flasks wrong in three areas");

            var odd = new InvariantTally();
            for (var monster = 0; monster < 30; monster++)
            {
                odd.Add(rule, monster < 15 ? forest : cave, 100 + monster, monster >= 3);
            }

            Equal(InvariantVerdict.Holds, odd.Verdict(rule).Verdict, "three in thirty broken (entities freed while read): it holds");
            for (var monster = 0; monster < 6; monster++)
            {
                odd.Add(rule, town, 500 + monster, false);
            }

            Equal(InvariantVerdict.NotJudged, odd.Verdict(rule).Verdict, "nine in thirty-six: a minority never fails a rule");
        }

        private static void ACooldownNamesOneOfTheActorsSkills()
        {
            var skills = new HashSet<uint> { 0x80401040, 0x80401041, 0x80401042 };
            var check = new IdLinkCheck();
            Equal(false, check.Observe(1, Array.Empty<uint>(), skills), "no cooldown entry: nothing to judge");
            Equal(true, check.Observe(1, new uint[] { 0x1234, 0x80401041 }, skills), "one entry names a listed skill: both fields proved");
            Equal(true, check.Proved, "and nothing more is judged");

            var moved = new IdLinkCheck();
            var garbage = new uint[] { 0x3F800000, 0x00000007 };
            for (var reading = 1; reading < IdLinkCheck.ReadingsPerArea; reading++)
            {
                Equal(false, moved.Observe(1, garbage, skills), "no id in common yet says nothing");
            }

            Equal(false, moved.Observe(1, garbage, skills), "one area's readings: not a loss");
            for (var reading = 1; reading < IdLinkCheck.ReadingsPerArea; reading++)
            {
                moved.Observe(2, garbage, skills);
            }

            Equal(true, moved.Observe(2, garbage, skills), "a second area agrees: lost");
            Equal(true, moved.Lost, "one of the two id fields moved");

            var late = new IdLinkCheck();
            for (var reading = 0; reading < IdLinkCheck.ReadingsPerArea; reading++)
            {
                late.Observe(1, garbage, skills);
            }

            Equal(true, late.Observe(2, new uint[] { 0x80401040 }, skills), "a match in the next area: proved, the first area's doubt forgotten");
            Equal(false, late.Lost, "never lost");
        }
    }
}
