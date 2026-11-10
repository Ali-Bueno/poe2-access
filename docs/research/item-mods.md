# Item modifiers from memory against the tooltip (0.5.5f, 2026-11-10)

Groundwork for saying or comparing an item's modifiers without its tooltip (a whole grid read at once, the Build
Planner's gear on items never focused). Read-only; nothing is extracted or spoken from these fields yet.

## What was read

- Component `Mods` of an item entity, the host's `GameReader/RemoteObjects/Components/Mods.cs` (GameHelper2 v2.8.0's
  model, vendored): `ModsOffsets.Details0` = `ModsAndObjectMagicProperties`
  (`host/GameOffsets/Objects/Components/ModsAndObjectMagicProperties.cs`): rarity int at `+0x94`, `AllModsType` at
  `+0xA0` = five `StdVector` of `ModArrayStruct` in the order implicit, explicit, enchant, hellscape, crucible.
- `ModArrayStruct` (0x40 bytes): `StdVector Values` at `+0x00` (ints), `Value0` at `+0x18`, the Mods.dat row pointer at
  `+0x28`; the mod's id is the string its row begins with (`ObjectMagicProperties.GetModName`), the values come from
  `GetValue` (none: NaN; one; two).
- Provenance of the offsets: GameHelper2's, unchanged; this mod had only used the rarity (`InventoryReader.Describe`).
- Method: dev command `itemmods` (ResearchMode) logs `InventoryReader.ModsReport` of the focused item beside the lines of
  its tooltip; yarkey, Act 2 town, Spanish client. Raw lines:
  [evidence](evidence/item-mods-2026-11-10.txt).

## Result: the fields hold on items

MEASURED on nine items (seven with a `Mods` component). Every modifier line of every tooltip was accounted for by a list entry, with the same number
once the unit is known:

| Item | Lists | Against the tooltip |
|---|---|---|
| Cetro de augurio, rare (`Sceptres/FourSceptre4`, bag) | explicit 5: `NearbyAlliesLifeRegeneration4` 352, `ReducedLocalAttributeRequirements1` -15, `NearbyAlliesCriticalStrikeChance1` 13, `NearbyAlliesAddedColdDamage3` 6/10, `IncreasedMana1` 10 | 5 of 5: "regeneran 5.9 de vida por segundo" (352 per minute), "reducidos un 15%", "13%", "de 6 a 10", "+10 al maná" |
| Runa de piedra menor (`SoulCores/RuneStunLesser`, bag) | no `Mods` component | its lines are the rune's own text, not modifiers |
| Esencia de incombustibilidad menor (`Currency/CurrencyLesserEssenceFireResist`, bag) | no `Mods` component | the same |
| Frasco de vida grandioso, magic (`Flasks/FourFlaskLife4`, belt) | explicit 2: `FlaskIncreasedRecoveryAmount2_` 46, `FlaskChargesUsed2` -19 | 2 of 2 |
| Vial de piedra, magic (`Flasks/FourCharm6`, belt) | implicit 1: `CharmImplicitUseOnStun1` 1; explicit 1: `FlaskChanceRechargeOnKill1` 25 | "Se usa cuando te aturden"; "25% de probabilidad de ganar una carga…" |
| Frasco de maná mayor, magic (`Flasks/FourFlaskMana3`, belt) | explicit 2: `FlaskChargesUsed2` -20, `FlaskIncreasedRecoveryAmount1` 45 | 2 of 2 |
| Escarpes de malla, rare (`Boots/FourBootsStrDex1`, worn) | explicit 4: `IncreasedMana2` 21, `LifeRegeneration1` 104, `ColdResist1` 8, `MovementVelocity1` 10 | 4 of 4: "Regeneras 1.7 de vida por segundo" (104 per minute) |
| Cinturón de cuero crudo, unique (`Belts/FourBelt1`, worn) | implicit 2: `BeltImplicitFlaskLifeRecovery1` 27, `BeltImplicitCharmSlots3` 1; explicit 3: `UniqueDamageTakenGoesToMana1` 50, `UniqueIncreasedMana19` 48, `UniqueAllResistances9` 9 | 5 of 5 (the two implicits share one tooltip line) |
| Manoplas remachadas, rare (`Gloves/FourGlovesStr2`, worn) | explicit 5: `CriticalMultiplier2` 15, `IncreasedMana3` 33, `IncreasedLife3` 32, `LifeGainPerTarget1` 2, `LocalIncreasedArmourAndLife2` 16/15 | 5 tooltip lines from 5 mods, **not one to one**: "+47 a la vida máxima" is 32 + 15 |

Enchant lists were empty on all seven (none of them is enchanted): that list is unproven.

## What does not match one to one

- **The tooltip adds up a stat given by several modifiers** (life 32 from `IncreasedLife3` and 15 from the hybrid
  `LocalIncreasedArmourAndLife2`, shown as +47). The list's entries are modifiers, the tooltip's lines are stats.
- **A hybrid modifier's two values are two stats** (armour % and life), not a range; a range is also two values
  (`NearbyAlliesAddedColdDamage3` 6/10). Which it is comes from the modifier's stats, not from the entry.
- **Units**: regeneration is stored per minute (352 → 5.9 per second, 104 → 1.7).
- **Signs**: "reduced" is a negative value of an "increased" stat (-15, -19).
- Lines that are no modifier: the base's properties (armour, spirit, block), requirements, a granted skill ("Otorga la
  habilidad: Malicia de nivel 7"), the flavour text, a rune's or an essence's own text.
- Entries carry a Mods.dat id ("IncreasedLife3"), not a text: `catalog/mod-texts.json` is keyed by wording.

## What saying them would need (not started)

An extraction Mods.dat id → its stat ids and value ranges → the stat's description text (the game's stat description
files), the per-minute and negated forms applied as the description says, and the same sums per stat the tooltip makes;
then a check of the result against the tooltip on a set like the one above, enchanted and corrupted items included.
`ModsAndObjectMagicProperties.StatsFromMods` (`+0x148`, the summed stats) may give the tooltip's sums directly and was
not read here.
