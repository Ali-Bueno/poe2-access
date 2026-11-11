namespace Poe2Accessibility.Services
{
    /// <summary>What a finished route search does to the way the guide follows.</summary>
    public enum SearchWay
    {
        /// <summary>A first search that found nothing: the way in use stands until the next one agrees.</summary>
        Wait,

        /// <summary>The found way replaces the one in use.</summary>
        Take,

        /// <summary>No way, said now: the one in use is dropped.</summary>
        Drop,

        /// <summary>No way, already said: nothing changes.</summary>
        Keep,
    }

    /// <summary>The one line a finished search says, if any.</summary>
    public enum SearchLine
    {
        None,
        LongWay,
        PartialWay,
        BarrierWay,
        NoRoute,
        AtClosest,
    }

    /// <param name="Spent">The way taken is only as far as the search's budget got (<see cref="WayCut.Long"/>).</param>
    public readonly record struct SearchVerdict(SearchWay Way, SearchLine Line, bool Spent);

    /// <summary>
    ///     The guide's lines about its way, each said once per lock: a long way, a way that stops short (or at a barrier), no
    ///     route yet (after two searches in a row agree) and "as close as it goes". Pure; the Beacon acts on its verdicts.
    /// </summary>
    public sealed class RouteSearchNews
    {
        /// <summary>Searches in a row (one every Beacon.RouteRefreshMs) that must find no way before "no route yet" is said.</summary>
        public const int SearchesToSayNoRoute = 2;

        private int searchesWithoutRoute;
        private bool noRouteSaid;
        private bool closestSaid;

        /// <summary>"The way is closed further on" is said once per lock, even if the route comes and goes.</summary>
        private bool partialSaid;

        /// <summary>"The way is long, still searching" is said once per lock; a closed way found later is still said.</summary>
        private bool longSaid;

        /// <summary>A new target: nothing said of its way yet but what the lock said (<paramref name="cut"/>: that it stops short).</summary>
        public void Aim(bool cut, bool longWay)
        {
            this.noRouteSaid = false;
            this.searchesWithoutRoute = 0;
            this.closestSaid = false;
            this.partialSaid = cut && !longWay;
            this.longSaid = cut && longWay; // the lock already said the way stops short: not again when the route arrives
        }

        /// <summary>A finished search: <paramref name="found"/> whether it gave a way, <paramref name="hadRoute"/> whether one was in use.</summary>
        public SearchVerdict Collect(bool found, bool partial, bool barrier, bool spent, bool hasTarget, bool hadRoute)
        {
            if (!found && ++this.searchesWithoutRoute < SearchesToSayNoRoute)
            {
                return new SearchVerdict(SearchWay.Wait, SearchLine.None, false);
            }

            if (found)
            {
                this.searchesWithoutRoute = 0;
                var cut = WayCuts.Of(partial, barrier, spent);
                var line = SearchLine.None;
                if (cut == WayCut.Long && !this.longSaid && !this.partialSaid && hasTarget)
                {
                    // Only the budget ran out: lead along the part that is worked out, searching again from further on.
                    this.longSaid = true;
                    line = SearchLine.LongWay;
                }
                else if (partial && cut != WayCut.Long && !this.partialSaid && hasTarget)
                {
                    // The way stops short (a gate the quest opens): lead as close as it goes, and say so once.
                    this.partialSaid = true;
                    line = barrier ? SearchLine.BarrierWay : SearchLine.PartialWay;
                }
                else if (!partial)
                {
                    this.noRouteSaid = false;
                    this.closestSaid = false;
                }

                return new SearchVerdict(SearchWay.Take, line, cut == WayCut.Long);
            }

            if (this.noRouteSaid || !hasTarget)
            {
                return new SearchVerdict(SearchWay.Keep, SearchLine.None, false);
            }

            // Standing where a partial route ended is "as close as it goes", not "no route yet".
            this.noRouteSaid = true;
            return new SearchVerdict(SearchWay.Drop, hadRoute && this.partialSaid ? this.Closest() : SearchLine.NoRoute, false);
        }

        /// <summary>"As close as it goes", once per lock until a whole way is found again; None when already said.</summary>
        public SearchLine Closest()
        {
            if (this.closestSaid)
            {
                return SearchLine.None;
            }

            this.closestSaid = true;
            return SearchLine.AtClosest;
        }
    }
}
