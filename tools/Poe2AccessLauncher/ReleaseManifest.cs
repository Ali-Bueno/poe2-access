using System; // also built into the tests, which have no implicit usings
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Poe2AccessLauncher;

/// <summary>A published release: its tag, notes, and the package's download link and SHA-256.</summary>
internal sealed record Release(string Tag, Version Version, string Notes, string ZipUrl, string Sha256);

/// <summary>
///     The release.json every release carries (Publish-Release.ps1). Fetched as a download it has no hourly limit per
///     address, unlike GitHub's API, which shared addresses use up.
/// </summary>
internal static class ReleaseManifest
{
    public const string Asset = "release.json";
    public const string PackageAsset = "Poe2Access.zip";
    private const string Releases = "https://github.com/Ali-Bueno/poe2-access/releases";

    /// <summary>GitHub redirects this link to the latest release's manifest.</summary>
    public const string LatestUrl = Releases + "/latest/download/" + Asset;

    /// <summary>The release a manifest describes, its package link pinned to its tag rather than to "latest".</summary>
    public static Release Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = (root.GetProperty("tag").GetString() ?? string.Empty).Trim();
        var version = ReleaseTag.Parse(tag) ?? throw new InvalidDataException($"Unknown release tag {tag}");
        var sha256 = (root.GetProperty("sha256").GetString() ?? string.Empty).Trim();
        if (sha256.Length != SHA256.HashSizeInBytes * 2 || !sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"Release {tag} has no checksum for {PackageAsset}");
        }

        var notes = root.TryGetProperty("notes", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        return new Release(tag, version, notes.Trim(), $"{Releases}/download/{tag}/{PackageAsset}", sha256);
    }
}
