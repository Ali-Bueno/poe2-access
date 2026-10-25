# Surviving game updates

What a PoE2 patch can break, what follows it on its own, and what needs a person. Code: `LayoutProbe`,
`InGameFields`, `ControlFields`, `EntityFields`, `FlagBits`, `PassiveTreeFields`, `CompatibilityReport` (plugin);
`GameDataSetup` (launcher); `tools/package/Update-GameHelper.ps1` (host).

## What depends on the game's build
| Layer | Examples | After a patch |
|---|---|---|
| GameHelper2 (upstream) | 4 byte patterns for the game states; struct offsets in `GameOffsets` (element base, entities, components) | Patterns usually survive. Offsets are updated upstream: pull `external/GameHelper2`, rebuild, repackage. |
| The plugin's own offsets (`GameLayout`, ~86) | control name and text fields, UI manager focus fields, in-game layer fields, Render name, Targetable flag, dat row columns | The ones below are found again at runtime; the rest are checked only by the readings that use them. |
| Data from the game's files | `catalog/item-sounds.json`, `area-names.json`, `ui-catalog.json`, `dat-columns.json`, the extracted sounds | Regenerated on the player's machine by the launcher when the game's files change. |

## Found again at runtime (plugin)
Each field is tried where it was measured first, so a build that did not move it behaves exactly as before; only
when it no longer holds what it must is it looked for nearby (`OffsetSearch`: outward, one 8-byte step either side,
nearest fit wins). Fields of one structure that move together follow an anchor.

| Anchor (how it is recognised) | Followers | Where |
|---|---|---|
| Control name: the root or a layer carries a known name (`root_control`, `HUD`, `map_overlay`, …) | font, manager pointer, tooltips | `LayoutProbe.ProbeNames` |
| Label text: tagged text plus two equal stripped copies, on 5+ labels | text colour | `LayoutProbe.ProbeText` |
| Image texture: 5+ images reach an art key (`Art/…dds|`) through the pointer, far fewer through the next slot (the smaller image class's pointer sits one step before) | the smaller class's texture | `LayoutProbe.ProbeTextures` |
| UI manager root slot: holds the root of the elements that point at the manager (2 elements must agree before an unproven manager is trusted; the direct in-game slot is taken only as measured) | focus, last focused, hovered, pad state | `LayoutProbe.AdoptManager` |
| In-game layers by name (`HUD`, `death_screen`, `skill_bar`, `bind_management_display`) or shape (inventory: child `panels_parent`; chat: `textField` and `scrollbar` below) | the objective box (no name or shape) follows the nearest one found | `InGameFields` |
| Entity names: half of 30 monsters, characters and players read a name | — | `LayoutProbe.ProbeRenderNames` |
| Targetable in-play flag: set on what is always in play (players, waypoint, stash, checkpoint, living monsters) and, of an NPC's story variants (one Render name), on at most one; the nearest byte that fits when the measured one does not | targeted flag; everything counts as in play while unjudged or lost | `EntityFields` (pure `InPlayFlag`) |
| NPC quest marker: the NPC component's dat reference leads to the MiscAnimated row `NPCQuestMarker…` on some NPC (a town with no marker proves nothing; the self-test lists the row ids it sees) | — | `EntityFields.HasMarkerAt` |
| Control flag bits: interactive is on most controls with a click sound and on no plain label; text input on combo boxes and text fields and on no label (some buttons carry it too); navigable on what the in-game controller focus lands on, rarely on its container (grids left out); a bit is kept while half its carriers still have it | — | `FlagBits` (pure `BitSearch`) |
| Enabled flag bit: nothing tells it apart (a disabled control is rare), so it is only noticed lost (under 20% of a screen's controls): then every control counts as enabled | — | `FlagBits.EnabledTrusted` |
| Pad state: sticks within ±1, triggers within 0..1, else no input (no phantom L3 gestures) | — | `GamepadReader` |
| Pad buttons: the left trigger is reported both as a pressure and as a button bit; the bit set in nearly every frame it is down alone and in nearly none with both up gives how far all the buttons moved | every button | `PadButtonCheck` |
| Passive tree graph: the tree state holds the graph file (named `Metadata/PassiveSkillGraph.psg`); its content an edge list whose sampled edges join node records; the state's allocated list holds only graph ids of those records (gathered off the frame thread) | links, allocations | `PassiveTreeFields` |
| Tile grid (GameHelper's `TileDetailsPtr`, read raw): record size = the grid's bytes over its sub-tiles; the file pointer and path where half the sub-tiles name a `.tdt`; the three id bytes where neighbours from one file differ by one step (90% in the Act 1 town) and selectors stay 0..7; searched on every grid, and a layout the search finds wins (the reading's own check looks at the paths and selectors only) | place names, props | `TileGridSearch` (pure), `PlaceNarrator` |
| Item base name (GameHelper's `BaseOffsets`, no static check): a normal or magic item's tooltip title holds it (each item counted once, case ignored; ground labels do not count); two titles in a row that miss start a search of the row pointer and the name nearby, taken once a second title agrees; none anywhere: titles are used instead | — | `BaseNameField` (pure), `InventoryReader` |
| Area row of the loading state: points at a world-area row with a name and an id shaped like `G1_1` (searched only in game, when it must hold the area) | — | `GameStateService.AreaRowOffset` |

### Control subclasses
Their fields sit after the base control, so when it grows they all move as far as the label text did
(`LayoutProbe.TextShift`); that is where each is tried first.

| Group (judged on a live control of that kind) | Fields | Where |
|---|---|---|
| Combo box: the selected entry reads as text and, while closed, the highlight rests on it (the entries vector has a copy right after it, so the entries alone fit at +0x18 too) | selected, highlight, open flag, both vectors | `UiElementReader.ComboBoxFits` |
| Slider: value within min..max, both within ±1,000,000, value above the minimum (a slider at its minimum fits beside zeros almost anywhere, so it proves nothing) | value, max, min | `UiElementReader.ReadSliderState` |
| Inventory: the cursor cell holds an item element whose entity is under `Metadata/Items` (an empty cell proves nothing) | grid cell size, hovered item, cell, server inventory; item element's entity | `InventoryReader.TryReadGrid` |
| Tabs: exactly one tab of a bar (two or more siblings with the tab click sound) has its byte set, the others 0 | tab selected | `UiSemantics.TabSelectedOffset` |
| Skill bar: the 24 slot elements hold distinct binding indices 0..23 | binding index | `UiSemantics.BindingOffset` |
| Skill slot icon: its skill details lead to a GrantedEffectsPerLevel row (an empty slot proves nothing) | skill details | `UiSemantics.SkillDetailsShift` |
| Passive tree canvas: its state object holds `Metadata/PassiveSkillGraph.psg` | canvas state | `UiSemantics.TreeState` |
| Passive node (a child of a group of that canvas): its record leads to a PassiveSkills row | node record | `UiSemantics.NodeRecord` |
| Class selector: its index and the class id beside it name the same class of the first row (`ClassSelector.FirstRowClassIds`; a variant highlighted proves nothing) | index, class id | `UiSemantics.SelectorAgrees` |
| Panel title (only under `panels_parent`): an ancestor of the panel reads a title there | title | `UiSemantics.TitleAt` |
| Text field hint: reads as words other than the field's font face (the field holds its font 0x50 before the hint) | hint | `UiSemantics.Placeholder` |
| Options section (weak): on a focused options row, the section two levels above its list has the active byte set and the row's index after it | active, highlighted row | `UiElementReader.IsSectionRow` |
| Check box, selectable list, quest map depth: nothing static, so judged the way they were measured, by watching them change: the shift whose value changes as the player toggles or moves is the field; another shift that changes twice while the expected one stays put takes its place | state, selected index | `ControlFields.Changing` (pure `ChangingField`) |

A shift other than the expected one is trusted once two controls agree on it (`ControlFields`); weak groups (tabs, panel
titles, options sections) need a second control even where measured, and a control that fitted nowhere is tried again later. Only combo
boxes and the area row are said when lost: a grid is recognised by reading it and a slider at its minimum proves
nothing, so their misses are only logged.

The child order of the in-game layer container changes between sessions: layers are never found by index.

Outcomes are logged once each ("Layout: HUD where measured", "moved from +0x558 to +0x560", "NOT FOUND"); parts not
found are said once together ("Aviso de compatibilidad: no encuentro el chat y la barra de habilidades…"). A new game
process (`OnAttachedChanged`) logs the game exe's size and time and checks everything again.

**Self-test:** dev command `layout` (write it to `Plugins/Poe2Accessibility/debug/command.txt`) starts every search
`LayoutProbe.SelfTestShift` (0x18) off target and logs whether it comes back ("element names ok; … UI manager
FAIL (found 0x…)"). For the area row and the control groups judged so far it lists every other offset within reach
that also fits ("combo boxes FAIL (also fits at +0x18)"); any of them could be taken for the true one after an update.
Parts with a judgment of their own report it ("in-play flag ok", "quest marker ok", "interactive flag ok (also fits
0x12: …)", "pad buttons ok (shift 0)", "passive tree graph ok; links ok; allocations ok", "tile grid ok (990 of 990
sub-tiles name a tile; …; a search finds it too)", "item base names ok", "selectable lists ok (seen changing)"), or
"not judged yet" with what they need. Run it on each new build of the plugin at login, at character creation (class
selector, name field), in town (an NPC with a quest marker), in the options, the inventory (a tooltip), the passive
tree, and with the left trigger pressed alone (2026-10-25: all ok, every part judged live).

## Regenerated by the launcher (packaged builds)
`GameDataSetup` stamps the install (`catalog/game-version.txt`: size and time of `Bundles2/_.index.bin` and of the exe,
`fmod.dll` product version) and, when the stamp differs, rebuilds the item sounds table, the area names, the UI
catalog and the sounds from the player's own install (about 25 s, each step independent, old files kept on
failure). The FMOD header version is read from `fmod.dll` (`SoundExtractor`).

`dat-columns` (`DatColumnExtractor`) finds the dat columns names are read from: a display name is the first string column
whose text differs between the English table and its Spanish copy (a string column holds the offset of a string's first
character in 80% of rows; the name differs in a quarter of the rows that have one). In a datc64 a string is 8 bytes, as
wide as the pointer in memory, so the file's column offset is the row offset the plugin reads (0.5.5e: Mods.Name 0x62,
PassiveSkills.Name 0x32, ActiveSkills.DisplayedName 0x8, all as measured live). The plugin applies the file at start
(`DatColumns`, logged "where measured" / "moved from … to …").

Flags and keys are found by what they hold (`DatContentColumns`), each on its own, so a column added between them is
followed too: a flag column is the 0/1 column set on the most rows whose Id carries its word (`keystone`, `notable`,
`mastery`, `jewel`; those rows at least a quarter of its set rows), the graph id the u16 column distinct on every row,
the class list the only key list into Characters, the per-level effect key the key column reaching nearly every
GrantedEffects row, and the ActiveSkills key the one whose target Ids appear inside the effect Ids (0.5.5e: 1160 rows,
the next column 41). A PassiveSkills column a launcher did not write follows the name's shift.

## Still needs a person after a patch
- GameHelper2 upstream offsets (its state patterns, entities, components, terrain): `pwsh
  tools/package/Update-GameHelper.ps1` (with GameHelper stopped) fetches upstream, lists the new commits, fast-forwards
  `external/GameHelper2`, rebuilds the host and the plugin and runs the tests; `-CheckOnly` only lists, `-Package` also
  builds the test package. When its patterns or offsets stop fitting, the plugin says so: attached two minutes
  without ever reading a game state (`GameStateService`, part "game states": "no encuentro el estado del juego
  (GameHelper necesita una actualización)").
- A part the self-test reports lost after a patch: each search assumes one change at a time near where the field was
  measured, so a redesign (a new structure, a field moved far) still needs a new measurement. A wrong part makes its
  reading fall back (a mod without its name, an item without its base name) rather than speak garbage, since every
  string goes through the decoders' checks.
- Run the `layout` self-test (above) and a short session through login, a menu, the inventory and a fight.

## What a search cannot recover
Asked 2026-10-24 to leave nothing fragile; since 2026-10-25 every field the plugin reads from the game has a check
(above), and each was judged live. What remains is by design:

- A search finds a field moved near where it was measured (within 0x40 to 0x100 bytes, or a bit renumbered), one
  change at a time; a field moved farther, or two fields of one judgment moved at once, is reported lost.
- Parts whose evidence is rare are judged late: the quest marker needs an NPC that shows one, check boxes and
  selectable lists a change by the player; the enabled bit is only noticed lost.
- GameHelper's own offsets beyond the tile grid and item base names (entity lists, components' other fields, the
  terrain) are GameHelper's to update; `Update-GameHelper.ps1` brings them in.
