namespace GameReader.Utils
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using GameOffsets.Natives;

    /// <summary>Reads one node of a std::map from the game; false when it cannot be read.</summary>
    internal delegate bool StdMapNodeReader<TKey, TValue>(IntPtr address, out StdMapNode<TKey, TValue> node)
        where TKey : struct
        where TValue : struct;

    /// <summary>What a std::map walk gave: the nodes read, whether it reached every node, and whether keys were out of order.</summary>
    internal readonly record struct StdMapWalkResult(int Nodes, bool Complete, bool OutOfOrder);

    /// <summary>Keys at or above <paramref name="Limit"/> are not wanted: the walk may leave the subtrees that hold only those unread.</summary>
    internal sealed record StdMapKeyLimit<TKey>(Func<TKey, ulong> KeyOf, ulong Limit);

    /// <summary>The keys a node may hold given its ancestors: above <see cref="Low"/> and below <see cref="High"/>, when set.</summary>
    internal readonly record struct KeyRange(ulong Low, bool HasLow, ulong High, bool HasHigh)
    {
        public static KeyRange All => default;

        public KeyRange Below(ulong key) => this with { High = key, HasHigh = true };

        public KeyRange Above(ulong key) => this with { Low = key, HasLow = true };
    }

    /// <summary>The rules of a walk bounded by a key limit: an MSVC std::map is ordered, left subtree &lt; node &lt; right subtree.</summary>
    internal static class StdMapPruning
    {
        /// <summary>Whether a node's key lies where its ancestors' keys say it must.</summary>
        public static bool InOrder(ulong key, KeyRange range) =>
            (!range.HasLow || key > range.Low) && (!range.HasHigh || key < range.High);

        /// <summary>Whether a node's right subtree can be left unread: its keys are all above this one, so none is under the limit.</summary>
        public static bool SkipsRight(ulong key, ulong limit) => key >= limit;
    }

    /// <summary>
    ///     When a map read out of order stops being read by its order (pure). One walk out of order is the game moving
    ///     nodes while they were read (seen live in town, 80 s into an area, 2026-11-10): that walk reads every node and
    ///     the next is ordered again. A map that is not ordered by the key at all is out of order on every walk.
    /// </summary>
    internal static class OrderFaults
    {
        /// <summary>Walks out of order in a row that mean the map is not ordered as assumed.</summary>
        public const int MaxInARow = 3;

        /// <summary>Counts a walk and says whether the order is to be given up for the area.</summary>
        public static bool GiveUp(ref int inARow, bool outOfOrder)
        {
            inARow = outOfOrder ? inARow + 1 : 0;
            return inARow >= MaxInARow;
        }
    }

    /// <summary>How long the entities an incomplete walk did not reach keep their state (pure).</summary>
    internal static class IncompleteWalks
    {
        /// <summary>Incomplete walks in a row whose unreached entities are kept; a map broken longer drops them, so it keeps no ghosts.</summary>
        public const int MaxKept = 3;

        /// <summary>Counts a walk and says whether the entities it did not reach keep their state.</summary>
        public static bool KeepUnreached(ref int inARow, bool complete)
        {
            inARow = complete ? 0 : inARow + 1;
            return inARow is > 0 and <= MaxKept;
        }
    }

    /// <summary>
    ///     Walks a std::map's red-black tree as read from the game, breadth first, calling back on every node read. With a key
    ///     limit it leaves unread the right subtree of a node at or above it, checks every node it reads against its ancestors'
    ///     keys, and when one is out of place reads the subtrees it left too, so that walk still reaches every node.
    /// </summary>
    internal sealed class StdMapWalk<TKey, TValue>
        where TKey : struct
        where TValue : struct
    {
        /// <summary>
        ///     A std::map smaller than this is walked on the calling thread: a town holds some 45 entities, and waking the
        ///     pool's workers for them cost more than their reads (profile, 2026-11-08). Fights, with hundreds, stay parallel.
        /// </summary>
        public const int ParallelMinNodes = 128;

        /// <summary>Nodes read on the calling thread before the parallel part: their children seed the workers.</summary>
        private const int SeedNodes = 63;

        /// <summary>
        ///     The seeds a tree full to the seed nodes' depth leaves. Fewer means the walk is nearly over (what the key limit
        ///     leaves of a town's map: 60 to 100 nodes of some 430), and it reads on on the calling thread up to
        ///     <see cref="ParallelMinNodes"/> nodes before any worker is woken, as a map under that size is.
        /// </summary>
        private const int FullSeeds = SeedNodes + 1;

        /// <summary>A worker's queue starts small: one of 2000 nodes was a large-object allocation per worker per walk (profile, 2026-11-10).</summary>
        private const int WorkerQueueNodes = 64;

        private readonly IntPtr head;
        private readonly StdMapNodeReader<TKey, TValue> read;
        private readonly Func<TKey, TValue, bool> onNode;
        private readonly StdMapKeyLimit<TKey>? limit;
        private readonly ConcurrentQueue<(IntPtr Address, KeyRange Range)> skipped = new();
        private int incomplete;
        private int outOfOrder;

        private StdMapWalk(IntPtr head, StdMapNodeReader<TKey, TValue> read, Func<TKey, TValue, bool> onNode, StdMapKeyLimit<TKey>? limit)
        {
            this.head = head;
            this.read = read;
            this.onNode = onNode;
            this.limit = limit;
        }

        /// <summary>Walks <paramref name="map"/>, calling <paramref name="onNode"/> on each node read (concurrently for big maps).</summary>
        public static StdMapWalkResult Run(
            StdMap map,
            int maxSizeAllowed,
            StdMapNodeReader<TKey, TValue> read,
            Func<TKey, TValue, bool> onNode,
            StdMapKeyLimit<TKey>? limit,
            int maxParallelism)
        {
            if (map.Size == 0)
            {
                return new(0, true, false);
            }

            if (map.Size < 0 || map.Size > maxSizeAllowed ||
                !read(map.Head, out var headNode) ||
                !read(headNode.Parent, out var root) || root.IsNil || root.Color > 1)
            {
                return new(0, false, false);
            }

            var walk = new StdMapWalk<TKey, TValue>(map.Head, read, onNode, limit);
            var queue = new Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)>(64);
            queue.Enqueue((root, KeyRange.All));
            int nodes;
            int cap;
            if (map.Size < ParallelMinNodes)
            {
                // A torn tree with a cycle must not walk the full cap on the frame thread: one node past the size is enough.
                cap = Math.Min(maxSizeAllowed, map.Size + 1);
                nodes = walk.Drain(queue, cap);
                walk.FailIfLeft(queue);
            }
            else
            {
                cap = maxSizeAllowed;
                nodes = walk.Drain(queue, SeedNodes);
                if (queue.Count < FullSeeds)
                {
                    nodes += walk.Drain(queue, ParallelMinNodes - SeedNodes);
                }

                nodes += walk.DrainParallel(queue, maxSizeAllowed, maxParallelism);
            }

            if (Volatile.Read(ref walk.outOfOrder) == 1 && !walk.skipped.IsEmpty)
            {
                // The order the limit relied on does not hold: what it left unread is read now, so this walk misses nothing.
                queue.Clear();
                while (walk.skipped.TryDequeue(out var subtree))
                {
                    walk.Descend(queue, subtree.Address, subtree.Range);
                }

                // On the calling thread, the frame's: a map holds no more nodes than its size, so a subtree the game tore
                // into a loop stops there, not at the cap of a big map (a hundred thousand nodes, each an entity read).
                nodes += walk.Drain(queue, Math.Max(0, Math.Min(cap, map.Size + 1) - nodes));
                walk.FailIfLeft(queue);
            }

            return new(nodes, Volatile.Read(ref walk.incomplete) == 0, Volatile.Read(ref walk.outOfOrder) == 1);
        }

        private int DrainParallel(Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)> seeds, int maxSizeAllowed, int maxParallelism)
        {
            if (seeds.Count == 0)
            {
                return 0;
            }

            var capPerSeed = maxSizeAllowed / seeds.Count;
            var nodes = 0;
            Parallel.ForEach(
                seeds,
                new ParallelOptions { MaxDegreeOfParallelism = maxParallelism },
                () => (Queue: new Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)>(WorkerQueueNodes), Nodes: 0),
                (seed, _, _, local) =>
                {
                    local.Queue.Enqueue(seed);
                    local.Nodes += this.Drain(local.Queue, capPerSeed);
                    this.FailIfLeft(local.Queue);
                    return local;
                },
                local => Interlocked.Add(ref nodes, local.Nodes));
            return nodes;
        }

        /// <summary>Visits queued nodes until none is left or <paramref name="cap"/> were visited; returns how many were.</summary>
        private int Drain(Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)> queue, int cap)
        {
            var visited = 0;
            while (visited < cap && queue.TryDequeue(out var item))
            {
                this.Visit(queue, item.Node, item.Range);
                visited++;
            }

            return visited;
        }

        /// <summary>Nodes still queued when a cap was reached were never visited: the walk is incomplete.</summary>
        private void FailIfLeft(Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)> queue)
        {
            if (queue.Count > 0)
            {
                Volatile.Write(ref this.incomplete, 1);
                queue.Clear();
            }
        }

        private void Visit(Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)> queue, StdMapNode<TKey, TValue> node, KeyRange range)
        {
            this.onNode(node.Data.Key, node.Data.Value);
            if (this.limit is not { } bound)
            {
                this.Descend(queue, node.Left, range);
                this.Descend(queue, node.Right, range);
                return;
            }

            var key = bound.KeyOf(node.Data.Key);
            if (!StdMapPruning.InOrder(key, range))
            {
                Volatile.Write(ref this.outOfOrder, 1);
            }

            this.Descend(queue, node.Left, range.Below(key));
            if (Volatile.Read(ref this.outOfOrder) == 0 && StdMapPruning.SkipsRight(key, bound.Limit))
            {
                if (node.Right != this.head)
                {
                    this.skipped.Enqueue((node.Right, range.Above(key)));
                }
            }
            else
            {
                this.Descend(queue, node.Right, range.Above(key));
            }
        }

        private void Descend(Queue<(StdMapNode<TKey, TValue> Node, KeyRange Range)> queue, IntPtr child, KeyRange range)
        {
            // A leaf's children point at the head node (MSVC's nil sentinel): known without a read, which halved the walk's
            // reads (a town's 250 nodes cost 750 reads a frame, 2026-11-08).
            if (child == this.head)
            {
                return;
            }

            // The tree is live and the game rebalances it while it is read: a child that cannot be read, or reads as nil or
            // with a colour other than red/black (string or float data behind a torn pointer), is a node the walk misses.
            if (this.read(child, out var node) && !node.IsNil && node.Color <= 1)
            {
                queue.Enqueue((node, range));
            }
            else
            {
                Volatile.Write(ref this.incomplete, 1);
            }
        }
    }
}
