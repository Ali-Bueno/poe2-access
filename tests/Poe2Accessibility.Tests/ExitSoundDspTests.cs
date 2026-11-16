namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Check;

    /// <summary>The exits' ambient sounds: the DSP the extractor makes them with and the recipes as shipped.</summary>
    public static class ExitSoundDspTests
    {
        private const int Rate = 44100;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(BiquadsPassAndStopTheirBands), BiquadsPassAndStopTheirBands),
            (nameof(AnEchoIsFfmpegsMultiTapOfTheInput), AnEchoIsFfmpegsMultiTapOfTheInput),
            (nameof(ASeamlessLoopRunsFromItsEndIntoItsStart), ASeamlessLoopRunsFromItsEndIntoItsStart),
            (nameof(LevelReachesTheRmsAndLimitsThePeak), LevelReachesTheRmsAndLimitsThePeak),
            (nameof(FadesStartAtSilenceAndEndAtSilence), FadesStartAtSilenceAndEndAtSilence),
            (nameof(TheShippedRecipesRenderTheSameSetsEveryTime), TheShippedRecipesRenderTheSameSetsEveryTime),
        };

        private static float[] Sine(double hz, int seconds = 1) =>
            Enumerable.Range(0, Rate * seconds).Select(i => (float)Math.Sin(2 * Math.PI * hz * i / Rate)).ToArray();

        private static double SteadyRms(float[] x) => ExitSoundDsp.Rms(x.Skip(x.Length / 2).ToArray());

        private static void BiquadsPassAndStopTheirBands()
        {
            Near(1f, (float)(SteadyRms(ExitSoundDsp.Apply("lowpass=f=1000", Sine(100), Rate)) / SteadyRms(Sine(100))), "low-pass passes 100 Hz", 0.02f);
            Equal(true, SteadyRms(ExitSoundDsp.Apply("lowpass=f=1000", Sine(8000), Rate)) < 0.02 * SteadyRms(Sine(8000)), "low-pass stops 8 kHz");
            Near(1f, (float)(SteadyRms(ExitSoundDsp.Apply("highpass=f=1000", Sine(8000), Rate)) / SteadyRms(Sine(8000))), "high-pass passes 8 kHz", 0.02f);
            Equal(true, SteadyRms(ExitSoundDsp.Apply("highpass=f=1000", Sine(100), Rate)) < 0.02 * SteadyRms(Sine(100)), "high-pass stops 100 Hz");
            var band = "bandpass=f=900:width_type=q:w=2";
            var centre = SteadyRms(ExitSoundDsp.Apply(band, Sine(900), Rate)) / SteadyRms(Sine(900));
            Near(1f, (float)centre, "the band-pass passes its centre at unity", 0.03f);
            Equal(true, SteadyRms(ExitSoundDsp.Apply(band, Sine(100), Rate)) < 0.2 * SteadyRms(Sine(100)), "and stops what is far from it");
            Equal(Rate, ExitSoundDsp.Apply("lowpass=f=1000,highpass=f=500", Sine(100), Rate).Length, "a chain keeps the length");
        }

        private static void AnEchoIsFfmpegsMultiTapOfTheInput()
        {
            var impulse = new float[Rate];
            impulse[10] = 1f;
            var echoed = ExitSoundDsp.Apply("aecho=0.8:0.5:100|200:0.35|0.2", impulse, Rate);
            Equal(Rate + 8820, echoed.Length, "the echo rings out past the input by its longest delay");
            Near(0.4f, echoed[10], "the dry sound is in_gain x out_gain", 1e-5f); // measured with ffmpeg 8.1: 6553 of 16384
            Near(0.175f, echoed[10 + 4410], "the first echo is the input x decay x out_gain", 1e-5f);
            Near(0.1f, echoed[10 + 8820], "the second is of the input, not of the first echo", 1e-5f);
            Equal(true, ExitSoundDsp.HasEcho("lowpass=f=1800,aecho=0.8:0.5:120|240:0.35|0.2") && !ExitSoundDsp.HasEcho("lowpass=f=1000"), "an echo is told from a plain filter");
            Equal(240 * Rate / 1000, ExitSoundDsp.EchoTailSamples("aecho=0.8:0.5:120|240:0.35|0.2", Rate), "the tail is the longest delay");
        }

        private static void ASeamlessLoopRunsFromItsEndIntoItsStart()
        {
            var source = Sine(220, 2);
            var loop = ExitSoundDsp.Seamless(source, Rate, Rate / 4);
            Equal(Rate, loop.Length, "the loop is the asked length");
            Near(source[Rate], loop[0], "it starts where the unfaded end would continue", 1e-4f);
            Near(source[Rate / 4], loop[Rate / 4], "outside the fade it is the source", 1e-6f);
            Equal(true, Math.Abs(loop[0] - source[Rate - 1]) < 0.1, "so the wrap is continuous");
        }

        private static void LevelReachesTheRmsAndLimitsThePeak()
        {
            var tone = Sine(300).Select(v => v * 0.5f).ToArray();
            Near(0.05f, (float)ExitSoundDsp.Rms(ExitSoundDsp.Level(tone, 0.05, 0.89)), "levelled to its RMS", 1e-4f);
            var spike = new float[Rate];
            spike[100] = 1f;
            Equal(true, ExitSoundDsp.Level(spike, 0.05, 0.89).Max(Math.Abs) <= 0.8901f, "a spike is held under the limit");
            var quiet = tone.Select(v => v * 0.1f).ToArray();
            Near(0.005f, (float)ExitSoundDsp.Rms(ExitSoundDsp.Level(quiet, 0.05, 10, reference: tone)), "an event is measured by its unfaded body: the gain is the body's", 1e-4f);
        }

        private static void FadesStartAtSilenceAndEndAtSilence()
        {
            var faded = ExitSoundDsp.Fade(Enumerable.Repeat(1f, 1000).ToArray(), 100, 200);
            Equal(0f, faded[0], "starts silent");
            Equal(0f, faded[999], "ends silent");
            Equal(1f, faded[500], "full in the middle");
        }

        /// <summary>The recipes as shipped (Poe2Accessibility/catalog/exit-sounds.json): every material has both sets, deterministic.</summary>
        private static void TheShippedRecipesRenderTheSameSetsEveryTime()
        {
            var directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", ExitSoundRecipes.FileName)))
            {
                directory = System.IO.Path.GetDirectoryName(directory) ?? throw new System.IO.FileNotFoundException(ExitSoundRecipes.FileName + " is not under any folder above the tests");
            }

            var recipes = ExitSoundRecipes.Parse(System.IO.File.ReadAllText(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", ExitSoundRecipes.FileName)));
            Equal(22, recipes.Sets.Count, "eleven materials, two sets each");
            Equal(11, recipes.Sets.Select(set => set.Material).Distinct().Count(), "eleven materials");
            Equal(true, recipes.Sets.All(set => set.Beds.Count >= 2 && (set.Events.Count + set.Drops.Count) >= 4), "each set has beds and events");

            // A made-up source (noise from a fixed seed) stands in for a bank's sample.
            var random = new Random(3);
            var noise = Enumerable.Range(0, Rate * 4).Select(_ => (float)((random.NextDouble() * 2) - 1) * 0.3f).ToArray();
            var missing = new List<string>();
            var stairs = recipes.Sets.First(set => set.Material == "stairs");
            var first = recipes.Render(stairs, (_, _) => noise, missing);
            var again = recipes.Render(stairs, (_, _) => noise, missing);
            Equal(0, missing.Count, "nothing missing when every sample is there");
            Equal(true, first.Keys.SequenceEqual(again.Keys) && first.All(file => file.Value.SequenceEqual(again[file.Key])), "the same files every time");
            Equal(true, first.Keys.Contains("bed_A1") && first.Keys.Contains("event_A8"), "files are named bed_<Set><n> and event_<Set><n>");
            Equal(3 * Rate, first["bed_A1"].Length, "a bed is the loop length");
            Near(0.05f, (float)ExitSoundDsp.Rms(first["bed_A1"]), "levelled to 0.05", 1e-3f);

            var cave = recipes.Sets.First(set => set.Material == "cave" && set.Name == "A");
            Equal(8, recipes.Render(cave, (_, _) => noise, missing).Keys.Count(name => name.StartsWith("event_A", StringComparison.Ordinal)), "the cave's synthesised drops");
            missing.Clear();
            Equal(true, recipes.Render(cave, (_, _) => null, missing).Keys.All(name => name.StartsWith("event", StringComparison.Ordinal)), "a bank the game lacks leaves its beds out, the drops need none");
            Equal(3, missing.Count, "and says which");
        }
    }
}
