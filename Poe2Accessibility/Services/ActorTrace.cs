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
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Research (dev command "trace [seconds]"): every frame, for each monster near the player that is acting (not
    ///     idle or moving), the Actor bytes that changed, its animation and its skills' use stages, with the player's
    ///     life — to find where an attack's progress and its moment of impact live, for the dodge warning.
    /// </summary>
    public sealed class ActorTrace
    {
        private const int DefaultSeconds = 25;

        private const int MaxSeconds = 180;

        /// <summary>Actor bytes watched: around the animation id (+0x8B0) up to the skills vector (+0xB08).</summary>
        private const int WindowStart = 0x780;

        private const int WindowBytes = 0x400;

        private const float WatchWorld = 1400f;

        private const int MaxSkills = 24;

        private const int ActionOffset = 0x8B8;

        private const int ActionBytes = 0x300;

        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly string directory;
        private readonly StringBuilder lines = new();
        private readonly Dictionary<uint, byte[]> previous = new();
        private readonly Dictionary<uint, int> animations = new();
        private int lastLife = -1;
        private long startedAt;
        private long endsAt;

        public ActorTrace(HostMemoryReader memory, PluginLog log, string directory)
        {
            this.memory = memory;
            this.log = log;
            this.directory = directory;
        }

        public void Start(int seconds = DefaultSeconds)
        {
            this.lines.Clear();
            this.previous.Clear();
            this.animations.Clear();
            this.lastLife = -1;
            this.startedAt = Environment.TickCount64;
            this.endsAt = this.startedAt + (Math.Clamp(seconds, 1, MaxSeconds) * 1000L);
            this.log.Info("Actor trace started");
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick()
        {
            if (this.endsAt == 0)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (now >= this.endsAt)
            {
                this.Finish();
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player) || !Seats.Followed.TryGetComponent<Life>(out var life))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var lifeNow = life.Health.Current;
            if (lifeNow != this.lastLife)
            {
                this.lines.AppendLine($"{now - this.startedAt,6} PLAYER life={lifeNow}");
                this.lastLife = lifeNow;
            }

            foreach (var monster in area.AwakeEntities.Values)
            {
                if (!monster.IsValid || monster.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(monster) ||
                    !monster.TryGetComponent<Actor>(out var actor) || !monster.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var gap = Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y));
                if (gap > WatchWorld)
                {
                    continue;
                }

                var animation = (int)actor.Animation;
                this.animations.TryGetValue(monster.Id, out var lastAnimation);
                this.animations[monster.Id] = animation;
                var calm = DodgeThreats.Of(animation, value => Enum.GetName(typeof(Animation), value)) == DodgeThreat.None;
                if (calm && animation == lastAnimation)
                {
                    this.previous.Remove(monster.Id);
                    continue;
                }

                // The action object the attack sets at +0x8B8 (its progress was not in the Actor itself, 2026-10-24).
                var action = this.memory.TryRead<IntPtr>(actor.Address + ActionOffset, out var pointer) && this.memory.IsValidAddress(pointer)
                    ? this.memory.ReadBytes(pointer, ActionBytes)
                    : new byte[ActionBytes];
                var bytes = this.memory.ReadBytes(actor.Address + WindowStart, WindowBytes).Concat(action).ToArray();
                var before = this.previous.TryGetValue(monster.Id, out var kept) ? kept : Array.Empty<byte>();
                var changes = new List<string>();
                for (var at = 0; at + 4 <= bytes.Length && before.Length == bytes.Length; at += 4)
                {
                    if (BitConverter.ToInt32(bytes, at) == BitConverter.ToInt32(before, at))
                    {
                        continue;
                    }

                    var value = BitConverter.ToInt32(bytes, at);
                    var single = BitConverter.ToSingle(bytes, at);
                    var where = at < WindowBytes ? $"+{WindowStart + at:X}" : $"A+{at - WindowBytes:X}";
                    changes.Add(float.IsFinite(single) && Math.Abs(single) is > 1e-4f and < 1e6f ? $"{where}={value}|{single:G5}" : $"{where}={value}");
                }

                this.previous[monster.Id] = bytes;
                var full = before.Length == 0 ? $" FULL {Convert.ToHexString(bytes)}" : string.Empty; // window, then the action object
                this.lines.AppendLine($"{now - this.startedAt,6} id={monster.Id} {monster.Path.Split('/').Last()} anim={animation} {actor.Animation} gap={gap:F0} " +
                                      $"skills[{this.Skills(actor.Address)}] {string.Join(' ', changes.Take(120))}{full}");
            }
        }

        /// <summary>Use stage (+0x08) and cast type (+0x0C) of each ActiveSkillDetails in Actor +0xB08.</summary>
        private string Skills(IntPtr actor)
        {
            if (!this.memory.TryRead<IntPtr>(actor + GameLayout.ActorComponent.SkillsOffset, out var first) ||
                !this.memory.TryRead<IntPtr>(actor + GameLayout.ActorComponent.SkillsOffset + IntPtr.Size, out var last) ||
                first == IntPtr.Zero || last.ToInt64() <= first.ToInt64())
            {
                return string.Empty;
            }

            var count = (int)Math.Min(MaxSkills, (last.ToInt64() - first.ToInt64()) / GameLayout.ActorComponent.SkillEntrySize);
            var parts = new List<string>();
            for (var i = 0; i < count; i++)
            {
                if (this.memory.TryRead<IntPtr>(first + (i * GameLayout.ActorComponent.SkillEntrySize), out var details) && details != IntPtr.Zero &&
                    this.memory.TryRead<int>(details + 0x08, out var stage) && this.memory.TryRead<int>(details + 0x0C, out var cast))
                {
                    parts.Add($"{stage}/{cast}");
                }
            }

            return string.Join(',', parts);
        }

        private void Finish()
        {
            this.endsAt = 0;
            Directory.CreateDirectory(this.directory);
            var path = Path.Join(this.directory, $"actor-trace-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, this.lines.ToString());
            this.log.Info($"Actor trace written to {path} ({this.lines.Length} chars)");
        }
    }
}
