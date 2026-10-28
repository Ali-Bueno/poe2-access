namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>States an enemy can be put in that skills pay off on (see EnemyStateWatch).</summary>
    public enum EnemyState
    {
        /// <summary>Heavy stun buildup past <see cref="PrimedThreshold"/>: the next big hit (Boneshatter) stuns.</summary>
        PrimedForStun,
        HeavyStunned,
        Frozen,
        Electrocuted,
        Pinned,
        ArmourBroken,
        Dazed,
        Parried,
    }

    /// <summary>
    ///     An enemy's states from its buffs and animation (pure). Buff ids are the game's BuffDefinitions ids (read
    ///     2026-10-26), which GameHelper suffixes with "_" and a hex skill id when the buff has one ("shocked_79").
    /// </summary>
    public static class EnemyStates
    {
        private static readonly Dictionary<string, EnemyState> Buffs = new(StringComparer.Ordinal)
        {
            ["frozen"] = EnemyState.Frozen,
            ["electrocute_display_buff"] = EnemyState.Electrocuted,
            ["pinned"] = EnemyState.Pinned,
            ["broken_armour_debuff"] = EnemyState.ArmourBroken,
            ["dazed"] = EnemyState.Dazed,
            ["parried"] = EnemyState.Parried,
        };

        public static EnemyState? FromBuffId(string id)
        {
            return Buffs.TryGetValue(id, out var state) || Buffs.TryGetValue(BuffIds.Base(id), out state) ? state : null;
        }

        /// <summary>The state an animation shows (GameHelper's Animation names); the stun deaths are deaths.</summary>
        public static EnemyState? FromAnimation(string name) => name switch
        {
            "HeavyStun" or "HeavyStunBack" or "HeavyStunForward" => EnemyState.HeavyStunned,
            "ElectrocuteForward" or "ElectrocuteBackward" or "ElectrocuteLeft" or "ElectrocuteRight" => EnemyState.Electrocuted,
            _ => null,
        };
    }

    /// <summary>
    ///     Which enemies have just entered a state (pure). A state that drops out for a moment (a torn read, a heavy stun
    ///     turning into its recovery animation) is still the same state until it has been gone <see cref="EndMs"/>.
    /// </summary>
    public sealed class EnemyStateNews
    {
        public const int EndMs = 400;

        private readonly Dictionary<uint, Dictionary<EnemyState, long>> known = new();
        private readonly HashSet<uint> seen = new();

        /// <summary>Starts a poll: enemies not updated before <see cref="EndPoll"/> are forgotten.</summary>
        public void BeginPoll() => this.seen.Clear();

        /// <summary>The states this enemy has now that it did not have before.</summary>
        public List<EnemyState> Update(uint id, IEnumerable<EnemyState> states, long now)
        {
            this.seen.Add(id);
            if (!this.known.TryGetValue(id, out var lastSeen))
            {
                lastSeen = new Dictionary<EnemyState, long>();
                this.known[id] = lastSeen;
            }

            var entered = new List<EnemyState>();
            foreach (var state in states)
            {
                if ((!lastSeen.TryGetValue(state, out var at) || now - at >= EndMs) && !entered.Contains(state))
                {
                    entered.Add(state);
                }

                lastSeen[state] = now;
            }

            return entered;
        }

        public void EndPoll()
        {
            foreach (var id in this.known.Keys.Where(id => !this.seen.Contains(id)).ToList())
            {
                this.known.Remove(id);
            }
        }

        public void Clear() => this.known.Clear();
    }
}
