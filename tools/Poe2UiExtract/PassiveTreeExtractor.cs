using Index = LibBundle3.Index;

/// <summary>
///     `passive-tree` command: the main passive tree as the game's files hold it, so a node's name, kind, links and the
///     classes' starts need no row offset in memory (docs/research/file-catalogs.md): { "nodes": { graph id: "PassiveSkills
///     Id|flags" }, "links": { graph id: [graph ids] }, "starts": { graph id: [Characters Ids] }, "classes": { language:
///     { Characters Id: name } }, "names": { language: { PassiveSkills Id: name } } }. Flags: k keystone, n notable, j
///     jewel socket, i icon only. The graph is metadata/passiveskillgraph.psg (<see cref="SkillGraphFile"/>); the rows and
///     their columns are PassiveSkills' and Characters', found as `dat-columns` finds them. Read-only on the game.
/// </summary>
internal static class PassiveTreeExtractor
{
    private const string Step = "passive-tree";
    private const string MainGraph = "metadata/passiveskillgraph.psg";
    private const string FlagSeparator = "|";

    /// <summary>The share of the graph's nodes that must be PassiveSkills rows: under it the graph id column is another one.</summary>
    private const double ResolvedShare = 0.99;

    private static readonly (string Column, char Flag)[] Flags =
    {
        ("PassiveSkills.IsKeystone", 'k'), ("PassiveSkills.IsNotable", 'n'), ("PassiveSkills.IsJewelSocket", 'j'), ("PassiveSkills.IsIconOnly", 'i'),
    };

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var passives = DatTable.Open(index, "passiveskills");
        var characters = DatTable.Open(index, "characters");
        if (passives == null || characters == null || BundleFiles.Find(index, MainGraph) is not { } graphFile)
        {
            Say.Out.WriteLine($"PassiveSkills, Characters or {MainGraph} not found: {Step} left out.");
            return 1;
        }

        var graph = SkillGraphFile.Parse(graphFile.Read().ToArray());
        if (graph == null || !graph.IsConsistent)
        {
            Say.Out.WriteLine($"  {MainGraph}: not read as a skill graph of version {SkillGraphFile.KnownVersion} (its first byte is {graphFile.Read().ToArray().FirstOrDefault()}): the .psg format changed, {Step} left out");
            return 1;
        }

        var columns = DatContentColumns.Find(index).Where(found => found.Offset != null).ToDictionary(found => found.Column, found => found.Offset!.Value, StringComparer.Ordinal);
        var wanted = Flags.Select(flag => flag.Column).Append("PassiveSkills.GraphId").Append("PassiveSkills.Characters").Append("Characters.Id").ToList();
        if (wanted.Where(column => !columns.ContainsKey(column)).ToList() is { Count: > 0 } missing ||
            DatColumnExtractor.NameColumn(index, NameAnchor.Passives) is not { } passiveName || DatColumnExtractor.NameColumn(index, NameAnchor.Characters) is not { } className)
        {
            Say.Out.WriteLine($"  {Step}: a PassiveSkills or Characters column was not found (check with dat-kinds)");
            return 1;
        }

        var rowOf = new Dictionary<int, int>();
        for (var row = 0; row < passives.RowCount; row++)
        {
            rowOf.TryAdd(passives.UInt16(row, columns["PassiveSkills.GraphId"]), row);
        }

        var resolved = graph.Nodes.Count(node => rowOf.ContainsKey(node.Id));
        if (resolved < graph.Nodes.Count * ResolvedShare)
        {
            Say.Out.WriteLine($"  {MainGraph}: {resolved} of {graph.Nodes.Count} nodes found in PassiveSkills: the graph id column moved");
            return 1;
        }

        var nodes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var links = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var starts = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var rows = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var graphLinks = graph.Links();
        foreach (var id in graphLinks.Keys.Where(rowOf.ContainsKey).Order())
        {
            var row = rowOf[id];
            if (passives.StringOrNull(row, DatColumnSearch.IdColumn) is not { Length: > 0 } passive)
            {
                continue;
            }

            var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nodes[key] = passive + FlagSeparator + string.Concat(Flags.Where(flag => passives.Bool(row, columns[flag.Column])).Select(flag => flag.Flag));
            links[key] = graphLinks[id].ToList();
            rows[passive] = row;
            var classes = passives.KeyList(row, columns["PassiveSkills.Characters"]).Select(key => DatAnchors.IdOf(characters, key)).OfType<string>().ToList();
            if (classes.Count > 0)
            {
                starts[key] = classes;
            }
        }

        if (starts.Count == 0)
        {
            Say.Out.WriteLine($"  {Step}: no node of the graph starts a class: the class list column holds something else");
            return 1;
        }

        var variants = Variants(index, passives, columns["PassiveSkills.GraphId"], rows);

        var names = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var classNames = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var localPassives = folder == null ? passives : DatTable.OpenTranslated(index, "passiveskills", folder);
            var localClasses = folder == null ? characters : DatTable.OpenTranslated(index, "characters", folder);
            if (localPassives == null || localClasses == null)
            {
                Say.Out.WriteLine($"  {code}: no PassiveSkills or Characters");
                continue;
            }

            if (!ClientStringColumns.SameShape(passives, localPassives) || !ClientStringColumns.SameShape(characters, localClasses))
            {
                Say.Out.WriteLine($"  {code}: PassiveSkills or Characters differs from the English table in rows or row size: {Step} left out");
                return 1;
            }

            var named = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (passive, row) in rows)
            {
                if (localPassives.StringOrNull(row, passiveName)?.Trim() is { Length: > 0 } name)
                {
                    named[passive] = name;
                }
            }

            var classesNamed = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (var row = 0; row < characters.RowCount; row++)
            {
                if (characters.StringOrNull(row, columns["Characters.Id"]) is { Length: > 0 } id && localClasses.StringOrNull(row, className)?.Trim() is { Length: > 0 } name)
                {
                    classesNamed[id] = name;
                }
            }

            names[code] = named;
            classNames[code] = classesNamed;
            Say.Out.WriteLine($"  {code}: {named.Count} node names, {classesNamed.Count} classes");
        }

        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["nodes"] = nodes, ["links"] = links, ["starts"] = starts, ["variants"] = variants, ["classes"] = classNames, ["names"] = names });
        Say.Out.WriteLine($"Wrote {outFile}: {nodes.Count} nodes of {graph.Nodes.Count} in the graph, {starts.Count} class starts, {variants.Count} variants, {names.Count} languages");
        return 0;
    }

    private const string VariantTable = "passiveskillvariants";
    private const string VariantTypeTable = "passiveskillvarianttypes";

    /// <summary>A PassiveSkillVariants row: the key of its type, then the key of the PassiveSkills row a node becomes (two 16-byte keys, 0.5.5g).</summary>
    private const int VariantRowSize = 0x20;
    private const int VariantTypeColumn = 0x0;
    private const int VariantPassiveColumn = 0x10;

    /// <summary>
    ///     What a node with a choice can be made ("+5 to any attribute" as Strength, Dexterity or Intelligence): { graph id of the
    ///     variant's PassiveSkills row: "its Id|its type's Id" }, the id the tree's state keeps beside the node's own
    ///     (docs/research/passive-tree.md#attribute-choices). The rows join <paramref name="rows"/> so that they are named too.
    ///     A table of another shape gives none: the tree is written without them.
    /// </summary>
    private static SortedDictionary<string, string> Variants(Index index, DatTable passives, int graphIdColumn, SortedDictionary<string, int> rows)
    {
        var variants = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var table = DatTable.Open(index, VariantTable);
        var types = DatTable.Open(index, VariantTypeTable);
        if (table == null || types == null || table.RowSize != VariantRowSize)
        {
            Say.Out.WriteLine($"  {Step}: PassiveSkillVariants not found or not {VariantRowSize} bytes a row ({table?.RowSize.ToString() ?? "no table"}): no variants written");
            return variants;
        }

        for (var row = 0; row < table.RowCount; row++)
        {
            var passive = table.Key(row, VariantPassiveColumn);
            if (DatAnchors.IdOf(passives, passive) is not { Length: > 0 } id || DatAnchors.IdOf(types, table.Key(row, VariantTypeColumn)) is not { Length: > 0 } type)
            {
                Say.Out.WriteLine($"  {Step}: PassiveSkillVariants row {row} names no PassiveSkills row or no type: its columns hold something else, no variants written");
                variants.Clear();
                return variants;
            }

            variants[passives.UInt16((int)passive, graphIdColumn).ToString(System.Globalization.CultureInfo.InvariantCulture)] = id + FlagSeparator + type;
            rows.TryAdd(id, (int)passive);
        }

        return variants;
    }
}
