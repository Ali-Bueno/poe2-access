using Index = LibBundle3.Index;

/// <summary>
///     `passive-tree` command: the main passive tree as the game's files hold it, so a node's name, kind, links and the
///     classes' starts need no row offset in memory (docs/research/file-catalogs.md): { "nodes": { graph id: "PassiveSkills
///     Id|flags" }, "links": { graph id: [graph ids] }, "starts": { graph id: [Characters Ids] }, "classes": { language:
///     { Characters Id: name } }, "names": { language: { PassiveSkills Id: name } } }, and, when their columns are found,
///     "ascendancies": { graph id: Ascendancy Id } and "offers": { graph id: PassiveSkillVariantTypes Id } (a node that
///     offers a choice). Flags: k keystone, n notable, j jewel socket, i icon only (left out when no such column is found),
///     a node of an ascendancy, s an ascendancy's start. The graph is <see cref="MainGraph"/> (<see cref="SkillGraphFile"/>);
///     the rows and their columns are PassiveSkills' and Characters', found as `dat-columns` finds them. Read-only on the game.
/// </summary>
internal static class PassiveTreeExtractor
{
    private const string Step = "passive-tree";
    private const string MainGraph = "metadata/passiveskillgraph.psg";

    /// <summary>The graph file's name up to its extension: a file of another name under it stands in when the exact one is gone.</summary>
    private const string GraphPrefix = "metadata/passiveskillgraph";

    private const string GraphExtension = ".psg";
    private const string FlagSeparator = "|";
    private const char AscendancyFlag = 'a';
    private const char AscendancyStartFlag = 's';

    /// <summary>The share of the graph's nodes that must be PassiveSkills rows: under it the graph id column is another one.</summary>
    private const double ResolvedShare = 0.99;

    private static readonly (string Column, char Flag)[] Flags =
    {
        ("PassiveSkills.IsKeystone", 'k'), ("PassiveSkills.IsNotable", 'n'), ("PassiveSkills.IsJewelSocket", 'j'), ("PassiveSkills.IsIconOnly", 'i'),
    };

    /// <summary>The main graph: the exact path, else the only .psg under <see cref="GraphPrefix"/>.</summary>
    private static LibBundle3.Records.FileRecord? GraphFileOf(Index index)
    {
        if (BundleFiles.Find(index, MainGraph) is { } exact)
        {
            return exact;
        }

        var near = BundleFiles.ByLowerPath(index).Where(file => file.Key.StartsWith(GraphPrefix, StringComparison.Ordinal) && file.Key.EndsWith(GraphExtension, StringComparison.Ordinal)).ToList();
        return near.Count == 1 ? near[0].Value : null;
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var passives = DatTable.Open(index, "passiveskills");
        var characters = DatTable.Open(index, "characters");
        if (passives == null || characters == null || GraphFileOf(index) is not { } graphFile)
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
        var wanted = Flags.Select(flag => flag.Column).Except(DatContentColumns.Optional).Append("PassiveSkills.GraphId").Append("PassiveSkills.Characters").Append("Characters.Id").ToList();
        if (wanted.Where(column => !columns.ContainsKey(column)).ToList() is { Count: > 0 } missing ||
            DatColumnExtractor.NameColumn(index, NameAnchor.Passives) is not { } passiveName || DatColumnExtractor.NameColumn(index, NameAnchor.Characters) is not { } className)
        {
            Say.Out.WriteLine($"  {Step}: a PassiveSkills or Characters column was not found (check with dat-kinds)");
            return 1;
        }

        foreach (var left in Flags.Select(flag => flag.Column).Where(column => !columns.ContainsKey(column)))
        {
            Say.Out.WriteLine($"  {Step}: {left} not found: written without it");
        }

        var extras = DatContentColumns.FindPassiveExtras(index).Where(found => found.Offset != null).ToDictionary(found => found.Column, found => found.Offset!.Value, StringComparer.Ordinal);
        if (graph.Nodes.FirstOrDefault(node => node.Id > ushort.MaxValue) is { } wide)
        {
            Say.Out.WriteLine($"  {MainGraph}: a node has graph id {wide.Id}, which PassiveSkills' 16-bit column cannot hold: {Step} left out rather than cut it");
            return 1;
        }

        NoteExceptions(passives, characters, columns);
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
        var ascendancies = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var offers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var ascendancyTable = DatTable.Open(index, "ascendancy");
        var typeTable = DatTable.Open(index, VariantTypeTable);
        foreach (var id in graphLinks.Keys.Where(rowOf.ContainsKey).Order())
        {
            var row = rowOf[id];
            if (passives.StringOrNull(row, DatColumnSearch.IdColumn) is not { Length: > 0 } passive)
            {
                continue;
            }

            var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var flags = string.Concat(Flags.Where(flag => columns.ContainsKey(flag.Column) && passives.Bool(row, columns[flag.Column])).Select(flag => flag.Flag));
            if (extras.TryGetValue(DatContentColumns.AscendancyColumn, out var ascendancyColumn) && ascendancyTable != null && DatAnchors.IdOf(ascendancyTable, passives.Key(row, ascendancyColumn)) is { Length: > 0 } ascendancy)
            {
                flags += AscendancyFlag;
                ascendancies[key] = ascendancy;
            }

            if (extras.TryGetValue(DatContentColumns.AscendancyStartColumn, out var startColumn) && passives.Bool(row, startColumn))
            {
                flags += AscendancyStartFlag;
            }

            if (extras.TryGetValue(DatContentColumns.VariantTypeColumn, out var typeColumn) && typeTable != null && DatAnchors.IdOf(typeTable, passives.Key(row, typeColumn)) is { Length: > 0 } offered)
            {
                offers[key] = offered;
            }

            nodes[key] = passive + FlagSeparator + flags;
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
        NoteOtherFlags(passives, graphLinks.Keys.Where(rowOf.ContainsKey).Select(id => rowOf[id]).ToList(), columns, extras);

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

        var catalog = new SortedDictionary<string, object>(StringComparer.Ordinal) { ["nodes"] = nodes, ["links"] = links, ["starts"] = starts, ["variants"] = variants, ["classes"] = classNames, ["names"] = names };
        if (ascendancies.Count > 0)
        {
            catalog["ascendancies"] = ascendancies;
        }

        if (offers.Count > 0)
        {
            catalog["offers"] = offers;
        }

        AreaExtractor.WriteJson(outFile, catalog);
        Say.Out.WriteLine($"Wrote {outFile}: {nodes.Count} nodes of {graph.Nodes.Count} in the graph, {starts.Count} class starts, {variants.Count} variants, {ascendancies.Count} ascendancy nodes, {offers.Count} nodes that offer a choice, {names.Count} languages");
        return 0;
    }

    /// <summary>Says what the relaxed column rules let through: rows whose graph id repeats or is zero, Characters rows no start names.</summary>
    private static void NoteExceptions(DatTable passives, DatTable characters, IReadOnlyDictionary<string, int> columns)
    {
        using var probe = DatTable.Probe();
        if (PassiveColumnRules.GraphIdExceptions(passives, columns["PassiveSkills.GraphId"]) is { Count: > 0 } odd)
        {
            Say.Out.WriteLine($"  {Step}: {odd.Count} PassiveSkills rows have a zero or repeated graph id (first {string.Join(", ", odd.Take(PassiveColumnRules.ExamplesShown).Select(row => $"row {row.Row} = {row.Value}"))}); the graph's nodes are found by it all the same");
        }

        if (PassiveColumnRules.UnnamedTargets(passives, columns["PassiveSkills.Characters"], characters.RowCount) is { Count: > 0 } unnamed)
        {
            Say.Out.WriteLine($"  {Step}: {unnamed.Count} of {characters.RowCount} Characters rows are named by no start node: {string.Join(", ", unnamed.Take(PassiveColumnRules.ExamplesShown).Select(row => characters.String(row, 0)))}");
        }
    }

    /// <summary>Logs the flag columns of the tree's rows that no kind of node here uses: a rebuilt tree may bring a new kind of node.</summary>
    private static void NoteOtherFlags(DatTable passives, IReadOnlyCollection<int> rows, IReadOnlyDictionary<string, int> columns, IReadOnlyDictionary<string, int> extras)
    {
        var known = Flags.Where(flag => columns.ContainsKey(flag.Column)).Select(flag => columns[flag.Column]).ToHashSet();
        if (extras.TryGetValue(DatContentColumns.AscendancyStartColumn, out var start))
        {
            known.Add(start);
        }

        foreach (var (offset, count, examples) in PassiveColumnRules.OtherFlags(passives, rows, known))
        {
            Say.Out.WriteLine($"  {Step}: a flag at +0x{offset:X} is set on {count} nodes of the tree and is no kind this reads (for example {string.Join(", ", examples)}): a new kind of node?");
        }
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
