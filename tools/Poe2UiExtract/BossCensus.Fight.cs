using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>One boss-bar monster's fight gathered from the files (docs/research/boss-mechanics.md §14).</summary>
internal sealed partial class BossCensus
{
    /// <summary>Objects beside a boss's script beyond this many are a folder of shared props, not its arena's (the census's cap).</summary>
    private const int MaxFolderObjects = 120;

    /// <summary>An add's name word shorter than this ("the", "of") names no one (the census's rule).</summary>
    private const int MinNameWord = 4;

    /// <summary>A buff id holds the boss script's name (sudoku_boss, ritual_wendigo) when the name has this many letters: shorter ones are common words.</summary>
    private const int MinStem = 6;

    // Stats of a buff that harm its bearer (the census's list): an area giving one is no shelter.
    private static readonly Regex HarmStat = new(@"damage_taken_per_minute|_taken_\+%|movement_velocity_\+%|cannot_|reduced_|less_|_-%|life_regeneration_rate_per_minute_%_|degen|reservation", RegexOptions.IgnoreCase);
    private static readonly Regex GainHandler = new(@"^on_(?:gain|lose)_(\w+)$");
    private static readonly Regex Snake = new("(?<=[a-z0-9])(?=[A-Z])");

    private BossFight Fight(int row)
    {
        var m = this.t.Monsters;
        var boss = m.String(row, 0);
        var script = m.StringOrNull(row, this.bc.Script) ?? string.Empty;
        var chain = this.ChainOf(script);
        var bossSkills = this.SkillsOf(row, followTriggers: true);

        var paths = chain.Preloads.Where(preload => preload.Kind == "preload_object").Select(preload => preload.Value)
            .Concat(bossSkills.SelectMany(skill => skill.Objects)).Concat(bossSkills.SelectMany(skill => skill.Geals).SelectMany(geal => geal.Objects))
            .Where(path => !this.monsterRows.ContainsKey(path)).ToList();
        paths.AddRange(this.Beside(script).Take(Math.Max(0, MaxFolderObjects - paths.Count)));
        var folder = string.Join('/', script.Split('/').Take(3));
        var fightObjects = paths.Select(path => path.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var dirs = this.folders.GetValueOrDefault(row) ?? new HashSet<string>();
        var arena = BossArenaRooms.Of(folder, fightObjects, dirs, this.rooms);
        paths.AddRange(BossArenaRooms.ObjectsOf(arena, folder, this.monsterRows.ContainsKey));
        var objects = paths.Select(this.CaseOf).Distinct(StringComparer.OrdinalIgnoreCase).Select(path => new BossObject(path, this.ChainOf(path))).ToList();
        var placed = arena.SelectMany(room => room.Objects).Where(this.monsterRows.ContainsKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var adds = new List<BossAdd>();
        void Add(string id, bool daemon, bool brought)
        {
            if (this.monsterRows.TryGetValue(id, out var other) && other != row && adds.All(add => !add.Id.Equals(m.String(other, 0), StringComparison.OrdinalIgnoreCase)))
            {
                adds.Add(new BossAdd(m.String(other, 0), m.StringOrNull(other, this.bc.Name) ?? string.Empty, m.Byte(other, this.bc.BossBar) == 1, this.SkillIdsOf(other),
                    this.SkillsOf(other, followTriggers: false), this.ChainOf(m.StringOrNull(other, this.bc.Script)), daemon, brought || placed.Contains(id)));
            }
        }

        foreach (var preload in chain.Preloads.Where(preload => preload.Kind == "preload_monster_variety"))
        {
            Add(preload.Value, daemon: false, brought: true);
        }

        foreach (var summon in bossSkills.SelectMany(skill => skill.Summons))
        {
            Add(summon, daemon: false, brought: true);
        }

        foreach (var daemon in bossSkills.SelectMany(skill => skill.Daemons))
        {
            Add(daemon, daemon: true, brought: true);
        }

        foreach (var participant in this.families.Participants(row))
        {
            Add(participant, daemon: false, brought: false);
        }

        var words = adds.SelectMany(add => BossScripts.WordsOf(add.Name)).Where(word => word.Length >= MinNameWord).Distinct().ToList();
        var named = this.orphans.Where(geal => words.Any(word => Regex.IsMatch(geal.Each, $@"AddBuff\(\s*{Regex.Escape(word)}_", RegexOptions.IgnoreCase))).ToList();
        var fight = new BossFight(boss, chain, bossSkills, adds, objects, named, Array.Empty<BossBuff>());
        return fight with { Buffs = this.LinkedBuffs(fight, script) };
    }

    /// <summary>A monster's skills read through their tables; a boss's with the skills its triggers fire, once each.</summary>
    private List<BossSkill> SkillsOf(int monster, bool followTriggers)
    {
        var skills = this.t.Monsters.KeyList(monster, this.bc.MonsterSkills).Where(key => key >= 0 && key < this.t.Skills.RowCount).Select(key => this.skills.Read((int)key)).ToList();
        for (var i = 0; followTriggers && i < skills.Count; i++)
        {
            foreach (var triggered in skills[i].Triggers.Where(id => skills.All(skill => skill.Id != id)))
            {
                if (this.skillRows.TryGetValue(triggered, out var skillRow))
                {
                    skills.Add(this.skills.Read(skillRow));
                }
            }
        }

        return skills.DistinctBy(skill => skill.Id).ToList();
    }

    private List<string> SkillIdsOf(int monster) =>
        this.t.Monsters.KeyList(monster, this.bc.MonsterSkills).Select(key => DatAnchors.IdOf(this.t.Skills, key)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    ///     The objects scripted beside a boss's script, in its folder and below (Yama's clones): no monster's script, none of
    ///     their bases, no regional copy ("….japan.ot").
    /// </summary>
    private IEnumerable<string> Beside(string script)
    {
        var dir = script.Contains('/') ? script[..script.LastIndexOf('/')].ToLowerInvariant() + "/" : string.Empty;
        if (dir.Count(ch => ch == '/') < 3)
        {
            return Enumerable.Empty<string>();
        }

        var bases = this.monsterScripts.Where(path => path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => this.ChainOf(path).Scripts.Select(facts => facts.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return this.scriptPaths.Where(path => path.StartsWith(dir, StringComparison.Ordinal)).Select(path => path[..^ScriptExtension.Length])
            .Where(path => !path[(path.LastIndexOf('/') + 1)..].Contains('.') && !this.monsterScripts.Contains(path) && !bases.Contains(path)).Order(StringComparer.Ordinal);
    }

    /// <summary>
    ///     The buffs a fight's own data reaches (§14 "linked buff"): its and its adds' scripts' buff calls, on_gain handlers and
    ///     preloads; its and its adds' skills'; its objects' handlers'; its ExecuteGEAL rows'; and those whose id holds the script's name.
    /// </summary>
    private List<BossBuff> LinkedBuffs(BossFight fight, string script)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in fight.Adds.Select(add => add.Script).Prepend(fight.Script))
        {
            foreach (var facts in chain.Own)
            {
                ids.UnionWith(facts.Handlers.SelectMany(handler => this.skills.BuffsIn(handler.Body)));
                ids.UnionWith(facts.Handlers.Select(handler => GainHandler.Match(handler.Name)).Where(match => match.Success).Select(match => match.Groups[1].Value).Where(this.skills.IsBuff));
                ids.UnionWith(facts.Preloads.Where(preload => preload.Kind == "preload_buff").Select(preload => preload.Value).Where(this.skills.IsBuff));
            }
        }

        ids.UnionWith(fight.Skills.Concat(fight.Adds.SelectMany(add => add.Skills)).SelectMany(skill => skill.Buffs));
        ids.UnionWith(fight.Objects.SelectMany(item => item.Script.Handlers).SelectMany(handler => this.skills.BuffsIn(handler.Body)));
        ids.UnionWith(fight.Geals.SelectMany(geal => this.skills.BuffsIn(geal.Self + " " + geal.Each)));
        var folder = script.Split('/').Take(3).LastOrDefault() ?? string.Empty;
        var stems = new[] { script[(script.LastIndexOf('/') + 1)..], folder }.Select(name => Snake.Replace(name, "_").ToLowerInvariant()).Where(stem => stem.Length >= MinStem).Distinct().ToList();
        ids.UnionWith(this.buffRows.Keys.Where(id => stems.Any(stem => Regex.IsMatch(id, $"(^|_){Regex.Escape(stem)}(_|$)"))));
        return ids.Order(StringComparer.Ordinal).Select(this.Buff).ToList();
    }

    private BossBuff Buff(string id)
    {
        var row = this.buffRows[id];
        var b = this.t.Buffs;
        var text = $"{b.StringOrNull(row, this.buffColumns.Description)} {b.StringOrNull(row, this.buffColumns.Name)}".Trim();
        var harmful = b.KeyList(row, this.bc.BuffStats).Select(key => DatAnchors.IdOf(this.statTable, key)).OfType<string>().Any(HarmStat.IsMatch);
        return new BossBuff(id, text, b.Byte(row, this.c.ShowCount) == 1, harmful);
    }
}
