using System;
using System.Linq;

/// <summary>
///     The columns `boss-mechanics` reads, each found by what it holds near where it was measured (<see cref="DatColumnSearch"/>):
///     MonsterVarieties' script, name and boss-bar flag, BuffDefinitions' stats, NPCTextAudio's text. Ids (text at +0) are read in place.
/// </summary>
internal sealed class BossColumns
{
    // Measured offsets (dat-schema poe2, row sizes checked on the 2026-11-06 files): MonsterVarieties 0x406 rows, NPCTextAudio 0x8F.
    internal const int ScriptOffset = 0x48;
    internal const int NameOffset = 0x110;
    internal const int BossBarOffset = 0x303;
    internal const int BuffStatsOffset = 0x1A;
    internal const int LineTextOffset = 0x18;
    internal const int SkillKindOffset = 0x4F;

    // Rows the data names: Count Geonor's wolf form has the boss bar, its summoned werewolf has none; god_mode makes one unhurtable.
    internal const string KnownBoss = "Metadata/Monsters/Baron/BaronBossCorruptedWolfForm";
    internal const string KnownBossScript = "Metadata/Monsters/Baron/BaronCorruptWolfForm";
    internal const string KnownBossName = "Geonor, the Putrid Wolf";
    internal const string KnownAdd = "Metadata/Monsters/Baron/BaronWerewolfSummon";
    internal const string KnownBuff = "god_mode";
    internal const string KnownStat = "base_cannot_be_damaged";
    internal const string KnownLine = "CorruptWolf_Skill_IceBreath_3";
    internal const string KnownLineText = "Desolation.";
    internal const string KnownSkill = "GSBaronBossColdBreathChannel";
    internal const string KnownSkillKind = "geometry_spell";

    public int Script, Name, BossBar, BuffStats, LineText, SkillKind;

    /// <summary>Every column, or null (said) when one was not found.</summary>
    public static BossColumns? Find(DatTable monsters, DatTable buffs, DatTable stats, DatTable lines, DatTable skills, DatTable kinds)
    {
        var search = new DatColumnSearch();
        var boss = DatColumnSearch.RowOf(monsters, KnownBoss);
        var add = DatColumnSearch.RowOf(monsters, KnownAdd);
        var buff = DatColumnSearch.RowOf(buffs, KnownBuff);
        var line = DatColumnSearch.RowOf(lines, KnownLine);
        var c = new BossColumns
        {
            Script = search.Text(monsters, "MonsterVarieties.BaseMonsterTypeIndex", ScriptOffset, text => text.Length == 0 || text.StartsWith("Metadata/", StringComparison.Ordinal),
                known: (boss, text => text == KnownBossScript)),
            Name = search.Text(monsters, "MonsterVarieties.Name", NameOffset, known: (boss, text => text == KnownBossName)),
            BossBar = search.Find(monsters, "MonsterVarieties.BossHealthBar", BossBarOffset, offset => IsFlag(monsters, offset) &&
                (boss < 0 || monsters.Byte(boss, offset) == 1) && (add < 0 || monsters.Byte(add, offset) == 0)),
            BuffStats = search.KeyList(buffs, "BuffDefinitions.Stats", BuffStatsOffset, stats,
                known: (buff, keys => keys.Any(key => key >= 0 && key < stats.RowCount && stats.StringOrNull((int)key, 0) == KnownStat))),
            LineText = search.Text(lines, "NPCTextAudio.Text", LineTextOffset, known: (line, text => text == KnownLineText)),
            SkillKind = search.Key(skills, "GrantedEffects.ActiveSkill", SkillKindOffset, kinds,
                known: (DatColumnSearch.RowOf(skills, KnownSkill), key => key >= 0 && key < kinds.RowCount && kinds.StringOrNull((int)key, 0) == KnownSkillKind)),
        };
        return search.Report("boss-mechanics") ? c : null;
    }

    /// <summary>A one-byte column holding only 0 and 1, both present.</summary>
    private static bool IsFlag(DatTable table, int offset)
    {
        bool zero = false, one = false;
        for (var row = 0; row < table.RowCount; row++)
        {
            var value = table.Byte(row, offset);
            if (value is not (0 or 1))
            {
                return false;
            }

            zero |= value == 0;
            one |= value == 1;
        }

        return zero && one;
    }
}
