using System; // also built into the tests, which have no implicit usings
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Poe2Access;

/// <summary>
///     The judgments of "Poe2Access.exe check" that need no process: whether the files a package lists are there and not
///     empty, and whether a catalog or a phrase table the plugin reads at start parses. A fault is one line, naming the file.
///     Pure but for reading the folder, so it can be tried on one.
/// </summary>
internal static class PackageCheck
{
    /// <summary>The package's own files (<see cref="PackageInstall.FileList"/>) that are missing or empty, one line each.</summary>
    public static IEnumerable<string> FileFaults(string installRoot, IReadOnlyList<string> shipped)
    {
        foreach (var relative in shipped)
        {
            var info = new FileInfo(Path.Join(installRoot, relative));
            if (!info.Exists)
            {
                yield return $"Missing file: {relative}";
            }
            else if (info.Length == 0)
            {
                yield return $"Empty file: {relative}";
            }
        }
    }

    /// <summary>Why a catalog does not read (not JSON, no entries), or null; a text catalog needs a data line, a JSON one a value.</summary>
    public static string? CatalogFault(string file, string text)
    {
        if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !IsJson(text))
        {
            return "it is not valid JSON";
        }

        return GameDataCounts.Count(file, text) == 0 ? "it holds no entries" : null;
    }

    /// <summary>Why a phrase table (<c>catalog/lang/&lt;language&gt;.json</c>, an object of text by key) does not read, or null.</summary>
    public static string? PhraseFault(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var table = document.RootElement;
            return table.ValueKind != JsonValueKind.Object || table.EnumerateObject().Any(entry => entry.Value.ValueKind != JsonValueKind.String)
                ? "it is not an object of texts"
                : null;
        }
        catch (JsonException error)
        {
            return $"it is not valid JSON ({error.Message})";
        }
    }

    /// <summary>The catalogs of a plugin folder that fail to read; the phrase tables of its lang folder likewise.</summary>
    public static IEnumerable<string> CatalogFaults(string pluginFolder)
    {
        var catalogs = Path.Join(pluginFolder, "catalog");
        if (!Directory.Exists(catalogs))
        {
            yield return $"No catalog folder: {catalogs}";
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(catalogs).Order(StringComparer.Ordinal).Where(IsCatalog))
        {
            if (Fault(path, CatalogFault) is { } fault)
            {
                yield return fault;
            }
        }

        var lang = Path.Join(catalogs, "lang");
        foreach (var path in Directory.Exists(lang) ? Directory.EnumerateFiles(lang, "*.json").Order(StringComparer.Ordinal) : Enumerable.Empty<string>())
        {
            if (Fault(path, (_, text) => PhraseFault(text)) is { } fault)
            {
                yield return fault;
            }
        }
    }

    private static bool IsCatalog(string path) => Path.GetExtension(path) is ".json" or ".txt";

    private static bool IsJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Fault(string path, Func<string, string, string?> judge)
    {
        var name = Path.GetFileName(path);
        try
        {
            return judge(name, File.ReadAllText(path)) is { } why ? $"Catalog {name}: {why}" : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"Catalog {name}: cannot be read ({error.Message})";
        }
    }
}
