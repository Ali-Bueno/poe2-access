using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
///     An .ao's AnimationController: its .amd, each animation's first "contact" (s), the first damage key frame of its
///     Melee section (fraction of the animation), and which animations have any event at all.
/// </summary>
internal sealed record AnimatedObject(string? Amd, Dictionary<string, double> Contact, Dictionary<string, double> MeleeKeyFrame, HashSet<string> EventClips);

/// <summary>One animation of an .amd: its length (ms) and the "contact" baked into it (ms), if any.</summary>
internal sealed record AmdClip(int Length, int? Contact);

/// <summary>A monster's animation files, each read once: .act action maps, .ao events (with their parents), .amd animations.</summary>
internal sealed class MonsterAnimationFiles
{
    public const int MaxAliasDepth = 4;

    public const double MsPerSecond = 1000;

    private const string ContactEvent = "contact";

    private static readonly Regex Comment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex Stance = new(@"stance\s+\w+\s*\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Regex Assignment = new("(\\w+)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex Extends = new("extends\\s+\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Controller = new(@"\nAnimationController\s*\{(.*?)\n\}", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Metadata = new("metadata\\s*=\\s*\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Animations = new(@"animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);

    // Settings such as "offset_scale = 1.2" may come before the list.
    private static readonly Regex MeleeSection = new(@"\nMelee\s*\{[^{}']*?animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly Func<string, byte[]?> read;
    private readonly Dictionary<string, Dictionary<string, string>> acts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimatedObject> aos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AmdClip>> amds = new(StringComparer.OrdinalIgnoreCase);

    public MonsterAnimationFiles(Func<string, byte[]?> read) => this.read = read;

    /// <summary>The animation names an action plays ("attack_01,attack_02"), aliases ("@Melee") followed.</summary>
    public static IEnumerable<string> Clips(IReadOnlyDictionary<string, string> act, string action, int depth = 0)
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

    /// <summary>An .act's actions ("GroundSlam = \"ground_slam_01\";"), the default stance's first.</summary>
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

    public AnimatedObject Ao(string path, int depth = 0)
    {
        if (this.aos.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var result = new AnimatedObject(null, new Dictionary<string, double>(), new Dictionary<string, double>(), new HashSet<string>());
        if (this.Text(path) is { } text)
        {
            if (Extends.Match(text) is { Success: true } parent && parent.Groups[1].Value != "Metadata/Parent" && depth < MaxAliasDepth)
            {
                var inherited = this.Ao(parent.Groups[1].Value.EndsWith(".ao", StringComparison.OrdinalIgnoreCase) ? parent.Groups[1].Value : parent.Groups[1].Value + ".ao", depth + 1);
                result = new AnimatedObject(inherited.Amd, new Dictionary<string, double>(inherited.Contact), new Dictionary<string, double>(inherited.MeleeKeyFrame), new HashSet<string>(inherited.EventClips));
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
                            if (events.GetArrayLength() > 0)
                            {
                                result.EventClips.Add(name);
                            }

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

    /// <summary>
    ///     An .amd's animations (versions 1–5 alike): a quoted name, "once" or "loop", the length in ms, the count of baked
    ///     events, then per event its quoted name, its time in ms and a position line.
    /// </summary>
    public Dictionary<string, AmdClip> Amd(string path)
    {
        if (this.amds.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var clips = new Dictionary<string, AmdClip>(StringComparer.Ordinal);
        if (this.Text(path) is { } text)
        {
            var lines = text.Split('\n');
            for (var i = 0; i + 3 < lines.Length; i++)
            {
                if (!lines[i].StartsWith('"') || !int.TryParse(lines[i + 2].Trim(), out var length))
                {
                    continue;
                }

                int? contact = null;
                if (int.TryParse(lines[i + 3].Trim(), out var events))
                {
                    for (var e = 0; e < events && i + 5 + (3 * e) < lines.Length; e++)
                    {
                        var at = i + 4 + (3 * e);
                        if (lines[at].Trim().Trim('"') == ContactEvent &&
                            double.TryParse(lines[at + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
                        {
                            contact = Math.Min(contact ?? int.MaxValue, (int)Math.Round(ms));
                        }
                    }
                }

                clips.TryAdd(lines[i].Trim().Trim('"').ToLowerInvariant(), new AmdClip(length, contact));
            }
        }

        this.amds[path] = clips;
        return clips;
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
