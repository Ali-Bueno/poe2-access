/// <summary>
///     The Animation table's links between actions (docs/research/dodge-timing.md): the fallback an .act that lacks an
///     action plays, and the actions a skill plays together (a charge's start and end).
/// </summary>
internal sealed class AnimationActions
{
    private readonly DatTable table;
    private readonly AnimationColumns? columns;
    private readonly Dictionary<int, List<int>> together = new();

    public AnimationActions(DatTable table)
    {
        this.table = table;
        this.columns = AnimationColumns.Find(table);
        if (this.columns is not { } c)
        {
            return;
        }

        for (var row = 0; row < table.RowCount; row++)
        {
            var related = new[] { table.Key(row, c.Before), table.Key(row, c.After) }
                .Concat(table.RowList(row, c.Others)).Where(this.IsRow).Select(key => (int)key);
            foreach (var other in related)
            {
                this.Link(row, other);
                this.Link(other, row);
            }
        }
    }

    /// <summary>The actions and every action played with them (ChargeStart → Charge → ChargeEnd, ChargeEndAlt).</summary>
    public SortedSet<int> WithFollowUps(IEnumerable<int> actions)
    {
        var all = new SortedSet<int>(actions);
        var queue = new Queue<int>(all);
        while (queue.Count > 0)
        {
            foreach (var other in this.together.GetValueOrDefault(queue.Dequeue()) ?? new List<int>())
            {
                if (all.Add(other))
                {
                    queue.Enqueue(other);
                }
            }
        }

        return all;
    }

    /// <summary>The animation names an action plays, through its fallbacks when the .act lacks it, and the action found.</summary>
    public (List<string> Clips, int Action) Clips(IReadOnlyDictionary<string, string> act, int action)
    {
        for (var depth = 0; depth <= MonsterAnimationFiles.MaxAliasDepth && this.IsRow(action); depth++)
        {
            var clips = MonsterAnimationFiles.Clips(act, this.table.String(action, 0)).Distinct().ToList();
            if (clips.Count > 0 || this.columns == null)
            {
                return (clips, action);
            }

            action = (int)this.table.Key(action, this.columns!.Fallback);
        }

        return (new List<string>(), action);
    }

    private bool IsRow(long key) => key >= 0 && key < this.table.RowCount;

    private void Link(int from, int to)
    {
        if (!this.together.TryGetValue(from, out var list))
        {
            list = new List<int>();
            this.together[from] = list;
        }

        list.Add(to);
    }
}
