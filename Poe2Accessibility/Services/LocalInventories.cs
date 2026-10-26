namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     Each local player's server inventories by name. ServerData +0x48 holds one player data per local player, each
    ///     with its inventory list at +0x320; GameHelper reads only the first (<c>ServerData.PlayerInventories</c>,
    ///     internal), which stays player 1's source. Player 2's is the second entry: its main inventory is the one player
    ///     2's inventory panel grid points to (+0x6F8), Clearfell 2026-10-25 (docs/research/local-coop.md).
    /// </summary>
    public sealed class LocalInventories
    {
        /// <summary>Inventories move only when the game rebuilds them (area change), so the list is read again this often.</summary>
        private const long RefreshMs = 1000;

        /// <summary>117 inventories per player in 0.5.5e; more than this is not an inventory list.</summary>
        private const int MaxInventories = 512;

        private static readonly PropertyInfo? FirstPlayer =
            typeof(ServerData).GetProperty("PlayerInventories", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly HostMemoryReader reader;
        private readonly Dictionary<InventoryName, IntPtr> second = new();
        private IntPtr server;
        private long readAt = long.MinValue / 2;

        public LocalInventories(HostMemoryReader reader)
        {
            this.reader = reader;
        }

        /// <summary>The inventories of local player <paramref name="seat"/> (1 or 2), null when they cannot be read.</summary>
        public Dictionary<InventoryName, IntPtr>? Of(int seat)
        {
            var serverData = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject;
            if (seat != 2)
            {
                return FirstPlayer?.GetValue(serverData) as Dictionary<InventoryName, IntPtr>;
            }

            var now = Environment.TickCount64;
            if (serverData.Address != this.server || now - this.readAt >= RefreshMs)
            {
                this.server = serverData.Address;
                this.readAt = now;
                this.ReadSecond(serverData.Address);
            }

            return this.second.Count > 0 ? this.second : null;
        }

        private void ReadSecond(IntPtr serverData)
        {
            this.second.Clear();
            if (serverData == IntPtr.Zero || !this.reader.TryRead<ServerDataOffsets>(serverData, out var data))
            {
                return;
            }

            var players = this.reader.ReadStdVector<IntPtr>(data.PlayerServerDataPtr, GameLayout.LocalPlayers.MaxPlayers);
            if (players.Length < 2 || !this.reader.IsValidAddress(players[1]) ||
                !this.reader.TryRead<ServerDataStructure>(players[1], out var player))
            {
                return;
            }

            foreach (var entry in this.reader.ReadStdVector<InventoryArrayStruct>(player.PlayerInventories, MaxInventories))
            {
                if (entry.InventoryPtr0 != IntPtr.Zero && this.reader.IsValidAddress(entry.InventoryPtr0))
                {
                    this.second[(InventoryName)entry.InventoryId] = entry.InventoryPtr0;
                }
            }
        }
    }
}
