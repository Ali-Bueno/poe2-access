using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `hit-times` command: when each monster attack lands, from the game's own animation data, so the mod can warn
///     before an attack's first blow (docs/research/dodge-timing.md). For every MonsterVarieties row:
///     - its actions: "Melee" and the animation of each granted skill (GrantedEffects +0x71 → Animation row);
///     - the action's animation names from the monster's .act ("GroundSlam = \"ground_slam_01\";", "@Alias" followed);
///     - each animation's hit: the "contact" event time of the .ao's AnimationController, else the first damage key
///       frame of its Melee section times the animation length from the .amd; the earliest of the action wins.
///     The Actor's animation id is the action's row in Animation.datc64. Written as
///     "monster path|animation id=hit ms/animation ms" lines. Checked against blows timed in game: mud golem slam 1169
///     (1156–1172 seen), Bloated Miller slam 1803 (1781–1828), larva melee 430 (437–469).
/// </summary>
internal static class HitTimeExtractor
{
    private const int MonsterIdOffset = 0x00;
    private const int ActFilesOffset = 0x28;
    private const int AoFilesOffset = 0x38;
    private const int GrantedEffectsOffset = 0xD0;
    private const int SkillAnimationOffset = 0x71;
    private const string ContactEvent = "contact";
    private const int MaxAliasDepth = 4;

    private static readonly Regex Comment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex Stance = new(@"stance\s+\w+\s*\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Regex Assignment = new("(\\w+)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex Extends = new("extends\\s+\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Controller = new(@"\nAnimationController\s*\{(.*?)\n\}", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Metadata = new("metadata\\s*=\\s*\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Animations = new(@"animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex MeleeSection = new(@"\nMelee\s*\{\s*animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.Open(Path.Combine(gameDir, "Bundles2", "_.index.bin"));
        index.ParsePaths();
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

        var reader = new GameFiles(path => files.TryGetValue(path.ToLowerInvariant(), out var file) ? file.Read().ToArray() : null);
        var lines = new List<string>();
        int actions = 0, timed = 0;
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

            var names = new SortedSet<string>(StringComparer.Ordinal) { "Melee" };
            foreach (var skill in monsters.KeyList(row, GrantedEffectsOffset).Where(key => key >= 0 && key < skills.RowCount))
            {
                var animation = skills.Int64((int)skill, SkillAnimationOffset);
                if (animation >= 0 && animation < animations.RowCount)
                {
                    names.Add(animations.String((int)animation, 0));
                }
            }

            var actMap = reader.Act(act);
            var animated = reader.Ao(ao);
            var lengths = animated.Amd == null ? new Dictionary<string, int>() : reader.Amd(animated.Amd);
            foreach (var action in names)
            {
                if (!animationRows.TryGetValue(action, out var animationId))
                {
                    continue;
                }

                var candidates = new List<(int Hit, int Length)>();
                foreach (var clip in Clips(actMap, action, 0))
                {
                    var length = lengths.GetValueOrDefault(clip);
                    if (animated.Contact.TryGetValue(clip, out var contact))
                    {
                        candidates.Add(((int)Math.Round(contact * 1000), length));
                    }
                    else if (animated.MeleeKeyFrame.TryGetValue(clip, out var keyFrame) && length > 0)
                    {
                        candidates.Add(((int)Math.Round(keyFrame * length), length));
                    }
                }

                actions += Clips(actMap, action, 0).Any() ? 1 : 0;
                if (candidates.Count == 0)
                {
                    continue;
                }

                var first = candidates.MinBy(candidate => candidate.Hit);
                timed++;
                lines.Add($"{id}|{animationId}={first.Hit}/{first.Length}");
            }
        }

        var header = new[]
        {
            "# When monster attacks land, from the game's animation data (tools/Poe2UiExtract hit-times), regenerated after patches.",
            "# monster path|animation id=ms from the attack's start to its blow/ms of one swing (0 unknown).",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(lines.Distinct().OrderBy(line => line, StringComparer.Ordinal)));
        Console.WriteLine($"Wrote {outFile}: {timed} of {actions} monster attacks with animations timed.");
        return 0;
    }

    /// <summary>The animation names an action plays ("attack_01,attack_02"), aliases ("@Melee") followed.</summary>
    private static IEnumerable<string> Clips(IReadOnlyDictionary<string, string> act, string action, int depth)
    {
        if (depth > MaxAliasDepth || !act.TryGetValue(action, out var value))
        {
            yield break;
        }

        foreach (var part in value.Split(','))
        {
            var name = part.Split(':')[0].Trim();
            if (name.StartsWith('@'))
            {
                foreach (var aliased in Clips(act, name[1..], depth + 1))
                {
                    yield return aliased;
                }
            }
            else if (name.Length > 0)
            {
                yield return name.ToLowerInvariant();
            }
        }
    }

    private sealed record Animated(string? Amd, Dictionary<string, double> Contact, Dictionary<string, double> MeleeKeyFrame);

    /// <summary>The monster files read once each: .act action maps, .ao events (with their parents), .amd lengths.</summary>
    private sealed class GameFiles
    {
        private readonly Func<string, byte[]?> read;
        private readonly Dictionary<string, Dictionary<string, string>> acts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Animated> aos = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, int>> amds = new(StringComparer.OrdinalIgnoreCase);

        public GameFiles(Func<string, byte[]?> read) => this.read = read;

        public Dictionary<string, string> Act(string path)
        {
            if (this.acts.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (this.Text(path) is { } text)
            {
                text = Comment.Replace(text, string.Empty);
                // The default stance first; a stance block's own definitions only fill what it lacks.
                foreach (var source in new[] { Stance.Replace(text, string.Empty), text })
                {
                    foreach (Match match in Assignment.Matches(source))
                    {
                        map.TryAdd(match.Groups[1].Value, match.Groups[2].Value);
                    }
                }
            }

            this.acts[path] = map;
            return map;
        }

        public Animated Ao(string path, int depth = 0)
        {
            if (this.aos.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var result = new Animated(null, new Dictionary<string, double>(), new Dictionary<string, double>());
            if (this.Text(path) is { } text)
            {
                if (Extends.Match(text) is { Success: true } parent && parent.Groups[1].Value != "Metadata/Parent" && depth < MaxAliasDepth)
                {
                    var inherited = this.Ao(parent.Groups[1].Value.EndsWith(".ao", StringComparison.OrdinalIgnoreCase) ? parent.Groups[1].Value : parent.Groups[1].Value + ".ao", depth + 1);
                    result = new Animated(inherited.Amd, new Dictionary<string, double>(inherited.Contact), new Dictionary<string, double>(inherited.MeleeKeyFrame));
                }

                if (Controller.Match(text) is { Success: true } controller)
                {
                    if (Metadata.Match(controller.Groups[1].Value) is { Success: true } amd)
                    {
                        result = result with { Amd = amd.Groups[1].Value };
                    }

                    if (Animations.Match(controller.Groups[1].Value) is { Success: true } list)
                    {
                        ForEachAnimation(list.Groups[1].Value, (name, animation) =>
                        {
                            if (animation.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                            {
                                var times = events.EnumerateArray()
                                    .Where(e => e.TryGetProperty("name", out var n) && n.GetString() == ContactEvent && e.TryGetProperty("time", out _))
                                    .Select(e => e.GetProperty("time").GetDouble()).ToList();
                                if (times.Count > 0)
                                {
                                    result.Contact[name] = times.Min();
                                }
                            }
                        });
                    }
                }

                if (MeleeSection.Match(text) is { Success: true } melee)
                {
                    ForEachAnimation(melee.Groups[1].Value, (name, animation) =>
                    {
                        if (animation.TryGetProperty("melee_data", out var data) && data.TryGetProperty("damage_patterns", out var patterns))
                        {
                            var frames = patterns.EnumerateArray()
                                .Where(p => p.TryGetProperty("key_frames", out var k) && k.GetArrayLength() > 0)
                                .Select(p => p.GetProperty("key_frames")[0][0].GetDouble()).ToList();
                            if (frames.Count > 0)
                            {
                                result.MeleeKeyFrame[name] = frames.Min();
                            }
                        }
                    });
                }
            }

            this.aos[path] = result;
            return result;
        }

        /// <summary>Animation lengths in ms: a quoted name, then two lines on, the length.</summary>
        public Dictionary<string, int> Amd(string path)
        {
            if (this.amds.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var lengths = new Dictionary<string, int>(StringComparer.Ordinal);
            if (this.Text(path) is { } text)
            {
                var lines = text.Split('\n');
                for (var i = 0; i + 2 < lines.Length; i++)
                {
                    if (lines[i].StartsWith('"') && int.TryParse(lines[i + 2].Trim(), out var ms))
                    {
                        lengths.TryAdd(lines[i].Trim().Trim('"').ToLowerInvariant(), ms);
                    }
                }
            }

            this.amds[path] = lengths;
            return lengths;
        }

        private static void ForEachAnimation(string json, Action<string, JsonElement> visit)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var animation in document.RootElement.EnumerateArray())
                {
                    if (animation.TryGetProperty("name", out var name) && name.GetString() is { } text)
                    {
                        visit(text.ToLowerInvariant(), animation);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
            {
                // A malformed list leaves this file's animations untimed.
            }
        }

        private string? Text(string path)
        {
            return this.read(path) is { } bytes ? Encoding.Unicode.GetString(bytes).TrimStart('\uFEFF') : null;
        }
    }
}
