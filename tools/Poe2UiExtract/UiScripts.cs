using Index = LibBundle3.Index;

/// <summary>The game's UI scripts (`metadata/ui/**`), extracted as files and turned into the control catalog.</summary>
internal static class UiScripts
{
    private static readonly string[] WantedExtensions = { ".ui", ".xml", ".txt", ".json", ".css", ".lua" };

    public static int Extract(Index index, string outDir)
    {
        Console.WriteLine($"Total files in index: {index.Files.Count}");
        Console.WriteLine($"Total bundles: {index.Bundles.Length}");

        var uiFiles = index.Files.Values
            .Where(f => f.Path != null && f.Path.Contains("metadata/ui", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"Metadata/UI path count: {uiFiles.Count}");
        Console.WriteLine("First 200 paths:");
        foreach (var f in uiFiles.Take(200))
            Console.WriteLine(f.Path);

        // Extensions of interest (or no extension at all)
        var toExtract = uiFiles.Where(f =>
        {
            var path = f.Path!;
            if (!path.StartsWith("metadata/ui/", StringComparison.OrdinalIgnoreCase))
                return false;
            var ext = Path.GetExtension(path);
            return ext.Length == 0 || WantedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }).ToList();

        Console.WriteLine($"Files selected for extraction: {toExtract.Count}");

        Directory.CreateDirectory(outDir);
        int extracted = 0, errors = 0;
        foreach (var f in toExtract)
        {
            try
            {
                var rel = f.Path!.Replace('/', Path.DirectorySeparatorChar);
                var dest = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                var data = f.Read();
                File.WriteAllBytes(dest, data.ToArray());
                extracted++;
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"ERROR extracting {f.Path}: {ex.Message}");
            }
        }

        Console.WriteLine($"Extracted: {extracted}, Errors: {errors}");
        return 0;
    }

    /// <summary>The `catalog` command's output, made from the scripts of this index without keeping them.</summary>
    public static int BuildCatalog(Index index, string outFile)
    {
        var scripts = Path.Join(Path.GetTempPath(), "poe2-ui-" + Environment.ProcessId);
        try
        {
            var code = Extract(index, scripts);
            return code != 0 ? code : CatalogGenerator.Run(scripts, outFile);
        }
        finally
        {
            if (Directory.Exists(scripts))
            {
                Directory.Delete(scripts, true);
            }
        }
    }
}
