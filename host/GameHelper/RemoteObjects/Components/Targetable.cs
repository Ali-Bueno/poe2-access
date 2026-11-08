// <copyright file="Targetable.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     The <see cref="Targetable" /> component in the entity.
    /// </summary>
    public class Targetable : ComponentBase
    {
        private TargetableOffsets cache;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Targetable" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Targetable" /> component.</param>
        public Targetable(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets the value indicating whether the entity is targetable or not.
        /// </summary>
        public bool IsTargetable { get; private set; } = false;

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<TargetableOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.cache = data;
            this.IsTargetable = data.IsTargetable && !data.HiddenfromPlayer &&
                data.NeedsTrue && data.MeetsQuestState && !data.NeedsFalse;
        }
    }
}