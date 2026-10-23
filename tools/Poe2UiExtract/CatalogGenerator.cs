using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>
/// Drives the "catalog" mode: parses every *.ui file under a root directory (see
/// docs/research/ui-layouts.md for the DSL) and writes a JSON catalog of every control that has an id.
/// </summary>
internal static class CatalogGenerator
{
    public static int Run(string uiRootDir, string outJsonPath)
    {
        if (!Directory.Exists(uiRootDir))
        {
            Console.WriteLine($"UI root directory not found: {uiRootDir}");
            return 1;
        }

        var uiFiles = Directory.EnumerateFiles(uiRootDir, "*.ui", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFileName(f), "credits.ui", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"Found {uiFiles.Count} .ui files under {uiRootDir} (credits.ui excluded).");

        var templates = new Dictionary<string, TemplateInfo>();
        var controls = new List<RawControl>();
        var controlsByFile = new Dictionary<string, Dictionary<string, RawControl>>();
        var parser = new UiScriptParser(templates, controls, controlsByFile);

        int parsedCount = 0;
        foreach (var file in uiFiles)
        {
            string relScreen = Path.GetRelativePath(uiRootDir, file).Replace('\\', '/').ToLowerInvariant();
            string text;
            try
            {
                // .ui files are UTF-16LE with a BOM; StreamReader auto-detects it, TrimStart is a
                // defensive backstop in case detection ever fails.
                text = File.ReadAllText(file, Encoding.Unicode).TrimStart('﻿');
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR reading {relScreen}: {ex.Message}");
                continue;
            }

            try
            {
                parser.ParseFile(text, relScreen);
                parsedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR parsing {relScreen}: {ex.Message}");
            }
        }

        ClassResolver.ResolveAll(controls, templates, controlsByFile);

        var root = new UiCatalogRoot
        {
            GeneratedAt = DateTime.UtcNow.ToString("o"),
            SourceCount = parsedCount,
            Controls = controls.Select(c => new UiControlRecord
            {
                Id = c.Id,
                ClassName = c.ClassName,
                BaseName = c.BaseName,
                ResolvedClass = c.ResolvedClass,
                Screen = c.Screen,
                ParentId = c.ParentId,
                TextKey = c.TextKey,
                TextLiteral = c.TextLiteral,
                TipTextKey = c.TipTextKey,
                Focusable = c.Focusable,
                TabIndex = c.TabIndex,
                Tag = c.Tag,
                KeyboardEnabled = c.KeyboardEnabled,
                Depth = c.Depth,
            }).ToList(),
        };

        var outDir = Path.GetDirectoryName(Path.GetFullPath(outJsonPath));
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        File.WriteAllText(outJsonPath, JsonSerializer.Serialize(root, jsonOptions));

        PrintSummary(uiFiles.Count, parsedCount, controls);
        Console.WriteLine($"Catalog written to: {outJsonPath}");
        return 0;
    }

    private static void PrintSummary(int fileCount, int parsedCount, List<RawControl> controls)
    {
        Console.WriteLine($"Files parsed: {parsedCount} / {fileCount}");
        Console.WriteLine($"Controls emitted: {controls.Count}");

        var byClass = controls
            .GroupBy(c => c.ResolvedClass)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Console.WriteLine($"Distinct resolvedClass values: {byClass.Count}");
        foreach (var g in byClass)
            Console.WriteLine($"  {g.Key}: {g.Count()}");

        var topIds = controls
            .GroupBy(c => c.Id)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        Console.WriteLine("Top 10 most common ids:");
        foreach (var g in topIds)
            Console.WriteLine($"  {g.Key}: {g.Count()}");
    }
}
