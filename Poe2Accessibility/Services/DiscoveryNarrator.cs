namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Discoveries (D2R exploration.md): the first time the game shows an exit, a waypoint or checkpoint, or a
    ///     unique monster in this area, it is named after what is being said ("Nueva salida: Sierraclara") with a
    ///     short sound. What surrounds the player when the area loads is not news (the exit just taken), nor anything
    ///     known when the host starts inside an area; finds in the first seconds wait until the area name has been said.
    /// </summary>
    public sealed class DiscoveryNarrator
    {
        private const int PollMs = 500;

        /// <summary>D2R: finds in the first 2.5 s of a level wait until after the area banner.</summary>
        private const int ArrivalMs = 2500;

        /// <summary>Within this distance at arrival: the way back, or where the player stands, not a discovery.</summary>
        private const float ArrivalNearWorld = 800f;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly AudioCueService audio;
        private readonly float[]? sound;
        private readonly HashSet<uint> seen = new();
        private readonly List<ScanEntry> waiting = new();
        private long areaStartedAt = Environment.TickCount64;
        private bool resumed;
        private long lastPoll;

        public DiscoveryNarrator(EntityCatalog catalog, IAccessibilityOutputService output, AudioCueService audio, GameSounds sounds)
        {
            this.catalog = catalog;
            this.output = output;
            this.audio = audio;
            this.sound = sounds.Get("waypoint");
        }

        public float Gain { get; set; } = 1f;

        /// <summary>A new area: nothing discovered yet; resumed when the host started inside it after the player arrived.</summary>
        public void Reset(bool resumed)
        {
            this.seen.Clear();
            this.waiting.Clear();
            this.areaStartedAt = Environment.TickCount64;
            this.resumed = resumed;
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs ||
                !Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var arriving = now - this.areaStartedAt < ArrivalMs;
            foreach (var entry in this.catalog.Entries())
            {
                if (!EntityCatalog.IsEntity(entry) || !IsDiscovery(entry) || !this.seen.Add(entry.Id))
                {
                    continue;
                }

                if (arriving && (this.resumed || Vector2.Distance(here, new Vector2(entry.WorldX, entry.WorldY)) <= ArrivalNearWorld))
                {
                    continue; // the exit just taken, the checkpoint the player stands on; all of it on a host restart
                }

                this.waiting.Add(entry);
            }

            if (arriving || this.waiting.Count == 0)
            {
                return;
            }

            this.output.Say(SpeechKind.Places, string.Join(". ", this.waiting.Select(Announcements.Discovered)));
            if (this.sound != null)
            {
                this.audio.Play(this.sound, 0f, 1f, this.Gain);
            }

            this.waiting.Clear();
        }

        private static bool IsDiscovery(ScanEntry entry) =>
            entry.Category is ScanCategory.Exits or ScanCategory.Waypoints ||
            (entry.Category == ScanCategory.Enemies && entry.Name.EndsWith(Announcements.UniqueRarityWord, StringComparison.Ordinal));
    }
}
