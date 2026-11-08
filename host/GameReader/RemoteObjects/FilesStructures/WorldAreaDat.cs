// <copyright file="WorldAreaDat.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.FilesStructures
{
    using System;
    using GameOffsets.Objects.FilesStructures;
    using States;

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
        ///     Gets the Area name.
        ///     The value is same as in <see cref="AreaLoadingState.CurrentAreaName" />.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the area Act number.
        /// </summary>
        public int Act { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the area is town or not.
        /// </summary>
        public bool IsTown { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether area is hideout or not.
        /// </summary>
        public bool IsHideout { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether player is in Battle Royale or not.
        /// </summary>
        public bool IsBattleRoyale { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether area has a waypoint or not.
        /// </summary>
        public bool HasWaypoint { get; private set; }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.Id = string.Empty;
            this.Name = string.Empty;
            this.Act = 0x00;
            this.IsTown = false;
            this.IsHideout = false;
            this.IsBattleRoyale = false;
            this.HasWaypoint = false;
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<WorldAreaDatOffsets>(this.Address);
            this.Id = reader.ReadUnicodeString(data.IdPtr);
            this.Name = reader.ReadUnicodeString(data.NamePtr);
            this.IsTown = data.IsTown || this.Id == "HeistHub" || this.Id == "KalguuranSettlersLeague";
            this.HasWaypoint = data.HasWaypoint || this.Id == "HeistHub";
            this.IsHideout = this.Id.ToLower().Contains("hideout") && !this.Id.ToLower().Contains("map");
            this.IsBattleRoyale = this.Id.ToLower().Contains("exileroyale");
        }
    }
}