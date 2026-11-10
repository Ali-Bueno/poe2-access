using Index = LibBundle3.Index;

/// <summary>
///     `item-bases` command: every base a character wears or wields as the game's files describe it, so the mod can say
///     what a base named in a build plan is (docs/research/file-catalogs.md, "Item bases"):
///     { "bases": { base item Id: { class, level, armour, evasion, es } }, "names": { language: { base item Id: name } } }.
///     BaseItemTypes gives the Id, the name, the ItemClasses key and the level; ArmourTypes (a BaseItemTypes key) the three
///     defences, written only for a base it has a row for. The columns are <see cref="ItemBaseColumns"/>'. Read-only on the game.
/// </summary>
internal static class ItemBaseExtractor
{
    private const string Step = ItemBaseColumns.Step;

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var opened = new[] { "baseitemtypes", "itemclasses", "armourtypes" }.Select(name => DatTable.Open(index, name)).ToArray();
        if (opened.Any(table => table == null))
        {
            Say.Out.WriteLine($"An item base table is missing: {Step} left out.");
            return 1;
        }

        var t = new ItemBaseColumns.Tables(opened[0]!, opened[1]!, opened[2]!);
        if (DatColumnExtractor.NameColumn(index, NameAnchor.Items) is not { } itemName || ItemBaseColumns.Find(t, itemName) is not { } c)
        {
            return 1;
        }

        var armourRows = new Dictionary<long, int>();
        for (var row = 0; row < t.Armours.RowCount; row++)
        {
            armourRows.TryAdd(t.Armours.Key(row, c.ArmourBase), row);
        }

        var bases = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var rows = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var withDefences = 0;
        for (var row = 0; row < t.Items.RowCount; row++)
        {
            if (t.Items.StringOrNull(row, DatColumnSearch.IdColumn) is not { Length: > 0 } id ||
                DatAnchors.IdOf(t.Classes, t.Items.Key(row, c.ItemClass)) is not { } itemClass || !ItemBaseColumns.Equippable.Contains(itemClass))
            {
                continue;
            }

            var entry = new SortedDictionary<string, object>(StringComparer.Ordinal) { ["class"] = itemClass, ["level"] = t.Items.Int32(row, c.Level) };
            if (armourRows.TryGetValue(row, out var armour))
            {
                entry["armour"] = t.Armours.Int32(armour, c.Armour);
                entry["evasion"] = t.Armours.Int32(armour, c.Evasion);
                entry["es"] = t.Armours.Int32(armour, c.EnergyShield);
                withDefences++;
            }

            rows[id] = row;
            bases[id] = entry;
        }

        var names = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (code, folder) in AreaExtractor.Languages)
        {
            var items = folder == null ? t.Items : DatTable.OpenTranslated(index, "baseitemtypes", folder);
            if (items == null)
            {
                Say.Out.WriteLine($"  {code}: no BaseItemTypes");
                continue;
            }

            if (!ClientStringColumns.SameShape(t.Items, items))
            {
                Say.Out.WriteLine($"  {code}: BaseItemTypes differs from the English one in rows or row size: its columns cannot be told, {Step} left out");
                return 1;
            }

            var itemNames = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (id, row) in rows)
            {
                if (items.StringOrNull(row, c.ItemName)?.Trim() is { Length: > 0 } name)
                {
                    itemNames[id] = name;
                }
            }

            names[code] = itemNames;
            Say.Out.WriteLine($"  {code}: {itemNames.Count} base names");
        }

        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["bases"] = bases, ["names"] = names });
        Say.Out.WriteLine($"Wrote {outFile}: {bases.Count} bases, {withDefences} with defences, {names.Count} languages");
        return bases.Count > 0 ? 0 : 1;
    }
}
