namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The entity listing's pure parts say what they said before they were made cheap (2026-11-10).</summary>
    public static class ListingTests
    {
        private static readonly string[] Names =
        {
            "Renly", "Renly, con misión", "Una", "El molinero hinchado, único", "Beira de la Brea Pútrida, raro", "Lobo, mágico", "Lobo",
            "Campamento de Sierraclara", "Sierraclara", "El bosque singular", "Mansión de Ogham", "Pueblo de Ogham", "Altar de ritual",
            "Ogham Farmlands Or Deadend", "Sala del jefe: Asinia", "Mecanismo antiguo", "Cofre", "Zona inexplorada", "Clearfell",
            "Clearfell Encampment", "The Bloated Miller, unique", "Árbol, raro", "Río", ", raro", "Entrada",
        };

        private static readonly string[] Subjects = { string.Empty, "Asinia", "Beira", "El Rey de la Niebla", "Rey" };

        private static readonly string[] Kinds =
        {
            string.Empty, QuestMatcher.ExitKind, "npc_quest", QuestMatcher.TileNote, MinimapMarks.BossRoomKind, MinimapMarks.SignKind, ExplorationMap.PocketKind, "waypoint",
        };

        private static readonly string[] Texts =
        {
            "Busca a Asinia en la Tumba de la consorte", "Habla con Renly", "Encuentra Sierraclara", "Sigue a través del Bosque singular",
            "El lobo rabioso de Ogham", "Infíltrate en la Mansión más allá del pueblo de Ogham", "Busca los altares de ritual y purifícalos",
            "Search for the Miller and find refuge in Clearfell", "Find Ogham Farmlands Or Deadend", "Mata a Beira de la Brea Pútrida",
            "Suelo traicionero", string.Empty, "Registra Sierraclara para encontrar la entrada al Lodazal", "Talk to Renly",
        };

        private static readonly string[] Areas = { string.Empty, "El bosque singular", "Sierraclara", "Clearfell", "Campamento de Sierraclara" };

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheObjectiveNamesWhatItAlwaysDid), TheObjectiveNamesWhatItAlwaysDid),
            (nameof(AnObjectiveKeptBetweenListingsAnswersTheSame), AnObjectiveKeptBetweenListingsAnswersTheSame),
            (nameof(ANamedTileStandsWhereItAlwaysDid), ANamedTileStandsWhereItAlwaysDid),
        };

        private static void TheObjectiveNamesWhatItAlwaysDid()
        {
            var random = new Random(7);
            var matched = 0;
            for (var run = 0; run < 600; run++)
            {
                var (entries, texts, area, from) = Scene(random);
                var expected = OldTargets(entries, texts, area, from);
                matched += expected.Count > 0 ? 1 : 0;
                Expect(Said(expected), Said(QuestMatcher.Targets(entries, texts, area, from)), $"scene {run}");
            }

            Expect(true, matched > 100, $"scenes with a target: {matched}");
        }

        private static void AnObjectiveKeptBetweenListingsAnswersTheSame()
        {
            var random = new Random(11);
            var memo = new QuestMatcher.Memo();
            for (var run = 0; run < 400; run++)
            {
                var (entries, texts, area, from) = Scene(random);
                var expected = Said(OldTargets(entries, texts, area, from));

                // The same objective several listings in a row, the entities changing under it; then another objective.
                Expect(expected, Said(QuestMatcher.Targets(entries, texts, area, from, memo)), $"scene {run}, first listing");
                Expect(expected, Said(QuestMatcher.Targets(entries, texts.ToList(), area, from, memo)), $"scene {run}, second listing");
                var (others, _, _, _) = Scene(random);
                Expect(Said(OldTargets(others, texts, area, from)), Said(QuestMatcher.Targets(others, texts, area, from, memo)), $"scene {run}, other entities");
            }
        }

        private static void ANamedTileStandsWhereItAlwaysDid()
        {
            var random = new Random(5);
            var files = new[] { "Art/Tiles/arena", "Art/Tiles/arena_floor", "Art/Tiles/stairs", "Art/Tiles/gate" };
            for (var run = 0; run < 200; run++)
            {
                var tiles = new Dictionary<string, List<Vector2>>();
                for (var tile = random.Next(0, 40); tile > 0; tile--)
                {
                    var key = $"{files[random.Next(files.Length)]}.tdtx:{random.Next(4)}-y:{random.Next(4)}";
                    var spots = tiles.TryGetValue(key, out var kept) ? kept : tiles[key] = new List<Vector2>();
                    spots.Add(new Vector2(random.Next(0, 30) * 23, random.Next(0, 30) * 23));
                }

                var reach = 2 * 23f;
                foreach (var key in files.Concat(tiles.Keys.Take(3)).Append("Art/Tiles/none").Append("Art/Tiles/none.tdtx:0-y:0"))
                {
                    var spots = key.Contains(':', StringComparison.Ordinal)
                        ? tiles.TryGetValue(key, out var exact) ? exact : new List<Vector2>()
                        : tiles.Where(tile => tile.Key.StartsWith(key + ".", StringComparison.Ordinal)).SelectMany(tile => tile.Value).ToList();
                    var expected = spots.Count == 0
                        ? Array.Empty<Vector2>()
                        : MinimapMarks.Groups(spots, reach).Select(members => new Vector2(members.Average(member => spots[member].X), members.Average(member => spots[member].Y))).ToArray();
                    Expect(true, spots.SequenceEqual(TilePlaces.Spots(tiles, key)), $"run {run}, the pieces of {key}");
                    Expect(string.Join(" ", expected), string.Join(" ", TilePlaces.Centres(tiles, key, reach)), $"run {run}, the places of {key}");
                }
            }
        }

        private static (List<ScanEntry> Entries, List<string> Texts, string Area, string From) Scene(Random random)
        {
            var categories = Enum.GetValues<ScanCategory>();
            var entries = new List<ScanEntry>();
            for (var count = random.Next(0, 25); count > 0; count--)
            {
                entries.Add(new ScanEntry(
                    (uint)random.Next(1, 18),
                    categories[random.Next(categories.Length)],
                    Names[random.Next(Names.Length)],
                    random.Next(100),
                    random.Next(100),
                    0,
                    Kinds[random.Next(Kinds.Length)],
                    QuestMarked: random.Next(12) == 0,
                    Subject: Subjects[random.Next(Subjects.Length)]));
            }

            // An enemy without its rarity is skipped before its name is cut: the matcher is never asked for the part before a comma it lacks.
            var texts = Enumerable.Range(0, random.Next(0, 4)).Select(_ => Texts[random.Next(Texts.Length)]).ToList();
            return (entries, texts, Areas[random.Next(Areas.Length)], Areas[random.Next(Areas.Length)]);
        }

        private static string Said(IEnumerable<ScanEntry> targets) => string.Join(" | ", targets);

        /// <summary>QuestMatcher.Targets as it was before its answers were kept (commit 6f2f3e3), word for word.</summary>
        private static List<ScanEntry> OldTargets(IReadOnlyList<ScanEntry> entries, IReadOnlyList<string> questTexts, string currentArea, string cameFrom)
        {
            var texts = questTexts.Select(Fold).Where(text => text.Length > 0).ToList();
            var targets = entries.Where(entry => entry.QuestMarked).Select(entry => entry with { Category = ScanCategory.Quest }).ToList();
            if (texts.Count == 0)
            {
                return targets;
            }

            var properNouns = new HashSet<string>(questTexts.SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
                .Where(word => word.Length >= 4 && char.IsUpper(word[0]))
                .Select(word => Fold(word.Trim(',', '.', ':', ';', '!', '?', '\'', '"'))));
            properNouns.ExceptWith(Fold(currentArea).Split(' ', StringSplitOptions.RemoveEmptyEntries));

            foreach (var entry in entries)
            {
                if (entry.Category is ScanCategory.Players or ScanCategory.Items or ScanCategory.Unexplored or ScanCategory.Places ||
                    entry.Kind == MinimapMarks.SignKind ||
                    (entry.Category == ScanCategory.Enemies && !entry.Name.Contains(", ", StringComparison.Ordinal)))
                {
                    continue;
                }

                var name = Fold(entry.Category == ScanCategory.Enemies ? entry.Name[..entry.Name.IndexOf(", ", StringComparison.Ordinal)]
                    : entry.Kind == "npc_quest" && entry.Name.LastIndexOf(", ", StringComparison.Ordinal) is > 0 and var marker ? entry.Name[..marker]
                    : entry.Kind == MinimapMarks.BossRoomKind ? entry.Subject
                    : entry.Name);
                if (name.Length < 4)
                {
                    continue;
                }

                var named = texts.Any(text => text != name && ContainsWord(text, name)) ||
                            (entry.Kind is not (QuestMatcher.TileNote or MinimapMarks.BossRoomKind) &&
                             name.Split(' ').Any(word => word.Length >= 4 && properNouns.Contains(word.Trim(',', '.', ':', ';'))));
                if (named && targets.All(target => target.Id != entry.Id))
                {
                    targets.Add(entry with { Category = ScanCategory.Quest });
                }
            }

            var here = Fold(currentArea).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => word.Length >= 4).ToList();
            if (targets.Count == 0 && properNouns.Count == 0 && here.Count > 0 && texts.Any(text => here.All(word => Words(text).Contains(word))))
            {
                var back = Fold(cameFrom);
                var onward = entries.Where(entry => entry.Category == ScanCategory.Exits && entry.Kind == QuestMatcher.ExitKind && Fold(entry.Name) != back).ToList();
                targets.AddRange((onward.Count > 0 ? onward : entries.Where(entry => entry.Category == ScanCategory.Unexplored).OrderBy(entry => entry.Kind == ExplorationMap.PocketKind).Take(1))
                    .Select(entry => entry with { Category = ScanCategory.Quest }));
            }

            return targets;
        }

        private static bool ContainsWord(string text, string name)
        {
            var words = Words(text);
            var wanted = Words(name);
            for (var at = 0; wanted.Length > 0 && at + wanted.Length <= words.Length; at++)
            {
                if (wanted.Select((word, i) => SameOrPlural(words[at + i], word)).All(same => same))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] Words(string text) =>
            text.Split(text.Where(c => !char.IsLetterOrDigit(c)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries);

        private static bool SameOrPlural(string word, string singular) =>
            word == singular || word == singular + "s" || word == singular + "es";

        private static string Fold(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            return new string(decomposed.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }
    }
}
