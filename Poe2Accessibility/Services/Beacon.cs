namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

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
        private Task<(List<Vector2>? Route, bool Partial, bool Barrier)>? search;

        /// <summary>The last search started, even one a lock change let go: a new one waits for it (rapid re-locks stacked them).</summary>
        private Task? running;
        private int version;
        private long lockedAt;
        private long lastPing;
        private long lastRefresh = long.MinValue / 2;
        private bool noRouteSaid;

        /// <summary>"The way is closed further on" is said once per lock, even if the route comes and goes.</summary>
        private bool partialSaid;
        private Vector2? lastTurn;
        private bool partialRoute;
        private bool barrierRoute;
        private bool closestSaid;

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

        /// <summary>Locks <paramref name="entry"/>; locking the same target again releases it (D2R second press).</summary>
        public void Toggle(ScanEntry entry)
        {
            if (this.target?.Id == entry.Id)
            {
                this.Clear(true);
                return;
            }

            if (this.catalog.DistanceTo(entry) <= ArrivalWorld)
            {
                // Already there: locking would say the way and then the arrival at once.
                this.output.Speak(Announcements.Here(entry.Name), true);
                return;
            }

            this.target = entry;
            this.route = null;
            this.search = null;
            this.version++;
            this.noRouteSaid = false;
            this.closestSaid = false;
            this.lockedAt = Environment.TickCount64;
            this.lastRefresh = long.MinValue / 2;
            this.output.Speak(Announcements.BeaconLocked(this.catalog.Describe(entry, out var cut)), true);
            this.partialSaid = cut; // the lock already said the way stops short: not again when the route arrives
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
            if (!area.Player.TryGetComponent<Render>(out var player))
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
                else
                {
                    this.SayClosest(goal);
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
            this.audio.Play(this.ping, pan, pitch, this.Gain);
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
            this.output.Speak(Announcements.RouteTurn(Announcements.StickDirections[legs[0].Direction], steps), false);
        }

        /// <summary>The target's current place; false when it is gone (and the lock was released).</summary>
        private bool Refresh(Vector2 here)
        {
            var goal = this.target!;
            var current = this.catalog.Entries().FirstOrDefault(entry => entry.Id == goal.Id);
            if (current != null)
            {
                this.target = current with { Category = goal.Category };
                return true;
            }

            if (EntityCatalog.IsEntity(goal) && Vector2.Distance(here, new Vector2(goal.WorldX, goal.WorldY)) <= GoneNearWorld)
            {
                // D2R: a target that vanishes on reaching it (chest opened, item picked up) counts as arrived.
                this.Arrive(goal);
                return false;
            }

            return true; // out of the game's range: keep walking to where it was last seen
        }

        private void StartSearch(Vector2 start, Vector2 end)
        {
            if (this.search is { IsCompleted: false } || this.running is { IsCompleted: false } || !this.catalog.TryLiveGrid(out var grid, out var row))
            {
                return;
            }

            var started = this.version;
            // Guiding to a barrier itself: it is the end of the way, not what cuts it.
            var barriers = this.target?.Kind == "barrier" ? Array.Empty<Vector2>() : this.catalog.ClosedBarriers();
            // The result travels with the task: a search left running by a lock change must not touch the new one's state.
            this.search = Task.Run(() =>
            {
                if (started != this.version)
                {
                    return ((List<Vector2>?)null, false, false);
                }

                var route = RoutePlanner.FindRoute(grid, row, start, end, true, out var reached);
                var barrier = route is { Count: > 0 } && EntityCatalog.BarrierBetween(barriers, route[^1], end);
                return (route, route != null && (!reached || barrier), barrier);
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

            var (found, partial, barrier) = done.IsCompletedSuccessfully ? done.Result : (null, false, false);
            if (found != null)
            {
                this.route = found;
                this.partialRoute = partial;
                this.barrierRoute = barrier;
                if (this.partialRoute && !this.partialSaid && this.target != null)
                {
                    // The way stops short (a gate the quest opens): lead as close as it goes, and say so once.
                    this.partialSaid = true;
                    this.output.Speak(this.barrierRoute ? Announcements.BeaconBarrierRoute(this.target.Name) : Announcements.BeaconPartialRoute(this.target.Name), false);
                }
                else if (!this.partialRoute)
                {
                    this.noRouteSaid = false;
                    this.closestSaid = false;
                }
            }
            else if (!this.noRouteSaid && this.target != null)
            {
                // Standing where a partial route ended is "as close as it goes", not "no route yet".
                this.noRouteSaid = true;
                if (this.route != null && this.partialSaid)
                {
                    this.SayClosest(this.target);
                }
                else
                {
                    this.output.Speak(Announcements.BeaconNoRoute(this.target.Name), false);
                }

                this.route = null;
            }
        }

        private void SayClosest(ScanEntry goal)
        {
            if (!this.closestSaid)
            {
                this.closestSaid = true;
                this.output.Speak(Announcements.BeaconAtClosest(goal.Name), false);
            }
        }

        private void Arrive(ScanEntry goal)
        {
            this.output.Say(SpeechKind.Arrivals, Announcements.BeaconArrived(goal.Name));
            this.log.Info($"Beacon arrived at {goal.Name}");
            this.target = null;
            this.route = null;
            this.search = null;
            this.version++;
        }

    }
}
