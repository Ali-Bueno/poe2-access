using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     The columns the `item-bases` catalog is read through (BaseItemTypes, ItemClasses, ArmourTypes), each found near
///     where it was measured (dat-kinds, 0.5.x, 2026-11-10) by its kind and by what the rows of known bases hold there,
///     values the game's own tooltips showed (docs/research/file-catalogs.md, "Item bases"). Pure.
/// </summary>
internal sealed class ItemBaseColumns
{
    internal const string Step = "item-bases";

    // Measured offsets, where each search starts.
    internal const int ClassOffset = 0x8, LevelOffset = 0x30;
    internal const int ArmourBaseOffset = 0x0, ArmourOffset = 0x10, EvasionOffset = 0x14, EnergyShieldOffset = 0x18;

    /// <summary>No base asks for a level past the game's last one (ExperienceLevels has 100 rows).</summary>
    internal const int MaxLevel = 100;

    // Rows whose values are known: War Spear, Ridged Buckler, Soldier Greathelm and Linen Wraps.
    internal const string KnownSpear = "Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5";
    internal const string KnownSpearClass = "Spear";
    internal const int KnownSpearLevel = 21;
    internal const string KnownBuckler = "Metadata/Items/Armours/Shields/FourShieldDex5";
    internal const int KnownBucklerLevel = 22, KnownBucklerEvasion = 55;
    internal const string KnownHelmet = "Metadata/Items/Armours/Helmets/FourHelmetStr2";
    internal const int KnownHelmetLevel = 12, KnownHelmetArmour = 80;
    internal const string KnownGloves = "Metadata/Items/Armours/Gloves/FourGlovesDexInt2";
    internal const int KnownGlovesEvasion = 23, KnownGlovesEnergyShield = 10;

    /// <summary>
    ///     The ItemClasses Ids of what a character wears or wields: weapons, off-hands, armour pieces, jewellery, flasks and
    ///     charms (UtilityFlask is the charms' class). A class a patch adds is not in the catalog until it is named here.
    /// </summary>
    internal static readonly IReadOnlySet<string> Equippable = new HashSet<string>(StringComparer.Ordinal)
    {
        "Claw", "Dagger", "Wand", "One Hand Sword", "One Hand Axe", "One Hand Mace", "Sceptre", "Spear", "Flail",
        "Bow", "Staff", "Two Hand Sword", "Two Hand Axe", "Two Hand Mace", "Warstaff", "Crossbow", "Talisman", "TrapTool", "FishingRod",
        "Quiver", "Shield", "Buckler", "Focus",
        "Helmet", "Body Armour", "Gloves", "Boots",
        "Amulet", "Ring", "Belt",
        "LifeFlask", "ManaFlask", "UtilityFlask",
    };

    /// <summary>The tables the columns are in.</summary>
    public sealed record Tables(DatTable Items, DatTable Classes, DatTable Armours);

    public int ItemName, ItemClass, Level, ArmourBase, Armour, Evasion, EnergyShield;

    /// <summary>The columns, or null (said) when one was not found; the name column is found by its own anchor.</summary>
    public static ItemBaseColumns? Find(Tables t, int itemName)
    {
        var search = new DatColumnSearch();
        int Item(string id) => DatAnchors.Row(t.Items, id, "the item bases' columns");
        var (spear, buckler, helmet, gloves) = (Item(KnownSpear), Item(KnownBuckler), Item(KnownHelmet), Item(KnownGloves));
        var spearClass = DatAnchors.Row(t.Classes, KnownSpearClass, "BaseItemTypes' item class");
        var itemClass = search.Key(t.Items, "BaseItemTypes.ItemClass", ClassOffset, t.Classes, known: (spearClass < 0 ? -1 : spear, key => key == spearClass));
        var level = search.Find(t.Items, "BaseItemTypes.Level", LevelOffset, offset =>
            offset + sizeof(int) <= t.Items.RowSize && Enumerable.Range(0, t.Items.RowCount).All(row => t.Items.Int32(row, offset) is >= 0 and <= MaxLevel) &&
            Holds(t.Items, offset, (spear, KnownSpearLevel), (buckler, KnownBucklerLevel), (helmet, KnownHelmetLevel)));

        var armourBase = search.Find(t.Armours, "ArmourTypes.BaseItemType", ArmourBaseOffset, offset =>
            DatColumnSearch.IsKey(t.Armours, offset, t.Items.RowCount) && (helmet < 0 || ArmourOf(t.Armours, offset, helmet) >= 0));
        if (armourBase < 0)
        {
            search.Report(Step);
            return null;
        }

        var (helmetArmour, bucklerArmour, glovesArmour) = (ArmourOf(t.Armours, armourBase, helmet), ArmourOf(t.Armours, armourBase, buckler), ArmourOf(t.Armours, armourBase, gloves));
        int Defence(string name, int measured, int after, int onHelmet, int onBuckler, int onGloves) =>
            search.Find(t.Armours, name, measured, offset =>
                offset + sizeof(int) <= t.Armours.RowSize && Enumerable.Range(0, t.Armours.RowCount).All(row => t.Armours.Int32(row, offset) >= 0) &&
                Holds(t.Armours, offset, (helmetArmour, onHelmet), (bucklerArmour, onBuckler), (glovesArmour, onGloves)), after);
        var armour = Defence("ArmourTypes.Armour", ArmourOffset, armourBase, KnownHelmetArmour, 0, 0);
        var evasion = Defence("ArmourTypes.Evasion", EvasionOffset, armour, 0, KnownBucklerEvasion, KnownGlovesEvasion);
        var energyShield = Defence("ArmourTypes.EnergyShield", EnergyShieldOffset, evasion, 0, 0, KnownGlovesEnergyShield);
        return !search.Report(Step) ? null : new ItemBaseColumns
        {
            ItemName = itemName, ItemClass = itemClass, Level = level, ArmourBase = armourBase, Armour = armour, Evasion = evasion, EnergyShield = energyShield,
        };
    }

    /// <summary>Whether every known row the table has holds its known number at <paramref name="offset"/>.</summary>
    private static bool Holds(DatTable table, int offset, params (int Row, int Value)[] known) =>
        known.All(check => check.Row < 0 || table.Int32(check.Row, offset) == check.Value);

    /// <summary>The ArmourTypes row whose key at <paramref name="offset"/> is the BaseItemTypes row <paramref name="item"/>, or -1.</summary>
    internal static int ArmourOf(DatTable armours, int offset, int item)
    {
        using var probe = DatTable.Probe();
        return item < 0 ? -1 : Enumerable.Range(0, armours.RowCount).FirstOrDefault(row => armours.Key(row, offset) == item, -1);
    }
}
