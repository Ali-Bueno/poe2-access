namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Whether the player is in a fight, so items on the ground and characters' lines fall quiet (user 2026-10-30: a
    ///     talking boss, the mod's speech and drops ringing at once were too much): an alive enemy within the range the
    ///     item sounds carry starts a fight with any enemy, a rare or unique one also a fight with rares (<see cref="FightState"/>).
    /// </summary>
    public sealed class FightWatch
    {
        private const int PollMs = 250;

        private readonly FightState rares = new();
        private readonly FightState anyEnemy = new();
        private long lastPoll;

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
            var (anyNear, rareNear) = EnemiesNear(player);
            this.anyEnemy.Update(anyNear, now);
            this.rares.Update(rareNear, now);
        }

        private static (bool Any, bool Rare) EnemiesNear(Render player)
        {
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var any = false;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity) ||
                    !entity.TryGetComponent<Render>(out var render) ||
                    Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) > ObjectSounds.RangeWorld)
                {
                    continue;
                }

                any = true;
                if (entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity is Rarity.Rare or Rarity.Unique)
                {
                    return (true, true);
                }
            }

            return (any, false);
        }
    }
}
