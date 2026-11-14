namespace Poe2Accessibility
{
    using System;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using Poe2Accessibility.Services;

    /// <summary>The room of a Trial's floor the player arrives in: its kind, as the Trial's map named it (docs/research/trials-and-campaign.md).</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>The player's place is looked at this often for a room's stairs.</summary>
        private const int TrialRoomPollMs = 250;

        private readonly CarriedWatch carried = new();

        private long trialRoomPolledAt = long.MinValue / 2;

        /// <summary>Per frame: on a Trial's floor, says the room chosen on the map as its stairs carry the player there.</summary>
        private void TickTrialRooms(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.trialRoomPolledAt < TrialRoomPollMs || this.entities == null || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.trialRoomPolledAt = now;
            var arrived = this.carried.Update(new Vector2(player.GridPosition.X, player.GridPosition.Y), this.entities.OnPlayersGround);
            if (arrived && this.trialScope.InTrial(now) && this.Settings.AnnounceTrialProgress && this.semantics?.TakeTrialRoomChosen() is { Length: > 0 } room)
            {
                this.output?.Say(SpeechKind.Places, Announcements.TrialRoomEntered(room));
            }
        }
    }
}
