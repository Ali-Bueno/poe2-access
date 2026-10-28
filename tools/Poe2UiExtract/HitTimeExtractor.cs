using Poe2Accessibility.Services;
using Index = LibBundle3.Index;

/// <summary>
///     `hit-times` command: when each monster attack lands, from the game's own animation data, so the mod can warn
///     before an attack's first blow (sources and checks: docs/research/dodge-timing.md). For every MonsterVarieties row:
///     - its actions: "Melee" and the animation of each granted skill (GrantedEffects +0x71 → Animation row), plus the
///       actions the Animation table plays with them (<see cref="AnimationActions"/>);
///     - each action's animations from the monster's .act, through the Animation table's fallbacks when it lacks one;
///     - each animation's hit (<see cref="HitOf"/>), the earliest of the action winning;
///     - for a skill that fires a projectile, its speed (<see cref="ProjectileSpeeds"/>), so the mod adds its flight.
///     Written as "monster path|animation id=hit ms/animation ms[/projectile speed]" lines; the Actor's animation id is the
///     action's row in Animation.datc64.
/// </summary>
internal static class HitTimeExtractor
{
    // MonsterVarieties: Id, ACTFiles, AOFiles, GrantedEffects (found with dat-strings; the community dat-schema's names).
    private const int MonsterIdOffset = 0x00;
    private const int ActFilesOffset = 0x28;
    private const int AoFilesOffset = 0x38;
    private const int GrantedEffectsOffset = 0xD0;

    // GrantedEffects: the skill's Animation row (inferred from 8 rows).
    private const int SkillAnimationOffset = 0x71;

    private const string MeleeAction = "Melee";

    private enum Source
    {
        Contact,
        DamagePattern,
        BakedContact,
    }

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var files = index.Files.Values.Where(f => f.Path != null).GroupBy(f => f.Path!.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        var monsters = DatTable.Open(index, "monstervarieties");
        var skills = DatTable.Open(index, "grantedeffects");
        var animations = DatTable.Open(index, "animation");
        if (monsters == null || skills == null || animations == null)
        {
            Console.WriteLine("MonsterVarieties, GrantedEffects or Animation not found.");
            return 1;
        }

        var animationRows = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var row = 0; row < animations.RowCount; row++)
        {
            animationRows.TryAdd(animations.String(row, 0), row);
        }

        var actions = new AnimationActions(animations);
        var speeds = ProjectileSpeeds.Read(index, skills);
        var reader = new MonsterAnimationFiles(path => files.TryGetValue(path.ToLowerInvariant(), out var file) ? file.Read().ToArray() : null);
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        var tally = new Tally();
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var id = monsters.StringOrNull(row, MonsterIdOffset);
            var act = monsters.StringList(row, ActFilesOffset).FirstOrDefault();
            var ao = monsters.StringList(row, AoFilesOffset).FirstOrDefault();
            if (id == null || act == null || ao == null)
            {
                continue;
            }

            if (!act.EndsWith(".act", StringComparison.OrdinalIgnoreCase) || !ao.EndsWith(".ao", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"MonsterVarieties columns moved (row {row}: '{act}', '{ao}'); find them again with dat-strings.");
                return 1;
            }

            // Each action and the skills that play it (Melee is every monster's basic attack).
            var skillsOf = new Dictionary<int, List<int>>();
            if (animationRows.TryGetValue(MeleeAction, out var melee))
            {
                skillsOf[melee] = new List<int>();
            }

            foreach (var skill in monsters.KeyList(row, GrantedEffectsOffset).Where(key => key >= 0 && key < skills.RowCount))
            {
                var animation = skills.Int64((int)skill, SkillAnimationOffset);
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
            foreach (var action in played)
            {
                var requested = skillsOf.ContainsKey(action);
                var (clips, resolved) = actions.Clips(actMap, action);
                var warned = DodgeThreats.Of(action, value => value >= 0 && value < animations.RowCount ? animations.String(value, 0) : null) != DodgeThreat.None;
                if (clips.Count == 0)
                {
                    tally.NoAnimation(requested, warned);
                    continue;
                }

                var timed = clips.Select(clip => (Hit: HitOf(animated, clipsData, clip), Length: clipsData.GetValueOrDefault(clip)?.Length ?? 0))
                    .Where(c => c.Hit != null).Select(c => (c.Hit!.Value.Ms, c.Hit.Value.Source, c.Length)).ToList();
                if (timed.Count == 0)
                {
                    tally.Untimed(requested, warned, clips.Any(animated.EventClips.Contains));
                    continue;
                }

                var first = timed.MinBy(candidate => candidate.Ms);
                var speed = requested && skillsOf[action].Count > 0 && skillsOf[action].All(speeds.ContainsKey) ? skillsOf[action].Max(skill => speeds[skill]) : 0;
                var value = $"{first.Ms}/{first.Length}" + (speed > 0 ? $"/{speed}" : string.Empty);
                lines.Add($"{id}|{action}={value}");
                tally.Timed(requested, warned, first.Source, speed > 0, resolved != action);

                // Which of the two ids the Actor shows for a fallback is not traced: both get the time.
                if (resolved != action && !played.Contains(resolved))
                {
                    lines.Add($"{id}|{resolved}={value}");
                }
            }
        }

        var header = new[]
        {
            "# When monster attacks land, from the game's animation data (tools/Poe2UiExtract hit-times), regenerated after patches.",
            "# monster path|animation id=ms from the attack's start to its blow (a projectile's release)/ms of one swing (0 unknown)",
            "# [/the projectile's speed in world units a second].",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(lines));
        Console.WriteLine($"Wrote {outFile}: {lines.Count} lines.");
        tally.Print();
        return 0;
    }

    /// <summary>A table's rows still have the size its offsets were read at; else says what is left out.</summary>
    internal static bool Sized(DatTable? table, int rowSize, string name, string skipped)
    {
        if (table != null && table.RowSize == rowSize)
        {
            return true;
        }

        Console.WriteLine($"{name} {(table == null ? "not found" : $"rows are 0x{table.RowSize:X} bytes, not 0x{rowSize:X}")}: {skipped} left out; " +
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

    /// <summary>Counts per outcome, printed so a patch that breaks a source shows at once.</summary>
    private sealed class Tally
    {
        private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);

        public void Timed(bool requested, bool warned, Source source, bool speed, bool fallback)
        {
            this.Add(requested, warned, "timed");
            this.Add(requested, warned, $"by {source}");
            if (fallback)
            {
                this.Add(requested, warned, "through a fallback action");
            }

            if (speed)
            {
                this.Add(requested, warned, "with a projectile speed");
            }
        }

        /// <summary>Neither the .act nor its fallbacks give the action an animation: not counted as an attack.</summary>
        public void NoAnimation(bool requested, bool warned) => this.Add(requested, warned, "no animation in the .act");

        public void Untimed(bool requested, bool warned, bool hasEvents) =>
            this.Add(requested, warned, hasEvents ? "untimed: server events, none a contact" : "untimed: no server event");

        public void Print()
        {
            foreach (var group in new[] { "attacks", "follow-ups" })
            {
                foreach (var warned in new[] { "warned of", "calm" })
                {
                    var prefix = $"{group}, {warned}|";
                    var parts = this.counts.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key[prefix.Length..]} {pair.Value}");
                    Console.WriteLine($"{prefix[..^1]}: {string.Join(", ", parts)}");
                }
            }
        }

        private void Add(bool requested, bool warned, string what)
        {
            var key = $"{(requested ? "attacks" : "follow-ups")}, {(warned ? "warned of" : "calm")}|{what}";
            this.counts[key] = this.counts.GetValueOrDefault(key) + 1;
        }
    }
}
