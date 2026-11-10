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
| Passive node name, kind, class starts | `UiSemantics.PassiveTree.cs:635-663`, `:819-874`; `GameLayout.PassiveTree.RowNameOffset` 0x32, `RowKeystoneOffset` 0x4A, `RowNotableOffset` 0x4B, `RowIconOnlyOffset` 0x54, `RowJewelSocketOffset` 0x65, `RowCharactersOffset` 0x3A, `CharacterIdOffset`, `CharacterNameOffset` | PassiveSkills (Id +0, name, graph id, four flags, Characters list) and Characters (Id, name), as `dat-columns` finds them | the row's Id (row +0, already read at `UiSemantics.PassiveTree.cs:339`) or the graph id (u16, also what the allocated list holds) | small | **done**: `passive-tree.json` (`nodes`, `starts`, `classes`, `names`) |
| Passive tree links | `UiSemantics.PassiveTree.cs:716-761`, `PassiveTreeFields.cs:210-238`; `GameLayout.cs:441-450` (graph file object, edge vector, edge ends) | `metadata/passiveskillgraph.psg` (`SkillGraphFile`: groups, nodes, links; 5,152 nodes, all PassiveSkills rows) | graph id | small | **done**: `passive-tree.json` `links` (both directions). Backs up the edge walk; the stick-step order still needs the per-record in/out vectors (`GameLayout.cs:458,460`), which the file does not order |
| Skill name | `PlayerSkills.cs:43-49`; `GameLayout.SkillSlot.SkillKeyOffset` 0x4F, `DisplayedNameOffset` 0x8 | GrantedEffects (Id, ActiveSkill key) -> ActiveSkills (DisplayedName), ten languages | the GrantedEffects Id (row +0, read by `PlayerSkills.SkillIds`, `:75-100`) | small | **done**: `gems.json` `skills` (1,474 skills a language) |
| Gem name, kind, tags, attribute | not read today (the Build Planner work needs it) | SkillGems (BaseItemTypes key +0x0, strength/dexterity/intelligence share +0x10/+0x14/+0x18, GemEffects list +0x8C) -> GemEffects (Id, GrantedEffects key +0x10, support text +0x20, GemTags list +0x30) -> GemTags (Id, name +0x8); BaseItemTypes (name, parent path +0x28 = the kind) | the gem's base item Id, a GemEffects Id or a GrantedEffects Id: the catalog lists all three | medium | **done**: `gems.json` (1,191 gems: 505 active, 642 support, 44 meta) |
| Area name, level, town flag | `GameStateService.cs:90-112`, `UiSemantics.WorldMapPins.cs:106-110`; `WorldAreaDatOffsets` name +0x08, `GameLayout.WorldAreaRow.LevelOffset` 0x26, `IsTownOffset` 0x14 | WorldAreas (Id, Name, AreaLevel, IsTown: all four already found by `dat-columns`) | the area Id (row +0) | small: names are in `area-names.json`; level and town need two columns added to it or a file of their own | noted |
| World map pin texts | `UiSemantics.WorldMapPins.cs:98-181`; `GameLayout.MapPinRow` area key 0x8, name 0x28, description 0x30, content 0x7C | MapPins (Id +0 and those four columns, found by `dat-columns`), translated | the pin's Id (row +0, read at `UiSemantics.WorldMapPins.cs:172`) | small | noted |
| Minimap icon label | `MinimapIconLabel.cs:13-47`; `GameLayout.MinimapIconRow` label 0x1A, row size 0x2A | MinimapIcons (Id, label; `NameAnchor.MinimapIcons`), translated | the icon's Id | small | noted |
| Monster mod name | `MonsterMods.cs:45-97`; `GameLayout.ModsDat.NameOffset` 0x62 | Mods (Id, Name; 16,911 rows, ten languages) | the mod's Id (row +0, not read today: one more read) | medium (large file unless cut to monster mods) | noted |
| Item base name | host `Components/Base.cs:64-71` (name row +0x30) | BaseItemTypes (Id, Name; `item-names.json` holds the currency ones) | the base item Id (`Base.cs:74-81`) | small: widen `item-names` | noted |
| Monster and NPC display name | `GameLayout.cs:212` (Render +0x160), ten callers | MonsterVarieties and NPCs (Id = metadata path, Name; `NameAnchor.Monsters`, `Npcs`) | the entity's metadata path | medium: rare monsters and renamed entities still need the live name, so a back-up only | noted |
| Skill stat values by level | `SkillStatSets.cs:99-162` (row +0x81 stats, +0x2C values) | GrantedEffectStatSetsPerLevel (41,558 rows) | GrantedEffects Id and level | large | noted |
| Gem requirements by level | not read | GrantedEffectsPerLevel: level +0x10, a float rising with it +0x6C (1.0 at level 1, 97.7 at 20 for Ice Nova) | GrantedEffects Id | blocked: the float's meaning is not verified against the game | noted |
| Buff names, hazards, class mechanics, Atlas nodes, class overrides | — | — | — | — | already catalogs |
| A stat's key | `GameIds` (row + 1) | no file holds the key | — | — | cannot be done from files |

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

## What the plugin must change (not done: those files belong to other work)

1. **Load the catalogs** beside `EndgameCatalog`: two small loaders (`GemCatalog`, `PassiveTreeCatalog`) reading
   `catalog/gems.json` and `catalog/passive-tree.json` with the IO guard the other loaders have, constructed in
   `Poe2AccessibilityCore.cs` near line 175.
2. **Passive nodes** (`UiSemantics.PassiveTree.cs:635-663`, `ReadPassiveRow`): keep reading the row's Id (+0) and take
   name and kind from `names[language][id]` and the flags after `|` in `nodes[graph id]`; fall back on the row columns
   when the Id is not in the catalog (a node a patch added before the launcher reran). `OwnStart` and `StartClasses`
   (`:819-874`) read `starts` and `classes`. Then `RowNameOffset`, the four flag offsets, `RowCharactersOffset` and
   the two Characters offsets are back-ups only.
3. **Links** (`:716-761`): `links[graph id]` gives each node's neighbours without the graph file object or the edge
   vector (`GameLayout.cs:441-450`). Keep the per-record vectors for the stick rule's order.
4. **Skill names** (`PlayerSkills.DisplayedName`, `:43-49`): read the GrantedEffects Id (the chain already passes through
   that row) and return `skills[language][id]`; follow the ActiveSkills key only when the catalog lacks it.
5. **Build Planner gems**: whichever Id the planner's element gives, `gems` is keyed by the base item Id and each entry
   lists its GemEffects and GrantedEffects Ids, so an index by those two gives the gem from any of the three.
6. **Stat keys**: check once per attach that key `row("level") + 1` of the player's stats reads the character's level
   (`PlayerOffsets.Level`); report `stats` through `CompatibilityReport` when it does not. No file can check it.
7. **Compiled dat offsets**: `DatColumns.Report(compatibility)` is called from `DatKey.Row` on the first key followed;
   the natural place is `Poe2AccessibilityCore.cs:177`, right after the report is made.
