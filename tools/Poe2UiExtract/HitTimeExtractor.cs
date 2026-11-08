using Poe2Accessibility.Services;
using Index = LibBundle3.Index;

/// <summary>
///     `hit-times` command: when each monster attack lands, from the game's own animation data, so the mod can warn
///     before an attack's first blow (sources and checks: docs/research/dodge-timing.md). For every MonsterVarieties row:
///     - its actions: "Melee" and the animation of each granted skill (GrantedEffects +0x71 → Animation row), plus the
///       actions the Animation table plays with them (<see cref="AnimationActions"/>);
///     - each action's animations from the monster's .act, through the Animation table's fallbacks when it lacks one;
///     - each animation's hit (<see cref="HitOf"/>), the earliest of the action winning;
///     - for a skill that fires a projectile, its speed (<see cref="ProjectileSpeeds"/>), so the mod adds its flight;
///     - what kind of threat the skills make it (<see cref="SkillThreats"/>: melee, ranged, calm, or nothing when the
///       tables are mute), and whether the player cannot block or parry it (<see cref="SkillFlags"/>), both carried to
///       the actions played with it.
///     Written as "monster path|animation id=hit ms/animation ms[/projectile speed][;flags]" lines ("-" for an untimed
///     attack that carries a kind or flags; a calm action is always "-"); the Actor's animation id is the action's row in
///     Animation.datc64. An audit file, when asked for, lists where the kind disagrees with the animation's name.
/// </summary>
internal static class HitTimeExtractor
{
    // MonsterVarieties: Id, ACTFiles, AOFiles, GrantedEffects (found with dat-strings; the community dat-schema's names).
    private const int MonsterIdOffset = 0x00;
    private const int ActFilesOffset = 0x28;
    private const int AoFilesOffset = 0x38;
    private const int GrantedEffectsOffset = 0xD0;

    // MonsterVarieties: the main hand's ItemClasses row (the schema's MainHand_ItemClass; measured 2026-11-08: every archer and
    // crossbow monster of the campaign reads "Bow", MeleeUrchin1 none). Its basic attack shoots with that weapon.
    private const int MainHandClassOffset = 0x154;

    /// <summary>ItemClasses ids of the weapons whose basic attack is a shot (crossbow monsters hold a "Bow" in 0.5.5f).</summary>
    private static readonly HashSet<string> ShootingClasses = new(StringComparer.Ordinal) { "Bow", "Crossbow" };

    // GrantedEffects: the skill's Animation row (inferred from 8 rows).
    private const int SkillAnimationOffset = 0x71;

    private const string MeleeAction = "Melee";
    private const string ActExtension = ".act";
    private const string AoExtension = ".ao";

    private enum Source
    {
        Contact,
        DamagePattern,
        BakedContact,
    }

    public static int Run(string gameDir, string outFile, string? auditFile = null)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile, auditFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile, string? auditFile = null)
    {
        var files = BundleFiles.ByLowerPath(index);
        var monsters = DatTable.Open(index, "monstervarieties");
        var skills = DatTable.Open(index, "grantedeffects");
        var animations = DatTable.Open(index, "animation");
        var itemClasses = DatTable.Open(index, "itemclasses");
        if (monsters == null || skills == null || animations == null || itemClasses == null)
        {
            Say.Out.WriteLine("MonsterVarieties, GrantedEffects, Animation or ItemClasses not found.");
            return 1;
        }

        // Found by what they hold: this table has no row size to guard it, and a shifted list would read as an empty one.
        var actFiles = DatContentColumns.Nearest(monsters, ActFilesOffset, offset => monsters.IsStringListColumn(offset, text => text.EndsWith(ActExtension, StringComparison.OrdinalIgnoreCase)));
        var aoFiles = DatContentColumns.Nearest(monsters, AoFilesOffset, offset => monsters.IsStringListColumn(offset, text => text.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase)));
        var grantedEffects = DatContentColumns.Nearest(monsters, GrantedEffectsOffset, offset => monsters.IsKeyListColumn(offset, skills.RowCount));
        var skillAnimation = DatContentColumns.Nearest(skills, SkillAnimationOffset, offset => skills.IsKeyColumn(offset, animations.RowCount));
        var mainHand = DatContentColumns.Nearest(monsters, MainHandClassOffset, offset => monsters.IsKeyColumn(offset, itemClasses.RowCount));
        if (actFiles == null || aoFiles == null || grantedEffects == null || skillAnimation == null || mainHand == null)
        {
            Say.Out.WriteLine("A MonsterVarieties or GrantedEffects column was not found again.");
            return 1;
        }

        var animationRows = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var row = 0; row < animations.RowCount; row++)
        {
            animationRows.TryAdd(animations.String(row, 0), row);
        }

        // A skill-table column not found would write a catalog with no speeds or threats: the previous one is kept instead.
        if (ProjectileSpeeds.Columns(index) == null)
        {
            return 1;
        }

        var actions = new AnimationActions(animations);
        var speeds = ProjectileSpeeds.Read(index, skills);
        var skillFlags = SkillFlags.Read(index, skills);
        var threats = SkillThreats.Read(index, skills, speeds);
        var reader = new MonsterAnimationFiles(path => files.TryGetValue(path.ToLowerInvariant(), out var file) ? file.Read().ToArray() : null);
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        var audit = new List<string> { "# monster\taction\tdata kind\tname kind\twhy (tools/Poe2UiExtract hit-times audit)" };
        var tally = new Tally();
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var id = monsters.StringOrNull(row, MonsterIdOffset);
            var act = monsters.StringList(row, actFiles.Value).FirstOrDefault();
            var ao = monsters.StringList(row, aoFiles.Value).FirstOrDefault();
            if (id == null || act == null || ao == null)
            {
                continue;
            }

            if (!act.EndsWith(".act", StringComparison.OrdinalIgnoreCase) || !ao.EndsWith(".ao", StringComparison.OrdinalIgnoreCase))
            {
                Say.Out.WriteLine($"MonsterVarieties columns moved (row {row}: '{act}', '{ao}'); find them again with dat-strings.");
                return 1;
            }

            // Each action and the skills that play it (Melee is every monster's basic attack).
            var skillsOf = new Dictionary<int, List<int>>();
            if (animationRows.TryGetValue(MeleeAction, out var melee))
            {
                skillsOf[melee] = new List<int>();
            }

            foreach (var skill in monsters.KeyList(row, grantedEffects.Value).Where(key => key >= 0 && key < skills.RowCount))
            {
                var animation = skills.Int64((int)skill, skillAnimation.Value);
                if (animation >= 0 && animation < animations.RowCount)
                {
                    if (!skillsOf.TryGetValue((int)animation, out var list))
                    {
                        list = new List<int>();
                        skillsOf[(int)animation] = list;
                    }

                    list.Add((int)skill);
                }
            }

            var actMap = reader.Act(act);
            var animated = reader.Ao(ao);
            var clipsData = animated.Amd == null ? new Dictionary<string, AmdClip>() : reader.Amd(animated.Amd);
            var played = actions.WithFollowUps(skillsOf.Keys);
            var flagsOf = Carried(skillsOf, actions, skill => skillFlags.GetValueOrDefault(skill) ?? Enumerable.Empty<string>());
            var kindsOf = Carried(skillsOf, actions, skill => new[] { SkillThreats.Name(threats.Of(skill).Verdict) });
            var boss = DodgeCoverage.IsBoss(id);
            var shoots = monsters.Key(row, mainHand.Value) is var weapon and >= 0 && weapon < itemClasses.RowCount &&
                         ShootingClasses.Contains(itemClasses.String((int)weapon, 0));
            foreach (var action in played)
            {
                var requested = skillsOf.ContainsKey(action);
                var name = animations.String(action, 0);
                var words = DodgeThreats.Of(action, value => value >= 0 && value < animations.RowCount ? animations.String(value, 0) : null);
                var verdict = KindOf(action, kindsOf, requested && skillsOf[action].Count == 0 && name == MeleeAction, shoots);
                var flags = Flags(verdict, flagsOf.GetValueOrDefault(action));
                if (requested && Disagrees(verdict, words))
                {
                    var why = string.Join("; ", skillsOf[action].Select(skill => $"{skills.String(skill, MonsterIdOffset)} ({threats.Of(skill).Reason})").DefaultIfEmpty("the basic attack"));
                    audit.Add($"{id}\t{name}\t{verdict}\t{words}\t{why}");
                }

                var (clips, resolved) = actions.Clips(actMap, action);
                if (verdict == SkillThreats.Verdict.Calm)
                {
                    // A buff's or a summon's "contact" is no blow: the kind alone is written.
                    lines.Add($"{id}|{action}=-{flags}");
                    tally.Calm(requested, boss, words);
                    continue;
                }

                if (clips.Count == 0)
                {
                    tally.NoAnimation(requested, boss, verdict, words);
                    AddUntimed(lines, id, action, flags);
                    continue;
                }

                var timed = clips.Select(clip => (Hit: HitOf(animated, clipsData, clip), Length: clipsData.GetValueOrDefault(clip)?.Length ?? 0))
                    .Where(c => c.Hit != null).Select(c => (c.Hit!.Value.Ms, c.Hit.Value.Source, c.Length)).ToList();
                if (timed.Count == 0)
                {
                    tally.Untimed(requested, boss, verdict, words, clips.Any(animated.EventClips.Contains));
                    AddUntimed(lines, id, action, flags);
                    continue;
                }

                var first = timed.MinBy(candidate => candidate.Ms);
                var speed = requested && skillsOf[action].Count > 0 && skillsOf[action].All(speeds.ContainsKey) ? skillsOf[action].Max(skill => speeds[skill]) : 0;
                var value = $"{first.Ms}/{first.Length}" + (speed > 0 ? $"/{speed}" : string.Empty);
                lines.Add($"{id}|{action}={value}{flags}");
                tally.Timed(requested, boss, verdict, words, first.Source, speed > 0, resolved != action);

                // Which of the two ids the Actor shows for a fallback is not traced: both get the time.
                if (resolved != action && !played.Contains(resolved))
                {
                    lines.Add($"{id}|{resolved}={value}{flags}");
                }
            }
        }

        if (auditFile != null)
        {
            File.WriteAllLines(auditFile, audit);
            Say.Out.WriteLine($"Wrote {auditFile}: {audit.Count - 1} attacks whose kind from the data disagrees with the animation's name.");
        }

        var header = new[]
        {
            "# When monster attacks land, from the game's animation data (tools/Poe2UiExtract hit-times), regenerated after patches.",
            "# monster path|animation id=ms from the attack's start to its blow (a projectile's release)/ms of one swing (0 unknown)",
            "# [/the projectile's speed in world units a second][;melee|ranged|calm: the kind the monster's skills give it, else the mod reads the",
            "# animation's name][,noblock,noparry: the player cannot block or parry it]; \"-\" untimed (always for a calm action).",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(lines));
        Say.Out.WriteLine($"Wrote {outFile}: {lines.Count} lines, {lines.Count(line => line.Contains(SkillFlags.NoBlock) || line.Contains(SkillFlags.NoParry))} attacks the player cannot block or parry.");
        tally.Print();
        return 0;
    }

    /// <summary>The tokens of the skills playing each action, carried to the actions played with it (the Miller's charge: ChargeStart, Charge, ChargeEnd).</summary>
    private static Dictionary<int, SortedSet<string>> Carried(Dictionary<int, List<int>> skillsOf, AnimationActions actions, Func<int, IEnumerable<string>> tokensOf)
    {
        var carried = new Dictionary<int, SortedSet<string>>();
        foreach (var (action, skills) in skillsOf)
        {
            var found = skills.SelectMany(tokensOf).ToList();
            foreach (var follow in found.Count == 0 ? new SortedSet<int>() : actions.WithFollowUps(new[] { action }))
            {
                if (!carried.TryGetValue(follow, out var set))
                {
                    set = new SortedSet<string>(StringComparer.Ordinal);
                    carried[follow] = set;
                }

                set.UnionWith(found);
            }
        }

        return carried;
    }

    /// <summary>
    ///     What an action's skills make of it; the basic Melee action, played by no granted skill, is the weapon's own attack: a
    ///     blow, or a shot for a bow or crossbow (a Decrepit Mercenary hit from 400-585 units, Ogham Farmlands 2026-11-08).
    /// </summary>
    private static SkillThreats.Verdict KindOf(int action, Dictionary<int, SortedSet<string>> kindsOf, bool basicMelee, bool shoots) =>
        basicMelee ? (shoots ? SkillThreats.Verdict.Ranged : SkillThreats.Verdict.Melee)
            : SkillThreats.Combine((kindsOf.GetValueOrDefault(action) ?? Enumerable.Empty<string>()).Select(SkillThreats.Parse));

    private static string Flags(SkillThreats.Verdict verdict, SortedSet<string>? flags)
    {
        var tokens = new List<string>();
        if (SkillThreats.Token(verdict) is { } kind)
        {
            tokens.Add(kind);
        }

        tokens.AddRange(flags ?? Enumerable.Empty<string>());
        return tokens.Count == 0 ? string.Empty : ";" + string.Join(',', tokens);
    }

    /// <summary>Whether the data's kind and the animation name's kind would warn differently.</summary>
    private static bool Disagrees(SkillThreats.Verdict verdict, DodgeThreat words) => verdict switch
    {
        SkillThreats.Verdict.Melee => words != DodgeThreat.Melee,
        SkillThreats.Verdict.Ranged => words != DodgeThreat.Ranged,
        SkillThreats.Verdict.Calm => words != DodgeThreat.None,
        _ => false,
    };

    /// <summary>An attack the files do not time is still written when the data gives it a kind or flags: a learnt blow times it later.</summary>
    private static void AddUntimed(SortedSet<string> lines, string id, int action, string flags)
    {
        if (flags.Length > 0)
        {
            lines.Add($"{id}|{action}=-{flags}");
        }
    }

    /// <summary>A table's rows still have the size its offsets were read at; else says what is left out.</summary>
    internal static bool Sized(DatTable? table, int rowSize, string name, string skipped)
    {
        if (table != null && table.RowSize == rowSize)
        {
            return true;
        }

        Say.Out.WriteLine($"{name} {(table == null ? "not found" : $"rows are 0x{table.RowSize:X} bytes, not 0x{rowSize:X}")}: {skipped} left out; " +
                          "find the columns again (dat-strings, dat-schema).");
        return false;
    }

    /// <summary>
    ///     One animation's blow: the .ao's "contact", else its Melee damage pattern's first key frame, else the "contact"
    ///     baked into the .amd; null when the files mark none.
    /// </summary>
    private static (int Ms, Source Source)? HitOf(AnimatedObject animated, IReadOnlyDictionary<string, AmdClip> clips, string clip)
    {
        var data = clips.GetValueOrDefault(clip);
        if (animated.Contact.TryGetValue(clip, out var contact))
        {
            return ((int)Math.Round(contact * MonsterAnimationFiles.MsPerSecond), Source.Contact);
        }

        if (animated.MeleeKeyFrame.TryGetValue(clip, out var keyFrame) && data is { Length: > 0 })
        {
            return ((int)Math.Round(keyFrame * data.Length), Source.DamagePattern);
        }

        return data?.Contact is { } baked ? (baked, Source.BakedContact) : null;
    }

    /// <summary>Counts per outcome, printed so a patch that breaks a source shows at once; bosses apart, the data's kind against the name's.</summary>
    private sealed class Tally
    {
        private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);

        public void Timed(bool requested, bool boss, SkillThreats.Verdict verdict, DodgeThreat words, Source source, bool speed, bool fallback)
        {
            this.Add(requested, boss, verdict, words, "timed");
            this.Add(requested, boss, verdict, words, $"by {source}");
            if (fallback)
            {
                this.Add(requested, boss, verdict, words, "through a fallback action");
            }

            if (speed)
            {
                this.Add(requested, boss, verdict, words, "with a projectile speed");
            }
        }

        public void Calm(bool requested, bool boss, DodgeThreat words) => this.Add(requested, boss, SkillThreats.Verdict.Calm, words, "calm");

        /// <summary>Neither the .act nor its fallbacks give the action an animation: not counted as an attack.</summary>
        public void NoAnimation(bool requested, bool boss, SkillThreats.Verdict verdict, DodgeThreat words) => this.Add(requested, boss, verdict, words, "no animation in the .act");

        public void Untimed(bool requested, bool boss, SkillThreats.Verdict verdict, DodgeThreat words, bool hasEvents) =>
            this.Add(requested, boss, verdict, words, hasEvents ? "untimed: server events, none a contact" : "untimed: no server event");

        public void Print()
        {
            foreach (var group in new[] { "attacks", "follow-ups", "boss attacks" })
            {
                foreach (var kind in Kinds)
                {
                    var prefix = $"{group}, {kind}|";
                    var parts = this.counts.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key[prefix.Length..]} {pair.Value}").ToList();
                    if (parts.Count > 0)
                    {
                        Say.Out.WriteLine($"{prefix[..^1]}: {string.Join(", ", parts)}");
                    }
                }
            }
        }

        private static readonly string[] Kinds =
        {
            "kind from the data, same by the name", "kind from the data, other by the name", "kind from the data, calm by the name",
            "calm by the data, calm by the name", "calm by the data, attack by the name", "kind from the name", "calm by the name",
        };

        private void Add(bool requested, bool boss, SkillThreats.Verdict verdict, DodgeThreat words, string what)
        {
            var kind = verdict switch
            {
                SkillThreats.Verdict.Calm => words == DodgeThreat.None ? Kinds[3] : Kinds[4],
                SkillThreats.Verdict.Mute => words == DodgeThreat.None ? Kinds[6] : Kinds[5],
                SkillThreats.Verdict.Melee => words == DodgeThreat.Melee ? Kinds[0] : words == DodgeThreat.None ? Kinds[2] : Kinds[1],
                _ => words == DodgeThreat.Ranged ? Kinds[0] : words == DodgeThreat.None ? Kinds[2] : Kinds[1],
            };
            foreach (var group in boss && requested ? new[] { "attacks", "boss attacks" } : new[] { requested ? "attacks" : "follow-ups" })
            {
                var key = $"{group}, {kind}|{what}";
                this.counts[key] = this.counts.GetValueOrDefault(key) + 1;
            }
        }
    }
}
