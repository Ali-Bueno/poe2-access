// <copyright file="Life.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     The <see cref="Life" /> component in the entity.
    /// </summary>
    public class Life : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Life" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Life" /> component.</param>
        public Life(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets a value indicating whether the entity is alive or not.
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        /// <summary>
        ///     Gets the health related information of the entity.
        /// </summary>
        public VitalStruct Health { get; private set; }

        /// <summary>
        ///     Gets the energyshield related information of the entity.
        /// </summary>
        public VitalStruct EnergyShield { get; private set; }

        /// <summary>
        ///     Gets the mana related information of the entity.
        /// </summary>
        public VitalStruct Mana { get; private set; }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<LifeOffset>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Health = data.Health;
            this.EnergyShield = data.EnergyShield;
            this.Mana = data.Mana;
            this.IsAlive = data.Health.Current > 0;
        }

    }
}
