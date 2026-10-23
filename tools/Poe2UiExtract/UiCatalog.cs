using System.Collections.Generic;

/// <summary>
/// Public JSON-serializable model for the UI catalog produced by <c>Poe2UiExtract catalog</c>.
/// Property names are written camelCase by the serializer options in <see cref="CatalogGenerator"/>.
/// </summary>
public sealed class UiControlRecord
{
    public required string Id { get; init; }
    public required string ClassName { get; init; }
    public string? BaseName { get; init; }
    public required string ResolvedClass { get; init; }
    public required string Screen { get; init; }
    public string? ParentId { get; init; }
    public string? TextKey { get; init; }
    public string? TextLiteral { get; init; }
    public string? TipTextKey { get; init; }
    public bool? Focusable { get; init; }
    public int? TabIndex { get; init; }
    public string? Tag { get; init; }
    public bool? KeyboardEnabled { get; init; }
    public required int Depth { get; init; }
}

public sealed class UiCatalogRoot
{
    public required string GeneratedAt { get; init; }
    public required int SourceCount { get; init; }
    public required List<UiControlRecord> Controls { get; init; }
}

/// <summary>A template declared via <c>template &lt;Type&gt; id = NAME; ... end</c>, used only to
/// resolve "based on" references; templates are never emitted as controls themselves.</summary>
internal sealed class TemplateInfo
{
    public required string DeclaredType { get; init; }
    public string? BaseName { get; init; }
}

/// <summary>One parsed <c>begin &lt;Type&gt; [based on &lt;Base&gt;] ... end</c> block that has an id.</summary>
internal sealed class RawControl
{
    public required string Id { get; init; }
    public required string ClassName { get; init; }
    public string? BaseName { get; init; }
    public required string Screen { get; init; }
    public string? ParentId { get; init; }
    public required int Depth { get; init; }
    public string? TextKey { get; init; }
    public string? TextLiteral { get; init; }
    public string? TipTextKey { get; init; }
    public bool? Focusable { get; init; }
    public int? TabIndex { get; init; }
    public string? Tag { get; init; }
    public bool? KeyboardEnabled { get; init; }

    /// <summary>Root class of the "based on" chain, fully resolved. Empty until <see cref="ClassResolver"/> runs.</summary>
    public string ResolvedClass { get; set; } = "";
}

/// <summary>
/// Resolves each control's <see cref="RawControl.ResolvedClass"/>: the root of its "based on" chain.
/// A base name is looked up first as a global template (by declared type), then as a control id
/// declared earlier in the SAME source file (by that control's own resolved class); if neither
/// matches, the block's own declared class is kept.
/// </summary>
internal static class ClassResolver
{
    public static void ResolveAll(
        List<RawControl> controls,
        Dictionary<string, TemplateInfo> templates,
        Dictionary<string, Dictionary<string, RawControl>> controlsByFile)
    {
        foreach (var control in controls)
            Resolve(control, templates, controlsByFile, new HashSet<RawControl>());
    }

    private static string Resolve(
        RawControl control,
        Dictionary<string, TemplateInfo> templates,
        Dictionary<string, Dictionary<string, RawControl>> controlsByFile,
        HashSet<RawControl> visitedControls)
    {
        if (control.ResolvedClass.Length > 0)
            return control.ResolvedClass;

        if (!visitedControls.Add(control))
            return control.ClassName; // cycle guard

        string result = control.BaseName is null
            ? control.ClassName
            : ResolveBaseName(control.BaseName, control.ClassName, control.Screen, templates, controlsByFile, visitedControls, new HashSet<string>());

        control.ResolvedClass = result;
        return result;
    }

    private static string ResolveBaseName(
        string baseName,
        string fallback,
        string screen,
        Dictionary<string, TemplateInfo> templates,
        Dictionary<string, Dictionary<string, RawControl>> controlsByFile,
        HashSet<RawControl> visitedControls,
        HashSet<string> visitedNames)
    {
        if (!visitedNames.Add(baseName))
            return fallback; // cycle guard on template-name chains

        // 1) Is the base a known template? Resolve through its own chain (templates fully resolved).
        if (templates.TryGetValue(baseName, out var template))
        {
            return template.BaseName is null
                ? template.DeclaredType
                : ResolveBaseName(template.BaseName, template.DeclaredType, screen, templates, controlsByFile, visitedControls, visitedNames);
        }

        // 2) Is the base a control id declared in the same source file? Resolve through its own chain.
        if (controlsByFile.TryGetValue(screen, out var byId) && byId.TryGetValue(baseName, out var otherControl))
            return Resolve(otherControl, templates, controlsByFile, visitedControls);

        // 3) Unresolved: keep the declared type of the referencing block.
        return fallback;
    }
}
