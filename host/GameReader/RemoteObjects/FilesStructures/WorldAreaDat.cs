// <copyright file="WorldAreaDat.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.FilesStructures
{
    using System;
    using GameOffsets.Objects.FilesStructures;

    /// <summary>
    ///     Points to a row in WorldArea.dat file.
    /// </summary>
    public class WorldAreaDat : RemoteObjectBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="WorldAreaDat" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal WorldAreaDat(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets the Area Id string.
        /// </summary>
        public string Id { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets a value indicating whether the area is town or not.
        /// </summary>
        public bool IsTown { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether area is hideout or not.
        /// </summary>
        public bool IsHideout { get; private set; }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.Id = string.Empty;
            this.IsTown = false;
            this.IsHideout = false;
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<WorldAreaDatOffsets>(this.Address);
            this.Id = reader.ReadUnicodeString(data.IdPtr);
            this.IsTown = data.IsTown || this.Id == "HeistHub" || this.Id == "KalguuranSettlersLeague";
            this.IsHideout = this.Id.ToLower().Contains("hideout") && !this.Id.ToLower().Contains("map");
        }
    }
}