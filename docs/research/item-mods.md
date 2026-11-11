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

Run on 2026-11-10; the results are under "Live check, 2026-11-10" below. The procedure:

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

## Live check, 2026-11-10

Run that evening on the patched 0.5.5f (the catalog loaded: `Modifier stat catalog: 15851 modifiers, 1060 not told
apart, 24 hidden stats from the game files`), on yarkey (Mercenary 23, Act 2 town) and malgdhler (Druid 8, Act 1
town) and, in a second short session, hairin (Huntress 4, Act 1 town); one pass of the cursor over each worn slot, then
dev `itemmodscheck`.

| Where | Items | Lines equal | Differ | Uncertain |
|---|---|---|---|---|
| yarkey, first run (as merged) | 16 | 57 | 2 | 4 |
| yarkey, after the three fixes below | 15 (the bag's sceptre not rested on again) | 54 | 1 | 2 |
| malgdhler | 10 | 27 | 0 | 0 |
| hairin (Huntress 4, Act 1 town), first run | 9 | 20 | 2 | 3 |
| hairin's belt, after the fourth fix below | 1 | 3 | 0 | 0 |
| stash tab 1, 12 items rested on (11 rare, 1 magic) | 12 | no `Item mods: … differ` line | 0 | not listed |

What is left on yarkey: the one "differ" is the charm's "No te pueden aturdir", the base's own line (tooltip only,
as expected); the two uncertain are the spears' `local_display_grants_spear_throw_skill`, which the description file
does not hold. `memory only` was never logged.

Found and fixed:
- **A line outside the description file made its whole piece "not compared".** Both spears carry the implicit that
  shows "Otorga la habilidad: Lanzamiento de lanza"; its stat has no block in `stat_descriptions.csd`, the line was in
  doubt and the piece partial, so with no slot focused the summary read "3 de 9 modificadores … Más lejos de la build:
  Segundo set de armas, mano secundaria" and after focusing "3 de 15 … Mano principal". Such a line cannot be a wanted
  modifier the file knows, and only those are ever said missing: it no longer makes the piece partial
  (`ItemModLine.OutsideTexts`, `BuildGear.LinesFromMemory`). Now "3 de 15 modificadores, leídos en 6 de 6 piezas. Más
  lejos de la build: Mano principal" before and after.
- **Hundredths.** "+1.4% a la probabilidad de impacto crítico" (`local_critical_strike_chance`, `divide_by_one_hundred`)
  was in doubt though the tooltip shows it so; a staff in the stash shows "+1.12%". A whole number of hundredths has no
  third decimal to round: written as it is, with up to two decimals and no zero after them (`StatHandlers.SeenExact`).
  The staff rested on again afterwards: no difference logged.
- **A half.** The unique ring's regeneration, 201 a minute, was written "3.4" (in doubt, as every half was) where the
  tooltip shows "Regeneras 3.3 de vida por segundo". Single precision holds 3.35 as 3.3499999, which also fits the two
  earlier readings (352 as 5.9, 104 as 1.7): under `per_minute_to_per_second` a half is now written as single precision
  rounds it and is certain (`StatHandlers.SeenTies`); a number that is a half in single precision too (2.25) stays in
  doubt, and so does a half under any other handler. One reading stands behind this.

- **A decimal of zero.** hairin's magic belt regenerates a little over 180 a minute: written "Regeneras 3.0 de vida por
  segundo" (in doubt) where the tooltip shows "Regeneras 3 de vida por segundo" (a pair of boots in the stash shows
  "Regeneras 2 …"). Under `per_minute_to_per_second` a zero decimal is dropped and the line is certain
  (`StatHandlers.SeenZeroDropped`). The belt rested on again: 3 lines equal, 0 differ.

Left as it is on hairin: her unique helm (Alas de Caelyn) shows "Cada punto de furia también otorga umbral de
aturdimiento aumentado un 1%", whose stat `stun_threshold_+%_per_rage` is in two blocks of the description file: in
doubt, never said and never held against the helm, as designed (which block is written is not known from one item);
her spear has the same skill grant as yarkey's.

The questions of step 4:
- **Quality:** the second set's spear has "Calidad: +8%" and its two modifier lines are equal: quality changes the
  base's own numbers, not the lists.
- **A socketed rune or soul core, an enchanted item, a corrupted item, one stat on an implicit and an explicit, a
  modifier of three values, a number over 999:** none at hand on either character or in stash tab 1. Open, each needing
  such an item rested on once (yarkey holds a Runa de piedra menor; socketing it is the owner's to do).
- **Another class:** malgdhler's staff (a unique), helm, gloves and ring (rare) and four magic pieces, all equal;
  hairin's nine pieces as in the table.
- The client in another language was not tried.

The feature itself (step 5): the summary counts every worn piece at the first opening and focusing the slots does not
change it (yarkey, above; malgdhler "3 de 7 piezas con su base, 3 de 13 modificadores, leídos en 6 de 7 piezas": the
seventh is the second set's talisman, whose lines are none).

## The item's own name, without the tooltip (2026-11-10)

A rare or unique item was named by its base wherever no tooltip had been read (a ground label, a move of an item the
cursor was never on, the first line of a focused slot whose tooltip came late). The name is in the item's Mods
component, as keys into Words.dat. No host struct names the field; it was found by reading the component of five worn
items of yarkey and three of malgdhler (0.5.5f, Spanish client; read-only reads through `tools/computer_use/peek.py`,
their output in [evidence](evidence/item-name-words-2026-11-10.txt)).

| Where | What | Seen |
|---|---|---|
| Mods component `+0x18`, `+0x20` | begin and end of a std::vector of 16-byte keys (row, file) | one key on a unique, two on a rare, an empty vector (0, 0) on a magic item |
| key `+0x08` | the file object; its name reads `Data/Balance/Words.dat` (the same shape `DatKey` checks everywhere) | all seven keys |
| Words row `+0x00` | Wordlist, int | 6 for a unique's name, 1 and 2 for a rare's two words |
| Words row `+0x04` | Text, English | "Cracklecreep", "Midnight Braid", "Chimeric", " Edge", "Fate", " Beads" |
| Words row `+0x30` | the text in the client's language, with gender branches | "Estallido repentino", "Trenza de medianoche", `<if:MS>{ quimérico}<elif:FS>{ quimérica}<elif:MP>{ quiméricos}<elif:FP>{ quiméricas}`, "Filo", " de destino", "Cuentas" |
| Words row `+0x38` | the inflection the row gives the other word | the gender alone on a singular noun ("F" on "Estrella", "Banda", "Zarpa"), both letters on a plural ("FP" on "Cuentas"), empty on a masculine singular ("Filo") and on the first words |
| row size | 0x40 (the next row's Text follows at `+0x44`) | "Matsya", "Corpse", " Frontier", "Spirit Link", "Foe", " Beak" |

The name (`ItemNameWords.Compose`, pure): a unique is its one word; a rare is its two words, the one that begins with a
space second ("Chimeric" + " Edge" in English, "Filo" + " quimérico" in Spanish: each language writes the space into
the word that goes second, so no language is assumed; the space may be a no-break one, as in " de kraken"), the
branch chosen by the inflection the noun's row gives ("F" stands for "FS": "Estrella quimérica"; the first reader took
the first branch and made "Estrella quimérico", caught on malgdhler's helm the same evening), the first branch when
none fits. Two words of which not exactly one begins with a space (a language written without
spaces), an empty word or markup left over give no name, and the base name is kept. A magic item's title holds its
affixes' words, which are in no Words key: it is still named by its base until its tooltip is read.

Validation at run time: each key must name Words.dat (`DatKey.Row`), at most two keys, and every tooltip title read is
compared with the name made (`ItemNameTrust`: given up, and reported as "item names", once two titles differ with
none or fewer in favour). Live, 2026-11-10: with no tooltip read since the mod started, dev `inventories` named the
eleven worn pieces by their own names (Manto de anochecer, Filo quimérico, Égida de glifo, Halo de cólera, Cuentas de
destino, Estallido repentino, Nudillo hipnótico, Agarraderas de necrófago, Paso de dominación, Trenza de medianoche;
the magic spear by its base); after each slot was focused once, dev `layout` said "item names from words ok (56 titles
agree, 0 differ)". On malgdhler (a Druid): "Estrella quimérica", "Banda de kraken", "Zarpa de apocalipsis", "Unión de
tierra, único", and "212 titles agree, 0 differ". Not seen: a ground label (no item lies on the ground in a town), an
unidentified rare (its vector is expected empty), a client in another language.

## Not done

- `ModsAndObjectMagicProperties.StatsFromMods` (`+0x148`, the summed stats) was not read: it may answer the rune and
  quality questions directly.
- A whole grid said at once stays removed at the owner's word; the lines of items in the bag or a stash are made only
  for the log.
