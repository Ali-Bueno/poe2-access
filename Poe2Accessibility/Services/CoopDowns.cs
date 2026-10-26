namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Local co-op deaths (Clearfell, 2026-10-25): a local player who dies while the other lives gets no death screen;
    ///     the body shows "acércate para revivir" and the other revives it by holding A beside it ("Revivir, A" is the
    ///     interact prompt). The death screen comes only once both are dead. So the followed player hears that the other
    ///     is down or back, and that it can be revived itself. The first reading in an area instance is not news: a respawn
    ///     at the checkpoint loads a new one with everyone alive (said as revived before the area change was seen, 2026-10-25).
    /// </summary>
    public sealed class CoopDowns
    {
        private long area;
        private bool? followedAlive;
        private (string Name, bool Alive)? other;

        /// <summary>A new area (a respawn reloads it): the next reading is the baseline.</summary>
        public void Reset()
        {
            this.followedAlive = null;
            this.other = null;
        }

        /// <param name="area">The area instance (its address): another one starts over.</param>
        /// <param name="followedAlive">Whether the player the mod follows is alive.</param>
        /// <param name="other">The other local player's name and whether it is alive; null when playing alone.</param>
        public List<string> Update(long area, bool followedAlive, (string Name, bool Alive)? other)
        {
            var news = new List<string>();
            if (area != this.area)
            {
                this.Reset();
                this.area = area;
            }

            if (other is { } partner && this.other is { } before && before.Name == partner.Name && this.followedAlive is { } wasAlive)
            {
                if (before.Alive && !partner.Alive && followedAlive)
                {
                    news.Add(Announcements.PartnerDown(partner.Name));
                }
                else if (!before.Alive && partner.Alive)
                {
                    news.Add(Announcements.PartnerUp(partner.Name));
                }

                if (wasAlive && !followedAlive && partner.Alive)
                {
                    news.Add(Announcements.YouAreDown);
                }
                else if (!wasAlive && followedAlive)
                {
                    news.Add(Announcements.YouAreUp);
                }
            }

            this.followedAlive = followedAlive;
            this.other = other;
            return news;
        }
    }
}
