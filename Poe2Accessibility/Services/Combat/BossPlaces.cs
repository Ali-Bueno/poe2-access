namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>One entity of a boss's arena that is a place of the fight this poll.</summary>
    /// <param name="Active">A refuge or clone: its state is set. A cover: it stands. A thing to use or break: it is in play.</param>
    public readonly record struct BossPlaceSight(uint Id, BossPlaceKind Kind, string Path, string Name, Vector2 At, float Radius, bool Active);

    /// <summary>A place worth a line: what it is, its name when it has one, and where to walk to.</summary>
    public readonly record struct BossPlaceNews(BossPlaceKind Kind, string Name, Vector2 To);

    /// <summary>
    ///     The places of a boss fight the player is led to (docs/research/boss-mechanics.md §14: SH, CL, CV, US, OB): the
    ///     shelter while it is up and the player is not in it, the true clone, the far side of a cover, and for a while a
    ///     thing to use or break. Each is said once as it turns up, the most pressing kind first. Pure.
    /// </summary>
    public sealed class BossPlaces
    {
        /// <summary>A thing to use or break is led to this long (ms) after it was said: three walks across an arena (<see cref="WeakPointGuide"/>).</summary>
        public const int OfferMs = 15000;

        private readonly HashSet<uint> saidIds = new();
        private readonly Dictionary<string, long> offeredAt = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<uint> reached = new();

        /// <summary>Where the beat leads now (world) and how wide the place is; null when nowhere.</summary>
        public (Vector2 At, float Radius)? Lead { get; private set; }

        public void Reset()
        {
            this.saidIds.Clear();
            this.offeredAt.Clear();
            this.reached.Clear();
            this.Lead = null;
        }

        /// <param name="reachWorld">The gap within which the player is at a place.</param>
        public IReadOnlyList<BossPlaceNews> Update(IReadOnlyList<BossPlaceSight> sights, Vector2 boss, Vector2 player, float playerRadius, float reachWorld,
                                                   bool sheltered, long now)
        {
            var news = new List<BossPlaceNews>();
            var active = sights.Where(sight => sight.Active).OrderBy(sight => sight.Kind).ThenBy(sight => Vector2.Distance(sight.At, player)).ToList();
            this.saidIds.RemoveWhere(id => active.All(sight => sight.Id != id)); // one that comes back is said again
            this.reached.RemoveWhere(id => active.All(sight => sight.Id != id));
            (Vector2 At, float Radius)? lead = null;
            foreach (var sight in active)
            {
                var to = Target(sight, boss, playerRadius);
                var offered = sight.Kind is BossPlaceKind.Use or BossPlaceKind.Break;
                if (offered ? !this.offeredAt.ContainsKey(sight.Path) : this.saidIds.Add(sight.Id))
                {
                    if (offered)
                    {
                        this.offeredAt[sight.Path] = now; // the nearest of its kind speaks for all of them
                    }

                    news.Add(new BossPlaceNews(sight.Kind, sight.Name, to));
                }

                var radius = sight.Kind == BossPlaceKind.Cover ? 0f : sight.Radius;
                if (Vector2.Distance(to, player) - radius - playerRadius <= reachWorld)
                {
                    this.reached.Add(sight.Id);
                }

                var leads = sight.Kind switch
                {
                    BossPlaceKind.Refuge => !sheltered,
                    BossPlaceKind.Clone => true,
                    BossPlaceKind.Cover => !this.reached.Contains(sight.Id),
                    _ => !this.reached.Contains(sight.Id) && now - this.offeredAt[sight.Path] < OfferMs,
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
        public static Vector2 Target(in BossPlaceSight sight, Vector2 boss, float playerRadius)
        {
            if (sight.Kind != BossPlaceKind.Cover)
            {
                return sight.At;
            }

            var away = sight.At - boss;
            return away.LengthSquared() < 1f ? sight.At : sight.At + (Vector2.Normalize(away) * (sight.Radius + (2f * playerRadius)));
        }
    }
}
