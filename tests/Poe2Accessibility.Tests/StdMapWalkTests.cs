namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using GameOffsets.Natives;
    using GameReader.Utils;
    using static Check;

    /// <summary>The host's std::map walk (the awake entities) on trees built in memory: pruning, its order check, completeness.</summary>
    public static class StdMapWalkTests
    {
        private const ulong Visuals = 40;

        /// <summary>A limit that leaves more than the parallel walk's seed nodes to read in a big map.</summary>
        private const ulong ManyUnder = 400;

        /// <summary>A limit that leaves a few nodes more than the seed nodes to read in a big map, as a town's does.</summary>
        private const ulong TownUnder = 71;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AWalkWithoutLimitReadsEveryNodeOnce), AWalkWithoutLimitReadsEveryNodeOnce),
            (nameof(AnOrderedMapIsReadUpToTheLimit), AnOrderedMapIsReadUpToTheLimit),
            (nameof(ABigOrderedMapIsReadUpToTheLimitInParallel), ABigOrderedMapIsReadUpToTheLimitInParallel),
            (nameof(ABigMapTheLimitCutsToATownIsReadOnTheCallingThread), ABigMapTheLimitCutsToATownIsReadOnTheCallingThread),
            (nameof(AMapOutOfOrderIsReadWhole), AMapOutOfOrderIsReadWhole),
            (nameof(ABigMapOutOfOrderIsReadWhole), ABigMapOutOfOrderIsReadWhole),
            (nameof(ALimitUnderEveryKeyReadsTheLeftEdgeOnly), ALimitUnderEveryKeyReadsTheLeftEdgeOnly),
            (nameof(ALimitOverEveryKeyReadsEverything), ALimitOverEveryKeyReadsEverything),
            (nameof(AnEmptyMapIsCompleteWithoutReads), AnEmptyMapIsCompleteWithoutReads),
            (nameof(ASingleNodeIsReadAlone), ASingleNodeIsReadAlone),
            (nameof(ANodeThatCannotBeReadMakesTheWalkIncomplete), ANodeThatCannotBeReadMakesTheWalkIncomplete),
            (nameof(ATreeLargerThanItsSizeIsCutShortAndSaysSo), ATreeLargerThanItsSizeIsCutShortAndSaysSo),
            (nameof(TheParallelPartOfABigMapIsBoundBySizeToo), TheParallelPartOfABigMapIsBoundBySizeToo),
            (nameof(UnreachedEntitiesAreKeptForThreeIncompleteWalks), UnreachedEntitiesAreKeptForThreeIncompleteWalks),
            (nameof(OneWalkOutOfOrderDoesNotGiveTheOrderUp), OneWalkOutOfOrderDoesNotGiveTheOrderUp),
        };

        private static void AWalkWithoutLimitReadsEveryNodeOnce()
        {
            var map = new FakeMap(Range(1, 100));
            var walk = map.Walk(null);
            Expect(true, walk.Complete, "complete");
            Equal(100, walk.Nodes, "nodes");
            Expect(string.Join(",", Range(1, 100)), string.Join(",", map.Seen.Order()), "every key once");
            Equal(101, map.Reads, "the head and each node once, nil children unread");
        }

        private static void AnOrderedMapIsReadUpToTheLimit()
        {
            var map = new FakeMap(Range(1, 100));
            var walk = map.Walk(Visuals);
            Expect(true, walk.Complete, "complete");
            Expect(false, walk.OutOfOrder, "in order");
            Expect(string.Join(",", Range(1, 39)), string.Join(",", map.Seen.Where(key => key < Visuals).Order()), "every key under the limit once");
            Expect(true, map.Reads <= 39 + map.Depth + 1, $"only the boundary's path past the limit ({map.Reads} reads)");
        }

        private static void ABigOrderedMapIsReadUpToTheLimitInParallel()
        {
            var map = new FakeMap(Range(1, 600));
            var walk = map.Walk(ManyUnder);
            Expect(true, walk.Complete, "complete");
            Expect(false, walk.OutOfOrder, "in order");
            Expect(string.Join(",", Range(1, 399)), string.Join(",", map.Seen.Where(key => key < ManyUnder).Order()), "every key under the limit once");
            Expect(true, map.Reads <= 399 + map.Depth + 1, $"only the boundary's path past the limit ({map.Reads} reads)");
        }

        private static void ABigMapTheLimitCutsToATownIsReadOnTheCallingThread()
        {
            // A town: some 430 nodes, 70 of them under the visuals' ids. The walk reads a few more than its seed nodes.
            var map = new FakeMap(Range(1, 430));
            var walk = map.Walk(TownUnder);
            Expect(true, walk.Complete, "complete");
            Expect(false, walk.OutOfOrder, "in order");
            Expect(string.Join(",", Range(1, (int)TownUnder - 1)), string.Join(",", map.Seen.Where(key => key < TownUnder).Order()), "every key under the limit once");
            Expect(true, map.Reads > 64, $"more than the seed nodes were read ({map.Reads} reads)");
            Expect(Environment.CurrentManagedThreadId.ToString(), string.Join(",", map.Threads.Order()), "no worker was woken for the few nodes left");
        }

        private static void AMapOutOfOrderIsReadWhole()
        {
            // Key 5 sits where 90 belongs, in a subtree the limit would leave unread; 90 sits in its place.
            var keys = Range(1, 100).ToArray();
            (keys[4], keys[89]) = (keys[89], keys[4]);
            var map = new FakeMap(keys);
            var walk = map.Walk(Visuals);
            Expect(true, walk.OutOfOrder, "out of order");
            Expect(true, walk.Complete, "complete");
            Expect(string.Join(",", Range(1, 100)), string.Join(",", map.Seen.Order()), "every key, each once");
        }

        private static void ABigMapOutOfOrderIsReadWhole()
        {
            var keys = Range(1, 600).ToArray();
            (keys[4], keys[550]) = (keys[550], keys[4]);
            var map = new FakeMap(keys);
            var walk = map.Walk(ManyUnder);
            Expect(true, walk.OutOfOrder, "out of order");
            Expect(true, walk.Complete, "complete");
            Expect(string.Join(",", Range(1, 600)), string.Join(",", map.Seen.Order()), "every key, each once");
        }

        private static void ALimitUnderEveryKeyReadsTheLeftEdgeOnly()
        {
            var map = new FakeMap(Range(100, 199));
            var walk = map.Walk(Visuals);
            Expect(true, walk.Complete, "complete");
            Equal(map.LeftEdge, map.Seen.Count, "the left edge, down to the smallest key");
            Equal(map.LeftEdge + 1, map.Reads, "the head and the left edge");
        }

        private static void ALimitOverEveryKeyReadsEverything()
        {
            var map = new FakeMap(Range(1, 30));
            var walk = map.Walk(1000);
            Expect(true, walk.Complete, "complete");
            Equal(30, map.Seen.Count, "every key");
            Equal(31, map.Reads, "the head and each node once");
        }

        private static void AnEmptyMapIsCompleteWithoutReads()
        {
            var map = new FakeMap(Array.Empty<ulong>());
            var walk = map.Walk(Visuals);
            Expect(true, walk.Complete, "complete");
            Equal(0, walk.Nodes, "nodes");
            Equal(0, map.Reads, "reads");
        }

        private static void ASingleNodeIsReadAlone()
        {
            foreach (var limit in new ulong?[] { null, 3, 1000 })
            {
                var map = new FakeMap(new ulong[] { 7 });
                var walk = map.Walk(limit);
                Expect(true, walk.Complete, $"complete (limit {limit})");
                Expect("7", string.Join(",", map.Seen), $"the node (limit {limit})");
                Equal(2, map.Reads, $"the head and the node (limit {limit})");
            }
        }

        private static void ANodeThatCannotBeReadMakesTheWalkIncomplete()
        {
            foreach (var count in new[] { 100, 600 })
            {
                var map = new FakeMap(Range(1, count));
                map.Break(10);
                var walk = map.Walk(null);
                Expect(false, walk.Complete, $"incomplete ({count} keys)");
                Expect(false, map.Seen.Contains(10), $"the broken node unseen ({count} keys)");
                Expect(true, map.Seen.Count is > 1 && map.Seen.Count < count, $"the rest still read ({count} keys)");
            }
        }

        private static void ATreeLargerThanItsSizeIsCutShortAndSaysSo()
        {
            var map = new FakeMap(Range(1, 100), size: 50);
            var walk = map.Walk(null);
            Expect(false, walk.Complete, "incomplete");
            Equal(51, walk.Nodes, "one node past the size");
        }

        private static void TheParallelPartOfABigMapIsBoundBySizeToo()
        {
            // A torn big map holding more nodes than its size says: the parallel part must stop near the size, not at 100,000.
            var map = new FakeMap(Range(1, 20000), size: 300);
            var walk = map.Walk(null);
            Expect(false, walk.Complete, "incomplete");
            Expect(true, walk.Nodes <= 5 * 301, $"bound near the size ({walk.Nodes} nodes)");
        }

        private static void UnreachedEntitiesAreKeptForThreeIncompleteWalks()
        {
            var inARow = 0;
            Expect(false, IncompleteWalks.KeepUnreached(ref inARow, true), "a complete walk keeps nothing");
            for (var walk = 1; walk <= IncompleteWalks.MaxKept; walk++)
            {
                Expect(true, IncompleteWalks.KeepUnreached(ref inARow, false), $"incomplete walk {walk} keeps them");
            }

            Expect(false, IncompleteWalks.KeepUnreached(ref inARow, false), "a fourth in a row drops them");
            Expect(false, IncompleteWalks.KeepUnreached(ref inARow, true), "a complete walk");
            Expect(true, IncompleteWalks.KeepUnreached(ref inARow, false), "counting starts over after a complete walk");
        }

        private static void OneWalkOutOfOrderDoesNotGiveTheOrderUp()
        {
            var inARow = 0;
            Expect(false, OrderFaults.GiveUp(ref inARow, false), "an ordered walk");
            Expect(false, OrderFaults.GiveUp(ref inARow, true), "one walk out of order: the game was moving nodes");
            Expect(false, OrderFaults.GiveUp(ref inARow, false), "ordered again");
            Expect(false, OrderFaults.GiveUp(ref inARow, true), "counting starts over");
            Expect(false, OrderFaults.GiveUp(ref inARow, true), "two in a row");
            Expect(true, OrderFaults.GiveUp(ref inARow, true), "three in a row: the map is not ordered by its key");
        }

        private static IEnumerable<ulong> Range(int first, int last) => Enumerable.Range(first, last - first + 1).Select(key => (ulong)key);

        /// <summary>A balanced tree laid out as MSVC's std::map: a head node (nil) whose parent is the root, leaves pointing back at it.</summary>
        private sealed class FakeMap
        {
            private const long Heap = 0x20_0000_0000;
            private const long NodeSize = 0x100;
            private readonly Dictionary<IntPtr, StdMapNode<ulong, int>> nodes = new();
            private readonly HashSet<IntPtr> broken = new();
            private readonly IntPtr head = new(Heap);
            private readonly int size;
            private int reads;

            public FakeMap(IEnumerable<ulong> inOrder, int? size = null)
            {
                var keys = inOrder.ToArray();
                this.size = size ?? keys.Length;
                var root = this.Build(keys, 0, keys.Length - 1);
                this.nodes[this.head] = new StdMapNode<ulong, int> { Parent = root, Left = this.head, Right = this.head, IsNil = true, Color = 1 };
                this.Depth = keys.Length == 0 ? 0 : (int)Math.Ceiling(Math.Log2(keys.Length + 1));
                for (var node = root; node != this.head; node = this.nodes[node].Left)
                {
                    this.LeftEdge++;
                }
            }

            public int Depth { get; }

            public int LeftEdge { get; }

            public int Reads => this.reads;

            public List<ulong> Seen { get; } = new();

            /// <summary>The threads that read a node.</summary>
            public HashSet<int> Threads { get; } = new();

            public void Break(int index) => this.broken.Add(Address(index - 1));

            public StdMapWalkResult Walk(ulong? limit) => StdMapWalk<ulong, int>.Run(
                new StdMap { Head = this.head, Size = this.size },
                100000,
                this.Read,
                (key, _) =>
                {
                    lock (this.Seen)
                    {
                        this.Seen.Add(key);
                    }

                    return true;
                },
                limit is { } below ? new StdMapKeyLimit<ulong>(key => key, below) : null,
                4);

            private static IntPtr Address(int index) => new(Heap + (index + 1) * NodeSize);

            private bool Read(IntPtr address, out StdMapNode<ulong, int> node)
            {
                Interlocked.Increment(ref this.reads);
                lock (this.Threads)
                {
                    this.Threads.Add(Environment.CurrentManagedThreadId);
                }

                node = default;
                return !this.broken.Contains(address) && this.nodes.TryGetValue(address, out node);
            }

            private IntPtr Build(ulong[] keys, int low, int high)
            {
                if (low > high)
                {
                    return this.head;
                }

                var middle = (low + high) / 2;
                var address = Address(middle);
                this.nodes[address] = new StdMapNode<ulong, int>
                {
                    Left = this.Build(keys, low, middle - 1),
                    Right = this.Build(keys, middle + 1, high),
                    Color = (byte)(middle % 2),
                    Data = new StdMapNodeData<ulong, int> { Key = keys[middle], Value = middle },
                };
                return address;
            }
        }
    }
}
