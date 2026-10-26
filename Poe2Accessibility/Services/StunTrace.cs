namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Research (dev command "stuntrace [seconds]"): every 100 ms, for each enemy being fought near the player, its life,
    ///     animation, buffs, the Actor slots from 0x4C0 (the heavy stun buildup entries, <see cref="GameLayout.ActorComponent"/>,
    ///     and what follows them) and its stats (raw Stats.dat keys, row + 1; only changes after the first sight) — found the
    ///     heavy stun buildup and its threshold; still to find: the Freeze, Electrocution and Pin buildups (the second and
    ///     third entries hold the same threshold and stayed 0 against a Warrior).
    /// </summary>
    public sealed class StunTrace
    {
        private const int DefaultSeconds = 90;
        private const int MaxSeconds = 600;
        private const int SampleMs = 100;
        private const float WatchWorld = 900f;

        /// <summary>Actor bytes logged: 0x4C0..0x5C0, the buildup entries and what follows them.</summary>
        private const int ActorStart = 0x4C0;
        private const int ActorBytes = 0x100;

        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly string directory;
        private readonly StringBuilder lines = new();
        private readonly Dictionary<uint, string> lastState = new();
        private readonly Dictionary<uint, Dictionary<int, int>> lastStats = new();
        private long startedAt;
        private long endsAt;
        private long lastSample;

        public StunTrace(HostMemoryReader memory, PluginLog log, string directory)
        {
            this.memory = memory;
            this.log = log;
            this.directory = directory;
        }

        public void Start(int seconds = DefaultSeconds)
        {
            this.lines.Clear();
            this.lastState.Clear();
            this.lastStats.Clear();
            this.startedAt = Environment.TickCount64;
            this.endsAt = this.startedAt + (Math.Clamp(seconds, 1, MaxSeconds) * 1000L);
            this.log.Info($"Stun trace started ({seconds} s)");
        }

        /// <summary>Per frame in game.</summary>
        public void Tick()
        {
            var now = Environment.TickCount64;
            if (this.endsAt == 0 || now - this.lastSample < SampleMs)
            {
                return;
            }

            if (now >= this.endsAt)
            {
                this.Finish();
                return;
            }

            this.lastSample = now;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            foreach (var monster in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!monster.IsValid || monster.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(monster) ||
                    !monster.TryGetComponent<Render>(out var render) || !monster.TryGetComponent<Life>(out var life) ||
                    !monster.TryGetComponent<Actor>(out var actor) ||
                    Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) > WatchWorld)
                {
                    continue;
                }

                var actorBytes = this.memory.ReadBytes(actor.Address + ActorStart, ActorBytes);
                var slots = actorBytes.Length == ActorBytes
                    ? string.Join(',', Enumerable.Range(0, ActorBytes / 4).Select(i => BitConverter.ToInt32(actorBytes, i * 4)))
                    : "-";
                if (life.Health.Current >= life.Health.Total && !this.lastState.ContainsKey(monster.Id))
                {
                    continue; // not fought yet
                }

                var animation = Enum.GetName(actor.Animation) ?? $"{(int)actor.Animation}";
                var effects = monster.TryGetComponent<Buffs>(out var buffs)
                    ? string.Join(",", buffs.StatusEffects.Keys.Order(StringComparer.Ordinal))
                    : string.Empty;
                var state = $"life={life.Health.Current}/{life.Health.Total} anim={animation} A[{slots}] B[{effects}]";
                var stats = monster.TryGetComponent<Stats>(out var statsComponent) ? Collect(statsComponent) : new Dictionary<int, int>();
                var statChanges = this.lastStats.TryGetValue(monster.Id, out var before)
                    ? string.Join(',', stats.Where(pair => !before.TryGetValue(pair.Key, out var was) || was != pair.Value).Select(pair => $"{pair.Key}={pair.Value}")
                        .Concat(before.Keys.Where(key => !stats.ContainsKey(key)).Select(key => $"{key}=gone")))
                    : string.Join(',', stats.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
                if (this.lastState.TryGetValue(monster.Id, out var lastLine) && lastLine == state && statChanges.Length == 0)
                {
                    continue;
                }

                var rarity = monster.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal;
                this.lines.AppendLine($"{now - this.startedAt,6} id={monster.Id} {monster.Path.Split('/').Last()} {rarity} {state} S[{statChanges}]");
                this.lastState[monster.Id] = state;
                this.lastStats[monster.Id] = stats;
            }
        }

        /// <summary>Both of a component's stat lists, by raw key (the buff and action list wins).</summary>
        private static Dictionary<int, int> Collect(Stats stats)
        {
            var all = new Dictionary<int, int>();
            foreach (var list in new[] { stats.StatsChangedByItems, stats.StatsChangedByBuffAndActions })
            {
                lock (list)
                {
                    foreach (var (key, value) in list)
                    {
                        all[(int)key] = value;
                    }
                }
            }

            return all;
        }

        private void Finish()
        {
            this.endsAt = 0;
            Directory.CreateDirectory(this.directory);
            var path = Path.Join(this.directory, $"stun-trace-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, this.lines.ToString());
            this.log.Info($"Stun trace written to {path} ({this.lines.Length} chars)");
        }
    }
}
