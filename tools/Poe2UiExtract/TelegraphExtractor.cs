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
///     telegraph-monsters.txt (monster path → its skills with a line). The batch runs the skills and the effects files as two commands (RunSkills, RunEffects), each from its own read of the tables; only skills a MonsterVarieties row grants are kept.
/// </summary>
internal static class TelegraphExtractor
{
    public const string SkillsFile = "telegraph-skills.txt";
    public const string EffectsFile = "telegraph-effects.txt";
    public const string MonstersFile = "telegraph-monsters.txt";

    // GrantedEffects: ActiveSkill key and SkillID (i32, unaligned), measured offsets where the searches start; the numeric columns of
    // GeometryAttack and GeometryTrigger are in TelegraphColumns. The other tables' lists start at +0x4 (Id first).
    private const int SkillActiveSkillOffset = 0x4F;
    private const int SkillIdOffset = 0x9B;
    private const int GeometryAnimationOffset = 0x4;
    private const int TriggerMarkerOffset = 0x73;
    private const int SpawnObjectsOffset = 0x4;
    private const int EffectAnimationOffset = 0x4;
    // MiscAnimated / MiscObjects: Id, file path. MonsterVarieties: Id, GrantedEffects list (found by content, as hit-times does).
    private const int FileOffset = 0x8;
    private const int MonsterSkillsOffset = 0xD0;
    private const string AoExtension = ".ao";

    /// <summary>
    ///     Skills whose lines are known (0.5.x, 2026-11-10): each its kind and a file its line names, so every list and path
    ///     column the line is read through is the one that held it then, not another of its type.
    /// </summary>
    private static readonly (string Key, string[] Holds)[] KnownLines =
    {
        ("BearerOfPenitenceSlam", new[] { "|geometry|", "penitence_slam.ao" }),
        ("FlankSpawnMarker", new[] { "|spawn|", "PathMarkers/FlankMarker" }),
        ("DeliriumBossPhysMortarSpray", new[] { "|mortar|", "vomitMortarImpact.ao" }),
        ("BoneCultistZealotFirestorm", new[] { "|effect|", "meteor_01.ao" }),
        ("GTAtziriStormCall", new[] { "|trigger|", "stormcall_marker.ao" }),
    };

    private const string SkillsHeader1 = "# Monster skills whose tables describe the danger area the client draws (tools/Poe2UiExtract telegraph-skills), regenerated after patches.";
    private const string SkillsHeader2 = "# skill id|kind|area (shape=circle|cone|rectangle|donut; length or radius, width, angle in degrees, delay ms, range, count)|visual .ao files|spawned objects";
    private const string EffectsHeader1 = "# Effect files the client plays for those skills: a ServerEffect entity's .ao names one of them (tools/Poe2UiExtract telegraph-effects).";
    private const string EffectsHeader2 = "# .ao path|roles (visual, marker, impact, effect)|shapes of its EffectDrivenEvent block as shape:ms from start:length:width:angle";

    /// <summary>The three files' lines, header apart.</summary>
    internal sealed record Lines(IReadOnlyCollection<string> Skills, IReadOnlyCollection<string> Effects, IReadOnlyCollection<string> Monsters);

    /// <summary>Writes the three files into a directory.</summary>
    public static int Run(Index index, string outDir)
    {
        if (Build(index) is not { } lines)
        {
            return 1;
        }

        Directory.CreateDirectory(outDir);
        File.WriteAllLines(System.IO.Path.Combine(outDir, SkillsFile), new[] { SkillsHeader1, SkillsHeader2 }.Concat(lines.Skills));
        File.WriteAllLines(System.IO.Path.Combine(outDir, EffectsFile), new[] { EffectsHeader1, EffectsHeader2 }.Concat(lines.Effects));
        File.WriteAllLines(System.IO.Path.Combine(outDir, MonstersFile), new[] { "# monster path|its skills with a line in " + SkillsFile }.Concat(lines.Monsters));
        return 0;
    }

    /// <summary>`telegraph-skills` batch command: the skills file alone (the batch makes one output per command).</summary>
    public static int RunSkills(Index index, string outFile) => RunOne(index, outFile, lines => new[] { SkillsHeader1, SkillsHeader2 }.Concat(lines.Skills));

    /// <summary>`telegraph-effects` batch command: the effects file alone.</summary>
    public static int RunEffects(Index index, string outFile) => RunOne(index, outFile, lines => new[] { EffectsHeader1, EffectsHeader2 }.Concat(lines.Effects));

    private static int RunOne(Index index, string outFile, Func<Lines, IEnumerable<string>> content)
    {
        if (Build(index) is not { } lines)
        {
            return 1;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, content(lines));
        return 0;
    }

    /// <summary>Reads the tables; null, and said, when one moved. Only skills some MonsterVarieties row grants are kept.</summary>
    internal static Lines? Build(Index index)
    {
        if (Sized(index, "grantedeffects") is not { } skills || Sized(index, "geometryattack") is not { } geometry ||
            Sized(index, "geometrytrigger") is not { } triggers || Sized(index, "spawnobject") is not { } spawns ||
            Sized(index, "effectdrivenskill") is not { } effects || Sized(index, "monstermortar") is not { } mortars ||
            Sized(index, "activeskills") is not { } activeSkills || Sized(index, "miscanimated") is not { } animated ||
            Sized(index, "miscobjects") is not { } objects || Sized(index, "monstervarieties") is not { } monsters)
        {
            return null;
        }

        var activeSkill = DatContentColumns.Nearest(skills, SkillActiveSkillOffset, offset => skills.IsKeyColumn(offset, activeSkills.RowCount));
        var aoFile = DatContentColumns.Nearest(animated, FileOffset, offset => animated.IsStringColumn(offset, text => text.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase)));
        var otFile = DatContentColumns.Nearest(objects, FileOffset, offset => objects.IsStringColumn(offset, text => text.StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase)));
        var urchin = DatAnchors.Row(monsters, DatAnchors.Monster, "MonsterVarieties' skills");
        var sling = DatAnchors.Row(skills, DatAnchors.MonsterSkill, "MonsterVarieties' skills");
        var monsterSkills = DatContentColumns.Nearest(monsters, MonsterSkillsOffset, offset => monsters.IsKeyListColumn(offset, skills.RowCount) && DatAnchors.GrantsOwnSkill(monsters, offset, urchin, sling));
        var geometryAnimation = DatContentColumns.Nearest(geometry, GeometryAnimationOffset, offset => geometry.IsKeyListColumn(offset, animated.RowCount));
        var triggerMarkers = DatContentColumns.Nearest(triggers, TriggerMarkerOffset, offset => triggers.IsKeyListColumn(offset, animated.RowCount));
        var spawnObjects = DatContentColumns.Nearest(spawns, SpawnObjectsOffset, offset => spawns.IsKeyListColumn(offset, objects.RowCount));
        var effectAnimation = DatContentColumns.Nearest(effects, EffectAnimationOffset, offset => effects.IsKeyListColumn(offset, animated.RowCount));
        if (activeSkill == null || aoFile == null || otFile == null || monsterSkills == null || geometryAnimation == null || triggerMarkers == null || spawnObjects == null || effectAnimation == null)
        {
            Say.Out.WriteLine("A list or path column of the telegraph tables was not found again (dat-strings, dat-schema).");
            return null;
        }

        var animationSearch = new DatColumnSearch();
        var triggerAnimation = TelegraphColumns.FindTriggerAnimation(triggers, animated, aoFile.Value, animationSearch);
        if (!animationSearch.Report("telegraphs"))
        {
            return null;
        }

        var granted = new HashSet<long>();
        for (var row = 0; row < monsters.RowCount; row++)
        {
            granted.UnionWith(monsters.KeyList(row, monsterSkills.Value));
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
        if (skillId == null)
        {
            Say.Out.WriteLine("GrantedEffects' SkillID column was not found again (docs/research/attack-telegraphs.md).");
            return null;
        }

        var triggerIds = Ids(triggers);
        var columns = TelegraphColumns.Find(geometry, triggers, mortars, animated, TelegraphColumns.RowsOf(skills, skillId.Value, geometryIds, triggerIds));
        if (columns == null)
        {
            return null;
        }

        var tables = new Dictionary<string, Dictionary<int, int>>
        {
            ["geometry"] = geometryIds, ["trigger"] = triggerIds, ["spawn"] = Ids(spawns), ["effect"] = Ids(effects), ["mortar"] = Ids(mortars),
        };
        var files = BundleFiles.ByLowerPath(index);
        var skillLines = new SortedSet<string>(StringComparer.Ordinal);
        var effectRoles = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        var tally = new Dictionary<string, int>(StringComparer.Ordinal);
        var withLine = new HashSet<int>();
        foreach (var (row, kind) in kindOf)
        {
            if (!granted.Contains(row))
            {
                continue; // no monster has it
            }

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
                    area = TelegraphShapes.PairsOf(("shape", 0), ("length", geometry.Int32(target, columns.GeometryLength)), ("width", geometry.Int32(target, columns.GeometryWidth)),
                        ("angle", geometry.Int32(target, columns.GeometryAngle)), ("delay", geometry.Int32(target, columns.GeometryDelay)), ("range", geometry.Int32(target, columns.GeometryRange)));
                    area = $"shape={TelegraphShapes.GeometryShape(geometry.Int32(target, columns.GeometryShape)) ?? "unknown"}" + (area.Length > 0 ? TelegraphShapes.PairSeparator + area : string.Empty);
                    visuals.AddRange(Paths(geometry, target, geometryAnimation.Value, animated, aoFile.Value));
                    break;
                case "trigger":
                    area = TelegraphShapes.PairsOf(("count", triggers.Int32(target, columns.TriggerCountColumn)), ("delay", triggers.Int32(target, columns.TriggerDelay)), ("range", triggers.Int32(target, columns.TriggerRangeColumn)),
                        ("radius", triggers.Int32(target, columns.TriggerRadiusColumn)), ("angle", triggers.Int32(target, columns.TriggerAngleColumn)));
                    visuals.AddRange(Paths(triggers, target, triggerMarkers.Value, animated, aoFile.Value).Select(path => path + ":marker"));
                    if (Path(triggers.Key(target, triggerAnimation), animated, aoFile.Value) is { } animation)
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
                    if (Path(mortars.Key(target, columns.MortarImpact), animated, aoFile.Value) is { } impact)
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
            var text = files.TryGetValue(path.ToLowerInvariant(), out var file) ? file.ReadText() : null;
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

        foreach (var (skill, _) in KnownLines.Where(known => !skillLines.Any(line => line.StartsWith(known.Key + TelegraphShapes.FieldSeparator, StringComparison.Ordinal))))
        {
            Say.Out.WriteLine($"  telegraphs: known skill {skill} has no line any more, so the columns it would tell were judged by type and shape alone");
        }

        if (DatAnchors.Unmet(skillLines, TelegraphShapes.FieldSeparator, KnownLines) is { Count: > 0 } unmet)
        {
            Say.Out.WriteLine($"  telegraphs: {string.Join("; ", unmet)}: a list or path column holds something else (check with dat-kinds), telegraphs keep their previous catalog");
            return null;
        }

        Say.Out.WriteLine($"Telegraphs: {skillLines.Count} skills, {effectLines.Count} effect files ({shaped} with shapes), {monsterLines.Count} monsters.");
        foreach (var (what, count) in tally.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Say.Out.WriteLine($"  {what}: {count}");
        }

        return new Lines(skillLines, effectLines, monsterLines);
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

    /// <summary>A table of the index; null, and said, when it is missing.</summary>
    private static DatTable? Sized(Index index, string name)
    {
        var table = DatTable.Open(index, name);
        if (table != null)
        {
            return table;
        }

        Say.Out.WriteLine($"{name} not found: telegraphs left out.");
        return null;
    }
}
