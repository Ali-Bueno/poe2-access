namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Harmful states on the player (D2R status-effects.md): a state that begins is spoken at once and cannot be cut
    ///     off for a moment; its end is said after it has been gone for half a second, so a chill renewed by every hit
    ///     stays one chill. Entering an area takes the states already there silently. States come from the player's
    ///     Buffs component (buff definition ids such as "chilled", "poisoned"); ids are matched by stem so PoE2's
    ///     variants share a name. Every new buff id is logged once, to learn PoE2's ids from real fights.
    /// </summary>
    public sealed class StatusWatch
    {
        private const int PollMs = 150;

        /// <summary>D2R EndHoldMs: an end is spoken once the state has been gone this long.</summary>
        private const int EndHoldMs = 500;

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

        /// <summary>A new area or character: what is there now is not news.</summary>
        public void Reset()
        {
            this.active.Clear();
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
            foreach (var id in buffs.StatusEffects.Keys)
            {
                if (this.seenIds.Add(id))
                {
                    this.log.Info($"Player buff seen: {id}");
                }

                if (StatusAilments.FromBuffId(id) is { } ailment)
                {
                    present.Add(ailment);
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
                if (!present.Contains(ailment) && now - lastSeen >= EndHoldMs)
                {
                    this.active.Remove(ailment);
                    this.output.Say(SpeechKind.StatusEnds, Announcements.AilmentEnds(ailment));
                }
            }

            this.baseline = false;
        }
    }
}
