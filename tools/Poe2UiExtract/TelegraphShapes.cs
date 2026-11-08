using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     The shapes the game's skill tables and effect files give an attack's danger area, and the catalog lines the
///     telegraph extractor writes (docs/research/attack-telegraphs.md). Pure: shared with the tests and, later, the mod.
/// </summary>
internal static class TelegraphShapes
{
    public const char FieldSeparator = '|';
    public const char ItemSeparator = ',';
    public const char PairSeparator = ';';
    public const string Circle = "circle";
    public const string Cone = "cone";
    public const string Rectangle = "rectangle";
    public const string Donut = "donut";
    public const string Triangle = "triangle";
    public const double MsPerSecond = 1000;

    /// <summary>GeometryAttack's shape column: 1 circle (novas, slams), 2 cone (breaths, cleaves), 3 rectangle (slashes, beams), 4 donut, 5 triangle (one row).</summary>
    private static readonly string[] GeometryShapes = { string.Empty, Circle, Cone, Rectangle, Donut, Triangle };

    /// <summary>The ActiveSkills kinds whose own table describes a danger area, and the catalog kind each gets.</summary>
    private static readonly (string Prefix, string Kind)[] Kinds =
    {
        ("geometry_attack", "geometry"), ("geometry_spell", "geometry"), ("geometry_trigger", "trigger"),
        ("effect_driven", "effect"), ("spawn_object", "spawn"), ("monster_mortar", "mortar"), ("geometry_mortars", "mortar"),
    };

    /// <summary>The catalog kind of an ActiveSkills Id, or null for a skill whose table says nothing about a danger area.</summary>
    public static string? KindOf(string activeSkillId) =>
        Kinds.FirstOrDefault(kind => activeSkillId.StartsWith(kind.Prefix, StringComparison.Ordinal)).Kind;

    private static readonly Regex EventBlock = new(@"\nEffectDrivenEvent\s*\{(.*?)\n\}", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Assignment = new("^\\s*(\\w+)\\s*=\\s*\"?([^\"\\r]*?)\"?\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>The name of a GeometryAttack shape value, or null for one the table has not used before.</summary>
    public static string? GeometryShape(int value) => value > 0 && value < GeometryShapes.Length ? GeometryShapes[value] : null;

    /// <summary>
    ///     The danger shapes an effect .ao's server-side EffectDrivenEvent block lists: each "shape" line starts one, the
    ///     lines after it are its settings (time in seconds from the effect's start, length, width, angle...), and an
    ///     "animation" line names the animation the following shapes belong to.
    /// </summary>
    public static IReadOnlyList<EffectShape> ParseEffectDrivenEvents(string aoText)
    {
        var shapes = new List<EffectShape>();
        if (EventBlock.Match(aoText) is not { Success: true } block)
        {
            return shapes;
        }

        var animation = string.Empty;
        EffectShape? current = null;
        foreach (Match match in Assignment.Matches(block.Groups[1].Value))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();
            switch (key)
            {
                case "animation":
                    animation = value;
                    break;
                case "shape":
                    current = new EffectShape(animation, value.ToLowerInvariant());
                    shapes.Add(current);
                    break;
                default:
                    current?.Settings[key] = value;
                    break;
            }
        }

        return shapes;
    }

    /// <summary>A number setting of a shape, in the file's unit; null when absent or malformed.</summary>
    public static double? Number(EffectShape shape, string key) =>
        shape.Settings.TryGetValue(key, out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>One shape as the effects catalog writes it: shape:ms:length:width:angle (empty where the file says nothing).</summary>
    public static string Format(EffectShape shape)
    {
        static string Field(double? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var time = Number(shape, "time") is { } seconds ? (double?)Math.Round(seconds * MsPerSecond) : null;
        return string.Join(':', shape.Shape, Field(time), Field(Number(shape, "length")), Field(Number(shape, "width")), Field(Number(shape, "angle")));
    }

    /// <summary>The earliest hit of an effect's shapes, in ms from the effect's start; null when none has a time.</summary>
    public static int? FirstHitMs(IEnumerable<EffectShape> shapes)
    {
        var times = shapes.Select(shape => Number(shape, "time")).Where(time => time != null).Select(time => (int)Math.Round(time!.Value * MsPerSecond)).ToList();
        return times.Count > 0 ? times.Min() : null;
    }

    /// <summary>A catalog line's fields, split on the field separator (a trailing empty field kept).</summary>
    public static string[] Fields(string line) => line.Split(FieldSeparator);

    /// <summary>The key=value pairs of a geometry field ("shape=circle;length=15;delay=33").</summary>
    public static Dictionary<string, string> Pairs(string field)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in field.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var at = part.IndexOf('=');
            if (at > 0)
            {
                pairs[part[..at]] = part[(at + 1)..];
            }
        }

        return pairs;
    }

    /// <summary>key=value pairs joined for a geometry field, zero values left out so a line says only what the table set.</summary>
    public static string PairsOf(params (string Key, long Value)[] values) =>
        string.Join(PairSeparator, values.Where(pair => pair.Value != 0).Select(pair => $"{pair.Key}={pair.Value}"));
}

/// <summary>A danger shape of an effect's EffectDrivenEvent block: its animation, its kind and its raw settings.</summary>
internal sealed class EffectShape
{
    public EffectShape(string animation, string shape)
    {
        this.Animation = animation;
        this.Shape = shape;
    }

    public string Animation { get; }

    public string Shape { get; }

    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);
}
