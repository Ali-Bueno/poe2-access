namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Whether the player is in a fight, so items on the ground stop sounding and being named (user 2026-10-30: a
    ///     talking boss, the mod's speech and drops ringing at once were too much): an alive enemy the mode counts, within
    ///     the range the item sounds carry, starts it (<see cref="FightState"/>).
    /// </summary>
    public sealed class FightWatch
    {
        private const int PollMs = 250;

        private readonly FightState state = new();
        private long lastPoll;

        /// <summary>Items keep quiet now.</summary>
        public bool InFight { get; private set; }

        /// <summary>Per frame while playing; <paramref name="mode"/> is the setting (<see cref="FightState.Modes"/>).</summary>
        public void Tick(bool playing, string mode)
        {
            var now = Environment.TickCount64;
            if (!playing || mode == CullThreshold.Never)
            {
                this.state.Reset();
                this.InFight = false;
                return;
            }

            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            this.InFight = this.state.Update(Seats.Followed.TryGetComponent<Render>(out var player) && EngagedNear(player, mode), now);
        }

        private static bool EngagedNear(Render player, string mode)
        {
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity) ||
                    !entity.TryGetComponent<Render>(out var render) ||
                    Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) > ObjectSounds.RangeWorld)
                {
                    continue;
                }

                var rarity = entity.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal;
                if (FightState.Engages(mode, rarity is Rarity.Rare or Rarity.Unique))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
