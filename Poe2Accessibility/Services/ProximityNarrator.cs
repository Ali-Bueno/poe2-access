namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
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

        /// <summary>
        ///     The same thing is named again only after a minute (a fight around a checkpoint repeated it every 8 s); an item
        ///     whenever it is walked back to (user 2026-10-26: walking away and back said nothing).
        /// </summary>
        private const int RepeatCooldownMs = 60000;

        /// <summary>D2R ground items: up to four names per utterance, later groups 900 ms apart.</summary>
        private const int NamesPerUtterance = 4;
        private const int UtteranceGapMs = 900;

        /// <summary>A name waits this long, so the game's target can settle on it and the name carry the button once.</summary>
        private const int NameHoldMs = 350;

        /// <summary>The game's target is read this often and must hold this long before it is named (walking sweeps it).</summary>
        private const int TargetPollMs = 100;
        private const int TargetSettleMs = 200;

        /// <summary>Another player walking past takes the target for a moment ("CaçaDoStorm, Cazadora, A" in the camp).</summary>
        private const int PlayerSettleMs = 1000;

        /// <summary>No target this long: the next one is news even if it is the last one named.</summary>
        private const int TargetForgetMs = 3000;

        private static readonly ScanCategory[] Announced =
        {
            ScanCategory.Exits, ScanCategory.Waypoints, ScanCategory.Encounters, ScanCategory.Npcs, ScanCategory.Objects, ScanCategory.Chests,
            ScanCategory.Items,
        };

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly ProximityTracker tracker = new(ReachWorld, RearmWorld, RepeatCooldownMs, entry => entry.Category != ScanCategory.Items);
        private readonly Queue<(ScanEntry Entry, long At)> pending = new();
        private long lastPoll = long.MinValue / 2;
        private long lastSpoken = long.MinValue / 2;
        private long lastTargetPoll = long.MinValue / 2;
        private uint? candidate;
        private long candidateSince;
        private uint? target;
        private uint? targetSpoken;
        private Vector2 targetSpokenAt;

        /// <summary>
        ///     The one named with the button that the player then walked away from: named again once back within reach.
        ///     A lone item kept the game's target, so walking back over it said nothing; a second item took the target
        ///     and the first was named again (user 2026-10-26).
        /// </summary>
        private uint? targetLeft;

        /// <summary>
        ///     An exit's label shows "Hold A (Show Options)" after a press, drawn outside the UI tree (2026-10-24): said
        ///     once per area, the first time an exit takes the target.
        /// </summary>
        private bool exitOptionsSaid;

        public ProximityNarrator(EntityCatalog catalog, IAccessibilityOutputService output)
        {
            this.catalog = catalog;
            this.output = output;
        }

        /// <summary>Which items are named on approach (<see cref="LootFilter"/> level, settings).</summary>
        public string LootLevel { get; set; } = LootFilter.All;

        /// <summary>A new area: everything near is news again.</summary>
        public void Reset()
        {
            this.tracker.Reset();
            this.pending.Clear();
            this.candidate = null;
            this.target = null;
            this.targetSpoken = null;
            this.targetLeft = null;
            this.exitOptionsSaid = false;
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
                Seats.Followed.TryGetComponent<Render>(out var player))
            {
                this.lastPoll = now;
                // Gold is said once, as it is picked up ("Recogido 2 de oro", GoldWatch); its sound leads the way to it.
                var near = this.catalog.Entries().Where(entry => EntityCatalog.IsEntity(entry) && Announced.Contains(entry.Category) && entry.Kind != "gold" &&
                    (entry.Category != ScanCategory.Items || LootFilter.Allows(this.LootLevel, entry.Loot)));
                foreach (var entry in this.tracker.Update(player.WorldPosition.X, player.WorldPosition.Y, near, now))
                {
                    this.pending.Enqueue((entry, now));
                }
            }

            if (this.pending.Count > 0 && now - this.pending.Peek().At >= NameHoldMs && now - this.lastSpoken >= UtteranceGapMs)
            {
                var names = new List<string>();
                var targeted = string.Empty;
                for (var taken = 0; taken < NamesPerUtterance && this.pending.Count > 0; taken++)
                {
                    // Already named with the button: not again. The one A uses is its own sentence, so the button
                    // is not heard as part of the list ("Finn, Alijo, A", 2026-10-24).
                    var entry = this.pending.Dequeue().Entry;
                    if (entry.Id == this.targetSpoken)
                    {
                        continue;
                    }

                    if (entry.Id == this.target)
                    {
                        targeted = this.Label(entry);
                    }
                    else
                    {
                        names.Add(entry.Name);
                    }
                }

                if (names.Count > 0 || targeted.Length > 0)
                {
                    this.output.Say(SpeechKind.Places, Announcements.Sentences(new[] { Announcements.Nearby(names), targeted }.Where(part => part.Length > 0)));
                    this.lastSpoken = now;
                }
            }
        }

        /// <summary>Follows the game's target; a new one that holds is named at once with the button.</summary>
        private void TrackTarget(long now)
        {
            var current = this.catalog.InteractTarget();
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            if (current != null && current.Id == this.targetSpoken)
            {
                this.targetSpokenAt = new Vector2(current.WorldX, current.WorldY); // an NPC walks
            }

            if (this.targetSpoken is { } spoken && Vector2.Distance(here, this.targetSpokenAt) > RearmWorld)
            {
                this.targetLeft = spoken;
                this.targetSpoken = null;
            }

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

            if (now - this.candidateSince < (current.Category == ScanCategory.Players ? PlayerSettleMs : TargetSettleMs))
            {
                return;
            }

            this.target = current.Id;
            if (current.Id == this.targetSpoken)
            {
                return;
            }

            if (current.Id == this.targetLeft)
            {
                if (Vector2.Distance(here, new Vector2(current.WorldX, current.WorldY)) > ReachWorld)
                {
                    return; // still the game's target from afar: named when walked back to
                }

                this.targetLeft = null;
            }

            // What A takes now cuts in, like a focus: queued behind other lines it came late and A took the next item
            // ("Ballesta tensa, A" heard while A already held a bow, 2026-10-26). A queued name of it is skipped later.
            this.output.Speak(this.Label(current), true);
        }

        /// <summary>A name, with the interact button when A uses it now.</summary>
        private string Label(ScanEntry entry)
        {
            if (entry.Id != this.target)
            {
                return entry.Name;
            }

            this.targetSpoken = entry.Id;
            this.targetSpokenAt = new Vector2(entry.WorldX, entry.WorldY);
            if (entry.Id == this.targetLeft)
            {
                this.targetLeft = null;
            }

            var label = Announcements.WithButton(entry.Name, ControllerGlyphs.Interact);
            if (entry.Category != ScanCategory.Exits || this.exitOptionsSaid)
            {
                return label;
            }

            this.exitOptionsSaid = true;
            return $"{label}. {Announcements.HoldForOptions(ControllerGlyphs.Interact)}";
        }
    }
}
