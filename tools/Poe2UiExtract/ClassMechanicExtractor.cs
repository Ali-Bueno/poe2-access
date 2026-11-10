using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `class-mechanics` command: what a class builds up and spends, from the game's tables (docs/research/class-mechanics.md):
///     the Stats pairs current/maximum (<see cref="ClassMechanics.Resources"/>) and active/allowed limits, the display names
///     per language of the buffs that are not harmful (the harmful ones are `buff-names`), and each granted effect's skill
///     types (ActiveSkills → ActiveSkillType: Banner, Grenade, Warcry, Minion…) by the id the player's Actor lists.
/// </summary>
internal static class ClassMechanicExtractor
{
    private const string Skipped = "class mechanics";

    // BuffDefinitions: Id +0x0; the name and type columns are found by BuffColumns.
    private const int BuffIdOffset = 0x0;
    private const int DebuffType = 2;
    private const int CurseType = 5;

    // GrantedEffects: Id +0x0, ActiveSkill key +0x4F (dat-columns "GrantedEffects.ActiveSkill"); ActiveSkills: Id +0x0, types key list +0x40.
    private const int EffectIdOffset = 0x0;
    private const int EffectActiveSkillOffset = 0x4F;
    private const int ActiveSkillIdOffset = 0x0;
    private const int ActiveSkillTypesOffset = 0x40;
    private const int ActiveSkillTypeIdOffset = 0x0;
    private const int KeyListSize = 16;

    /// <summary>Two skills whose types are known: the detected columns must give them (KeywordPopups "Banner", "Grenade").</summary>
    private static readonly (string Skill, string Type)[] Checks = { ("war_banner", "Banner"), ("explosive_grenade", "Grenade") };

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var stats = DatTable.Open(index, "stats");
        var buffs = DatTable.Open(index, "buffdefinitions");
        if (stats == null || buffs == null || stats.String(0, 0) != "level")
        {
            Say.Out.WriteLine($"Stats or BuffDefinitions missing or not as before: {Skipped} left out.");
            return 1;
        }

        if (BuffColumns.Find(buffs, "class-mechanics") is not { } buffColumns)
        {
            return 1;
        }

        var ids = Enumerable.Range(0, stats.RowCount).Select(row => stats.String(row, 0)).ToList();
        var resources = ClassMechanics.Resources(ids);
        var limits = ClassMechanics.Limits(ids);
        if (resources.Count == 0 || limits.Count == 0)
        {
            Say.Out.WriteLine($"No current/maximum or active/allowed stat pairs: {Skipped} left out.");
            return 1;
        }

        var result = new
        {
            stats = resources.Select(resource => new { name = resource.Name, current = resource.Current, maximum = resource.Maximum, virtualMaximum = resource.VirtualMaximum }).ToList(),
            limits = limits.Select(limit => new { name = limit.Name, active = limit.Active, allowed = limit.Allowed }).ToList(),
            buffs = BuffNames(index, buffs, buffColumns),
            skills = SkillTypes(index),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Say.Out.WriteLine($"Wrote {outFile}: {result.stats.Count} resources, {result.limits.Count} limits, {result.buffs.Count} named buffs, {result.skills.Count} typed skills.");
        return 0;
    }

    /// <summary>Every buff that is not a debuff or curse and has a shown name: its type and its name per language ("en" first).</summary>
    private static SortedDictionary<string, object> BuffNames(Index index, DatTable english, BuffColumns columns)
    {
        var named = new SortedDictionary<string, (int Type, SortedDictionary<string, string> Names)>(StringComparer.Ordinal);
        for (var row = 0; row < english.RowCount; row++)
        {
            var id = english.StringOrNull(row, BuffIdOffset);
            var name = english.StringOrNull(row, columns.Name)?.Trim();
            var type = english.Byte(row, columns.Type);
            if (!string.IsNullOrEmpty(id) && ClassMechanics.IsShownName(name) && type is not (DebuffType or CurseType))
            {
                named[id] = (type, new SortedDictionary<string, string>(StringComparer.Ordinal) { ["en"] = name! });
            }
        }

        foreach (var (folder, code) in AreaExtractor.LanguageCodes)
        {
            var table = DatTable.OpenTranslated(index, "buffdefinitions", folder);
            if (table == null || table.RowCount != english.RowCount)
            {
                Say.Out.WriteLine($"  {code}: no BuffDefinitions of the same size");
                continue;
            }

            for (var row = 0; row < table.RowCount; row++)
            {
                var id = table.StringOrNull(row, BuffIdOffset);
                var name = table.StringOrNull(row, columns.Name)?.Trim();
                if (id != null && ClassMechanics.IsShownName(name) && named.TryGetValue(id, out var entry))
                {
                    entry.Names[code] = name!;
                }
            }
        }

        return new SortedDictionary<string, object>(named.ToDictionary(pair => pair.Key, pair => (object)new { type = pair.Value.Type, names = pair.Value.Names }), StringComparer.Ordinal);
    }

    /// <summary>Each granted effect with an active skill: its skill type names, by the GrantedEffects id; empty when the columns fail the checks.</summary>
    private static SortedDictionary<string, List<string>> SkillTypes(Index index)
    {
        var result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var effects = DatTable.Open(index, "grantedeffects");
        var skills = DatTable.Open(index, "activeskills");
        var types = DatTable.Open(index, "activeskilltype");
        if (effects == null || skills == null || types == null)
        {
            Say.Out.WriteLine("GrantedEffects, ActiveSkills or ActiveSkillType missing: skill types left out.");
            return result;
        }

        var typeNames = Enumerable.Range(0, types.RowCount).Select(row => types.String(row, ActiveSkillTypeIdOffset)).ToList();
        var typesOffset = TypesColumn(skills, types.RowCount, typeNames);
        if (typesOffset < 0)
        {
            Say.Out.WriteLine("No ActiveSkills column gives war_banner 'Banner' and explosive_grenade 'Grenade': skill types left out.");
            return result;
        }

        var skillOffset = effects.IsKeyColumn(EffectActiveSkillOffset, skills.RowCount) ? EffectActiveSkillOffset
            : Enumerable.Range(0, effects.RowSize - 8).FirstOrDefault(offset => effects.IsKeyColumn(offset, skills.RowCount), -1);
        if (skillOffset < 0)
        {
            Say.Out.WriteLine("No GrantedEffects column keys into ActiveSkills: skill types left out.");
            return result;
        }

        for (var row = 0; row < effects.RowCount; row++)
        {
            var skill = effects.Key(row, skillOffset);
            var id = effects.StringOrNull(row, EffectIdOffset);
            if (skill >= 0 && skill < skills.RowCount && !string.IsNullOrEmpty(id))
            {
                var names = skills.KeyList((int)skill, typesOffset).Where(type => type >= 0 && type < typeNames.Count).Select(type => typeNames[(int)type]).Distinct().ToList();
                if (names.Count > 0)
                {
                    result[id] = names;
                }
            }
        }

        return result;
    }

    /// <summary>The ActiveSkills key-list column into ActiveSkillType: the measured one when it passes the checks, else the first that does; -1 when none.</summary>
    private static int TypesColumn(DatTable skills, int typeRows, List<string> typeNames)
    {
        var rows = Checks.Select(check => (Row: Enumerable.Range(0, skills.RowCount).FirstOrDefault(row => skills.String(row, ActiveSkillIdOffset) == check.Skill, -1), check.Type)).ToList();
        if (rows.Any(check => check.Row < 0))
        {
            return -1;
        }

        bool Passes(int offset) => rows.All(check =>
            skills.KeyList(check.Row, offset).Any(type => type >= 0 && type < typeNames.Count && typeNames[(int)type] == check.Type));
        if (Passes(ActiveSkillTypesOffset))
        {
            return ActiveSkillTypesOffset;
        }

        return Enumerable.Range(0, skills.RowSize - KeyListSize).FirstOrDefault(offset => skills.IsKeyListColumn(offset, typeRows) && Passes(offset), -1);
    }
}
