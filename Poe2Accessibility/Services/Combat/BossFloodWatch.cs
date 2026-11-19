namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>
    ///     A boss arena's flood (<see cref="FloodRule"/>): while its object plays the flowing clip, where its edge is against the
    ///     player, told as it nears (<see cref="FloodWarnings"/>) so the player walks away before stepping in.
    /// </summary>
    public sealed class BossFloodWatch
    {
        /// <summary>The front moves about 40 world units a second: read this often (ms), it is told well within a step.</summary>
        private const int PollMs = 250;

        /// <summary>While it flows, where the front is goes to the log this often (ms), to check the reading against the ground alarm.</summary>
        private const int LogMs = 5000;

        /// <summary>World units of one spoken step, the scanner's.</summary>
        private const float StepWorldUnits = 100f;

        private readonly PluginLog log;
        private readonly FloodWarnings warnings = new();
        private FloodRule? rule;
        private uint lavaId;
        private Vector2 origin;
        private RoomFit? fit;
        private bool unfitLogged;
        private bool flowing;
        private long lastPoll;
        private long lastLog;

        public BossFloodWatch(PluginLog log) => this.log = log;

        /// <summary>The animation reader (the boss watch's), null until it is built.</summary>
        public Func<AttackReader?> Reader { get; set; } = () => null;

        public void Reset()
        {
            this.warnings.Reset();
            (this.rule, this.lavaId, this.fit, this.unfitLogged, this.flowing) = (null, 0, null, false, false);
        }

        /// <summary>With the bosses in sight: the lines of the flood to say now.</summary>
        public IReadOnlyList<string> Poll(BossMechanicsCatalog catalog, IEnumerable<string> bosses, long now)
        {
            var rule = bosses.Select(catalog.Flood).FirstOrDefault(found => found != null);
            if (rule == null)
            {
                if (this.rule != null)
                {
                    this.Reset();
                }

                return Array.Empty<string>();
            }

            if (now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<string>();
            }

            this.lastPoll = now;
            if (!ReferenceEquals(rule, this.rule))
            {
                this.Reset();
                this.rule = rule;
            }

            if (this.Lava(rule) is not { } lava || this.fit is not { } fit || !lava.TryGetComponent<Animated>(out var animated) ||
                this.Reader()?.Progress(lava.Id, animated.Address) is not { } clock)
            {
                return Array.Empty<string>();
            }

            // The pool waits at its source looping the clip's first seconds; it flows once the whole clip plays.
            var flows = clock.End >= rule.ClipSeconds - rule.PeriodSeconds && clock.Position >= 0f && clock.Position <= rule.ClipSeconds + rule.PeriodSeconds;
            var lines = new List<string>();
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var outline = FloodGeometry.Outline(rule, clock.Position);
            if (flows != this.flowing)
            {
                this.flowing = flows;
                this.warnings.Reset();
                this.log.Info($"Boss flood: {rule.Object} {(flows ? "flows" : "stopped")} at {clock.Position:0.0} of {clock.End:0.0} s");
                if (flows)
                {
                    var front = this.origin + fit.ToWorld(outline[0]);
                    lines.Add(Announcements.BossFloodFlows(ScreenProjection.Octant(player.WorldPosition, front - here)));
                }
            }

            if (!flows)
            {
                return lines;
            }

            var (inside, distance, nearest) = FloodGeometry.Measure(outline, fit.ToRoom(here - this.origin));
            var edge = this.origin + fit.ToWorld(nearest);
            var steps = (int)Math.Round(distance / StepWorldUnits);
            if (now - this.lastLog >= LogMs)
            {
                this.lastLog = now;
                var tip = this.origin + fit.ToWorld(outline[0]);
                this.log.Info($"Boss flood: {clock.Position:0.0} s, front {tip.X:0},{tip.Y:0}, player {(inside ? "inside" : "outside")} {distance:0} from its edge");
            }

            var way = ScreenProjection.Octant(player.WorldPosition, edge - here);
            switch (this.warnings.Update(inside, steps))
            {
                case FloodNewsKind.Near:
                    lines.Add(Announcements.BossFloodNear(way, Math.Max(1, steps)));
                    break;
                case FloodNewsKind.Inside:
                    lines.Add(Announcements.BossFloodInside(way));
                    break;
            }

            return lines;
        }

        /// <summary>The flood's object, and the fit of its room to the world from the first anchor in sight.</summary>
        private Entity? Lava(FloodRule rule)
        {
            Entity? lava = null;
            var anchors = new Dictionary<string, List<Vector2>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType == EntityTypes.Player || entity.Path.Length == 0)
                {
                    continue;
                }

                var path = BossMechanicsCatalog.MonsterId(entity.Path);
                if (path.Equals(rule.Object, StringComparison.OrdinalIgnoreCase))
                {
                    lava = entity;
                }
                else if (this.fit == null && rule.Anchors.Any(anchor => anchor.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) &&
                         entity.TryGetComponent<Render>(out var render))
                {
                    if (!anchors.TryGetValue(path, out var seen))
                    {
                        anchors[path] = seen = new List<Vector2>();
                    }

                    seen.Add(new Vector2(render.WorldPosition.X, render.WorldPosition.Y));
                }
            }

            if (lava == null || !lava.TryGetComponent<Render>(out var at))
            {
                return null;
            }

            var origin = new Vector2(at.WorldPosition.X, at.WorldPosition.Y);
            if (lava.Id != this.lavaId)
            {
                (this.lavaId, this.origin, this.fit, this.unfitLogged) = (lava.Id, origin, null, false);
            }

            if (this.fit == null)
            {
                // An anchor counts only when it is the one of its path in sight: two would leave which one the room placed unknown.
                foreach (var (path, room) in rule.Anchors)
                {
                    if (anchors.TryGetValue(path, out var seen) && seen.Count == 1 && RoomFit.Of(room, seen[0] - origin) is { } found)
                    {
                        this.fit = found;
                        this.log.Info($"Boss flood: {rule.Object} at {origin.X:0},{origin.Y:0}, its room fitted by {path} as {found}");
                        break;
                    }
                }

                if (this.fit == null && anchors.Count > 0 && !this.unfitLogged)
                {
                    this.unfitLogged = true;
                    this.log.Info($"Boss flood: {rule.Object} at {origin.X:0},{origin.Y:0}, no anchor fits its room ({string.Join(", ", anchors.Select(pair => $"{pair.Key} x{pair.Value.Count}"))})");
                }
            }

            return lava;
        }
    }
}
