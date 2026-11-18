namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Says a video's skip prompt and narration (<see cref="CinematicTracker"/>) and learns where a load leads.</summary>
    public sealed class CinematicWatch
    {
        private const int AreaPollMs = 250;

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly CinematicTracker tracker = new();
        private long loadingSince;
        private long lastAreaPoll;
        private string leaving = string.Empty;
        private string destination = string.Empty;

        public CinematicWatch(IAccessibilityOutputService output, PluginLog log)
        {
            this.output = output;
            this.log = log;
        }

        /// <summary>The id of the area the loading state holds, null when it reads none.</summary>
        public Func<string?> LoadingAreaId { get; init; } = static () => null;

        /// <summary>The id of the area last played in.</summary>
        public Func<string> AreaId { get; init; } = static () => string.Empty;

        /// <summary>A video covers the screen: what is written under it is not news.</summary>
        public bool Playing => this.tracker.Playing;

        public void StateChanged(bool loading)
        {
            var now = Environment.TickCount64;
            (this.loadingSince, this.lastAreaPoll, this.leaving, this.destination) = (loading ? now : 0, 0, this.AreaId(), string.Empty);
            this.tracker.StateChanged(loading, now, this.leaving);
        }

        /// <summary>Per frame.</summary>
        public void Tick(ScreenSnapshot snapshot, bool enabled)
        {
            var now = Environment.TickCount64;
            this.TrackDestination(now);
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
                    ? $"Cinematic: a {now - this.loadingSince} ms load from {this.leaving} to {this.destination}, no skip prompt yet"
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
