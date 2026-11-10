using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
///     The second opinion on every column a step read (docs/compatibility.md, "Self-describing tables"): each table opened
///     while the audit runs is remembered, and at the step's end the bytes of every column it read are judged against how it
///     was read (<see cref="DatColumnKinds"/>). A column whose kind changed fails the step, naming the table, the offset and
///     both kinds, where it would otherwise have written a catalog of garbage.
/// </summary>
internal sealed class DatColumnAudit
{
    private static readonly AsyncLocal<DatColumnAudit?> Running = new();
    private readonly List<DatTable> tables = new();

    /// <summary>What <see cref="Judge"/> found: the columns read, and of them the ones the bytes confirm, cannot tell, or contradict.</summary>
    internal sealed record Result(int Tables, int Columns, int Agree, int CannotTell, IReadOnlyList<string> Changed)
    {
        public bool Holds => this.Changed.Count == 0;
    }

    /// <summary>Starts judging the tables the calling step (and the tasks it starts) opens from now on.</summary>
    public static DatColumnAudit Start()
    {
        var audit = new DatColumnAudit();
        Running.Value = audit;
        return audit;
    }

    /// <summary>Remembers a table the running step opened; nothing outside a step.</summary>
    public static void Opened(DatTable table) => Running.Value?.Add(table);

    public void Add(DatTable table)
    {
        lock (this.tables)
        {
            this.tables.Add(table);
        }
    }

    /// <summary>Judges every column read so far of every table opened; a table opened twice is judged once.</summary>
    public Result Judge()
    {
        List<DatTable> opened;
        lock (this.tables)
        {
            opened = this.tables.ToList();
        }

        int columns = 0, agree = 0, cannotTell = 0;
        var changed = new List<string>();
        var byName = opened.GroupBy(table => table.Name, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var group in byName)
        {
            var table = group.First();
            foreach (var other in group.Skip(1))
            {
                table.MergeReads(other);
            }

            foreach (var (offset, reads) in table.Reads().ToList())
            {
                var facts = DatColumnKinds.Of(table, offset);
                foreach (var read in Enum.GetValues<DatRead>().Where(read => read != DatRead.None && reads.HasFlag(read)))
                {
                    columns++;
                    switch (DatColumnKinds.Judge(facts, read))
                    {
                        // An Id is read at the row's start by every extractor: text there that reads as names is another column.
                        case DatKindVerdict.Agrees when offset == DatColumnSearch.IdColumn && read == DatRead.Text && DatColumnKinds.StartsWithIds(table) == false:
                            changed.Add($"{table.Name} +0x0 is read as the Id, but its bytes hold display text (blanks in more than {DatColumnKinds.MostIdsSpaced:P0} of the rows)");
                            break;
                        case DatKindVerdict.Agrees:
                            agree++;
                            break;
                        case DatKindVerdict.CannotTell:
                            cannotTell++;
                            break;
                        default:
                            changed.Add($"{table.Name} +0x{offset:X} is read as {DatColumnKinds.Name(read)}, but its bytes hold {DatColumnKinds.Name(DatColumnKinds.KindOf(facts))}");
                            break;
                    }
                }
            }
        }

        return new Result(byName.Count, columns, agree, cannotTell, changed);
    }

    /// <summary>Prints the judgment and says whether the step's output may stand.</summary>
    public bool Report(string step)
    {
        var result = this.Judge();
        if (result.Columns > 0)
        {
            Say.Out.WriteLine($"{step}: {result.Columns} columns read in {result.Tables} tables: {result.Agree} hold the kind they are read as, " +
                              $"{result.CannotTell} cannot be told (blank or constant), {result.Changed.Count} changed kind");
        }

        foreach (var line in result.Changed)
        {
            Say.Out.WriteLine($"  column changed kind: {line} (lay the table out with dat-kinds), {step} keeps its previous catalog");
        }

        return result.Holds;
    }
}
