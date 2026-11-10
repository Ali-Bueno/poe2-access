using System.Text.Json;
using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `item-sounds` command: maps every ground-item base type (BaseItemTypes.Id, the metadata path) to the drop sample
///     the game plays for it, written as { baseId: sampleName }. The chain, all English dat tables of Bundles2:
///     BaseItemTypes.SoundEffect (offset 88) and .ItemVisualIdentity (124) -> ItemVisualIdentity's two sound keys
///     (24 and 140) -> SoundEffects.SoundFile (8) = an FMOD event path such as
///     "Audio/Sound Effects/ItemSounds/4.0/Currency/Scrolls/Scroll_Drop.ogg". The offsets are where the columns were on
///     2026-11-10; they are used only while every one still is its kind and items whose sample is known still resolve to
///     it through them (<see cref="Known"/>), else found again by content and judged the same way, never trusted for a
///     row size. The event is resolved to a sample of ItemDrops.bank by name (event
///     "Belt_Drop" -> sample "Belt_Drop_01"; the numbered samples are variants of one sound). Orb rows carry only a use
///     event ("OrbUse_Chaos_..."), so they take the bank's "OrbDrop_Chaos_01". Read-only on the game; the `sounds`
///     command extracts the samples listed in the output.
/// </summary>
internal static partial class ItemSoundExtractor
{
    public const string Bank = "ItemDrops";

    private const int BaseSoundEffect = 88, BaseVisual = 124;
    private const int VisualInventorySound = 24, VisualDropSound = 140;
    private const int SoundFile = 8, SoundFile2D = 16;

    /// <summary>
    ///     Items whose drop sample is known (0.5.x, 2026-11-10): a scroll, an orb (through its use event) and an amulet
    ///     (through its visual identity), so each of the six columns is on the way to one of them.
    /// </summary>
    private static readonly (string Item, string Sample)[] Known =
    {
        ("Metadata/Items/Currency/CurrencyIdentification", "Scroll_Drop_01"),
        ("Metadata/Items/Currency/CurrencyRerollRare", "OrbDrop_Chaos_01"),
        ("Metadata/Items/Amulets/FourAmulet1", "Amulet_Drop_01"),
    };

    /// <summary>On an index already opened with its paths parsed (the batch command); the bank is read from the install.</summary>
    public static int Run(string gameDir, Index index, string outFile)
    {
        if (GameContent.ReadBank(gameDir, Bank) is not { } bank)
        {
            Say.Out.WriteLine($"Bank missing: FMOD/Desktop/{Bank}.bank in {gameDir}");
            return 1;
        }

        if (Fsb5.Problem(bank) is { } changed)
        {
            Say.Out.WriteLine($"FMOD/Desktop/{Bank}.bank is not the FMOD bank this reader knows: {changed}");
            return 1;
        }

        var items = DatTable.Open(index, "baseitemtypes");
        var visuals = DatTable.Open(index, "itemvisualidentity");
        var sounds = DatTable.Open(index, "soundeffects");
        if (items == null || visuals == null || sounds == null)
        {
            Say.Out.WriteLine("A dat table is missing.");
            return 1;
        }

        var resolver = new SampleResolver(Fsb5.Names(bank));
        if (!FindColumns(items, visuals, sounds, resolver, out var columns))
        {
            Say.Out.WriteLine("The sound columns were not found again (update the offsets from the dat schema).");
            return 1;
        }

        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var unresolved = new Dictionary<string, int>();
        var tables = new Tables(items, visuals, sounds, resolver);
        for (var row = 0; row < items.RowCount; row++)
        {
            var misses = new List<string>();
            var sample = SampleOf(tables, columns, row, misses);
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
        Say.Out.WriteLine($"Wrote {outFile}: {map.Count} base types -> {map.Values.Distinct().Count()} samples; "
            + $"{unresolved.Count} drop events without a sample in {Bank} (left {unresolved.Values.Sum()} base types silent)");
        foreach (var (name, count) in unresolved.OrderByDescending(p => p.Value).Take(15))
        {
            Say.Out.WriteLine($"  unresolved {count,4}  {name}");
        }

        return map.Count > 0 ? 0 : 1;
    }

    private sealed record Tables(DatTable Items, DatTable Visuals, DatTable Sounds, SampleResolver Resolver);

    /// <summary>The drop sample of a BaseItemTypes row through the six columns, or null; its unresolved events go to <paramref name="misses"/>.</summary>
    private static string? SampleOf(Tables t, (int BaseSound, int BaseVisual, int VisualInventory, int VisualDrop, int File, int File2D) c, int row, List<string>? misses = null)
    {
        var baseSound = Row(t.Items.Key(row, c.BaseSound), t.Sounds);
        var visual = Row(t.Items.Key(row, c.BaseVisual), t.Visuals);
        var candidates = new List<long> { baseSound };
        if (visual >= 0)
        {
            candidates.Add(Row(t.Visuals.Key((int)visual, c.VisualInventory), t.Sounds));
            candidates.Add(Row(t.Visuals.Key((int)visual, c.VisualDrop), t.Sounds));
        }

        // Orb drops: the row only carries the use event; the bank's matching drop sample is the cue.
        var sample = baseSound >= 0 ? t.Resolver.ResolveOrbDrop(t.Sounds.String((int)baseSound, c.File2D)) : null;
        foreach (var file in candidates.Where(candidate => candidate >= 0).Select(candidate => t.Sounds.String((int)candidate, c.File)))
        {
            if (sample != null || file.Length == 0)
            {
                continue;
            }

            sample = t.Resolver.Resolve(file);
            if (sample == null)
            {
                misses?.Add(EventName(file));
            }
        }

        return sample;
    }

    /// <summary>A key as a row of its target; -1 for a null key or one past the table.</summary>
    private static long Row(long key, DatTable target) => key >= 0 && key < target.RowCount ? key : -1;

    /// <summary>
    ///     The six columns: where they were measured while each still is its kind and the known items resolve through them;
    ///     otherwise found again by content (the sound paths are strings under "Audio/", a key column is the one whose keys
    ///     lead to the most item drop samples, which a random number column never does) and judged by the same known items.
    /// </summary>
    private static bool FindColumns(DatTable items, DatTable visuals, DatTable sounds, SampleResolver resolver,
        out (int BaseSound, int BaseVisual, int VisualInventory, int VisualDrop, int File, int File2D) columns)
    {
        static bool IsSoundPath(string text) => text.StartsWith("Audio/", StringComparison.OrdinalIgnoreCase);
        using var probe = DatTable.Probe();
        var tables = new Tables(items, visuals, sounds, resolver);
        var known = Known.Select(check => (check.Item, check.Sample, Row: DatColumnSearch.RowOf(items, check.Item)))
            .Where(check => check.Row >= 0 && resolver.Has(check.Sample)).ToList();
        foreach (var gone in Known.Where(check => known.All(kept => kept.Item != check.Item)))
        {
            Say.Out.WriteLine($"  item-sounds: known item {gone.Item} or its sample {gone.Sample} is gone, so it judges no column");
        }

        bool Holds((int BaseSound, int BaseVisual, int VisualInventory, int VisualDrop, int File, int File2D) c) =>
            known.All(check => SampleOf(tables, c, check.Row) == check.Sample);

        columns = (BaseSoundEffect, BaseVisual, VisualInventorySound, VisualDropSound, SoundFile, SoundFile2D);
        var measured = sounds.IsStringColumn(SoundFile, IsSoundPath) && sounds.IsStringColumn(SoundFile2D, IsSoundPath) &&
                       items.IsKeyColumn(BaseSoundEffect, sounds.RowCount) && items.IsKeyColumn(BaseVisual, visuals.RowCount) &&
                       visuals.IsKeyColumn(VisualInventorySound, sounds.RowCount) && visuals.IsKeyColumn(VisualDropSound, sounds.RowCount) && Holds(columns);
        if (!measured)
        {
            var paths = Enumerable.Range(1, Math.Max(0, sounds.RowSize - 8)).Where(offset => sounds.IsStringColumn(offset, IsSoundPath)).ToList();
            var (file, file2D) = paths.Count switch { 0 => (-1, -1), 1 => (paths[0], paths[0]), _ => (paths[0], paths[1]) };
            columns = (-1, -1, -1, -1, file, file2D);
            if (file < 0)
            {
                return false;
            }

            bool Drops(long sound) => resolver.Resolve(sounds.String((int)sound, file)) != null;
            bool BaseDrops(long sound) => Drops(sound) || resolver.ResolveOrbDrop(sounds.String((int)sound, file2D)) != null;
            var visualKeys = KeyColumns(visuals, sounds.RowCount, Drops, 2);
            bool VisualDrops(long visual) => visualKeys.Any(column => visuals.Key((int)visual, column) is >= 0 and var sound && Drops(sound));
            var baseSound = KeyColumns(items, sounds.RowCount, BaseDrops, 1);
            var baseVisual = visualKeys.Count == 0 ? new List<int>() : KeyColumns(items, visuals.RowCount, VisualDrops, 1);
            columns = (baseSound.FirstOrDefault(-1), baseVisual.FirstOrDefault(-1), visualKeys.FirstOrDefault(-1), visualKeys.LastOrDefault(-1), file, file2D);
            if (baseSound.Count != 1 || baseVisual.Count != 1 || visualKeys.Count == 0)
            {
                return false;
            }

            if (!Holds(columns))
            {
                Say.Out.WriteLine($"  item-sounds: the columns found by content (BaseItemTypes {columns.BaseSound}/{columns.BaseVisual}, ItemVisualIdentity {columns.VisualInventory}/{columns.VisualDrop}, " +
                                  $"SoundEffects {file}/{file2D}) do not give the known items their samples ({string.Join(", ", known.Select(check => $"{check.Item} = {check.Sample}"))})");
                return false;
            }
        }

        Say.Out.WriteLine($"Sound columns: BaseItemTypes {columns.BaseSound}/{columns.BaseVisual}, ItemVisualIdentity {columns.VisualInventory}/{columns.VisualDrop}, "
            + $"SoundEffects {columns.File}/{columns.File2D} ({(measured ? "as measured" : "found by content")}, {known.Count} known items resolve)");
        return true;
    }

    /// <summary>
    ///     The best scoring key columns into a table of <paramref name="targetRows"/> rows, at most <paramref name="count"/>, in row order.
    /// </summary>
    private static List<int> KeyColumns(DatTable table, int targetRows, Func<long, bool> good, int count)
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

        /// <summary>Whether the bank holds a sample by this name.</summary>
        public bool Has(string sample) => this.byKey.ContainsValue(sample) || this.orbDrops.Contains(sample);

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
