namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     A graph node's links as the stick finds them: "up right, Torre precursora", clockwise from up, as the passive tree
    ///     says its own (<see cref="PassiveStick"/>). Positions in screen pixels or the graph file's units, y down. Pure.
    /// </summary>
    public static class AtlasLinks
    {
        /// <summary>A link closer than this to the node (a node on the same group centre) has no side to give.</summary>
        private const float SamePlace = 1;

        /// <summary>The links in clockwise order from up, each "direction, label" (or the label alone without a side).</summary>
        public static IReadOnlyList<string> Describe(Vector2? here, IEnumerable<(Vector2? At, string Label)> links)
        {
            var parts = new List<(double Order, string Text)>();
            foreach (var (at, label) in links.Where(link => link.Label.Length > 0))
            {
                if (here is not { } origin || at is not { } there || Vector2.Distance(origin, there) < SamePlace)
                {
                    parts.Add((double.MaxValue, label));
                    continue;
                }

                var offset = there - origin;
                var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
                var clockwiseFromUp = (((112.5 - angle) % 360) + 360) % 360; // from the up sector's left edge, so sectors keep their order
                parts.Add((clockwiseFromUp, $"{Announcements.StickDirections[PassiveStick.Nearest(offset)]}, {label}"));
            }

            return parts.OrderBy(part => part.Order).Select(part => part.Text).ToList();
        }
    }
}
