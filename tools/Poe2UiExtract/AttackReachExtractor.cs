using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `attack-reach` command: how far a monster's blow reaches, per animation clip, for the dodge warning
///     (docs/research/attack-telegraphs.md). Every monster `.ao` (parents followed through `extends`, a child's clip
///     replacing its parent's of the same name) gives two numbers: the farthest reach of the clip's Melee damage patterns
///     from the monster's centre, and the distance of its "contact" events' position, where the hit lands.
///     Line: `.ao path (lower case)|clip=reach/impact`, both whole game units, 0 when the file has no such value.
/// </summary>
internal static class AttackReachExtractor
{
    private const string Folder = "metadata/monsters/";
    private const string AoExtension = ".ao";
    private const string ContactEvent = "contact";
    private const string RectangleShape = "Rectangle";
    private const string RootParent = "Metadata/Parent";
    private const double MaxLength = 5000;
    private const int MaxParentDepth = 6;

    private static readonly Regex Extends = new("extends\\s+\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Controller = new(@"\nAnimationController\s*\{(.*?)\n\}", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Animations = new(@"animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);

    // Settings such as "offset_scale = 1.2" may come before the list.
    private static readonly Regex MeleeSection = new(@"\nMelee\s*\{[^{}']*?animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>One .ao's own clips: melee reach and contact distance by clip name (lower case), before its parents'.</summary>
    internal sealed record ClipValues(Dictionary<string, double> Reach, Dictionary<string, double> Impact);

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var files = BundleFiles.ByLowerPath(index);
        var own = new Dictionary<string, (string? Parent, ClipValues Values)>(StringComparer.Ordinal);
        (string? Parent, ClipValues Values) Own(string path)
        {
            if (!own.TryGetValue(path, out var cached))
            {
                var text = files.TryGetValue(path, out var file) ? Encoding.Unicode.GetString(file.Read().ToArray()).TrimStart('﻿') : null;
                own[path] = cached = text == null ? (null, new ClipValues(new(), new())) : (ParentOf(text), Parse(text));
            }

            return cached;
        }

        var lines = new SortedSet<string>(StringComparer.Ordinal);
        int fileCount = 0, withLine = 0, melee = 0;
        foreach (var path in files.Keys.Where(p => p.StartsWith(Folder, StringComparison.Ordinal) && p.EndsWith(AoExtension, StringComparison.Ordinal)))
        {
            var reach = new Dictionary<string, double>(StringComparer.Ordinal);
            var impact = new Dictionary<string, double>(StringComparer.Ordinal);
            var chain = new List<string>();
            for (string? at = path; at != null && chain.Count <= MaxParentDepth && !chain.Contains(at); at = Own(at).Parent)
            {
                chain.Add(at);
            }

            for (var i = chain.Count - 1; i >= 0; i--) // the farthest parent first, so each child overrides
            {
                var values = Own(chain[i]).Values;
                foreach (var (clip, value) in values.Reach)
                {
                    reach[clip] = value;
                }

                foreach (var (clip, value) in values.Impact)
                {
                    impact[clip] = value;
                }
            }

            fileCount++;
            var clips = reach.Keys.Union(impact.Keys, StringComparer.Ordinal).ToList();
            withLine += clips.Count > 0 ? 1 : 0;
            melee += reach.Count;
            foreach (var clip in clips)
            {
                lines.Add($"{path}|{clip}={Whole(reach.GetValueOrDefault(clip))}/{Whole(impact.GetValueOrDefault(clip))}");
            }
        }

        if (lines.Count == 0)
        {
            Say.Out.WriteLine("No monster .ao file gave a melee pattern or a contact position (docs/research/attack-telegraphs.md).");
            return 1;
        }

        var header = new[]
        {
            "# How far a monster's blow reaches, per animation clip (tools/Poe2UiExtract attack-reach), regenerated after patches.",
            "# .ao path (lower case)|clip name (lower case)=melee reach/impact distance, game units: the farthest point of the clip's Melee",
            "# damage patterns from the monster's centre (offset + length; a rectangle's far corner), and the distance of its \"contact\" event's position; 0 when absent.",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllLines(outFile, header.Concat(lines));
        Say.Out.WriteLine($"Wrote {outFile}: {lines.Count} clips in {withLine} of {fileCount} .ao files ({melee} with a melee pattern).");
        return 0;
    }

    /// <summary>The .ao named by `extends`, as a lower case path with its extension; null for none or the root.</summary>
    internal static string? ParentOf(string text)
    {
        if (Extends.Match(text) is not { Success: true } match || match.Groups[1].Value == RootParent)
        {
            return null;
        }

        var parent = match.Groups[1].Value.ToLowerInvariant();
        return parent.EndsWith(AoExtension, StringComparison.Ordinal) ? parent : parent + AoExtension;
    }

    /// <summary>A file's own clips: Melee damage pattern reach and AnimationController contact distance.</summary>
    internal static ClipValues Parse(string text)
    {
        var result = new ClipValues(new Dictionary<string, double>(StringComparer.Ordinal), new Dictionary<string, double>(StringComparer.Ordinal));
        if (MeleeSection.Match(text) is { Success: true } melee)
        {
            ForEachClip(melee.Groups[1].Value, (name, clip) =>
            {
                if (clip.TryGetProperty("melee_data", out var data) && data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("damage_patterns", out var patterns) && patterns.ValueKind == JsonValueKind.Array)
                {
                    var reach = patterns.EnumerateArray().Select(Reach).Where(value => value.HasValue).Select(value => value!.Value).ToList();
                    if (reach.Count > 0)
                    {
                        result.Reach[name] = reach.Max();
                    }
                }
            });
        }

        if (Controller.Match(text) is { Success: true } controller && Animations.Match(controller.Groups[1].Value) is { Success: true } list)
        {
            ForEachClip(list.Groups[1].Value, (name, clip) =>
            {
                if (clip.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                {
                    var distances = events.EnumerateArray().Select(Contact).Where(value => value.HasValue).Select(value => value!.Value).ToList();
                    if (distances.Count > 0)
                    {
                        result.Impact[name] = distances.Max();
                    }
                }
            });
        }

        return result;
    }

    /// <summary>A damage pattern's farthest reach from the monster's centre; null for a pattern with no usable length.</summary>
    private static double? Reach(JsonElement pattern)
    {
        if (pattern.ValueKind != JsonValueKind.Object || Number(pattern, "length") is not { } length || length <= 0 || length > MaxLength)
        {
            return null;
        }

        var offset = 0.0;
        if (pattern.TryGetProperty("offset", out var at) && at.ValueKind == JsonValueKind.Array && at.GetArrayLength() >= 2 &&
            at[0].ValueKind == JsonValueKind.Number && at[1].ValueKind == JsonValueKind.Number &&
            at[0].TryGetDouble(out var x) && at[1].TryGetDouble(out var y) && double.IsFinite(x) && double.IsFinite(y))
        {
            offset = Math.Sqrt((x * x) + (y * y));
        }

        var rectangle = pattern.TryGetProperty("shape", out var shape) && shape.ValueKind == JsonValueKind.String && shape.GetString() == RectangleShape;
        var half = rectangle ? (Number(pattern, "width") ?? 0) / 2 : 0;
        var far = offset + (rectangle ? Math.Sqrt((length * length) + (half * half)) : length);
        return double.IsFinite(far) ? far : null;
    }

    private static double? Contact(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() != ContactEvent ||
            !e.TryGetProperty("position", out var at) || at.ValueKind != JsonValueKind.Array || at.GetArrayLength() < 2 ||
            at[0].ValueKind != JsonValueKind.Number || at[1].ValueKind != JsonValueKind.Number || !at[0].TryGetDouble(out var x) || !at[1].TryGetDouble(out var y))
        {
            return null;
        }

        var distance = Math.Sqrt((x * x) + (y * y));
        return double.IsFinite(distance) ? distance : null;
    }

    private static double? Number(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static string Whole(double value) => Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

    private static void ForEachClip(string json, Action<string, JsonElement> visit)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var clip in document.RootElement.EnumerateArray())
            {
                if (clip.ValueKind == JsonValueKind.Object && clip.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() is { } text)
                {
                    visit(text.ToLowerInvariant(), clip);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
        {
            // A malformed list leaves this file's clips out.
        }
    }
}
