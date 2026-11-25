namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>
    ///     Benedictus's arena (docs/research/boss-mechanics.md, "Benedictus"; Aurelian in maps): the hole of the maze's first
    ///     blast, the way through its walls before the second, the middle strip before the pillars fall, and the rocks he
    ///     raises, which burst. The player is led as to a shelter (<see cref="SafeSpotGuide"/>, <see cref="MazeExitGuide"/>).
    /// </summary>
    public sealed class BossMazeWatch
    {
        private const string Folder = "Metadata/Monsters/TwilightOrder/TwilightOrderExcavationOverlord/";
        private const string Boss = Folder + "TwilightOrderOverlord";
        private const string RockPath = Folder + "Objects/TwilightOverlordMazeRock";
        private const string MiddlePath = Folder + "Objects/ArenaMiddle";
        private const string PillarPath = Folder + "Objects/Pillar";
        private const string RaisedPath = Folder + "Objects/TwilightOverlordUpwardRock";
        private const string PillarState = "break_pillar";
        private const string BrokenState = "break";
        private const string ShatterState = "shatter";

        /// <summary>The entities are looked for this often (ms).</summary>
        private const int PollMs = 100;

        /// <summary>The middle strip the pillars spare: the room's sector `pillar`, 245 to 332 across, its middle at 286 (cells): 41 on the narrower side.</summary>
        private const float StripWorld = 41f * AttackAreas.WorldPerCell;

        /// <summary>The pillars crush 1.35 s after they break (2026-11-25): this long (ms) after it they are past.</summary>
        private const int CrushMs = 2500;

        /// <summary>The line blast where its effect is not drawn yet: 110 cells from the round's marker (24071 to 22875, 2026-11-25).</summary>
        private const float LineReachWorld = 110f * AttackAreas.WorldPerCell;

        /// <summary>Past the blast's edge by this much (world units) the player is led on, so the body is out of it.</summary>
        private const float BeyondWorld = 60f;

        /// <summary>Rocks raised together: two within this (ms) are the field that bursts; one alone is his retreat's, gone in half a second.</summary>
        private const int FieldMs = 1000;

        /// <summary>A raised rock bursts out 1.7 s after its mark (25 cells) and is struck 4.8 s after it (18 cells): the skills' areas, the times of 2026-11-25.</summary>
        private static readonly EffectDanger[] RaisedDangers =
        {
            new(AttackAreas.Circle, 1700, 25f * AttackAreas.WorldPerCell, 0f, 0f, Vector2.Zero, 0f, 0),
            new(AttackAreas.Circle, 4800, 18f * AttackAreas.WorldPerCell, 0f, 0f, Vector2.Zero, 0f, 0),
        };

        /// <summary>The ring he slams up: its middle falls 2.05 s after its effect (GSTwilightOverlordDonutSlamCentre, 27 cells; drawn 2.0 s after it, 2026-11-25).</summary>
        private const string SlamEffect = "/rock_donut_form.ao";

        private static readonly EffectDanger[] SlamDangers = { new(AttackAreas.Circle, 2050, 27f * AttackAreas.WorldPerCell, 0f, 0f, Vector2.Zero, 0f, 0) };

        private readonly EntityPlaces positions;
        private readonly PluginLog log;
        private readonly MazeRound round = new();
        private readonly SafeSpotGuide safeSpot = new();
        private readonly MazeExitGuide exit = new();
        private readonly Dictionary<uint, int> kinds = new();
        private readonly List<MazeRock> rocks = new();
        private readonly Dictionary<uint, (Vector2 At, long SeenAt, bool Told)> raised = new();
        private readonly List<Vector2> pillars = new();
        private Vector2? middle;
        private EffectZone? line;
        private bool pillarsFall;
        private long? pillarsBrokeAt;
        private uint roundId;
        private MazePhase told;
        private long lastPoll;

        public BossMazeWatch(EntityPlaces positions, PluginLog log)
        {
            this.positions = positions;
            this.log = log;
        }

        /// <summary>A skill's danger area from the game's tables.</summary>
        public Func<string, AttackArea?> AreaOf { get; set; } = _ => null;

        /// <summary>A danger's shapes standing at a place since a moment: the effects' guide leads out of them.</summary>
        public Action<IReadOnlyList<EffectDanger>, Vector2, long> Danger { get; set; } = (_, _, _) => { };

        /// <summary>The player is being led: nothing else leads meanwhile.</summary>
        public bool Up { get; private set; }

        /// <summary>Where the lead goes (world) and how wide the place is; null when the player is there.</summary>
        public (Vector2 At, float Radius)? Lead { get; private set; }

        public void Reset()
        {
            this.round.Reset();
            this.safeSpot.Reset();
            this.exit.Reset();
            this.kinds.Clear();
            this.rocks.Clear();
            this.raised.Clear();
            this.pillars.Clear();
            (this.middle, this.line, this.pillarsBrokeAt, this.Lead) = (null, null, null, null);
            (this.pillarsFall, this.Up, this.told) = (false, false, MazePhase.None);
        }

        /// <summary>An effect the game drew while the boss is fought; true when this watch leads by it.</summary>
        public bool Drawn(string ao, Vector2 at, float rotation, IReadOnlyList<EffectDanger>? shapes, long now)
        {
            if (ao.EndsWith(SlamEffect, StringComparison.OrdinalIgnoreCase))
            {
                this.Danger(SlamDangers, at, now);
            }

            if (!this.round.Drawn(ao, at, this.middle, now))
            {
                return false;
            }

            this.roundId++;
            if (shapes is { Count: > 0 })
            {
                this.line = new EffectZone(shapes[0], at, rotation);
            }
            else if (!this.round.SecondDrawn)
            {
                this.line = null;
            }

            this.log.Info($"Boss maze: {ao} at {at.X:0},{at.Y:0} turned {rotation:0.00}, {this.rocks.Count} rocks stand, the arena's middle {(this.middle is { } arena ? $"{arena.X:0},{arena.Y:0}" : "unknown")}");
            return true;
        }

        /// <summary>At each poll of the boss mechanics, with the bosses fought of late.</summary>
        public void Poll(IReadOnlyList<string> fighting, long now)
        {
            if (!fighting.Any(monster => monster.StartsWith(Boss, StringComparison.OrdinalIgnoreCase)))
            {
                if (this.kinds.Count > 0)
                {
                    this.Reset();
                }

                return;
            }

            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            this.rocks.Clear();
            this.pillars.Clear();
            var (falling, broken) = (false, false);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || this.KindOf(entity) is not (> 0 and var kind))
                {
                    continue;
                }

                if (kind == 1)
                {
                    falling |= EntityCatalog.IsAliveEnemy(entity) && StateOf(entity, PillarState) == 1;
                }
                else if (this.positions.Of(entity) is { } at)
                {
                    this.Note(entity, kind, at, ref broken, now);
                }
            }

            this.pillarsBrokeAt = broken ? this.pillarsBrokeAt ?? now : null;
            if (falling && !broken && !this.pillarsFall)
            {
                this.pillarsFall = true;
                this.roundId++;
                this.log.Info($"Boss maze: the pillars are to fall, {this.pillars.Count} of them");
            }
            else if (this.pillarsBrokeAt is { } brokeAt ? now - brokeAt > CrushMs : !falling)
            {
                this.pillarsFall = false;
            }

            this.TellRaised(now);
        }

        /// <summary>Each frame: where the lead goes, and the line that leaves no time to wait; null when none.</summary>
        public string? Follow(bool heartbeat, long now)
        {
            var phase = this.round.Phase(now);
            if (phase != this.told)
            {
                this.told = phase;
                this.exit.Reset();
                this.log.Info($"Boss maze: {phase}, {this.rocks.Count} rocks stand");
            }

            if ((phase == MazePhase.None && !this.pillarsFall) || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                (this.Up, this.Lead) = (false, null);
                this.safeSpot.Reset();
                return null;
            }

            this.Up = true;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var body = player.ModelBounds.X;
            int Octant(Vector2 offset) => ScreenProjection.Octant(player.WorldPosition, offset);
            return phase switch
            {
                MazePhase.Hole => this.ToHole(here, body, Octant, heartbeat, now),
                MazePhase.Rings or MazePhase.Lines => this.ToExit(phase, here, body, Octant, heartbeat, now),
                _ => this.ToStrip(here, body, Octant, heartbeat, now),
            };
        }

        private string? ToHole(Vector2 here, float body, Func<Vector2, int> octant, bool heartbeat, long now)
        {
            var hole = this.AreaOf(MazeRound.HoleSkill) is { Shape: AttackAreas.Donut, Width: > 0f } area ? area.Width : (float?)null;
            return this.ToSafe(this.round.Centre, here, body, hole, octant, now, word => Announcements.BossClone(hole != null, word.Octant, word.Steps, heartbeat));
        }

        private string? ToStrip(Vector2 here, float body, Func<Vector2, int> octant, bool heartbeat, long now)
        {
            if (this.pillars.Count < 2 || this.middle is not { } arena)
            {
                (this.Up, this.Lead) = (false, null);
                return null;
            }

            // The nearest point of the middle line, between the two pillars.
            var across = Vector2.Normalize(this.pillars[1] - this.pillars[0]);
            var spot = here - (across * Vector2.Dot(here - arena, across));
            return this.ToSafe(spot, here, body, StripWorld, octant, now, word => Announcements.BossPillars(word.Octant, word.Steps, heartbeat));
        }

        private string? ToSafe(Vector2 spot, Vector2 here, float body, float? hole, Func<Vector2, int> octant, long now, Func<SafeSpotNews, string> first)
        {
            var news = this.safeSpot.Update(this.roundId, spot, true, here, body, hole, octant, now);
            this.Lead = this.safeSpot.Inside ? null : (spot, hole is { } radius ? SafeSpotGuide.LeadRadius(radius, body) : 0f);
            if (news is not { } word)
            {
                return null;
            }

            this.log.Info($"Boss maze: {word.Kind}, player {here.X:0},{here.Y:0}, {Vector2.Distance(spot, here):0} from {spot.X:0},{spot.Y:0}");
            return word.Kind switch
            {
                SafeSpotNewsKind.First => first(word),
                SafeSpotNewsKind.Inside => Announcements.BossCloneReached,
                SafeSpotNewsKind.Out => Announcements.BossCloneLeft(word.Octant, word.Steps),
                _ => Announcements.BossCloneWay(word.Octant, word.Steps),
            };
        }

        private string? ToExit(MazePhase phase, Vector2 here, float body, Func<Vector2, int> octant, bool heartbeat, long now)
        {
            this.safeSpot.Reset();
            if (this.rocks.Count == 0 && !this.round.SecondDrawn)
            {
                (this.Up, this.Lead) = (false, null);
                return null; // the walls have not risen yet
            }

            var (step, isOut) = phase == MazePhase.Rings ? this.OutOfRings(here, body) : this.ThroughLines(here, body);
            this.Lead = isOut ? null : (step.At, 0f);
            if (this.exit.Update(step, isOut, here, octant, now) is not { } word)
            {
                return null;
            }

            this.log.Info($"Boss maze: {phase} {word.Kind}, wall {step.Wall}, to {step.At.X:0},{step.At.Y:0}, player {here.X:0},{here.Y:0}, {this.rocks.Count} rocks stand");
            return word.Kind switch
            {
                MazeExitNewsKind.First => Announcements.BossMaze(word.Octant, word.Steps, heartbeat),
                MazeExitNewsKind.Out => Announcements.OutOfBlast,
                _ => Announcements.BossCloneWay(word.Octant, word.Steps),
            };
        }

        private (MazeStep Step, bool Out) OutOfRings(Vector2 here, float body)
        {
            var centre = this.round.Centre;
            var reach = (this.AreaOf(MazeRound.RingSkill)?.Length ?? 0f) + body + AttackAim.MarginWorld;
            var beyond = Vector2.Distance(here, centre) >= reach;
            if (MazeWay.Rings(this.rocks, here, centre) is { } step && !beyond)
            {
                return (step, false);
            }

            return (new MazeStep(this.Clear(here, point => Vector2.Distance(point, centre) >= reach) ?? (here + (here - centre)), -1), beyond);
        }

        private (MazeStep Step, bool Out) ThroughLines(Vector2 here, float body)
        {
            var forward = Vector2.Normalize((this.middle ?? this.round.Centre) - this.round.Centre);
            var goal = this.round.Centre + (forward * (LineReachWorld + BeyondWorld));
            if (this.line is { } zone)
            {
                // Beyond the end of the blast that has ground: it starts at the arena's edge behind him.
                forward = zone.Forward;
                goal = zone.Origin + (forward * (zone.Shape.Length + body + BeyondWorld));
                if (!Walkable(goal) && Walkable(zone.Origin - (forward * (body + BeyondWorld))))
                {
                    (goal, forward) = (zone.Origin - (forward * (body + BeyondWorld)), -forward);
                }
            }

            var beyond = this.line?.Catches(here, body + AttackAim.MarginWorld) is { } caught ? !caught : Vector2.Dot(here - goal, forward) >= 0f;
            return (MazeWay.Lines(this.rocks, here, forward, goal), beyond);
        }

        /// <summary>The nearest place walked to straight that is clear.</summary>
        private Vector2? Clear(Vector2 here, Func<Vector2, bool> clear)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var (grid, row) = (area.GridWalkableData, area.TerrainMetadata.BytesPerRow);
            return grid == null || grid.Length == 0 || row <= 0 ? null
                : FloodGeometry.NearestDry(here, clear,
                    (from, to) => BeaconMath.LineWalkable(grid, row, from / AttackAreas.WorldPerCell, to / AttackAreas.WorldPerCell), 50f, 1000f, 16);
        }

        private static bool Walkable(Vector2 at)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            return RoutePlanner.IsWalkable(area.GridWalkableData, area.TerrainMetadata.BytesPerRow, (int)(at.X / AttackAreas.WorldPerCell), (int)(at.Y / AttackAreas.WorldPerCell));
        }

        private void Note(Entity entity, int kind, Vector2 at, ref bool broken, long now)
        {
            switch (kind)
            {
                case 2 when StateOf(entity, ShatterState) == 0:
                    this.rocks.Add(new MazeRock(at, StateOf(entity, "left") == 1, StateOf(entity, "right") == 1));
                    break;
                case 3:
                    this.middle = at;
                    break;
                case 4:
                    this.pillars.Add(at);
                    broken |= StateOf(entity, BrokenState) > 0;
                    break;
                case 5 when !this.raised.ContainsKey(entity.Id):
                    this.raised[entity.Id] = (at, now, false);
                    break;
            }
        }

        /// <summary>The field of raised rocks, its dangers handed on as its rocks turn up.</summary>
        private void TellRaised(long now)
        {
            foreach (var (id, rock) in this.raised.Where(pair => !pair.Value.Told).ToList())
            {
                if (now - rock.SeenAt > FieldMs)
                {
                    this.raised[id] = rock with { Told = true };
                }
                else if (this.raised.Any(other => other.Key != id && Math.Abs(other.Value.SeenAt - rock.SeenAt) <= FieldMs))
                {
                    this.raised[id] = rock with { Told = true };
                    this.Danger(RaisedDangers, rock.At, rock.SeenAt);
                }
            }
        }

        /// <summary>1 the boss, 2 a rock of the maze, 3 the arena's middle, 4 a pillar, 5 a raised rock, 0 none: asked once per entity.</summary>
        private int KindOf(Entity entity)
        {
            if (!this.kinds.TryGetValue(entity.Id, out var kind))
            {
                var path = BossMechanicsCatalog.MonsterId(entity.Path);
                if (path.Length == 0)
                {
                    return 0; // not read yet
                }

                bool Is(string start) => path.StartsWith(start, StringComparison.OrdinalIgnoreCase);
                this.kinds[entity.Id] = kind = !Is(Folder) ? 0 : Is(RockPath) ? 2 : Is(MiddlePath) ? 3 : Is(PillarPath) ? 4 : Is(RaisedPath) ? 5 : Is(Boss) ? 1 : 0;
            }

            return kind;
        }

        private static long StateOf(Entity entity, string name) =>
            entity.TryGetComponent<StateMachine>(out var machine)
                ? machine.States.Where(state => string.Equals(state.Name, name, StringComparison.OrdinalIgnoreCase)).Select(state => state.Value).FirstOrDefault()
                : 0;
    }
}
