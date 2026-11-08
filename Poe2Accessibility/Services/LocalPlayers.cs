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

        /// <summary>From GameHelper's local player field to the player list: both live in the area's PlayerInfo.</summary>
        private static readonly int ListAfterPlayer =
            GameLayout.LocalPlayers.ListOffset - (int)Marshal.OffsetOf<LocalPlayerStruct>(nameof(LocalPlayerStruct.LocalPlayerPtr));

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

        /// <summary>Dev command "layout": the list read where the area search puts it starts with GameHelper's player.</summary>
        public string SelfTest()
        {
            var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
            var entries = this.memory.TryRead<StdVector>(areaInstance.Address + areaInstance.Layout.Fields.LocalPlayer + ListAfterPlayer, out var list)
                ? this.memory.ReadStdVector<IntPtr>(list, GameLayout.LocalPlayers.MaxPlayers) : Array.Empty<IntPtr>();
            return entries.Length > 0 && entries[0] == areaInstance.Player.Address ? $"local player list ok ({entries.Length} players)"
                : $"local player list FAIL ({entries.Length} entries, the first not the player)";
        }

        private Entity? Find(AreaInstance areaInstance)
        {
            if (areaInstance.Address == IntPtr.Zero ||
                !this.memory.TryRead<StdVector>(areaInstance.Address + areaInstance.Layout.Fields.LocalPlayer + ListAfterPlayer, out var list) || !this.memory.IsValidAddress(list.First) ||
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
