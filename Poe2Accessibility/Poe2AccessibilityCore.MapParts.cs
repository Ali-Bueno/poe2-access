namespace Poe2Accessibility
{
    using System;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The part of the map the player walks into, said as they cross into it (docs/radar.md).</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>A part of the map is many seconds of walking across: looked at twice a second.</summary>
        private const int MapPartPollMs = 500;

        private readonly MapPartWatch mapPart = new();

        private long mapPartPolledAt = long.MinValue / 2;

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
            if (this.entities == null || this.entities.RoomsApart() || this.entities.PlaceOnMap() is not { } place ||
                this.mapPart.Update(place.Frame.Share(place.Cell), place.Frame.Span(ExplorationMap.SeenRadiusCells), now) is not { } entered)
            {
                return;
            }

            var standing = this.entities.MapParts(place.Frame).Cast<MapPart?>().FirstOrDefault(part => (part!.Value.Across, part.Value.Down) == entered);
            this.output?.Say(SpeechKind.Places, Announcements.MapPartEntered(entered.Across, entered.Down, standing));
        }
    }
}
