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

        /// <summary>The NPCs whose travel plays a video (the same NPCTalk rows' column 0): beside one as a load starts, the video is on.</summary>
        private static readonly string[] TravelNpcs =
        {
            "Metadata/NPC/Four_Act1/HoodedMentor",
            "Metadata/NPC/Four_Act2/MarakethMatriarchCouncil",
            "Metadata/NPC/Four_Act3/AlvaZigguratEncampment",
            "Metadata/NPC/Four_Act4/HoodedMentorKingsmarch",
        };

        public static string? VideoTo(string destinationAreaId) => VideoByDestination.GetValueOrDefault(destinationAreaId);

        /// <summary>An entity path, with its "@level" suffix or none, is one of the NPCs whose travel plays a video.</summary>
        public static bool IsTravelNpc(string path)
        {
            var at = path.IndexOf('@');
            return Array.IndexOf(TravelNpcs, at < 0 ? path : path[..at]) >= 0;
        }
    }

    /// <summary>
    ///     A pre-rendered video, told by its skip prompt or, for a travel's, by its load: what to say as it starts
    ///     and, for a narrated one played over an area's load, each line as the narrator reaches it.
    /// </summary>
    public sealed class CinematicTracker
    {
        /// <summary>
        ///     A load from a town to a travel's video area lasting this long is its video: plain loads from a town took
        ///     1.5 to 3.8 s in the logs of 2026-11-15/18, the Act 2 video's 40.9 s, and its skip prompt showed only at
        ///     39.3 s (2026-11-17), so the prompt cannot be waited for.
        /// </summary>
        public const int TravelVideoAfterMs = 4000;

        private IReadOnlyList<(double Seconds, string Text)> lines = Array.Empty<(double, string)>();
        private int next;
        private bool loading;
        private bool fromTown;
        private bool byTravelNpc;
        private long loadingSince;

        /// <summary>A video is on screen, until the state it plays in ends.</summary>
        public bool Playing { get; private set; }

        /// <summary>The narrated video recognised, for the log.</summary>
        public string? Video { get; private set; }

        /// <summary>
        ///     The video of a travel starts with the load, so its lines are timed from there. <paramref name="leaving"/>
        ///     is the area the load leaves: every travel with a video is offered by an NPC in a town.
        ///     <paramref name="byTravelNpc"/>: the player stood beside one of those NPCs as it began, so the video is taken
        ///     as on from the first instant instead of after <see cref="TravelVideoAfterMs"/>.
        /// </summary>
        public void StateChanged(bool loading, long now, string leaving = "", bool byTravelNpc = false)
        {
            (this.loading, this.loadingSince, this.Playing, this.Video, this.next) = (loading, now, false, null, 0);
            this.fromTown = leaving.EndsWith("_town", StringComparison.OrdinalIgnoreCase);
            this.byTravelNpc = loading && byTravelNpc;
            this.lines = Array.Empty<(double, string)>();
        }

        /// <summary>
        ///     What to say now. <paramref name="skipPrompt"/> is the prompt's text while it shows, null otherwise;
        ///     <paramref name="destination"/> the area being loaded, empty until known.
        /// </summary>
        public IReadOnlyList<string> Tick(string? skipPrompt, string destination, long now)
        {
            var travelVideo = this.loading && this.fromTown && (this.byTravelNpc || now - this.loadingSince >= TravelVideoAfterMs) && Cinematics.VideoTo(destination) != null;
            if (!this.Playing && skipPrompt == null && !travelVideo)
            {
                return Array.Empty<string>();
            }

            List<string>? say = null;
            if (!this.Playing)
            {
                this.Playing = true;
                (say ??= new List<string>()).Add(Announcements.CinematicStarted(skipPrompt ?? string.Empty));
            }

            if (this.Video == null && this.loading && Cinematics.VideoTo(destination) is { } video)
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
