namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Keeps <see cref="EssencePrison"/> told of the monsters the area's Essence crystals hold, so they stay silent until freed.
    ///     It runs whatever the league announcements' setting: this is which enemies are in the fight, not news.
    /// </summary>
    public sealed class EssenceWatch
    {
        /// <summary>A freed monster must ring at once: looked at more often than the league news.</summary>
        private const int PollMs = 200;

        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private readonly List<Vector2> whole = new();
        private readonly HashSet<uint> freed = new();
        private long lastPoll;

        public EssenceWatch(HostMemoryReader memory, PluginLog log)
        {
            this.memory = memory;
            this.log = log;
        }

        /// <summary>A new area: nothing is held.</summary>
        public void Reset()
        {
            EssencePrison.Publish(new HashSet<uint>());
            this.logged.Clear();
            this.freed.Clear();
        }

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            this.whole.Clear();
            var entities = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values;
            foreach (var entity in entities)
            {
                var path = entity.Path ?? string.Empty;
                if (!entity.IsValid || !IsCrystal(entity) || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var transition = TransitionState.Of(this.memory, entity);
                bool? inPlay = entity.TryGetComponent<Targetable>(out _) ? EntityCatalog.IsInPlay(this.memory, entity) : null;
                var holds = EssencePrison.IsWhole(transition, inPlay);
                // How the rule is confirmed from a session's log: each crystal's transition as it first reads and as it changes.
                this.LogOnce($"Essence crystal: {path}: transition {(transition is { } read ? $"{read.State} of {read.Count}" : "unread")}, " +
                             $"in play {inPlay?.ToString() ?? "no target"}, holds {holds}");
                if (holds)
                {
                    this.whole.Add(new Vector2(render.WorldPosition.X, render.WorldPosition.Y));
                }
            }

            if (this.whole.Count == 0)
            {
                if (EssencePrison.Any)
                {
                    EssencePrison.Publish(new HashSet<uint>());
                }

                return;
            }

            var monsters = new List<(uint Id, Vector2 Place, bool Pack)>();
            foreach (var entity in entities)
            {
                // Only an enemy: the player's own skill objects are monsters too (a monk's spectral bell, dropped by a crystal, 2026-11-26).
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || this.freed.Contains(entity.Id) || !EntityCatalog.IsAliveEnemy(entity) ||
                    !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                // Freed once, free for good: the game's own mark of it outlives any doubt about the crystal's state.
                var hasMarks = entity.TryGetComponent<Buffs>(out var marks);
                if (hasMarks && IsFreed(marks!))
                {
                    this.freed.Add(entity.Id);
                    continue;
                }

                monsters.Add((entity.Id, new Vector2(render.WorldPosition.X, render.WorldPosition.Y), hasMarks && Has(marks!, EssencePrison.PackBuff)));
            }

            var held = EssencePrison.Held(this.whole, monsters);
            foreach (var entity in entities)
            {
                if (held.Contains(entity.Id))
                {
                    // The game's own marks of a held monster, for a rule that needs no place: its buffs and the stat of Stats row 4785.
                    var buffs = entity.TryGetComponent<Buffs>(out var effects) ? string.Join(", ", effects.StatusEffects.Keys) : "-";
                    this.LogOnce($"Essence holds: {entity.Path}: buffs [{buffs}], monster_inside_monolith {PlayerStats.Of(entity, HeldStat)?.ToString() ?? "unread"}");
                }
            }

            EssencePrison.Publish(held);
        }

        /// <summary>The stat the game's tables name for a monster in a crystal (Stats 4785); logged, not yet relied on.</summary>
        private const string HeldStat = "monster_inside_monolith";

        /// <summary>Whether an awake entity is an Essence's crystal, by <see cref="EssencePrison.IsCrystal(string, bool, bool)"/>.</summary>
        public static bool IsCrystal(Entity entity) =>
            EssencePrison.IsCrystal(entity.Path ?? string.Empty, EntityComponents.AddressOf(entity, GameLayout.MonolithComponent.Name) != null,
                entity.TryGetComponent<MinimapIcon>(out var icon) && icon.IconName == MinimapMarks.EssenceIcon);

        private static bool IsFreed(Buffs buffs) => Has(buffs, EssencePrison.FreedBuff);

        private static bool Has(Buffs buffs, string id)
        {
            foreach (var key in buffs.StatusEffects.Keys)
            {
                if (key == id || BuffIds.Base(key) == id)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogOnce(string line)
        {
            if (this.logged.Add(line))
            {
                this.log.Info(line);
            }
        }
    }
}
