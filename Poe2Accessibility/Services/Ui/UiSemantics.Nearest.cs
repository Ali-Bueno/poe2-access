namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>Pure geometry of <see cref="UiSemantics"/> (compiled into the tests).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Index of the first rect holding the point, else of the rect whose centre is nearest; -1 when there are none.</summary>
        internal static int NearestRect(IReadOnlyList<(Vector2 Position, Vector2 Size)> rects, Vector2 point)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < rects.Count; i++)
            {
                var (position, size) = rects[i];
                if (point.X >= position.X && point.X <= position.X + size.X && point.Y >= position.Y && point.Y <= position.Y + size.Y)
                {
                    return i;
                }

                var distance = Vector2.Distance(point, position + (size / 2));
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (i, distance);
                }
            }

            return best;
        }
    }
}
