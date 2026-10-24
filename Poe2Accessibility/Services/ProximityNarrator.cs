namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Names what the player walks up to or stands on (checkpoints, NPCs, the stash, chests, items on the ground,
    ///     exits), once per approach, after what is being said; and what A uses, with the button, whenever the game's
    ///     target moves to something else (Finn beside the stash took A, 2026-10-24). Design: docs/proximity.md.
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

        /// <summary>A name waits this long, so the game's target can settle on it and the name carry the button once.</summary>
        private const int NameHoldMs = 350;

        /// <summary>The game's target is read this often and must hold this long before it is named (walking sweeps it).</summary>
        private const int TargetPollMs = 100;
        private const int TargetSettleMs = 200;

        /// <summary>No target this long: the next one is news even if it is the last one named.</summary>
        private const int TargetForgetMs = 3000;

        private static readonly ScanCategory[] Announced =
        {
            ScanCategory.Exits, ScanCategory.Waypoints, ScanCategory.Npcs, ScanCategory.Objects, ScanCategory.Chests, ScanCategory.Items,
        };

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly ProximityTracker tracker = new(ReachWorld, RearmWorld, RepeatCooldownMs);
        private readonly Queue<(ScanEntry Entry, long At)> pending = new();
        private long lastPoll = long.MinValue / 2;
        private long lastSpoken = long.MinValue / 2;
        private long lastTargetPoll = long.MinValue / 2;
        private uint? candidate;
        private long candidateSince;
        private uint? target;
        private uint? targetSpoken;

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
            this.candidate = null;
            this.target = null;
            this.targetSpoken = null;
        }

        /// <summary>
        ///     Per frame; paused while a panel or dialogue is open, so closing one does not repeat what is near.
        ///     <paramref name="nearby"/> names what is walked up to, <paramref name="interact"/> what A uses.
        /// </summary>
        public void Tick(bool active, bool nearby, bool interact)
        {
            if (!active)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (interact && now - this.lastTargetPoll >= TargetPollMs)
            {
                this.lastTargetPoll = now;
                this.TrackTarget(now);
            }

            if (nearby && now - this.lastPoll >= PollMs &&
                Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Render>(out var player))
            {
                this.lastPoll = now;
                // Gold is said once, as it is picked up ("Recogido 2 de oro", GoldWatch); its sound leads the way to it.
                var near = this.catalog.Entries().Where(entry => EntityCatalog.IsEntity(entry) && Announced.Contains(entry.Category) && entry.Kind != "gold");
                foreach (var entry in this.tracker.Update(player.WorldPosition.X, player.WorldPosition.Y, near, now))
                {
                    this.pending.Enqueue((entry, now));
                }
            }

            if (this.pending.Count > 0 && now - this.pending.Peek().At >= NameHoldMs && now - this.lastSpoken >= UtteranceGapMs)
            {
                var names = new List<string>();
                while (names.Count < NamesPerUtterance && this.pending.Count > 0)
                {
                    // Already named with the button: not again.
                    var entry = this.pending.Dequeue().Entry;
                    if (entry.Id != this.targetSpoken)
                    {
                        names.Add(this.Label(entry));
                    }
                }

                if (names.Count > 0)
                {
                    this.output.Say(SpeechKind.Places, Announcements.Nearby(names));
                    this.lastSpoken = now;
                }
            }
        }

        /// <summary>Follows the game's target; a new one that holds is named with the button, unless a queued name will carry it.</summary>
        private void TrackTarget(long now)
        {
            var current = this.catalog.InteractTarget();
            if (current?.Id != this.candidate)
            {
                this.candidate = current?.Id;
                this.candidateSince = now;
                return;
            }

            if (current == null)
            {
                this.target = null;
                if (now - this.candidateSince >= TargetForgetMs)
                {
                    this.targetSpoken = null;
                }

                return;
            }

            if (now - this.candidateSince < TargetSettleMs)
            {
                return;
            }

            this.target = current.Id;
            if (current.Id == this.targetSpoken || this.pending.Any(item => item.Entry.Id == current.Id))
            {
                return;
            }

            this.output.Say(SpeechKind.Places, this.Label(current));
        }

        /// <summary>A name, with the interact button when A uses it now.</summary>
        private string Label(ScanEntry entry)
        {
            if (entry.Id != this.target)
            {
                return entry.Name;
            }

            this.targetSpoken = entry.Id;
            return Announcements.WithButton(entry.Name, ControllerGlyphs.Interact);
        }
    }
}
