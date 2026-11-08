namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Whether the player is in a fight, so items on the ground and characters' lines fall quiet (user 2026-10-30: a
    ///     talking boss, the mod's speech and drops ringing at once were too much): an alive enemy in play within the range
    ///     the item sounds carry starts a fight with any enemy, a rare or unique one also a fight with rares
    ///     (<see cref="FightState"/>, <see cref="FightRoster"/>).
    /// </summary>
    public sealed class FightWatch
    {
        private const int PollMs = 250;

        private readonly HostMemoryReader memory;
        private readonly FightRoster roster = new();
        private readonly FightState rares = new();
        private readonly FightState anyEnemy = new();
        private long lastPoll;

        public FightWatch(HostMemoryReader memory) => this.memory = memory;

        /// <summary>Whether a fight the mode counts is on (<see cref="FightState.Modes"/>); never for "never".</summary>
        public bool InFight(string mode)
        {
            var now = Environment.TickCount64;
            return mode switch
            {
                FightState.RaresAndUniques => this.rares.IsOn(now),
                FightState.AnyEnemy => this.anyEnemy.IsOn(now),
                _ => false,
            };
        }

        /// <summary>Per frame; while not playing nothing is seen and a fight ends once its hold runs out.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var (anyNear, rareNear) = this.EnemiesNear(player, now);
            this.anyEnemy.Update(anyNear, now);
            this.rares.Update(rareNear, now);
            this.roster.Prune(now);
        }

        /// <summary>Research: each fight and the alive enemies in range, for the dev command.</summary>
        public string Diagnose()
        {
            var now = Environment.TickCount64;
            var lines = new List<string> { $"Fight: rares {this.rares.IsOn(now)}, any {this.anyEnemy.IsOn(now)}" };
            if (Seats.Followed.TryGetComponent<Render>(out var player))
            {
                foreach (var entity in this.AliveInRange(player))
                {
                    var inPlay = EntityCatalog.IsInPlay(this.memory, entity);
                    lines.Add($"  {entity.Id} {entity.Path}: in play {inPlay}, counts {this.roster.Counts(entity.Id, inPlay, now)}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private (bool Any, bool Rare) EnemiesNear(Render player, long now)
        {
            var (any, rare) = (false, false);
            foreach (var entity in this.AliveInRange(player))
            {
                if (this.roster.Counts(entity.Id, EntityCatalog.IsInPlay(this.memory, entity), now))
                {
                    any = true;
                    rare |= entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity is Rarity.Rare or Rarity.Unique;
                }
            }

            return (any, rare);
        }

        private IEnumerable<GameReader.RemoteObjects.States.InGameStateObjects.Entity> AliveInRange(Render player)
        {
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.EntityType == EntityTypes.Monster && EntityCatalog.IsAliveEnemy(entity) &&
                    entity.TryGetComponent<Render>(out var render) &&
                    Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) <= ObjectSounds.RangeWorld)
                {
                    yield return entity;
                }
            }
        }
    }
}
