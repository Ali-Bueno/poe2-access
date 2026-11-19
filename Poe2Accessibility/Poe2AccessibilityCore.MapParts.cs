namespace Poe2Accessibility
{
    using System;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The part of the map the player walks into, said as they cross into it, and the parts they finish (docs/radar.md).</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>A part of the map is many seconds of walking across: looked at twice a second.</summary>
        private const int MapPartPollMs = 500;

        /// <summary>The frontier the parts are read from is worked out every two seconds.</summary>
        private const int MapPartsDonePollMs = 2000;

        private readonly MapPartWatch mapPart = new();
        private readonly MapPartsDone partsDone = new();

        private long mapPartPolledAt = long.MinValue / 2;
        private long partsDonePolledAt = long.MinValue / 2;

        /// <summary>Per frame: names the part entered with how it stands. A fight holds it back: the part is said once it is over.</summary>
        private void TickMapParts(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || !this.Settings.AnnounceMapParts || now - this.mapPartPolledAt < MapPartPollMs ||
                this.InTownOrHideout() || this.fights?.InFight(FightState.AnyEnemy) == true)
            {
                return;
            }

            this.mapPartPolledAt = now;
            if (this.entities == null || this.entities.RoomsApart() || this.entities.PlaceOnMap() is not { } place)
            {
                return;
            }

            if (this.mapPart.Update(place.Frame.Share(place.Cell), place.Frame.Span(ExplorationMap.SeenRadiusCells), now) is { } entered)
            {
                var standing = this.entities.MapParts(place.Frame).Cast<MapPart?>().FirstOrDefault(part => (part!.Value.Across, part.Value.Down) == entered);
                this.output?.Say(SpeechKind.Places, Announcements.MapPartEntered(entered.Across, entered.Down, standing));
            }

            // Every block of the map is read for it: not every poll, the frontier it reads changes every two seconds at most.
            if (now - this.partsDonePolledAt >= MapPartsDonePollMs)
            {
                this.partsDonePolledAt = now;
                if (this.partsDone.Update(this.entities.MapParts(place.Frame)) is { } news)
                {
                    this.output?.Say(SpeechKind.Places, Announcements.MapPartsDone(news.Done, news.Left));
                }
            }
        }
    }
}
