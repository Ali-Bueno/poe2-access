// <copyright file="Player.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     The <see cref="Chest" /> component in the entity.
    /// </summary>
    public class Player : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Player" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Chest" /> component.</param>
        public Player(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets the name of the player.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the Xp of the player.
        /// </summary>
        public int Xp { get; private set; }

        /// <summary>
        ///     Gets the Level of the player.
        /// </summary>
        public int Level { get; private set; }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<PlayerOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;

            if (hasAddressChanged)
            {
                this.Name = reader.ReadStdWString(data.Name);
            }

            this.Xp = data.Xp;
            this.Level = data.Level;
        }
    }
}