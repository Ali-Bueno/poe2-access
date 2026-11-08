// <copyright file="Actor.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using System.Collections.Generic;
    using GameHelper.Utils;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.FilesStructures;
    using RemoteEnums;

    /// <summary>
    ///     The <see cref="Actor" /> component in the entity.
    /// </summary>
    public class Actor : ComponentBase
    {
        // private Dictionary<IntPtr, VaalSoulStructure> ActiveSkillsVaalSouls { get; } = new();

        private Dictionary<uint, ActiveSkillCooldown> ActiveSkillCooldowns { get; } = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="Actor" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Actor" /> component.</param>
        public Actor(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets a value indicating what the player is doing.
        /// </summary>
        public Animation Animation { get; private set; }

        /// <summary>
        ///     Gets the details of all known Active Skills.
        /// </summary>
        public Dictionary<string, ActiveSkillDetails> ActiveSkills { get; } = new();

        /// <summary>
        ///     Gets a value indicating if the skill can be used or not.
        /// </summary>
        public HashSet<string> IsSkillUsable { get; } = new();

        /// <summary>
        ///     Gets the "command minion" skills available on this player's summoned minions,
        ///     keyed by skill name (e.g. "KnifeThrow"). The value is <c>true</c> when the command
        ///     is usable on at least one summoned minion (i.e. that minion has it off cooldown).
        ///     These commands carry their cooldown on the minion, not the player, so they never
        ///     appear in <see cref="IsSkillUsable" />. Only populated for the player's own actor.
        /// </summary>
        public Dictionary<string, bool> MinionCommandSkills { get; } = new();

        /// <summary>
        ///     Gets the names of <see cref="MinionCommandSkills" /> that are currently usable on at
        ///     least one summoned minion. Parallels <see cref="IsSkillUsable" /> for minion commands.
        /// </summary>
        public HashSet<string> UsableMinionCommandSkills { get; } = new();

        /// <summary>Gets the current deployed entity records, including ownership IDs and source skill IDs.</summary>
        public IReadOnlyList<DeployedEntityStructure> DeployedEntityRecords { get; private set; } = Array.Empty<DeployedEntityStructure>();

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            base.CleanUpData();
            this.DeployedEntityRecords = Array.Empty<DeployedEntityStructure>();
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<ActorOffset>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Animation = (Animation)data.AnimationId;
            this.IsSkillUsable.Clear();
            this.ActiveSkills.Clear();
            this.ActiveSkillCooldowns.Clear();
            // var skillsvaalsouls = reader.ReadStdVector<VaalSoulStructure>(data.VaalSoulsPtr);
            // for (var i = 0; i < skillsvaalsouls.Length; i++)
            // {
            //     this.ActiveSkillsVaalSouls[skillsvaalsouls[i].ActiveSkillsDatPtr] = skillsvaalsouls[i];
            // }

            var cooldowns = reader.ReadStdVector<ActiveSkillCooldown>(data.CooldownsPtr);
            for (var i = 0; i < cooldowns.Length; i++)
            {
                this.ActiveSkillCooldowns[cooldowns[i].UnknownIdAndEquipmentInf0] = cooldowns[i];
            }

            var activeSkills = reader.ReadStdVector<ActiveSkillStructure>(data.ActiveSkillsPtr);
            for (var i = 0; i < activeSkills.Length; i++)
            {
                var skillDetails = reader.ReadMemory<ActiveSkillDetails>(activeSkills[i].ActiveSkillPtr);
                if (skillDetails.GrantedEffectsPerLevelDatRow == IntPtr.Zero ||
                    (skillDetails.UnknownIdAndEquipmentInfo >> 0x10) < 0x8000)
                {
                    // No usecase for these skills.
                    // this.ActiveSkills[i.ToString()] = skillDetails;
                }
                else
                {
                    (var name, skillDetails.ActiveSkillsDatPtr) = ((string, IntPtr))Core.GgpkObjectCache.
                        AddOrGetExisting(skillDetails.GrantedEffectsPerLevelDatRow, (key) =>
                        {
                            return (reader.ReadUnicodeString(reader.ReadMemory<IntPtr>(key)), key);
                        });

                    // skillDetails.CurrentVaalSouls = -1;
                    var cannotbeused = false;
                    if (this.ActiveSkillCooldowns.TryGetValue(skillDetails.UnknownIdAndEquipmentInfo, out var cooldownInfo))
                    {
                        cannotbeused |= cooldownInfo.CannotBeUsed();
                    }
                    // else if (this.ActiveSkillsVaalSouls.TryGetValue(skillDetails.ActiveSkillsDatPtr, out var vaalSoulInfo))
                    // {
                    //     skillDetails.CurrentVaalSouls = vaalSoulInfo.CurrentSouls;
                    //     cannotbeused |= vaalSoulInfo.CannotBeUsed();
                    // }

                    this.ActiveSkills[name] = skillDetails;
                    if (!cannotbeused)
                    {
                        this.IsSkillUsable.Add(name);
                    }
                }
            }

            var deployedEntities = reader.ReadStdVector<DeployedEntityStructure>(data.DeployedEntityArray);
            this.DeployedEntityRecords = Array.AsReadOnly(deployedEntities);
            this.UpdateMinionCommandSkills(deployedEntities);
        }

        /// <summary>
        ///     Aggregates the "command minion" skills off the player's summoned minions into
        ///     <see cref="MinionCommandSkills" /> / <see cref="UsableMinionCommandSkills" />. Only the
        ///     player's own actor scans (gated on the owner being the current player); other actors
        ///     just clear their (empty) collections cheaply.
        /// </summary>
        /// <param name="deployedEntities">the deployed-entity array already read for this actor.</param>
        private void UpdateMinionCommandSkills(DeployedEntityStructure[] deployedEntities)
        {
            this.MinionCommandSkills.Clear();
            this.UsableMinionCommandSkills.Clear();

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (deployedEntities.Length == 0 ||
                this.OwnerEntityAddress == IntPtr.Zero ||
                this.OwnerEntityAddress != area.Player.Address)
            {
                return;
            }

            var deployedIds = new HashSet<uint>();
            for (var i = 0; i < deployedEntities.Length; i++)
            {
                deployedIds.Add((uint)deployedEntities[i].EntityId);
            }

            foreach (var (_, entity) in area.AwakeEntities)
            {
                if (!entity.IsValid ||
                    !deployedIds.Contains(entity.Id) ||
                    !entity.TryGetComponent<Actor>(out var minionActor))
                {
                    continue;
                }

                minionActor.CollectCommandSkills(this.MinionCommandSkills);
            }

            foreach (var (name, usable) in this.MinionCommandSkills)
            {
                if (usable)
                {
                    this.UsableMinionCommandSkills.Add(name);
                }
            }
        }

        /// <summary>
        ///     Reads this (minion) actor's "command minion" skills (keys in the <c>0x40000000</c>
        ///     range) and folds them into <paramref name="aggregate" /> by name, OR-ing usability so
        ///     a command counts as usable when any minion has it off cooldown.
        /// </summary>
        /// <param name="aggregate">player-level name -> usable map to add to.</param>
        private void CollectCommandSkills(Dictionary<string, bool> aggregate)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<ActorOffset>(this.Address);
            var skills = reader.ReadStdVector<ActiveSkillStructure>(data.ActiveSkillsPtr);
            for (var i = 0; i < skills.Length; i++)
            {
                if (skills[i].ActiveSkillPtr == IntPtr.Zero)
                {
                    continue;
                }

                var skillDetails = reader.ReadMemory<ActiveSkillDetails>(skills[i].ActiveSkillPtr);

                // Command skills sit at sequential keys starting at 0x40000000; everything else
                // (shared 0x2000xxxx internals, etc.) is not a player-issuable command.
                // TODO: this still over-includes. A minion can have skills in the 0x40000000 range
                // that the player has NO command bound for (minion-type-specific abilities without a
                // player command), so they show up as "usable commands" the player can't actually
                // issue. To filter to only player-commandable skills we'd need to cross-reference the
                // player's own command entries (which aren't in the player skill list this session)
                // or find another ownership/command marker on the skill.
                if ((skillDetails.UnknownIdAndEquipmentInfo & 0xFFFF0000u) != 0x40000000u ||
                    skillDetails.GrantedEffectsPerLevelDatRow == IntPtr.Zero)
                {
                    continue;
                }

                // TODO: this is the internal GGPK skill name (GrantedEffectsPerLevel row). It can
                // differ noticeably from the in-game command name the player sees. If we want the
                // friendlier command-skill names, we'd need to map these via another dat table.
                var (name, _) = ((string, IntPtr))Core.GgpkObjectCache.AddOrGetExisting(
                    skillDetails.GrantedEffectsPerLevelDatRow,
                    key => (reader.ReadUnicodeString(reader.ReadMemory<IntPtr>(key)), key));
                var usable = !(this.ActiveSkillCooldowns.TryGetValue(
                    skillDetails.UnknownIdAndEquipmentInfo, out var cooldown) && cooldown.CannotBeUsed());

                aggregate[name] = aggregate.TryGetValue(name, out var existing) ? existing || usable : usable;
            }
        }

    }
}
