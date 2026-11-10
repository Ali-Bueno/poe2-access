using Index = LibBundle3.Index;

/// <summary>
///     `gems` command: the skill and support gems as the game's files describe them, so the mod can name a gem or a skill
///     from an Id it holds without following pointers through table rows in memory (docs/research/file-catalogs.md):
///     { "gems": { base item Id: { kind, effects, skills, tags, str, dex, int } }, "names": { language: { base item Id: name } },
///     "tags": { language: { tag Id: name } }, "support": { language: { base item Id: what the support does } },
///     "skills": { language: { GrantedEffects Id: the skill's name } } }. The chain: SkillGems (a BaseItemTypes key, the
///     attribute shares, its GemEffects) -> GemEffects (its GrantedEffects, its GemTags, the support text) -> GrantedEffects
///     -> ActiveSkills (the displayed name). The columns are <see cref="GemColumns"/>'. Read-only on the game.
/// </summary>
internal static class GemExtractor
{
    private const string Step = GemColumns.Step;

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var opened = new[] { "skillgems", "gemeffects", "gemtags", "baseitemtypes", "grantedeffects", "activeskills" }.Select(name => DatTable.Open(index, name)).ToArray();
        if (opened.Any(table => table == null))
        {
            Say.Out.WriteLine($"A gem table is missing: {Step} left out.");
            return 1;
        }

        var t = new GemColumns.Tables(opened[0]!, opened[1]!, opened[2]!, opened[3]!, opened[4]!, opened[5]!);
        if (DatColumnExtractor.NameColumn(index, NameAnchor.Items) is not { } itemName || DatColumnExtractor.NameColumn(index, NameAnchor.ActiveSkills) is not { } skillName ||
            GemColumns.Find(t, itemName, skillName) is not { } c)
        {
            return 1;
        }

        var gems = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var gemRows = new SortedDictionary<string, (int Item, List<int> Effects)>(StringComparer.Ordinal);
        for (var row = 0; row < t.Gems.RowCount; row++)
        {
            var item = (int)t.Gems.Key(row, c.GemBase);
            if (item < 0 || item >= t.Items.RowCount || t.Items.StringOrNull(item, DatColumnSearch.IdColumn) is not { Length: > 0 } id)
            {
                continue;
            }

            var effects = t.Gems.KeyList(row, c.GemEffects).Where(key => key >= 0 && key < t.Effects.RowCount).Select(key => (int)key).ToList();
            var skills = effects.Select(effect => DatAnchors.IdOf(t.Skills, t.Effects.Key(effect, c.EffectSkill))).OfType<string>().Where(skill => skill.Length > 0).Distinct().ToList();
            var tags = effects.SelectMany(effect => t.Effects.KeyList(effect, c.EffectTags)).Select(tag => DatAnchors.IdOf(t.Tags, tag)).OfType<string>().Distinct().ToList();
            var kind = t.Items.StringOrNull(item, c.ItemKind) ?? string.Empty;
            gemRows[id] = (item, effects);
            gems[id] = new
            {
                kind = kind[(kind.LastIndexOf('/') + 1)..],
                effects = effects.Select(effect => t.Effects.String(effect, DatColumnSearch.IdColumn)).ToList(),
                skills,
                tags,
                str = t.Gems.Int32(row, c.Strength),
                dex = t.Gems.Int32(row, c.Dexterity),
                @int = t.Gems.Int32(row, c.Intelligence),
            };
        }

        var names = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var tagNames = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var support = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var skillNames = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            DatTable? Local(DatTable english, string name) => folder == null ? english : DatTable.OpenTranslated(index, name, folder);
            var (items, effects, tags, active) = (Local(t.Items, "baseitemtypes"), Local(t.Effects, "gemeffects"), Local(t.Tags, "gemtags"), Local(t.ActiveSkills, "activeskills"));
            if (items == null || effects == null || tags == null || active == null)
            {
                Say.Out.WriteLine($"  {code}: no gem tables");
                continue;
            }

            if (!ClientStringColumns.SameShape(t.Items, items) || !ClientStringColumns.SameShape(t.Effects, effects) || !ClientStringColumns.SameShape(t.Tags, tags) || !ClientStringColumns.SameShape(t.ActiveSkills, active))
            {
                Say.Out.WriteLine($"  {code}: a gem table differs from the English one in rows or row size: its columns cannot be told, {Step} left out");
                return 1;
            }

            var itemNames = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var supportTexts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (id, (item, gemEffects)) in gemRows)
            {
                if (items.StringOrNull(item, c.ItemName)?.Trim() is { Length: > 0 } name)
                {
                    itemNames[id] = name;
                }

                if (gemEffects.Select(effect => effects.StringOrNull(effect, c.SupportText)?.Trim()).FirstOrDefault(text => !string.IsNullOrEmpty(text)) is { } text)
                {
                    supportTexts[id] = GameMarkup.Plain(text);
                }
            }

            names[code] = itemNames;
            support[code] = supportTexts;
            tagNames[code] = Texts(t.Tags, tags, c.TagName, GameMarkup.Plain);
            skillNames[code] = SkillNames(t, active, c);
            Say.Out.WriteLine($"  {code}: {itemNames.Count} gem names, {supportTexts.Count} support texts");
        }

        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["gems"] = gems, ["names"] = names, ["tags"] = tagNames, ["support"] = support, ["skills"] = skillNames });
        Say.Out.WriteLine($"Wrote {outFile}: {gems.Count} gems, {names.Count} languages");
        return gems.Count > 0 ? 0 : 1;
    }

    /// <summary>Each row's Id (from the English table) with its text in a language's copy; rows without text are left out.</summary>
    private static SortedDictionary<string, string> Texts(DatTable english, DatTable local, int column, Func<string, string> shown)
    {
        var texts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < english.RowCount; row++)
        {
            if (english.StringOrNull(row, DatColumnSearch.IdColumn) is { Length: > 0 } id && local.StringOrNull(row, column)?.Trim() is { Length: > 0 } text)
            {
                texts[id] = shown(text);
            }
        }

        return texts;
    }

    /// <summary>Every GrantedEffects Id whose ActiveSkills row has a displayed name, with that name in a language.</summary>
    private static SortedDictionary<string, string> SkillNames(GemColumns.Tables t, DatTable active, GemColumns c)
    {
        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < t.Skills.RowCount; row++)
        {
            var key = t.Skills.Key(row, c.SkillActive);
            if (key >= 0 && key < active.RowCount && t.Skills.StringOrNull(row, DatColumnSearch.IdColumn) is { Length: > 0 } id && active.StringOrNull((int)key, c.SkillName)?.Trim() is { Length: > 0 } name)
            {
                names[id] = name;
            }
        }

        return names;
    }
}
