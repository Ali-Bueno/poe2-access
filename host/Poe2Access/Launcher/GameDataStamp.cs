using System; // also built into the tests, which have no implicit usings
using System.Collections.Generic;
using System.Linq;

namespace Poe2Access;

/// <summary>Why game data is made again: none made yet, a game patch, or the mod's own steps (a raised version, a retry).</summary>
internal enum GameDataCause
{
    FirstStart,
    GamePatch,
    ModSteps,
}

/// <summary>
///     What the game data was made from: the install (lines such as "index=size:time") and the version each
///     regeneration step ran at, so a mod update reruns only the steps whose version it raised. An optional step that failed
///     is not stamped: it is counted ("tried") and retried on the next starts, and after <see cref="MaxAttempts"/> it is
///     stamped and marked "gaveup" so it is left alone and still listed as failed.
/// </summary>
internal sealed record GameDataStamp(string Install, IReadOnlyDictionary<string, int> Steps)
{
    /// <summary>How many starts an optional step may fail on, per game build and step version: its slowest run is about 12 s on top of opening the index.</summary>
    public const int MaxAttempts = 3;

    private const string TriedPrefix = "tried ";
    private const string GaveUpPrefix = "gaveup ";
    private const char VersionMark = '@';

    private static readonly IReadOnlyDictionary<string, int> None = new Dictionary<string, int>();

    /// <summary>Failed starts of a step at a version, keyed "step@version".</summary>
    public IReadOnlyDictionary<string, int> Tried { get; init; } = None;

    /// <summary>Steps stamped without having worked, with the version they were given up at.</summary>
    public IReadOnlyDictionary<string, int> GaveUp { get; init; } = None;

    /// <summary>The version every step had when one format number for all of them was replaced.</summary>
    public const int LegacyVersion = 1;

    private const string StepPrefix = "step ";
    private const string LegacyFormatKey = "format=";
    private const int LegacyCurrentFormat = 7; // made every step as LegacyVersion does
    private const int LegacyCatalogFormat = 3; // the first whose stamp covered every catalog step; later ones changed only the sounds

    /// <summary>A regenerated file under half the old one: a column that moved reads few rows, and the old file serves better.</summary>
    public static bool Collapsed(long before, long now) => now * 2 < before;

    public static GameDataStamp Parse(string? text)
    {
        var install = new List<string>();
        var steps = new Dictionary<string, int>();
        var tried = new Dictionary<string, int>();
        var gaveUp = new Dictionary<string, int>();
        foreach (var line in Lines(text))
        {
            var equals = line.LastIndexOf('=');
            var into = new[] { (StepPrefix, steps), (TriedPrefix, tried), (GaveUpPrefix, gaveUp) }
                .FirstOrDefault(entry => line.StartsWith(entry.Item1, StringComparison.Ordinal) && equals > entry.Item1.Length && int.TryParse(line[(equals + 1)..], out _));
            if (into.Item2 is { } target)
            {
                target[line[into.Item1.Length..equals]] = int.Parse(line[(equals + 1)..]);
            }
            else
            {
                install.Add(line);
            }
        }

        return new GameDataStamp(Join(install), steps) { Tried = tried, GaveUp = gaveUp };
    }

    /// <summary>
    ///     The steps a stamp of the old layout (one "format=" number for every step) vouches for on this install: all of
    ///     them at the last format, the catalogs alone at an earlier one that already covered them, none for another install.
    /// </summary>
    public static GameDataStamp FromLegacy(string? text, string install, IEnumerable<string> steps, string soundsStep)
    {
        var lines = Lines(text).ToList();
        var format = lines.Where(line => line.StartsWith(LegacyFormatKey, StringComparison.Ordinal))
            .Select(line => int.TryParse(line[LegacyFormatKey.Length..], out var number) ? number : 0)
            .FirstOrDefault();
        var vouched = new Dictionary<string, int>();
        if (Join(lines.Where(line => !line.StartsWith(LegacyFormatKey, StringComparison.Ordinal))) == install && format >= LegacyCatalogFormat)
        {
            foreach (var step in steps.Where(step => format >= LegacyCurrentFormat || step != soundsStep))
            {
                vouched[step] = LegacyVersion;
            }
        }

        return new GameDataStamp(install, vouched);
    }

    /// <summary>Why steps are stale against this install.</summary>
    public GameDataCause Cause(string install) =>
        this.Steps.Count == 0 ? GameDataCause.FirstStart : this.Install != install ? GameDataCause.GamePatch : GameDataCause.ModSteps;

    public bool IsCurrent(string install, string step, int version) =>
        this.Install == install && this.Steps.TryGetValue(step, out var done) && done == version;

    /// <summary>
    ///     Whether the step's file in GameData was made at this version from this install. A step given up on is stamped
    ///     without one: the file there is an earlier version's, and must not go over the copy the package ships.
    /// </summary>
    public bool Made(string install, string step, int version) =>
        this.IsCurrent(install, step, version) && !(this.GaveUp.TryGetValue(step, out var givenUpAt) && givenUpAt == version);

    /// <summary>
    ///     This stamp after a step ran: stamped when it worked, a failed start counted when an optional one failed by
    ///     itself, and unchanged (the same object) otherwise. A failure by <paramref name="sharedCause"/> (the bundle index
    ///     would not open) is every step's at once and none's own: counted, three such starts gave every optional step up
    ///     for the game build, and they stayed so once the index opened again.
    /// </summary>
    public GameDataStamp After(string install, string step, int version, bool optional, string? error, string sharedCause) =>
        error is null ? this.With(install, step, version)
        : optional && error != sharedCause ? this.WithFailure(install, step, version)
        : this;

    /// <summary>This stamp with one more step done on the given install; a different install forgets the other steps.</summary>
    public GameDataStamp With(string install, string step, int version)
    {
        var same = this.Install == install;
        var steps = same ? new Dictionary<string, int>(this.Steps) : new Dictionary<string, int>();
        steps[step] = version;
        var tried = same ? this.Tried.Where(pair => !pair.Key.StartsWith(step + VersionMark, StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value) : new Dictionary<string, int>();
        var gaveUp = same ? this.GaveUp.Where(pair => pair.Key != step).ToDictionary(pair => pair.Key, pair => pair.Value) : new Dictionary<string, int>();
        return new GameDataStamp(install, steps) { Tried = tried, GaveUp = gaveUp };
    }

    /// <summary>
    ///     This stamp after a start on which an optional step failed: the start is counted and the step stays unstamped, so
    ///     the next start retries it; at <see cref="MaxAttempts"/> it is stamped as given up instead.
    /// </summary>
    public GameDataStamp WithFailure(string install, string step, int version)
    {
        var same = this.Install == install;
        var steps = same ? new Dictionary<string, int>(this.Steps) : new Dictionary<string, int>();
        var tried = same ? new Dictionary<string, int>(this.Tried) : new Dictionary<string, int>();
        var gaveUp = same ? new Dictionary<string, int>(this.GaveUp) : new Dictionary<string, int>();
        var key = $"{step}{VersionMark}{version}";
        var attempts = tried.GetValueOrDefault(key) + 1;
        if (attempts >= MaxAttempts)
        {
            tried.Remove(key);
            steps[step] = version;
            gaveUp[step] = version;
        }
        else
        {
            tried[key] = attempts;
        }

        return new GameDataStamp(install, steps) { Tried = tried, GaveUp = gaveUp };
    }

    /// <summary>How many starts the step has failed on at this version.</summary>
    public int Attempts(string step, int version) => this.Tried.GetValueOrDefault($"{step}{VersionMark}{version}");

    /// <summary>The steps stamped without having worked, still at the version they were given up at.</summary>
    public IEnumerable<string> GivenUp() =>
        this.GaveUp.Where(pair => this.Steps.TryGetValue(pair.Key, out var done) && done == pair.Value).Select(pair => pair.Key).Order(StringComparer.Ordinal);

    public override string ToString() =>
        this.Install + string.Concat(this.Steps.OrderBy(step => step.Key, StringComparer.Ordinal).Select(step => $"{StepPrefix}{step.Key}={step.Value}\n")) +
        string.Concat(this.Tried.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{TriedPrefix}{pair.Key}={pair.Value}\n")) +
        string.Concat(this.GaveUp.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{GaveUpPrefix}{pair.Key}={pair.Value}\n"));

    private static IEnumerable<string> Lines(string? text) =>
        (text ?? string.Empty).Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0);

    private static string Join(IEnumerable<string> lines) => string.Concat(lines.Select(line => line + "\n"));
}
