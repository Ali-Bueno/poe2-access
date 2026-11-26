using Index = LibBundle3.Index;

/// <summary>
///     The <c>child|</c> lines of the skills a boss's clips fire by an event (<see cref="BossEventChildren"/>), for every
///     boss-bar monster: its skills split into those a clip plays (through its .act) and those none does, the latter with the
///     effects the telegraph tables draw for them (telegraph-skills' visuals) and the trigger that fires them.
/// </summary>
internal static class BossEventChildLines
{
    private const string ChildKind = "child";

    /// <summary>The lines, or an empty list (said) when a column is lost: the rest of the catalog stands.</summary>
    public static List<string> Of(Index index, DatTable monsters, DatTable skills, IEnumerable<int> bossRows, IEnumerable<string> censusLines)
    {
        if (DatTable.Open(index, "animation") is not { } animations || MonsterAnimationColumns.Find(monsters, skills, animations) is not { } columns ||
            TelegraphExtractor.Build(index) is not { } telegraphs)
        {
            Say.Out.WriteLine("The animation columns or the telegraph tables were not found: the skills clips fire by an event are left out of boss-mechanics.");
            return new List<string>();
        }

        var effects = telegraphs.Skills.Select(line => line.Split('|')).Where(f => f.Length > 3 && f[3].Length > 0)
            .ToDictionary(f => f[0], f => (IReadOnlyList<string>)f[3].Split(',').Select(path => path.Split(':')[0]).ToList(), StringComparer.Ordinal);
        var triggered = censusLines.Select(line => line.Split('|')).Where(f => f.Length > 2 && f[0] == ChildKind).Select(f => (Trigger: f[1], Child: f[2])).ToList();
        var triggers = triggered.GroupBy(pair => pair.Child, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Trigger, StringComparer.Ordinal);
        var files = BundleFiles.ByLowerPath(index);
        string? TextOf(string path) => files.TryGetValue(path.ToLowerInvariant(), out var file) ? file.ReadText() : null;
        var reader = new MonsterAnimationFiles(path => files.TryGetValue(path.ToLowerInvariant(), out var file) ? file.Read().ToArray() : null);
        var actions = new AnimationActions(animations);
        var clipsByAo = new Dictionary<string, ClipEffects>(StringComparer.OrdinalIgnoreCase);
        var found = new List<ClipLink>();
        foreach (var row in bossRows)
        {
            if (columns.FilesOf(monsters, row) is not ({ } act, { } ao))
            {
                continue;
            }

            var actMap = reader.Act(act);
            var (parents, children) = (new List<ClipSkill>(), new List<ClipChild>());
            foreach (var skill in monsters.KeyList(row, columns.Skills).Where(key => key >= 0 && key < skills.RowCount).Select(key => (int)key).Distinct())
            {
                var id = skills.String(skill, 0);
                var action = skills.Int64(skill, columns.SkillAnimation);
                if (action >= 0 && action < animations.RowCount)
                {
                    if (actions.Clips(actMap, (int)action).Clips is { Count: > 0 } clips)
                    {
                        parents.Add(new ClipSkill(id, clips));
                    }
                }
                else
                {
                    // A trigger's own hits are skills of their own, often granted to no monster: each is a child of the trigger's.
                    foreach (var child in triggered.Where(pair => pair.Trigger == id).Select(pair => pair.Child).Prepend(id))
                    {
                        if (effects.TryGetValue(child, out var drawn))
                        {
                            children.Add(new ClipChild(child, drawn, triggers.GetValueOrDefault(child)));
                        }
                    }
                }
            }

            if (!clipsByAo.TryGetValue(ao, out var clipEffects))
            {
                clipsByAo[ao] = clipEffects = ClipEffects.Read(ao, TextOf);
            }

            found.AddRange(BossEventChildren.Links(parents, children, clipEffects));
        }

        // The catalog is by skill: of the variants' clips, the earliest event that names a child (a word early beats one late).
        var links = found.GroupBy(link => (link.Parent, link.Child)).Select(group => group.OrderBy(link => link.Ms ?? int.MaxValue).First()).ToList();
        Say.Out.WriteLine($"Skills a boss's clip fires by an event: {links.Count} child lines, {links.Count(link => link.Ms == null)} by the effect family alone, the rest by a named event:");
        foreach (var link in links)
        {
            Say.Out.WriteLine($"  {BossEventChildren.Line(link)} ({link.Evidence})");
        }

        return links.Select(BossEventChildren.Line).ToList();
    }
}
