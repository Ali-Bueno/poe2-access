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

// Args: paths <gameDir> <part>  -- research aid: every bundled file path containing <part> (quest reward icons' names).
if (args.Length > 2 && string.Equals(args[0], "paths", StringComparison.OrdinalIgnoreCase))
{
    using var pathIndex = SharedBundles.Open(Path.Combine(args[1], "Bundles2", "_.index.bin"));
    pathIndex.ParsePaths();
    foreach (var path in pathIndex.Files.Values.Select(file => file.Path).Where(path => path != null && path.Contains(args[2], StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(path);
    }

    return 0;
}

// Args: cat <gameDir> <path> [maxChars]  -- research aid: a bundled UTF-16 text file (.ao, .aoc, .ot) printed as text.
if (args.Length > 2 && string.Equals(args[0], "cat", StringComparison.OrdinalIgnoreCase))
{
    using var catIndex = SharedBundles.Open(Path.Combine(args[1], "Bundles2", "_.index.bin"));
    catIndex.ParsePaths();
    var catFile = catIndex.Files.Values.FirstOrDefault(file => string.Equals(file.Path, args[2], StringComparison.OrdinalIgnoreCase));
    if (catFile == null)
    {
        Console.WriteLine($"No file {args[2]}");
        return 1;
    }

    var text = System.Text.Encoding.Unicode.GetString(catFile.Read().ToArray()).TrimStart('﻿');
    Console.WriteLine(args.Length > 3 ? text[..Math.Min(text.Length, int.Parse(args[3]))] : text);
    return 0;
}

// Args: ao-sounds <gameDir> <pathPart>  -- research aid: every .ao whose path contains <pathPart> with the sound
// events it plays (path <TAB> distinct "Audio/..." filenames).
if (args.Length > 2 && string.Equals(args[0], "ao-sounds", StringComparison.OrdinalIgnoreCase))
{
    using var aoIndex = SharedBundles.Open(Path.Combine(args[1], "Bundles2", "_.index.bin"));
    aoIndex.ParsePaths();
    var audioName = new System.Text.RegularExpressions.Regex("""
        "filename"\s*:\s*"(Audio/[^"]+)"
        """);
    foreach (var file in aoIndex.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(".ao", StringComparison.OrdinalIgnoreCase)
        && file.Path.Contains(args[2], StringComparison.OrdinalIgnoreCase)).OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
    {
        var aoText = System.Text.Encoding.Unicode.GetString(file.Read().ToArray());
        var names = audioName.Matches(aoText).Select(match => match.Groups[1].Value).Distinct().ToList();
        Console.WriteLine($"{file.Path}	{string.Join(" | ", names)}");
    }

    return 0;
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

// Args: dat-rows <gameDir> <table> <id substring>  -- research aid: the raw bytes (as int32s) of the rows whose id matches.
if (args.Length > 3 && string.Equals(args[0], "dat-rows", StringComparison.OrdinalIgnoreCase))
{
    using var bundles = SharedBundles.Open(Path.Combine(args[1], "Bundles2", "_.index.bin"));
    bundles.ParsePaths();
    var table = DatTable.Open(bundles, args[2]);
    if (table == null)
    {
        Console.WriteLine($"No table {args[2]}");
        return 1;
    }

    for (var row = 0; row < table.RowCount; row++)
    {
        var id = table.StringOrNull(row, 0) ?? string.Empty;
        if (id.Contains(args[3], StringComparison.OrdinalIgnoreCase))
        {
            var words = Enumerable.Range(0, (table.RowSize - 8) / 2).Select(at => 8 + (at * 2)).Where(at => at + 4 <= table.RowSize)
                .Select(at => $"+{at:X}={table.Int32(row, at)}");
            Console.WriteLine($"{row} {id}: {string.Join(' ', words)}");
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

// Args: hazards <gameDir> [outFile]  -- buffs from standing in harmful ground, for the hazard alarm.
if (args.Length > 1 && string.Equals(args[0], "hazards", StringComparison.OrdinalIgnoreCase))
{
    return HazardExtractor.Run(args[1], args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\hazards.txt");
}

// Args: cull-skills <gameDir> [outFile]  -- granted effects that deal Culling Strike, for the mod's culling cue.
if (args.Length > 1 && string.Equals(args[0], "cull-skills", StringComparison.OrdinalIgnoreCase))
{
    return CullSkillExtractor.Run(args[1], args.Length > 2 ? args[2] : @"Poe2Accessibility\catalog\cull-skills.txt");
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

// Args: batch <gameDir> <command> <outFile> [...]  -- item-sounds, areas, dat-columns, experience, hit-times, hazards, cull-skills, places and
// ui-catalog on one opened index (the launcher after a game patch); each output complete or absent.
if (args.Length > 3 && string.Equals(args[0], "batch", StringComparison.OrdinalIgnoreCase))
{
    return Batch.Run(args[1], args[2..]);
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
var code = UiScripts.Extract(index, outDir);
Console.WriteLine("Done.");
return code;
