using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `boss-mechanics` command: what the mod tells of a boss fight, from the game's files (docs/research/boss-mechanics.md).
///     Every monster with the boss life bar (MonsterVarieties.BossHealthBar) and, from its script (.ot) and the scripts it
///     extends: the state values that hide it, its phase and enrage states, the monsters it brings (adds, or its next form
///     when they have a boss bar too) and the voice lines that announce a mechanic, in every language the game ships; then
///     the buffs that make a monster unhurtable or enraged, and those whose description tells the player to stop or keep moving.
/// </summary>
internal static class BossMechanicsExtractor
{
    /// <summary>The game's name for an invisible monster that only carries a skill (Balbala's fog, Ketzuli's black hole): no add.</summary>
    private const string HelperName = "Daemon";

    private const string ScriptExtension = ".ot";
    private const string HiddenStat = "is_hidden_monster";
    private const string UnhurtableStat = "base_cannot_be_damaged";
    private const char Field = '|';

    private static readonly Regex StopMoving = new(@"\b(stop moving|stand still|remain still|stay still|do not move|don't move)\b", RegexOptions.IgnoreCase);
    private static readonly Regex KeepMoving = new(@"\b(keep moving|moving breaks|moving .{0,40}\bbreak)", RegexOptions.IgnoreCase);

    private static readonly string[] Header =
    {
        "# What the mod tells of boss fights, from the game's files (tools/Poe2UiExtract boss-mechanics), regenerated after patches.",
        "# boss|monster|name   hide|monster|state=value (stops drawing it)   phase|monster|state (its value is the fight's phase)",
        "# enrage|monster|state   add|monster|monster it brings   form|monster|boss it turns into   buff|id|invulnerable or enrage",
        "# move|buff id|stop or keep (the player's rule)   line|language|text|monster|skill or -|line id (a voice line announcing a mechanic)",
    };

    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, "monstervarieties") is not { } monsters || DatTable.Open(index, "buffdefinitions") is not { } buffs ||
            DatTable.Open(index, "stats") is not { } stats || DatTable.Open(index, "npctextaudio") is not { } lines ||
            DatTable.Open(index, "grantedeffects") is not { } skills || DatTable.Open(index, "activeskills") is not { } kinds)
        {
            Say.Out.WriteLine("MonsterVarieties, BuffDefinitions, Stats, NPCTextAudio, GrantedEffects or ActiveSkills not found: boss mechanics left out.");
            return 1;
        }

        var c = BossColumns.Find(monsters, buffs, stats, lines, skills, kinds);
        var buffColumns = BuffColumns.FindWithDescription(buffs, "boss-mechanics");
        if (c == null || buffColumns == null)
        {
            return 1;
        }

        var files = index.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase))
            .GroupBy(file => file.Path!.ToLowerInvariant()).ToDictionary(group => group.Key, group => group.First());
        var scripts = new Dictionary<string, BossScripts.Script?>(StringComparer.Ordinal);
        BossScripts.Script? ScriptOf(string path)
        {
            var key = (path.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase) ? path : path + ScriptExtension).ToLowerInvariant();
            if (!scripts.TryGetValue(key, out var script))
            {
                script = files.TryGetValue(key, out var file) ? BossScripts.Parse(file.ReadText()) : null;
                scripts[key] = script;
            }

            return script;
        }

        var skillKinds = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < skills.RowCount; row++)
        {
            if (skills.Key(row, c.SkillKind) is >= 0 and var kind && kind < kinds.RowCount && kinds.StringOrNull((int)kind, 0) is { } name)
            {
                skillKinds.TryAdd(skills.String(row, 0), name);
            }
        }

        string? KindOf(string handler) => BossScripts.SkillOf(handler) is { } skill ? skillKinds.GetValueOrDefault(skill) : null;

        var monsterRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var row = 0; row < monsters.RowCount; row++)
        {
            monsterRows.TryAdd(monsters.String(row, 0), row);
        }

        var output = new List<string>(Header);
        var played = new List<(string Boss, string Handler, string Line)>();
        var bosses = 0;
        for (var row = 0; row < monsters.RowCount; row++)
        {
            if (monsters.Byte(row, c.BossBar) != 1)
            {
                continue;
            }

            bosses++;
            var id = monsters.String(row, 0);
            output.Add($"boss|{id}|{Clean(monsters.StringOrNull(row, c.Name) ?? string.Empty)}");
            var chain = Chain(monsters.StringOrNull(row, c.Script) ?? string.Empty, ScriptOf);
            output.AddRange(chain.SelectMany(script => script.Hides).Distinct().Select(hide => $"hide|{id}|{hide.State}={hide.Value}"));
            output.AddRange(chain.SelectMany(script => script.PhaseStates).Distinct().Select(state => $"phase|{id}|{state}"));
            output.AddRange(chain.SelectMany(script => script.EnrageStates).Distinct().Select(state => $"enrage|{id}|{state}"));
            foreach (var monster in chain.SelectMany(script => script.Monsters).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (monsterRows.TryGetValue(monster, out var other) && other != row && monsters.StringOrNull(other, c.Name) is { Length: > 0 } name && name != HelperName)
                {
                    output.Add($"{(monsters.Byte(other, c.BossBar) == 1 ? "form" : "add")}|{id}|{monsters.String(other, 0)}");
                }
            }

            played.AddRange(chain.SelectMany(script => script.Lines).Where(line => BossScripts.AnnouncesMechanic(line.Handler, line.Line, KindOf(line.Handler))).Select(line => (id, line.Handler, line.Line)));
        }

        var (buffLines, rules) = Buffs(buffs, stats, c, buffColumns);
        output.AddRange(buffLines);
        var voiced = Lines(index, lines, c, played);
        output.AddRange(voiced);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, output);
        Say.Out.WriteLine($"Wrote {outFile}: {bosses} bosses, {buffLines.Count - rules} boss buffs, {rules} movement rules, {voiced.Count} mechanic lines.");
        return 0;
    }

    /// <summary>A boss's script and every script it extends, nearest first.</summary>
    private static List<BossScripts.Script> Chain(string path, Func<string, BossScripts.Script?> scriptOf)
    {
        var chain = new List<BossScripts.Script>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(path);
        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (next.Length == 0 || !seen.Add(next) || scriptOf(next) is not { } script)
            {
                continue;
            }

            chain.Add(script);
            foreach (var parent in script.Extends)
            {
                queue.Enqueue(parent);
            }
        }

        return chain;
    }

    /// <summary>The buffs that make their bearer unhurtable (and not merely hidden) or enraged, and the player's movement rules.</summary>
    private static (List<string> Lines, int Rules) Buffs(DatTable buffs, DatTable stats, BossColumns c, BuffColumns buffColumns)
    {
        var lines = new List<string>();
        var rules = 0;
        for (var row = 0; row < buffs.RowCount; row++)
        {
            var id = buffs.String(row, 0);
            var statIds = buffs.KeyList(row, c.BuffStats).Where(key => key >= 0 && key < stats.RowCount).Select(key => stats.StringOrNull((int)key, 0)).ToList();
            if (statIds.Contains(UnhurtableStat) && !statIds.Contains(HiddenStat))
            {
                lines.Add($"buff|{id}|invulnerable");
            }

            if (BossScripts.WordsOf(id).Any(word => word is "enrage" or "enraged"))
            {
                lines.Add($"buff|{id}|enrage");
            }

            var description = buffs.StringOrNull(row, buffColumns.Description) ?? string.Empty;
            var rule = StopMoving.IsMatch(description) ? "stop" : KeepMoving.IsMatch(description) ? "keep" : null;
            if (rule != null)
            {
                lines.Add($"move|{id}|{rule}");
                rules++;
            }
        }

        return (lines, rules);
    }

    /// <summary>Each mechanic line's text in English and in every translation the game ships, once per language and text.</summary>
    private static List<string> Lines(Index index, DatTable english, BossColumns c, List<(string Boss, string Handler, string Line)> played)
    {
        var byStem = english.RowCount == 0 ? new Dictionary<string, List<int>>() : Enumerable.Range(0, english.RowCount)
            .GroupBy(row => BossScripts.Stem(english.String(row, 0)), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var languages = AreaExtractor.Languages;
        var output = new List<string>();
        var said = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? english : DatTable.OpenTranslated(index, "npctextaudio", folder);
            if (table == null || table.RowCount != english.RowCount)
            {
                continue; // a translation out of step with the English rows would pair the wrong lines
            }

            foreach (var (boss, handler, line) in played)
            {
                foreach (var row in byStem.GetValueOrDefault(BossScripts.Stem(line)) ?? new List<int>())
                {
                    if (!BossScripts.IsVariant(line, english.String(row, 0)) || Clean(table.StringOrNull(row, c.LineText) ?? string.Empty) is not { Length: > 0 } text ||
                        !said.Add($"{code}|{text}"))
                    {
                        continue;
                    }

                    output.Add($"line|{code}|{text}|{boss}|{BossScripts.SkillOf(handler) ?? "-"}|{english.String(row, 0)}");
                }
            }
        }

        return output;
    }

    /// <summary>A text on one catalog line: no field separator, no line breaks.</summary>
    private static string Clean(string text) => text.Replace(Field, ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
}
