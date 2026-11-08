// <copyright file="Transitionable.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{

    using System;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     The <see cref="Transitionable" /> component in the entity.
    /// </summary>
    public class Transitionable : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Transitionable" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Transitionable" /> component.</param>
        public Transitionable(IntPtr address) : base(address)
        {
        }

        /// <summary>
        ///     Gets the transitionable state that the entity is currently in.
        /// </summary>
        public int CurrentState { get; private set; }

        /// <inheritdoc/>
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<TransitionableOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.CurrentState = data.CurrentStateEnum;
        }
    }
}
