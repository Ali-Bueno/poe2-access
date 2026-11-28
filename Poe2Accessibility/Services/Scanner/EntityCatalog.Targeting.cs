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
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>The quest marker, the ground items and the target the game aims at.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>The NPC shows the quest indicator over its head (<see cref="GameLayout.NpcComponent"/>).</summary>
        private bool HasQuestMarker(Entity entity) =>
            entity.TryGetComponent<NPC>(out var npc) && (this.Fields?.HasMarkerAt(npc.Address, GameLayout.NpcComponent.MarkerRowOffset) ?? false);

        /// <summary>Ids of the items lying on the ground (no names read): an item that leaves the inventory and lands here was dropped.</summary>
        public IReadOnlyCollection<uint> GroundItemIds()
        {
            var ids = new HashSet<uint>();
            foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (entity.IsValid && entity.TryGetComponent<WorldItem>(out _))
                {
                    ids.Add(entity.Id);
                }
            }

            return ids;
        }

        /// <summary>The name of the ground item with this id, as the inventory names it; null when it is not there.</summary>
        public string? GroundItemName(uint id)
        {
            foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (entity.Id == id && entity.IsValid && entity.TryGetComponent<WorldItem>(out var item))
                {
                    return this.inventory.DescribeItemEntity(item.ItemEntityAddress);
                }
            }

            return null;
        }

        /// <summary>For the log as A goes down: every entity the game flags as targeted, by file, minimap icon and script states.</summary>
        public string TargetTrace()
        {
            var targets = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values
                .Where(entity => entity.IsValid && this.IsTargeted(entity))
                .Select(entity =>
                {
                    var icon = entity.TryGetComponent<MinimapIcon>(out var minimap) ? minimap.IconName ?? string.Empty : string.Empty;
                    var states = string.Join(",", StatesOf(entity).Select(state => $"{state.Name}={state.Value}"));
                    return $"{entity.Id} {entity.Path} icon \"{icon}\" states [{states}]";
                })
                .ToList();
            return targets.Count == 0 ? "nothing targeted" : string.Join("; ", targets);
        }

        /// <summary>The game flags the entity as the player's target (<see cref="GameLayout.TargetableComponent.IsTargetedOffset"/>).</summary>
        private bool IsTargeted(Entity entity) =>
            entity.TryGetComponent<Targetable>(out var target) &&
            this.memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetedOffset, out var flag) && flag != 0;

        /// <summary>An item on the ground as it reads now, for its tooltip; null for anything else.</summary>
        private GroundTooltip.Item? GroundItemOf(Entity entity)
        {
            if (!entity.IsValid || !entity.TryGetComponent<WorldItem>(out var worldItem) || !entity.TryGetComponent<Render>(out var render))
            {
                return null;
            }

            var label = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
            var spoken = this.inventory.DescribeItemEntity(worldItem.ItemEntityAddress, label) ?? label;
            var gold = this.inventory.ItemKind(worldItem.ItemEntityAddress).Kind == "gold";
            return new GroundTooltip.Item(GroundTooltip.TitleOf(spoken), this.inventory.ItemBaseName(worldItem.ItemEntityAddress), gold ? string.Empty : spoken);
        }

        /// <summary>
        ///     What A uses now: the entity the game flags as the player's target (<see
        ///     cref="GameLayout.TargetableComponent.IsTargetedOffset"/>), named as the scanner names it; monsters are the
        ///     target bar's.
        /// </summary>
        public ScanEntry? InteractTarget()
        {
            foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (!entity.IsValid || !this.IsTargeted(entity) || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                // The game keeps a Trial of Chaos lever targeted while it is shut (the lift away): "Interruptor, A" and A did
                // nothing (the owner, 2026-11-19).
                // Nor is a lever whose lift already stands by it: pulling it does nothing.
                var piece = ChaosTrial.PieceOf(entity.Path ?? string.Empty);
                if ((piece is ChaosPiece.LiftLever or ChaosPiece.Selector && !ChaosTrial.IsEnabled(StatesOf(entity))) ||
                    (piece == ChaosPiece.LiftLever && this.LiftAwayFrom(render) == false))
                {
                    continue;
                }

                // The game targets the monster an essence holds, and A frees it: said as the essence. Held, it is out of play for
                // every other reader (EssencePrison), so it is asked first.
                if (EssencePrison.Holds(entity.Id) && this.EssenceHolding(render) is { } holding)
                {
                    return holding;
                }

                if (this.Classify(entity, render) is { } entry)
                {
                    if (entry.Category is not ScanCategory.Enemies)
                    {
                        return entry;
                    }

                    // The game targets the monster an essence holds, and A frees it: said as the essence, not left silent as a foe.
                    if (this.EssenceHolding(render) is { } essence)
                    {
                        return essence;
                    }
                }
            }

            return null;
        }
    }
}
