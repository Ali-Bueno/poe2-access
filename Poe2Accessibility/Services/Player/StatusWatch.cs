namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameOffsets.Objects.States.InGameState;
    using GameReader;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Harmful states on the player (D2R status-effects.md): a state that begins is spoken at once and cannot be cut
    ///     off for a moment; its end is said after it has been gone a while (<see cref="StatusAilments.EndHoldMs"/>), so a
    ///     chill renewed by every hit stays one chill. Entering an area takes the states already there silently. States come from the player's
    ///     Buffs component (buff definition ids such as "chilled", "poisoned"); ids are matched by stem so PoE2's
    ///     variants share a name. Every new buff id is logged once with who applied it, to learn PoE2's ids from real
    ///     fights. A harmful buff named by the game is said only when no friend applied it (<see cref="SourceOf"/>).
    /// </summary>
    public sealed class StatusWatch
    {
        private const int PollMs = 150;

        private readonly Func<Entity?> subject;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly Dictionary<StatusAilment, long> active = new();
        private readonly HashSet<string> seenIds = new(StringComparer.Ordinal);
        private bool baseline = true;
        private long lastPoll;

        public StatusWatch(Func<Entity?> subject, IAccessibilityOutputService output, PluginLog log)
        {
            this.subject = subject;
            this.output = output;
            this.log = log;
        }

        /// <summary>States active now, for the vitals report ("Poisoned, chilled").</summary>
        public IReadOnlyCollection<StatusAilment> Active => this.active.Keys;

        /// <summary>The game's other harmful buffs, spoken by their own name; null until the catalog is loaded.</summary>
        public HarmfulBuffs? Harmful { get; set; }

        /// <summary>Buffs something else already tells the player (a hazard's alarm), so they are not named too.</summary>
        public Func<string, bool> IsCovered { get; set; } = _ => false;

        /// <summary>A new area or character: what is there now is not news.</summary>
        public void Reset()
        {
            this.active.Clear();
            this.Harmful?.Reset();
            this.baseline = true;
        }

        /// <summary>Per frame; <paramref name="reading"/> false while dead, paused or in a menu (nothing is said).</summary>
        public void Tick(bool reading)
        {
            var now = Environment.TickCount64;
            if (!reading || now - this.lastPoll < PollMs ||
                this.subject() is not { } entity || !entity.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            this.lastPoll = now;
            var present = new HashSet<StatusAilment>();
            var language = Localization.Language;
            var fromFoes = new List<string>();
            foreach (var (id, effect) in buffs.StatusEffects)
            {
                var seen = this.seenIds.Add(id);
                var named = this.Harmful?.NameOf(id, language).Length > 0;
                if (seen || named)
                {
                    var source = SourceOf(effect.SourceEntityId, entity);
                    if (seen)
                    {
                        this.log.Info($"Player buff seen: {id} from {source.Description}");
                    }

                    if (named && !source.Friendly)
                    {
                        fromFoes.Add(id);
                    }
                }

                if (StatusAilments.FromBuffId(id) is { } ailment)
                {
                    present.Add(ailment);
                }
            }

            if (this.Harmful is { } harmful)
            {
                foreach (var name in harmful.Begun(fromFoes, now, language, id => StatusAilments.FromBuffId(id) != null || this.IsCovered(id)))
                {
                    this.output.Say(SpeechKind.StatusBegins, name);
                }
            }

            foreach (var ailment in present)
            {
                if (!this.active.ContainsKey(ailment) && !this.baseline)
                {
                    this.output.Say(SpeechKind.StatusBegins, Announcements.AilmentBegins(ailment));
                }

                this.active[ailment] = now;
            }

            foreach (var (ailment, lastSeen) in this.active.ToList())
            {
                if (!present.Contains(ailment) && now - lastSeen >= StatusAilments.EndHoldMs(ailment))
                {
                    this.active.Remove(ailment);
                    this.output.Say(SpeechKind.StatusEnds, Announcements.AilmentEnds(ailment));
                }
            }

            this.baseline = false;
        }

        /// <summary>
        ///     Who applied a buff (the buff's source entity id, the one GameHelper matches flasks by): the player, another
        ///     player or a friendly creature is a friend. The ground of the player's own Whirling Slash puts the enemies'
        ///     debuff spear_sandstorm ("Remolino") on the player as well, and it was named at every swing (user 2026-11-09).
        /// </summary>
        private static (bool Friendly, string Description) SourceOf(uint id, Entity player)
        {
            if (id == 0)
            {
                return (false, "no source");
            }

            if (id == player.Id)
            {
                return (true, "the player");
            }

            if (!Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.TryGetValue(new EntityNodeKey { id = id }, out var from))
            {
                return (false, $"entity {id}, not in the area");
            }

            var friendly = from.EntityType == EntityTypes.Player || (from.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly);
            return (friendly, $"{(friendly ? "friendly" : "hostile")} {from.Path}");
        }
    }
}
