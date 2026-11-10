/// <summary>
///     A row of an English table whose display name is stable, to tell the table's name column by its value and not by
///     which column a translation changes. Read from the 0.5.x files on 2026-11-10 (dat-row-strings).
/// </summary>
/// <param name="Measured">Where the column was measured; the search starts there.</param>
internal sealed record NameAnchor(string Table, string Column, int Measured, string Id, string Name)
{
    public static readonly NameAnchor Mods = new("mods", "Mods.Name", 0x62, "Strength1", "of the Brute");
    public static readonly NameAnchor Passives = new("passiveskills", "PassiveSkills.Name", 0x32, "passive_keystone_resolute_technique", "Resolute Technique");
    public static readonly NameAnchor ActiveSkills = new("activeskills", "ActiveSkills.DisplayedName", 0x8, "fireball", "Fireball");
    public static readonly NameAnchor MinimapIcons = new("minimapicons", "MinimapIcons.Label", 0x1A, "Waypoint", "Waypoint");
    public static readonly NameAnchor Characters = new("characters", "Characters.Name", 0x8, "Metadata/Characters/Str/StrFourb", "Warrior");

    /// <summary>The first area of the campaign (its Id is the stable part, as the Endgame and map-pin searches use it).</summary>
    public static readonly NameAnchor Areas = new("worldareas", "WorldAreas.Name", 0x8, "G1_1", "The Riverbank");

    public static readonly NameAnchor Items = new("baseitemtypes", "BaseItemTypes.Name", 0x20, "Metadata/Items/Currency/CurrencyIdentification", "Scroll of Wisdom");
    public static readonly NameAnchor Monsters = new("monstervarieties", "MonsterVarieties.Name", 0x110, BossColumns.KnownBoss, BossColumns.KnownBossName);
    public static readonly NameAnchor Npcs = new("npcs", "NPCs.Name", 0x8, "Metadata/NPC/Oba", "Oba");

    /// <summary>The name columns `dat-columns` writes.</summary>
    public static readonly NameAnchor[] Written = { Mods, Passives, ActiveSkills, MinimapIcons, Characters, Areas };
}
