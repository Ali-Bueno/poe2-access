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

    /// <summary>How <see cref="EntityCatalog"/> sorts an entity into a scanner category and names it, and the component flags it reads.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>How far from a cage the one it holds may be: Risu was one cell (about 11) from hers; the Karui elders are not measured.</summary>
        private const float CageWorld = 100f;

        /// <summary>Files of minimap marks the scanner left out, logged once each.</summary>
        private readonly HashSet<string> silentMarks = new(StringComparer.Ordinal);
        private readonly HashSet<string> namedFromPath = new(StringComparer.Ordinal);

        /// <summary>
        ///     Category and name, or null for what the scanner leaves out (self, dead, opened, unnamed, and what is
        ///     not in play: the town holds every story variant of an NPC, only one of them targetable).
        /// </summary>
        private ScanEntry? Classify(Entity entity, Render render)
        {
            var path = entity.Path ?? string.Empty;
            var leagueIcon = LeagueDeviceIcon(entity, path);
            // A league's altar stays listed out of play; what a quest icon marks does not: a Trial's reward is spent once used.
            var trap = TrialTraps.KindOf(path);
            if ((leagueIcon == null || MinimapMarks.IsQuestIcon(leagueIcon)) && trap == null && !this.InPlay(entity))
            {
                return null;
            }

            var name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
            var kind = string.Empty;
            var plain = false;
            var subject = string.Empty;
            var baseId = string.Empty;
            var loot = LootTier.Valuable;
            var sound = string.Empty;
            var timed = false;
            var icon = entity.TryGetComponent<MinimapIcon>(out var minimap) ? minimap.IconName ?? string.Empty : string.Empty;
            ScanCategory? category;
            if (icon == MinimapMarks.StoryGlyph)
            {
                // Typed NPC but a sign: "Pueblo de Ogham" by the road was listed as a character and taken for the quest's
                // village (user 2026-10-28).
                category = name.Length > 0 ? ScanCategory.Objects : null;
                name = Announcements.Sign(name);
                kind = MinimapMarks.SignKind;
            }
            else if (entity.EntityType == EntityTypes.Player)
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
                // A rock or an egg breaks like an urn and shows no label; it sounded like a chest (user 2026-10-26). A
                // switch or lever built as a chest (the mausoleum's secret door switch) sounded like a breakable (2026-10-27);
                // a body searched for loot sounded like a chest (2026-10-28).
                kind = Switches.IsSwitch(path) ? "switch" : Breakables.IsCorpse(path) ? "corpse"
                    : chest is { IsLabelVisible: false, IsStrongbox: false } ? "breakable" : "chest";
                sound = kind == "breakable" ? Breakables.Material(path) : string.Empty;
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
                name = name.Length > 0 ? name : this.landmarks.WorldTitle(path);
                name = name.Length > 0 ? name : Announcements.LeagueDevice(leagueIcon, path.Contains(BossDevice, StringComparison.Ordinal),
                    ObjectNames.LeagueName(minimap is null ? string.Empty : MinimapIconLabel.Of(this.memory, this.elements, minimap), path));
                kind = MinimapMarks.DeviceKind(leagueIcon);
            }
            else if (trap is { } kindOfTrap)
            {
                // Untargetable and flagged hidden, yet there on the floor for anyone who can see.
                category = TrialTraps.IsSeenBeforeItStrikes(kindOfTrap) ? ScanCategory.PointsOfInterest : null;
                name = Announcements.TrialTrapName(kindOfTrap);
                kind = TrapKind;
            }
            else if (entity.EntityType == EntityTypes.Monster && (path.StartsWith(CreatureFolder, StringComparison.Ordinal) || !entity.TryGetComponent<MinimapIcon>(out _)))
            {
                // A "monster" outside the creatures that the minimap marks is a device: it goes on as a plain object below.
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
            else if (!MinimapMarks.IsQuestIcon(icon) && entity.TryGetComponent<TriggerableBlockage>(out var blockage))
            {
                // Tutorial beams and gates: where the way is shut, while it is (BarrierWatch says when one opens).
                // A barrier stays one for the guide, so what the story wants opened sounds as a quest object through its Sound: the
                // Ancient Seal. Under the quest's icon it is no barrier but a quest object: Balbala's three Runic Seals have this
                // component too, walk-through and not blocked, and were listed nowhere, so the fight's start was found by luck (user 2026-11-07).
                category = blockage.IsBlocked ? ScanCategory.Objects : null;
                sound = name.Length > 0 && MinimapMarks.IsStoryBarrier(path) ? MinimapMarks.QuestObjectKind : string.Empty;
                name = name.Length > 0 ? name : Announcements.ClosedBarrier;
                kind = "barrier";
            }
            else
            {
                category = entity.TryGetComponent<Targetable>(out _) ? ScanCategory.Objects : null; // stash, bench, well
                plain = true;
                if (this.landmarks.ObjectiveOf(path) is { Length: > 0 } objective)
                {
                    if (!this.QuestTexts().Any(text => text.Contains(objective, StringComparison.Ordinal)))
                    {
                        return null; // a Death Crystal in a room of another kind, called out from 352 steps off (user 2026-11-07)
                    }

                    timed = true;
                }

                // Levers in Trial rooms open the way out: SanctumGenericLever, a plain object, sounded nothing, and must stand out (user 2026-11-07).
                sound = Switches.IsSwitch(path) ? MinimapMarks.QuestObjectKind : sound;
                if (name.Length == 0)
                {
                    // Named only in a WorldDescription, which the mod cannot read: the real Relic Altar, after its named stand-in leaves
                    // play on the first use, and the Trial's Death Crystals (user 2026-11-07). The game explains such an object in a box
                    // of its own because it is there to be used, so it sounds as a quest object: the Blood Chalice, the Sand Hourglass.
                    name = this.landmarks.WorldTitle(path);
                    sound = name.Length > 0 ? MinimapMarks.QuestObjectKind : sound;
                }
            }

            // Only a plain object can be a mark: a used shrine, an opened chest or a minion stays out whatever its icon says.
            if (plain && (category == null || name.Length == 0) && icon.Length > 0 &&
                MinimapMarks.PointOfInterest(path, icon, this.MarkLabel(path, icon, minimap!, render)) is { } point)
            {
                // A boss's arena, a secret or a quest landmark as the minimap labels it: the objects carry no name of their own.
                category = ScanCategory.PointsOfInterest;
                kind = point.Kind;
                subject = point.Kind == MinimapMarks.BossRoomKind ? point.Label : string.Empty;
                name = point.Kind != MinimapMarks.BossRoomKind ? point.Label
                    : point.Defeated ? Announcements.BossRoomDefeated(point.Label) : Announcements.BossRoom(point.Label);
            }

            if (plain && category == ScanCategory.Objects && name.Length == 0)
            {
                // A new interactable no table names yet: its metadata file name, so it is heard instead of dropped.
                name = ObjectNames.FromPath(path);
                if (name.Length > 0 && this.namedFromPath.Add(path))
                {
                    this.log.Info($"Object named from its file: {path} -> {name}");
                }
            }

            if (category == null || name.Length == 0)
            {
                if (icon.Length > 0 && !MinimapMarks.IsSpent(icon) && !path.StartsWith(CreatureFolder, StringComparison.Ordinal) && this.silentMarks.Add(path))
                {
                    // What the minimap draws and the scanner does not list may be a quest's target: logged to find the gap.
                    this.log.Info($"Minimap mark not listed: {path} (icon {icon}, {entity.EntityType})");
                }

                return null; // unnamed placeholders are not things a player can find
            }


            if (category == ScanCategory.Npcs && this.HasQuestMarker(entity))
            {
                // D2R's NPC "!": the game's quest indicator over the head (someone to talk to for the story).
                name = Announcements.NpcWithQuest(name);
                kind = "npc_quest";
            }

            var position = render.WorldPosition;
            return new ScanEntry(entity.Id, category.Value, name, position.X, position.Y, position.Z, MinimapMarks.KindUnder(icon, kind), baseId, loot, sound,
                MinimapMarks.IsQuestIcon(icon), subject, timed);
        }

        /// <summary>
        ///     What the minimap labels a mark. The quest's own icon needs no label to be what the quest wants: Risu's cage in the
        ///     Mawdun Mine has none, no name and an empty world label, and was found by neither the scanner nor the button; said
        ///     as a bare "Objeto de misión" it did not tell what it was (user 2026-11-07).
        /// </summary>
        private string MarkLabel(string path, string icon, MinimapIcon minimap, Render render)
        {
            var label = MinimapIconLabel.Of(this.memory, this.elements, minimap);
            if (label.Length > 0 || !MinimapMarks.IsQuestIcon(icon))
            {
                return label;
            }

            if (!MinimapMarks.IsCage(path))
            {
                return Announcements.QuestObject;
            }

            // The character at a cage: caged, they are out of play and listed nowhere.
            var held = MinimapMarks.BareName(this.NameNear(render, CageWorld, other => other.EntityType == EntityTypes.NPC));
            return held.Length > 0 ? Announcements.CageOf(held) : Announcements.Cage;
        }

        /// <summary>The first name, in the client's language, of an awake entity within reach of a place that passes the test, in play or not.</summary>
        private string NameNear(Render thing, float reach, Func<Entity, bool> passes)
        {
            var place = new Vector2(thing.WorldPosition.X, thing.WorldPosition.Y);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && passes(entity) && entity.TryGetComponent<Render>(out var render) &&
                    Vector2.Distance(place, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) < reach &&
                    this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is { Length: > 0 } name)
                {
                    return name;
                }
            }

            return string.Empty;
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

        /// <summary>Ids of the items lying on the ground (no names read): an item that leaves the inventory and lands here was dropped.</summary>
        public IReadOnlyCollection<uint> GroundItemIds()
        {
            var ids = new HashSet<uint>();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
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
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.Id == id && entity.IsValid && entity.TryGetComponent<WorldItem>(out var item))
                {
                    return this.inventory.DescribeItemEntity(item.ItemEntityAddress);
                }
            }

            return null;
        }

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

        /// <summary>The scanner's kind of a Trial's trap.</summary>
        private const string TrapKind = "trap";

        /// <summary>
        ///     The minimap icon of a league device (the Ritual altar, Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable,
        ///     icon "RitualRune"), or null; its rune object and light share the folder but carry no icon (dump 2026-10-26).
        /// </summary>
        private static string? LeagueDeviceIcon(Entity entity, string path) =>
            entity.TryGetComponent<MinimapIcon>(out var icon) && !string.IsNullOrEmpty(icon.IconName) &&
            (path.Contains("/Leagues/", StringComparison.Ordinal) || MinimapMarks.IsRitualAltar(icon.IconName) ||
             (!path.StartsWith(CreatureFolder, StringComparison.Ordinal) && EndgameCatalog.Shared.IsObjectiveIcon(icon.IconName))) // a breach, an essence: what a map objective asks for
                ? icon.IconName
                : null;

        /// <summary>A device whose file names a boss (CanopyBossRitualAltar): it summons the boss once the area's quest is done.</summary>
        private const string BossDevice = "Boss";

        /// <summary>A living creature that is not the player's (minions are friendly).</summary>
        public static bool IsAliveEnemy(Entity entity) =>
            entity.Path.StartsWith(CreatureFolder, StringComparison.Ordinal) && !IsFrozenDead(entity) &&
            entity.TryGetComponent<Life>(out var life) && life.IsAlive &&
            !(entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly);

        private bool InPlay(Entity entity) => IsInPlay(this.memory, entity);

        /// <summary>Set aside by the game (<see cref="BuffIds.HiddenMonster"/>): alive and flagged targetable, yet out of the fight.</summary>
        public static bool IsHidden(Entity entity) =>
            entity.TryGetComponent<Buffs>(out var buffs) && buffs.StatusEffects.Keys.Any(BuffIds.IsHiddenMonster);

        /// <summary>Not hidden and targetable by its own flag (<see cref="GameLayout.TargetableComponent"/>); no component: in play.</summary>
        public static bool IsInPlay(HostMemoryReader memory, Entity entity) =>
            !IsHidden(entity) &&
            (!EntityFields.InPlayTrusted || !entity.TryGetComponent<Targetable>(out var target) ||
             (memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetableOffset, out var flag) && flag != 0));
    }
}
