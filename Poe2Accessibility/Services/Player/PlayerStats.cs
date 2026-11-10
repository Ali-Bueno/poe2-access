namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>The numbers the game keeps for a character (its Stats component), by stat name.</summary>
    public static class PlayerStats
    {
        private const int NotListed = -1;

        /// <summary>A stat's value, 0 when the game holds none; null when the stat's key or the component cannot be read.</summary>
        public static int? Of(Entity player, string name)
        {
            var key = GameIds.Current.Stat(name, NotListed);
            return key != NotListed && player.TryGetComponent<Stats>(out var stats) ? Value(stats, (GameStats)key) : null;
        }

        /// <summary>Stat that a buff or action changed, else one items changed, else 0.</summary>
        public static int Value(Stats stats, GameStats key) =>
            stats.StatsChangedByBuffAndActions.TryGetValue(key, out var value) ? value : stats.StatsChangedByItems.GetValueOrDefault(key);
    }
}
