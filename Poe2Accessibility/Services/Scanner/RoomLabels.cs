namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The labels the game's room files (.arm) carry, as keys: the words of the label without its variant and role words,
    ///     and the keys the plugin speaks (Announcements.RoomName). Also built into the extractor's `rooms`
    ///     command, so the catalog keeps only rooms that are spoken (docs/research/rooms.md).
    /// </summary>
    public static class RoomLabels
    {
        /// <summary>Words that tell a room's role in the layout, not what the room is.</summary>
        private static readonly HashSet<string> RoleWords = new(StringComparer.Ordinal)
        {
            "entrance", "exit", "end", "mid", "st", "cnr", "cc", "cv", "sink", "source", "join", "overlay", "mission", "rare",
            "checkpoint", "special", "injector",
        };

        /// <summary>The keys spoken; stairs are left out, the tiles already name them.</summary>
        public static readonly HashSet<string> Spoken = new(StringComparer.Ordinal)
        {
            "chapel", "study", "wine cellar", "throne room", "tortureroom", "torture room", "top level hole", "level hole", "boss entry",
            "boss", "map boss", "town", "bridge", "bridge straight", "bridge long", "footbridge", "arena", "merchant", "tunnels",
            "outer tunnels", "clearing", "crossing", "intersection", "boat", "lake", "river", "stream", "dam", "shrine", "courtyard",
            "foyer", "hamlet", "campsite", "encampment", "pit", "vault", "treasure", "forge", "library", "beach", "graves",
            "churchruin", "secret chamber", "chamber", "prison gate", "hallway", "passage", "square", "tomb", "mausoleum",
            "hunting grounds", "alley", "graveyard", "lair", "airlock", "armoury", "garrison", "smithy", "commander", "thaumaturge",
            "corruption", "golemworks", "generator", "alchemylab", "fleshsurgeon", "synthfleshlab", "viperspymaster",
            "viperspymaster defeated", "sacrificechamber", "transcendentbarracks", "viperlegionbarracks",
        };

        private static readonly Regex Words = new(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", RegexOptions.Compiled);

        /// <summary>Variant and level words: numbers, "v2", "lvl3", single letters ("libraryH", "Oasis_E").</summary>
        private static readonly Regex VariantWord = new(@"^(?:\d+|v\d+|lvl\d*|[a-z])$", RegexOptions.Compiled);

        /// <summary>"WineCellar_2" → "wine cellar", "boss_entry" → "boss entry", "armoury_lvl2" → "armoury"; empty when nothing is left.</summary>
        public static string Key(string label) =>
            string.Join(" ", Words.Matches(label).Select(word => word.Value.ToLowerInvariant()).Where(word => !VariantWord.IsMatch(word) && !RoleWords.Contains(word)));
    }
}
