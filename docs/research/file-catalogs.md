# Static game data: from memory rows to file catalogs (2026-11-10)

The plugin reads two kinds of things from the game's memory: state (life, what is allocated, what is on screen) and
static data (a node's name, a skill's name, an area's level), which is the same for every player of one game build and
also sits in the game's files. A row offset in memory moves with a patch; a catalog made from the files at start-up
(`tools/Poe2UiExtract`, [how a column is found and judged](../compatibility.md#self-describing-tables)) cannot. This
note lists every static read, the file that holds the same fact, the key the plugin already has in hand, and what was
done. Offsets are those of the 0.5.x files; table layouts were read with `Poe2UiExtract dat-kinds`.

## Candidates

| Fact | Read from memory today (offsets it would retire or back up) | File and columns | Key in hand | Work | State |
|---|---|---|---|---|---|
| Passive node name, kind, class starts | `UiSemantics.PassiveTree.cs:635-663`, `:819-874`; `GameLayout.PassiveTree.RowNameOffset` 0x32, `RowKeystoneOffset` 0x4A, `RowNotableOffset` 0x4B, `RowIconOnlyOffset` 0x54, `RowJewelSocketOffset` 0x65, `RowCharactersOffset` 0x3A, `CharacterIdOffset`, `CharacterNameOffset` | PassiveSkills (Id +0, name, graph id, four flags, Characters list) and Characters (Id, name), as `dat-columns` finds them | the row's Id (row +0, already read at `UiSemantics.PassiveTree.cs:339`) or the graph id (u16, also what the allocated list holds) | small | **read by the plugin** (`PassiveTreeCatalog`, files first): `passive-tree.json` (`nodes`, `starts`, `classes`, `names`) |
| Passive tree links | `UiSemantics.PassiveTree.cs:716-761`, `PassiveTreeFields.cs:210-238`; `GameLayout.cs:441-450` (graph file object, edge vector, edge ends) | `metadata/passiveskillgraph.psg` (`SkillGraphFile`: groups, nodes, links; 5,152 nodes, all PassiveSkills rows) | graph id | small | **read by the plugin** (files first): `passive-tree.json` `links` (both directions, sorted by id). The stick-step order still comes from memory (the edge list, each record's in/out vectors, `GameLayout.cs:458,460`): the file does not order a node's links |
| Skill name | `PlayerSkills.cs:43-49`; `GameLayout.SkillSlot.SkillKeyOffset` 0x4F, `DisplayedNameOffset` 0x8 | GrantedEffects (Id, ActiveSkill key) -> ActiveSkills (DisplayedName), ten languages | the GrantedEffects Id (row +0, read by `PlayerSkills.SkillIds`, `:75-100`) | small | **read by the plugin** (`GemCatalog`; the game's read first, the files as fallback and cross-check): `gems.json` `skills` (1,474 skills a language) |
| Gem name, kind, tags, attribute | not read today (the Build Planner work needs it) | SkillGems (BaseItemTypes key +0x0, strength/dexterity/intelligence share +0x10/+0x14/+0x18, GemEffects list +0x8C) -> GemEffects (Id, GrantedEffects key +0x10, support text +0x20, GemTags list +0x30) -> GemTags (Id, name +0x8); BaseItemTypes (name, parent path +0x28 = the kind) | the gem's base item Id, a GemEffects Id or a GrantedEffects Id: the catalog lists all three | medium | **indexed** (`GemIndex`, for the Build Planner's readers): `gems.json` (1,191 gems: 505 active, 642 support, 44 meta) |
| Area name, level, town flag | `GameStateService.cs:90-112`, `UiSemantics.WorldMapPins.cs:106-110`; `WorldAreaDatOffsets` name +0x08, `GameLayout.WorldAreaRow.LevelOffset` 0x26, `IsTownOffset` 0x14 | WorldAreas (Id, Name, AreaLevel, IsTown: all four already found by `dat-columns`) | the area Id (row +0) | small: names are in `area-names.json`; level and town need two columns added to it or a file of their own | noted |
| World map pin texts | `UiSemantics.WorldMapPins.cs:98-181`; `GameLayout.MapPinRow` area key 0x8, name 0x28, description 0x30, content 0x7C | MapPins (Id +0 and those four columns, found by `dat-columns`), translated | the pin's Id (row +0, read at `UiSemantics.WorldMapPins.cs:172`) | small | noted |
| Minimap icon label | `MinimapIconLabel.cs:13-47`; `GameLayout.MinimapIconRow` label 0x1A, row size 0x2A | MinimapIcons (Id, label; `NameAnchor.MinimapIcons`), translated | the icon's Id | small | noted |
| Monster mod name | `MonsterMods.cs:45-97`; `GameLayout.ModsDat.NameOffset` 0x62 | Mods (Id, Name; 16,911 rows, ten languages) | the mod's Id (row +0, not read today: one more read) | medium (large file unless cut to monster mods) | noted |
| Item base name | host `Components/Base.cs:64-71` (name row +0x30) | BaseItemTypes (Id, Name; `item-names.json` holds the currency ones) | the base item Id (`Base.cs:74-81`) | small: widen `item-names` | wearable bases are in `item-bases.json` ([below](#item-bases)); the host's read is unchanged |
| Monster and NPC display name | `GameLayout.cs:212` (Render +0x160), ten callers | MonsterVarieties and NPCs (Id = metadata path, Name; `NameAnchor.Monsters`, `Npcs`) | the entity's metadata path | medium: rare monsters and renamed entities still need the live name, so a back-up only | noted |
| Skill stat values by level | `SkillStatSets.cs:99-162` (row +0x81 stats, +0x2C values) | GrantedEffectStatSetsPerLevel (41,558 rows) | GrantedEffects Id and level | large | noted |
| Gem requirements by level | not read | GrantedEffectsPerLevel: level +0x10, a float rising with it +0x6C (1.0 at level 1, 97.7 at 20 for Ice Nova) | GrantedEffects Id | blocked: the float's meaning is not verified against the game | noted |
| Buff names, hazards, class mechanics, Atlas nodes, class overrides | — | — | — | — | already catalogs |
| A stat's key | `GameIds` (row + 1) | no file holds the key | — | — | cannot be done from files; checked live on the character's level (`StatKeyCheck`) |

## The Build Planner and the gems

The game's files hold no build guide. `BuildPlannerInventories` (17 rows) is one key per row into Inventories, the slots
the planner shows; `BuildPlannerSkillTrees` (1 row) is a key into PassiveSkillTrees. No table or file name mentions
guides or recommendations (`ShopItemRecommendation` is the shop's): a plan is the player's own `*.build` file, which
`BuildPlanFolder` already reads. So what the planner's gems need from the files is the gem data, and that is `gems.json`:

```
"gems":   { "Metadata/Items/Gems/SkillGemIceNova": { "kind": "ActiveSkillGem", "effects": ["IceNova"], "skills": ["IceNovaPlayer"],
            "tags": ["intelligence", "grants_active_skill", "spell", "area", "cold", "duration", "nova", "repeatable"], "str": 0, "dex": 0, "int": 100 } }
"names":  { "es": { "Metadata/Items/Gems/SkillGemIceNova": "Nova de hielo" } }      ten languages, 1,191 gems each
"tags":   { "es": { "cold": "Hielo" } }                                              61 named tags
"support":{ "es": { "Metadata/Items/Gems/SupportGemIgnition": "Asiste cualquier habilidad que impacte…" } }
"skills": { "es": { "IceNovaPlayer": "Nova de hielo" } }                             every GrantedEffects row with a displayed name
```

`kind` is the last part of the base item's parent path (ActiveSkillGem, SupportSkillGem, MetaSkillGem). `str`, `dex`
and `int` are the shares (0 to 100) of the gem's attribute requirement. Other tables seen and not used: SupportGems
(637 rows: a SkillGems key, an icon, SupportGemFamily keys), SupportGemFamily (389: Id, name), UncutGems, SkillGemInfo,
SkillGemSearchTerms, IndexableSupportGems.

## What the plugin reads from them (2026-11-10)

**The rule.** A catalog supplies only what cannot change while the game runs: what a node or a gem is (its Id, kind,
tags, base name, a support's description), which nodes link, which node starts a class. Whatever the game computes or
changes in play stays the game's to say, exactly as before: what is allocated, a skill's level, damage, cost, cooldown and
quality, the supports linked to it, every tooltip and panel line, a requirement met or not. No value by level and no
damage figure is taken from a file. A catalog missing or unreadable is logged once and the memory reads go on alone.

| Reading | First source | Second source | When they differ |
|---|---|---|---|
| A passive node's name, kind, graph id, class start (`UiSemantics.ReadPassiveRow`) | the files, by the row's Id (`PassiveTreeCatalog`); the name in the plugin's language | the row's columns: alone for a node the files lack (one a patch added) and for the row a class sees in another's place (`PassiveOverrides`, whose Ids are no nodes of the graph) | the first 64 nodes read are compared (`CatalogCrossCheck`: name in any language, four flags, graph id, class list). Log `Passive tree catalog: the game files and the rows in memory agree on 64 nodes`, or `DISAGREE on N of 64 nodes (first …)`; most of them differing is said as `code fields` |
| A node's links (`PassiveGraphOf`) | the files' `links`, for every record whose row's Id they know | the graph's edge list in memory: alone without the catalog, and always for what the files cannot know (a node newer than the catalog, a class start's root record, the stick's order) | each record's two link sets are compared when the tree is read. Log `Passive tree catalog: the game files' links and the graph in memory agree on N nodes` or `DISAGREE …`. The edge list not found is logged and the tree reads on (`Passive tree links: not found in memory; the game files' links are read…`); `passive tree links` is said lost only when no catalog stands in |
| The class's own start, a start's class names (`OwnStart`, `StartClasses`) | the files' `starts` and `classes` | the row's Characters list and the Characters rows | the class list is part of the node comparison above |
| The allocated list's judgment (`PassiveTreeFields.JudgeAllocated`) | every graph id of the files | the ids gathered from the records, with the id of the row a class sees in each one's place | — (the list itself, what is allocated, is always memory's) |
| A skill's name (`PlayerSkills.DisplayedName`) | **the game's read** (GrantedEffects → ActiveSkills, as before), whenever it is text | the files' name of the same GrantedEffects Id: when the game's read is empty or no text, and for every skill once the game's read is judged moved | all the followed player's skills are compared every 2 s (`SkillNameCheck`, in any language). Agreeing on most: the game's names are said, and one that differs is logged as the game's own (`Skill name: the game shows … as …`). Disagreeing on most, twice running: the row offsets moved, the files' names are said, log `Skill names: the game's read DISAGREES …`, said as `code fields`. Fewer than three skills judge nothing |

**Gems** (`GemIndex` over `GemCatalog`): the gem behind a base item Id (whole, or its last part as a build plan keeps
it), a GemEffects Id, a GrantedEffects Id or a name in the game's language. It answers identity only: kind (skill,
support, meta), the tags' names, the attribute the requirement leans on and the three shares, a support's text. A gem's
level, quality, sockets, linked supports and requirement numbers are not in it.

**Found while switching** (0.5.x files):

- A gem's name and its skill's name are two columns that say the same for 77 to 83% of the 504 named skill gems, not
  all ("Armour Piercing Rounds" grants "Load Armour Piercing Rounds"): a skill is named by `skills`, never by its gem.
- 32 skill names and 3 gem names are patterns the game fills in play ("Command: {0}", "Spectre: {0}"); the game's read
  holds the same pattern. `GemInfo.NameIsPattern` marks them.
- Thai gem names carry a size tag (`<size:37>{…}`), 459 of them; skill, node and class names do not. `GemInfo.Name`
  strips it.
- Passive names carry keyword links (`Engarce de [Jewel|joya]`), shown as the game shows them (`KeywordLink`), and 120
  of the 5,152 nodes have no name (ascendancy nodes): the row reads empty too.
- Four GrantedEffects Ids are granted by more than one gem (a placeholder skill of unused gems, a unique's copy), and
  18 English gem names are carried by two gems or more (83 gems): `GemIndex.BySkill` answers the first in Id order, `ByName` all.
- The files hold no "spirit gem" kind: what they hold is the MetaSkillGem kind (44) and the game's Persistent tag (193
  gems), which `GemInfo.IsPersistent` gives.
- PassiveSkills +0x66, the 8 bytes the stick step compares with 0 (`RowNoLinkedStepOffset`), is a 16-byte key: null on a
  main-tree row (`blind17`), row 4 on `AscendancyWarrior2Small9` (`dat-kinds passiveskills english <Id>`). The stick's
  linked stage is now skipped for such nodes ([passive tree](passive-tree.md)).

**Still to confirm live** (the game was not run for this change): the three log lines above with the tree open and a
character with three skills or more, and `Stat keys: key 1 reads the character's level (N)`.

## Item bases

`item-bases.json` (`Poe2UiExtract item-bases`, `ItemBaseCatalog`) is what a base named in a build plan is: 1,869 bases
of 33 item classes (weapons, quivers, shields, bucklers, foci, the four armour pieces, amulets, rings, belts, life and
mana flasks, charms), 1,280 of them with defences, named in ten languages (Simplified Chinese ships no BaseItemTypes).

```
"bases": { "Metadata/Items/Armours/Helmets/FourHelmetStrDex5": { "armour": 97, "class": "Helmet", "es": 0, "evasion": 87, "level": 33 },
           "Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5": { "class": "Spear", "level": 21 } }
"names": { "es": { "Metadata/Items/Armours/Helmets/FourHelmetStrDex5": "Yelmo blindado" } }
```

| Column | Where (0.5.x) | How it was proven |
|---|---|---|
| BaseItemTypes.ItemClass | +0x8, a 16-byte key | `dat-kinds`: a key; War Spear's row (`FourSpear5`) points at the ItemClasses row whose Id is `Spear`, Shielded Helm's at `Helmet`, Ridged Buckler's at `Buckler` |
| BaseItemTypes level | +0x30, int32 | 0 to 100 on every row; 21 on War Spear, 22 on Ridged Buckler, 12 on Soldier Greathelm: the level the game's tooltip asks for (play log 2026-11-10: "Broquel corrugado … Requiere: Nivel 22, 33 Des", a normal item) |
| ArmourTypes.BaseItemType | +0x0, a 16-byte key | `dat-kinds`: a key; every row a BaseItemTypes row, the known bases each have one |
| ArmourTypes armour | +0x10, int32 | Soldier Greathelm 80 (a rare one with 10% increased armour showed 88), Riveted Mitts 38 (44 with 16%), 0 on Ridged Buckler and Linen Wraps |
| ArmourTypes evasion | +0x14, int32 | Ridged Buckler 55 (the normal item's tooltip: "Evasión: 55"), Linen Wraps 23 (a magic pair with no defence modifier showed 23), 0 on Soldier Greathelm |
| ArmourTypes energy shield | +0x18, int32 | Linen Wraps 10 (the same pair showed 10), 0 on the other two |

The three defence columns also follow the bases' own Ids: a `…Str…` base holds armour alone, `…Dex…` evasion alone,
`…DexInt…` evasion and energy shield. ArmourTypes +0x1C (-400 on Explorer Armour) is not read. The extractor finds each
column again by its kind and by those four bases (`ItemBaseColumns`); a known base that reads otherwise fails the step.

What a class is to the game (a slot, one hand or two) is not taken from the files: the wearable classes are a named list
of ItemClasses Ids, `ItemBaseColumns.Equippable` (`UtilityFlask` is the charms' class).

**Names.** 31 English names are carried by more than one base. `ItemBaseCatalog.ByName` answers none for the 16 whose
bases differ in class (2 names), level or defences ("Ascetic Garb" is two body armours of level 45 and 51; the
Runeforged bases); bases the files tell nothing apart of answer the first in Id order. The level is the base's own: an item's modifiers raise what its tooltip asks for.

## Modifier texts

The game writes every modifier line from stat description files, `data/statdescriptions/*.csd` in the bundles (589
files: one for items, the others for skills, the passive tree, maps, monsters and one a skill). They are UTF-16 text:

```
description
	1 base_fire_damage_resistance_%                         the number of stats, then their Ids
	1                                                       how many lines follow
		# "{0:+d}% to [Resistances|Fire Resistance]"        a condition a stat, the text, then handlers
	lang "Spanish"
	1
		# "{0:+d}% a la resistencia al [Resistances|fuego]"
```

- A **condition** is `#` (any value), `N`, `A|B` with `#` for an open end (`1|#`, `#|-1`), or `!N`. The game writes the
  first line of the language whose conditions the stat values meet: that is how singular and plural, and "increased"
  and "reduced", are told (`1|# "{0}% increased …"`, `#|-1 "{0}% reduced …" negate 1`).
- A **place** is `{0}`, `{0:+d}` (always signed), `{}` (the next stat), `{0:d}`; 33 lines use `{0:-d}`, whose meaning
  is not known here.
- A **handler** after the text turns a stat's value into the number shown: `negate 1`, `per_minute_to_per_second 1`,
  `milliseconds_to_seconds 1`, `divide_by_one_hundred 1`, with `_Ndp` (N decimals) or `_Ndp_if_required`; 44 names in
  all. Nine are no arithmetic (`passive_hash`, `mod_value_to_item_class`…): they show something looked up, and the
  plugin does not apply them. `canonical_line` takes no stat.
- Words before the text that are no condition (`table_only`, `gem_quality`) mark lines for other views.
- Between blocks: `no_description <stat>` (24) and, in other files, `include "<file>"`.

`mod-texts.json` (`Poe2UiExtract mod-texts`) is `stat_descriptions.csd` whole: the file an item's modifiers are
described from (`advanced_mod_stat_descriptions.csd` only includes it and adds 18 blocks). 10,776 blocks, 171,832
lines, eleven languages, a block a line of the JSON, every line as the file holds it:

```
{"stats":"base_fire_damage_resistance_%","texts":{"en":["# \"{0:+d}% to [Resistances|Fire Resistance]\""],"es":["# \"{0:+d}% a la resistencia al [Resistances|fuego]\""]}}
```

The step fails when no block is read, when more than one line in a hundred fits nowhere in the format, or when the fire
resistance block no longer reads "Fire Resistance" in English and "fuego" in Spanish. It is not cut to the stats item
modifiers use, so the file is 18 MB; the Mods table's stat columns are proven since (`mod-stats.json`, below), and the
4,038 stats they name would be the cut.

**Modifier stats.** `mod-stats.json` (`Poe2UiExtract mod-stats`, `ModStatCatalog`) is Mods.dat's side of the same
lines: each modifier's stats in the order its values follow, with what each rolls. With it an item's modifier lines
are written from the modifiers it holds in memory, the texts taken from `mod-texts.json` by stat
(`ModTextCatalog.BlocksOf`, `Show`): columns, handlers, the rules for a certain line and the live check are in
[item modifiers](item-mods.md).

**The plugin** (`ModTextCatalog`, pure `ModTextVariation`) keeps the lines as UTF-8 (about 20 MB) and indexes them on a
worker thread at start-up (about 2 s) by their skeleton: markup out as `BuildNotes.Plain` and the keyword links do it,
one case, single spaces, every number or place a `#`.

- `Parse(text)` reads a line in any language. The numbers found fill the places (a number the text writes out, "for 6
  seconds", must be the text's), the handlers are undone to get the stat values, the line must be the first of its block
  those values fit, and the English line the same values fit gives the answer: its wording with the places left in
  (`{0:+d}% to Fire Resistance`) is the line's identity, and the numbers are the ones that English line shows, one a
  stat it has a place for. Blocks that word a line alike in English ("Adds {0} to {1} Physical Damage", local and
  global) are one line; a text two different English lines fit is not told.
- A tooltip's rolled number with its range, `+23(20-30) al maná máximo`, gives 23. A range alone, as a crafting text
  shows it (`+(11-15)% a la resistencia al fuego`), gives its lower end: the contract has one number a place.
- A "reduced" line is another line than its "increased" one, with the number as written (25, the stat being -25).
- `Say(line, language)` undoes the English handlers, picks the first line of the language the values fit and writes it
  with that line's handlers and decimals, with the decimal point the Spanish client shows ("2.8").
- Not told: a line with a handler that is no arithmetic, a `{0:-d}` place or a word before the text that is no condition.

Verified against the game's Spanish tooltips (play log 2026-11-10), English to Spanish: "+16% to Fire Resistance" →
"+16% a la resistencia al fuego"; "+40 to maximum Life" → "+40 a la vida máxima"; "Adds 6 to 11 Physical Damage" →
"Agrega de 6 a 11 de daño físico"; "Adds 1 to 16 Lightning damage to Attacks" → "Agrega de 1 a 16 de daño de rayo a los
ataques"; "65% increased Physical Damage" → "Daño físico aumentado un 65%"; "11% increased Attack Speed" → "Velocidad de
ataque aumentada un 11%"; "+64 to Stun Threshold" → "+64 al umbral de aturdimiento"; "31% increased Rarity of Items
found" → "Rareza de los objetos encontrados aumentada un 31%". All 97 modifier lines and 41 bases of the owner's build
plans are told (`GearCatalogTests`).
