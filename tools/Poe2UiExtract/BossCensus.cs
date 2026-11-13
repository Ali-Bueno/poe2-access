using System;
using System.Collections.Generic;
using System.Linq;
using Index = LibBundle3.Index;

/// <summary>
///     The census's lines of `boss-mechanics` (docs/research/boss-mechanics.md §14-§15): each boss-bar monster's fight read
///     from the skill tables, the scripts, the arena rooms and the linked buffs (<see cref="BossFight"/>), then judged by
///     <see cref="BossCensusRules"/>. A table or column not found leaves these classes out, said, and the rest of the catalog stands.
/// </summary>
internal sealed partial class BossCensus
{
    /// <summary>A boss the arena areas are known to list (WorldAreas.Bosses on 0.5.5f): Jamanra, the Abomination.</summary>
    internal const string KnownArenaBoss = "Metadata/Monsters/PerennialKingStage2/PerennialKing";

    private const string ScriptExtension = ".ot";

    private static readonly string[] TableNames =
    {
        "geometryattack", "geometrytrigger", "executegeal", "spawnobject", "miscobjects", "movedaemon", "summonedspecificmonsters", "worldareas",
        "topologies", "monstermortar", "miscanimated",
    };

    private readonly BossCensusColumns.Tables t;
    private readonly BossCensusColumns c;
    private readonly BossColumns bc;
    private readonly BuffColumns buffColumns;
    private readonly BossSkills skills;
    private readonly Func<string, string?> textOf;
    private readonly IReadOnlyCollection<string> scriptPaths;
    private readonly HashSet<string> shared;
    private readonly Dictionary<string, BossScriptFacts?> facts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> cased = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> monsterRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> skillRows = new(StringComparer.Ordinal);
    private readonly HashSet<string> monsterScripts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> buffRows = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<BossGeal> orphans;
    private readonly Dictionary<int, HashSet<string>> folders;
    private readonly BossArenaRooms.Rooms rooms;
    private readonly BossFamilies families;
    private readonly DatTable statTable;

    private BossCensus(Index index, BossCensusColumns.Tables tables, BossCensusColumns columns, BossColumns bossColumns, BuffColumns buffColumns, BossSkills skills,
        DatTable stats, Func<string, string?> textOf, IReadOnlyDictionary<string, LibBundle3.Records.FileRecord> scripts)
    {
        (this.t, this.c, this.bc, this.buffColumns, this.skills, this.statTable, this.textOf) = (tables, columns, bossColumns, buffColumns, skills, stats, textOf);
        this.scriptPaths = scripts.Keys.ToList();
        var extends = new System.Collections.Concurrent.ConcurrentBag<IReadOnlyList<string>>();
        BundleFiles.ReadGrouped(scripts.Values, (file, bytes) => extends.Add(BossScripts.ExtendsOf(GameFormats.Decode(bytes, file.Path))));
        this.shared = BossScriptChain.SharedBases(extends);
        for (var row = 0; row < tables.Monsters.RowCount; row++)
        {
            this.monsterRows.TryAdd(tables.Monsters.String(row, 0), row);
            this.Cased(tables.Monsters.String(row, 0));
            if (tables.Monsters.StringOrNull(row, bossColumns.Script) is { Length: > 0 } script)
            {
                this.monsterScripts.Add(script);
                this.Cased(script);
            }
        }

        for (var row = 0; row < tables.Skills.RowCount; row++)
        {
            this.skillRows.TryAdd(tables.Skills.String(row, 0), row);
        }

        for (var row = 0; row < tables.Buffs.RowCount; row++)
        {
            this.buffRows.TryAdd(tables.Buffs.String(row, 0), row);
        }

        for (var row = 0; row < tables.Objects.RowCount; row++)
        {
            this.Cased(tables.Objects.StringOrNull(row, columns.ObjectPath));
        }

        this.orphans = skills.Orphans();
        this.folders = BossArenaRooms.Folders(tables, columns);
        this.rooms = BossArenaRooms.Read(index);
        foreach (var path in this.rooms.All.SelectMany(room => room.Objects))
        {
            this.Cased(path);
        }

        this.families = new BossFamilies(tables.Monsters, tables.Skills, bossColumns);
    }

    /// <summary>The census's lines and the buff ids they name, or null (said) when a table or column is missing.</summary>
    public static (IReadOnlyList<string> Lines, IReadOnlyList<string> Buffs)? Build(Index index, DatTable monsters, DatTable skillTable, DatTable kinds, DatTable stats, DatTable buffs,
        BossColumns bossColumns, BuffColumns buffColumns, Func<string, string?> textOf, IReadOnlyDictionary<string, LibBundle3.Records.FileRecord> scripts)
    {
        var opened = TableNames.ToDictionary(name => name, name => DatTable.Open(index, name));
        if (opened.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList() is { Count: > 0 } missing)
        {
            Say.Out.WriteLine($"{string.Join(", ", missing)} not found: the census's classes are left out of boss-mechanics.");
            return null;
        }

        if (ProjectileSpeeds.Columns(index) is not var (statTables, statColumns))
        {
            Say.Out.WriteLine("The skill stat sets were not found: the census's classes are left out of boss-mechanics.");
            return null;
        }

        var kindOf = new Dictionary<int, string>();
        for (var row = 0; row < skillTable.RowCount; row++)
        {
            if (DatAnchors.IdOf(kinds, skillTable.Key(row, bossColumns.SkillKind)) is { } kind && TelegraphShapes.KindOf(kind) is { } shape)
            {
                kindOf[row] = shape;
            }
        }

        var geometryIds = BossCensusColumns.IdRows(opened["geometryattack"]!);
        if (TelegraphExtractor.FindSkillIdColumn(skillTable, kindOf, geometryIds) is not { } skillId ||
            TelegraphColumns.Find(opened["geometryattack"]!, opened["geometrytrigger"]!, opened["monstermortar"]!, opened["miscanimated"]!,
                TelegraphColumns.RowsOf(skillTable, skillId, geometryIds, BossCensusColumns.IdRows(opened["geometrytrigger"]!))) is not { } shapes)
        {
            Say.Out.WriteLine("GrantedEffects' SkillID or GeometryAttack's length was not found: the census's classes are left out of boss-mechanics.");
            return null;
        }

        var tables = new BossCensusColumns.Tables(monsters, skillTable, statTables.StatSets, opened["geometryattack"]!, opened["geometrytrigger"]!, opened["executegeal"]!,
            opened["spawnobject"]!, opened["miscobjects"]!, opened["movedaemon"]!, opened["summonedspecificmonsters"]!, buffs, opened["worldareas"]!, opened["topologies"]!);
        if (BossCensusColumns.Find(tables, skillId, statColumns.SkillStatSet, DatColumnSearch.RowOf(monsters, KnownArenaBoss)) is not { } columns)
        {
            return null;
        }

        var reader = new BossSkills(tables, columns, statColumns, kinds, stats, bossColumns.SkillKind, skillId, shapes.GeometryLength);
        var census = new BossCensus(index, tables, columns, bossColumns, buffColumns, reader, stats, textOf, scripts);
        var lines = new List<string>();
        for (var row = 0; row < monsters.RowCount; row++)
        {
            if (monsters.Byte(row, bossColumns.BossBar) == 1)
            {
                lines.AddRange(BossCensusRules.Lines(census.Fight(row)));
            }
        }

        var distinct = lines.Distinct(StringComparer.Ordinal).ToList();
        return (distinct, BossCensusRules.TextBuffs(distinct));
    }

    /// <summary>How many lines of each kind, for the log ("daemon 44, arena 12").</summary>
    internal static string Tally(IEnumerable<string>? lines) =>
        string.Join(", ", (lines ?? Enumerable.Empty<string>()).GroupBy(line => line[..line.IndexOf('|')], StringComparer.Ordinal).Select(group => $"{group.Key} {group.Count()}"));

    /// <summary>A script's facts, read once; the paths it names teach the case they are written in.</summary>
    private BossScriptFacts? FactsOf(string path)
    {
        if (this.facts.TryGetValue(path, out var known))
        {
            return known;
        }

        var key = path.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase) ? path : path + ScriptExtension;
        var read = this.textOf(key) is { } text ? BossScriptFacts.Parse(path, text) : null;
        this.facts[path] = read;
        foreach (var named in read?.Extends.Concat(read.Preloads.Select(preload => preload.Value)) ?? Enumerable.Empty<string>())
        {
            this.Cased(named);
        }

        return read;
    }

    private BossScriptChain ChainOf(string? path) => BossScriptChain.Of(path ?? string.Empty, this.FactsOf, this.shared.Contains);

    /// <summary>Remembers how the game writes a path (the index lists its files in lower case).</summary>
    private void Cased(string? path)
    {
        if (!string.IsNullOrEmpty(path) && path.Any(char.IsUpper))
        {
            this.cased.TryAdd(path, path);
        }
    }

    /// <summary>A path as the game writes it, where any of its files or tables does; else as the index has it.</summary>
    private string CaseOf(string path) => this.cased.GetValueOrDefault(path) ?? path;
}
