using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     What the census rules read of an object's or monster's script (.ot, docs/research/boss-mechanics.md §14): the scripts it
///     extends, its component sections, a few component fields, the states it declares (with their defaults), its handlers with
///     their bodies and its preloads. Comments are dropped first, as the game's parser does. Pure.
/// </summary>
internal sealed record BossScriptFacts(
    string Path, IReadOnlyList<string> Extends, IReadOnlyList<string> Sections, IReadOnlyList<(string Name, string Value)> Fields,
    IReadOnlyList<(string Name, string? Default)> States, IReadOnlyList<(string Name, string Body)> Handlers, IReadOnlyList<(string Kind, string Value)> Preloads)
{
    private static readonly Regex Comments = new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline);
    private static readonly Regex ExtendsLine = new(@"^\s*extends\s+""([^""]+)""", RegexOptions.Multiline);
    private static readonly Regex StateList = new(@"define_\w*state\s*=\s*""([^""]*)""");
    private static readonly Regex HandlerStart = new(@"\b(on_[A-Za-z0-9_]+|create_state_[A-Za-z0-9_]+|create_transitioned|create_untransitioned|on_transition)\s*=");
    private static readonly Regex SectionStart = new(@"^[ \t]*([A-Za-z][A-Za-z0-9_]*)[ \t]*\r?\n[ \t]*\{", RegexOptions.Multiline);
    private static readonly Regex PreloadLine = new(@"\b(preload_[a-z_]+)\s*=\s*""([^""]+)""");
    private static readonly Regex FieldLine = new(@"\b(start_blocked|blocking|transition_on_damage_taken|attackable|server_only)\s*=\s*(""[^""]*""|[^\s;]+)");
    private static readonly Regex StateHandler = new(@"^(?:on_or_create_state|on_state|create_state|on_exit_state)_(\w+?)_(\d+)$");

    /// <summary>The section a script's client half is written in: an object with it is drawn by the client even when server-only.</summary>
    public const string ClientSection = "client";

    public static BossScriptFacts Parse(string path, string text)
    {
        var plain = Comments.Replace(text.TrimStart('﻿'), string.Empty);
        var states = StateList.Matches(plain).SelectMany(match => match.Groups[1].Value.Split(';'))
            .Select(state => state.Split('=', 2)).Where(parts => parts[0].Trim().Length > 0)
            .Select(parts => (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : (string?)null)).ToList();
        var handlers = HandlerStart.Matches(plain).Select(match => (match.Groups[1].Value, Body(plain, match.Index + match.Length))).ToList();
        return new BossScriptFacts(path, ExtendsLine.Matches(plain).Select(match => match.Groups[1].Value).Where(parent => parent != "nothing").ToList(),
            SectionStart.Matches(plain).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList(),
            FieldLine.Matches(plain).Select(match => (match.Groups[1].Value, match.Groups[2].Value.Trim('"'))).ToList(), states, handlers,
            PreloadLine.Matches(plain).Select(match => (match.Groups[1].Value, match.Groups[2].Value)).ToList());
    }

    /// <summary>A state handler's state and value ("on_or_create_state_invis_1" → invis, 1), or null for another handler.</summary>
    public static (string State, int Value)? StateOf(string handler) =>
        StateHandler.Match(handler) is { Success: true } match && int.TryParse(match.Groups[2].Value, out var value) ? (match.Groups[1].Value, value) : null;

    /// <summary>Whether a field is set to <paramref name="value"/> in the script.</summary>
    public bool Has(string field, string value) => this.Fields.Any(pair => pair.Name == field && string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>A handler's body: a braced block, a quoted string, or the rest of its line.</summary>
    private static string Body(string text, int start)
    {
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        if (start >= text.Length)
        {
            return string.Empty;
        }

        if (text[start] == '"')
        {
            var close = text.IndexOf('"', start + 1);
            return close > start ? text[(start + 1)..close] : text[(start + 1)..];
        }

        if (text[start] != '{')
        {
            var end = text.IndexOf('\n', start);
            return end > start ? text[start..end] : text[start..];
        }

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            depth += text[i] == '{' ? 1 : text[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return text[start..(i + 1)];
            }
        }

        return text[start..];
    }
}

/// <summary>
///     A script and every script it extends, nearest first, read as one: what the census rules ask of an entity. Its components
///     and fields come from the whole chain; its states, handlers and preloads from its own scripts, the shared bases
///     (<see cref="IsShared"/>: Monster, BossBase, InanimateObject) left out, as the census read them. Pure.
/// </summary>
internal sealed class BossScriptChain
{
    /// <summary>A script extended by this many others or more is a shared base, not a fight's own (the census's threshold, scripts.py).</summary>
    internal const int SharedBase = 25;

    public BossScriptChain(IReadOnlyList<BossScriptFacts> scripts, Func<string, bool>? isShared = null)
    {
        this.Scripts = scripts;
        this.Own = scripts.Where(script => isShared?.Invoke(script.Path) != true).ToList();
    }

    public IReadOnlyList<BossScriptFacts> Scripts { get; }

    /// <summary>The chain's scripts without the shared bases.</summary>
    public IReadOnlyList<BossScriptFacts> Own { get; }

    public bool Found => this.Scripts.Count > 0;

    public IEnumerable<(string Name, string Body)> Handlers => this.Own.SelectMany(script => script.Handlers);

    public IEnumerable<(string Name, string? Default)> States => this.Own.SelectMany(script => script.States);

    public IEnumerable<(string Kind, string Value)> Preloads => this.Own.SelectMany(script => script.Preloads);

    /// <summary>The parents extended by <see cref="SharedBase"/> scripts or more, from every script's extends lines.</summary>
    public static HashSet<string> SharedBases(IEnumerable<IReadOnlyList<string>> extends) =>
        extends.SelectMany(parents => parents.Distinct(StringComparer.OrdinalIgnoreCase)).GroupBy(parent => parent, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= SharedBase).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool HasSection(string section) => this.Scripts.Any(script => script.Sections.Contains(section, StringComparer.Ordinal));

    public bool Has(string field, string value) => this.Scripts.Any(script => script.Has(field, value));

    public bool Extends(string parent) => this.Scripts.Any(script => script.Extends.Contains(parent, StringComparer.OrdinalIgnoreCase));

    /// <summary>The chain of <paramref name="path"/> through <paramref name="factsOf"/> (null for a script the index lacks), each script once.</summary>
    public static BossScriptChain Of(string path, Func<string, BossScriptFacts?> factsOf, Func<string, bool>? isShared = null)
    {
        var chain = new List<BossScriptFacts>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(path);
        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (next.Length == 0 || !seen.Add(next) || factsOf(next) is not { } facts)
            {
                continue;
            }

            chain.Add(facts);
            foreach (var parent in facts.Extends)
            {
                queue.Enqueue(parent);
            }
        }

        return new BossScriptChain(chain, isShared);
    }
}
