# Item modifiers from memory, without the tooltip (0.5.5f, 2026-11-10)

An item's modifier lines are written from the modifiers it holds in memory and two catalogs made from the game's
files, so a worn piece is compared with the Build Planner's build before its slot was ever focused. Read-only. What is
proven, what is only built, and what the live game must still show are kept apart below.

| Part | Where | State |
|---|---|---|
| The `Mods` lists of an item | host `GameReader/RemoteObjects/Components/Mods.cs` | MEASURED on seven items against their tooltips |
| Modifier → stats and rolls | `tools/Poe2UiExtract` `mod-stats` → `catalog/mod-stats.json` | from the files; the 22 modifiers seen live read as their tooltips |
| Stats → line | `catalog/mod-texts.json` (unchanged), `ModTextCatalog.Show` | from the files; 25 lines equal the Spanish tooltips letter for letter |
| The lines of an item | plugin `ItemModLines.Render` (pure) | unit tests on the evidence file |
| Use in the build's gear | `UiSemantics.WornItemOf`, `BuildGear.LinesFromMemory` | code and unit tests; **not run live** |
| Live check | dev command `itemmodscheck`, log line `Item mods: … differ` | **to run**, see [Live check](#live-check) |

## The fields in memory

- Component `Mods` of an item entity (GameHelper2 v2.8.0's model, vendored unchanged): `ModsOffsets.Details0` =
  `ModsAndObjectMagicProperties` (`host/GameOffsets/Objects/Components/ModsAndObjectMagicProperties.cs`): rarity int at
  `+0x94`, `AllModsType` at `+0xA0` = five `StdVector` of `ModArrayStruct` in the order implicit, explicit, enchant,
  hellscape, crucible.
- `ModArrayStruct` (0x40 bytes): `StdVector Values` at `+0x00` (int32s), `Value0` at `+0x18`, the Mods.dat row pointer
  at `+0x28`. The modifier's Id is the string its row begins with (`ObjectMagicProperties.GetModName`).
- **Provenance:** GameHelper2's offsets; this mod had only used the rarity until 2026-11-10. **Validation:** dev command
  `itemmods` on nine items (seven with the component), yarkey, Act 2 town, Spanish client
  ([raw lines](evidence/item-mods-2026-11-10.txt)): every modifier line of every tooltip was accounted for, and each of
  the 22 modifiers' values lies inside the range the game's files give its stat
  ([the catalog's entries](evidence/item-mods-catalog-2026-11-10.txt)) — a second, independent agreement of the value
  fields with the files.
- The host kept two values a modifier. `Mods.ImplicitModValues` … `HellscapeModValues` now keep all of them
  (`ObjectMagicProperties.GetValues`: none, `Value0` for one, the vector for more — the same reads as before) and keep an
  entry without a row under an empty name, so nothing an item holds is dropped unseen. `itemmods` logs every value
  (`Id 16/15`, `Id -` for none) and a fourth list, `other` (hellscape and crucible).

| Item | Lists | Against the tooltip |
|---|---|---|
| Cetro de augurio, rare (`Sceptres/FourSceptre4`, bag) | explicit 5 | 5 of 5: "regeneran 5.9 de vida por segundo" (352 a minute), "reducidos un 15%" (-15), "de 6 a 10" (6/10) |
| Runa de piedra menor, Esencia de incombustibilidad menor (bag) | no `Mods` component | their lines are their own text, not modifiers |
| Frasco de vida grandioso, magic (belt) | explicit 2 | 2 of 2 |
| Vial de piedra, magic (belt) | implicit 1, explicit 1 | 2 of 2; "No te pueden aturdir" is the charm's own effect, in no list |
| Frasco de maná mayor, magic (belt) | explicit 2 | 2 of 2 |
| Escarpes de malla, rare (worn) | explicit 4 | 4 of 4: "Regeneras 1.7 de vida por segundo" (104 a minute) |
| Cinturón de cuero crudo, unique (worn) | implicit 2, explicit 3 | 5 of 5; the two implicit lines are one text of the tooltip |
| Manoplas remachadas, rare (worn) | explicit 5 | 5 lines from 5 modifiers, **not one to one**: "+47 a la vida máxima" is 32 + 15 |

Enchant, hellscape and crucible lists were empty on all seven: **unproven**, so a line made from them is never certain.

## Mods.dat: which value is which stat

`Poe2UiExtract dat-kinds <game> mods english <Id>` and the new `dat-keys <game> mods stats` (every offset that reads as
a 16-byte key into another table), 0.5.x files: Mods has 16,911 rows of 0x2B5 bytes, Stats 27,299 rows.

| Column | Where | How it was proven |
|---|---|---|
| Stat 1 to 4 | +0x1E, +0x2E, +0x3E, +0x4E, 16-byte keys into Stats | `IncreasedLife3` → `base_maximum_life` (its line read "+… a la vida máxima"); `LocalIncreasedArmourAndLife2` → `local_physical_damage_reduction_rating_+%`, then `base_maximum_life` (16 → "Armadura aumentada un 16%", 15 → part of "+47 a la vida máxima"). The run ends where a column is no key into Stats (+0x5E, a number and a text) |
| Least and most of each | +0x7E, four pairs of int32 | the hybrid's 14–20 and 11–19 hold its rolls 16 and 15; all 22 live values lie in their stat's pair. An empty stat column has 0, 0 on all but 78 rows |
| Other keys into Stats | +0x12E (771 rows filled), +0x196 (476), +0x1DF (96), +0x1EF (42), +0x25C (265) | by shape only. The first four hold further stats of one modifier (`HandWrapsUniqueBaseDamageOverrideForMaceAttacks1`: three `…facebreaker…` stats at +0x4E, +0x12E, +0x196); +0x25C is not understood |

A modifier's values follow its stat columns: value 1 is stat 1, value 2 is stat 2 (the range `NearbyAlliesAddedColdDamage3`
6/10 = minimum and maximum, the hybrid 16/15 = armour and life). Proven for two values; three and four are the same
run of columns and no item with one was seen. Where the values of a stat in another column go is **not known**: a
modifier with one, with a stat after an empty column, or with a roll at an empty column is written as *unproven* and
never gives a certain line.

## The catalog

`catalog/mod-stats.json` (`Poe2UiExtract mod-stats`, launcher step "mod stats", 1.7 MB):

```
"hidden":   [ "level", … ]                      24 stats the description file marks no_description
"mods":     { "LocalIncreasedArmourAndLife2": "local_physical_damage_reduction_rating_+% 14 20 base_maximum_life 11 19" }   15,851
"unproven": { "HandWrapsUniqueBaseDamageOverrideForMaceAttacks1": "stat stat stat" }                                          1,060
```

- **The texts are not copied.** `mod-texts.json` already holds `stat_descriptions.csd` whole, each block with its stat
  Ids; the plugin now indexes it by stat (`ModTextCatalog.BlocksOf`), so the new catalog holds only what Mods.dat adds.
  It is not cut to item modifiers: which rows are an item's is a column (the domain) nobody has proven, and the rest costs
  1 MB. `mod-texts.json` could now be cut to the 4,038 stats the modifiers use; it was left whole because the build
  plans' lines are read through it too.
- The step fails, and the launcher keeps the previous catalog, when: the description file does not read; a handler is
  not in `StatHandlers`; a column is not found; `IncreasedLife3` or the hybrid no longer reads as its stats with the
  rolls seen live inside their ranges. Columns are found by kind (`DatColumnSearch`, the `DatColumnKinds` judge as second
  opinion) near the measured offsets, so a patch that moves them is followed.
- Of the 5,545 stats the modifiers use, 4,038 have a block in `stat_descriptions.csd`, 14 are hidden, 1,494 are not
  mentioned there (monster, map and skill stats, described in other files).

## Transforms

The 44 handler names of `stat_descriptions.csd` (0.5.x), all in `StatHandlers` (shared by the plugin and the extractor):

| Kind | Names | Done |
|---|---|---|
| Arithmetic, applied | `negate`, `negate_and_double`, `double`, `times_twenty`, `add_one`, `subtract_one`, `plus_two_hundred`, `divide_by_one_hundred` (and `_1dp`, `_2dp`, `_2dp_if_required`), `divide_by_one_hundred_and_negate`, `per_minute_to_per_second` (and `_0dp`, `_1dp`, `_2dp`, `_2dp_if_required`), `milliseconds_to_seconds` (and `_0dp`, `_1dp`, `_2dp`, `_2dp_if_required`), `deciseconds_to_seconds`, `divide_by_ten_0dp`, `_1dp`, `_1dp_if_required`, `divide_by_two_0dp`, `divide_by_three`, `divide_by_four`, `divide_by_five`, `divide_by_fifteen_0dp`, `divide_by_fifty` | shown = value × factor + offset, as the name says |
| Arithmetic, never certain | `divide_by_twenty_then_double_0dp` | applied as ÷10; what is rounded between its two steps is not known |
| Marks | `canonical_line`, `canonical_stat` | no number changes |
| Look-ups, not applied | `affliction_reward_type`, `display_indexable_support`, `mages_legacy_index`, `mod_value_to_item_class`, `passive_hash`, `passive_keystone_index`, `specific_skill`, `tree_expansion_jewel_passive`, `ultimatum_wager_type_hash` | they show a name found by the value: the line is not written and is uncertain |

Seen live: `negate` (-15 → "reducidos un 15%", -19 and -20 on the flasks) and `per_minute_to_per_second` without a
decimals part (352 → "5.9", 104 → "1.7": one decimal, rounded). Every other one is applied as its name reads.

### Numbers

A line is **certain** only when every number in it is written a way a tooltip was seen to write one
(`ModTextVariation.Show`). Not seen, so the line is made but uncertain:

- a number of 1,000 or more (grouped digits?), a number shown below zero (which minus sign?);
- a fraction where no handler states the decimals (`divide_by_one_hundred`, `milliseconds_to_seconds`: 2.5 or 2.50?),
  or where it states none (`_0dp`: rounded or cut?);
- a whole number under decimals that are always shown (`per_minute_to_per_second` at 120: "2" or "2.0"?; `_1dp`,
  `_2dp`), and a last decimal of zero under them; `_if_required` drops it, as its name says;
- an exact half at the last decimal (rounded up or to even?);
- a fraction in a language other than English and Spanish (the decimal mark was seen only as "5.9" in Spanish).

## The lines of an item

`ItemModLines.Render(lists, language, ModStatCatalog, ModTextCatalog)` → per line its text, whether it is certain and
what it was made from:

1. Each list apart (implicit, explicit, enchant, other): a tooltip shows them apart. Within a list every modifier's
   values are added to their stats — this is the tooltip's sum (32 + 15 = 47).
2. Each stat's block of `stat_descriptions.csd`, with the sums of all the block's stats (a stat the item lacks is 0); a
   block whose stats are all 0 has no line. The first line of the language whose conditions the values fit is written,
   with its handlers. Blocks come in the file's order: on the seven items that is the tooltip's own order.
3. Uncertain, never said and never held against the item: a modifier the catalog lacks (every line of its list, since
   it may add to any), a modifier with other than a value a stat or marked unproven (the lines of its stats), a value
   outside what its stat rolls, a stat with no block or with two (26 stats are in two blocks), a line before the fitting
   one that cannot be read (a look-up, `table_only`, a `{0:-d}` place), no fitting line, no text in the language, a
   number as above, and anything from the enchant and other lists. A stat marked `no_description` gives no line and no
   doubt.

**Result on the evidence** (`ItemModTests.TheComparedItemsReadAsTheirTooltips`, the evidence file against the shipped
catalogs): 7 items, 25 lines, **25 equal to the Spanish tooltip letter for letter, 0 uncertain, 0 wrong**, in the
tooltip's order.

Over the whole catalog (each of the 15,457 proven modifiers with a stat, at its least and at its most roll, Spanish):
22,529 of 30,914 rolls have every line certain. The lines in doubt: 8,800 for a stat `stat_descriptions.csd` does not
describe (monster, map and skill modifiers among them: the catalog does not tell an item's modifiers from those), 676
for a number written a way not seen, 124 with no line that could be written, 108 for a stat in two blocks.

## What the gear feature does with them

- A worn item's lines are its tooltip's once its slot was focused in the session, else the certain lines made from
  memory (`UiSemantics.WornItemOf`). So the inventory's build summary counts every worn piece at the first opening,
  "Más lejos de la build" weighs them all, and an item under the cursor is said "más/menos que el puesto" at once.
- An item with a line in doubt is *partial* (`GearItem.LinesPartial`): the wanted modifiers found among its certain
  lines count, the others are "not compared", never "missing". An item whose every line is in doubt is as before: not read.
- The item under the cursor is always judged by its own tooltip, as before. Nothing new is spoken.
- Each tooltip read is set against the lines made from memory once (`UiSemantics.CheckItemMods`); a difference is
  logged: `Item mods: <item> memory and tooltip differ: memory only: …; tooltip only: …; uncertain: …`.

## Live check

Not run: the game was in use by another session while this was built. Before the feature is trusted:

1. Start the game with the build; the log must say `Modifier stat catalog: 15851 modifiers, 1060 not told apart, 24
   hidden stats from the game files`.
2. Open the inventory and rest the cursor on every worn slot and every bag item once (a tooltip exists only after
   that), then send the dev command `itemmodscheck`. It logs, per worn and bag item,
   `Item mods check: <name> (<inventory> x,y): n lines equal, m differ, k uncertain` with the differing lines, or
   `tooltip not read yet` with the lines made from memory, then the totals. Nothing is spoken.
3. **Must be seen:** `0 differ` on every item with modifiers; `memory only` never (a line memory makes that the
   tooltip does not show would be a false claim). A `tooltip only` line is fine when it is the base's own (a charm's
   "No te pueden aturdir") and a finding when it is a modifier.
4. **Items to look for,** each answers a question the nine did not:
   - a piece with a **socketed rune or soul core**: are the rune's lines in a list (enchant?) or only in the tooltip?
     If only there, a worn piece with runes would be said to lack what its rune gives until its slot is focused;
   - an **enchanted**, a **corrupted** and a **quality** (catalyst) item: is the enchant list filled, and are the
     stored values the ones shown?
   - a ring or amulet whose **implicit and an explicit give the same stat**: two lines, or one sum?
   - a modifier of **three values or more**, and one logged as `its values are not told apart`;
   - `uncertain: "…" (…) as the tooltip shows it`: each such line is evidence to make that case certain (a regeneration
     of 120 a minute, a number over 999, a negative resistance, a leech fraction);
   - the client in another language for the decimal mark.
5. Then the feature itself: with no slot focused since the game started, open the inventory — the summary must read
   "leídos en N de N piezas" with N the worn pieces, and focusing each slot afterwards must not change its counts.

## Not done

- `ModsAndObjectMagicProperties.StatsFromMods` (`+0x148`, the summed stats) was not read: it may answer the rune and
  quality questions directly.
- A whole grid said at once stays removed at the owner's word; the lines of items in the bag or a stash are made only
  for the log.
