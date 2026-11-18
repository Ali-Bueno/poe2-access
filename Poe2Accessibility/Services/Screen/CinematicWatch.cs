namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using Poe2Accessibility.Models;

    /// <summary>Says a video's skip prompt and narration (<see cref="CinematicTracker"/>) and learns where a load leads.</summary>
    public sealed class CinematicWatch
    {
        private const int AreaPollMs = 250;

        /// <summary>How often, in a town, the travel NPCs' distance is looked at.</summary>
        private const int NpcPollMs = 500;

        /// <summary>Beside a travel NPC: within 2.5 scanner steps, as when talking to it.</summary>
        private const float NpcReachWorld = 250f;

        /// <summary>The last look beside one must be this recent as the load starts (dialogue closed, load 0.7 s later on 2026-11-18).</summary>
        private const int NpcRecentMs = 1500;

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly CinematicTracker tracker = new();
        private long loadingSince;
        private long lastAreaPoll;
        private string leaving = string.Empty;
        private string destination = string.Empty;
        private long npcPolledAt;
        private long besideNpcAt;
        private bool byNpc;

        public CinematicWatch(IAccessibilityOutputService output, PluginLog log)
        {
            this.output = output;
            this.log = log;
        }

        /// <summary>The id of the area the loading state holds, null when it reads none.</summary>
        public Func<string?> LoadingAreaId { get; init; } = static () => null;

        /// <summary>World distance from the player to the nearest travel NPC (<see cref="Cinematics.IsTravelNpc"/>), null when none is near.</summary>
        public Func<float?> TravelNpcDistance { get; init; } = NearestTravelNpc;

        /// <summary>The id of the area last played in.</summary>
        public Func<string> AreaId { get; init; } = static () => string.Empty;

        /// <summary>A video covers the screen: what is written under it is not news.</summary>
        public bool Playing => this.tracker.Playing;

        public void StateChanged(bool loading)
        {
            var now = Environment.TickCount64;
            if (!loading && this.byNpc && this.tracker.Playing && now - this.loadingSince < CinematicTracker.TravelVideoAfterMs)
            {
                // Beside the NPC, yet a plain load (a waypoint next to it): what was begun of the narration is cut.
                this.log.Info($"Cinematic: the load ended {now - this.loadingSince} ms in, no video after all");
                this.output.Stop();
            }

            (this.loadingSince, this.lastAreaPoll, this.leaving, this.destination) = (loading ? now : 0, 0, this.AreaId(), string.Empty);
            this.byNpc = loading && this.besideNpcAt > 0 && now - this.besideNpcAt < NpcRecentMs;
            this.tracker.StateChanged(loading, now, this.leaving, this.byNpc);
        }

        /// <summary>Per frame.</summary>
        public void Tick(ScreenSnapshot snapshot, bool enabled)
        {
            var now = Environment.TickCount64;
            this.TrackDestination(now);
            this.WatchTravelNpc(now);
            if (!enabled)
            {
                return;
            }

            var wasPlaying = this.tracker.Playing;
            var known = this.tracker.Video;
            var prompt = wasPlaying ? null : snapshot.Layers.FirstOrDefault(layer => layer.SkipsCinematic);
            var lines = this.tracker.Tick(prompt == null ? null : string.Join(" ", prompt.Texts.Select(text => text.Text)), this.destination, now);
            if (!wasPlaying && this.tracker.Playing)
            {
                this.log.Info(prompt == null
                    ? $"Cinematic: a {now - this.loadingSince} ms load from {this.leaving} to {this.destination}{(this.byNpc ? ", begun beside the travel NPC" : string.Empty)}, no skip prompt yet"
                    : $"Cinematic: skip prompt on layer {prompt.Index} ({prompt.Name}), leaving {this.leaving}");
            }

            if (known == null && this.tracker.Video is { } video)
            {
                this.log.Info($"Cinematic narration: {video}, {now - this.loadingSince} ms into the load");
            }

            foreach (var line in lines)
            {
                this.output.Speak(line, false);
            }
        }

        /// <summary>In a town, out of a load: whether the player stands beside a travel NPC, twice a second.</summary>
        private void WatchTravelNpc(long now)
        {
            if (this.loadingSince != 0 || now - this.npcPolledAt < NpcPollMs || !this.AreaId().EndsWith("_town", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.npcPolledAt = now;
            if (this.TravelNpcDistance() is { } distance && distance <= NpcReachWorld)
            {
                this.besideNpcAt = now;
            }
        }

        private static float? NearestTravelNpc()
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            float? nearest = null;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && Cinematics.IsTravelNpc(entity.Path) && entity.TryGetComponent<Render>(out var render))
                {
                    var distance = Vector2.Distance(here, new Vector2(render.WorldPosition.X, render.WorldPosition.Y));
                    nearest = nearest is { } near && near <= distance ? near : distance;
                }
            }

            return nearest;
        }

        /// <summary>The loading state names the area ahead before the load ends; it is asked a few times a second until it does.</summary>
        private void TrackDestination(long now)
        {
            if (this.loadingSince == 0 || this.destination.Length > 0 || now - this.lastAreaPoll < AreaPollMs)
            {
                return;
            }

            this.lastAreaPoll = now;
            if (this.LoadingAreaId() is { } id && id != this.leaving)
            {
                this.destination = id;
                this.log.Info($"Loading: {id} known {now - this.loadingSince} ms in (leaving {(this.leaving.Length > 0 ? this.leaving : "no area")})");
            }
        }
    }
}
