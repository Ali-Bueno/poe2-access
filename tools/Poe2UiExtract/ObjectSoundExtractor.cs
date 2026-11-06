using Poe2Accessibility.Services;

/// <summary>
///     `object-sounds` command: for every chest-like object the mod has no material word for, the sound the game plays
///     when it opens, as <c>breakables/&lt;name&gt;.wav</c>, and <c>breakables.txt</c> saying which object plays which.
///     A process of its own: it runs native code the `sounds` command does not, and failing must not cost the other cues.
/// </summary>
internal static class ObjectSoundExtractor
{
    private const string ChestBanks = "Chests";
    private const string Folder = "breakables";
    private const string CatalogFile = "breakables.txt";
    private const double MaxSeconds = 1.0;

    /// <summary>Below this the event played nothing an ear would catch.</summary>
    private const float SilentPeak = 0.01f;

    public static int Run(string gameDir, string outDir)
    {
        outDir = Path.GetFullPath(outDir);
        using var index = SharedBundles.OpenParsed(gameDir);
        if (ObjectSoundLinks.Read(index) is not { } links)
        {
            Console.WriteLine("Chests table not found.");
            return 1;
        }

        var wanted = links.Where(link => Breakables.Material(link.Key).Length == 0 && !Breakables.IsCorpse(link.Key)).ToList();
        var sounds = wanted.Select(link => link.Value).Distinct().ToDictionary(sound => sound, AoSoundEvents.EventPaths);
        if (EventRenderer.Render(gameDir, SoundExtractor.HeaderVersion(Path.Combine(gameDir, "fmod.dll")), sounds, ChestBanks) is not { } render)
        {
            return 1;
        }

        Directory.CreateDirectory(Path.Combine(outDir, Folder));
        var written = new HashSet<string>();
        foreach (var (sound, mix) in render.Mixes)
        {
            if (mix.Length == 0 || mix.Max(Math.Abs) < SilentPeak)
            {
                Console.WriteLine($"SKIP silent: {sound}");
                continue;
            }

            var (pcm, seconds) = SoundExtractor.TrimAndCap(mix, 1, render.Rate, MaxSeconds);
            SoundExtractor.WriteWav(Path.Combine(outDir, Folder, AoSoundEvents.FileName(sound) + ".wav"), pcm, 1, render.Rate);
            Console.WriteLine($"{AoSoundEvents.FileName(sound)}  {seconds:F2}s");
            written.Add(sound);
        }

        var lines = wanted.Where(link => written.Contains(link.Value))
            .Select(link => $"{link.Key}\t{AoSoundEvents.FileName(link.Value)}").Order(StringComparer.Ordinal).ToList();
        File.WriteAllLines(Path.Combine(outDir, CatalogFile), lines);
        Console.WriteLine($"{lines.Count} of {wanted.Count} objects have a sound; {written.Count} of {sounds.Count} sounds");
        return written.Count > 0 ? 0 : 1;
    }
}
