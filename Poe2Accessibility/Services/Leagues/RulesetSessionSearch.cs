namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Where a session keeps its area and its rulesets once a game update moved them (<see cref="GameLayout.Rulesets"/>).
    ///     A session is still the object that holds the area's address, and its rulesets still the pointers of one array, by
    ///     row of Rulesets.dat, that each point back to it; every area has one at least (AncestralOmenRules). Both must hold,
    ///     and a ruleset must hold the number of the row it is found in, for a place to be taken. Pure.
    /// </summary>
    public static class RulesetSessionSearch
    {
        /// <summary>The part said lost when no session is found in two areas: every area has one.</summary>
        public const string Part = "rulesets";

        /// <summary>How far either side of where a field of the session was it is looked for: the session grows by a few fields a patch.</summary>
        public const int Reach = 0x400;

        /// <summary>How far either side the session's place in a ruleset is looked for: the rulesets' base is a few fields long.</summary>
        public const int BackReach = 0x40;

        /// <summary>Where a session holds the area, where its rulesets' array starts, and where a ruleset holds its session.</summary>
        public readonly record struct Places(int Area, int Rulesets, int Back);

        /// <summary>
        ///     The places of <paramref name="session"/> nearest <paramref name="measured"/>; null unless it holds
        ///     <paramref name="area"/> and a ruleset that points back to it from the row whose number it holds.
        ///     <paramref name="pointerAt"/> reads a pointer (0 when unreadable); <paramref name="named"/> says whether a
        ///     ruleset is the one of a row.
        /// </summary>
        public static Places? Of(Func<long, long> pointerAt, Func<long, int, bool> named, long session, long area, Places measured, int rows)
        {
            if (session == 0 || area == 0 ||
                OffsetSearch.First(measured.Area, Reach, IntPtr.Size, at => pointerAt(session + at) == area) is not { } areaAt)
            {
                return null;
            }

            // The array has most likely moved as far as the area's pointer: both sit deep in the same object.
            var moved = areaAt - measured.Area;
            foreach (var back in OffsetSearch.Outward(measured.Back, BackReach, IntPtr.Size))
            {
                if (OffsetSearch.First(measured.Rulesets + moved, Reach, IntPtr.Size, at => HoldsItsRuleset(pointerAt, named, session, at, back, rows)) is { } rulesetsAt)
                {
                    return new Places(areaAt, rulesetsAt, back);
                }
            }

            return null;
        }

        /// <summary>Whether an array starting at <paramref name="array"/> holds, at some row, a ruleset of that row that points back to the session.</summary>
        private static bool HoldsItsRuleset(Func<long, long> pointerAt, Func<long, int, bool> named, long session, int array, int back, int rows)
        {
            for (var row = 0; row < rows; row++)
            {
                var rules = pointerAt(session + array + (row * IntPtr.Size));
                if (rules != 0 && pointerAt(rules + back) == session && named(rules, row))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
