using Index = LibBundle3.Index;

/// <summary>
///     `spectres` command: every MonsterVarieties row as { "spectres": { "&lt;monster path&gt;": &lt;experience multiplier&gt; } }.
///     The plugin turns the multiplier into the Spirit a Bind Spectre of that monster reserves
///     (<see cref="Poe2Accessibility.Services.SpectreCatalog"/>, docs/research/corpse-scanning.md): the game's dat files
///     hold no spirit column, and the cost follows the multiplier on every monster whose tooltip cost is published.
///     The column is MonsterVarieties.ExperienceMultiplier (dat-schema name), measured at +0xA8 in 0x406-byte rows and
///     found again by the values of the rows below; the boss variant is among them because its life multiplier (266)
///     differs from its experience multiplier (200), which tells the two columns apart. The step fails, and writes
///     nothing, when the column is not as expected. Read-only on the game.
/// </summary>
internal static class SpectreExtractor
{
    private const string Step = "spectres";
    private const string MonstersTable = "monstervarieties";

    private const int ExpectedRowSize = 0x406;

    // Measured offset (2026-11-17, dat-rows), where the column is looked for first.
    private const int ExperienceOffset = 0xA8;

    /// <summary>Rows whose experience multiplier was read on 2026-11-17 and whose spectre costs are published (poe2wiki, Bind Spectre).</summary>
    private static readonly (string Id, int Experience)[] Known =
    {
        ("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving", 100), // Vaal Guard (grenade), 50 Spirit
        ("Metadata/Monsters/VaalMonsters/Living/VaalGuardSpearLiving", 110), // Vaal Guard (spear), 60 Spirit
        ("Metadata/Monsters/BloodBathers/VaalApparition/SunVaalApparition", 200), // Priest of the Sun, 100 Spirit
        ("Metadata/Monsters/QuillCrab/QuillCrabPoison", 70), // Venomous Crab, 40 Spirit
        ("Metadata/Monsters/SerpentClanMonster/SerpentClanCasterBoss", 200), // life multiplier 266: tells the columns apart
    };

    /// <summary>No monster's experience multiplier reads outside this on the measured table (largest seen 2026-11-17: 250).</summary>
    private const int MaxExperience = 10000;

    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, MonstersTable) is not { } monsters)
        {
            Say.Out.WriteLine($"MonsterVarieties not found: {Step} left out.");
            return 1;
        }

        if (monsters.RowSize != ExpectedRowSize)
        {
            Say.Out.WriteLine($"MonsterVarieties rows are 0x{monsters.RowSize:X} bytes, not 0x{ExpectedRowSize:X}: its columns moved, {Step} keeps its previous catalog");
            return 1;
        }

        var knownRows = Known.Select(known => (known.Id, known.Experience, Row: DatColumnSearch.RowOf(monsters, known.Id))).ToList();
        if (knownRows.Any(known => known.Row < 0))
        {
            Say.Out.WriteLine($"A known MonsterVarieties row is gone ({string.Join(", ", knownRows.Where(known => known.Row < 0).Select(known => known.Id))}): {Step} keeps its previous catalog");
            return 1;
        }

        var search = new DatColumnSearch();
        var experience = search.Find(monsters, "MonsterVarieties.ExperienceMultiplier", ExperienceOffset,
            offset => IsExperience(monsters, offset, knownRows));
        if (!search.Report(Step))
        {
            return 1;
        }

        var spectres = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var id = monsters.String(row, DatColumnSearch.IdColumn);
            var value = monsters.Int32(row, experience);
            if (id.Length > 0 && value > 0)
            {
                spectres[id] = value;
            }
        }

        if (!KnownRowsHold(spectres))
        {
            return 1;
        }

        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["spectres"] = spectres });
        Say.Out.WriteLine($"Wrote {outFile}: {spectres.Count} monsters with an experience multiplier of {monsters.RowCount} rows");
        return 0;
    }

    /// <summary>
    ///     The experience multiplier: a whole number from 0 to <see cref="MaxExperience"/> on every row, worth something
    ///     on most, and the measured value on each known row.
    /// </summary>
    private static bool IsExperience(DatTable monsters, int offset, List<(string Id, int Experience, int Row)> knownRows)
    {
        using var probe = DatTable.Probe();
        if (offset + sizeof(int) > monsters.RowSize || knownRows.Any(known => known.Row < 0 || monsters.Int32(known.Row, offset) != known.Experience))
        {
            return false;
        }

        var worth = 0;
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var value = monsters.Int32(row, offset);
            if (value < 0 || value > MaxExperience)
            {
                return false;
            }

            worth += value > 0 ? 1 : 0;
        }

        return worth > monsters.RowCount / 2;
    }

    private static bool KnownRowsHold(SortedDictionary<string, int> spectres)
    {
        var ok = true;
        foreach (var (id, experience) in Known)
        {
            if (!spectres.TryGetValue(id, out var found) || found != experience)
            {
                Say.Out.WriteLine($"  {id} should hold an experience multiplier of {experience}, found {(spectres.ContainsKey(id) ? found.ToString() : "no row")}: {Step} keeps its previous catalog");
                ok = false;
            }
        }

        return ok;
    }
}
