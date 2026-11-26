namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     Where to take the Expedition charge being placed: once the charge stands still at a new spot, the nearest find that no
    ///     charge placed reaches and this one would not either. A sighted player sees the markers glow; this is that glow's
    ///     direction. Pure: the caller turns the offset into steps and a stick direction.
    /// </summary>
    public sealed class PlacementGuide
    {
        /// <summary>The charge has stood this long before its spot is news: sweeping the stick is no stream of speech.</summary>
        public const long SettleMs = 600;

        /// <summary>A spot this many grid cells from the one last said is new (a blast reaches 28 cells).</summary>
        public const float NewSpotCells = 4f;

        private Vector2? moving;
        private long stillSince;
        private Vector2? saidAt;
        private uint? saidFind;

        /// <summary>No charge is being placed.</summary>
        public void Reset() => (this.moving, this.saidAt, this.saidFind) = (null, null, null);

        /// <summary>
        ///     The find to head for and its offset from the charge (grid cells), when the charge has just settled at a new spot or the
        ///     nearest open find changed; null otherwise, while not placing and once every find is covered.
        /// </summary>
        public (ExpeditionFind Find, Vector2 Offset)? Next(Vector2? charge, IReadOnlyList<(uint Id, ExpeditionFind Find, Vector2 Place)> open, long now)
        {
            if (charge is not { } at)
            {
                this.Reset();
                return null;
            }

            if (this.moving is not { } was || Vector2.Distance(was, at) >= 1f)
            {
                (this.moving, this.stillSince) = (at, now);
                return null;
            }

            if (now - this.stillSince < SettleMs || open.Count == 0)
            {
                return null;
            }

            var nearest = open[0];
            foreach (var find in open)
            {
                if (Vector2.DistanceSquared(find.Place, at) < Vector2.DistanceSquared(nearest.Place, at))
                {
                    nearest = find;
                }
            }

            if (this.saidFind == nearest.Id && this.saidAt is { } said && Vector2.Distance(said, at) < NewSpotCells)
            {
                return null;
            }

            (this.saidAt, this.saidFind) = (at, nearest.Id);
            return (nearest.Find, nearest.Place - at);
        }
    }
}
