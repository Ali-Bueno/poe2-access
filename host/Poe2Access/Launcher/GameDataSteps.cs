using System; // also built into the tests, which have no implicit usings
using System.Collections.Generic;
using System.Linq;

namespace Poe2Access;

/// <summary>The game data catalogs <see cref="GameDataSetup"/> keeps current, each made by one extractor command.</summary>
internal static class GameDataSteps
{
    public const string ItemSounds = "item sounds";
    public const string Sounds = "sounds";

    /// <param name="Version">Raise it when the step's output changes: that step alone reruns, once, on every machine.</param>
    /// <param name="Output">The catalog file it makes, or null for the sounds (the plugin's own folder, never shipped).</param>
    /// <param name="Command">The extractor's batch command making the catalog; the stale ones share one opened index.</param>
    /// <param name="Optional">Its failure keeps the previous file and is retried on the next starts, at most
    /// <see cref="GameDataStamp.MaxAttempts"/> per game build, then left alone and listed as failed.</param>
    /// <param name="Input">The step whose output it reads: it reruns whenever that one does.</param>
    internal sealed record Step(string Name, int Version, string? Output, string? Command, bool Optional = false, string? Input = null);

    /// <summary>The catalog steps in the order they run; each name, output and command once (the boss mechanics step was
    /// listed twice after a merge, ran twice in one batch and the second run failed on the first's output, 2026-11-08).</summary>
    internal static readonly Step[] All =
    [
        new Step(ItemSounds, 1, "item-sounds.json", "item-sounds"),
        new Step("area names", 1, "area-names.json", "areas"),
        new Step("area layouts", 2, "area-layouts.json", "area-layouts", true), // never 1: a legacy stamp vouches for every step at 1
        new Step("dat columns", 7, "dat-columns.json", "dat-columns"), // 7: a skill's stats at its gem's level (GrantedEffectStatSetsPerLevel)
        new Step("experience", 1, "experience.json", "experience"),
        new Step("hit times", 7, "hit-times.txt", "hit-times", true), // 7: a geometry trigger sets off hits, whatever its action's name; 6: a clip without a contact is timed by its "impact" event (Doryani's Triumph's self-destruct); 5: skills that cannot be dodged, evaded or blocked are unblockable
        new Step("hazards", 3, "hazards.txt", "hazards", true),
        new Step("attack reach", 1, "attack-reach.txt", "attack-reach", true),
        new Step("action types", 1, "action-types.txt", "action-types", true),
        new Step("telegraph skills", 1, "telegraph-skills.txt", "telegraph-skills", true),
        new Step("telegraph effects", 2, "telegraph-effects.txt", "telegraph-effects", true),
        new Step("boss mechanics", 10, "boss-mechanics.txt", "boss-mechanics", true), // 10: the ways clones walk and the area skills beside them (clonepath|, areas|); 9: the skills a boss skill sets off (child|), adds with an area of 60 cells or more as the boss's pieces; 8: a boss's pieces (part|: Doryani's Triumph's legs); 7: an arena's flood (Mektul's lava); 6: the census's classes and their buffs' words
        new Step("buff names", 2, "buff-names.json", "buff-names", true), // 2: without the names the game never shows ("[DNT…")
        new Step("passive overrides", 1, "passive-overrides.txt", "passive-overrides", true),
        new Step("class mechanics", 1, "class-mechanics.json", "class-mechanics", true),
        new Step("currency art", 1, "currency-art.json", "currency-art", true),
        new Step("inline icons", 1, "inline-icons.txt", "inline-icons", true),
        new Step("cull skills", 2, "cull-skills.txt", "cull-skills", true),
        // At 2: a stamp from the old layout vouches for every step at 1, these two included, though they never ran.
        new Step("game ids", 5, "game-ids.txt", "game-ids", true), // 5: the rows of Rulesets, for the client's rulesets
        new Step("game keys", 3, "game-keys.txt", "game-keys", true),
        new Step("game names", 4, "game-names.txt", "game-names", true), // 4: the Reforging Bench's icon; 3: minimap icons and the Trial map's art // fails, naming them, when a name a rule matches is gone
        new Step("places", 2, "places.json", "places", true), // 2: the props vocabulary, every rotation, each tile's own .tmd (2026-11-16)
        new Step("rooms", 3, "rooms.json", "rooms", true), // 3: generic labels named by file name or props (2026-11-16)
        new Step("endgame", 3, "endgame.json", "endgame", true),
        new Step("UI catalog", 1, "ui-catalog.json", "ui-catalog"),
        new Step("trial data", 3, "trials.json", "trial-data", true), // 2: the class names of four languages were colour strings; 3: the rounds per trial
        new Step("quest goals", 3, "quest-goals.json", "quest-goals", true), // 3: the HUD's "Travel to {0}" and the areas' names
        new Step("shrines", 1, "shrines.json", "shrines", true),
        new Step("spectres", 4, "spectres.json", "spectres", true), // 4: a spear its monster's id names is a spear, not the data's Warstaff; 3: a variety told by its Spirit cost, not the raw multiplier; monsters only; a row of both tags' reach unsaid. Never 1: a legacy stamp vouches for every step at 1; 2: each monster's weapon and reach, and whether its variety is told
        new Step("gems", 1, "gems.json", "gems", true),
        new Step("passive tree", 3, "passive-tree.json", "passive-tree", true), // 3: each jewel socket's PassiveJewelSlots row (where its jewel lies); 2: the rows a node with a choice can be made (an attribute node's three)
        new Step("item bases", 1, "item-bases.json", "item-bases", true),
        new Step("mod texts", 1, "mod-texts.json", "mod-texts", true),
        new Step("mod stats", 3, "mod-stats.json", "mod-stats", true), // 3: what runes and soul cores give socketed; 2: a modifier keying its own buff is told apart
        new Step(Sounds, 21, null, null, Input: ItemSounds), // 21: the Reforging Bench's six hammer blows; 19: the powder barrels; 18: the bench's six anvil strikes; 17: a petrifying statue opening its eyes; 16: the Trial of Chaos lift's own (ElevatorEnd_02); 15: the strongbox's sound louder (0.16), the Essence's a little (0.13), the Abyss chests' own, the boss shelter's loop and its end; 14: the exits' ambient sounds (exits/, from exit-sounds.json); 13: the Delirium, Essence, spirit and strongbox sounds the user picked by ear
    ];

    /// <summary>
    ///     What a start does with each step: reruns it when its required output is missing, it is not current at its version
    ///     on this install, or the step it reads reruns; else, when this install made its output, puts that over the
    ///     plugin's copy (an update has just put the shipped one back). <paramref name="made"/> says whether a catalog file is
    ///     in GameData; <paramref name="soundsMade"/> whether the sounds' manifest is in the plugin.
    /// </summary>
    internal static (List<string> Stale, List<string> CopyBack) Plan(IReadOnlyList<Step> steps, GameDataStamp stamp, string install, Func<string, bool> made, bool soundsMade)
    {
        var stale = new List<string>();
        var copyBack = new List<string>();
        foreach (var step in steps)
        {
            var missing = step.Output is { } output ? !step.Optional && !made(output) : !soundsMade;
            if (missing || !stamp.IsCurrent(install, step.Name, step.Version) || (step.Input is { } input && stale.Contains(input)))
            {
                stale.Add(step.Name);
            }
            else if (step.Output is { } current && stamp.Made(install, step.Name, step.Version))
            {
                copyBack.Add(current);
            }
        }

        return (stale, copyBack);
    }

    /// <summary>
    ///     The steps the plugin is told failed: this start's, the bundle index once for all it failed, then those given up on
    ///     an earlier start and still not working.
    /// </summary>
    internal static List<string> Failed(IEnumerable<(string Step, string Error)> failures, IEnumerable<string> givenUp, string indexFailure)
    {
        var failed = new List<string>();
        foreach (var listed in failures.Select(failure => failure.Error == indexFailure ? failure.Error : failure.Step).Concat(givenUp))
        {
            if (!failed.Contains(listed))
            {
                failed.Add(listed);
            }
        }

        return failed;
    }
}
