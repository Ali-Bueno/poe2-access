using System;
using System.Collections.Generic;
using System.Linq;
using Poe2Accessibility.Services;

/// <summary>
///     Whether each name the plugin's rules match (<see cref="GameNames"/>) is still in the game's files: an Id of its table,
///     the start of one, a word of a monster's file, an object's script and a state it or a script it extends declares, part
///     of a UI image's name. Pure.
/// </summary>
internal static class GameNameCheck
{
    /// <summary>How the step's last line starts when names are gone, the launcher logging it as the step's reason.</summary>
    public const string GonePrefix = "gone from the game's files: ";

    /// <summary>
    ///     What the files hold: the Ids of the four tables, a reader of an object's script by its path (null when absent) and
    ///     the names of the UI images.
    /// </summary>
    internal sealed record Files(
        IReadOnlySet<string> Buffs, IReadOnlySet<string> Animations, IReadOnlyCollection<string> Monsters, Func<string, string?> Script,
        IReadOnlySet<string> MinimapIcons, IReadOnlyCollection<string> UiArt);

    /// <summary>The names the files no longer hold, in the order given.</summary>
    public static IReadOnlyList<GameName> Gone(IEnumerable<GameName> names, Files files) => names.Where(name => !Holds(name, files)).ToList();

    public static bool Holds(GameName name, Files files) => name.Kind switch
    {
        GameNameKind.Buff => files.Buffs.Contains(name.Value),
        GameNameKind.Animation => files.Animations.Contains(name.Value),
        GameNameKind.AnimationStart => files.Animations.Any(id => id.StartsWith(name.Value, StringComparison.Ordinal)),
        GameNameKind.Monster => files.Monsters.Any(id => id.AsSpan(id.LastIndexOf('/') + 1).Contains(name.Value, StringComparison.Ordinal)),
        GameNameKind.ObjectFile => files.Script(name.Value) is { } text && (name.State is not { } state || StatesOf(name.Value, text, files.Script).Contains(state)),
        GameNameKind.MinimapIcon => files.MinimapIcons.Contains(name.Value),
        GameNameKind.UiArt => files.UiArt.Any(art => art.Contains(name.Value, StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };

    /// <summary>The image names of the list of UI images: what each line quotes first.</summary>
    public static List<string> UiArtNames(string list) =>
        list.Split('\n').Select(line => line.Split('"')).Where(parts => parts.Length > 1).Select(parts => parts[1]).ToList();

    /// <summary>The line naming each gone name and the rule it silences.</summary>
    public static string Line(IEnumerable<GameName> gone) =>
        GonePrefix + string.Join("; ", gone.Select(name => $"{name.Kind} {name.Value}{(name.State is { } state ? $" state {state}" : string.Empty)} ({name.Rule})"));

    /// <summary>The states a script declares, with those of the scripts it extends.</summary>
    private static HashSet<string> StatesOf(string path, string text, Func<string, string?> script)
    {
        var states = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
        var pending = new Queue<string>(new[] { text });
        while (pending.TryDequeue(out var next))
        {
            states.UnionWith(BossScripts.DeclaredStatesOf(next));
            foreach (var parent in BossScripts.ExtendsOf(next).Where(seen.Add))
            {
                if (script(parent) is { } parentText)
                {
                    pending.Enqueue(parentText);
                }
            }
        }

        return states;
    }
}
