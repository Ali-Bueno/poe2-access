/// <summary>
///     The Animation table's links between actions (docs/research/dodge-timing.md): the fallback an .act that lacks an
///     action plays, and the actions a skill plays together (a charge's start and end).
/// </summary>
internal sealed class AnimationActions
{
    // Animation (community dat-schema, 0x6A-byte rows): +0x0A "Mainhand_AnimationKey", the fallback (SpellProjectileFire →
    // SpellProjectile); +0x2B / +0x34 the actions before and after (Charge: ChargeStart, ChargeEnd); +0x3C others it plays.
    private const int AnimationRowSize = 0x6A;
    private const int FallbackOffset = 0x0A;
    private const int BeforeOffset = 0x2B;
    private const int AfterOffset = 0x34;
    private const int OthersOffset = 0x3C;

    private readonly DatTable table;
    private readonly bool linked;
    private readonly Dictionary<int, List<int>> together = new();

    public AnimationActions(DatTable table)
    {
        this.table = table;
        this.linked = HitTimeExtractor.Sized(table, AnimationRowSize, "Animation", "fallbacks and follow-up actions");
        if (!this.linked)
        {
            return;
        }

        for (var row = 0; row < table.RowCount; row++)
        {
            var related = new[] { table.Key(row, BeforeOffset), table.Key(row, AfterOffset) }
                .Concat(table.RowList(row, OthersOffset)).Where(this.IsRow).Select(key => (int)key);
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
            if (clips.Count > 0 || !this.linked)
            {
                return (clips, action);
            }

            action = (int)this.table.Key(action, FallbackOffset);
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
