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

    /// <summary>Static predicates on an entity: alive, corpse, in play, character, scenery, hidden.</summary>
    public sealed partial class EntityCatalog
    {
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
        ///     A player's cosmetic pet (Metadata/Pet/OtterPet/GrayOtter): it follows its owner and nothing is done with it, yet
        ///     another player's was "Objeto sin nombre, Gray Otter" in town.
        /// </summary>
        public static bool IsPet(string path) => path.StartsWith("Metadata/Pet/", StringComparison.Ordinal);

        /// <summary>A terrain object of this area's own folder ("Metadata/Terrain/…/P2_1/Objects/Wayfinder3Pointer" in P2_1).</summary>
        public static bool IsAreaOwnObject(string path, string areaId) =>
            areaId.Length > 0 && path.StartsWith("Metadata/Terrain/", StringComparison.Ordinal) &&
            path.Contains($"/{areaId}/Objects/", StringComparison.OrdinalIgnoreCase);

        /// <summary>The scanner's kind of a Trial's trap.</summary>
        private const string TrapKind = "trap";

        /// <summary>The loaded model (.ao) of an entity and its animated object's path, which tell apart objects sharing one file (Expedition markers).</summary>
        internal static string ModelOf(Entity entity) =>
            entity.TryGetComponent<Animated>(out var animated) ? $"{animated.ModelPath} {animated.Path}".Trim() : string.Empty;

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

        /// <summary>
        ///     A living creature that is not the player's (minions are friendly) and not one to talk to now
        ///     (<see cref="IsTalkable"/>): town NPCs read hostile, and so do Doryani and Freya Hartlin between their fights (issue #9).
        /// </summary>
        public static bool IsAliveEnemy(Entity entity) =>
            entity.Path.StartsWith(CreatureFolder, StringComparison.Ordinal) && !IsFrozenDead(entity) &&
            entity.TryGetComponent<Life>(out var life) && life.IsAlive &&
            !(entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly) &&
            !IsTalkable(entity) && !IsProp(entity);

        /// <summary>
        ///     A thing under the creatures' folder with a life and no side, yet no creature: neither the Monster component nor one
        ///     to be targeted by. The pirates' trails of gunpowder (Pirates/objects/GunpowderLines, life 1) growled all along
        ///     them; every other living hostile of 132 dumps (639 of them) has both (owner, 2026-11-24).
        /// </summary>
        private static bool IsProp(Entity entity) => !entity.HasComponent(MonsterComponent) && !entity.TryGetComponent<Targetable>(out _);

        /// <summary>The game's component of a creature; the reader has no class for it.</summary>
        private const string MonsterComponent = "Monster";

        /// <summary>
        ///     A character by its type or NPC component. Its path names no portal: the Ziggurat Encampment's Servi, Oswald,
        ///     Hooded One and Alva wait by the time portal as `…NearTimePortal` and `…AfterTimePortalEntered` (G3_town), and were
        ///     said as "Portal: Servi" from the past half of the camp, 200 steps off (the owner, 2026-11-20).
        /// </summary>
        private static bool IsNpc(Entity entity) => entity.EntityType == EntityTypes.NPC || entity.TryGetComponent<NPC>(out _);

        /// <summary>One A talks to now: the NPC component's Friendly flag where it reads, else a character the player cannot attack.</summary>
        public static bool IsTalkable(Entity entity) => Friendly(entity) ?? (IsCharacter(entity) && !Attackable(entity));

        /// <summary>The NPC component's Friendly flag (<see cref="GameLayout.NpcComponent.FriendlyOffset"/>); null without one, unread or untrusted.</summary>
        public static bool? Friendly(Entity entity) =>
            EntityFields.FriendlyTrusted && entity.TryGetComponent<NPC>(out var npc) &&
            HostMemoryReader.Read<byte>(npc.Address + GameLayout.NpcComponent.FriendlyOffset, out var flag) && flag <= 1
                ? flag != 0
                : null;

        /// <summary>The scanner's kind of a dead monster's corpse, told apart from a body searched for loot (kind "corpse", a chest).</summary>
        public const string CorpseKind = "monster_corpse";

        /// <summary>
        ///     A dead creature whose corpse is still there to use: read dead by the game's own word (the is_dead stat,
        ///     which freezes its life where it fell, or a life of nothing), never a unique (Bind Spectre takes none), a
        ///     character or one of the player's own. A monster that rises again is read alive once it does
        ///     (<see cref="IsFrozenDead"/>) and leaves the category then.
        /// </summary>
        public static bool IsCorpse(Entity entity) =>
            entity.EntityType == EntityTypes.Monster && entity.Path.StartsWith(CreatureFolder, StringComparison.Ordinal) &&
            (IsFrozenDead(entity) || (entity.TryGetComponent<Life>(out var life) && !life.IsAlive)) &&
            !IsCharacter(entity) && !IsHidden(entity) &&
            !(entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly) &&
            !(entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity == Rarity.Unique);

        private bool InPlay(Entity entity) => IsInPlay(this.memory, entity);

        /// <summary>A character the game holds out of play who walks (<see cref="WalkingCharacters"/>): an escort, followed rather than reached.</summary>
        public bool IsEscort(uint id) => this.walking.Walked(id);

        /// <summary>The player can attack it (<see cref="GameLayout.TargetableComponent.AttackableOffset"/>); unread counts as yes.</summary>
        private static bool Attackable(Entity entity) =>
            !EntityFields.InPlayTrusted || !entity.TryGetComponent<Targetable>(out var target) ||
            !HostMemoryReader.Read<byte>(target.Address + GameLayout.TargetableComponent.AttackableOffset, out var flag) || flag != 0;

        /// <summary>
        ///     Someone to talk to: the NPC component, or a creature the game lets the player use (InteractionAction), as Sekhema
        ///     Asala in the Dreadnought, who has no NPC component (issue #7).
        /// </summary>
        public static bool IsCharacter(Entity entity) =>
            entity.TryGetComponent<NPC>(out _) || (entity.EntityType == EntityTypes.Monster && entity.HasComponent(InteractionActionComponent));

        /// <summary>
        ///     A figure of a scene, with a character's name and nothing to talk to: the NPC component with neither Targetable nor
        ///     InteractionAction (the Countess and Kanu by Benedictus, who sounded on after they had left or died, 2026-11-25;
        ///     7 of the 401 NPC files: they extend "nothing").
        /// </summary>
        public static bool IsScenery(Entity entity) =>
            entity.TryGetComponent<NPC>(out _) && !entity.TryGetComponent<Targetable>(out _) && !entity.HasComponent(InteractionActionComponent);

        /// <summary>The game's component that makes an entity usable; the reader has no class for it.</summary>
        private const string InteractionActionComponent = "InteractionAction";

        /// <summary>A character listed and sounded as an enemy, logged once with what the game said of it (issue #9).</summary>
        private void LogCharacterAsEnemy(Entity entity, string path)
        {
            if (this.hostileCharacters.Add(path))
            {
                var rarity = entity.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal;
                this.log.Info($"Character listed as an enemy: {path} ({rarity}, NPC component {entity.TryGetComponent<NPC>(out _)}, friendly {Friendly(entity)?.ToString() ?? "unread"}, in play {this.InPlay(entity)})");
            }
        }

        /// <summary>A character the scanner leaves out, logged once per file with the reason, so a missing one is found from the log.</summary>
        private void LogLeftOutCharacter(Entity entity, string path, string reason)
        {
            if (IsCharacter(entity) && this.silentCharacters.Add(path))
            {
                this.log.Info($"Character not listed: {path} ({reason})");
            }
        }

        /// <summary>Set aside by the game (<see cref="BuffIds.HiddenMonster"/>): alive and flagged targetable, yet out of the fight.</summary>
        public static bool IsHidden(Entity entity)
        {
            if (EssencePrison.Holds(entity.Id))
            {
                return true; // in an Essence's crystal until it shatters: it cannot be hit
            }

            if (!entity.TryGetComponent<Buffs>(out var buffs))
            {
                return false;
            }

            // Asked of every monster by every sound each frame: no LINQ here.
            foreach (var buff in buffs.StatusEffects.Keys)
            {
                if (BuffIds.IsHiddenMonster(buff))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Not hidden, targetable by its own flag and reached by the quest (<see cref="GameLayout.TargetableComponent"/>:
        ///     the rune seals sounded before their step, user 2026-11-11); no component: in play.
        /// </summary>
        public static bool IsInPlay(HostMemoryReader memory, Entity entity) =>
            // The game's own interact test reads no buff (exe 0.5.5f, 0x1A7AE0): a set-aside mark does not hide a character.
            (!IsHidden(entity) || IsCharacter(entity)) &&
            (!EntityFields.InPlayTrusted || !entity.TryGetComponent<Targetable>(out var target) ||
             (memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetableOffset, out var flag) && flag != 0 &&
              (!EntityFields.QuestStateTrusted ||
               !memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.MeetsQuestStateOffset, out var quest) || quest != 0)));
    }
}
