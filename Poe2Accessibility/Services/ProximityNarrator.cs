namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Names what the player walks up to or stands on (checkpoints, NPCs, the stash, chests, items on the ground,
    ///     exits), once per approach, after what is being said. Design: docs/proximity.md.
    /// </summary>
    public sealed class ProximityNarrator
    {
        private const int PollMs = 250;

        /// <summary>About half a second of walking; announced again after moving three steps away.</summary>
        private const float ReachWorld = 200f;
        private const float RearmWorld = 300f;

        /// <summary>The same thing is named again only after a minute (a fight around a checkpoint repeated it every 8 s).</summary>
        private const int RepeatCooldownMs = 60000;

        /// <summary>D2R ground items: up to four names per utterance, later groups 900 ms apart.</summary>
        private const int NamesPerUtterance = 4;
        private const int UtteranceGapMs = 900;

        private static readonly ScanCategory[] Announced =
        {
            ScanCategory.Exits, ScanCategory.Waypoints, ScanCategory.Npcs, ScanCategory.Objects, ScanCategory.Chests, ScanCategory.Items,
        };

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly ProximityTracker tracker = new(ReachWorld, RearmWorld, RepeatCooldownMs);
        private readonly Queue<string> pending = new();
        private long lastPoll = long.MinValue / 2;
        private long lastSpoken = long.MinValue / 2;

        public ProximityNarrator(EntityCatalog catalog, IAccessibilityOutputService output)
        {
            this.catalog = catalog;
            this.output = output;
        }

        /// <summary>A new area: everything near is news again.</summary>
        public void Reset()
        {
            this.tracker.Reset();
            this.pending.Clear();
        }

        /// <summary>Per frame; paused while a panel or dialogue is open, so closing one does not repeat what is near.</summary>
        public void Tick(bool active)
        {
            if (!active)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (now - this.lastPoll >= PollMs &&
                Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Render>(out var player))
            {
                this.lastPoll = now;
                var near = this.catalog.Entries().Where(entry => EntityCatalog.IsEntity(entry) && Announced.Contains(entry.Category));
                foreach (var entry in this.tracker.Update(player.WorldPosition.X, player.WorldPosition.Y, near, now))
                {
                    this.pending.Enqueue(entry.Name);
                }
            }

            if (this.pending.Count > 0 && now - this.lastSpoken >= UtteranceGapMs)
            {
                var names = new List<string>();
                while (names.Count < NamesPerUtterance && this.pending.Count > 0)
                {
                    names.Add(this.pending.Dequeue());
                }

                this.output.Speak(Announcements.Nearby(names), false);
                this.lastSpoken = now;
            }
        }
    }
}
