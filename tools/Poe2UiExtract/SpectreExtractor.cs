using Index = LibBundle3.Index;

/// <summary>
///     `spectres` command: every MonsterVarieties row as
///     { "spectres": { "&lt;monster path&gt;": { "x": &lt;experience multiplier&gt;, "w": "&lt;main-hand ItemClasses Id&gt;", "a": "ranged|melee", "t": true } } }
///     ("w"/"a" left out when empty, "t" when the variety needs no telling). The plugin turns the multiplier into the
///     Spirit a Bind Spectre of that monster reserves (<see cref="Poe2Accessibility.Services.SpectreCatalog"/>,
///     docs/research/corpse-scanning.md): the game's dat files hold no spirit column, and the cost follows the
///     multiplier on every monster whose tooltip cost is published. The weapon and the melee/ranged tag say which
///     variety of a shared name a corpse is (the three Vaal Guards read one name: grenade at 50, bow and spear at 60),
///     what a sighted player sees in its hands; "t" marks the rows of a name whose varieties differ in cost, weapon or
///     reach, the only ones spoken with a variety. The columns are MonsterVarieties.ExperienceMultiplier (+0xA8),
///     Name (+0x110), MainHand_ItemClass (+0x154) and Tags (+0x98) in 0x406-byte rows (dat-schema names, measured
///     2026-11-17/18), each found again by what it holds and judged on the rows below; the boss variant is among them
///     because its life multiplier (266) differs from its experience multiplier (200), which tells the two columns
///     apart. The step fails, and writes nothing, when a column is not as expected. Read-only on the game.
/// </summary>
internal static class SpectreExtractor
{
    private const string Step = "spectres";
    private const string MonstersTable = "monstervarieties";
    private const string ItemClassesTable = "itemclasses";
    private const string TagsTable = "tags";

    private const int ExpectedRowSize = 0x406;

    // Measured offsets (2026-11-17/18, dat-rows and dat-kinds), where each column is looked for first.
    private const int ExperienceOffset = 0xA8;
    private const int NameOffset = 0x110;
    private const int MainHandOffset = 0x154;
    private const int TagsOffset = 0x98;

    /// <summary>The three guards of Utzaal, one name ("Vaal Guard") between them: the rows the variety is judged on.</summary>
    private const string MortarGuard = "Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"; // grenade, 50 Spirit
    private const string BowGuard = "Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving"; // bow, 60 Spirit
    private const string SpearGuard = "Metadata/Monsters/VaalMonsters/Living/VaalGuardSpearLiving"; // spear, 60 Spirit
    private const string GuardName = "Vaal Guard";
    private const string BowClass = "Bow";
    private const string SpearClass = "Warstaff"; // the spear guard's spear is a Warstaff in the game's data (row 58, 2026-11-18)

    /// <summary>Rows whose experience multiplier was read on 2026-11-17 and whose spectre costs are published (poe2wiki, Bind Spectre).</summary>
    private static readonly (string Id, int Experience)[] Known =
    {
        (MortarGuard, 100), // Vaal Guard (grenade), 50 Spirit
        (SpearGuard, 110), // Vaal Guard (spear), 60 Spirit
        ("Metadata/Monsters/BloodBathers/VaalApparition/SunVaalApparition", 200), // Priest of the Sun, 100 Spirit
        ("Metadata/Monsters/QuillCrab/QuillCrabPoison", 70), // Venomous Crab, 40 Spirit
        ("Metadata/Monsters/SerpentClanMonster/SerpentClanCasterBoss", 200), // life multiplier 266: tells the columns apart
    };

    /// <summary>No monster's experience multiplier reads outside this on the measured table (largest seen 2026-11-17: 250).</summary>
    private const int MaxExperience = 10000;

    private sealed record Columns(int Experience, int Name, int MainHand, int Tags);

    private sealed record Monster(string Id, int Experience, string Name, string Weapon, string Attack);

    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, MonstersTable) is not { } monsters || DatTable.Open(index, ItemClassesTable) is not { } itemClasses ||
            DatTable.Open(index, TagsTable) is not { } tags)
        {
            Say.Out.WriteLine($"MonsterVarieties, ItemClasses or Tags not found: {Step} left out.");
            return 1;
        }

        if (monsters.RowSize != ExpectedRowSize)
        {
            Say.Out.WriteLine($"MonsterVarieties rows are 0x{monsters.RowSize:X} bytes, not 0x{ExpectedRowSize:X}: its columns moved, {Step} keeps its previous catalog");
            return 1;
        }

        var knownRows = Known.Select(known => (known.Id, known.Experience, Row: DatColumnSearch.RowOf(monsters, known.Id))).ToList();
        var guards = new[] { MortarGuard, BowGuard, SpearGuard }.Select(id => (Id: id, Row: DatColumnSearch.RowOf(monsters, id))).ToList();
        if (knownRows.Concat(guards.Select(guard => (guard.Id, Experience: 0, guard.Row))).Any(known => known.Row < 0))
        {
            var gone = knownRows.Select(known => (known.Id, known.Row)).Concat(guards).Where(known => known.Row < 0).Select(known => known.Id);
            Say.Out.WriteLine($"A known MonsterVarieties row is gone ({string.Join(", ", gone)}): {Step} keeps its previous catalog");
            return 1;
        }

        var search = new DatColumnSearch();
        var columns = new Columns(
            search.Find(monsters, "MonsterVarieties.ExperienceMultiplier", ExperienceOffset, offset => IsExperience(monsters, offset, knownRows)),
            search.Text(monsters, "MonsterVarieties.Name", NameOffset, known: (guards[0].Row, text => text == GuardName)),
            search.Key(monsters, "MonsterVarieties.MainHand_ItemClass", MainHandOffset, itemClasses,
                known: (guards[1].Row, key => DatAnchors.IdOf(itemClasses, key) == BowClass)),
            search.KeyList(monsters, "MonsterVarieties.Tags", TagsOffset, tags,
                known: (guards[0].Row, keys => keys.Any(key => DatAnchors.IdOf(tags, key) == "ranged"))));
        if (!search.Report(Step))
        {
            return 1;
        }

        var rows = new List<Monster>();
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var id = monsters.String(row, DatColumnSearch.IdColumn);
            var experience = monsters.Int32(row, columns.Experience);
            if (id.Length == 0 || experience <= 0)
            {
                continue;
            }

            var tagIds = monsters.KeyList(row, columns.Tags).Select(key => DatAnchors.IdOf(tags, key)).ToList();
            rows.Add(new Monster(
                id,
                experience,
                monsters.String(row, columns.Name),
                DatAnchors.IdOf(itemClasses, monsters.Key(row, columns.MainHand)) ?? string.Empty,
                tagIds.Contains("ranged") ? "ranged" : tagIds.Contains("melee") ? "melee" : string.Empty));
        }

        // A variety is told when monsters of one name differ in what a corpse of them asks or does.
        var told = rows.Where(monster => monster.Name.Length > 0).GroupBy(monster => monster.Name, StringComparer.Ordinal)
            .Where(group => group.Select(monster => (monster.Experience, monster.Weapon, monster.Attack)).Distinct().Count() > 1)
            .SelectMany(group => group.Select(monster => monster.Id)).ToHashSet(StringComparer.Ordinal);

        var spectres = new SortedDictionary<string, SortedDictionary<string, object>>(StringComparer.Ordinal);
        foreach (var monster in rows)
        {
            var entry = new SortedDictionary<string, object>(StringComparer.Ordinal) { ["x"] = monster.Experience };
            if (told.Contains(monster.Id))
            {
                entry["t"] = true;
                if (monster.Weapon.Length > 0)
                {
                    entry["w"] = monster.Weapon;
                }

                if (monster.Attack.Length > 0)
                {
                    entry["a"] = monster.Attack;
                }
            }

            spectres[monster.Id] = entry;
        }

        if (!KnownRowsHold(rows, told))
        {
            return 1;
        }

        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["spectres"] = spectres });
        Say.Out.WriteLine($"Wrote {outFile}: {spectres.Count} monsters with an experience multiplier of {monsters.RowCount} rows, {told.Count} of a name shared with another variety");
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

    private static bool KnownRowsHold(List<Monster> rows, HashSet<string> told)
    {
        var ok = true;
        void Holds(string what, bool holds)
        {
            if (!holds)
            {
                Say.Out.WriteLine($"  {what}: {Step} keeps its previous catalog");
                ok = false;
            }
        }

        foreach (var (id, experience) in Known)
        {
            var found = rows.FirstOrDefault(monster => monster.Id == id);
            Holds($"{id} should hold an experience multiplier of {experience}, found {(found == null ? "no row" : found.Experience.ToString())}", found?.Experience == experience);
        }

        var mortar = rows.FirstOrDefault(monster => monster.Id == MortarGuard);
        var bow = rows.FirstOrDefault(monster => monster.Id == BowGuard);
        var spear = rows.FirstOrDefault(monster => monster.Id == SpearGuard);
        Holds($"the three Vaal Guards should share the name {GuardName}", new[] { mortar, bow, spear }.All(guard => guard?.Name == GuardName));
        Holds($"the grenade guard should hold no weapon and fight ranged, found {mortar?.Weapon} / {mortar?.Attack}", mortar is { Weapon: "", Attack: "ranged" });
        Holds($"the bow guard should hold a {BowClass} and fight ranged, found {bow?.Weapon} / {bow?.Attack}", bow is { Weapon: BowClass, Attack: "ranged" });
        Holds($"the spear guard should hold a {SpearClass} and fight melee, found {spear?.Weapon} / {spear?.Attack}", spear is { Weapon: SpearClass, Attack: "melee" });
        Holds("each guard's variety should be told", new[] { MortarGuard, BowGuard, SpearGuard }.All(told.Contains));
        return ok;
    }
}
