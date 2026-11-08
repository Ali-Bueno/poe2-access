// <copyright file="Actor.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;
    using RemoteEnums;

    /// <summary>
    ///     The <see cref="Actor" /> component in the entity: what it is doing. The mod reads the skills, cooldowns and
    ///     deployed minions from the component's memory itself, so GameHelper's per-skill reads (one a frame per active
    ///     skill) were dropped.
    /// </summary>
    public class Actor : ComponentBase
    {
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

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var data = Core.Process.Handle.ReadMemory<ActorOffset>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Animation = (Animation)data.AnimationId;
        }
    }
}
