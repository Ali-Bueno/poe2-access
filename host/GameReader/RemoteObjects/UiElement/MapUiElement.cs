// <copyright file="MiniMapUiElement.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.UiElement
{
    using System;
    using GameReader.Cache;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     Points to the Map UiElement.
    /// </summary>
    public class MapUiElement : UiElementBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MapUiElement" /> class.
        /// </summary>
        /// <param name="address">address to the Map Ui Element of the game.</param>
        /// <param name="parents">parent cache to use for this Ui Element.</param>
        internal MapUiElement(IntPtr address, UiElementParents parents)
            : base(address, parents) { }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var data = Core.Process.Handle.ReadMemory<MapUiElementOffset>(this.Address);
            this.UpdateData(data.UiElementBase, hasAddressChanged);
        }
    }
}
