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

// Args: sounds <gameDir> [outDir] [itemSoundsJson]  -- decode the audio-cue set (plus the drop samples named in the item
// catalog, as items/<sample>.wav) from the FMOD banks into WAV files + manifest.txt.
if (args.Length > 0 && string.Equals(args[0], "sounds", StringComparison.OrdinalIgnoreCase))
{
    string game = args.Length > 1 ? args[1] : @"D:\games\steam\steamapps\common\Path of Exile 2";
    return SoundExtractor.Run(game, args.Length > 2 ? args[2] : @"Poe2Accessibility\sounds",
        args.Length > 3 ? args[3] : @"Poe2Accessibility\catalog\item-sounds.json");
}

// Args: dat-strings <gameDir> <table> [language] [rows]  -- research aid: every string column of a table's first rows.
if (args.Length > 2 && string.Equals(args[0], "dat-strings", StringComparison.OrdinalIgnoreCase))
{
    using var bundles = SharedBundles.Open(Path.Combine(args[1], "Bundles2", "_.index.bin"));
    bundles.ParsePaths();
    var language = args.Length > 3 && args[3] != "english" ? args[3] : null;
    var table = language == null ? DatTable.Open(bundles, args[2]) : DatTable.OpenTranslated(bundles, args[2], language);
    if (table == null)
    {
        Console.WriteLine($"No table {args[2]}");
        return 1;
    }

    var rows = Math.Min(table.RowCount, args.Length > 4 ? int.Parse(args[4]) : 20);
    Console.WriteLine($"{args[2]}: {table.RowCount} rows of 0x{table.RowSize:X} bytes");
    for (var offset = 0; offset + 8 <= table.RowSize; offset++)
    {
        var texts = Enumerable.Range(0, rows).Select(row => table.StringOrNull(row, offset)).ToList();
        if (texts.Count(text => text != null) >= rows * 0.8 && texts.Any(text => !string.IsNullOrEmpty(text)))
        {
            Console.WriteLine($"+0x{offset:X}: {string.Join(" | ", texts.Select(text => text ?? "?"))}");
        }
    }

    return 0;
}

// Args: item-sounds <gameDir> [outFile]  -- base item id -> drop sample (ItemDrops.bank) from the dat tables as JSON.
if (args.Length > 0 && string.Equals(args[0], "item-sounds", StringComparison.OrdinalIgnoreCase))
{
    string game = args.Length > 1 ? args[1] : @"D:\games\steam\steamapps\common\Path of Exile 2";
    return ItemSoundExtractor.Run(game, args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\item-sounds.json");
}

// Args: hit-times <gameDir> [outFile]  -- when each monster attack lands, from the animation data, for dodge warnings.
if (args.Length > 1 && string.Equals(args[0], "hit-times", StringComparison.OrdinalIgnoreCase))
{
    return HitTimeExtractor.Run(args[1], args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\hit-times.txt");
}

// Args: places <gameDir> [outFile]  -- named map pieces and props for the place announcements (docs/research/places.md).
if (args.Length > 1 && string.Equals(args[0], "places", StringComparison.OrdinalIgnoreCase))
{
    return PlaceExtractor.Run(args[1], args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\places.json");
}

// Args: experience <gameDir> [outFile]  -- the total experience each character level starts at.
if (args.Length > 1 && string.Equals(args[0], "experience", StringComparison.OrdinalIgnoreCase))
{
    return ExperienceExtractor.Run(args[1], args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\experience.json");
}

// Args: item-names <gameDir> [outFile]  -- currency item names per language, for inline item art in legends.
if (args.Length > 1 && string.Equals(args[0], "item-names", StringComparison.OrdinalIgnoreCase))
{
    return ItemNameExtractor.Run(args[1], args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\item-names.json");
}

// Args: areas <gameDir> <outFile>  -- localized world-area names from WorldAreas.datc64 as { lang: { id: name } }.
if (args.Length > 0 && string.Equals(args[0], "areas", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: Poe2UiExtract.exe areas <gameDir> <outFile>");
        return 1;
    }
    return AreaExtractor.Run(args[1], args[2]);
}

// Args: dat-columns <gameDir> <outFile>  -- the dat columns the plugin reads names from, found again in this install.
if (args.Length > 0 && string.Equals(args[0], "dat-columns", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: Poe2UiExtract.exe dat-columns <gameDir> <outFile>");
        return 1;
    }
    return DatColumnExtractor.Run(args[1], args[2]);
}

// Args: [0] = path to _.index.bin, [1] = output dir
string indexPath = args.Length > 0 ? args[0] : @"D:\games\steam\steamapps\common\Path of Exile 2\Bundles2\_.index.bin";
string outDir = args.Length > 1 ? args[1] : @"out";

Console.WriteLine($"Opening index (read-only intent): {indexPath}");

// Open with parsePaths:false so a handful of unparsable paths (known PoE2 quirk,
// see LibGGPK3 issue #25) don't abort the whole run; we call ParsePaths ourselves
// and just report the failure count.
using var index = SharedBundles.Open(indexPath);
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
