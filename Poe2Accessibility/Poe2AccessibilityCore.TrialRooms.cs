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
            var cell = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var arrived = this.carried.Update(cell, this.entities.OnPlayersGround);
            var news = this.entities.NoteGround(cell, this.carried.CarriedFrom);
            if (this.trialScope.InTrial(now))
            {
                if (arrived && this.Settings.AnnounceTrialProgress && this.semantics?.TakeTrialRoomChosen() is { Length: > 0 } room)
                {
                    this.output?.Say(SpeechKind.Places, Announcements.TrialRoomEntered(room));
                }
            }
            else if (news != PieceNews.None && this.Settings.AnnounceMapParts && !this.InTownOrHideout())
            {
                this.log?.Info($"Ground: {news} at cell {cell.X:0},{cell.Y:0}");
                this.output?.Say(SpeechKind.Places, news == PieceNews.EnteredApart ? Announcements.EnteredGroundApart : Announcements.BackToMainGround);
            }
        }
    }
}
