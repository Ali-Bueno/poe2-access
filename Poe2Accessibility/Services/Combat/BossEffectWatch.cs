namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>
    ///     A boss's effect-driven attacks (Diamora's waves) met as the effects the client draws, most of them visuals the host
    ///     leaves out of its entities: while one whose shapes the files time holds the player, the way out is said, led to by
    ///     the heartbeat, and a wave's front is heard (docs/research/boss-mechanics.md, Diamora).
    /// </summary>
    public sealed class BossEffectWatch
    {
        private const int PollMs = 100;

        /// <summary>An effect that strikes sooner than this (ms) leaves no time for a word: the dodge cue alone.</summary>
        private const int MinLeadMs = 700;

        /// <summary>The way out is said again this often (ms) while the player stays in it.</summary>
        private const int AgainMs = 1000;

        private const int WordHoldMs = 700;

        /// <summary>The front heard changes this often (ms) among the waves coming, so waves from two sides are both heard.</summary>
        private const int FrontTurnMs = 450;

        /// <summary>A visual unseen this long (ms) is forgotten: its id may come again.</summary>
        private const int ForgetMs = 5000;

        /// <summary>The boss lost for less than this (ms) is the same fight: Benedictus is out of sight 10 s in his maze, whose second blast comes then.</summary>
        private const int FightOverMs = 12000;

        /// <summary>Effect models logged a fight, each once: the evidence of what the client draws.</summary>
        private const int MaxLogged = 80;

        private const string EffectPaths = "Metadata/Effects/";

        /// <summary>The way out is looked for in rings this far apart (world units)…</summary>
        private const float SafeStep = 50f;

        /// <summary>…out to this far, at this many directions.</summary>
        private const float SafeReach = 1000f;

        private const int SafeDirections = 16;

        private readonly IAccessibilityOutputService output;
        private readonly EffectAreas areas;
        private readonly PluginLog log;
        private readonly ConcurrentDictionary<uint, long> known = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private readonly List<(IReadOnlyList<EffectZone> Zones, long StartedAt)> active = new();
        private long lastPoll;
        private long lastFought = long.MinValue / 2;
        private long saidAt;
        private bool caught;
        private bool baselined;

        public BossEffectWatch(IAccessibilityOutputService output, EffectAreas areas, PluginLog log) => (this.output, this.areas, this.log) = (output, areas, log);

        /// <summary>Where a boss being fought stands; null when none is.</summary>
        public Func<Vector2?> Fighting { get; set; } = () => null;

        /// <summary>An effect another watch leads by (its model, place, turn, shapes and when): true when it is not guided here too.</summary>
        public Func<string, Vector2, float, IReadOnlyList<EffectDanger>?, long, bool> Taken { get; set; } = (_, _, _, _, _) => false;

        /// <summary>Where to walk to out of the effect; null when the player is in none.</summary>
        public Vector2? Lead { get; private set; }

        /// <summary>A wave's front coming at the player (as <see cref="BossFloodWatch.Edge"/>); null when none is.</summary>
        public (Vector2 At, float Distance, bool Inside)? Front { get; private set; }

        public void Reset()
        {
            this.known.Clear();
            this.logged.Clear();
            this.active.Clear();
            (this.Lead, this.Front, this.caught, this.baselined) = (null, null, false, false);
        }

        /// <summary>Per frame.</summary>
        public void Tick(bool on)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (on && this.Fighting() != null)
            {
                this.lastFought = now;
            }

            if (!on || now - this.lastFought > FightOverMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                if (this.baselined || !this.known.IsEmpty)
                {
                    this.Reset();
                }

                return;
            }

            this.Meet(now);
            this.Guide(player, now);
        }

        /// <summary>The effects drawn since the last poll, those with timed shapes placed in the world.</summary>
        private void Meet(long now)
        {
            var first = !this.baselined; // what the fight's first poll finds started no one knows when
            this.baselined = true;
            var fresh = new ConcurrentBag<(uint Id, Entity Entity)>();
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            area.ScanVisuals(this.Met, (id, entity) => fresh.Add((id, entity)));
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.Path.StartsWith(EffectPaths, StringComparison.Ordinal) && !this.Met(entity.Id))
                {
                    fresh.Add((entity.Id, entity));
                }
            }

            foreach (var (id, entity) in fresh)
            {
                var ao = entity.TryGetComponent<Animated>(out var animated) ? animated.ModelPath : null;
                entity.TryGetComponent<Render>(out var render);
                var unplaced = render is { WorldPosition: { X: 0f, Y: 0f } } && ao != null && this.areas.Of(ao) != null;
                if (ao is { Length: 0 } || unplaced)
                {
                    continue; // still being made (a wave was once met at 0,0): read again at the next poll
                }

                this.known[id] = now;
                if (!first && ao != null && entity.Path.StartsWith(EffectPaths, StringComparison.Ordinal) && render != null)
                {
                    this.Place(id, entity.Path, ao, render, now);
                }
            }

            foreach (var (id, seenAt) in this.known)
            {
                if (now - seenAt > ForgetMs)
                {
                    this.known.TryRemove(id, out _);
                }
            }
        }

        private bool Met(uint id)
        {
            if (!this.known.ContainsKey(id))
            {
                return false;
            }

            this.known[id] = Environment.TickCount64;
            return true;
        }

        private void Place(uint id, string path, string ao, Render render, long now)
        {
            var at = new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
            var turned = render.Rotation;
            if (this.logged.Count < MaxLogged && this.logged.Add(ao))
            {
                this.log.Info($"Boss effect drawn: {path} #{id} {ao} at {at.X:0},{at.Y:0} turned {turned:0.00}");
            }

            if (this.Taken(ao, at, float.IsNaN(turned) ? 0f : turned, this.areas.Of(ao), now))
            {
                return;
            }

            if (float.IsNaN(turned) || this.areas.Of(ao) is not { } shapes || shapes.Max(shape => shape.EndMs) < MinLeadMs)
            {
                return;
            }

            this.active.Add((shapes.Select(shape => new EffectZone(shape, at, turned)).ToList(), now));
            this.log.Info($"Boss effect: {ao} at {at.X:0},{at.Y:0} turned {turned:0.00}, {shapes.Count} shapes, the first in {shapes.Min(shape => shape.HitMs)} ms");
        }

        /// <summary>Danger shapes known from something other than an effect (a raised rock), standing since <paramref name="since"/>.</summary>
        public void Add(IReadOnlyList<EffectDanger> shapes, Vector2 at, long since) =>
            this.active.Add((shapes.Select(shape => new EffectZone(shape, at, 0f)).ToList(), since));

        private void Guide(Render player, long now)
        {
            this.active.RemoveAll(effect => effect.Zones.All(zone => now - effect.StartedAt > zone.Shape.EndMs));
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var body = player.ModelBounds.X + AttackAim.MarginWorld;
            var zones = this.active.SelectMany(effect => effect.Zones.Select(zone => (Zone: zone, Elapsed: (int)(now - effect.StartedAt))))
                .Where(pair => pair.Elapsed <= pair.Zone.Shape.EndMs).ToList();
            var holding = zones.Where(pair => pair.Zone.Catches(here, body) && pair.Zone.StrikesIn(here, pair.Elapsed) >= 0).ToList();
            if (holding.Count == 0)
            {
                if (this.caught && zones.Count > 0)
                {
                    this.output.Urge(Announcements.OutOfBlast, WordHoldMs);
                    this.log.Info("Boss effect: the player is out of it");
                }

                (this.Lead, this.Front, this.caught) = (null, null, false);
                return;
            }

            var fronts = holding.Select(pair => pair.Zone.Front(here, pair.Elapsed)).OfType<(Vector2 At, float Ahead)>().Where(front => front.Ahead > 0f).ToList();
            this.Front = null;
            if (fronts.Count > 0)
            {
                var coming = fronts[(int)(now / FrontTurnMs % fronts.Count)];
                this.Front = (coming.At, coming.Ahead, false);
            }

            this.Lead = Safe(here, body, zones.Select(pair => pair.Zone).ToList());
            var strikesIn = holding.Min(pair => pair.Zone.StrikesIn(here, pair.Elapsed));
            if (this.Lead is not { } safe || (this.caught ? now - this.saidAt < AgainMs : strikesIn < MinLeadMs))
            {
                return;
            }

            (this.caught, this.saidAt) = (true, now);
            var text = Announcements.Counterplay(CounterplayKind.Step, ScreenProjection.Octant(player.WorldPosition, safe - here));
            this.output.Urge(text, WordHoldMs);
            this.log.Info($"Boss effect: strikes in {strikesIn} ms, safe at {safe.X:0},{safe.Y:0}, {Vector2.Distance(here, safe):0} away -> {text}");
        }

        /// <summary>The nearest place walked to straight that none of the shapes holds.</summary>
        private static Vector2? Safe(Vector2 here, float body, IReadOnlyList<EffectZone> zones)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var (grid, row) = (area.GridWalkableData, area.TerrainMetadata.BytesPerRow);
            return grid == null || grid.Length == 0 || row <= 0
                ? null
                : FloodGeometry.NearestDry(here, point => zones.All(zone => !zone.Catches(point, body)),
                    (from, to) => BeaconMath.LineWalkable(grid, row, from / AttackAreas.WorldPerCell, to / AttackAreas.WorldPerCell), SafeStep, SafeReach, SafeDirections);
        }
    }
}
