using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `quest-goals` command: where each quest tracker line leads, { "goals": { language: { line: [area ids] } },
///     "connections": { area id: [area ids] } }. A quest's states are ordered by Order and a step ends when the state of the
///     next lower Order of the same quest becomes true, so a state's goals are the areas whose FlagOnEntered is one of that
///     state's FlagsPresent. Every column is found by what it holds and judged on the rows named below (0.5.5f); the step
///     fails, and writes nothing, when a known line does not lead where the game's data says. Read-only on the game.
/// </summary>
internal static class QuestGoalExtractor
{
    private const string Step = "quest-goals";
    private const string StatesTable = "queststates";
    private const string FlagsTable = "questflags";
    private const string QuestsTable = "quest";
    private const string AreasTable = "worldareas";

    // Measured offsets (0.5.5f, dat-kinds), where each column is looked for first.
    private const int QuestOffset = 0x0;
    private const int OrderOffset = 0x10;
    private const int FlagsPresentOffset = 0x14;
    private const int TrackerTextOffset = 0x59;
    private const int FlagOnEnteredOffset = 0x3C;
    private const int ConnectionsOffset = 0x16;

    /// <summary>
    ///     A flag set by more areas than this is not a place (EnteredHideout is set by every hideout, 80 areas): it names no
    ///     goal. A goal flag sets one area, a few at most (6 on 0.5.5f).
    /// </summary>
    private const int MaxAreasPerFlag = 8;

    /// <summary>Two states of BurnTheWaterGoddess (0.5.5f): their tracker text, Order, and the area each one ends on.</summary>
    private static readonly (string Text, int Order, string Goal)[] KnownStates =
    {
        ("Find the Sanctum at the heart of Keth", 9, "G2_4_3"),
        ("Find the entrance to Keth buried somewhere in the sands", 10, "G2_4_2"),
    };

    /// <summary>An area that sets a flag no other area does, and that flag's Id.</summary>
    private const string KnownFlagArea = "G2_4_3";
    private const string KnownFlag = "Visited-G2_4_3";

    /// <summary>An area and two of its neighbours.</summary>
    private const string KnownLinkedArea = "G2_4_2";
    private static readonly string[] KnownNeighbours = { "G2_4_1", "G2_4_3" };

    private static readonly Regex Colour = new(@"<\w+>\{([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>One tracker line as the plugin matches it: links and colours gone, blanks collapsed (QuestGoalCatalog.Normalize does the same).</summary>
    internal static string Normalize(string line) => Spaces.Replace(GameMarkup.Plain(Colour.Replace(line, "$1")), " ").Trim();

    /// <summary>The columns found in the English tables.</summary>
    private sealed record Columns(int Quest, int Order, int Present, int Text, int FlagOnEntered, int Connections);

    public static int Run(Index index, string outFile)
    {
        var tables = (States: DatTable.Open(index, StatesTable), Flags: DatTable.Open(index, FlagsTable), Quests: DatTable.Open(index, QuestsTable), Areas: DatTable.Open(index, AreasTable));
        if (tables.States == null || tables.Flags == null || tables.Quests == null || tables.Areas == null)
        {
            Say.Out.WriteLine($"QuestStates, QuestFlags, Quest or WorldAreas not found: {Step} left out.");
            return 1;
        }

        var (states, flags, quests, areas) = (tables.States, tables.Flags, tables.Quests, tables.Areas);
        var search = new DatColumnSearch();
        var columns = Find(states, flags, quests, areas, search);
        if (!search.Report(Step) || columns == null)
        {
            return 1;
        }

        var connections = ReadConnections(areas, columns.Connections);
        var flagAreas = ReadFlagAreas(areas, flags, columns.FlagOnEntered);
        var goals = GoalsByState(states, columns, flagAreas);
        if (!KnownGoalsHold(states, columns, goals))
        {
            return 1;
        }

        var result = new SortedDictionary<string, SortedDictionary<string, string[]>>(StringComparer.Ordinal);
        foreach (var (code, folder) in AreaExtractor.Languages)
        {
            var table = folder == null ? states : DatTable.OpenTranslated(index, StatesTable, folder);
            if (table == null)
            {
                Say.Out.WriteLine($"  {code}: no QuestStates");
                continue;
            }

            if (!ClientStringColumns.SameShape(states, table))
            {
                Say.Out.WriteLine($"  {code}: QuestStates differs from the English table in rows or row size: its columns cannot be told, {Step} left out");
                return 1;
            }

            var (lines, dropped) = LinesOf(table, columns.Text, goals);
            Say.Out.WriteLine($"  {code}: {lines.Count} lines, {dropped} ambiguous dropped");
            result[code] = lines;
        }

        if (!result.TryGetValue("en", out var english) || !KnownLinesHold(english))
        {
            return 1;
        }

        Say.Out.WriteLine($"  {connections.Count} areas with connections; {goals.Count} states with goals");
        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["connections"] = connections, ["goals"] = result });
        Say.Out.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }

    private static Columns? Find(DatTable states, DatTable flags, DatTable quests, DatTable areas, DatColumnSearch search)
    {
        var textColumn = search.Find(states, "QuestStates.MapPinsText", TrackerTextOffset, offset =>
            DatColumnSearch.IsText(states, offset, _ => true) && KnownStates.All(state => DatAnchors.RowWith(states, offset, state.Text) >= 0));
        var rows = textColumn < 0 ? KnownStates.Select(_ => -1).ToArray() : KnownStates.Select(state => DatAnchors.RowWith(states, textColumn, state.Text)).ToArray();
        if (rows.Any(row => row < 0))
        {
            return null;
        }

        var quest = search.Key(states, "QuestStates.Quest", QuestOffset, quests, known: (rows[0], key => key == states.Key(rows[1], QuestOffset)));
        var order = search.Find(states, "QuestStates.Order", OrderOffset, offset => KnownStates.Select((state, at) => states.Int32(rows[at], offset) == state.Order).All(held => held));
        var present = search.KeyList(states, "QuestStates.FlagsPresent", FlagsPresentOffset, flags);
        var flagId = DatAnchors.RowWith(flags, DatColumnSearch.IdColumn, KnownFlag);
        var areaRow = DatColumnSearch.RowOf(areas, KnownFlagArea);
        var entered = search.KeyList(areas, "WorldAreas.FlagOnEntered", FlagOnEnteredOffset, flags, known: (areaRow, keys => flagId >= 0 && keys.Contains(flagId)));
        var linked = DatColumnSearch.RowOf(areas, KnownLinkedArea);
        var neighbours = KnownNeighbours.Select(id => (long)DatColumnSearch.RowOf(areas, id)).ToList();
        var connections = search.Checking("WorldAreas.Connections", linked).Find(areas, "WorldAreas.Connections", ConnectionsOffset, offset =>
            IsRowList(areas, offset) && (linked < 0 || areas.RowList(linked, offset).Intersect(neighbours).Count() == neighbours.Count));
        return search.AllFound ? new Columns(quest, order, present, textColumn, entered, connections) : null;
    }

    /// <summary>A list of rows of the same table: every item a row of it, some row with items.</summary>
    private static bool IsRowList(DatTable table, int offset)
    {
        using var probe = DatTable.Probe();
        var filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var items = table.RowList(row, offset);
            if (items.Any(item => item < 0 || item >= table.RowCount))
            {
                return false;
            }

            filled += items.Count > 0 ? 1 : 0;
        }

        return filled > 0;
    }

    private static SortedDictionary<string, string[]> ReadConnections(DatTable areas, int column)
    {
        var result = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        for (var row = 0; row < areas.RowCount; row++)
        {
            var near = areas.RowList(row, column).Select(key => DatAnchors.IdOf(areas, key)).OfType<string>().Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
            if (areas.StringOrNull(row, DatColumnSearch.IdColumn) is { Length: > 0 } id && near.Length > 0)
            {
                result[id] = near;
            }
        }

        return result;
    }

    /// <summary>The areas each flag (a QuestFlags row) is set by entering, for the flags a few areas set.</summary>
    private static Dictionary<long, List<string>> ReadFlagAreas(DatTable areas, DatTable flags, int column)
    {
        var setBy = new Dictionary<long, List<string>>();
        for (var row = 0; row < areas.RowCount; row++)
        {
            if (areas.StringOrNull(row, DatColumnSearch.IdColumn) is not { Length: > 0 } id)
            {
                continue;
            }

            foreach (var flag in areas.KeyList(row, column).Where(flag => flag >= 0 && flag < flags.RowCount))
            {
                if (!setBy.TryGetValue(flag, out var list))
                {
                    setBy[flag] = list = new List<string>();
                }

                list.Add(id);
            }
        }

        return setBy.Where(pair => pair.Value.Count <= MaxAreasPerFlag).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    /// <summary>For each state with goals, the areas its step ends on: those the next lower Order of its quest sets by entering.</summary>
    private static Dictionary<int, string[]> GoalsByState(DatTable states, Columns c, Dictionary<long, List<string>> flagAreas)
    {
        var byQuest = Enumerable.Range(0, states.RowCount).GroupBy(row => states.Key(row, c.Quest)).Where(group => group.Key >= 0);
        var goals = new Dictionary<int, string[]>();
        foreach (var quest in byQuest)
        {
            var ordered = quest.Select(row => (Row: row, Order: states.Int32(row, c.Order))).ToList();
            foreach (var (row, order) in ordered)
            {
                var lower = ordered.Where(other => other.Order < order).ToList();
                if (lower.Count == 0)
                {
                    continue;
                }

                var next = lower.Max(other => other.Order);
                var near = lower.Where(other => other.Order == next).SelectMany(other => states.KeyList(other.Row, c.Present));
                var reached = near.Where(flagAreas.ContainsKey).SelectMany(flag => flagAreas[flag]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (reached.Length > 0)
                {
                    goals[row] = reached;
                }
            }
        }

        return goals;
    }

    /// <summary>
    ///     Each tracker line of a state with goals (and the whole text) to its goals; a line the states give different goals
    ///     is dropped, and counted.
    /// </summary>
    private static (SortedDictionary<string, string[]> Lines, int Dropped) LinesOf(DatTable table, int textColumn, Dictionary<int, string[]> goals)
    {
        var lines = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (row, reached) in goals)
        {
            var text = table.String(row, textColumn).Replace("\r", string.Empty);
            foreach (var line in text.Split('\n').Append(text).Select(Normalize).Where(line => line.Length > 0).Distinct(StringComparer.Ordinal))
            {
                if (lines.TryGetValue(line, out var before) && !before.SequenceEqual(reached, StringComparer.Ordinal))
                {
                    ambiguous.Add(line);
                }

                lines[line] = reached;
            }
        }

        var kept = new SortedDictionary<string, string[]>(lines.Where(pair => !ambiguous.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal);
        return (kept, ambiguous.Count);
    }

    /// <summary>The known states lead where the game's data says: the columns and the rule read right.</summary>
    private static bool KnownGoalsHold(DatTable states, Columns c, Dictionary<int, string[]> goals)
    {
        foreach (var (text, _, goal) in KnownStates)
        {
            var row = DatAnchors.RowWith(states, c.Text, text);
            if (row < 0 || !goals.TryGetValue(row, out var reached) || !reached.Contains(goal))
            {
                Say.Out.WriteLine($"  \"{text}\" does not lead to {goal} ({(row < 0 || !goals.ContainsKey(row) ? "no goal" : string.Join(", ", goals[row]))}): {Step} keeps its previous catalog");
                return false;
            }
        }

        return true;
    }

    private static bool KnownLinesHold(SortedDictionary<string, string[]> english)
    {
        var missing = KnownStates.Where(state => !english.TryGetValue(Normalize(state.Text), out var reached) || !reached.Contains(state.Goal)).Select(state => state.Text).ToList();
        if (missing.Count > 0)
        {
            Say.Out.WriteLine($"  English lines dropped or wrong: {string.Join("; ", missing)}: {Step} keeps its previous catalog");
        }

        return missing.Count == 0;
    }
}
