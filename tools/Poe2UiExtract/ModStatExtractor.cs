using Poe2Accessibility.Services;
using Index = LibBundle3.Index;

/// <summary>
///     `mod-stats` command: what each modifier of the game gives, so an item's modifier lines can be written from the
///     modifiers it holds in memory, without its tooltip (docs/research/item-mods.md):
///     { "source", "hidden": [ stats the description file says have no line ], "mods": { Mods Id: "stat least most …" in
///     the order the modifier's values follow }, "unproven": { Mods Id: "stat …" for a modifier whose values cannot be
///     told apart } }. The lines themselves stay in mod-texts.json, found there by stat. The columns are
///     <see cref="ModStatColumns"/>'; the step fails on a handler <see cref="StatHandlers"/> does not know. Read-only on the game.
/// </summary>
internal static class ModStatExtractor
{
    private const string Step = ModStatColumns.Step;

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var (mods, stats, buffs) = (DatTable.Open(index, "mods"), DatTable.Open(index, "stats"), DatTable.Open(index, "bufftemplates"));
        if (mods == null || stats == null || buffs == null || BundleFiles.Find(index, ModTextExtractor.Source) is not { } file)
        {
            Say.Out.WriteLine($"Mods, Stats, BuffTemplates or {ModTextExtractor.Source} is missing: {Step} left out.");
            return 1;
        }

        var parsed = StatDescriptionFile.Parse(file.ReadText());
        if (ModTextExtractor.Problem(parsed) is { } unread)
        {
            Say.Out.WriteLine($"  {unread}, {Step} keeps its previous catalog");
            return 1;
        }

        var handlers = Handlers(parsed);
        Say.Out.WriteLine($"  {ModTextExtractor.Source}: {handlers.Count} handlers: {string.Join(", ", handlers.Select(pair => $"{pair.Key} {pair.Value}"))}");
        if (handlers.Keys.Where(name => !StatHandlers.IsKnown(name)).ToList() is { Count: > 0 } unknown)
        {
            Say.Out.WriteLine($"  handlers the mod does not know what to do with: {string.Join(", ", unknown)}: {Step} keeps its previous catalog");
            return 1;
        }

        if (ModStatColumns.Find(mods, stats, buffs) is not { } columns)
        {
            return 1;
        }

        Say.Out.WriteLine($"  Mods: {columns.Stats.Count} stat columns from +0x{columns.Stats[0]:X}, ranges at +0x{columns.Ranges:X}, other keys into Stats at {string.Join(", ", columns.Others.Select(offset => $"+0x{offset:X}"))}" +
                          $", keys into BuffTemplates at {string.Join(", ", columns.Buffs.Select(offset => $"+0x{offset:X}"))}");
        if (columns.Problem(mods, stats) is { } problem)
        {
            Say.Out.WriteLine($"  {problem}, {Step} keeps its previous catalog");
            return 1;
        }

        var (proven, unproven) = (new SortedDictionary<string, string>(StringComparer.Ordinal), new SortedDictionary<string, string>(StringComparer.Ordinal));
        var (repeated, used) = (0, new HashSet<string>(StringComparer.Ordinal));
        for (var row = 0; row < mods.RowCount; row++)
        {
            if (mods.StringOrNull(row, DatColumnSearch.IdColumn) is not { Length: > 0 } id)
            {
                continue;
            }

            if (proven.ContainsKey(id) || unproven.ContainsKey(id))
            {
                repeated++; // two rows with one Id: which one an item holds is not known
                proven.Remove(id);
                unproven[id] = string.Empty;
                continue;
            }

            var read = columns.Read(mods, stats, row);
            (read.Proven ? proven : unproven)[id] = ModStatColumns.Written(read);
            used.UnionWith(read.Stats.Select(stat => stat.Stat));
        }

        var described = parsed.Descriptions.SelectMany(description => description.Stats).ToHashSet(StringComparer.Ordinal);
        var hidden = parsed.Hidden.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var silent = used.Count(stat => !described.Contains(stat) && !hidden.Contains(stat));
        Say.Out.WriteLine($"  {used.Count} stats in use: {used.Count(described.Contains)} described, {used.Count(hidden.Contains)} said to have no description, {silent} the file does not mention; {repeated} Ids on more than one row");
        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["source"] = $"{mods.Name}, {stats.Name}, {ModTextExtractor.Source}", ["hidden"] = hidden, ["mods"] = proven, ["unproven"] = unproven,
        });
        Say.Out.WriteLine($"Wrote {outFile}: {proven.Count} modifiers, {unproven.Count} whose values are not told apart, {hidden.Count} hidden stats");
        return proven.Count > 0 ? 0 : 1;
    }

    /// <summary>Every handler name the file's lines use, with how many lines use it.</summary>
    internal static SortedDictionary<string, int> Handlers(StatDescriptionFile.Parsed parsed)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var name in parsed.Descriptions.SelectMany(description => description.Texts.Values).SelectMany(lines => lines).SelectMany(StatHandlers.NamesIn))
        {
            counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        return counts;
    }
}
