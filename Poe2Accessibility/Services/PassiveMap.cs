namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>The lists of the passive tree's finder (D-pad in the tree); the first says where the cursor is and holds no node.</summary>
    public enum PassiveCategory
    {
        Position,

        /// <summary>The Build Planner's active build: its keystones, notables and jewel sockets not taken yet.</summary>
        Build,
        Keystones,
        Notables,
        JewelSockets,
        Reachable,
    }

    /// <summary>What a passive node is, best first where a list mixes them.</summary>
    public enum PassiveClass
    {
        Keystone,
        Notable,
        JewelSocket,
        Small,
        Other,
    }

    /// <summary>Where a node lies on the passive tree: the points between it and what is taken, and the tree's side and ring. Pure.</summary>
    public static class PassiveMap
    {
        /// <summary>No links lead there from what is taken.</summary>
        public const int NoPath = -1;

        private const int Rings = 3;

        /// <summary>A trail's cost for one point; <see cref="TrailCost"/> takes a little off it to choose between trails as long.</summary>
        private const int Point = 1024;

        /// <summary>For a build's node, a point off the build costs this many on it: the trail keeps to the build where it can.</summary>
        private const int OffBuild = 64;

        /// <returns>The links from the nearest taken node to each node, which is what it costs in points; taken nodes 0.</returns>
        public static int[] Steps(IReadOnlyList<int[]> links, IEnumerable<int> taken)
        {
            var steps = new int[links.Count];
            Array.Fill(steps, NoPath);
            var queue = new Queue<int>();
            foreach (var node in taken)
            {
                if (steps[node] == NoPath)
                {
                    steps[node] = 0;
                    queue.Enqueue(node);
                }
            }

            while (queue.TryDequeue(out var node))
            {
                foreach (var next in links[node])
                {
                    if (steps[next] == NoPath)
                    {
                        steps[next] = steps[node] + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            return steps;
        }

        /// <returns>
        ///     The nodes to take for <paramref name="target"/>, in the order they can be taken: the cheapest line of links from
        ///     what is taken by <paramref name="cost"/> of each node it enters; empty when the target is taken, null when no line leads there.
        /// </returns>
        public static List<int>? Trail(IReadOnlyList<int[]> links, IEnumerable<int> taken, int target, Func<int, int> cost)
        {
            var best = new int[links.Count];
            var from = new int[links.Count];
            Array.Fill(best, int.MaxValue);
            Array.Fill(from, -1);
            var queue = new PriorityQueue<int, int>();
            foreach (var node in taken)
            {
                best[node] = 0;
                queue.Enqueue(node, 0);
            }

            while (queue.TryDequeue(out var node, out var reached))
            {
                if (node == target)
                {
                    break;
                }

                if (reached > best[node])
                {
                    continue; // a cheaper way here came out first
                }

                foreach (var next in links[node])
                {
                    var through = reached + cost(next);
                    if (through < best[next])
                    {
                        (best[next], from[next]) = (through, node);
                        queue.Enqueue(next, through);
                    }
                }
            }

            if (best[target] == int.MaxValue)
            {
                return null;
            }

            var trail = new List<int>();
            for (var node = target; best[node] > 0; node = from[node])
            {
                trail.Add(node);
            }

            trail.Reverse();
            return trail;
        }

        /// <summary>
        ///     What entering a node costs a trail: a point, a little less on the active build and on a notable, so the fewest points win
        ///     and among them the build's nodes and then notables; toward a build's own node a point off the build costs <see cref="OffBuild"/>.
        /// </summary>
        public static int TrailCost(bool inBuild, bool notable, bool targetInBuild) =>
            (targetInBuild && !inBuild ? OffBuild * Point : Point) - (inBuild ? 2 : 0) - (notable ? 1 : 0);

        /// <returns>The side of the tree the node is on (octant counter-clockwise from right) and its ring, 0 innermost.</returns>
        public static (int Octant, int Ring) Zone(IReadOnlyList<Vector2> positions, int node)
        {
            var (low, high) = (positions[0], positions[0]);
            foreach (var position in positions)
            {
                (low, high) = (Vector2.Min(low, position), Vector2.Max(high, position));
            }

            var offset = positions[node] - ((low + high) / 2);
            var radius = MathF.Max(high.X - low.X, high.Y - low.Y) / 2;
            return (PassiveStick.Nearest(offset), radius > 0 ? Math.Min(Rings - 1, (int)(offset.Length() / radius * Rings)) : 0);
        }
    }

    /// <summary>
    ///     The finder's choice: up and down walk the lists, left and right a list's entries, nearest first, both wrapping.
    ///     The entry chosen is the target, with nothing to confirm; going back to <see cref="PassiveCategory.Position"/> lets
    ///     it go. The guide first leads the cursor to the target to read it; landing on it ends the guide when it is taken,
    ///     else the guide goes on along the nodes to take for it (<see cref="Taking"/>) until it is. Pure.
    /// </summary>
    public sealed class PassiveFinder
    {
        private static readonly PassiveCategory[] Categories = Enum.GetValues<PassiveCategory>();

        public PassiveCategory Category { get; private set; } = PassiveCategory.Position;

        /// <summary>The node guided to; zero when none.</summary>
        public IntPtr Target { get; private set; }

        /// <summary>The cursor has been on the target, which is not taken yet: the guide leads to the nodes to take for it.</summary>
        public bool Taking { get; private set; }

        /// <summary>The target is where the cursor was when the tree was last left: landing there ends the guide, nothing to take.</summary>
        public bool Returning { get; private set; }

        /// <param name="step">+1 next, -1 previous.</param>
        /// <param name="members">A list's nodes, nearest first; an empty list is passed over.</param>
        public void ChangeCategory(int step, Func<PassiveCategory, IReadOnlyList<IntPtr>> members)
        {
            for (var tried = 0; tried < Categories.Length; tried++)
            {
                this.Category = Categories[Wrap((int)this.Category + step, Categories.Length)];
                var list = this.Category == PassiveCategory.Position ? Array.Empty<IntPtr>() : members(this.Category);
                if (this.Category == PassiveCategory.Position || list.Count > 0)
                {
                    this.Target = list.Count > 0 ? list[0] : IntPtr.Zero;
                    (this.Taking, this.Returning) = (false, false);
                    return;
                }
            }
        }

        /// <param name="step">+1 next, -1 previous.</param>
        /// <param name="members">The list's nodes now, nearest first: it re-sorts as the cursor moves, the target stays.</param>
        public void ChangeEntry(int step, IReadOnlyList<IntPtr> members)
        {
            if (members.Count > 0)
            {
                var at = members.ToList().IndexOf(this.Target);
                this.Target = members[at < 0 ? 0 : Wrap(at + step, members.Count)];
                (this.Taking, this.Returning) = (false, false);
            }
        }

        /// <param name="hovered">The node under the cursor.</param>
        /// <param name="toTake">The target still has nodes to take: the guide goes on to them.</param>
        /// <returns>True when the cursor landed on the node it was guided to, the first time.</returns>
        public bool Arrive(IntPtr hovered, bool toTake)
        {
            if (hovered == IntPtr.Zero || hovered != this.Target || this.Taking)
            {
                return false;
            }

            if (toTake && !this.Returning)
            {
                this.Taking = true;
            }
            else
            {
                this.Drop();
            }

            return true;
        }

        /// <summary>Ends the guide: the target was taken, or reached on the way back.</summary>
        public void Drop() => (this.Target, this.Taking, this.Returning) = (IntPtr.Zero, false, false);

        /// <summary>Leads back to where the cursor was when the tree was last left; the D-pad's lists start again from Position.</summary>
        public void Return(IntPtr node) => (this.Category, this.Target, this.Taking, this.Returning) = (PassiveCategory.Position, node, false, true);

        private static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
