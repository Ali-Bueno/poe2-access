using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns a rune's or soul core's stats are read through, for the `mod-stats` catalog's "runes"
///     (docs/research/item-mods.md, "Sockets and runes"): SoulCores keys its base item, and SoulCoreStats holds one row a
///     core and a category of item with the stats it gives there and one fixed value each (a range is two stats).
///     Measured 2026-11-16 (dat-kinds, 0.5.x): SoulCores +0x00 into BaseItemTypes; SoulCoreStats +0x00 into SoulCores,
///     +0x10 into SoulCoreStatCategories, +0x20 a list of keys into Stats and +0x30 their values. Proven on the Lesser Iron
///     Rune, whose Martial Weapon row gives `local_physical_damage_+%` 14, the line a spear with it socketed shows and its
///     Mods lists lack. A second pair of lists (+0x40, +0x50) matched no tooltip and is not read. Pure.
/// </summary>
internal sealed class RuneStatColumns
{
    // Measured offsets, where each search starts.
    internal const int BaseOffset = 0x00, CoreOffset = 0x00, CategoryOffset = 0x10, StatsOffset = 0x20, ValuesOffset = 0x30;

    // The rune set against a tooltip: Chainsting with it socketed showed "Daño físico aumentado un 14%" (2026-11-16).
    internal const string KnownRune = "Metadata/Items/SoulCores/RuneEnhanceLesser", KnownStat = "local_physical_damage_+%";
    internal const int KnownValue = 14;

    public int Base { get; private init; }

    public int Core { get; private init; }

    public int Category { get; private init; }

    public int Stats { get; private init; }

    public int Values { get; private init; }

    /// <summary>The columns, or null (said) when one was not found or the known rune does not read as its tooltip showed.</summary>
    public static RuneStatColumns? Find(DatTable cores, DatTable coreStats, DatTable categories, DatTable bases, DatTable stats, string step)
    {
        var search = new DatColumnSearch();
        var (rune, stat) = (DatAnchors.Row(bases, KnownRune, "SoulCores' base"), DatAnchors.Row(stats, KnownStat, "SoulCoreStats' stats"));
        var baseColumn = search.Key(cores, "SoulCores.BaseItem", BaseOffset, bases);
        var core = baseColumn < 0 || rune < 0 ? -1 : DatAnchors.RowKeyed(cores, baseColumn, rune);
        var coreColumn = search.Key(coreStats, "SoulCoreStats.SoulCore", CoreOffset, cores);
        var category = search.Key(coreStats, "SoulCoreStats.Category", CategoryOffset, categories, after: coreColumn);
        var statList = search.KeyList(coreStats, "SoulCoreStats.Stats", StatsOffset, stats, after: category);
        using var probe = DatTable.Probe();
        var values = statList < 0 ? -1 : search.Find(coreStats, "SoulCoreStats.Values", ValuesOffset, offset => offset != statList &&
            Enumerable.Range(0, coreStats.RowCount).All(row => coreStats.Int32List(row, offset).Count == coreStats.KeyList(row, statList).Count), statList);
        if (!search.Report(step))
        {
            return null;
        }

        var columns = new RuneStatColumns { Base = baseColumn, Core = coreColumn, Category = category, Stats = statList, Values = values };
        if (core >= 0 && stat >= 0 &&
            !Enumerable.Range(0, coreStats.RowCount).Any(row => coreStats.Key(row, coreColumn) == core && columns.Read(coreStats, stats, row) is [var only] && only == (KnownStat, KnownValue)))
        {
            Say.Out.WriteLine($"  {KnownRune} gives no row of {KnownStat} {KnownValue}: the runes are left out");
            return null;
        }

        return columns;
    }

    /// <summary>One row's stats and values, in order; empty when a key is no stat.</summary>
    public IReadOnlyList<(string Stat, int Value)> Read(DatTable coreStats, DatTable stats, int row)
    {
        var (keys, numbers) = (coreStats.KeyList(row, this.Stats), coreStats.Int32List(row, this.Values));
        var read = new List<(string Stat, int Value)>();
        for (var at = 0; at < keys.Count && at < numbers.Count; at++)
        {
            if (DatAnchors.IdOf(stats, keys[at]) is not { Length: > 0 } id)
            {
                return Array.Empty<(string, int)>();
            }

            read.Add((id, numbers[at]));
        }

        return read;
    }

    /// <summary>
    ///     Every core's rows: its base item's path, then for each category row of SoulCoreStatCategories "stat value …".
    ///     A core is left out when its base is unknown or a row of it does not read.
    /// </summary>
    public SortedDictionary<string, SortedDictionary<string, string>> All(DatTable cores, DatTable coreStats, DatTable bases, DatTable stats)
    {
        var all = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        for (var row = 0; row < coreStats.RowCount; row++)
        {
            var (core, category) = (coreStats.Key(row, this.Core), coreStats.Key(row, this.Category));
            if (core < 0 || core >= cores.RowCount || category < 0 || DatAnchors.IdOf(bases, cores.Key((int)core, this.Base)) is not { Length: > 0 } path ||
                this.Read(coreStats, stats, row) is not { Count: > 0 } read)
            {
                continue;
            }

            if (!all.TryGetValue(path, out var rows))
            {
                all[path] = rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            }

            // Two rows of one core and category would be summed by nobody knows what: the second is kept apart.
            var key = category.ToString(System.Globalization.CultureInfo.InvariantCulture);
            rows[rows.ContainsKey(key) ? $"{key}#{row}" : key] = string.Join(' ', read.Select(stat => $"{stat.Stat} {stat.Value}"));
        }

        return all;
    }
}
