using System.Text.Json;
using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `item-sounds` command: maps every ground-item base type (BaseItemTypes.Id, the metadata path) to the drop sample
///     the game plays for it, written as { baseId: sampleName }. The chain, all English dat tables of Bundles2:
///     BaseItemTypes.SoundEffect (offset 88) and .ItemVisualIdentity (124) -> ItemVisualIdentity.InventorySoundEffect
///     (24) / .DropSoundEffect (919) -> SoundEffects.SoundFile (8) = an FMOD event path such as
///     "Audio/Sound Effects/ItemSounds/4.0/Currency/Scrolls/Scroll_Drop.ogg". Offsets follow poe-tool-dev/dat-schema
///     (PoE2) and are guarded by the row sizes. The event is resolved to a sample of ItemDrops.bank by name (event
///     "Belt_Drop" -> sample "Belt_Drop_01"; the numbered samples are variants of one sound). Orb rows carry only a use
///     event ("OrbUse_Chaos_..."), so they take the bank's "OrbDrop_Chaos_01". Read-only on the game; the `sounds`
///     command extracts the samples listed in the output.
/// </summary>
internal static partial class ItemSoundExtractor
{
    public const string Bank = "ItemDrops";

    private const int BaseRowSize = 360, BaseSoundEffect = 88, BaseVisual = 124;
    private const int VisualRowSize = 968, VisualInventorySound = 24, VisualDropSound = 919;
    private const int SoundRowSize = 33, SoundFile = 8, SoundFile2D = 16;

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(gameDir, index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command); the bank is read from the install.</summary>
    public static int Run(string gameDir, Index index, string outFile)
    {
        var bankPath = Path.Combine(gameDir, "FMOD", "Desktop", Bank + ".bank");
        if (!File.Exists(bankPath))
        {
            Console.WriteLine($"Bank missing: {bankPath}");
            return 1;
        }

        var items = DatTable.Open(index, "baseitemtypes");
        var visuals = DatTable.Open(index, "itemvisualidentity");
        var sounds = DatTable.Open(index, "soundeffects");
        if (items == null || visuals == null || sounds == null)
        {
            Console.WriteLine("A dat table is missing.");
            return 1;
        }

        var resolver = new SampleResolver(Fsb5.Names(File.ReadAllBytes(bankPath)));
        if (!FindColumns(items, visuals, sounds, resolver, out var columns))
        {
            Console.WriteLine("The sound columns were not found again (update the offsets from the dat schema).");
            return 1;
        }

        var (baseSoundEffect, baseVisual, visualInventorySound, visualDropSound, soundFile, soundFile2D) = columns;
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var unresolved = new Dictionary<string, int>();
        for (var row = 0; row < items.RowCount; row++)
        {
            var baseSound = items.Key(row, baseSoundEffect);
            var visual = items.Key(row, baseVisual);
            var candidates = new List<long> { baseSound };
            if (visual >= 0)
            {
                candidates.Add(visuals.Key((int)visual, visualInventorySound));
                candidates.Add(visuals.Key((int)visual, visualDropSound));
            }

            // Orb drops: the row only carries the use event; the bank's matching drop sample is the cue.
            var sample = baseSound >= 0 ? resolver.ResolveOrbDrop(sounds.String((int)baseSound, soundFile2D)) : null;
            var misses = new List<string>();
            foreach (var file in candidates.Where(c => c >= 0).Select(c => sounds.String((int)c, soundFile)))
            {
                if (sample != null || file.Length == 0)
                {
                    continue;
                }

                sample = resolver.Resolve(file);
                if (sample == null)
                {
                    misses.Add(EventName(file));
                }
            }

            if (sample != null)
            {
                map[items.String(row, 0)] = sample;
            }
            else
            {
                foreach (var name in misses.Distinct())
                {
                    unresolved[name] = unresolved.GetValueOrDefault(name) + 1;
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {outFile}: {map.Count} base types -> {map.Values.Distinct().Count()} samples; "
            + $"{unresolved.Count} drop events without a sample in {Bank} (left {unresolved.Values.Sum()} base types silent)");
        foreach (var (name, count) in unresolved.OrderByDescending(p => p.Value).Take(15))
        {
            Console.WriteLine($"  unresolved {count,4}  {name}");
        }

        return map.Count > 0 ? 0 : 1;
    }

    /// <summary>
    ///     The six columns: as measured while the row sizes are unchanged and each still holds what it must; after a game
    ///     update that changed a table, found again by content: the sound paths are strings under "Audio/", and a key
    ///     column is the one whose keys lead to the most item drop samples (a random number column leads to none).
    /// </summary>
    private static bool FindColumns(DatTable items, DatTable visuals, DatTable sounds, SampleResolver resolver,
        out (int BaseSound, int BaseVisual, int VisualInventory, int VisualDrop, int File, int File2D) columns)
    {
        static bool IsSoundPath(string text) => text.StartsWith("Audio/", StringComparison.OrdinalIgnoreCase);
        var measured = items.RowSize == BaseRowSize && visuals.RowSize == VisualRowSize && sounds.RowSize == SoundRowSize;
        int file, file2D;
        if (measured && sounds.IsStringColumn(SoundFile, IsSoundPath) && sounds.IsStringColumn(SoundFile2D, IsSoundPath))
        {
            (file, file2D) = (SoundFile, SoundFile2D);
        }
        else
        {
            var paths = Enumerable.Range(1, Math.Max(0, sounds.RowSize - 8)).Where(offset => sounds.IsStringColumn(offset, IsSoundPath)).ToList();
            (file, file2D) = paths.Count switch { 0 => (-1, -1), 1 => (paths[0], paths[0]), _ => (paths[0], paths[1]) };
        }

        columns = (-1, -1, -1, -1, file, file2D);
        if (file < 0)
        {
            return false;
        }

        bool Drops(long sound) => resolver.Resolve(sounds.String((int)sound, file)) != null;
        bool BaseDrops(long sound) => Drops(sound) || resolver.ResolveOrbDrop(sounds.String((int)sound, file2D)) != null;
        var visualKeys = KeyColumns(visuals, sounds.RowCount, Drops, measured ? new[] { VisualInventorySound, VisualDropSound } : null, 2);
        bool VisualDrops(long visual) => visualKeys.Any(column => visuals.Key((int)visual, column) is >= 0 and var sound && Drops(sound));
        var baseSound = KeyColumns(items, sounds.RowCount, BaseDrops, measured ? new[] { BaseSoundEffect } : null, 1);
        var baseVisual = visualKeys.Count == 0 ? new List<int>() : KeyColumns(items, visuals.RowCount, VisualDrops, measured ? new[] { BaseVisual } : null, 1);
        columns = (baseSound.FirstOrDefault(-1), baseVisual.FirstOrDefault(-1), visualKeys.FirstOrDefault(-1), visualKeys.LastOrDefault(-1), file, file2D);
        Console.WriteLine($"Sound columns: BaseItemTypes {columns.BaseSound}/{columns.BaseVisual}, ItemVisualIdentity {columns.VisualInventory}/{columns.VisualDrop}, "
            + $"SoundEffects {file}/{file2D} ({(measured ? "row sizes as measured" : "a table changed: found by content")})");
        return baseSound.Count == 1 && baseVisual.Count == 1 && visualKeys.Count > 0;
    }

    /// <summary>
    ///     The <paramref name="measured"/> columns while they still are keys that lead somewhere; otherwise the best
    ///     scoring key columns into a table of <paramref name="targetRows"/> rows, at most <paramref name="count"/>, in row order.
    /// </summary>
    private static List<int> KeyColumns(DatTable table, int targetRows, Func<long, bool> good, int[]? measured, int count)
    {
        int Score(int offset)
        {
            var score = 0;
            for (var row = 0; row < table.RowCount; row++)
            {
                score += table.Key(row, offset) is >= 0 and var key && good(key) ? 1 : 0;
            }

            return score;
        }

        if (measured != null && measured.All(offset => table.IsKeyColumn(offset, targetRows) && Score(offset) > 0))
        {
            return measured.ToList();
        }

        return Enumerable.Range(0, Math.Max(0, table.RowSize - 7))
            .Where(offset => table.IsKeyColumn(offset, targetRows))
            .Select(offset => (Offset: offset, Score: Score(offset)))
            .Where(column => column.Score > 0)
            .OrderByDescending(column => column.Score)
            .Take(count)
            .Select(column => column.Offset)
            .Order()
            .ToList();
    }

    /// <summary>The distinct sample names of a written item-sounds.json; empty when the file does not exist.</summary>
    public static IReadOnlyList<string> ReadSamples(string catalogFile) =>
        File.Exists(catalogFile)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogFile))!.Values.Distinct().Order().ToList()
            : Array.Empty<string>();

    private static string EventName(string path) => EventSuffix().Replace(Path.GetFileName(path.Replace("event:/", string.Empty)), string.Empty);

    [GeneratedRegex(@"\.(once\.)?ogg(2d)?(SG)?$|SG$")]
    private static partial Regex EventSuffix();

    /// <summary>Event name -> sample name, comparing names with punctuation and case removed.</summary>
    private sealed partial class SampleResolver
    {
        private readonly Dictionary<string, string> byKey = new();
        private readonly List<string> orbDrops = new();

        public SampleResolver(IEnumerable<string> samples)
        {
            foreach (var sample in samples.Where(s => s.EndsWith("_01", StringComparison.Ordinal)))
            {
                this.byKey.TryAdd(Key(sample[..^3]), sample);
                if (sample.StartsWith("OrbDrop_", StringComparison.Ordinal))
                {
                    this.orbDrops.Add(sample);
                }
            }
        }

        /// <summary>Only drop events: use, move, equip and interact sounds are not what lies on the ground.</summary>
        public string? Resolve(string eventPath)
        {
            var name = EventName(eventPath);
            if (UseLike().IsMatch(name))
            {
                return null;
            }

            var key = Key(name);
            if (this.byKey.TryGetValue(key, out var sample) || (key.StartsWith("four") && this.byKey.TryGetValue(key[4..], out sample)))
            {
                return sample;
            }

            // "Essence_Drop" -> "Item_Essence_Drop_01": the bank prefixes some events with Item/Items.
            return name.EndsWith("Drop", StringComparison.Ordinal)
                && this.byKey.TryGetValue("item" + key, out sample) ? sample
                : name.EndsWith("Drop", StringComparison.Ordinal) && this.byKey.TryGetValue("items" + key, out sample) ? sample : null;
        }

        /// <summary>"…/OrbUse_Exalted_AddModToRare.ogg2d" -> "OrbDrop_Exalt_01" (token equal, prefix or suffix).</summary>
        public string? ResolveOrbDrop(string useEventPath)
        {
            var match = OrbUse().Match(EventName(useEventPath));
            if (!match.Success)
            {
                return null;
            }

            var orb = match.Groups[1].Value.ToLowerInvariant();
            return this.orbDrops.FirstOrDefault(s =>
            {
                var core = s["OrbDrop_".Length..^3].ToLowerInvariant();
                return core == orb || core.StartsWith(orb) || orb.StartsWith(core) || core.EndsWith(orb);
            });
        }

        private static string Key(string name) => NonAlnum().Replace(name, string.Empty).ToLowerInvariant();

        [GeneratedRegex("[^A-Za-z0-9]")]
        private static partial Regex NonAlnum();

        [GeneratedRegex("(use|move|equip|interact)(_?\\d+)?$", RegexOptions.IgnoreCase)]
        private static partial Regex UseLike();

        [GeneratedRegex("^OrbUse_([A-Za-z0-9]+)_")]
        private static partial Regex OrbUse();
    }
}
