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
        new Step("dat columns", 7, "dat-columns.json", "dat-columns"), // 7: a skill's stats at its gem's level (GrantedEffectStatSetsPerLevel)
        new Step("experience", 1, "experience.json", "experience"),
        new Step("hit times", 5, "hit-times.txt", "hit-times", true), // 5: skills that cannot be dodged, evaded or blocked are unblockable
        new Step("hazards", 3, "hazards.txt", "hazards", true),
        new Step("attack reach", 1, "attack-reach.txt", "attack-reach", true),
        new Step("action types", 1, "action-types.txt", "action-types", true),
        new Step("telegraph skills", 1, "telegraph-skills.txt", "telegraph-skills", true),
        new Step("telegraph effects", 1, "telegraph-effects.txt", "telegraph-effects", true),
        new Step("boss mechanics", 6, "boss-mechanics.txt", "boss-mechanics", true), // 6: the census's classes (daemons, arena, shelters, objects) and their buffs' words
        new Step("buff names", 2, "buff-names.json", "buff-names", true), // 2: without the names the game never shows ("[DNT…")
        new Step("passive overrides", 1, "passive-overrides.txt", "passive-overrides", true),
        new Step("class mechanics", 1, "class-mechanics.json", "class-mechanics", true),
        new Step("currency art", 1, "currency-art.json", "currency-art", true),
        new Step("inline icons", 1, "inline-icons.txt", "inline-icons", true),
        new Step("cull skills", 2, "cull-skills.txt", "cull-skills", true),
        // At 2: a stamp from the old layout vouches for every step at 1, these two included, though they never ran.
        new Step("game ids", 4, "game-ids.txt", "game-ids", true),
        new Step("game keys", 3, "game-keys.txt", "game-keys", true),
        new Step("game names", 3, "game-names.txt", "game-names", true), // 3: minimap icons and the Trial map's art // fails, naming them, when a name a rule matches is gone
        new Step("places", 1, "places.json", "places", true),
        new Step("endgame", 3, "endgame.json", "endgame", true),
        new Step("UI catalog", 1, "ui-catalog.json", "ui-catalog"),
        new Step("trial data", 2, "trials.json", "trial-data", true), // 2: the class names of four languages were colour strings
        new Step("gems", 1, "gems.json", "gems", true),
        new Step("passive tree", 1, "passive-tree.json", "passive-tree", true),
        new Step("item bases", 1, "item-bases.json", "item-bases", true),
        new Step("mod texts", 1, "mod-texts.json", "mod-texts", true),
        new Step("mod stats", 2, "mod-stats.json", "mod-stats", true), // 2: a modifier keying its own buff is told apart
        new Step(Sounds, 10, null, null, Input: ItemSounds), // 10: the opened way's sound as the user picked it
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
