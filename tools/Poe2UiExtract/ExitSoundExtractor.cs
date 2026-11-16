using System.Text;

internal static partial class SoundExtractor
{
    /// <summary>The exits' ambient sounds go to <c>exits/&lt;material&gt;/</c> of the sounds folder.</summary>
    private const string ExitsFolder = "exits";

    /// <summary>The sample length the audition recipes were cut from: <see cref="TrimAndCap"/> at 4 s.</summary>
    private const double ExitSourceMaxSeconds = 4;

    /// <summary>Under this RMS a rendered file is silence (the recipes level to 0.05).</summary>
    private const double SilentRms = 0.001;

    /// <summary>
    /// Renders the exits' ambient beds and events from the recipes beside the item catalog (no game audio is shipped: each
    /// piece is cut from the player's own banks). A bank or sample the game lacks leaves that file out and is listed; nothing here
    /// fails the run.
    /// </summary>
    private static void WriteExitSounds(IntPtr sys, string gameDir, string outDir, string recipePath, string tmp)
    {
        if (!File.Exists(recipePath))
        {
            Say.Out.WriteLine($"No exit sound recipes at {recipePath}: the exits stay silent.");
            return;
        }

        ExitSoundRecipes recipes;
        try
        {
            recipes = ExitSoundRecipes.Parse(File.ReadAllText(recipePath, Encoding.UTF8));
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Say.Out.WriteLine($"Exit sound recipes unreadable ({error.Message}): the exits stay silent.");
            return;
        }

        // Every sample the recipes read, decoded once, trimmed and capped as the audition extractor did, as mono.
        var samples = new Dictionary<(string, string), float[]>();
        foreach (var bankGroup in recipes.Sources().GroupBy(source => source.Bank))
        {
            if (OpenBank(sys, gameDir, bankGroup.Key, tmp) is not { } snd)
            {
                continue;
            }

            FMOD_Sound_GetNumSubSounds(snd, out int count);
            foreach (var (bank, sample) in bankGroup)
            {
                if (FindSub(snd, count, sample) is { } sub && sub != IntPtr.Zero)
                {
                    var (pcm, ch, rate) = Decode(sub);
                    if (rate == recipes.Rate)
                    {
                        samples[(bank, sample)] = Mono(Clipped16(TrimAndCap(pcm, ch, rate, ExitSourceMaxSeconds).pcm), ch);
                    }
                }
            }

            FMOD_Sound_Release(snd);
        }

        var missing = new List<string>();
        int written = 0;
        foreach (var set in recipes.Sets)
        {
            var folder = Path.Combine(outDir, ExitsFolder, set.Material);
            Directory.CreateDirectory(folder);
            foreach (var (name, pcm) in recipes.Render(set, (bank, sample) => samples.GetValueOrDefault((bank, sample)), missing))
            {
                if (ExitSoundDsp.Rms(pcm) < SilentRms)
                {
                    missing.Add($"{set.Material}/{name} (silent)"); // a dead sample (a gust of 5 ms) would only be a gap in the scatter
                    continue;
                }

                WriteWav(Path.Combine(folder, name + ".wav"), pcm, 1, recipes.Rate);
                written++;
            }
        }

        Say.Out.WriteLine($"Exit sounds: {written} files in {ExitsFolder}/ ({recipes.Sets.Count} sets)"
            + (missing.Count > 0 ? $"; {missing.Count} left out: {string.Join("; ", missing)}" : string.Empty));
    }

    /// <summary>
    /// The samples as the 16-bit WAV dumps the recipes were cut from held them (<see cref="DumpBanks"/>): a decoded peak past
    /// full scale clipped there, which levels a clipped sound differently.
    /// </summary>
    private static float[] Clipped16(float[] pcm) => pcm.Select(v => MathF.Round(Math.Clamp(v, -1f, 1f) * 32767f) / 32767f).ToArray();

    /// <summary>Interleaved samples as one channel, the channels averaged.</summary>
    private static float[] Mono(float[] pcm, int channels)
    {
        if (channels == 1)
        {
            return pcm;
        }

        var mono = new float[pcm.Length / channels];
        for (int i = 0; i < mono.Length; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++) sum += pcm[i * channels + c];
            mono[i] = sum / channels;
        }

        return mono;
    }
}
