using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>A named event of a clip's AnimationController list ("combo_slam" at 2003 ms): what the server's AI can act on.</summary>
internal readonly record struct ClipEvent(string Name, int Ms);

/// <summary>
///     Per clip of an .ao: the effect files its client section plays (AnimatedObject, ParticleEffect, EffectPack, Trails events:
///     the clip's own art) and the named events of its AnimationController list (docs/research/boss-mechanics.md, Lythara).
/// </summary>
internal sealed record ClipEffects(Dictionary<string, HashSet<string>> Effects, Dictionary<string, List<ClipEvent>> Events)
{
    private const string StandardEvent = "StandardEventType";
    private const string EffectRoot = "Metadata/Effects/";
    private const string ClientSection = "\nclient";

    private static readonly Regex AnyAnimations = new(@"animations\s*=\s*'(\[.*?\])'", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>An .ao's clips, those of the .ao it extends first (as <see cref="MonsterAnimationFiles.Ao"/> follows them).</summary>
    public static ClipEffects Read(string path, Func<string, string?> textOf, int depth = 0)
    {
        var result = new ClipEffects(new Dictionary<string, HashSet<string>>(StringComparer.Ordinal), new Dictionary<string, List<ClipEvent>>(StringComparer.Ordinal));
        if (textOf(path) is not { } text)
        {
            return result;
        }

        if (MonsterAnimationFiles.Extends.Match(text) is { Success: true } parent && parent.Groups[1].Value != "Metadata/Parent" && depth < MonsterAnimationFiles.MaxAliasDepth)
        {
            var from = parent.Groups[1].Value;
            var inherited = Read(from.EndsWith(".ao", StringComparison.OrdinalIgnoreCase) ? from : from + ".ao", textOf, depth + 1);
            foreach (var (clip, files) in inherited.Effects)
            {
                result.Effects[clip] = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var (clip, events) in inherited.Events)
            {
                result.Events[clip] = new List<ClipEvent>(events);
            }
        }

        result.Add(text);
        return result;
    }

    /// <summary>One .ao's own text over what it inherited: a clip it lists anew replaces the inherited one's events.</summary>
    internal void Add(string text)
    {
        if (MonsterAnimationFiles.Controller.Match(text) is { Success: true } controller && MonsterAnimationFiles.Animations.Match(controller.Groups[1].Value) is { Success: true } list)
        {
            ForEach(list.Groups[1].Value, (clip, events) => this.Events[clip] = events
                .Where(e => Text(e, "type") == StandardEvent && Text(e, "name") is { Length: > 0 } && e.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Number)
                .Select(e => new ClipEvent(Text(e, "name")!, (int)Math.Round(e.GetProperty("time").GetDouble() * MonsterAnimationFiles.MsPerSecond))).ToList());
        }

        var client = text.IndexOf(ClientSection, StringComparison.Ordinal);
        if (client < 0)
        {
            return;
        }

        foreach (Match block in AnyAnimations.Matches(text, client))
        {
            ForEach(block.Groups[1].Value, (clip, events) =>
            {
                foreach (var file in events.SelectMany(e => new[] { Text(e, "ao_type"), Text(e, "filename") }).OfType<string>()
                             .Where(file => file.StartsWith(EffectRoot, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!this.Effects.TryGetValue(clip, out var files))
                    {
                        this.Effects[clip] = files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    files.Add(file);
                }
            });
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void ForEach(string json, Action<string, List<JsonElement>> visit)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var animation in document.RootElement.EnumerateArray())
            {
                if (Text(animation, "name") is { } name)
                {
                    var events = animation.TryGetProperty("events", out var list) && list.ValueKind == JsonValueKind.Array
                        ? list.EnumerateArray().Select(e => e.Clone()).ToList()
                        : new List<JsonElement>();
                    visit(name.ToLowerInvariant(), events);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            // A malformed list leaves its clips without effects or events.
        }
    }
}
