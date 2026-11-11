using System; // also built into the tests, which have no implicit usings

namespace Poe2Access;

/// <summary>Release tags are dates (v2026.10.28), with -2, -3... for later releases the same day (Publish-Release.ps1).</summary>
internal static class ReleaseTag
{
    /// <summary>The tag as a comparable version: year, month, day, and the release's number that day (1 when unnumbered).</summary>
    public static Version? Parse(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        var date = dash < 0 ? text : text[..dash];
        var number = dash < 0 ? "1" : text[(dash + 1)..];
        return Version.TryParse(date, out var day) && day.Build >= 0 && day.Revision < 0 && int.TryParse(number, out var n) && n >= 1
            ? new Version(day.Major, day.Minor, day.Build, n)
            : null;
    }
}

/// <summary>Where an offer to update stops, before or after asking the latest release.</summary>
internal enum UpdateStep
{
    /// <summary>No version file: a development build, which never updates itself.</summary>
    NoVersion,

    /// <summary>An automatic check with CheckForUpdates off.</summary>
    Off,

    /// <summary>Ask GitHub for the latest release.</summary>
    Check,

    /// <summary>The latest is not newer.</summary>
    UpToDate,

    /// <summary>A newer release: ask the player.</summary>
    Ask,
}

/// <summary>The update offer's rule: a development build never updates, an automatic check follows the setting, only a newer release is offered, and only a check the player asked for says why nothing happens. Pure.</summary>
internal static class UpdateOffer
{
    /// <summary>Before any network: whether to check at all; the setting is read only for an automatic check of a versioned build.</summary>
    public static UpdateStep Before(string? installedTag, bool manual, Func<bool> automaticOn) =>
        installedTag is null || ReleaseTag.Parse(installedTag) is null ? UpdateStep.NoVersion : !manual && !automaticOn() ? UpdateStep.Off : UpdateStep.Check;

    /// <summary>With the latest release's version: offered only when newer.</summary>
    public static UpdateStep After(Version installed, Version latest) => latest <= installed ? UpdateStep.UpToDate : UpdateStep.Ask;

    /// <summary>Whether a step that installs nothing is told in a box: only to a player who asked (an automatic check is silent).</summary>
    public static bool Tells(UpdateStep step, bool manual) => manual && step is UpdateStep.NoVersion or UpdateStep.UpToDate;
}
