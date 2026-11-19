using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

/// <summary>
///     A boss arena's flood ([boss mechanics](../../docs/research/boss-mechanics.md#mektuls-lava)): an object placed in the arena whose
///     .ao plays an .amd clip in which a set of bones travels together down the room, the front and its tongue's edges. Its catalog
///     line gives the front's track, the tongue and other placed objects as offsets from it in the room's frame, which the plugin
///     fits to the world. Pure but for the file reader.
/// </summary>
internal static class BossFloods
{
    /// <summary>A placed object of a room: where (world units of the room), turned how far (radians), scaled, its art and its entity.</summary>
    internal sealed record Placement(Vector2 At, float Rotation, float Scale, string Ao, string Path);

    /// <summary>A bone's positions over a clip, frame by frame (model units).</summary>
    internal sealed record Bone(string Name, IReadOnlyList<Vector2> Frames);

    private const float WorldPerCell = 250f / 23f;

    /// <summary>A flood's front travels a room's length: a front going less far (world units) is a prop's sway.</summary>
    private const float MinTravel = 2000f;

    /// <summary>Bones whose travel is the front's within this share of it move with the front: the tongue's edges.</summary>
    private const float WithFront = 0.1f;

    /// <summary>A tongue is drawn by edges on both sides of the front, at least two bones.</summary>
    private const int MinEdgeBones = 2;

    /// <summary>The front is given every this many ms of its clip; the plugin goes between the samples.</summary>
    private const int SampleMs = 1000;

    /// <summary>An anchor tells the room's eight fits apart when each of its axes, and their difference, is this share of its distance.</summary>
    private const float AnchorShare = 0.1f;

    private const int MaxAnchors = 3;

    private static readonly Regex Quoted = new(@"""([^""]*)""");
    private static readonly Regex AmdPath = new(@"AnimationController\s*\{[^{}]*?metadata\s*=\s*""([^""]+\.amd)""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex BonesLine = new(@"^(\d+) (\d+)$");

    /// <summary>The flood line of a fight's arena rooms, the first room that holds one; null when none floods.</summary>
    public static string? Line(string boss, IEnumerable<BossArenaRooms.Room> arena, Func<string, string?> textOf)
    {
        foreach (var room in arena)
        {
            if (textOf(room.Path) is not { } text)
            {
                continue;
            }

            var placed = Placements(text);
            foreach (var lava in placed)
            {
                if (lava.Ao.Length > 0 && textOf(lava.Ao) is { } ao && AmdPath.Match(ao) is { Success: true } amd && textOf(amd.Groups[1].Value) is { } clip &&
                    Clip(clip) is var (ms, bones) && Flood(lava, ms, bones, placed) is { } line)
                {
                    return $"flood|{boss}|{line}";
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     A room's object lines: "x y 0 rotation …" or "x y 1 worldX worldY rotation …" (the exact place), a quaternion, flags,
    ///     the scale, then the art and the entity quoted.
    /// </summary>
    public static IReadOnlyList<Placement> Placements(string text)
    {
        var placed = new List<Placement>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            var quote = line.IndexOf('"');
            if (quote <= 0)
            {
                continue;
            }

            var numbers = line[..quote].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Number).ToList();
            var quoted = Quoted.Matches(line[quote..]).Select(match => match.Groups[1].Value).ToList();
            if (numbers.Count < 6 || numbers.Any(n => n == null) || quoted.Count < 2 || !quoted[^1].StartsWith("Metadata/", StringComparison.Ordinal))
            {
                continue;
            }

            var exact = numbers[2] == 1f;
            var at = exact ? new Vector2(numbers[3]!.Value, numbers[4]!.Value) : new Vector2(numbers[0]!.Value, numbers[1]!.Value) * WorldPerCell;
            var ao = quoted.FirstOrDefault(part => part.EndsWith(".ao", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            placed.Add(new Placement(at, numbers[exact ? 5 : 3]!.Value, numbers[^1]!.Value, ao, quoted[^1]));
        }

        return placed;
    }

    /// <summary>An .amd's first clip: its length (ms) and its bones' positions ("name" xyz x y z …, one triple a frame).</summary>
    public static (int Ms, IReadOnlyList<Bone> Bones)? Clip(string text)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n').Select(line => line.Trim()).ToList();
        var name = lines.FindIndex(line => line.StartsWith('"'));
        if (name < 0 || name + 2 >= lines.Count || !int.TryParse(lines[name + 2], out var ms))
        {
            return null;
        }

        var header = lines.FindIndex(name, line => BonesLine.IsMatch(line));
        if (header < 0)
        {
            return null;
        }

        var counts = BonesLine.Match(lines[header]);
        var (count, frames) = (int.Parse(counts.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(counts.Groups[2].Value, CultureInfo.InvariantCulture));
        var bones = new List<Bone>();
        foreach (var line in lines.Skip(header + 1).Take(count))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 + (3 * frames) && parts[1] == "xyz" && parts.Skip(2).Select(Number).ToList() is var values && values.All(v => v != null))
            {
                bones.Add(new Bone(parts[0].Trim('"'), Enumerable.Range(0, frames).Select(f => new Vector2(values[3 * f]!.Value, values[(3 * f) + 1]!.Value)).ToList()));
            }
        }

        return (ms, bones);
    }

    /// <summary>The line's fields after the boss when the object's clip carries a front and its tongue, else null.</summary>
    internal static string? Flood(Placement lava, int ms, IReadOnlyList<Bone> bones, IReadOnlyList<Placement> room)
    {
        static Vector2 Travel(Bone bone) => bone.Frames[^1] - bone.Frames[0];
        var front = bones.Where(bone => bone.Frames.Count > 1).OrderByDescending(bone => Travel(bone).Length()).FirstOrDefault();
        if (front == null || Travel(front).Length() * lava.Scale < MinTravel)
        {
            return null;
        }

        var travel = Travel(front);
        var edges = bones.Where(bone => bone != front && bone.Frames.Count == front.Frames.Count && Vector2.Distance(Travel(bone), travel) <= WithFront * travel.Length()).ToList();
        if (edges.Count < MinEdgeBones)
        {
            return null;
        }

        // The tongue at the clip's end, when it is whole: each edge's distance behind the front and beside its line, widening backwards.
        var axis = Vector2.Normalize(travel);
        var tip = front.Frames[^1];
        var outline = edges.Select(bone => bone.Frames[^1] - tip).Select(offset => new Vector2(-Vector2.Dot(offset, axis), Math.Abs((axis.X * offset.Y) - (axis.Y * offset.X))) * lava.Scale)
            .Append(Vector2.Zero).OrderBy(point => point.X).ToList();
        for (var k = 1; k < outline.Count; k++)
        {
            outline[k] = outline[k] with { Y = Math.Max(outline[k].Y, outline[k - 1].Y) };
        }

        var step = Math.Max(1, (int)Math.Round(SampleMs * (front.Frames.Count - 1) / (double)ms));
        var samples = Enumerable.Range(0, ((front.Frames.Count - 1) / step) + 1).Select(k => k * step).ToList();
        if (samples[^1] != front.Frames.Count - 1)
        {
            return null; // the samples must end on the clip's last frame, at even steps of it
        }

        var track = samples.Select(f => Turn(front.Frames[f] * lava.Scale, lava.Rotation));
        var anchors = Anchors(lava, room);
        return anchors.Count == 0
            ? null
            : $"{lava.Path}|{ms}|{string.Join(';', outline.Select(Pair).Distinct())}|{string.Join(';', track.Select(Pair))}|{string.Join(';', anchors.Select(anchor => $"{anchor.Path}@{Pair(anchor.At - lava.At)}"))}";
    }

    /// <summary>A model offset turned into the room's frame, as its placement turns it.</summary>
    internal static Vector2 Turn(Vector2 model, float rotation)
    {
        var (sin, cos) = MathF.SinCos(rotation);
        return new Vector2((model.X * cos) - (model.Y * sin), (model.X * sin) + (model.Y * cos));
    }

    /// <summary>The room's other objects, one of their path, whose offset from the flood tells the room's turn and mirroring apart; best first.</summary>
    private static List<Placement> Anchors(Placement lava, IReadOnlyList<Placement> room)
    {
        static float Margin(Vector2 d) => Math.Min(Math.Min(Math.Abs(d.X), Math.Abs(d.Y)), Math.Abs(Math.Abs(d.X) - Math.Abs(d.Y)));
        return room.Where(placement => placement.Path != lava.Path && placement.Path.Contains("/Objects/", StringComparison.Ordinal) &&
                                       !placement.Path.StartsWith("Metadata/MiscellaneousObjects/", StringComparison.Ordinal))
            .GroupBy(placement => placement.Path, StringComparer.Ordinal).Where(group => group.Count() == 1).Select(group => group.First())
            .Where(placement => Margin(placement.At - lava.At) >= AnchorShare * (placement.At - lava.At).Length())
            .OrderByDescending(placement => Margin(placement.At - lava.At)).Take(MaxAnchors).ToList();
    }

    private static string Pair(Vector2 point) => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(point.X):0}:{Math.Round(point.Y):0}");

    private static float? Number(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
