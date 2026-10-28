using System; // also built into the tests, which have no implicit usings
using System.Collections.Generic;
using System.Linq;

namespace Poe2AccessLauncher;

/// <summary>
///     What the game data was made from: the install (lines such as "index=size:time") and the version each
///     regeneration step ran at, so a mod update reruns only the steps whose version it raised.
/// </summary>
internal sealed record GameDataStamp(string Install, IReadOnlyDictionary<string, int> Steps)
{
    /// <summary>The version every step had when one format number for all of them was replaced.</summary>
    public const int LegacyVersion = 1;

    private const string StepPrefix = "step ";
    private const string LegacyFormatKey = "format=";
    private const int LegacyCurrentFormat = 7; // made every step as LegacyVersion does
    private const int LegacyCatalogFormat = 3; // the first whose stamp covered every catalog step; later ones changed only the sounds

    public static GameDataStamp Parse(string? text)
    {
        var install = new List<string>();
        var steps = new Dictionary<string, int>();
        foreach (var line in Lines(text))
        {
            var equals = line.LastIndexOf('=');
            if (line.StartsWith(StepPrefix, StringComparison.Ordinal) && equals > StepPrefix.Length && int.TryParse(line[(equals + 1)..], out var version))
            {
                steps[line[StepPrefix.Length..equals]] = version;
            }
            else
            {
                install.Add(line);
            }
        }

        return new GameDataStamp(Join(install), steps);
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

    public bool IsCurrent(string install, string step, int version) =>
        this.Install == install && this.Steps.TryGetValue(step, out var done) && done == version;

    /// <summary>This stamp with one more step done on the given install; a different install forgets the other steps.</summary>
    public GameDataStamp With(string install, string step, int version)
    {
        var steps = this.Install == install ? new Dictionary<string, int>(this.Steps) : new Dictionary<string, int>();
        steps[step] = version;
        return new GameDataStamp(install, steps);
    }

    public override string ToString() =>
        this.Install + string.Concat(this.Steps.OrderBy(step => step.Key, StringComparer.Ordinal).Select(step => $"{StepPrefix}{step.Key}={step.Value}\n"));

    private static IEnumerable<string> Lines(string? text) =>
        (text ?? string.Empty).Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0);

    private static string Join(IEnumerable<string> lines) => string.Concat(lines.Select(line => line + "\n"));
}
