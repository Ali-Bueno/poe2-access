namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The Trial of the Sekhemas' links out of the lines the game draws between the rooms of its map: a second source,
    ///     apart from the floor (<see cref="TrialFloorLocator"/>), and the very thing on screen. The canvas of the map
    ///     window keeps one segment a link, two points, each end cut short by the same distance from the centre of its room
    ///     (measured 2026-11-12 on 45 lines, docs/research/trials-and-campaign.md §1.5). Nothing of the picture is assumed
    ///     but that: the columns stand left to right and a column's rooms top to bottom, as the window lists them. How far
    ///     the ends are cut, and where the rooms' centres are, come out of the lines themselves. Pure.
    /// </summary>
    public static class TrialMapLines
    {
        /// <summary>How much of an element of the map window is searched for the lines.</summary>
        public const int Window = 0x800;

        // Two points of two floats each: a line is no smaller, and none was seen larger than 0x18 bytes.
        private const int MinLineSize = 4 * sizeof(float);
        private const int MaxLineSize = 0x40;

        // Rooms stand further apart than the cut around each (their icons do not touch): centres closer than a part of it are one room.
        private const double SameRoom = 0.25;

        // Lines of one pair of columns share their middle; the middles of two pairs stand a column apart.
        private const double SameGap = 0.25;

        /// <summary>A line as drawn: from a room to one of the next column, left to right.</summary>
        public readonly record struct Line(float X1, float Y1, float X2, float Y2);

        /// <summary>
        ///     The links these lines draw: for each column but the last, each room's ways into the next. Null unless they
        ///     are the lines of a map of these rooms (<see cref="TrialMap.Fits"/>).
        /// </summary>
        /// <param name="rooms">The rooms of each column, as the window shows them.</param>
        public static IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? Links(IReadOnlyList<Line> lines, IReadOnlyList<int> rooms)
        {
            var gaps = rooms.Count - 1;
            if (gaps < 2 || lines.Count < gaps ||
                lines.Any(line => !(float.IsFinite(line.X1) && float.IsFinite(line.Y1) && float.IsFinite(line.X2) && float.IsFinite(line.Y2)) || line.X2 <= line.X1))
            {
                return null;
            }

            // Which pair of columns each line joins: the cut is the same at both ends, so the lines of a pair share their middle.
            if (Split(lines.Select(line => (line.X1 + line.X2) / 2.0).ToList(), gaps) is not { } gapOf)
            {
                return null;
            }

            // How far from a room's centre a line ends. Between two columns a fixed distance apart, a line's width is that
            // distance less twice the cut along it: the steeper the line, the wider. The slope of width over steepness is the cut.
            var widths = lines.Select(line => (double)line.X2 - line.X1).ToList();
            var along = lines.Select((line, each) => widths[each] / Math.Sqrt((widths[each] * widths[each]) + Math.Pow((double)line.Y2 - line.Y1, 2))).ToList();
            double over = 0, under = 0;
            for (var gap = 0; gap < gaps; gap++)
            {
                var own = Enumerable.Range(0, lines.Count).Where(each => gapOf[each] == gap).ToList();
                var (meanAlong, meanWidth) = (own.Average(each => along[each]), own.Average(each => widths[each]));
                over += own.Sum(each => (along[each] - meanAlong) * (widths[each] - meanWidth));
                under += own.Sum(each => (along[each] - meanAlong) * (along[each] - meanAlong));
            }

            var cut = under > 0 ? -over / (2 * under) : 0;
            if (!(cut > 0) || double.IsInfinity(cut))
            {
                return null; // every line as steep as the others, or ends that are not cut around a centre: not these lines
            }

            // Each end's room: the centres of a column's rooms, top to bottom.
            var from = new int[lines.Count];
            var to = new int[lines.Count];
            for (var column = 0; column < rooms.Count; column++)
            {
                var ends = new List<(int Line, bool Start, double Y)>();
                for (var each = 0; each < lines.Count; each++)
                {
                    var down = ((double)lines[each].Y2 - lines[each].Y1) / (widths[each] / along[each]);
                    if (gapOf[each] == column)
                    {
                        ends.Add((each, true, lines[each].Y1 - (cut * down)));
                    }
                    else if (gapOf[each] == column - 1)
                    {
                        ends.Add((each, false, lines[each].Y2 + (cut * down)));
                    }
                }

                if (Group(ends.Select(end => end.Y).ToList(), cut * SameRoom) is not { } rowOf || rowOf.Max() + 1 != rooms[column])
                {
                    return null;
                }

                for (var each = 0; each < ends.Count; each++)
                {
                    (ends[each].Start ? from : to)[ends[each].Line] = rowOf[each];
                }
            }

            var links = Enumerable.Range(0, gaps).Select(gap => (IReadOnlyList<IReadOnlyList<int>>)Enumerable.Range(0, rooms[gap])
                .Select(room => (IReadOnlyList<int>)Enumerable.Range(0, lines.Count).Where(each => gapOf[each] == gap && from[each] == room)
                    .Select(each => to[each]).Distinct().OrderBy(row => row).ToList()).ToList()).ToList();
            return TrialMap.Fits(rooms, links) ? links : null;
        }

        /// <summary>The links of the lines a vector on an element holds, <paramref name="lineSize"/> bytes a line; null unless they fit the map.</summary>
        public static IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? Read(Func<long, int, byte[]> read, long element, int offset, int lineSize, IReadOnlyList<int> rooms) =>
            TrialFloorLocator.TryVector(read(element + offset, 2 * sizeof(long)), 0, out var first, out var span) ? LinksAt(read, first, span, lineSize, rooms) : null;

        /// <summary>Finds the lines on an element by their shape: a vector of as many lines as a map of these rooms can have, that draw one.</summary>
        public static (int Offset, int LineSize, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links)? Find(Func<long, int, byte[]> read, long element, IReadOnlyList<int> rooms)
        {
            var body = TrialFloorLocator.Window(read, element, Window);
            for (var at = 0; at + (3 * sizeof(long)) <= body.Length; at += sizeof(long))
            {
                if (!TrialFloorLocator.TryVector(body, at, out var first, out var span) || span == 0 || TrialFloorLocator.Word(body, at + (2 * sizeof(long))) < first + span)
                {
                    continue;
                }

                for (var lineSize = MinLineSize; lineSize <= MaxLineSize; lineSize += sizeof(float))
                {
                    if (LinksAt(read, first, span, lineSize, rooms) is { } links)
                    {
                        return (at, lineSize, links);
                    }
                }
            }

            return null;
        }

        private static IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? LinksAt(Func<long, int, byte[]> read, long first, long span, int lineSize, IReadOnlyList<int> rooms)
        {
            // A room past the entrance is led to at least once, and a room leads to no more rooms than the next column has.
            var fewest = rooms.Skip(1).Sum();
            var most = Enumerable.Range(0, rooms.Count - 1).Sum(column => rooms[column] * rooms[column + 1]);
            if (lineSize < MinLineSize || span % lineSize != 0 || span / lineSize < fewest || span / lineSize > most)
            {
                return null;
            }

            var block = read(first, (int)span);
            if (block.Length != span)
            {
                return null;
            }

            var lines = Enumerable.Range(0, (int)(span / lineSize)).Select(each => new Line(
                BitConverter.ToSingle(block, each * lineSize), BitConverter.ToSingle(block, (each * lineSize) + sizeof(float)),
                BitConverter.ToSingle(block, (each * lineSize) + (2 * sizeof(float))), BitConverter.ToSingle(block, (each * lineSize) + (3 * sizeof(float))))).ToList();
            return Links(lines, rooms);
        }

        /// <summary>
        ///     Which of <paramref name="count"/> groups each value falls in, the groups in rising order, split at the widest
        ///     steps between values; null unless the values within a group stand much closer than the groups do.
        /// </summary>
        private static int[]? Split(IReadOnlyList<double> values, int count)
        {
            var order = Enumerable.Range(0, values.Count).OrderBy(each => values[each]).ToList();
            var steps = Enumerable.Range(1, values.Count - 1).Select(each => values[order[each]] - values[order[each - 1]]).ToList();
            var widest = steps.OrderByDescending(step => step).Take(count - 1).ToList();
            if (widest.Count != count - 1 || widest[^1] <= 0)
            {
                return null;
            }

            var groups = new int[values.Count];
            var group = 0;
            for (var each = 1; each < order.Count; each++)
            {
                // The steps as wide as the narrowest of the widest part the groups; any other must be far narrower.
                if (steps[each - 1] >= widest[^1])
                {
                    group++;
                }
                else if (steps[each - 1] > widest[^1] * SameGap)
                {
                    return null;
                }

                groups[order[each]] = group;
            }

            return group == count - 1 ? groups : null;
        }

        /// <summary>Which group each value falls in, the groups in rising order: values closer than <paramref name="near"/> to the one before are one group.</summary>
        private static int[]? Group(IReadOnlyList<double> values, double near)
        {
            if (values.Count == 0)
            {
                return null;
            }

            var order = Enumerable.Range(0, values.Count).OrderBy(each => values[each]).ToList();
            var groups = new int[values.Count];
            var group = 0;
            for (var each = 1; each < order.Count; each++)
            {
                if (values[order[each]] - values[order[each - 1]] > near)
                {
                    group++;
                }

                groups[order[each]] = group;
            }

            return groups;
        }
    }
}
