namespace Poe2Accessibility.Services
{
    using System;
    using System.Runtime.InteropServices;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>The second local (couch co-op) player's entity; GameHelper's <c>AreaInstance.Player</c> is the first.</summary>
    public sealed class LocalPlayers
    {
        /// <summary>The list changes only when a player joins or leaves, so it is read again this often.</summary>
        private const long RefreshMs = 500;

        private static readonly int ListOffset =
            (int)Marshal.OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.PlayerInfo)) + GameLayout.LocalPlayers.ListOffset;

        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private Entity? second;
        private IntPtr area;
        private long lastRefresh = long.MinValue / 2;

        public LocalPlayers(HostMemoryReader memory, PluginLog log)
        {
            this.memory = memory;
            this.log = log;
        }

        /// <summary>The second local player, or null when playing alone.</summary>
        public Entity? Second()
        {
            var now = Environment.TickCount64;
            var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
            if (now - this.lastRefresh >= RefreshMs || areaInstance.Address != this.area)
            {
                this.lastRefresh = now;
                this.area = areaInstance.Address;
                var found = this.Find(areaInstance);
                if (found?.Id != this.second?.Id)
                {
                    this.log.Info(found == null
                        ? "Local co-op: no second player"
                        : $"Local co-op: second player {(found.TryGetComponent<Player>(out var player) ? player.Name : string.Empty)} (id {found.Id})");
                }

                this.second = found;
            }

            return this.second is { IsValid: true } entity ? entity : null;
        }

        private Entity? Find(AreaInstance areaInstance)
        {
            if (areaInstance.Address == IntPtr.Zero ||
                !this.memory.TryRead<StdVector>(areaInstance.Address + ListOffset, out var list) || !this.memory.IsValidAddress(list.First) ||
                LocalPlayerList.Second(this.memory.ReadStdVector<IntPtr>(list, GameLayout.LocalPlayers.MaxPlayers), areaInstance.Player.Address) is not { } address)
            {
                return null;
            }

            foreach (var entity in areaInstance.AwakeEntities.Values)
            {
                if (entity.Address == address && entity.IsValid)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
