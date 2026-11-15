namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>Which narrated video a travel plays (docs/research/cinematic-subtitles.md).</summary>
    public static class Cinematics
    {
        /// <summary>By the area travelled to: NPCTalk rows 286, 411, 527 and 679, SetAreaTransitionVideoPath beside AreaTransitionTo (0.5.5f).</summary>
        private static readonly Dictionary<string, string> VideoByDestination = new(StringComparer.Ordinal)
        {
            ["G2_1"] = "Act1_Transition",
            ["G3_1"] = "Act2_Transition",
            ["G4_town"] = "Act3_Transition",
            ["G_Endgame_Town"] = "Interlude_Endgame_Transition",
        };

        public static string? VideoTo(string destinationAreaId) => VideoByDestination.GetValueOrDefault(destinationAreaId);
    }

    /// <summary>
    ///     A pre-rendered video, told by its skip prompt: what to say as it starts and, for a narrated one played
    ///     over an area's load, each line as the narrator reaches it.
    /// </summary>
    public sealed class CinematicTracker
    {
        private IReadOnlyList<(double Seconds, string Text)> lines = Array.Empty<(double, string)>();
        private int next;
        private bool loading;
        private long loadingSince;

        /// <summary>A video is on screen, until the state it plays in ends.</summary>
        public bool Playing { get; private set; }

        /// <summary>The narrated video recognised, for the log.</summary>
        public string? Video { get; private set; }

        /// <summary>The video of a travel starts with the load, so its lines are timed from there.</summary>
        public void StateChanged(bool loading, long now)
        {
            (this.loading, this.loadingSince, this.Playing, this.Video, this.next) = (loading, now, false, null, 0);
            this.lines = Array.Empty<(double, string)>();
        }

        /// <summary>
        ///     What to say now. <paramref name="skipPrompt"/> is the prompt's text while it shows, null otherwise;
        ///     <paramref name="destination"/> the area being loaded, empty until known.
        /// </summary>
        public IReadOnlyList<string> Tick(string? skipPrompt, string destination, long now)
        {
            if (!this.Playing && skipPrompt == null)
            {
                return Array.Empty<string>();
            }

            List<string>? say = null;
            if (!this.Playing)
            {
                this.Playing = true;
                (say ??= new List<string>()).Add(Announcements.CinematicStarted(skipPrompt!));
            }

            if (this.Video == null && this.loading &&Cinematics.VideoTo(destination) is { } video)
            {
                this.Video = video;
                this.lines = Announcements.Narration(video);
            }

            // A line whose moment passed before the video was told is still said: nothing of the narration is lost.
            while (this.next < this.lines.Count && now - this.loadingSince >= this.lines[this.next].Seconds * 1000)
            {
                (say ??= new List<string>()).Add(this.lines[this.next++].Text);
            }

            return say ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
    }
}
