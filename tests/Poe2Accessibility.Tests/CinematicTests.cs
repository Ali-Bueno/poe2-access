namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A video's skip prompt said once, and a travel's narration line by line as the video reaches each.</summary>
    public static class CinematicTests
    {
        private const string Prompt = "Presiona A para saltar";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheNarrationFollowsTheVideo), TheNarrationFollowsTheVideo),
            (nameof(LinesPassedBeforeTheVideoWasToldAreStillSaid), LinesPassedBeforeTheVideoWasToldAreStillSaid),
            (nameof(AVideoWithNoNarrationSaysItsPrompt), AVideoWithNoNarrationSaysItsPrompt),
            (nameof(ATravelsVideoIsToldByItsLoadWithoutThePrompt), ATravelsVideoIsToldByItsLoadWithoutThePrompt),
            (nameof(BesideTheTravelNpcTheNarrationStartsAtOnce), BesideTheTravelNpcTheNarrationStartsAtOnce),
            (nameof(APlainLoadSaysNothing), APlainLoadSaysNothing),
            (nameof(EnglishHearsTheVoice), EnglishHearsTheVoice),
            (nameof(EveryTravelWithAVideoHasItsLinesInOrder), EveryTravelWithAVideoHasItsLinesInOrder),
        };

        private static void InSpanish(Action test)
        {
            try
            {
                Localization.Detect("es");
                test();
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void TheNarrationFollowsTheVideo() => InSpanish(() =>
        {
            var tracker = new CinematicTracker();
            tracker.StateChanged(true, 1000);
            Equal(0, tracker.Tick(null, "G2_1", 1500).Count, "no prompt yet");
            Equal("Cinemática. Presiona A para saltar | La condesa ha escapado con la bestia.", string.Join(" | ", tracker.Tick(Prompt, "G2_1", 3500)), "prompt, then the line under way");
            Equal(true, tracker.Playing, "playing");
            Equal(0, tracker.Tick(Prompt, "G2_1", 7000).Count, "between lines");
            Equal("Todo intento de controlarla ha terminado en cataclismo.", tracker.Tick(Prompt, "G2_1", 7300).Single(), "second line at 6.3 s");
            Equal(0, tracker.Tick(null, "G2_1", 7400).Count, "the prompt going does not end it");
            Equal(3, tracker.Tick(null, "G2_1", 40000).Count, "the rest");
            Equal(0, tracker.Tick(null, "G2_1", 50000).Count, "said once");
            tracker.StateChanged(false, 51000);
            Equal(false, tracker.Playing, "over with the load");
        });

        private static void LinesPassedBeforeTheVideoWasToldAreStillSaid() => InSpanish(() =>
        {
            var tracker = new CinematicTracker();
            tracker.StateChanged(true, 0);
            Equal(1, tracker.Tick(Prompt, string.Empty, 2500).Count, "the prompt alone while the area is unknown");
            Equal(2, tracker.Tick(Prompt, "G3_1", 3000).Count, "both lines begun by 3 s");
            Equal("Act2_Transition", tracker.Video, "video");
        });

        private static void AVideoWithNoNarrationSaysItsPrompt() => InSpanish(() =>
        {
            var tracker = new CinematicTracker();
            tracker.StateChanged(false, 0);
            Equal("Cinemática. Presiona A para saltar", tracker.Tick(Prompt, string.Empty, 100).Single(), "after creating a character");
            Equal(0, tracker.Tick(Prompt, "G2_1", 30000).Count, "no load, no narration");
            tracker.StateChanged(true, 40000);
            Equal(1, tracker.Tick(Prompt, "G1_4", 43000).Count, "a load to an area with no video of its own");
        });

        private static void ATravelsVideoIsToldByItsLoadWithoutThePrompt() => InSpanish(() =>
        {
            // 2026-11-17, the Matriarchs' travel from G2_town: the prompt showed 39.3 s into a 40.9 s load.
            var tracker = new CinematicTracker();
            tracker.StateChanged(true, 0, "G2_town");
            Equal(0, tracker.Tick(null, "G3_1", 3900).Count, "a plain load may still end");
            Equal("Cinemática. | La bestia se ha vuelto demasiado poderosa. | Ya no podemos contenerla por medios mortales.", string.Join(" | ", tracker.Tick(null, "G3_1", 4000)), "the video, and the lines begun by then");
            Equal(true, tracker.Playing, "playing");
            Equal(0, tracker.Tick(Prompt, "G3_1", 4100).Count, "a prompt after that is not news");
            Equal(5, tracker.Tick(null, "G3_1", 39300).Count, "the rest as the narrator reaches them");
        });

        private static void BesideTheTravelNpcTheNarrationStartsAtOnce() => InSpanish(() =>
        {
            var tracker = new CinematicTracker();
            tracker.StateChanged(true, 0, "G2_town", byTravelNpc: true);
            Equal(0, tracker.Tick(null, string.Empty, 30).Count, "the area ahead not known yet");
            Equal("Cinemática. | La bestia se ha vuelto demasiado poderosa.", string.Join(" | ", tracker.Tick(null, "G3_1", 63)), "from the first instant");
            tracker.StateChanged(true, 0, "G2_town", byTravelNpc: true);
            Equal(0, tracker.Tick(null, "G2_6", 100).Count, "beside the NPC, to an area with no video");
        });

        private static void APlainLoadSaysNothing() => InSpanish(() =>
        {
            var tracker = new CinematicTracker();
            tracker.StateChanged(true, 0, "G1_town");
            Equal(0, tracker.Tick(null, "G2_1", 2500).Count, "by waypoint from the town, a plain load ends first");
            tracker.StateChanged(true, 3000, "G1_12");
            Equal(0, tracker.Tick(null, "G2_1", 30000).Count, "a slow load from outside a town");
            Equal(false, tracker.Playing, "not playing");
        });

        private static void EnglishHearsTheVoice()
        {
            var tracker = new CinematicTracker();
            tracker.StateChanged(true, 0);
            Equal("Cinematic. Press A to skip", tracker.Tick("Press A to skip", "G2_1", 30000).Single(), "the prompt alone");
            Equal(0, Announcements.Narration("Act1_Transition").Count, "no lines in the voice's language");
        }

        private static void EveryTravelWithAVideoHasItsLinesInOrder() => InSpanish(() =>
        {
            foreach (var area in new[] { "G2_1", "G3_1", "G4_town", "G_Endgame_Town" })
            {
                var lines = Announcements.Narration(Cinematics.VideoTo(area)!);
                Equal(true, lines.Count >= 5, $"{area} has lines");
                Equal(true, lines.Zip(lines.Skip(1), (a, b) => a.Seconds < b.Seconds).All(inOrder => inOrder), $"{area} in order");
            }

            Equal(null, Cinematics.VideoTo("G1_town"), "a town has none");
        });
    }
}
