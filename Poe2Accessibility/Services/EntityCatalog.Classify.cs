namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>How <see cref="EntityCatalog"/> sorts an entity into a scanner category and names it, and the component flags it reads.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>
        ///     Category and name, or null for what the scanner leaves out (self, dead, opened, unnamed, and what is
        ///     not in play: the town holds every story variant of an NPC, only one of them targetable).
        /// </summary>
        private ScanEntry? Classify(Entity entity, Render render)
        {
            var path = entity.Path ?? string.Empty;
            var leagueIcon = LeagueDeviceIcon(entity, path);
            if (leagueIcon == null && !this.InPlay(entity))
            {
                return null;
            }

            var name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
            var kind = string.Empty;
            var baseId = string.Empty;
            var loot = LootTier.Valuable;
            ScanCategory? category;
            if (entity.EntityType == EntityTypes.Player)
            {
                category = Seats.IsFollowed(entity) ? null : ScanCategory.Players;
                if (entity.TryGetComponent<Player>(out var other) && other.Name.Length > 0)
                {
                    name = Announcements.OtherPlayer(other.Name, name, other.Level); // the render name is the class
                }
            }
            else if (path.Contains("AreaTransition", StringComparison.Ordinal))
            {
                category = ScanCategory.Exits;
                kind = "exit";
            }
            else if (path.Contains("Waypoint", StringComparison.Ordinal) || path.Contains("Checkpoint", StringComparison.Ordinal) ||
                     path.Contains("Portal", StringComparison.Ordinal))
            {
                category = ScanCategory.Waypoints;
                kind = path.Contains("Checkpoint", StringComparison.Ordinal) ? "checkpoint" : path.Contains("Waypoint", StringComparison.Ordinal) ? "waypoint" : "portal";
            }
            else if (entity.TryGetComponent<WorldItem>(out var worldItem))
            {
                category = ScanCategory.Items;
                name = this.inventory.DescribeItemEntity(worldItem.ItemEntityAddress, name) ?? name;
                (kind, loot) = this.inventory.ItemKind(worldItem.ItemEntityAddress);
                if (name.Split(',')[0].Trim() is { Length: > 0 } title)
                {
                    this.itemTitles[title] = kind == "gold" ? string.Empty : name; // "Oro, 2": the title before the amount
                }

                if (this.inventory.ItemBaseName(worldItem.ItemEntityAddress) is { Length: > 0 } baseName)
                {
                    this.itemTitles.TryAdd(baseName, kind == "gold" ? string.Empty : name); // a rare's tooltip names its base on a line of its own
                }
                baseId = this.inventory.ItemBaseId(worldItem.ItemEntityAddress);
            }
            else if (entity.EntityType == EntityTypes.Chest)
            {
                var chest = entity.TryGetComponent<Chest>(out var component) ? component : null;
                category = chest is { IsOpened: true } ? null : ScanCategory.Chests;
                // A rock or an egg breaks like an urn and shows no label; it sounded like a chest (user 2026-10-26).
                kind = chest is { IsLabelVisible: false, IsStrongbox: false } ? "breakable" : "chest";
            }
            else if (entity.EntityType == EntityTypes.Shrine)
            {
                category = entity.TryGetComponent<Shrine>(out var shrine) && shrine.IsUsed ? null : ScanCategory.Chests;
                kind = "shrine";
            }
            else if (leagueIcon != null)
            {
                // Listed while the game makes it untargetable too (during the ritual): it is where the fight is and,
                // once done, where its tribute is spent ("Interactúa para ver los favores").
                category = ScanCategory.Encounters;
                name = name.Length > 0 ? name : Announcements.LeagueDevice(leagueIcon);
                kind = leagueIcon == "RitualRune" ? "ritual" : "encounter";
            }
            else if (entity.EntityType == EntityTypes.Monster)
            {
                // A friendly monster with the NPC component is a character (Mortimer by the Miller); minions have none.
                category = IsAliveEnemy(entity) ? ScanCategory.Enemies : entity.TryGetComponent<NPC>(out _) ? ScanCategory.Npcs : null;
                if (category == ScanCategory.Enemies && entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity != Rarity.Normal)
                {
                    name = Announcements.EnemyWithRarity(name, magic.Rarity);
                }
            }
            else if (entity.EntityType == EntityTypes.NPC || entity.TryGetComponent<NPC>(out _))
            {
                category = ScanCategory.Npcs;
            }
            else if (entity.TryGetComponent<TriggerableBlockage>(out var blockage))
            {
                // Tutorial beams and gates: where the way is shut, while it is (BarrierWatch says when one opens).
                category = blockage.IsBlocked ? ScanCategory.Objects : null;
                name = name.Length > 0 ? name : Announcements.ClosedBarrier;
                kind = "barrier";
            }
            else
            {
                category = entity.TryGetComponent<Targetable>(out _) ? ScanCategory.Objects : null; // stash, bench, well
            }

            if (category == null || name.Length == 0)
            {
                return null; // unnamed placeholders are not things a player can find
            }

            if (category == ScanCategory.Npcs && this.HasQuestMarker(entity))
            {
                // D2R's NPC "!": the game's quest indicator over the head (someone to talk to for the story).
                name = Announcements.NpcWithQuest(name);
                kind = "npc_quest";
            }

            var position = render.WorldPosition;
            return new ScanEntry(entity.Id, category.Value, name, position.X, position.Y, position.Z, kind, baseId, loot);
        }

        /// <summary>Feeds the entity name check (judged once on enough monsters, characters and players).</summary>
        private void CheckLayout(Entity entity, Render render, List<IntPtr> renders)
        {
            if ((entity.EntityType == EntityTypes.Monster && IsAliveEnemy(entity)) || entity.EntityType is EntityTypes.NPC or EntityTypes.Player)
            {
                renders.Add(render.Address);
            }
        }

        /// <summary>The NPC shows the quest indicator over its head (<see cref="GameLayout.NpcComponent"/>).</summary>
        private bool HasQuestMarker(Entity entity) =>
            entity.TryGetComponent<NPC>(out var npc) && (this.Fields?.HasMarkerAt(npc.Address, GameLayout.NpcComponent.MarkerRowOffset) ?? false);

        /// <summary>
        ///     What A uses now: the entity the game flags as the player's target (<see
        ///     cref="GameLayout.TargetableComponent.IsTargetedOffset"/>), named as the scanner names it; monsters are the
        ///     target bar's.
        /// </summary>
        public ScanEntry? InteractTarget()
        {
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.TryGetComponent<Targetable>(out var target) &&
                    this.memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetedOffset, out var flag) && flag != 0 &&
                    entity.TryGetComponent<Render>(out var render) && this.Classify(entity, render) is { } entry &&
                    entry.Category is not ScanCategory.Enemies)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        ///     GameHelper stops reading a monster once the game flags it dead (stat is_dead) and marks it Useless, so its
        ///     life can stay where it was ("Ezomita antiguo" frozen at 16/50 mid-hit, sounded and listed as an enemy that
        ///     could not be hit, 2026-10-26); a monster that rises again is read and unmarked by GameHelper.
        /// </summary>
        public static bool IsFrozenDead(Entity entity) => entity.EntityState == EntityStates.Useless;

        /// <summary>
        ///     The game's creatures all live under this folder; a "monster" elsewhere is a league device given life (the
        ///     Ritual rune, Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable, sounded as an enemy, 2026-10-26).
        /// </summary>
        public const string CreatureFolder = "Metadata/Monsters/";

        /// <summary>
        ///     The minimap icon of a league device (the Ritual altar, Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable,
        ///     icon "RitualRune"), or null; its rune object and light share the folder but carry no icon (dump 2026-10-26).
        /// </summary>
        private static string? LeagueDeviceIcon(Entity entity, string path) =>
            path.Contains("/Leagues/", StringComparison.Ordinal) && entity.TryGetComponent<MinimapIcon>(out var icon) &&
            !string.IsNullOrEmpty(icon.IconName)
                ? icon.IconName
                : null;

        /// <summary>A living creature that is not the player's (minions are friendly).</summary>
        public static bool IsAliveEnemy(Entity entity) =>
            entity.Path.StartsWith(CreatureFolder, StringComparison.Ordinal) && !IsFrozenDead(entity) &&
            entity.TryGetComponent<Life>(out var life) && life.IsAlive &&
            !(entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly);

        private bool InPlay(Entity entity) => IsInPlay(this.memory, entity);

        /// <summary>Targetable by its own flag (<see cref="GameLayout.TargetableComponent"/>); no component: in play.</summary>
        public static bool IsInPlay(HostMemoryReader memory, Entity entity) =>
            !EntityFields.InPlayTrusted || !entity.TryGetComponent<Targetable>(out var target) ||
            (memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetableOffset, out var flag) && flag != 0);
    }
}
