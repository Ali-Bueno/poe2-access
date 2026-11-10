// <copyright file="Render.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.Components
{
    using System;
    using GameOffsets.Natives;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     The <see cref="Render" /> component in the entity.
    /// </summary>
    public class Render : ComponentBase
    {
        // Computed on each read: the terrain search sets the cells per tile from what the terrain holds (AreaLayoutSearch).
        private static float WorldToGridRatio => TileStructure.TileToWorldConversion / TileStructure.TileToGridConversion;

        /// <summary>Both grid coordinates in one word (X low, Y high): written and read whole, with no object per update.</summary>
        private long gridSnap;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Render" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Render" /> component.</param>
        public Render(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets the position where entity is located on the grid (map).
        ///     Returns a per-call snapshot — atomic with respect to UpdateData.
        ///     Z is always 0 (the underlying field is 2D).
        /// </summary>
        public StdTuple3D<float> GridPosition
        {
            get
            {
                var s = System.Threading.Volatile.Read(ref this.gridSnap);
                return new StdTuple3D<float> { X = BitConverter.Int32BitsToSingle((int)s), Y = BitConverter.Int32BitsToSingle((int)(s >> 32)), Z = 0f };
            }
        }

        /// <summary>
        ///     Gets the position where entity is located on the grid (map).
        /// </summary>
        public StdTuple3D<float> ModelBounds { get; private set; }

        /// <summary>
        ///     Gets the position where entity is rendered in the game world.
        ///     NOTE: Z-Axis is pointing to the (visible/invisible) healthbar.
        /// </summary>
        public StdTuple3D<float> WorldPosition { get; private set; }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<RenderOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.WorldPosition = data.CurrentWorldPosition;
            this.ModelBounds = data.CharactorModelBounds;

            var newX = data.CurrentWorldPosition.X / WorldToGridRatio;
            var newY = data.CurrentWorldPosition.Y / WorldToGridRatio;
            System.Threading.Volatile.Write(
                ref this.gridSnap,
                ((long)BitConverter.SingleToInt32Bits(newY) << 32) | (uint)BitConverter.SingleToInt32Bits(newX));
        }
    }
}