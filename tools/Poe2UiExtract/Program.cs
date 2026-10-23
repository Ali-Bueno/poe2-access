using System;
using System.IO;
using System.Linq;
using Index = LibBundle3.Index;

// Args: catalog <uiRootDir> <outJson>  -- parse extracted .ui scripts into a JSON control catalog.
if (args.Length > 0 && string.Equals(args[0], "catalog", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: Poe2UiExtract.exe catalog <uiRootDir> <outJson>");
        return 1;
    }
    return CatalogGenerator.Run(args[1], args[2]);
}

// Args: [0] = path to _.index.bin, [1] = output dir
string indexPath = args.Length > 0 ? args[0] : @"D:\games\steam\steamapps\common\Path of Exile 2\Bundles2\_.index.bin";
string outDir = args.Length > 1 ? args[1] : @"out";

Console.WriteLine($"Opening index (read-only intent): {indexPath}");

// Open with parsePaths:false so a handful of unparsable paths (known PoE2 quirk,
// see LibGGPK3 issue #25) don't abort the whole run; we call ParsePaths ourselves
// and just report the failure count.
using var index = new Index(indexPath, parsePaths: false);
int failed = index.ParsePaths();
Console.WriteLine($"ParsePaths failed count: {failed}");
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
var wantedExt = new[] { ".ui", ".xml", ".txt", ".json", ".css", ".lua" };
var toExtract = uiFiles.Where(f =>
{
    var path = f.Path!;
    if (!path.StartsWith("metadata/ui/", StringComparison.OrdinalIgnoreCase))
        return false;
    var ext = Path.GetExtension(path);
    return ext.Length == 0 || wantedExt.Contains(ext, StringComparer.OrdinalIgnoreCase);
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
Console.WriteLine("Done.");
return 0;
