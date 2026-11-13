using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     The census's class rules (docs/research/boss-mechanics.md §14-§15) over one fight: what its skills, adds, ExecuteGEAL
///     rows, objects and linked buffs make of it, as catalog lines. The rules on its states are in <see cref="BossStateRules"/>. Pure.
/// </summary>
internal static class BossCensusRules
{
    /// <summary>A skill whose area is this long or longer is one a cover is for (the census's rule: Zalmarath's blast is 150).</summary>
    internal const int CoverSkillLength = 60;

    // Names of an invisible helper: the game's "Invisible" and "Daemon" names, an empty one, or such a word in its path (InvisibleFire).
    private static readonly HashSet<string> HelperNames = new(StringComparer.Ordinal) { string.Empty, "Invisible", "Daemon" };
    private static readonly Regex HelperWord = new("invisible|daemon|helper|marker|controller|dummy|proxy", RegexOptions.IgnoreCase);

    private static readonly Regex GrabSkill = new("Grab|Cocoon|Entomb|Encase|Devour|Swallow|Imprison(?!er)|Prison(?!er)");
    private static readonly Regex NotToLead = new(@"Transition|Portal|LootProxy|Waypoint|Checkpoint|Stash|/NPC/|Exit|Entrance|Elevator|Lift|Chest|Glyph|KillCounter|Converter|Pedestal|Crafting|Telepad|Summon(Ally|Alva)", RegexOptions.IgnoreCase);
    private static readonly Regex AddBuff = new(@"AddBuff\(\s*(\w+)");
    private static readonly Regex ShieldWords = new("shield|shelter|sanctu|protect|safe", RegexOptions.IgnoreCase);
    private static readonly Regex BossReference = new(@"\b(this|globals|boss)\.|\w_boss\b");

    private static readonly Regex Empowering = new(@"\b(are empowering|empowered by|protected by)\b|\bwhile\b(?!\s+standing).{0,40}\b(live|lives|alive|remain|remains)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Within = new(@"\b(you are within|while (you are )?(standing )?(in|inside|within)|gaining significant)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Protecting = new(@"regenerat|less damage|immune|protect|cannot be (hit|damaged)", RegexOptions.IgnoreCase);
    private static readonly Regex Stacking = new(@"\bstacks?\b", RegexOptions.IgnoreCase);
    private static readonly Regex Held = new(@"\byou are (being )?(imprisoned|held|trapped|grabbed|encased|entombed|cocooned|restrained|webbed|petrified)\b", RegexOptions.IgnoreCase);

    // Components (sections) of an object the player uses or hits.
    private static readonly string[] UseSections = { "InteractionAction", "Interactable", "Usable" };
    private const string LifeSection = "Life";
    private const string BlockageSection = "TriggerableBlockage";
    private const string InanimateScript = "Metadata/Utility/InanimateObject";

    public static IEnumerable<string> Lines(BossFight fight) =>
        Walls(fight).Concat(Daemons(fight)).Concat(Geals(fight)).Concat(Buffs(fight)).Concat(Covers(fight)).Concat(Objects(fight)).Concat(Skills(fight))
            .Concat(BossStateRules.Lines(fight)).Distinct(StringComparer.Ordinal);

    /// <summary>
    ///     W: a named, skill-less add that extends the inanimate object and blocks the ground, however the boss brings it (Asinia's
    ///     and Lady Elswyth's bone walls are a skill's summons, Kabala's a preload): the walls the existing wall rule saw only preloaded.
    /// </summary>
    private static IEnumerable<string> Walls(BossFight fight) =>
        fight.Adds.Where(add => !add.BossBar && add.SkillIds.Count == 0 && !HelperNames.Contains(add.Name) && IsWall(add.Script)).Select(add => $"wall|{fight.Boss}|{add.Id}");

    private static bool IsWall(BossScriptChain script) => script.Has("blocking", "true") && script.Extends(InanimateScript);

    /// <summary>The buffs the plugin says the text of: those of the empowered, shelter, zone, stack and grabbed lines.</summary>
    internal static IReadOnlyList<string> TextBuffs(IEnumerable<string> lines) =>
        lines.Select(line => line.Split('|')).Where(fields => fields.Length >= 3 && fields[0] is "empowered" or "shelter" or "zone" or "stack" or "grabbed")
            .Select(fields => fields[0] == "stack" ? fields[1] : fields[2]).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The buffs that shelter the player in this fight (an area's, or one whose words say so).</summary>
    internal static IEnumerable<string> ShelterBuffs(BossFight fight) =>
        Geals(fight).Concat(Buffs(fight)).Where(line => line.StartsWith("shelter|", StringComparison.Ordinal)).Select(line => line[(line.LastIndexOf('|') + 1)..]).Distinct(StringComparer.Ordinal);

    /// <summary>Whether an add is an invisible helper: by its name or a helper's word in its path, or a skill's move-daemon.</summary>
    public static bool IsHelper(BossAdd add) => add.Daemon || HelperNames.Contains(add.Name) || HelperWord.IsMatch(add.Id);

    /// <summary>D: a helper carrying an area skill attacks for the boss; its own skills follow.</summary>
    private static IEnumerable<string> Daemons(BossFight fight) =>
        fight.Adds.Where(add => !add.BossBar && IsHelper(add) && add.Skills.Any(skill => skill.Area)).Select(add => $"daemon|{fight.Boss}|{add.Id}|{string.Join(',', add.SkillIds)}");

    /// <summary>GZ, SH and EM: an area putting a buff on whoever stands in it (a shelter when the buff shields), and adds the boss consumes.</summary>
    private static IEnumerable<string> Geals(BossFight fight)
    {
        var buffs = fight.Buffs.ToDictionary(buff => buff.Id, StringComparer.Ordinal);
        foreach (var geal in fight.Geals)
        {
            if (geal.Objects.Count == 0 && geal.Monsters.Count == 0 && AddBuff.Match(geal.Each) is { Success: true } added)
            {
                var id = added.Groups[1].Value;
                var buff = buffs.GetValueOrDefault(id);
                var shelters = ShieldWords.IsMatch(id + " " + (buff?.Text ?? string.Empty)) && buff is not { Harmful: true };
                yield return $"{(shelters ? "shelter" : "zone")}|{fight.Boss}|{id}";
            }

            // On the boss itself it ends a phase (the census's PH, not ported); on monsters that reach back to the boss, the boss consumes them.
            if (!BossReference.IsMatch(geal.Self + " " + geal.Each) || geal.Monsters.Any(path => path.Equals(fight.Boss, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var monster in geal.Monsters)
            {
                yield return $"consume|{fight.Boss}|{monster}";
            }
        }
    }

    /// <summary>EM, SH, ST and GR from the linked buffs' own words.</summary>
    private static IEnumerable<string> Buffs(BossFight fight)
    {
        foreach (var buff in fight.Buffs)
        {
            if (Empowering.IsMatch(buff.Text))
            {
                yield return $"empowered|{fight.Boss}|{buff.Id}";
            }

            if (Within.IsMatch(buff.Text) && Protecting.IsMatch(buff.Text) && !buff.Harmful)
            {
                yield return $"shelter|{fight.Boss}|{buff.Id}";
            }

            if (buff.ShowCount && Stacking.IsMatch(buff.Text))
            {
                yield return $"stack|{buff.Id}|{fight.Boss}";
            }

            if (Held.IsMatch(buff.Text))
            {
                yield return $"grabbed|{fight.Boss}|{buff.Id}";
            }
        }
    }

    /// <summary>CV: an object the boss spawns that is a blockage, blocked from the start, in a fight with an area long enough to hide from.</summary>
    private static IEnumerable<string> Covers(BossFight fight)
    {
        if (!fight.Skills.Any(skill => skill.Area && skill.Length >= CoverSkillLength))
        {
            yield break;
        }

        var objects = fight.Objects.ToDictionary(item => item.Path, item => item.Script, StringComparer.OrdinalIgnoreCase);
        foreach (var path in fight.Skills.SelectMany(skill => skill.Objects))
        {
            if (objects.GetValueOrDefault(path) is { } script && script.HasSection(BlockageSection) && (script.Has("start_blocked", "true") || script.Has("blocking", "true")))
            {
                yield return $"cover|{fight.Boss}|{path}";
            }
        }
    }

    /// <summary>US and OB: objects to use or to hit (not the ways out, loot or NPCs), and skill-less adds with life that are not walls.</summary>
    private static IEnumerable<string> Objects(BossFight fight)
    {
        foreach (var item in fight.Objects.Where(item => item.Script.Found && !NotToLead.IsMatch(item.Path)))
        {
            if (UseSections.Any(item.Script.HasSection))
            {
                yield return $"use|{fight.Boss}|{item.Path}";
            }
            else if (IsHit(item.Script) && !(item.Script.Has("server_only", "true") && !item.Script.HasSection(BossScriptFacts.ClientSection)))
            {
                yield return $"target|{fight.Boss}|{item.Path}";
            }
        }

        foreach (var add in fight.Adds.Where(add => !add.BossBar && !add.Daemon && add.Name.Length > 0 && !HelperWord.IsMatch(add.Name) && !HelperWord.IsMatch(add.Id[(add.Id.LastIndexOf('/') + 1)..])))
        {
            if (add.SkillIds.Count == 0 && !IsWall(add.Script) && add.Script.HasSection(LifeSection))
            {
                yield return $"target|{fight.Boss}|{add.Id}";
            }
        }
    }

    private static bool IsHit(BossScriptChain script) => script.HasSection(LifeSection) || script.Has("transition_on_damage_taken", "1") || script.Has("attackable", "true");

    /// <summary>GR and U: the boss's holds by their skills' ids, and its skills that cannot be dodged, evaded or blocked.</summary>
    private static IEnumerable<string> Skills(BossFight fight) =>
        fight.Skills.Where(skill => GrabSkill.IsMatch(skill.Id)).Select(skill => $"grab|{fight.Boss}|{skill.Id}")
            .Concat(fight.Skills.Where(skill => skill.Unavoidable).Select(skill => $"unavoidable|{fight.Boss}|{skill.Id}"));
}
