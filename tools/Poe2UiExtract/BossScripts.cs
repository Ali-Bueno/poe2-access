using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     What a monster's script (.ot) says of its fight (docs/research/boss-mechanics.md): the scripts it extends, the
///     state values that stop drawing it, the states whose value is the fight's phase (they set a "*Phase" music
///     parameter), its enrage states, the states in which it hides to be found, the voice lines it plays and from which
///     handler, the monsters and objects it preloads (its adds, its next forms, its arena objects), and whether it blocks the way (a wall). Pure.
/// </summary>
internal static class BossScripts
{
    /// <summary>One script's facts, before its parents are merged in.</summary>
    internal sealed record Script(
        IReadOnlyList<string> Extends, IReadOnlyList<(string State, int Value)> Hides, IReadOnlyList<string> PhaseStates,
        IReadOnlyList<string> EnrageStates, IReadOnlyList<(string Handler, string Line)> Lines, IReadOnlyList<string> Monsters,
        IReadOnlyList<string> Objects, IReadOnlyList<string> SeekStates, bool Blocks);

    /// <summary>The handler prefix of a skill starting: <c>on_effect_start_&lt;GrantedEffects id&gt;</c>.</summary>
    internal const string SkillStartHandler = "on_effect_start_";

    // The client call that stops drawing an object, and the music parameter naming the fight's phase (BaronPhase, DoryaniPhase).
    private const string HideCall = "DisableRendering(";
    private const string PhaseParameter = "Phase";

    // The effect pack that draws an object by its distance from the player: a boss hiding until the player comes near (Balbala).
    private const string AddPackCall = "AddEffectPack(";
    private const string SeekPack = "fade_by_player_dist";

    private static readonly Regex ExtendsLine = new(@"^\s*extends\s+""([^""]+)""", RegexOptions.Multiline);
    private static readonly Regex SharedStates = new(@"define_shared_state\s*=\s*""([^""]*)""");
    private static readonly Regex StateHandler = new(@"\b(?:on_or_create_state|on_state|create_state)_(\w+)_(\d+)\s*=\s*");
    private static readonly Regex PhaseCall = new(@"SetGlobalAudioParam\(\s*(\w+)");
    private static readonly Regex LineCall = new(@"\b(?:PlayTextAudio|QueueTextAudio)\(\s*(\w+)");
    private static readonly Regex HandlerName = new(@"^\s*(\w+)\s*=");
    private static readonly Regex Blocking = new(@"\bblocking\s*=\s*true\b");
    private static readonly Regex PreloadMonster = new(@"preload_monster_variety\s*=\s*""([^""]+)""");
    private static readonly Regex PreloadObject = new(@"preload_object\s*=\s*""([^""]+)""");
    private static readonly Regex Words = new("[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+");

    /// <summary>
    ///     Words of a voice line's id or handler that mark it as no mechanic: its entrance, intro, death, aggro, a player's
    ///     death, taunts, a knock-down, a poem or a greeting (the ids of 2026-11-08: SwollenMillerAggro, CorruptWolf_Skill_PlayerDeath).
    /// </summary>
    private static readonly HashSet<string> Flavour = new(StringComparer.Ordinal)
    {
        "intro", "emerge", "emerges", "emerged", "death", "dies", "died", "dead", "defeat", "defeated", "aggro", "kill", "kills",
        "killed", "taunt", "taunts", "victory", "outro", "lullaby", "engage", "engaged", "poem", "knockdown", "stun", "stunned",
        "greeting", "approach", "proximity", "flee", "laugh", "idle", "dialogue", "cutscene", "camera", "triggered",
        "construction", "delete", "spawned", "player", "players", "encounter", "loss", "win", "percent", "halfway",
    };

    /// <summary>Words of a line's or its skill's id that name a fight's turn (CorruptWolf_Skill_FogPhaseStart, Kabala_Summon, GSBaronBossColdBreathChannel).</summary>
    private static readonly HashSet<string> MechanicWords = new(StringComparer.Ordinal)
    {
        "phase", "enrage", "enraged", "summon", "summons", "transform", "transformation", "channel", "channelled", "hex",
    };

    /// <summary>ActiveSkills kinds of a fight's turn: summons, stance changes (a boss changing form) and channels, as prefix or suffix.</summary>
    private static readonly string[] MechanicKinds = { "summon_", "change_to_stance", "_channelled" };

    /// <summary>A fight's opening, not a mechanic: the boss's intro skill (CTS1BossFightStart).</summary>
    private static readonly string[] FightStart = { "fight", "start" };

    public static Script Parse(string text)
    {
        var extends = ExtendsOf(text);
        var hides = new List<(string, int)>();
        var phases = new List<string>();
        var seeks = new List<string>();
        foreach (Match handler in StateHandler.Matches(text))
        {
            var body = Body(text, handler.Index + handler.Length);
            var (state, value) = (handler.Groups[1].Value, int.Parse(handler.Groups[2].Value));
            if (value != 0 && body.Contains(AddPackCall, StringComparison.Ordinal) && body.Contains(SeekPack, StringComparison.OrdinalIgnoreCase) && !seeks.Contains(state))
            {
                seeks.Add(state);
            }

            if (body.Contains(HideCall, StringComparison.Ordinal) && !hides.Contains((state, value)))
            {
                hides.Add((state, value));
            }

            if (PhaseCall.Matches(body).Any(call => call.Groups[1].Value.Contains(PhaseParameter, StringComparison.Ordinal)) && !phases.Contains(state))
            {
                phases.Add(state);
            }
        }

        // "enraged", "enrage": not "enrage_swings" or "enrage_time", counters beside it (Rudja, Tangmazu).
        var enrages = StatesOf(text).Where(state => WordsOf(state) is { Count: > 0 } words && words[^1] is "enrage" or "enraged").ToList();
        return new Script(extends, hides, phases, enrages, Lines(text),
            PreloadMonster.Matches(text).Select(match => match.Groups[1].Value).Distinct().ToList(),
            PreloadObject.Matches(text).Select(match => match.Groups[1].Value).Distinct().ToList(), seeks, Blocking.IsMatch(text));
    }

    /// <summary>The states a script declares itself (define_shared_state "spike; rumble; trap_speed = 2;"), without their defaults.</summary>
    internal static IReadOnlyList<string> StatesOf(string text) =>
        SharedStates.Matches(text).SelectMany(match => match.Groups[1].Value.Split(';'))
            .Select(state => state.Split('=')[0].Trim()).Where(state => state.Length > 0).Distinct().ToList();

    /// <summary>The scripts a script extends ("nothing" left out).</summary>
    internal static IReadOnlyList<string> ExtendsOf(string text) =>
        ExtendsLine.Matches(text).Select(match => match.Groups[1].Value).Where(path => path != "nothing").ToList();

    /// <summary>Each voice line the script plays, with the handler or function it is played from.</summary>
    private static List<(string Handler, string Line)> Lines(string text)
    {
        var lines = new List<(string, string)>();
        var rows = text.Split('\n');
        for (var i = 0; i < rows.Length; i++)
        {
            foreach (Match call in LineCall.Matches(rows[i]))
            {
                var handler = string.Empty;
                for (var j = i; j >= 0; j--)
                {
                    if (HandlerName.Match(rows[j]) is { Success: true } named && !named.Groups[1].Value.StartsWith("this", StringComparison.Ordinal))
                    {
                        handler = named.Groups[1].Value;
                        break;
                    }
                }

                if (!lines.Contains((handler, call.Groups[1].Value)))
                {
                    lines.Add((handler, call.Groups[1].Value));
                }
            }
        }

        return lines;
    }

    /// <summary>
    ///     Whether a voice line announces a fight's turn rather than one blow: its id or its skill's id names a phase, a
    ///     summon, an enrage, a transformation, a channel or a hex, or its skill is a summon, a stance change or a channel
    ///     (ActiveSkills kind); never a line whose id is flavour. Attack cries ("Split you!") stay muted: the dodge cues tell those.
    /// </summary>
    public static bool AnnouncesMechanic(string handler, string lineId, string? skillKind)
    {
        var words = WordsOf(lineId);
        if (words.Any(Flavour.Contains) || FightStart.All(words.Contains) || FightStart.All(WordsOf(handler).Contains))
        {
            return false;
        }

        return words.Concat(WordsOf(SkillOf(handler) ?? string.Empty)).Any(MechanicWords.Contains) ||
               (skillKind != null && MechanicKinds.Any(kind => skillKind.StartsWith(kind, StringComparison.Ordinal) || skillKind.EndsWith(kind, StringComparison.Ordinal)));
    }

    /// <summary>The skill a line is played for, from an <c>on_effect_start_</c> handler; null otherwise.</summary>
    public static string? SkillOf(string handler) =>
        handler.StartsWith(SkillStartHandler, StringComparison.Ordinal) && handler.Length > SkillStartHandler.Length ? handler[SkillStartHandler.Length..] : null;

    /// <summary>
    ///     The NPCTextAudio ids a played id stands for: a "…Random" id picks among its numbered siblings
    ///     ("CorruptWolf_Skill_IceBreath_Random" → "…_2" … "…_6"), itself included.
    /// </summary>
    public static bool IsVariant(string played, string id)
    {
        if (id == played)
        {
            return true;
        }

        if (!played.EndsWith(RandomSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var stem = played[..^RandomSuffix.Length].TrimEnd('_');
        if (!id.StartsWith(stem, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = id[stem.Length..].TrimStart('_');
        return rest.Length > 0 && rest.All(char.IsAsciiDigit);
    }

    /// <summary>An id without its "Random" or number ending, shared by a line and its siblings.</summary>
    public static string Stem(string id)
    {
        var stem = id.EndsWith(RandomSuffix, StringComparison.Ordinal) ? id[..^RandomSuffix.Length] : id.TrimEnd("0123456789".ToCharArray());
        return stem.TrimEnd('_');
    }

    private const string RandomSuffix = "Random";

    /// <summary>The lower-case words of an id: CamelCase and underscores split, digits apart ("CorruptWolf_Skill_IceBreath_2").</summary>
    internal static List<string> WordsOf(string id) => Words.Matches(id).Select(match => match.Value.ToLowerInvariant()).ToList();

    /// <summary>A text on one catalog line: no field separator, no line breaks.</summary>
    internal static string Clean(string text) => text.Replace(Field, ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

    private const char Field = '|';

    /// <summary>A handler's body: a braced block, a quoted string, or the rest of its line.</summary>
    private static string Body(string text, int start)
    {
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
