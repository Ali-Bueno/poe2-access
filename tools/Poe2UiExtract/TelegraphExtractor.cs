using System.Text;
using Index = LibBundle3.Index;

/// <summary>
///     `telegraphs` prototype: what the game draws before a monster skill lands, from the skill tables, so the mod can
///     warn when the warning is on screen (docs/research/attack-telegraphs.md). Monster skills are grouped by their
///     ActiveSkills kind; the kind's own table (found by the skill's SkillID, an i32 equal to the table's Id) gives the
///     danger area and the visual the client plays for it:
///     - geometry_attack / geometry_spell → GeometryAttack: shape, length (radius), width, cone angle, delay, range, visuals;
///     - geometry_trigger → GeometryTrigger: how many hits, after how long, spread and radius, the ground markers shown first;
///     - effect_driven_* → EffectDrivenSkill: the effect .ao whose EffectDrivenEvent block lists the shapes and their times;
///     - spawn_object → SpawnObject: the objects (.ot) the skill spawns, entities the mod sees by path;
///     - monster_mortar / geometry_mortars → MonsterMortar: the impact visual.
///     Writes telegraph-skills.txt (one line a skill), telegraph-effects.txt (one line an .ao the client may show for
///     one of them: its role, and its shapes with times where the file has an EffectDrivenEvent block) and
///     telegraph-monsters.txt (monster path → its skills with a line). Not wired into the batch yet.
/// </summary>
internal static class TelegraphExtractor
{
    public const string SkillsFile = "telegraph-skills.txt";
    public const string EffectsFile = "telegraph-effects.txt";
    public const string MonstersFile = "telegraph-monsters.txt";

    // GrantedEffects (0x129-byte rows, dat-schema poe2 2026-11-08): ActiveSkill key, SkillID (i32, unaligned).
    private const int SkillRowSize = 0x129;
    private const int SkillActiveSkillOffset = 0x4F;
    private const int SkillIdOffset = 0x9B;

    // GeometryAttack (0x152): Id, Animation list, then unnamed columns read by content (geom3.py, 2026-11-08):
    // shape 1-5, length or radius, width (rectangles and donuts), cone angle in degrees, delay in ms, range.
    private const int GeometryRowSize = 0x152;
    private const int GeometryAnimationOffset = 0x4;
    private const int GeometryShapeOffset = 0x28;
    private const int GeometryLengthOffset = 0x30;
    private const int GeometryWidthOffset = 0x34;
    private const int GeometryAngleOffset = 0x71;
    private const int GeometryDelayOffset = 0x7A;
    private const int GeometryRangeOffset = 0x24;
    private const int MaxGeometryShape = 5;

    // GeometryTrigger (0xEA): Id, GrantedEffect key, Animation, count, delay ms, spread, radius, angle, GroundMarker list.
    private const int TriggerRowSize = 0xEA;
    private const int TriggerAnimationOffset = 0x14;
    private const int TriggerCountOffset = 0x24;
    private const int TriggerDelayOffset = 0x28;
    private const int TriggerRangeOffset = 0x34;
    private const int TriggerRadiusOffset = 0x38;
    private const int TriggerAngleOffset = 0x3C;
    private const int TriggerMarkerOffset = 0x73;

    // SpawnObject (0x56): Id, MiscObjects list. EffectDrivenSkill (0x77): Id, Animation list. MonsterMortar (0x88): Id, ImpactAnimation.
    private const int SpawnRowSize = 0x56;
    private const int SpawnObjectsOffset = 0x4;
    private const int EffectRowSize = 0x77;
    private const int EffectAnimationOffset = 0x4;
    private const int MortarRowSize = 0x88;
    private const int MortarImpactOffset = 0x24;

    // MiscAnimated / MiscObjects: Id, file path. MonsterVarieties: Id, GrantedEffects list (found by content, as hit-times does).
    private const int FileOffset = 0x8;
    private const int MonsterSkillsOffset = 0xD0;
    private const string AoExtension = ".ao";

    public static int Run(string gameDir, string outDir)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outDir);
    }

    /// <summary>On an index already opened with its paths parsed.</summary>
    public static int Run(Index index, string outDir)
    {
        if (Sized(index, "grantedeffects", SkillRowSize) is not { } skills || Sized(index, "geometryattack", GeometryRowSize) is not { } geometry ||
            Sized(index, "geometrytrigger", TriggerRowSize) is not { } triggers || Sized(index, "spawnobject", SpawnRowSize) is not { } spawns ||
            Sized(index, "effectdrivenskill", EffectRowSize) is not { } effects || Sized(index, "monstermortar", MortarRowSize) is not { } mortars ||
            Sized(index, "activeskills") is not { } activeSkills || Sized(index, "miscanimated") is not { } animated ||
            Sized(index, "miscobjects") is not { } objects || Sized(index, "monstervarieties") is not { } monsters)
        {
            return 1;
        }

        var activeSkill = DatContentColumns.Nearest(skills, SkillActiveSkillOffset, offset => skills.IsKeyColumn(offset, activeSkills.RowCount));
        var aoFile = DatContentColumns.Nearest(animated, FileOffset, offset => animated.IsStringColumn(offset, text => text.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase)));
        var otFile = DatContentColumns.Nearest(objects, FileOffset, offset => objects.IsStringColumn(offset, text => text.StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase)));
        var monsterSkills = DatContentColumns.Nearest(monsters, MonsterSkillsOffset, offset => monsters.IsKeyListColumn(offset, skills.RowCount));
        var geometryAnimation = DatContentColumns.Nearest(geometry, GeometryAnimationOffset, offset => geometry.IsKeyListColumn(offset, animated.RowCount));
        var triggerMarkers = DatContentColumns.Nearest(triggers, TriggerMarkerOffset, offset => triggers.IsKeyListColumn(offset, animated.RowCount));
        var spawnObjects = DatContentColumns.Nearest(spawns, SpawnObjectsOffset, offset => spawns.IsKeyListColumn(offset, objects.RowCount));
        var effectAnimation = DatContentColumns.Nearest(effects, EffectAnimationOffset, offset => effects.IsKeyListColumn(offset, animated.RowCount));
        if (activeSkill == null || aoFile == null || otFile == null || monsterSkills == null || geometryAnimation == null || triggerMarkers == null || spawnObjects == null || effectAnimation == null)
        {
            Console.WriteLine("A list or path column of the telegraph tables was not found again (dat-strings, dat-schema).");
            return 1;
        }

        var kindOf = new Dictionary<int, string>();
        for (var row = 0; row < skills.RowCount; row++)
        {
            var active = skills.Key(row, activeSkill.Value);
            if (active >= 0 && active < activeSkills.RowCount && TelegraphShapes.KindOf(activeSkills.String((int)active, 0)) is { } kind)
            {
                kindOf[row] = kind;
            }
        }

        var geometryIds = Ids(geometry);
        var skillId = FindSkillIdColumn(skills, kindOf, geometryIds);
        if (skillId == null || !geometry.IsFlagLike(GeometryShapeOffset, MaxGeometryShape))
        {
            Console.WriteLine("GrantedEffects' SkillID or GeometryAttack's shape column was not found again (docs/research/attack-telegraphs.md).");
            return 1;
        }

        var tables = new Dictionary<string, Dictionary<int, int>>
        {
            ["geometry"] = geometryIds, ["trigger"] = Ids(triggers), ["spawn"] = Ids(spawns), ["effect"] = Ids(effects), ["mortar"] = Ids(mortars),
        };
        var files = index.Files.Values.Where(f => f.Path != null).GroupBy(f => f.Path!.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        var skillLines = new SortedSet<string>(StringComparer.Ordinal);
        var effectRoles = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        var tally = new Dictionary<string, int>(StringComparer.Ordinal);
        var withLine = new HashSet<int>();
        foreach (var (row, kind) in kindOf)
        {
            if (!tables[kind].TryGetValue(skills.Int32(row, skillId.Value), out var target))
            {
                Count(tally, $"{kind}: no table row");
                continue;
            }

            string area;
            var visuals = new List<string>();
            var spawned = new List<string>();
            switch (kind)
            {
                case "geometry":
                    area = TelegraphShapes.PairsOf(("shape", 0), ("length", geometry.Int32(target, GeometryLengthOffset)), ("width", geometry.Int32(target, GeometryWidthOffset)),
                        ("angle", geometry.Int32(target, GeometryAngleOffset)), ("delay", geometry.Int32(target, GeometryDelayOffset)), ("range", geometry.Int32(target, GeometryRangeOffset)));
                    area = $"shape={TelegraphShapes.GeometryShape(geometry.Int32(target, GeometryShapeOffset)) ?? "unknown"}" + (area.Length > 0 ? TelegraphShapes.PairSeparator + area : string.Empty);
                    visuals.AddRange(Paths(geometry, target, geometryAnimation.Value, animated, aoFile.Value));
                    break;
                case "trigger":
                    area = TelegraphShapes.PairsOf(("count", triggers.Int32(target, TriggerCountOffset)), ("delay", triggers.Int32(target, TriggerDelayOffset)), ("range", triggers.Int32(target, TriggerRangeOffset)),
                        ("radius", triggers.Int32(target, TriggerRadiusOffset)), ("angle", triggers.Int32(target, TriggerAngleOffset)));
                    visuals.AddRange(Paths(triggers, target, triggerMarkers.Value, animated, aoFile.Value).Select(path => path + ":marker"));
                    if (Path(triggers.Key(target, TriggerAnimationOffset), animated, aoFile.Value) is { } animation)
                    {
                        visuals.Add(animation);
                    }

                    break;
                case "effect":
                    area = string.Empty;
                    visuals.AddRange(Paths(effects, target, effectAnimation.Value, animated, aoFile.Value));
                    break;
                case "spawn":
                    area = string.Empty;
                    spawned.AddRange(Paths(spawns, target, spawnObjects.Value, objects, otFile.Value));
                    break;
                default:
                    area = string.Empty;
                    if (Path(mortars.Key(target, MortarImpactOffset), animated, aoFile.Value) is { } impact)
                    {
                        visuals.Add(impact + ":impact");
                    }

                    break;
            }

            withLine.Add(row);
            Count(tally, $"{kind}: with a line");
            Count(tally, visuals.Count > 0 ? $"{kind}: with a visual" : spawned.Count > 0 ? $"{kind}: with an object" : $"{kind}: area only");
            skillLines.Add(string.Join(TelegraphShapes.FieldSeparator, skills.String(row, 0), kind, area, string.Join(TelegraphShapes.ItemSeparator, visuals.Select(Plain)), string.Join(TelegraphShapes.ItemSeparator, spawned)));
            foreach (var visual in visuals)
            {
                var at = visual.LastIndexOf(':');
                var path = at > 0 ? visual[..at] : visual;
                var role = at > 0 ? visual[(at + 1)..] : kind == "effect" ? "effect" : "visual";
                if (!effectRoles.TryGetValue(path, out var roles))
                {
                    effectRoles[path] = roles = new SortedSet<string>(StringComparer.Ordinal);
                }

                roles.Add(role);
            }
        }

        var effectLines = new List<string>();
        var shaped = 0;
        foreach (var (path, roles) in effectRoles)
        {
            var text = files.TryGetValue(path.ToLowerInvariant(), out var file) ? Encoding.Unicode.GetString(file.Read().ToArray()).TrimStart('﻿') : null;
            var shapes = text == null ? Array.Empty<EffectShape>() : TelegraphShapes.ParseEffectDrivenEvents(text);
            shaped += shapes.Count > 0 ? 1 : 0;
            effectLines.Add(string.Join(TelegraphShapes.FieldSeparator, path, string.Join(TelegraphShapes.ItemSeparator, roles), string.Join(TelegraphShapes.ItemSeparator, shapes.Select(TelegraphShapes.Format))));
        }

        var monsterLines = new SortedSet<string>(StringComparer.Ordinal);
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var ids = monsters.KeyList(row, monsterSkills.Value).Where(key => key >= 0 && key < skills.RowCount && withLine.Contains((int)key)).Select(key => skills.String((int)key, 0)).ToList();
            if (ids.Count > 0 && monsters.StringOrNull(row, 0) is { Length: > 0 } id)
            {
                monsterLines.Add($"{id}{TelegraphShapes.FieldSeparator}{string.Join(TelegraphShapes.ItemSeparator, ids)}");
            }
        }

        Directory.CreateDirectory(outDir);
        File.WriteAllLines(System.IO.Path.Combine(outDir, SkillsFile), new[]
        {
            "# Monster skills whose tables describe the danger area the client draws (tools/Poe2UiExtract telegraphs, prototype).",
            "# skill id|kind|area (shape=circle|cone|rectangle|donut; length or radius, width, angle in degrees, delay ms, range, count)|visual .ao files|spawned objects",
        }.Concat(skillLines));
        File.WriteAllLines(System.IO.Path.Combine(outDir, EffectsFile), new[]
        {
            "# Effect files the client plays for those skills: a ServerEffect entity's .ao names one of them.",
            "# .ao path|roles (visual, marker, impact, effect)|shapes of its EffectDrivenEvent block as shape:ms from start:length:width:angle",
        }.Concat(effectLines));
        File.WriteAllLines(System.IO.Path.Combine(outDir, MonstersFile), new[] { "# monster path|its skills with a line in " + SkillsFile }.Concat(monsterLines));
        Console.WriteLine($"Wrote {outDir}: {skillLines.Count} skills, {effectLines.Count} effect files ({shaped} with shapes), {monsterLines.Count} monsters.");
        foreach (var (what, count) in tally.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {what}: {count}");
        }

        return 0;
    }

    /// <summary>
    ///     GrantedEffects' SkillID: the i32 column nearest to where it was measured whose value, for every geometry skill,
    ///     is the Id of a GeometryAttack row (1132 of 1132 on 2026-11-08). The column sits at an odd offset, so every
    ///     byte offset is tried.
    /// </summary>
    internal static int? FindSkillIdColumn(DatTable skills, IReadOnlyDictionary<int, string> kindOf, IReadOnlyDictionary<int, int> geometryIds)
    {
        var geometrySkills = kindOf.Where(pair => pair.Value == "geometry").Select(pair => pair.Key).ToList();
        if (geometrySkills.Count < DatMinRows)
        {
            return null;
        }

        return DatContentColumns.Nearest(skills, SkillIdOffset, offset => offset + 4 <= skills.RowSize && geometrySkills.All(row => geometryIds.ContainsKey(skills.Int32(row, offset))));
    }

    private const int DatMinRows = 50;

    /// <summary>A table's Id (i32 at +0) → row.</summary>
    private static Dictionary<int, int> Ids(DatTable table)
    {
        var ids = new Dictionary<int, int>();
        for (var row = 0; row < table.RowCount; row++)
        {
            ids.TryAdd(table.Int32(row, 0), row);
        }

        return ids;
    }

    private static IEnumerable<string> Paths(DatTable table, int row, int listOffset, DatTable target, int pathOffset) =>
        table.KeyList(row, listOffset).Select(key => Path(key, target, pathOffset)).Where(path => path != null)!;

    private static string? Path(long key, DatTable target, int pathOffset) =>
        key >= 0 && key < target.RowCount && target.StringOrNull((int)key, pathOffset) is { Length: > 0 } path ? path : null;

    private static string Plain(string visual) => visual.LastIndexOf(':') is var at and > 0 ? visual[..at] : visual;

    private static void Count(Dictionary<string, int> tally, string what) => tally[what] = tally.GetValueOrDefault(what) + 1;

    /// <summary>A table whose rows still have the size its offsets were read at (any size when none is given); null, and said, otherwise.</summary>
    private static DatTable? Sized(Index index, string name, int rowSize = 0)
    {
        var table = DatTable.Open(index, name);
        if (table != null && (rowSize == 0 || table.RowSize == rowSize))
        {
            return table;
        }

        Console.WriteLine($"{name} {(table == null ? "not found" : $"rows are 0x{table.RowSize:X} bytes, not 0x{rowSize:X}")}: telegraphs left out; find the columns again (dat-schema poe2).");
        return null;
    }
}

internal static class TelegraphTableChecks
{
    /// <summary>An i32 column holding only values 1..max (GeometryAttack's shape).</summary>
    public static bool IsFlagLike(this DatTable table, int offset, int max)
    {
        for (var row = 0; row < table.RowCount; row++)
        {
            var value = table.Int32(row, offset);
            if (value < 1 || value > max)
            {
                return false;
            }
        }

        return table.RowCount > 0;
    }
}
