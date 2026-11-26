using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>A boss skill that plays clips (the parent side of <see cref="BossEventChildren"/>).</summary>
internal sealed record ClipSkill(string Id, IReadOnlyList<string> Clips);

/// <summary>A boss skill no clip of its own plays, with the effect files it draws and the trigger that fires it, if any.</summary>
internal sealed record ClipChild(string Id, IReadOnlyList<string> Effects, string? Trigger);

/// <summary>A child tied to the skill whose clip fires it, with the named event's time when one names it (ms from the clip's start).</summary>
internal readonly record struct ClipLink(string Parent, string Child, int? Ms, string Evidence);

/// <summary>
///     Which of a boss's clips fires a skill no clip plays (the audit's EVENT class, docs/research/boss-mechanics.md §17): the
///     clip whose own effect art (client section of the .ao) lies in the same effect family as the child's drawn effect, when
///     it is the only clip that does; else, among those clips, the one whose named event's words are all in the child's id.
///     Pure: the lines are <c>child|parent|child[|ms]</c>.
/// </summary>
internal static class BossEventChildren
{
    /// <summary>An event name word shorter than this ("01", "a") names nothing.</summary>
    private const int MinWord = 3;

    /// <summary>A named event must hold this many words to name a child ("combo_slam"): one word ("slam") names too many.</summary>
    private const int MinEventWords = 2;

    /// <summary>The folders effect artists sort an attack's files by type in, before the attack's own folder.</summary>
    private static readonly HashSet<string> TypeFolders = new(StringComparer.OrdinalIgnoreCase) { "ao", "pet", "epk", "trl", "trails", "aoc" };

    /// <summary>Events every clip may carry that name no skill.</summary>
    private static readonly HashSet<string> Generic = new(StringComparer.Ordinal) { "contact", "move", "start_target_update", "pick_end" };

    private static readonly Regex Camel = new("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");

    /// <summary>
    ///     An effect file's attack family: the folder above its type folder plus the attack folder below it
    ///     (<c>…/ViperNapuatzi/ao/serpent_strike/x.ao</c> and <c>…/ViperNapuatzi/pet/serpent_strike/y.pet</c> are one); null for a
    ///     file in no attack folder (a monster's flat effect folder holds all its attacks).
    /// </summary>
    public static string? Family(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        for (var i = 1; i + 2 < parts.Length; i++)
        {
            if (TypeFolders.Contains(parts[i]))
            {
                return string.Join('/', parts.Take(i).Append(parts[i + 1])).ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>An effect file's attack family, else its own folder: enough beside a named event (<see cref="Links"/>).</summary>
    public static string Folder(string path) => Family(path) ?? path[..Math.Max(0, path.Replace('\\', '/').LastIndexOf('/'))].ToLowerInvariant();

    /// <summary>The lower-case words of a skill id ("GAViperComboEndSlam": ga, viper, combo, end, slam).</summary>
    public static HashSet<string> WordsOf(string id) =>
        Camel.Split(id).Select(word => word.ToLowerInvariant()).Where(word => word.Length > 0).ToHashSet(StringComparer.Ordinal);

    /// <summary>An event's words that may name a skill ("combo_slam": combo, slam), or none for a generic event.</summary>
    public static List<string> EventWords(string name) =>
        Generic.Contains(name) || MonsterAnimationFiles.IsImpact(name)
            ? new List<string>()
            : name.ToLowerInvariant().Split('_').Where(word => word.Length >= MinWord && !word.All(char.IsDigit)).ToList();

    public static List<ClipLink> Links(IReadOnlyList<ClipSkill> parents, IReadOnlyList<ClipChild> children, ClipEffects clips)
    {
        var played = parents.SelectMany(parent => parent.Clips).Distinct(StringComparer.Ordinal).ToList();
        List<string> Sharing(IReadOnlyList<string> effects, Func<string, string?> group)
        {
            var own = effects.Select(group).OfType<string>().ToHashSet(StringComparer.Ordinal);
            return played.Where(clip => clips.Effects.TryGetValue(clip, out var art) && art.Select(group).OfType<string>().Any(own.Contains)).ToList();
        }

        var links = new List<ClipLink>();
        foreach (var child in children)
        {
            var sharing = Sharing(child.Effects, Family);
            var words = WordsOf(child.Id);
            var named = Sharing(child.Effects, Folder).SelectMany(clip => (clips.Events.GetValueOrDefault(clip) ?? new List<ClipEvent>()).Select(e => (Clip: clip, Event: e)))
                .Where(pair => EventWords(pair.Event.Name) is { Count: >= MinEventWords } eventWords && eventWords.All(words.Contains)).ToList();
            (string Clip, int? Ms, string Evidence)? found = named.Select(pair => pair.Clip).Distinct().Count() == 1
                ? (named[0].Clip, named.Min(pair => pair.Event.Ms), $"event {named[0].Event.Name}")
                : sharing.Count == 1 ? (sharing[0], null, "effect family") : null;
            if (found is not { } link)
            {
                continue;
            }

            var target = child.Trigger ?? child.Id; // a trigger's hits land where it scatters them: its area is the one to leave
            foreach (var parent in parents.Where(parent => parent.Clips.Contains(link.Clip) && parent.Id != target))
            {
                links.Add(new ClipLink(parent.Id, target, link.Ms, $"{link.Clip}: {link.Evidence}"));
            }
        }

        return links.DistinctBy(link => (link.Parent, link.Child)).ToList();
    }

    public static string Line(ClipLink link) => link.Ms is { } ms ? $"child|{link.Parent}|{link.Child}|{ms}" : $"child|{link.Parent}|{link.Child}";
}
