namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The minions around the player for the vitals readout (D2R minion report): friendly living creatures without the
    ///     NPC component (EntityCatalog's rule), how many of each and their life. A party member's minions count too: the
    ///     client marks them friendly alike.
    /// </summary>
    public sealed class MinionReport
    {
        private readonly UiElementReader elements;

        public MinionReport(UiElementReader elements)
        {
            this.elements = elements;
        }

        /// <summary>Each minion near the followed player: its name and life percent.</summary>
        public IReadOnlyList<(string Name, int LifePercent)> Near()
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<(string, int)>();
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var minions = new List<(string, int)>();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (IsMinion(entity) && entity.TryGetComponent<Render>(out var render) && entity.TryGetComponent<Life>(out var life) &&
                    Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) <= ObjectSounds.RangeWorld &&
                    this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is { Length: > 0 } name)
                {
                    minions.Add((name, life.Health.CurrentInPercent()));
                }
            }

            return minions;
        }

        private static bool IsMinion(Entity entity) =>
            entity.IsValid && entity.EntityType == EntityTypes.Monster && entity.Path.StartsWith(EntityCatalog.CreatureFolder, StringComparison.Ordinal) &&
            !EntityCatalog.IsFrozenDead(entity) && entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly &&
            entity.TryGetComponent<Life>(out var life) && life.IsAlive && !entity.TryGetComponent<NPC>(out _);
    }
}
