using System; // also built into the tests, which have no implicit usings

namespace Poe2AccessLauncher;

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
