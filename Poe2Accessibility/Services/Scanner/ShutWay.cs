namespace Poe2Accessibility.Services
{
    /// <summary>What a finished route search says of a way that must stay whole.</summary>
    public enum ShutWayVerdict
    {
        /// <summary>The way is whole, or was never whole yet: guided as any other.</summary>
        Open,

        /// <summary>A first search found the way cut after it had been whole: nothing changes until the next one agrees.</summary>
        Held,

        /// <summary>The way that was whole is cut: the guide ends.</summary>
        Shut,
    }

    /// <summary>
    ///     A target the guide leaves once the way to it shuts: a door the Trial opened and then closed behind the player is
    ///     no place to be led "as close as it goes" (user's run, 2026-11-12: the guide to the room door stayed on through a
    ///     whole "find the exit" room, leading by a looping way to the cell nearest a door already passed). A way that was
    ///     never whole is left to the ordinary lines (the door may still be opening), and so is one a search only ran out of
    ///     budget on. Pure; the Beacon acts on its verdicts.
    /// </summary>
    public sealed class ShutWay
    {
        /// <summary>Searches in a row (one every Beacon.RouteRefreshMs) that must find the way cut before it is called shut.</summary>
        public const int SearchesToCallItShut = 2;

        private bool wasWhole;
        private int cuts;

        /// <summary>A new target.</summary>
        public void Reset() => (this.wasWhole, this.cuts) = (false, 0);

        /// <param name="whole">The search found a way all the way to the target.</param>
        /// <param name="spent">The search stopped at its budget: the way is long, not known to be cut.</param>
        public ShutWayVerdict Collect(bool whole, bool spent)
        {
            if (whole)
            {
                (this.wasWhole, this.cuts) = (true, 0);
                return ShutWayVerdict.Open;
            }

            if (!this.wasWhole || spent)
            {
                return ShutWayVerdict.Open;
            }

            return ++this.cuts >= SearchesToCallItShut ? ShutWayVerdict.Shut : ShutWayVerdict.Held;
        }
    }
}
