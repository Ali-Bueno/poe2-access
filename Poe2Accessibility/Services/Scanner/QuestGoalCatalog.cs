namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    /// <summary>
    ///     Where each quest tracker line leads and how the areas connect, from the game's tables (catalog/quest-goals.json,
    ///     `Poe2UiExtract quest-goals`): a step ends on entering its goal areas. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class QuestGoalCatalog
    {
        private const string FallbackLanguage = "en";

        /// <summary>A keyword link in a text, "[Hit|hits]" or "[Hit]", shown as its last half.</summary>
        private static readonly Regex Link = new(@"\[([^\]|]+)(?:\|([^\]]+))?\]", RegexOptions.Compiled);

        /// <summary>A colour around words, "&lt;white&gt;{words}".</summary>
        private static readonly Regex Colour = new(@"<\w+>\{([^{}]*)\}", RegexOptions.Compiled);

        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        private readonly Dictionary<string, Dictionary<string, string[]>> goals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> connections;

        private readonly Dictionary<string, Regex[]> travel = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string[]>> areas = new(StringComparer.Ordinal);

        /// <summary>Per language, each tracker line that belongs to one quest alone, to all that quest's lines.</summary>
        private readonly Dictionary<string, Dictionary<string, string[]>> questOf = new(StringComparer.Ordinal);

        /// <param name="goals">Per language, a tracker line to the areas its step ends on.</param>
        /// <param name="connections">An area to the areas one exit leads to.</param>
        /// <param name="quests">Per language, each quest's tracker lines.</param>
        /// <param name="travel">Per language, the lines the HUD makes of a step done in another area ("Travel to {0}").</param>
        /// <param name="areas">Per language, the areas by name.</param>
        public QuestGoalCatalog(
            IReadOnlyDictionary<string, Dictionary<string, string[]>> goals, IReadOnlyDictionary<string, string[]> connections,
            IReadOnlyDictionary<string, List<string[]>>? quests = null, IReadOnlyDictionary<string, string[]>? travel = null,
            IReadOnlyDictionary<string, Dictionary<string, string[]>>? areas = null)
        {
            foreach (var (language, lines) in travel ?? new Dictionary<string, string[]>())
            {
                this.travel[language] = lines.Select(TravelPattern).ToArray();
            }

            foreach (var (language, names) in areas ?? new Dictionary<string, Dictionary<string, string[]>>())
            {
                this.areas[language] = new Dictionary<string, string[]>(names, StringComparer.Ordinal);
            }

            foreach (var (language, list) in quests ?? new Dictionary<string, List<string[]>>())
            {
                var normalized = list.Select(lines => lines.Select(Normalize).Where(line => line.Length > 0).Distinct(StringComparer.Ordinal).ToArray()).ToList();
                this.questOf[language] = normalized.SelectMany(lines => lines.Select(line => (Line: line, Lines: lines))).GroupBy(pair => pair.Line, StringComparer.Ordinal)
                    .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.First().Lines, StringComparer.Ordinal);
            }

            foreach (var (language, lines) in goals)
            {
                this.goals[language] = new Dictionary<string, string[]>(lines.GroupBy(pair => Normalize(pair.Key)).ToDictionary(group => group.Key, group => group.First().Value), StringComparer.Ordinal);
            }

            this.connections = new Dictionary<string, string[]>(connections.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal);
        }

        public static QuestGoalCatalog Empty { get; } = new(new Dictionary<string, Dictionary<string, string[]>>(), new Dictionary<string, string[]>());

        /// <summary>A missing or unreadable file gives an empty catalog and one log line.</summary>
        public static QuestGoalCatalog Load(PluginLog log, string file)
        {
            if (!File.Exists(file))
            {
                log.Warn($"No quest goals at {file}: quest steps are guessed from their words");
                return Empty;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<QuestGoalFile>(File.ReadAllText(file)) ?? new QuestGoalFile();
                log.Info($"Quest goals: {parsed.Goals.Count} languages, {parsed.Connections.Count} areas with connections");
                return new QuestGoalCatalog(parsed.Goals, parsed.Connections, parsed.Quests, parsed.Travel, parsed.Areas);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Quest goals not read from {file}: {ex.Message}");
                return Empty;
            }
        }

        /// <summary>A tracker line as the catalog keys it: links and colours gone, blanks collapsed.</summary>
        public static string Normalize(string line) => Spaces.Replace(Link.Replace(Colour.Replace(line, "$1"), match => match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value), " ").Trim();

        /// <summary>Areas the quest step shown by these tracker texts ends on entering, in the given language (English when it has none); empty when unknown.</summary>
        public IReadOnlyCollection<string> GoalsOf(IReadOnlyList<string> trackerTexts, string language)
        {
            this.goals.TryGetValue(language, out var local);
            this.goals.TryGetValue(FallbackLanguage, out var english);
            var reached = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in trackerTexts.SelectMany(text => text.Replace("\r", string.Empty).Split('\n')).Select(Normalize).Where(line => line.Length > 0))
            {
                if ((local != null && local.TryGetValue(line, out var areas)) || (english != null && english.TryGetValue(line, out areas)))
                {
                    reached.UnionWith(areas);
                }
            }

            return reached;
        }

        /// <summary>
        ///     The areas the HUD names for a step done elsewhere ("Viaja a Tierras de cultivo de Ogham"). Apart from
        ///     <see cref="GoalsOf"/>: several areas off a player takes a waypoint, so only an exit straight there is the quest's.
        /// </summary>
        public IReadOnlyCollection<string> TravelGoalsOf(IReadOnlyList<string> trackerTexts, string language) =>
            trackerTexts.SelectMany(text => text.Replace("\r", string.Empty).Split('\n')).Select(Normalize).Where(line => line.Length > 0)
                .SelectMany(line => this.TravelGoals(line, language)).ToHashSet(StringComparer.Ordinal);

        /// <summary>The goals one exit away from an area.</summary>
        public IReadOnlyList<string> NextDoor(string hereId, IReadOnlyCollection<string> goalIds) =>
            !goalIds.Contains(hereId) && this.connections.TryGetValue(hereId, out var next) ? next.Where(goalIds.Contains).Distinct(StringComparer.Ordinal).ToList() : Array.Empty<string>();

        /// <summary>The areas of a line the HUD makes of a step done elsewhere ("Viaja a Tierras de cultivo de Ogham"); empty for any other line.</summary>
        private IEnumerable<string> TravelGoals(string line, string language)
        {
            foreach (var code in new[] { language, FallbackLanguage })
            {
                if (!this.travel.TryGetValue(code, out var patterns) || !this.areas.TryGetValue(code, out var names))
                {
                    continue;
                }

                foreach (var match in patterns.Select(pattern => pattern.Match(line)).Where(match => match.Success))
                {
                    var found = match.Groups.Values.Skip(1).Select(group => names.GetValueOrDefault(group.Value.Trim())).ToList();
                    if (found.All(ids => ids != null))
                    {
                        return found.SelectMany(ids => ids!);
                    }
                }
            }

            return Enumerable.Empty<string>();
        }

        private static Regex TravelPattern(string line) =>
            new("^" + Regex.Replace(Regex.Escape(Normalize(line)), @"\\\{\d\}", "(.+?)") + "$", RegexOptions.Compiled);

        /// <summary>
        ///     The areas one exit from <paramref name="hereId"/> on a shortest way to a goal (the goal itself when it is next
        ///     door); empty when here is a goal or nothing connects.
        /// </summary>
        /// <summary>
        ///     Every tracker line of the quests the tracker's lines belong to, in the client's language (English behind it): the
        ///     steps the tracker has moved past are among them ("Coloca el núcleo de alma pequeño en el Altar de piedra").
        /// </summary>
        public IReadOnlyCollection<string> QuestLinesOf(IReadOnlyList<string> trackerTexts, string language)
        {
            this.questOf.TryGetValue(language, out var local);
            this.questOf.TryGetValue(FallbackLanguage, out var english);
            var lines = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in trackerTexts.SelectMany(text => text.Replace("\r", string.Empty).Split('\n')).Select(Normalize).Where(line => line.Length > 0))
            {
                if ((local != null && local.TryGetValue(line, out var all)) || (english != null && english.TryGetValue(line, out all)))
                {
                    lines.UnionWith(all);
                }
            }

            return lines;
        }

        public IReadOnlyCollection<string> StepsToward(string hereId, IReadOnlyCollection<string> goalIds)
        {
            if (goalIds.Contains(hereId) || !this.connections.TryGetValue(hereId, out var next))
            {
                return Array.Empty<string>();
            }

            var ways = next.Distinct(StringComparer.Ordinal).Where(area => area != hereId).Select(area => (Area: area, Distance: this.Distance(area, goalIds, hereId))).Where(way => way.Distance >= 0).ToList();
            return ways.Count == 0 ? Array.Empty<string>() : ways.Where(way => way.Distance == ways.Min(other => other.Distance)).Select(way => way.Area).ToList();
        }

        /// <summary>Exits to a goal from <paramref name="from"/>, not passing <paramref name="avoid"/>; -1 when none leads there.</summary>
        private int Distance(string from, IReadOnlyCollection<string> goalIds, string avoid)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { avoid, from };
            var layer = new List<string> { from };
            for (var distance = 0; layer.Count > 0; distance++)
            {
                if (layer.Any(goalIds.Contains))
                {
                    return distance;
                }

                layer = layer.SelectMany(area => this.connections.TryGetValue(area, out var near) ? near : Array.Empty<string>()).Where(seen.Add).ToList();
            }

            return -1;
        }

        private sealed class QuestGoalFile
        {
            [JsonProperty("goals")]
            public Dictionary<string, Dictionary<string, string[]>> Goals { get; set; } = new();

            [JsonProperty("connections")]
            public Dictionary<string, string[]> Connections { get; set; } = new();

            [JsonProperty("quests")]
            public Dictionary<string, List<string[]>> Quests { get; set; } = new();

            [JsonProperty("travel")]
            public Dictionary<string, string[]> Travel { get; set; } = new();

            [JsonProperty("areas")]
            public Dictionary<string, Dictionary<string, string[]>> Areas { get; set; } = new();
        }
    }
}
