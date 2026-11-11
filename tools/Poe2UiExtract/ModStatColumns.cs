using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns the `mod-stats` catalog is read through (Mods and Stats; docs/research/item-mods.md, "Mods.dat"): the
///     run of keys into Stats a modifier's values follow, the least and most each rolls, and every other column that
///     reads as a key into Stats, whose place among the values nobody has seen. Found near where measured (dat-kinds and
///     dat-keys, 0.5.x, 2026-11-10) by kind and by two modifiers whose stats and rolls the game's tooltips showed. Pure.
/// </summary>
internal sealed class ModStatColumns
{
    internal const string Step = "mod-stats";

    // Measured offsets, where each search starts.
    internal const int FirstStatOffset = 0x1E, RangesOffset = 0x7E;

    internal const int KeyBytes = 2 * sizeof(long), RangeBytes = 2 * sizeof(int);

    // Modifiers of the items set against their tooltips (docs/research/evidence/item-mods-2026-11-10.txt): the stat each
    // line was written from, the values rolled, and the hybrid's two ranges as dat-kinds read them.
    internal const string KnownLife = "IncreasedLife3", LifeStat = "base_maximum_life";
    internal const int KnownLifeRoll = 32;
    internal const string KnownHybrid = "LocalIncreasedArmourAndLife2", ArmourStat = "local_physical_damage_reduction_rating_+%";
    internal const int KnownHybridArmourRoll = 16, KnownHybridLifeRoll = 15;
    internal static readonly IReadOnlyList<int> KnownHybridRanges = new[] { 14, 20, 11, 19 };

    /// <summary>The stat columns in the order a modifier's values follow.</summary>
    public IReadOnlyList<int> Stats { get; private init; } = Array.Empty<int>();

    /// <summary>Where the first stat's least and most are; each next stat's follow.</summary>
    public int Ranges { get; private init; }

    /// <summary>The other columns that read as a key into Stats with some row empty: a modifier with one filled is not told apart.</summary>
    public IReadOnlyList<int> Others { get; private init; } = Array.Empty<int>();

    /// <summary>The columns after the run that key BuffTemplates, not Stats (<see cref="IsBuffColumn"/>): no value of the modifier is theirs.</summary>
    public IReadOnlyList<int> Buffs { get; private init; } = Array.Empty<int>();

    /// <summary>
    ///     Whether a column after the run keys BuffTemplates rather than Stats: every filled row's key is one of its rows and
    ///     most name the modifier's own Id. 0.5.x, 2026-11-10: +0x25C, all 265 filled keys under BuffTemplates' 665 rows (Stats
    ///     has 27,299), 206 naming their own modifier, LocalDisplayYouAndNearbyAlliesHaveIncreasedItemRarityUnique__1 among them.
    /// </summary>
    internal static bool IsBuffColumn(DatTable mods, DatTable buffs, int offset)
    {
        using var probe = DatTable.Probe();
        if (!DatColumnSearch.IsKey(mods, offset, buffs.RowCount, wide: true))
        {
            return false;
        }

        var (filled, own) = (0, 0);
        for (var row = 0; row < mods.RowCount; row++)
        {
            if (mods.Key(row, offset) is >= 0 and var key)
            {
                filled++;
                own += DatAnchors.IdOf(buffs, key) is { Length: > 0 } id && id == mods.StringOrNull(row, DatColumnSearch.IdColumn) ? 1 : 0;
            }
        }

        return own * 2 > filled;
    }

    /// <summary>The columns, or null (said) when one was not found; <paramref name="buffs"/> (BuffTemplates) tells its key columns from the stats'.</summary>
    public static ModStatColumns? Find(DatTable mods, DatTable stats, DatTable? buffs = null)
    {
        var search = new DatColumnSearch();
        var (life, hybrid) = (DatAnchors.Row(mods, KnownLife, "Mods' stats"), DatAnchors.Row(mods, KnownHybrid, "Mods' stats and ranges"));
        var (lifeStat, armourStat) = (DatAnchors.Row(stats, LifeStat, "Mods' stats"), DatAnchors.Row(stats, ArmourStat, "Mods' stats"));
        using var probe = DatTable.Probe();
        bool IsStat(int offset) => DatColumnSearch.IsKey(mods, offset, stats.RowCount, wide: true);
        var first = search.Find(mods, "Mods.Stat1", FirstStatOffset, offset => IsStat(offset) &&
            (life < 0 || lifeStat < 0 || mods.Key(life, offset) == lifeStat) &&
            (hybrid < 0 || lifeStat < 0 || armourStat < 0 || (mods.Key(hybrid, offset) == armourStat && IsStat(offset + KeyBytes) && mods.Key(hybrid, offset + KeyBytes) == lifeStat)));
        if (first < 0)
        {
            search.Report(Step);
            return null;
        }

        var run = new List<int>();
        for (var offset = first; IsStat(offset); offset += KeyBytes)
        {
            run.Add(offset);
        }

        var end = run[^1] + KeyBytes;
        var ranges = search.Find(mods, "Mods.StatRanges", RangesOffset, offset => IsRanges(mods, run, offset, hybrid), end - 1);
        var keys = Enumerable.Range(end, Math.Max(0, mods.RowSize - end)).Where(offset => IsStat(offset) && Enumerable.Range(0, mods.RowCount).Any(row => mods.Key(row, offset) < 0)).ToList();
        var buffColumns = buffs == null ? new List<int>() : keys.Where(offset => IsBuffColumn(mods, buffs, offset)).ToList();
        return search.Report(Step) ? new ModStatColumns { Stats = run, Ranges = ranges, Others = keys.Except(buffColumns).ToList(), Buffs = buffColumns } : null;
    }

    /// <summary>Of this many rows, fewer than one may roll a number where its stat column is empty (78 of 16,911 do, 0.5.x).</summary>
    internal const int RollsWithoutStatPer = 100;

    /// <summary>A least and a most per stat column: nothing where the column is empty on nearly every row, and the known hybrid's own.</summary>
    private static bool IsRanges(DatTable mods, IReadOnlyList<int> run, int offset, int hybrid)
    {
        if (offset + (run.Count * RangeBytes) > mods.RowSize ||
            (hybrid >= 0 && KnownHybridRanges.Where((value, at) => mods.Int32(hybrid, offset + (at * sizeof(int))) != value).Any()))
        {
            return false;
        }

        var stray = Enumerable.Range(0, mods.RowCount).Count(row => RollsWithoutStat(mods, run, offset, row));
        return stray * RollsWithoutStatPer < mods.RowCount;
    }

    /// <summary>Whether the row rolls a number at a stat column it leaves empty.</summary>
    private static bool RollsWithoutStat(DatTable mods, IReadOnlyList<int> run, int ranges, int row) =>
        run.Where((column, stat) => mods.Key(row, column) < 0 && (mods.Int32(row, ranges + (stat * RangeBytes)) != 0 || mods.Int32(row, ranges + (stat * RangeBytes) + sizeof(int)) != 0)).Any();

    /// <summary>One modifier's stats with what each rolls, in the order its values follow.</summary>
    /// <param name="Proven">Whether the values can be told apart: the stats fill the run from its start and no other column holds one.</param>
    public sealed record Row(IReadOnlyList<(string Stat, int Least, int Most)> Stats, bool Proven);

    /// <summary>The stats of one row of Mods.</summary>
    public Row Read(DatTable mods, DatTable stats, int row)
    {
        var found = new List<(string Stat, int Least, int Most)>();
        // A number rolled where no stat is: the modifier may hold a value for it.
        var (proven, ended) = (!RollsWithoutStat(mods, this.Stats, this.Ranges, row), false);
        for (var stat = 0; stat < this.Stats.Count; stat++)
        {
            if (DatAnchors.IdOf(stats, mods.Key(row, this.Stats[stat])) is not { Length: > 0 } id)
            {
                ended = true;
                continue;
            }

            // A stat after an empty column: whether its value keeps the column's place is not known.
            proven &= !ended;
            var at = this.Ranges + (stat * RangeBytes);
            var (one, other) = (mods.Int32(row, at), mods.Int32(row, at + sizeof(int)));
            found.Add((id, Math.Min(one, other), Math.Max(one, other)));
        }

        foreach (var offset in this.Others)
        {
            if (DatAnchors.IdOf(stats, mods.Key(row, offset)) is { Length: > 0 } id)
            {
                proven = false;
                found.Add((id, 0, 0));
            }
        }

        return new Row(found, proven);
    }

    /// <summary>Why the columns found must not make a catalog, or null: a known modifier reading other stats, or its roll outside what the row says it rolls.</summary>
    public string? Problem(DatTable mods, DatTable stats)
    {
        static bool Holds((string Stat, int Least, int Most) stat, string id, int roll) => stat.Stat == id && roll >= stat.Least && roll <= stat.Most;
        if (DatColumnSearch.RowOf(mods, KnownLife) is >= 0 and var life && this.Read(mods, stats, life) is var one &&
            !(one.Proven && one.Stats.Count == 1 && Holds(one.Stats[0], LifeStat, KnownLifeRoll)))
        {
            return $"{KnownLife} no longer reads as {LifeStat} rolling {KnownLifeRoll} (now: {Written(one)})";
        }

        if (DatColumnSearch.RowOf(mods, KnownHybrid) is >= 0 and var hybrid && this.Read(mods, stats, hybrid) is var two &&
            !(two.Proven && two.Stats.Count == 2 && Holds(two.Stats[0], ArmourStat, KnownHybridArmourRoll) && Holds(two.Stats[1], LifeStat, KnownHybridLifeRoll)))
        {
            return $"{KnownHybrid} no longer reads as {ArmourStat} rolling {KnownHybridArmourRoll} and {LifeStat} rolling {KnownHybridLifeRoll} (now: {Written(two)})";
        }

        return null;
    }

    /// <summary>A row as the catalog holds it: "stat least most" per stat when its values are told apart, the stats alone when not.</summary>
    public static string Written(Row row) =>
        string.Join(' ', row.Stats.Select(stat => row.Proven ? $"{stat.Stat} {stat.Least} {stat.Most}" : stat.Stat));
}
