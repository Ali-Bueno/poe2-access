using System.Text.Json;
using AsmResolver.PE;
using AsmResolver.PE.File;
using AsmResolver.PE.File.Headers;
using AsmResolver.PE.Win32Resources.Builder;
using AsmResolver.PE.Win32Resources.Version;

namespace Poe2AccessLauncher;

/// <summary>Makes random-named copies of GameHelper.exe with rewritten version-info strings, and cleans them up.</summary>
internal static class HostCopy
{
    private const string HostExe = "GameHelper.exe";
    private const string HostStoreFile = "tempFileLocations.dat"; // JSON string array, paths relative to the host folder
    private const string OwnStoreFile = "Poe2Access.copies.txt"; // one file name per line
    private const string Letters = "qwertyuiopasdfghjklzxcvbnm" + "eioadfc"; // weighted towards common letters

    public static string Create(string hostDirectory, string sourceExe)
    {
        var name = RandomName();
        var target = Path.Join(hostDirectory, $"{name}.exe");
        Rewrite(sourceExe, target, name);
        File.AppendAllLines(Path.Join(AppContext.BaseDirectory, OwnStoreFile), [Path.GetFileName(target)]);
        return target;
    }

    public static void DeleteOldCopies(string hostDirectory)
    {
        var files = new List<string>();
        var own = Path.Join(AppContext.BaseDirectory, OwnStoreFile);
        if (File.Exists(own))
        {
            files.AddRange(File.ReadAllLines(own).Where(line => line.Length > 0).Select(line => Path.Join(hostDirectory, line)));
        }

        var host = Path.Join(hostDirectory, HostStoreFile);
        if (File.Exists(host))
        {
            var names = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(host)) ?? [];
            files.AddRange(names.Select(name => Path.Join(hostDirectory, name)));
            File.WriteAllText(host, "[]");
        }

        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(Path.GetFileName(file), HostExe, StringComparison.OrdinalIgnoreCase))
            {
                continue; // a corrupt list must never delete the real executable
            }

            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    Log.Write($"Deleted old copy {file}");
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Log.Write($"Could not delete {file}: {error.Message}");
            }
        }

        File.WriteAllText(own, string.Empty);
    }

    private static string RandomName()
    {
        var random = new Random();
        char Letter() => Letters[random.Next(Letters.Length)];
        string Word() => char.ToUpperInvariant(Letter()) + new string(Enumerable.Range(0, random.Next(5, 10)).Select(_ => Letter()).ToArray());
        return string.Join(' ', Enumerable.Range(0, random.Next(1, 4)).Select(_ => Word()));
    }

    private static void Rewrite(string inputPath, string outputPath, string name)
    {
        var file = PEFile.FromFile(inputPath);
        var resources = PEImage.FromFile(file).Resources
            ?? throw new InvalidDataException($"No resource directory in {inputPath}");
        var version = VersionInfoResource.FromDirectory(resources)
            ?? throw new InvalidDataException($"No version info in {inputPath}");
        var table = version.GetChild<StringFileInfo>(StringFileInfo.StringFileInfoKey).Tables[0];
        table[StringTable.CommentsKey] = string.Empty;
        table[StringTable.CompanyNameKey] = name;
        table[StringTable.FileDescriptionKey] = name;
        table[StringTable.InternalNameKey] = name;
        table[StringTable.OriginalFilenameKey] = Path.GetFileName(outputPath);
        table[StringTable.ProductNameKey] = name;
        version.WriteToDirectory(resources);

        var buffer = new ResourceDirectoryBuffer();
        buffer.AddDirectory(resources);
        var directory = file.OptionalHeader.GetDataDirectory(DataDirectoryIndex.ResourceDirectory);
        file.GetSectionContainingRva(directory.VirtualAddress).Contents = buffer;
        file.Write(outputPath);
    }
}
