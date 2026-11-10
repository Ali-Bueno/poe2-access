using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The numeric columns of the telegraph tables (GeometryAttack, GeometryTrigger) and MonsterMortar's impact key, each
///     found by what it holds near where it was measured (<see cref="DatColumnSearch"/>), with the value of a known row
///     checked where the game's data has one. Nothing depends on a row size: a patch that adds a column moves the offsets
///     and they are found again. Pure.
/// </summary>
internal sealed class TelegraphColumns
{
    // Measured 2026-11-08 (docs/research/attack-telegraphs.md); where each search starts.
    internal const int GeometryRangeOffset = 0x24;
    internal const int GeometryShapeOffset = 0x28;
    internal const int GeometryLengthOffset = 0x30;
    internal const int GeometryWidthOffset = 0x34;
    internal const int GeometryAngleOffset = 0x71;
    internal const int GeometryDelayOffset = 0x7A;
    internal const int TriggerCountOffset = 0x24;
    internal const int TriggerDelayOffset = 0x28;
    internal const int TriggerRangeOffset = 0x34;
    internal const int TriggerRadiusOffset = 0x38;
    internal const int TriggerAngleOffset = 0x3C;
    internal const int MortarImpactOffset = 0x24;
    internal const int TriggerAnimationOffset = 0x14;
    private const string AoExtension = ".ao";
    private const int MinAnimations = 2;

    /// <summary>The game's units are small whole numbers (a cone of 10, a delay of 33 ms, 360 degrees); a column beyond this is not one of them.</summary>
    internal const int LargestValue = 100_000;

    /// <summary>GeometryAttack's shape codes: 1 circle, 2 cone, 3 rectangle, 4 donut, 5 triangle.</summary>
    internal const int MaxShape = 5;
    internal const int ConeShape = 2;
    internal const int RectangleShape = 3;

    // Rows the game's data gives known values for (the skills' ids, and what their rows hold on 2026-11-08).
    internal const string ConeSkill = "BalbalaAttack1TriggeredSlash1";
    internal const int ConeLength = 10;
    internal const int ConeAngle = 100;
    internal const int ConeDelay = 33;
    internal const int ConeRange = 85;
    internal const string RectangleSkill = "BearerOfPenitenceSlam";
    internal const int RectangleLength = 10;
    internal const int RectangleWidth = 20;
    internal const string TriggerSkill = "DeliriumBeamMonsterGTGeneric";
    internal const int TriggerCount = 1;
    internal const int TriggerRange = 45;
    internal const int TriggerRadius = 40;
    internal const int TriggerAngle = 360;

    public int GeometryRange, GeometryShape, GeometryLength, GeometryWidth, GeometryAngle, GeometryDelay;
    public int TriggerCountColumn, TriggerDelay, TriggerRangeColumn, TriggerRadiusColumn, TriggerAngleColumn, MortarImpact;

    /// <summary>The rows (in the geometry and trigger tables) of the skills with known values, -1 where the table has none.</summary>
    internal readonly record struct KnownRows(int Cone, int Rectangle, int Trigger);

    /// <summary>The columns, or null (said) when one was not found.</summary>
    public static TelegraphColumns? Find(DatTable geometry, DatTable triggers, DatTable mortars, DatTable animated, KnownRows known)
    {
        var search = new DatColumnSearch();
        var c = new TelegraphColumns();
        c.GeometryRange = Number(search, geometry, "GeometryAttack.Range", GeometryRangeOffset, -1, (known.Cone, ConeRange));
        c.GeometryShape = search.Find(geometry, "GeometryAttack.Shape", GeometryShapeOffset, offset => Plausible(geometry, offset, 1, MaxShape, requireTwo: true) && Holds(geometry, known.Cone, offset, ConeShape) && Holds(geometry, known.Rectangle, offset, RectangleShape), c.GeometryRange);
        c.GeometryLength = Number(search, geometry, "GeometryAttack.Length", GeometryLengthOffset, c.GeometryShape, (known.Cone, ConeLength), (known.Rectangle, RectangleLength));
        c.GeometryWidth = Number(search, geometry, "GeometryAttack.Width", GeometryWidthOffset, c.GeometryLength, (known.Rectangle, RectangleWidth));
        c.GeometryAngle = Number(search, geometry, "GeometryAttack.Angle", GeometryAngleOffset, c.GeometryWidth, (known.Cone, ConeAngle));
        c.GeometryDelay = Number(search, geometry, "GeometryAttack.Delay", GeometryDelayOffset, c.GeometryAngle, (known.Cone, ConeDelay));
        c.TriggerCountColumn = Number(search, triggers, "GeometryTrigger.Count", TriggerCountOffset, -1, (known.Trigger, TriggerCount));
        c.TriggerDelay = Number(search, triggers, "GeometryTrigger.Delay", TriggerDelayOffset, c.TriggerCountColumn);
        c.TriggerRangeColumn = Number(search, triggers, "GeometryTrigger.Range", TriggerRangeOffset, c.TriggerDelay, (known.Trigger, TriggerRange));
        c.TriggerRadiusColumn = Number(search, triggers, "GeometryTrigger.Radius", TriggerRadiusOffset, c.TriggerRangeColumn, (known.Trigger, TriggerRadius));
        c.TriggerAngleColumn = Number(search, triggers, "GeometryTrigger.Angle", TriggerAngleOffset, c.TriggerRadiusColumn, (known.Trigger, TriggerAngle));
        c.MortarImpact = search.Find(mortars, "MonsterMortar.Impact", MortarImpactOffset,
            offset => DatColumnSearch.IsKey(mortars, offset, animated.RowCount) && Enumerable.Range(0, mortars.RowCount).Select(row => mortars.Key(row, offset)).Distinct().Skip(1).Any());
        return search.Report("telegraphs") ? c : null;
    }

    /// <summary>
    ///     GeometryTrigger's animation key (a few rows have one): a key into the animations whose every filled row leads to an
    ///     .ao file, over more than one file. Unaligned offsets overlap the neighbouring keys' bytes, so the nearest fit wins.
    /// </summary>
    internal static int FindTriggerAnimation(DatTable triggers, DatTable animated, int aoFileColumn, DatColumnSearch search) =>
        search.Find(triggers, "GeometryTrigger.Animation", TriggerAnimationOffset, offset => IsAnimationKey(triggers, offset, animated, aoFileColumn));

    private static bool IsAnimationKey(DatTable triggers, int offset, DatTable animated, int aoFileColumn)
    {
        if (!DatColumnSearch.IsKey(triggers, offset, animated.RowCount))
        {
            return false;
        }

        var keys = Enumerable.Range(0, triggers.RowCount).Select(row => triggers.Key(row, offset)).Where(key => key >= 0).Distinct().ToList();
        return keys.Count >= MinAnimations && keys.All(key => animated.StringOrNull((int)key, aoFileColumn) is { } path && path.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The geometry and trigger rows of the skills whose values are known, found through the skills' ids.</summary>
    internal static KnownRows RowsOf(DatTable skills, int skillIdColumn, IReadOnlyDictionary<int, int> geometryIds, IReadOnlyDictionary<int, int> triggerIds)
    {
        int Through(string skill, IReadOnlyDictionary<int, int> ids) =>
            DatColumnSearch.RowOf(skills, skill) is var row and >= 0 && ids.TryGetValue(skills.Int32(row, skillIdColumn), out var target) ? target : -1;

        return new KnownRows(Through(ConeSkill, geometryIds), Through(RectangleSkill, geometryIds), Through(TriggerSkill, triggerIds));
    }

    private static int Number(DatColumnSearch search, DatTable table, string name, int measured, int after, params (int Row, int Value)[] known) =>
        search.Find(table, name, measured, offset => Plausible(table, offset, 0, LargestValue, requireTwo: false) && known.All(check => Holds(table, check.Row, offset, check.Value)), after);

    /// <summary>A 4-byte column whose every value lies in the range, and (when asked) that is not one constant.</summary>
    private static bool Plausible(DatTable table, int offset, int low, int high, bool requireTwo)
    {
        if (offset + sizeof(int) > table.RowSize)
        {
            return false;
        }

        var first = table.RowCount > 0 ? table.Int32(0, offset) : 0;
        var varies = false;
        for (var row = 0; row < table.RowCount; row++)
        {
            var value = table.Int32(row, offset);
            if (value < low || value > high)
            {
                return false;
            }

            varies |= value != first;
        }

        return table.RowCount > 0 && (varies || !requireTwo);
    }

    private static bool Holds(DatTable table, int row, int offset, int expected) => row < 0 || table.Int32(row, offset) == expected;
}
