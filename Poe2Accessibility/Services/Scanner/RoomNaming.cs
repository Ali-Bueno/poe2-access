namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The spoken key of a room file (.arm): its label when that is spoken, else what its file name says, else the most telling
    ///     prop it holds, else none (docs/research/rooms.md, "Generic labels"). Built into the extractor's `rooms` command.
    /// </summary>
    public static class RoomNaming
    {
        /// <summary>Prefix of the keys named by a prop ("with statue").</summary>
        public const string WithPrefix = "with ";

        public const string MiniBoss = "miniboss";

        /// <summary>Words of a layout piece (a corner, a fill, a border…): the file is a piece of the map's fabric, not a place to name.</summary>
        private static readonly Regex Piece = new(
            @"^(?:cnr(?:large)?|corner|(?:cc|cv|cr|st)(?:[cmsvwrt]{2}|large|end)?|str|tri|ledges?\w*|straight|sides?|edges?|mid|end|diag\w*|\w*fill\w*|overlays?\w*|tileoverlay|quad|cross|border\w*|open|walls?|thinwalls?|thickwalls?|insidewall|unwalkable\w*|injectors?|buffer|wildcard|void|flip\w*|curve|bend|jut|concave|convex|cliff\w*|ridge|connect\w*|transition|combo|nocrop|random\w*)$",
            RegexOptions.Compiled);

        /// <summary>A room a player would name, by the whole words of its file name and label; the first entry matching wins (new keys, and existing ones: shrine, tomb, graves, treasure).</summary>
        private static readonly (string Key, Regex Words)[] Table =
        {
            ("tree of souls", Of(@"tree of souls|soultree")),
            ("shrine", Of("shrines?")),
            ("gallows", Of("gallows|gibbets?")),
            ("witch hut", Of("witch hut|witchhut")),
            ("huts", Of("huts?|longhut|highhut|dwellings?")),
            ("crypt", Of("crypts?")),
            ("tomb", Of("tombs?")),
            ("gravestones", Of("gravestones?")),
            ("graves", Of("graves|graverows|fencedgraves|smallgraves")),
            ("tower", Of("towers?")),
            ("altar", Of("altars?")),
            ("idols", Of("idols?")),
            ("statue", Of("statues?|bigstatue")),
            ("well", Of("wells?")),
            ("fountain", Of("fountains?")),
            ("caravan", Of("caravan")),
            ("cart", Of("carts?|largecarts?|smallcarts?|wagons?")),
            ("shipwreck", Of("shipwrecks?")),
            ("ship", Of("ship|kalguurship")),
            ("pier", Of("piers?")),
            ("workshop", Of("workshop")),
            ("offices", Of("offices?")),
            ("storage", Of("storage")),
            ("basement", Of("basement")),
            ("greenhouse", Of("greenhouse")),
            ("bedroom", Of("bedroom")),
            ("hideout", Of("hideouts?")),
            ("pools", Of("pools")),
            ("village", Of("village")),
            ("tent", Of("tents?")),
            ("smithy", Of("blacksmith|smithy")),
            ("logpile", Of("logpiles?")),
            (WithPrefix + "campfire", Of("bonfires?|campfires?")),
            ("treasure", Of("treasure")),
        };

        /// <summary>Spoken label keys that are also a single word a file name may say alone ("arena_riverside", "courtyard_02"); terrain words (river, stream, lake) are left out, those files are map pieces.</summary>
        private static readonly HashSet<string> NameWords = new(StringComparer.Ordinal)
        {
            "bridge", "arena", "courtyard", "passage", "town", "ritual", "clearing", "library", "forge", "smithy", "garrison", "armoury", "lair",
            "vault", "chamber", "hallway", "graveyard", "mausoleum", "square", "alley", "foyer", "hamlet", "dam", "oasis", "sinkhole", "merchant",
        };

        /// <summary>Props that name a place by themselves, in the order a tie is settled (the extractor's prop kinds).</summary>
        public static readonly string[] TellingProps =
        {
            "statue", "altar", "fountain", "well", "campfire", "gallows", "cart", "tent", "stall", "anvil", "totem", "tower", "tomb",
            "throne", "cannon", "bell", "waterwheel", "noticeboard", "forge",
        };

        /// <summary>Words of a file that only say "some feature of the level" ("landmark_01", "encounter_3"): what the room holds names it.</summary>
        private static readonly HashSet<string> GenericWords = new(StringComparer.Ordinal)
        {
            "landmark", "feature", "features", "encounter", "encounters", "lore", "unique", "quest", "special",
        };

        /// <summary>A boss waits in a room that holds one of these objects.</summary>
        private static readonly Regex BossObject = new("CheckpointBoss|BossRoomMinimapIcon|BossController", RegexOptions.Compiled);

        private static readonly Regex ShrineObject = new("Shrine", RegexOptions.Compiled);

        /// <summary>Folders of rooms that are no place in a level: the character selection's scenery.</summary>
        private static readonly Regex NotInALevel = new("^metadata/terrain/(?:characterselection|worldmaps)/", RegexOptions.Compiled);

        /// <summary>Every key <see cref="Key"/> can return besides a spoken label's, for the phrases that must exist.</summary>
        public static IEnumerable<string> Keys => Table.Select(entry => entry.Key).Append(MiniBoss).Concat(TellingProps.Select(kind => WithPrefix + kind)).Distinct();

        /// <summary>
        ///     A room's spoken key, or null when it stays silent. <paramref name="path"/> is the file's path, <paramref name="label"/> the
        ///     label the designers gave it, <paramref name="propKinds"/> the kinds of its doodads and <paramref name="objectClasses"/> the classes of all its objects.
        /// </summary>
        public static string? Key(string path, string label, IReadOnlyCollection<string> propKinds, IReadOnlyCollection<string> objectClasses)
        {
            var key = RoomLabels.Key(label);
            if (RoomLabels.Spoken.Contains(key))
            {
                return key;
            }

            var lower = path.ToLowerInvariant();
            if (NotInALevel.IsMatch(lower))
            {
                return null;
            }

            var stem = RoomLabels.Words(lower[(lower.LastIndexOf('/') + 1)..].Replace(".arm", string.Empty, StringComparison.Ordinal)).ToList();
            if (stem.Any(Piece.IsMatch))
            {
                return null;
            }

            var words = string.Join(" ", stem.Concat(RoomLabels.Words(label)));
            if (Table.FirstOrDefault(entry => entry.Words.IsMatch(words)) is { Key: not null } named)
            {
                return named.Key;
            }

            if (stem.FirstOrDefault(NameWords.Contains) is { } word)
            {
                return word;
            }

            if (words.Contains("miniboss", StringComparison.Ordinal) || words.Contains("mini boss", StringComparison.Ordinal))
            {
                return objectClasses.Any(BossObject.IsMatch) ? MiniBoss : null;
            }

            if (!stem.All(word => GenericWords.Contains(word) || (word.Length == 1 && word != "x") || char.IsDigit(word[0])))
            {
                return null;
            }

            if (objectClasses.Any(ShrineObject.IsMatch))
            {
                return "shrine";
            }

            var telling = propKinds.Where(TellingProps.Contains).GroupBy(kind => kind).ToList();
            return telling.Count == 0
                ? null
                : WithPrefix + telling.OrderByDescending(group => group.Count()).ThenBy(group => Array.IndexOf(TellingProps, group.Key)).First().Key;
        }

        private static Regex Of(string words) => new($@"\b(?:{words})\b", RegexOptions.Compiled);
    }
}
