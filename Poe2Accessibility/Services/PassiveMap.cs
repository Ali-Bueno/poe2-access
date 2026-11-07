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
    ///     it go, and so does landing on it. Pure.
    /// </summary>
    public sealed class PassiveFinder
    {
        private static readonly PassiveCategory[] Categories = Enum.GetValues<PassiveCategory>();

        public PassiveCategory Category { get; private set; } = PassiveCategory.Position;

        /// <summary>The node guided to; zero when none.</summary>
        public IntPtr Target { get; private set; }

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
            }
        }

        /// <returns>True when the cursor landed on the node it was guided to, which ends the guide.</returns>
        public bool Arrive(IntPtr hovered)
        {
            if (hovered == IntPtr.Zero || hovered != this.Target)
            {
                return false;
            }

            this.Target = IntPtr.Zero;
            return true;
        }

        private static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
