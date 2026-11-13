using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     The census's rules on states (docs/research/boss-mechanics.md §14-§15): the boss's states that change its arena, count a
///     task or name its element; the state marking the true one among its clones; the state of the person who shelters the
///     player from the server's area named for them. Pure.
/// </summary>
internal static class BossStateRules
{
    private const string BracketCall = "TriggerBracket(";
    private const string ArenaWord = "arena";
    private const string ChangeKind = "change";

    /// <summary>The arena changes the plugin can name, each the start of a word of the state's name ("room_burning_progress" → burn).</summary>
    private static readonly string[] ArenaKinds = { "burn", "dark", "flood", "lava", "collapse", "shrink", "sandstorm", "maze", "retreat", "platform" };

    /// <summary>Handlers and words of a fight's end, not an arena change ("dead", "fight_complete", on_defeat).</summary>
    private static readonly HashSet<string> EndHandlers = new(StringComparer.Ordinal) { "on_death", "on_defeat", "on_spawned_dead", "on_construction_complete" };
    private static readonly HashSet<string> EndWords = new(StringComparer.Ordinal) { "dead", "death", "die", "died", "defeat", "defeated", "killed" };

    /// <summary>A state whose last word is a measure holds a number about the change (its range), not the change.</summary>
    private static readonly HashSet<string> Measures = new(StringComparer.Ordinal) { "range", "radius", "distance", "size", "speed", "timer", "cooldown", "duration" };

    private static readonly Regex ProgressName = new("progress|collect|timetrial|count(?!er)");
    private static readonly Regex ElementName = new("element|infuse|infusion");
    private static readonly HashSet<string> CloneWords = new(StringComparer.Ordinal) { "clone", "clones", "illusion", "decoy", "mirror", "fake", "copy" };
    private static readonly HashSet<string> TrueWords = new(StringComparer.Ordinal) { "chosen", "real", "true", "original", "fake", "decoy" };
    private static readonly HashSet<string> ShelterWords = new(StringComparer.Ordinal) { "defence", "defense", "shield", "shelter", "sanctuary", "protect", "protection", "barrier", "ward" };
    private static readonly Regex Played = new(@"\b(PlayTextAudio|QueueTextAudio|PlayAnimation\w*)\(");

    /// <summary>The damage elements a handler's words may name, each with the words that name it ("infused_ice" is cold).</summary>
    private static readonly (string Element, string[] Words)[] Elements =
    {
        ("fire", new[] { "fire", "flame", "flames" }), ("cold", new[] { "cold", "ice", "frost" }), ("lightning", new[] { "lightning" }),
        ("chaos", new[] { "chaos" }), ("physical", new[] { "physical" }),
    };

    public static IEnumerable<string> Lines(BossFight fight) => Arena(fight).Concat(Progress(fight)).Concat(Element(fight)).Concat(Clones(fight)).Concat(Refuges(fight));

    /// <summary>AR: a state whose handler triggers an arena bracket, or one named for an arena change; never the fight's end.</summary>
    private static IEnumerable<string> Arena(BossFight fight)
    {
        var bracketed = fight.Script.Handlers.Where(handler => !EndHandlers.Contains(handler.Name) && handler.Body.Contains(BracketCall, StringComparison.Ordinal))
            .Select(handler => BossScriptFacts.StateOf(handler.Name)?.State).OfType<string>();
        var named = fight.Script.States.Select(state => state.Name).Concat(fight.Script.Handlers.Select(handler => BossScriptFacts.StateOf(handler.Name)?.State).OfType<string>())
            .Where(state => BossScripts.WordsOf(state) is var words && words.Any(word => word == ArenaWord || ArenaKinds.Any(kind => word.StartsWith(kind, StringComparison.Ordinal))) && !Measures.Contains(words[^1]));
        return bracketed.Concat(named).Where(state => !IsEnd(state)).Distinct(StringComparer.Ordinal).Select(state => $"arena|{fight.Boss}|{state}|{KindOf(state)}");
    }

    /// <summary>The arena change a state's name says, "change" when it says none.</summary>
    internal static string KindOf(string state) =>
        BossScripts.WordsOf(state).Select(word => ArenaKinds.FirstOrDefault(kind => word.StartsWith(kind, StringComparison.Ordinal))).FirstOrDefault(kind => kind != null) ?? ChangeKind;

    private static bool IsEnd(string state) =>
        BossScripts.WordsOf(state) is var words && (words.Any(EndWords.Contains) || (words.Contains("fight") && words.Any(word => word is "complete" or "over" or "end")));

    /// <summary>MG: a state counting a task, with its maximum when a handler compares it with a number.</summary>
    private static IEnumerable<string> Progress(BossFight fight)
    {
        foreach (var state in fight.Script.States.Select(state => state.Name).Where(state => ProgressName.IsMatch(state)).Distinct(StringComparer.Ordinal))
        {
            yield return MaximumOf(fight.Script, state) is { } max ? $"progress|{fight.Boss}|{state}|{max}" : $"progress|{fight.Boss}|{state}";
        }
    }

    /// <summary>The largest number a handler compares the state with (IfState( s, 5 …), GetState( s ) &gt;= 5), or null.</summary>
    internal static int? MaximumOf(BossScriptChain script, string state)
    {
        var name = Regex.Escape(state);
        var compares = new Regex($@"IfState\(\s*{name}\s*,\s*(\d+)|GetState\(\s*{name}\s*\)\s*(?:==|>=|>)\s*(\d+)");
        var found = script.Handlers.SelectMany(handler => compares.Matches(handler.Body))
            .Select(match => int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value, CultureInfo.InvariantCulture)).Where(value => value > 0).ToList();
        return found.Count > 0 ? found.Max() : null;
    }

    /// <summary>EL: each value of an element state whose handler names one element; the state alone when none does.</summary>
    private static IEnumerable<string> Element(BossFight fight)
    {
        foreach (var state in fight.Script.States.Select(state => state.Name).Where(state => ElementName.IsMatch(state)).Distinct(StringComparer.Ordinal))
        {
            var named = fight.Script.Handlers.Select(handler => (Of: BossScriptFacts.StateOf(handler.Name), handler.Body)).Where(handler => handler.Of?.State == state)
                .Select(handler => (handler.Of!.Value.Value, Element: ElementOf(handler.Body))).Where(value => value.Element != null)
                .DistinctBy(value => value.Value).OrderBy(value => value.Value).ToList();
            if (named.Count == 0)
            {
                yield return $"element|{fight.Boss}|{state}|-";
            }

            foreach (var (value, element) in named)
            {
                yield return $"element|{fight.Boss}|{state}={value}|{element}";
            }
        }
    }

    /// <summary>The one element a handler's words name, or null for none or several.</summary>
    internal static string? ElementOf(string body)
    {
        var words = BossScripts.WordsOf(body).ToHashSet(StringComparer.Ordinal);
        var named = Elements.Where(element => element.Words.Any(words.Contains)).Select(element => element.Element).ToList();
        return named.Count == 1 ? named[0] : null;
    }

    /// <summary>CL: an object or add of a clone kind (its path says so) and the state it declares that marks the true one.</summary>
    private static IEnumerable<string> Clones(BossFight fight) =>
        fight.Objects.Select(item => (item.Path, item.Script)).Concat(fight.Adds.Select(add => (Path: add.Id, add.Script)))
            .Where(entity => BossScripts.WordsOf(entity.Path[(entity.Path.LastIndexOf('/') + 1)..]).Any(CloneWords.Contains))
            .SelectMany(entity => entity.Script.States.Select(state => state.Name).Where(state => BossScripts.WordsOf(state).Any(TrueWords.Contains)).Distinct(StringComparer.Ordinal)
                .Select(state => $"clone|{fight.Boss}|{entity.Path}|{state}"));

    /// <summary>
    ///     The person a shelter comes from: the add named in the shelter's buff id (asala_sand_shield, Sekhema Asala) whose script plays
    ///     a line or an animation when a state named for a defence is set; that state is non-zero while it shelters. The add must be
    ///     in this fight: brought by the boss, placed in its arena, or its state named by an object of the fight (MatriarchSandstormDefence).
    /// </summary>
    private static IEnumerable<string> Refuges(BossFight fight)
    {
        var shelters = BossCensusRules.ShelterBuffs(fight).ToList();
        var objectWords = fight.Objects.Select(item => BossScripts.WordsOf(item.Path[(item.Path.LastIndexOf('/') + 1)..]).ToHashSet(StringComparer.Ordinal)).ToList();
        foreach (var add in fight.Adds)
        {
            var nameWords = BossScripts.WordsOf(add.Name).ToHashSet(StringComparer.Ordinal);
            if (!shelters.Any(buff => BossScripts.WordsOf(buff).FirstOrDefault() is { } first && nameWords.Contains(first)))
            {
                continue;
            }

            foreach (var state in add.Script.Handlers.Where(handler => BossScriptFacts.StateOf(handler.Name) is { Value: > 0 } of && BossScripts.WordsOf(of.State).Any(ShelterWords.Contains) && Played.IsMatch(handler.Body))
                         .Select(handler => BossScriptFacts.StateOf(handler.Name)!.Value.State).Distinct(StringComparer.Ordinal)
                         .Where(state => add.InArena || objectWords.Any(words => BossScripts.WordsOf(state).All(words.Contains))))
            {
                yield return $"refuge|{fight.Boss}|{add.Id}|{state}";
            }
        }
    }
}
