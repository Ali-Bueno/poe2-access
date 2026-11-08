// <copyright file="ObjectMagicProperties.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.Utils;
    using GameOffsets.Natives;
    using GameOffsets.Objects.Components;
    using RemoteEnums;

    /// <summary>
    ///     ObjectMagicProperties component of the entity.
    /// </summary>
    public class ObjectMagicProperties : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ObjectMagicProperties" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="ObjectMagicProperties" /> component.</param>
        public ObjectMagicProperties(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets a value indicating entity rarity information.
        /// </summary>
        public Rarity Rarity { get; private set; } = Rarity.Normal;

        /// <summary>
        ///     Gets the Mods and their values of the entity.
        ///     If a mod doesn't have a value, it will be represented by
        ///     <see cref="float.NaN"/>.
        /// </summary>
        public List<(string name, (float value0, float value1) values)> Mods = new();

        /// <summary>
        ///     Gets the Mod names that exists on the entity.
        /// </summary>
        public HashSet<string> ModNames = new();

        /// <summary>
        ///     Gets the stats that are on the entity due to entity Mods.
        /// </summary>
        public Dictionary<GameStats, int> ModStats = new();

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<ObjectMagicPropertiesOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Rarity = (Rarity)data.Details1.Rarity;

            if (hasAddressChanged)
            {
                this.Mods.Clear();
                this.ModNames.Clear();
                AddToMods(this.Mods, reader.ReadStdVector<ModArrayStruct>(data.Details1.Mods.ImplicitMods));
                AddToMods(this.Mods, reader.ReadStdVector<ModArrayStruct>(data.Details1.Mods.ExplicitMods));
                AddToMods(this.Mods, reader.ReadStdVector<ModArrayStruct>(data.Details1.Mods.EnchantMods));
                AddToMods(this.Mods, reader.ReadStdVector<ModArrayStruct>(data.Details1.Mods.HellscapeMods));
                AddToMods(this.Mods, reader.ReadStdVector<ModArrayStruct>(data.Details1.Mods.CrucibleMods));
                _ = this.Mods.All(k => this.ModNames.Add(k.name));
                base.StatUpdator(this.ModStats, data.Details1.StatsFromMods);
            }
        }

        internal static void AddToMods(List<(string name, (float value0, float value1) values)> collection, ModArrayStruct[] mods)
        {
            for (var i = 0; i < mods.Length; i++)
            {
                var mod = mods[i];
                if (mod.ModsPtr != IntPtr.Zero)
                {
                    collection.Add((GetModName(mod.ModsPtr), GetValue(mod.Values, mod.Value0)));
                }
            }
        }

        internal static string GetModName(IntPtr modsDatRowAddress)
        {
            return Core.GgpkStringCache.AddOrGetExisting(modsDatRowAddress, key =>
            {
                var reader = Core.Process.Handle;
                return reader.ReadUnicodeString(reader.ReadMemory<IntPtr>(key));
            });
        }

        internal static (float, float) GetValue(StdVector valuesPtr, int value0)
        {
            var totalElements = valuesPtr.TotalElements(0x04);
            if (totalElements == 0)
            {
                return (float.NaN, float.NaN);
            }
            else if(totalElements == 1)
            {
                return (value0, float.NaN);
            }
            else
            {
                var values = Core.Process.Handle.ReadStdVector<int>(valuesPtr);
                if (values.Length > 1)
                {
                    return (values[0], values[1]);
                }
                else
                {
                    return (float.NaN, float.NaN);
                }
            }
        }
    }
}