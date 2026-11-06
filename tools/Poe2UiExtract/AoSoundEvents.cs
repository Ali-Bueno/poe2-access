using System;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>What an <c>.ao</c> says an object sounds like, and the FMOD event and file names that follow from it.</summary>
internal static class AoSoundEvents
{
    private const string SoundBlock = "SoundEvents";
    private const string ListStart = "'[";
    private const string ListEnd = "]'";
    private const string ParentKey = "extends \"";
    private const string RootParent = "Metadata/Parent";
    private const string AoExtension = ".ao";
    private const string AudioFolder = "Audio/";
    private const string EffectsFolder = "Audio/Sound Effects/";
    private const string EventPrefix = "event:/";

    // Audio/Sound Effects/X plays event:/Sound Effects/XSG (events.txt against every .ao, 2026-11-06).
    private const string EventSuffix = "SG";
    private const char VolumeMark = '%';
    private const int MaxParents = 8;

    /// <summary>Animations that are the object coming apart, the likeliest first; the object's cue plays that sound.</summary>
    private static readonly string[] OpeningWords = { "open", "break", "destroy", "shatter", "smash", "death", "die", "fall" };

    /// <summary>The opening sound of an <c>.ao</c>, or of the nearest parent that has sounds.</summary>
    public static string? OpeningEventOf(string path, Func<string, string?> text)
    {
        for (var depth = 0; depth < MaxParents; depth++)
        {
            if (text(path) is not { } ao)
            {
                return null;
            }

            if (OpeningEvent(ao) is { } sound)
            {
                return sound;
            }

            if (Parent(ao) is not { } parent)
            {
                return null;
            }

            path = parent + AoExtension;
        }

        return null;
    }

    /// <summary>The first sound of the animation that opens the object, as the <c>.ao</c> names it; null when it has none.</summary>
    public static string? OpeningEvent(string ao)
    {
        var block = ao.IndexOf(SoundBlock, StringComparison.Ordinal);
        var from = block < 0 ? -1 : ao.IndexOf(ListStart, block, StringComparison.Ordinal);
        var to = from < 0 ? -1 : ao.IndexOf(ListEnd, from, StringComparison.Ordinal);

        // An empty block ends before the next block's list.
        if (to < 0 || ao.IndexOf('}', block) < from)
        {
            return null;
        }

        try
        {
            using var list = JsonDocument.Parse(ao[(from + 1)..(to + 1)]);
            var animations = list.RootElement.EnumerateArray().Select(Animation).ToList();
            foreach (var word in OpeningWords)
            {
                if (animations.FirstOrDefault(animation => animation.Name.Contains(word, StringComparison.OrdinalIgnoreCase) && animation.Sound != null) is { Sound: { } sound })
                {
                    return sound;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string Name, string? Sound) Animation(JsonElement animation)
    {
        var name = animation.TryGetProperty("name", out var named) ? named.GetString() ?? string.Empty : string.Empty;
        if (!animation.TryGetProperty("events", out var events))
        {
            return (name, null);
        }

        var first = events.EnumerateArray()
            .Where(soundEvent => soundEvent.TryGetProperty("filename", out var file) && file.GetString()?.StartsWith(AudioFolder, StringComparison.Ordinal) == true)
            .OrderBy(soundEvent => soundEvent.TryGetProperty("time", out var time) ? time.GetDouble() : 0)
            .Select(soundEvent => soundEvent.GetProperty("filename").GetString())
            .FirstOrDefault();
        return (name, first);
    }

    private static string? Parent(string ao)
    {
        var at = ao.IndexOf(ParentKey, StringComparison.Ordinal);
        var end = at < 0 ? -1 : ao.IndexOf('"', at + ParentKey.Length);
        var parent = end < 0 ? null : ao[(at + ParentKey.Length)..end];
        return parent is null or RootParent ? null : parent;
    }

    /// <summary>The event an <c>.ao</c>'s sound plays: the usual name first, then the older one with no suffix.</summary>
    public static string[] EventPaths(string sound)
    {
        var mark = sound.IndexOf(VolumeMark);
        var name = EventPrefix + (mark < 0 ? sound : sound[..mark])[AudioFolder.Length..];
        return new[] { name + EventSuffix, name };
    }

    /// <summary>A file name for a sound, unique per sound: its path with anything but letters and digits as underscores.</summary>
    public static string FileName(string sound)
    {
        var name = new StringBuilder(sound[EffectsFolder.Length..]);
        for (var i = 0; i < name.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(name[i]))
            {
                name[i] = '_';
            }
        }

        return name.ToString();
    }
}
