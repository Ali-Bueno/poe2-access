namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>
    ///     The evidence of a boss's arena (docs/research/boss-mechanics.md §12): while a boss is fought, each thing that
    ///     appears near it (walls, objects, daemons, helpers, adds) is logged once with its place and states, and each new
    ///     reading of its states after that. The files do not say which object is the way out of a mechanic; a fight does.
    ///     Nothing is spoken.
    /// </summary>
    public sealed class BossArenaLog
    {
        private const int PollMs = 500;

        /// <summary>A boss arena around the boss (world units), as <see cref="WeakPointGuide"/> takes it.</summary>
        private const float ArenaWorld = 2000f;

        /// <summary>Entities of one path logged per fight: a ring of walls is whole, a stream of projectiles is not.</summary>
        private const int PerPath = 24;

        /// <summary>State readings logged per entity, and lines per fight.</summary>
        private const int PerEntity = 12;

        private const int PerFight = 800;

        /// <summary>A boss unseen this long (ms) and then fought again is a new fight.</summary>
        private const int NewFightMs = 5000;

        private readonly PluginLog log;
        private readonly Dictionary<uint, (string States, int Readings)> seen = new();
        private readonly Dictionary<string, int> perPath = new(StringComparer.Ordinal);
        private int lines;
        private long lastPoll;
        private long lastFought;

        public BossArenaLog(PluginLog log)
        {
            this.log = log;
        }

        /// <summary>Where a boss being fought stands (<see cref="BossMechanicsWatch.Fighting"/>); null when none is.</summary>
        public Func<Vector2?> Fighting { get; set; } = () => null;

        /// <summary>A new area: nothing of the last arena is known.</summary>
        public void Reset()
        {
            this.seen.Clear();
            this.perPath.Clear();
            this.lines = 0;
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (this.Fighting() is not { } boss)
            {
                return;
            }

            if (now - this.lastFought > NewFightMs)
            {
                this.Reset();
            }

            this.lastFought = now;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (this.lines >= PerFight)
                {
                    return;
                }

                if (entity.IsValid && entity.EntityType != EntityTypes.Player)
                {
                    this.Note(entity, boss);
                }
            }
        }

        private void Note(Entity entity, Vector2 boss)
        {
            var place = "no place";
            if (entity.TryGetComponent<Render>(out var render))
            {
                var at = new Vector2(render.WorldPosition.X, render.WorldPosition.Y);
                var gap = Vector2.Distance(at, boss);
                if (gap > ArenaWorld)
                {
                    return;
                }

                place = $"at {at.X:0},{at.Y:0}, {gap:0} from the boss";
            }

            var states = entity.TryGetComponent<StateMachine>(out var machine)
                ? string.Join(",", machine.States.Where(state => state.Value != 0).Select(state => $"{state.Name}={state.Value}"))
                : "-";
            if (!this.seen.TryGetValue(entity.Id, out var known))
            {
                var count = this.perPath.GetValueOrDefault(entity.Path);
                if (count >= PerPath)
                {
                    return;
                }

                this.perPath[entity.Path] = count + 1;
                this.seen[entity.Id] = (states, 1);
                this.Line($"Boss arena: + {entity.Path} #{entity.Id} ({entity.EntityType}) {place} states [{states}]");
            }
            else if (known.States != states && known.Readings < PerEntity)
            {
                this.seen[entity.Id] = (states, known.Readings + 1);
                this.Line($"Boss arena: {entity.Path} #{entity.Id} {place} states [{states}]");
            }
        }

        private void Line(string text)
        {
            this.lines++;
            this.log.Info(text);
        }
    }
}
