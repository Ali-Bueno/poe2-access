namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Guide beacon (D2R docs/beacon.md): once the player locks a scanner entry, a short ping repeats, faster as the
    ///     walkable route shortens, panned toward where the route goes next — the farthest route point within reach in
    ///     a straight line, so it turns where the corridor turns and never points into a wall. No route: silence (never
    ///     a straight line through walls). Arriving names the target and releases the lock; so does an area change.
    ///     Routes are searched off the frame thread (A* over a whole area takes up to ~300 ms).
    /// </summary>
    public sealed class Beacon
    {
        /// <summary>D2R: silence after the lock so the spoken name is not stepped on.</summary>
        private const int LockDelayMs = 600;

        /// <summary>How often the target's place and the route are refreshed while the player walks.</summary>
        private const int RouteRefreshMs = 700;

        /// <summary>Route length the ping looks ahead along, in grid cells (~ 0.7 s of walking).</summary>
        private const float LookAheadGrid = 25f;

        /// <summary>Close enough to count as arrived: a step and a half.</summary>
        private const float ArrivalWorld = 150f;

        /// <summary>A chest or item that vanished while the player stood this close was opened or picked up: arrived.</summary>
        private const float GoneNearWorld = 600f;

        /// <summary>The target was not listed at the last refresh: its place is where it was last seen, not where it is.</summary>
        private bool targetGone;

        /// <summary>The scanner has listed the target since the lock: only then does its absence from the list say anything.</summary>
        private bool everListed;

        /// <summary>"No longer in sight" is said once per lock: a boss that burrows comes and goes all fight.</summary>
        private bool lostSaid;

        /// <summary>The category the scanner lists the target under; a target locked from the Quest category is a copy.</summary>
        private ScanCategory listedAs;

        /// <summary>The area instance the target was locked in: its id and place mean nothing in another.</summary>
        private (IntPtr Address, string Hash) lockedIn;


        /// <summary>Route prompts (D2R route-description.md): a turn is named when the player is this close to it.</summary>
        private const float TurnPromptWorld = 160f;

        /// <summary>A corner within this many grid cells of the last one named is the same corner (routes are re-planned).</summary>
        private const float SameCornerGrid = 6f;

        private const float CornerToleranceGrid = 3f;
        private const float MinLegGrid = 5f;

        /// <summary>One spoken step (EntityCatalog), and the probe used to read the camera's axes.</summary>
        private const float StepWorld = 100f;
        private const float ProbeOffset = 100f;

        private readonly AudioCueService audio;
        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly float[] ping;
        private ScanEntry? target;
        private List<Vector2>? route;
        private Task<(List<Vector2>? Route, bool Partial, bool Barrier, bool Spent)>? search;

        /// <summary>The last search started, even one a lock change let go: a new one waits for it (rapid re-locks stacked them).</summary>
        private Task? running;
        private int version;
        private long lockedAt;
        private long lastPing;
        private long lastRefresh = long.MinValue / 2;
        private readonly RouteSearchNews news = new();

        /// <summary>The partial way in use is only as far as the last search got (<see cref="WayCut.Long"/>), not closed.</summary>
        private bool routeSpent;
        private Vector2? lastTurn;
        private bool partialRoute;

        public Beacon(AudioCueService audio, EntityCatalog catalog, IAccessibilityOutputService output, PluginLog log)
        {
            this.audio = audio;
            this.catalog = catalog;
            this.output = output;
            this.log = log;
            this.ping = CueSynth.Ping(AudioCueService.SampleRate);
        }

        /// <summary>Beacon gain under the master volume (settings BeaconVolume).</summary>
        public float Gain { get; set; } = 1f;

        public ScanEntry? Target => this.target;

        /// <summary>
        ///     Locks <paramref name="entry"/> in place of any earlier target. <paramref name="asked"/>: the player chose it just
        ///     now, so the answer is their own move and a quest page being said does not hold it back.
        /// </summary>
        public void Lock(ScanEntry entry, bool asked = false)
        {
            if (this.catalog.DistanceTo(entry) <= ArrivalWorld)
            {
                // Already there: locking would say the way and then the arrival at once.
                this.Answer(Announcements.Here(entry.Name), asked);
                return;
            }

            this.Aim(entry);
            this.Answer(Announcements.BeaconLocked(this.catalog.Describe(entry, out var cut, out var longWay)), asked);
            this.news.Aim(cut, longWay);
        }

        private void Answer(string text, bool asked)
        {
            if (asked)
            {
                this.output.Moved(text);
            }
            else
            {
                this.output.Speak(text, true);
            }
        }

        /// <summary>
        ///     Guides to what the scanner just named (setting AutoGuide), in place of any earlier target, adding nothing to what
        ///     was said; <paramref name="cut"/> is whether that said the way stops short.
        /// </summary>
        public void Follow(ScanEntry entry, bool cut, bool longWay = false)
        {
            if (this.catalog.DistanceTo(entry) <= ArrivalWorld)
            {
                this.Clear(false); // already there: nothing to guide to, and no ping left going to the last one
                return;
            }

            this.Aim(entry);
            this.news.Aim(cut, longWay);
        }

        private void Aim(ScanEntry entry)
        {
            this.target = entry;
            this.route = null;
            this.search = null;
            this.version++;
            this.targetGone = false;
            this.everListed = false;
            this.lostSaid = false;
            this.listedAs = entry.Category;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            this.lockedIn = (area.Address, area.AreaHash ?? string.Empty);
            this.lockedAt = Environment.TickCount64;
            this.lastRefresh = long.MinValue / 2;
            this.log.Info($"Beacon locked on {entry.Name} ({entry.Category})");
        }

        /// <summary>Releases the lock (area change, arrival, second press).</summary>
        public void Clear(bool speak)
        {
            if (this.target == null)
            {
                return;
            }

            if (speak)
            {
                this.output.Speak(Announcements.BeaconOff, true);
            }

            this.log.Info($"Beacon released ({this.target.Name})");
            this.target = null;
            this.route = null;
            this.search = null;
            this.version++;
        }

        /// <summary>Per frame. Silent (lock kept) while a panel, menu or dialogue is open.</summary>
        public void Tick(bool playing)
        {
            if (this.target is not { } goal || !playing)
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!BeaconMath.SameInstance(this.lockedIn.Address, this.lockedIn.Hash, area.Address, area.AreaHash ?? string.Empty))
            {
                this.Clear(false); // another instance, should the area handler not have released it
                return;
            }

            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            var gridSize = area.WorldToGridConvertor;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            if (now - this.lastRefresh >= RouteRefreshMs)
            {
                this.lastRefresh = now;
                if (!this.Refresh(here))
                {
                    return;
                }

                goal = this.target!;
                this.StartSearch(here / gridSize, new Vector2(goal.WorldX, goal.WorldY) / gridSize);
            }

            this.CollectSearch();
            if (Vector2.Distance(here, new Vector2(goal.WorldX, goal.WorldY)) <= ArrivalWorld)
            {
                this.Arrive(goal);
                return;
            }

            if (this.route is not { Count: > 0 } path || now - this.lockedAt < LockDelayMs)
            {
                return;
            }

            var playerGrid = here / gridSize;
            var from = BeaconMath.NearestIndex(path, playerGrid);
            var remainingWorld = BeaconMath.Remaining(path, from, playerGrid) * gridSize;
            if (remainingWorld <= ArrivalWorld)
            {
                if (!this.partialRoute)
                {
                    this.Arrive(goal);
                }
                else if (!this.routeSpent)
                {
                    this.Say(this.news.Closest(), goal); // a way that only ran out of budget is not shut: the next search goes on from here
                }

                return; // at the end of a way that stops short: silent until the gate opens and the route goes on
            }

            if (this.TurnPrompts)
            {
                this.PromptTurn(path, from, playerGrid, gridSize, player.WorldPosition);
            }

            if (now - this.lastPing < BeaconMath.IntervalMs(remainingWorld) || !this.catalog.TryLiveGrid(out var grid, out var row))
            {
                return;
            }

            this.lastPing = now;
            var aim = BeaconMath.AimPoint(grid, row, path, from, playerGrid, LookAheadGrid) * gridSize;
            var screen = ScreenProjection.Direction(player.WorldPosition, aim - here);
            this.LastAim = (screen, remainingWorld);
            var (pan, pitch) = BeaconMath.Voice(screen);
            this.audio.Play(this.ping, pan, pitch, this.Gain, "beacon");
        }

        /// <summary>Test aid (dev command "beacon"): target, route, search and ping state.</summary>
        public string Diagnose() =>
            $"target={this.target?.Name ?? "none"} at={this.target?.WorldX:F0},{this.target?.WorldY:F0} route={this.route?.Count ?? -1} partial={this.partialRoute} search={(this.search == null ? "none" : this.search.Status.ToString())} lastPing={Environment.TickCount64 - this.lastPing} ms ago";

        /// <summary>Test aid (dev command "beacon"): the screen direction of the last ping and the route left, world units.</summary>
        public (Vector2 Screen, float RemainingWorld)? LastAim { get; private set; }

        /// <summary>Speak "Now up right, 12" as the player reaches each turn of the route (setting RoutePrompts).</summary>
        public bool TurnPrompts { get; set; } = true;

        /// <summary>
        ///     The next real turn of the route (corners of the walked grid path), named once as the player comes within
        ///     <see cref="TurnPromptWorld"/> of it: the direction of the leg after it and how many steps it lasts.
        /// </summary>
        private void PromptTurn(List<Vector2> path, int from, Vector2 playerGrid, float gridSize, GameOffsets.Natives.StdTuple3D<float> origin)
        {
            var corners = RoutePlanner.Corners(path.GetRange(from, path.Count - from), CornerToleranceGrid);
            if (corners.Count < 3)
            {
                return; // a straight line to the end: nothing to turn at
            }

            var turn = corners[1];
            if (Vector2.Distance(playerGrid, turn) * gridSize > TurnPromptWorld ||
                (this.lastTurn is { } said && Vector2.Distance(said, turn) <= SameCornerGrid))
            {
                return;
            }

            var alongX = ScreenProjection.Direction(origin, new Vector2(ProbeOffset, 0));
            var alongY = ScreenProjection.Direction(origin, new Vector2(0, ProbeOffset));
            var legs = RoutePlanner.Legs(corners.GetRange(1, corners.Count - 1), delta => StickDirection.Octant(alongX, alongY, delta.X, delta.Y), MinLegGrid);
            if (legs.Count == 0)
            {
                return;
            }

            this.lastTurn = turn;
            var steps = Math.Max(1, (int)Math.Round(legs[0].Length * gridSize / StepWorld));
            this.output.SpeakReading(Announcements.RouteTurn(Announcements.StickDirections[legs[0].Direction], steps));
        }

        /// <summary>The target's current place; false when it is gone (and the lock was released).</summary>
        private bool Refresh(Vector2 here)
        {
            var goal = this.target!;
            var entries = this.catalog.Entries();
            var current = entries.FirstOrDefault(entry => entry.Id == goal.Id);
            this.everListed |= current != null;
            this.targetGone = current == null && this.everListed;
            if (current != null)
            {
                this.listedAs = entries.FirstOrDefault(entry => entry.Id == goal.Id && entry.Category != ScanCategory.Quest)?.Category ?? current.Category;
                this.target = current with { Category = goal.Category };
                return true;
            }

            switch (BeaconMath.Gone(this.Listed(goal), EntityCatalog.IsEntity(goal), Vector2.Distance(here, new Vector2(goal.WorldX, goal.WorldY)), GoneNearWorld, this.everListed))
            {
                case GoneTarget.Reached:
                    // D2R: a target that vanishes on reaching it (chest opened, item picked up) counts as arrived.
                    this.Arrive(goal);
                    return false;
                case GoneTarget.LostFromView when !this.lostSaid:
                    // A boss that hid, burrowed or died, an NPC that left: said as gone, never as reached.
                    this.lostSaid = true;
                    this.output.Say(SpeechKind.Arrivals, Announcements.BeaconLost(goal.Name));
                    this.log.Info($"Beacon target lost from view: {goal.Name}");
                    break;
            }

            return true; // gone or out of the game's range: keep walking to where it was last seen
        }

        /// <summary>The target under the category the scanner lists it in: a quest's copy is judged as the thing it copies.</summary>
        private ScanEntry Listed(ScanEntry goal) => goal with { Category = this.listedAs };

        private void StartSearch(Vector2 start, Vector2 end)
        {
            if (this.search is { IsCompleted: false } || this.running is { IsCompleted: false } || !this.catalog.TryLiveGrid(out var grid, out var row))
            {
                return;
            }

            var started = this.version;
            // Guiding to a barrier itself: it is the end of the way, not what cuts it.
            var barriers = this.target?.Kind == "barrier" ? Array.Empty<Vector2>() : this.catalog.ClosedBarriers();
            // The way being walked: a fresh search only replaces it when clearly shorter (RoutePlanner.Keep).
            var walking = this.partialRoute ? null : this.route;
            // The result travels with the task: a search left running by a lock change must not touch the new one's state.
            this.search = Task.Run(() =>
            {
                if (started != this.version)
                {
                    return ((List<Vector2>?)null, false, false, false);
                }

                var route = RoutePlanner.FindRoute(grid, row, start, end, true, out var reached, out var spent);
                if (route != null && reached)
                {
                    route = RoutePlanner.Keep(walking, route, start, grid, row);
                }

                var barrier = route is { Count: > 0 } && EntityCatalog.BarrierBetween(barriers, route[^1], end);
                return (route, route != null && (!reached || barrier), barrier, spent);
            });
            this.running = this.search;
        }

        private void CollectSearch()
        {
            if (this.search is not { IsCompleted: true } done)
            {
                return;
            }

            this.search = null;
            if (done.IsFaulted)
            {
                this.log.Warn($"Guide: route search failed ({done.Exception?.GetBaseException().Message}); heard as no route");
            }

            var (found, partial, barrier, spent) = done.IsCompletedSuccessfully ? done.Result : (null, false, false, false);
            var verdict = this.news.Collect(found != null, partial, barrier, spent, this.target != null, this.route != null);
            switch (verdict.Way)
            {
                case SearchWay.Wait:
                    this.log.Info($"Guide: a route search {(done.IsFaulted ? "failed" : "found no way")}; searched again before it is said");
                    return;
                case SearchWay.Take:
                    (this.route, this.partialRoute, this.routeSpent) = (found, partial, verdict.Spent);
                    break;
                case SearchWay.Drop:
                    this.route = null;
                    break;
            }

            if (this.target is { } goal)
            {
                this.Say(verdict.Line, goal);
            }
        }

        private void Say(SearchLine line, ScanEntry goal)
        {
            var text = line switch
            {
                SearchLine.LongWay => Announcements.BeaconLongWay,
                SearchLine.PartialWay => Announcements.BeaconPartialRoute(goal.Name),
                SearchLine.BarrierWay => Announcements.BeaconBarrierRoute(goal.Name),
                SearchLine.NoRoute => Announcements.BeaconNoRoute(goal.Name),
                SearchLine.AtClosest => Announcements.BeaconAtClosest(goal.Name),
                _ => null,
            };
            if (text != null)
            {
                this.output.Speak(text, false);
            }
        }

        private void Arrive(ScanEntry goal)
        {
            // At the place of a target that is no longer listed, the place is all that was reached.
            var lastSeen = this.targetGone && BeaconMath.Gone(this.Listed(goal), EntityCatalog.IsEntity(goal), 0f, GoneNearWorld) == GoneTarget.LostFromView;
            this.output.Say(SpeechKind.Arrivals, lastSeen ? Announcements.BeaconLastSeen(goal.Name) : Announcements.BeaconArrived(goal.Name));
            this.log.Info(lastSeen ? $"Beacon arrived where {goal.Name} was last seen" : $"Beacon arrived at {goal.Name}");
            this.target = null;
            this.route = null;
            this.search = null;
            this.version++;
        }

    }
}
