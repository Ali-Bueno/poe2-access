using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     The NPCs who take part in a boss's fight (Asala in Jamanra's): an NPC monster granted a skill of the boss's skill family
///     ("EGPerennialKingAsalaStun" is of the PerennialKing family), a family few monster folders share (docs/research/boss-mechanics.md §14).
/// </summary>
internal sealed class BossFamilies
{
    /// <summary>A family shared by more monster folders than this is a common one (melee, generic slams), not the boss's (the census's cap).</summary>
    internal const int MaxOwners = 4;

    private const string NpcFolder = "/npc/";
    private static readonly Regex Prefix = new("^[A-Z0-9]+(?=[A-Z][a-z])");
    private static readonly Regex Word = new("[A-Z][a-z0-9]+");
    private static readonly Regex SharedOwner = new("/(npc|invisiblefire)$");
    private static readonly HashSet<string> Unnamed = new(StringComparer.Ordinal) { string.Empty, "Invisible" };

    private readonly Dictionary<string, HashSet<string>> owners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<int>> npcs = new(StringComparer.Ordinal);
    private readonly DatTable monsters;
    private readonly DatTable skills;
    private readonly BossColumns c;

    public BossFamilies(DatTable monsters, DatTable skills, BossColumns columns)
    {
        (this.monsters, this.skills, this.c) = (monsters, skills, columns);
        for (var row = 0; row < monsters.RowCount; row++)
        {
            var id = monsters.String(row, 0);
            var top = string.Join('/', (monsters.StringOrNull(row, columns.Script) is { Length: > 0 } script ? script : id).Split('/').Take(3)).ToLowerInvariant();
            var npc = monsters.Byte(row, columns.BossBar) != 1 && id.Contains(NpcFolder, StringComparison.OrdinalIgnoreCase);
            foreach (var family in this.FamiliesOf(row))
            {
                Set(this.owners, family).Add(top);
                if (npc && !Set(this.npcs, family).Contains(row))
                {
                    this.npcs[family].Add(row);
                }
            }
        }
    }

    /// <summary>A skill id's family: its first two words after the kind's capitals ("GSPerennialKingSwordSlam" → PerennialKing), or null.</summary>
    internal static string? Of(string skill) =>
        Word.Matches(Prefix.Replace(skill, string.Empty)) is { Count: >= 2 } words ? words[0].Value + words[1].Value : null;

    /// <summary>The named NPCs sharing a boss's own skill families, by MonsterVarieties Id.</summary>
    public IEnumerable<string> Participants(int boss) =>
        this.FamiliesOf(boss).Where(family => this.owners[family].Count(owner => !SharedOwner.IsMatch(owner)) <= MaxOwners)
            .SelectMany(family => this.npcs.GetValueOrDefault(family) ?? new List<int>()).Where(row => row != boss).Distinct()
            .Where(row => !Unnamed.Contains(this.monsters.StringOrNull(row, this.c.Name) ?? string.Empty)).Select(row => this.monsters.String(row, 0));

    private IEnumerable<string> FamiliesOf(int monster) =>
        this.monsters.KeyList(monster, this.c.MonsterSkills).Select(key => DatAnchors.IdOf(this.skills, key)).OfType<string>().Select(Of).OfType<string>().Distinct(StringComparer.Ordinal);

    private static HashSet<string> Set(Dictionary<string, HashSet<string>> sets, string key) => sets.TryGetValue(key, out var set) ? set : sets[key] = new HashSet<string>(StringComparer.Ordinal);

    private static List<int> Set(Dictionary<string, List<int>> lists, string key) => lists.TryGetValue(key, out var list) ? list : lists[key] = new List<int>();
}
