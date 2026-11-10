// <copyright file="Positioned.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     The <see cref="Positioned" /> component in the entity.
    /// </summary>
    public class Positioned : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Positioned" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Positioned" /> component.</param>
        public Positioned(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets a value indicating whether the entity is friendly or not.
        /// </summary>
        public bool IsFriendly { get; private set; }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<PositionedOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.IsFriendly = EntityHelper.IsFriendly(data.Reaction);
        }
    }
}