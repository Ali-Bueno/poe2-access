// <copyright file="Mods.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.Components
{
    using System;
    using System.Collections.Generic;
    using GameReader.RemoteEnums;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     The <see cref="Mods" /> component in the entity.
    /// </summary>
    public class Mods : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Mods" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Mods" /> component.</param>
        public Mods(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets a value indicating item rarity information.
        /// </summary>
        public Rarity Rarity { get; private set; } = Rarity.Normal;

        /// <summary>
        ///     Gets the mods and their values of the entity.
        ///     If a mod doesn't have a value, it will be represented by
        ///     <see cref="float.NaN"/>.
        /// </summary>
        public List<(string name, (float value0, float value1) values)>
            ImplicitMods = new(),
            ExplicitMods = new(),
            EnchantMods = new(),
            HellscapeMods = new();

        /// <summary>
        ///     The same lists with every value each entry holds, in the entry's order, and the entries without a Mods.dat
        ///     row kept under an empty name: what an item's lines are written from (docs/research/item-mods.md).
        /// </summary>
        public List<(string name, int[] values)>
            ImplicitModValues = new(),
            ExplicitModValues = new(),
            EnchantModValues = new(),
            HellscapeModValues = new();

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<ModsOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Rarity = (Rarity)data.Details0.Rarity;

            if (hasAddressChanged)
            {
                this.ImplicitMods.Clear();
                this.ExplicitMods.Clear();
                this.EnchantMods.Clear();
                this.HellscapeMods.Clear();
                this.ImplicitModValues.Clear();
                this.ExplicitModValues.Clear();
                this.EnchantModValues.Clear();
                this.HellscapeModValues.Clear();
                Add(this.ImplicitMods, this.ImplicitModValues, reader.ReadStdVector<ModArrayStruct>(data.Details0.Mods.ImplicitMods));
                Add(this.ExplicitMods, this.ExplicitModValues, reader.ReadStdVector<ModArrayStruct>(data.Details0.Mods.ExplicitMods));
                Add(this.EnchantMods, this.EnchantModValues, reader.ReadStdVector<ModArrayStruct>(data.Details0.Mods.EnchantMods));
                Add(this.HellscapeMods, this.HellscapeModValues, reader.ReadStdVector<ModArrayStruct>(data.Details0.Mods.HellscapeMods));
                Add(this.HellscapeMods, this.HellscapeModValues, reader.ReadStdVector<ModArrayStruct>(data.Details0.Mods.CrucibleMods));
            }
        }

        private static void Add(List<(string name, (float value0, float value1) values)> collection, List<(string name, int[] values)> whole, ModArrayStruct[] mods)
        {
            foreach (var mod in mods)
            {
                // One read of the values for both lists: the first keeps two of them, as before.
                var name = mod.ModsPtr == IntPtr.Zero ? string.Empty : ObjectMagicProperties.GetModName(mod.ModsPtr);
                var values = ObjectMagicProperties.GetValues(mod.Values, mod.Value0);
                whole.Add((name, values));
                if (mod.ModsPtr != IntPtr.Zero)
                {
                    collection.Add((name, (values.Length > 0 ? values[0] : float.NaN, values.Length > 1 ? values[1] : float.NaN)));
                }
            }
        }
    }
}