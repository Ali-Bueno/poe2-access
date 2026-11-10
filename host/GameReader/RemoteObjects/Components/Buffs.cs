// <copyright file="Buffs.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.Components
{
    using System;
    using System.Collections.Generic;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.FilesStructures;
    using Utils;

    /// <summary>
    ///     The <see cref="Buffs" /> component in the entity.
    /// </summary>
    public class Buffs : ComponentBase
    {

        /// <summary>
        ///     Initializes a new instance of the <see cref="Buffs" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Buffs" /> component.</param>
        public Buffs(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets the Buffs/Debuffs associated with the entity.
        ///     This is not updated anymore once entity dies.
        ///     A plain dictionary, refilled in place on the thread that updates the entity and read on the frame thread only
        ///     (docs/research/performance.md): the concurrent one cost a node per buff per frame and a list per Keys.
        /// </summary>
        public Dictionary<string, StatusEffectStruct> StatusEffects { get; } = new();

        public bool[] FlaskActive { get; private set; } = new bool[5];

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<BuffsOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.StatusEffects.Clear();
            using var statusEffects = reader.RentStdVector<IntPtr>(data.StatusEffectPtr);
            Array.Fill(this.FlaskActive, false);

            // F-129: snapshot the 4-level Player.Id chain once. Each access goes
            // through RemoteObjectBase.Address.get (a lock); re-traversing per-loop
            // is both costly and racy during state transitions. NRE during the
            // chain -> playerId stays uint.MaxValue, all flask matches fail
            // (statusEffectData.SourceEntityId is uint, max value is unreachable).
            uint playerId;
            try
            {
                playerId = Core.States.InGameStateObject.CurrentAreaInstance.Player.Id;
            }
            catch (NullReferenceException)
            {
                playerId = uint.MaxValue;
            }

            var effects = statusEffects.Span;
            for (var i = 0; i < effects.Length; i++)
            {
                var statusEffectData = reader.ReadMemory<StatusEffectStruct>(effects[i]);
                if (statusEffectData.BuffDefinationPtr == IntPtr.Zero)
                {
                    continue;
                }

                if (playerId != statusEffectData.SourceEntityId)
                {
                    statusEffectData.FlaskSlot = -1;
                }

                MiscHelper.ActiveSkillGemDataParser(
                    statusEffectData.UnknownIdAndEquipmentInfo,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out var skillGemUnknownId);

                // A status effect freed after the list was read leaves a garbage definition pointer: a name read through it
                // that is no buff id serves this frame only and is never kept for the area.
                var (effectName, effectType) = ((string, byte))Core.GgpkObjectCache.GetOrRead(
                    statusEffectData.BuffDefinationPtr,
                    key => GetNameFromBuffDefination(key),
                    definition => StructShapes.IsBuffId((((string Name, byte))definition).Name));

                if (effectType != 0x4) // Flask Effect Type is 4.
                {
                    statusEffectData.FlaskSlot = -1;
                }
                else if (statusEffectData.FlaskSlot >= 0 && statusEffectData.FlaskSlot < 5)
                {
                    this.FlaskActive[statusEffectData.FlaskSlot] = true;
                }

                if (skillGemUnknownId != 0)
                {
                    effectName += $"_{skillGemUnknownId:X}";
                }

                if (this.StatusEffects.TryGetValue(effectName, out var oldValue))
                {
                    // The entry already kept counts too: three entries without a count of their own are three stacks.
                    statusEffectData.Charges = BuffStacks.Merged(oldValue.Charges, statusEffectData.Charges);
                    statusEffectData.TimeLeft = Math.Max(oldValue.TimeLeft, statusEffectData.TimeLeft);
                }

                this.StatusEffects[effectName] = statusEffectData;
            }
        }

        /// <summary>
        ///     Adds a status effect read from an entity-backed player skill to the player's
        ///     normal status-effect collection. A real player status effect with the same
        ///     name wins unless the synthetic effect reports more stacks.
        /// </summary>
        /// <param name="effectName">Name used by normal status-effect consumers.</param>
        /// <param name="statusEffectData">Status-effect data with its stage represented as charges.</param>
        internal void AddSyntheticStatusEffect(string effectName, StatusEffectStruct statusEffectData)
        {
            if (!this.StatusEffects.TryGetValue(effectName, out var oldValue) || statusEffectData.Charges > oldValue.Charges)
            {
                this.StatusEffects[effectName] = statusEffectData;
            }
        }

        private static (string, byte) GetNameFromBuffDefination(IntPtr addr)
        {
            var reader = Core.Process.Handle;
            return reader.TryReadMemory<BuffDefinitionsOffset>(addr, out var data)
                ? (reader.ReadUnicodeString(data.Name), data.BuffType)
                : (string.Empty, (byte)0);
        }
    }
}
