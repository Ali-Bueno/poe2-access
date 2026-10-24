namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Barriers that open on their own (TriggerableBlockage, GameOffsets +0x30 IsBlocked): a sighted player sees
    ///     the red beam fade, a blind one only learns it from a route that suddenly goes on. Riverbank, 2026-10-24:
    ///     TutorialBlocker3 by the large chest opens once the gem from that chest is cast. Doors the player opens
    ///     (targetable) are not said: they asked for it.
    /// </summary>
    public sealed class BarrierWatch
    {
        private const int PollMs = 300;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly Dictionary<uint, bool> blocked = new();
        private long lastPoll;

        public BarrierWatch(EntityCatalog catalog, IAccessibilityOutputService output, PluginLog log)
        {
            this.catalog = catalog;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area: its barriers are learnt again without a word.</summary>
        public void Reset() => this.blocked.Clear();

        /// <summary>Per frame in game.</summary>
        public void Tick(bool enabled)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<TriggerableBlockage>(out var blockage) || entity.TryGetComponent<Targetable>(out _))
                {
                    continue;
                }

                var closed = blockage.IsBlocked;
                if (this.blocked.TryGetValue(entity.Id, out var was) && was && !closed && entity.TryGetComponent<Render>(out var render))
                {
                    this.log.Info($"Barrier opened: {entity.Path}");
                    if (enabled)
                    {
                        var at = render.WorldPosition;
                        var entry = new ScanEntry(entity.Id, ScanCategory.Objects, Announcements.BarrierOpened, at.X, at.Y, at.Z, "barrier");
                        this.output.Say(SpeechKind.Places, this.catalog.Describe(entry));
                    }
                }

                this.blocked[entity.Id] = closed;
            }
        }
    }
}
