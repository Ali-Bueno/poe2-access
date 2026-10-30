namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Enemies coming into view: each monster the camera shows for the first time in this area is named once, a
    ///     pack gathered into one line by name with its count and the direction of its middle ("Ahogado, 3, derecha.
    ///     Lobo podrido, mágico, arriba"). Uniques are left to <see cref="DiscoveryNarrator"/> ("Jefe a la vista").
    /// </summary>
    public sealed class EnemyNarrator
    {
        private const int PollMs = 300;

        /// <summary>A pack walks onto the screen over a moment: what appears this soon after the first is said with it.</summary>
        private const int GatherMs = 500;

        /// <summary>Monsters in view on arrival wait until the area's name has been said (as discoveries do).</summary>
        private const int ArrivalMs = 2500;

        /// <summary>A crowd of kinds is said by its biggest packs only; the enemy sounds tell the rest.</summary>
        private const int MaxGroups = 4;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly HashSet<uint> named = new();
        private readonly List<ScanEntry> waiting = new();
        private long firstWaitingAt;
        private long areaStartedAt = Environment.TickCount64;
        private long lastPoll;

        public EnemyNarrator(EntityCatalog catalog, IAccessibilityOutputService output)
        {
            this.catalog = catalog;
            this.output = output;
        }

        /// <summary>Each pack's direction after its name (settings EnemyDirections; off, names only: fewer words in a boss fight).</summary>
        public bool Directions { get; set; } = true;

        /// <summary>A new area: nothing named yet.</summary>
        public void Reset()
        {
            this.named.Clear();
            this.waiting.Clear();
            this.areaStartedAt = Environment.TickCount64;
        }

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs ||
                !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            foreach (var entry in this.catalog.Entries())
            {
                if (entry.Category != ScanCategory.Enemies || entry.Name.EndsWith(Announcements.UniqueRarityWord, StringComparison.Ordinal) ||
                    !this.named.Add(entry.Id))
                {
                    continue;
                }

                if (this.waiting.Count == 0)
                {
                    this.firstWaitingAt = now;
                }

                this.waiting.Add(entry);
            }

            if (this.waiting.Count == 0 || now - this.firstWaitingAt < GatherMs || now - this.areaStartedAt < ArrivalMs)
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var groups = this.waiting
                .GroupBy(entry => entry.Name)
                .Select(group =>
                {
                    var middle = new Vector2(group.Average(entry => entry.WorldX), group.Average(entry => entry.WorldY));
                    return (Name: group.Key, Count: group.Count(), Direction: ScreenProjection.Octant(player.WorldPosition, middle - here));
                })
                .OrderByDescending(group => group.Count)
                .ToList();
            this.waiting.Clear();
            var rare = groups.Any(group => group.Name.EndsWith(Announcements.RareRarityWord, StringComparison.Ordinal));
            this.output.Say(SpeechKind.EnemyNames, Announcements.EnemiesAppeared(groups.Take(MaxGroups), this.Directions), rare);
        }
    }
}
