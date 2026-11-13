namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>One entity of a boss's arena that is a place of the fight this poll.</summary>
    /// <param name="Active">A refuge or clone: its state is set. A cover: it stands. A thing to use or break: it can be targeted.</param>
    /// <param name="Boss">Where the boss whose place it is stands (world).</param>
    public readonly record struct BossPlaceSight(uint Id, BossPlaceKind Kind, string Path, string Name, Vector2 At, float Radius, bool Active, Vector2 Boss);

    /// <summary>A place worth a line: what it is, its name when it has one, and where it is.</summary>
    public readonly record struct BossPlaceNews(BossPlaceKind Kind, string Name, Vector2 To)
    {
        /// <summary>A place to go to now (a shelter, the true clone, a cover); a thing to use or break is only told of.</summary>
        public bool Pressing => BossPlaces.Leads(this.Kind);
    }

    /// <summary>
    ///     The places of a boss fight (docs/research/boss-mechanics.md §14: SH, CL, CV, US, OB). The player is led to the
    ///     shelter while it is up and they are not in it, to the true clone, and behind a cover until they have stood there;
    ///     each is said once as it turns up, the most pressing kind first. A thing to use or break is told of once by its
    ///     name and never led to: the files do not say whether using or breaking it helps (Azarian's censers do not). Pure.
    /// </summary>
    public sealed class BossPlaces
    {
        /// <summary>The gap (world units) within which the player is at a place: the player's own reach (<see cref="BossWatch"/>), whatever the reach setting.</summary>
        public const float AtPlaceWorld = 40f;

        private readonly HashSet<uint> saidIds = new();
        private readonly HashSet<string> toldOf = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<uint> reached = new();

        /// <summary>Where the beat leads now (world) and how wide the place is; null when nowhere.</summary>
        public (Vector2 At, float Radius)? Lead { get; private set; }

        /// <summary>Whether the player is led to a place of this kind.</summary>
        public static bool Leads(BossPlaceKind kind) => kind is BossPlaceKind.Refuge or BossPlaceKind.Clone or BossPlaceKind.Cover;

        public void Reset()
        {
            this.saidIds.Clear();
            this.toldOf.Clear();
            this.reached.Clear();
            this.Lead = null;
        }

        public IReadOnlyList<BossPlaceNews> Update(IReadOnlyList<BossPlaceSight> sights, Vector2 player, float playerRadius, bool sheltered)
        {
            var news = new List<BossPlaceNews>();
            var active = sights.Where(sight => sight.Active).OrderBy(sight => sight.Kind).ThenBy(sight => Vector2.Distance(sight.At, player)).ToList();
            this.saidIds.RemoveWhere(id => active.All(sight => sight.Id != id)); // one that comes back is said again
            this.reached.RemoveWhere(id => active.All(sight => sight.Id != id));
            (Vector2 At, float Radius)? lead = null;
            foreach (var sight in active)
            {
                var to = Target(sight, playerRadius);
                if (!Leads(sight.Kind))
                {
                    // Told of once a fight by its name, the nearest for all that share it; one the game does not name tells nothing.
                    if (sight.Name.Length > 0 && this.toldOf.Add($"{sight.Kind}|{sight.Name}"))
                    {
                        news.Add(new BossPlaceNews(sight.Kind, sight.Name, to));
                    }

                    continue;
                }

                if (this.saidIds.Add(sight.Id))
                {
                    news.Add(new BossPlaceNews(sight.Kind, sight.Name, to));
                }

                var radius = sight.Kind == BossPlaceKind.Cover ? 0f : sight.Radius;
                if (Vector2.Distance(to, player) - radius - playerRadius <= AtPlaceWorld)
                {
                    this.reached.Add(sight.Id);
                }

                var leads = sight.Kind switch
                {
                    BossPlaceKind.Refuge => !sheltered,
                    BossPlaceKind.Cover => !this.reached.Contains(sight.Id),
                    _ => true,
                };
                if (leads && lead == null)
                {
                    lead = (to, radius);
                }
            }

            this.Lead = lead;
            return news;
        }

        /// <summary>Where to stand for a place: at it, or for a cover a body's width behind it as seen from the boss.</summary>
        public static Vector2 Target(in BossPlaceSight sight, float playerRadius)
        {
            if (sight.Kind != BossPlaceKind.Cover)
            {
                return sight.At;
            }

            var away = sight.At - sight.Boss;
            return away.LengthSquared() < 1f ? sight.At : sight.At + (Vector2.Normalize(away) * (sight.Radius + (2f * playerRadius)));
        }
    }
}
