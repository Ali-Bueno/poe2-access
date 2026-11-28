namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Objects.States.InGameState;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>A fought boss that must be made to throw at a part of its arena (<see cref="BossThrows"/>): the target to use now, read from the fight's entities.</summary>
    public sealed class BossThrowWatch
    {
        /// <summary>The fight's entities are looked for this often (ms); where the player and the boss stand is read each time it is asked.</summary>
        private const int PollMs = 150;

        private readonly EntityPlaces positions;
        private readonly PluginLog log;
        private readonly BossThrows throws = new();
        private readonly List<(Vector2 At, bool Busy)> helpers = new();
        private readonly List<Vector2> targets = new();
        private readonly Dictionary<uint, int> kinds = new();
        private Vector2? target;
        private Entity? thrower;
        private bool carrying;
        private long lastPoll;

        public BossThrowWatch(EntityPlaces positions, PluginLog log)
        {
            this.positions = positions;
            this.log = log;
        }

        /// <summary>The target to use now; null when no helper is at work.</summary>
        public BossThrowAim? Aim
        {
            get
            {
                if (this.target is not { } at || this.thrower is not { } boss || !Seats.Followed.TryGetComponent<Render>(out var player))
                {
                    return null;
                }

                // The boss kept from the last poll may be gone with its area: only an entity still among the area's is read.
                if (!Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.TryGetValue(new EntityNodeKey { id = boss.Id }, out var still) ||
                    !still.IsValid || !still.TryGetComponent<Render>(out var render))
                {
                    return null;
                }

                var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
                return new BossThrowAim(at, this.carrying, BossThrows.LinedUp(new Vector2(render.WorldPosition.X, render.WorldPosition.Y), here, at));
            }
        }

        public void Reset()
        {
            this.throws.Reset();
            this.kinds.Clear();
            this.target = null;
            this.thrower = null;
            this.carrying = false;
        }

        /// <summary>At each poll of the boss mechanics, with the bosses being fought.</summary>
        public void Poll(IReadOnlyList<string> fighting, long now)
        {
            var fight = BossThrows.Torvian;
            if (!fighting.Any(monster => monster.StartsWith(fight.Boss, StringComparison.OrdinalIgnoreCase)))
            {
                this.Reset();
                return;
            }

            if (now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            this.helpers.Clear();
            this.targets.Clear();
            Entity? boss = null;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || this.KindOf(entity, fight) is not (> 0 and var kind) || this.positions.Of(entity) is not { } at)
                {
                    continue;
                }

                switch (kind)
                {
                    case 1 when EntityCatalog.IsAliveEnemy(entity):
                        boss = entity;
                        break;
                    case 2:
                        this.helpers.Add((at, StateOf(entity, fight.Busy) is var state && state > 0 && state < fight.Stopped));
                        break;
                    case 3:
                        this.targets.Add(at);
                        break;
                }
            }

            var carried = boss != null && StateOf(boss, fight.Carries) > 0;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var picked = boss == null ? null : this.throws.Pick(this.helpers, this.targets, here);
            if (picked != this.target || carried != this.carrying)
            {
                this.log.Info($"Boss throw: target {(picked is { } to ? $"{to.X:0},{to.Y:0}" : "none")}, {this.helpers.Count(helper => helper.Busy)} of {this.helpers.Count} helpers at work, " +
                              $"carrying {carried}, player {here.X:0},{here.Y:0}, boss {(boss != null && this.positions.Of(boss) is { } stands ? $"{stands.X:0},{stands.Y:0}" : "?")}, " +
                              $"lined up {this.Aim?.LinedUp}");
            }

            (this.target, this.carrying, this.thrower) = (picked, carried, boss);
        }

        /// <summary>1 the boss, 2 a helper, 3 a target, 0 none: asked once per entity.</summary>
        private int KindOf(Entity entity, BossThrowFight fight)
        {
            if (!this.kinds.TryGetValue(entity.Id, out var kind))
            {
                var path = BossMechanicsCatalog.MonsterId(entity.Path);
                if (path.Length == 0)
                {
                    return 0; // not read yet
                }

                this.kinds[entity.Id] = kind = string.Equals(path, fight.Target, StringComparison.OrdinalIgnoreCase) ? 3
                    : path.StartsWith(fight.Helper, StringComparison.OrdinalIgnoreCase) ? 2
                    : path.StartsWith(fight.Boss, StringComparison.OrdinalIgnoreCase) && entity.TryGetComponent<StateMachine>(out var machine) &&
                      machine.States.Any(state => string.Equals(state.Name, fight.Carries, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
            }

            return kind;
        }

        private static long StateOf(Entity entity, string name) =>
            entity.TryGetComponent<StateMachine>(out var machine)
                ? machine.States.Where(state => string.Equals(state.Name, name, StringComparison.OrdinalIgnoreCase)).Select(state => state.Value).FirstOrDefault()
                : 0;
    }
}
