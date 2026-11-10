namespace Poe2Accessibility.Services
{
    /// <summary>Why a route falls short of its goal, which decides what the player is told.</summary>
    public enum WayCut
    {
        /// <summary>The route reaches the goal.</summary>
        None,

        /// <summary>The ground ends before the goal (a gate the quest opens, a bridge out of the grid): a way that is closed.</summary>
        Closed,

        /// <summary>A closed barrier lies between the route's end and the goal.</summary>
        Barrier,

        /// <summary>The search ran out of budget: the way may well go on, and the next search starts from further along.</summary>
        Long,
    }

    /// <summary>The rule that tells a closed way from one that was only too long to work out.</summary>
    public static class WayCuts
    {
        /// <summary>
        ///     A known barrier is a fact whatever the budget did; otherwise a search that spent its budget has found no
        ///     closure, so saying "the way stops short" would be untrue.
        /// </summary>
        public static WayCut Of(bool partial, bool barrier, bool spent) =>
            barrier ? WayCut.Barrier : !partial ? WayCut.None : spent ? WayCut.Long : WayCut.Closed;
    }
}
