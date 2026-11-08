namespace Poe2Access;

/// <summary>The game data catalogs <see cref="GameDataSetup"/> keeps current, each made by one extractor command.</summary>
internal static class GameDataSteps
{
    public const string ItemSounds = "item sounds";
    public const string Sounds = "sounds";

    /// <param name="Version">Raise it when the step's output changes: that step alone reruns, once, on every machine.</param>
    /// <param name="Output">The catalog file it makes, or null for the sounds (the plugin's own folder, never shipped).</param>
    /// <param name="Command">The extractor's batch command making the catalog; the stale ones share one opened index.</param>
    /// <param name="Optional">Its failure keeps the previous file and still counts as done: a patch that moves the
    /// tables these read must not rerun the step on every start (review 2026-10-25).</param>
    /// <param name="Input">The step whose output it reads: it reruns whenever that one does.</param>
    internal sealed record Step(string Name, int Version, string? Output, string? Command, bool Optional = false, string? Input = null);

    /// <summary>The catalog steps in the order they run; each name, output and command once (the boss mechanics step was
    /// listed twice after a merge, ran twice in one batch and the second run failed on the first's output, 2026-11-08).</summary>
    internal static readonly Step[] All =
    [
        new Step(ItemSounds, 1, "item-sounds.json", "item-sounds"),
        new Step("area names", 1, "area-names.json", "areas"),
        new Step("dat columns", 5, "dat-columns.json", "dat-columns"),
        new Step("experience", 1, "experience.json", "experience"),
        new Step("hit times", 3, "hit-times.txt", "hit-times", true),
        new Step("hazards", 3, "hazards.txt", "hazards", true),
        new Step("attack reach", 1, "attack-reach.txt", "attack-reach", true),
        new Step("action types", 1, "action-types.txt", "action-types", true),
        new Step("telegraph skills", 1, "telegraph-skills.txt", "telegraph-skills", true),
        new Step("telegraph effects", 1, "telegraph-effects.txt", "telegraph-effects", true),
        new Step("boss mechanics", 2, "boss-mechanics.txt", "boss-mechanics", true), // 2: reruns once where the doubled step left it "failed"
        new Step("buff names", 1, "buff-names.json", "buff-names", true),
        new Step("class mechanics", 1, "class-mechanics.json", "class-mechanics", true),
        new Step("currency art", 1, "currency-art.json", "currency-art", true),
        new Step("inline icons", 1, "inline-icons.txt", "inline-icons", true),
        new Step("cull skills", 2, "cull-skills.txt", "cull-skills", true),
        // At 2: a stamp from the old layout vouches for every step at 1, these two included, though they never ran.
        new Step("game ids", 4, "game-ids.txt", "game-ids", true),
        new Step("game keys", 3, "game-keys.txt", "game-keys", true),
        new Step("places", 1, "places.json", "places", true),
        new Step("endgame", 3, "endgame.json", "endgame", true),
        new Step("UI catalog", 1, "ui-catalog.json", "ui-catalog"),
        new Step("trial data", 1, "trials.json", "trial-data", true),
        new Step(Sounds, 6, null, null, Input: ItemSounds),
    ];
}
