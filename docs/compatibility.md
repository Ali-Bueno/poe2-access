# Surviving game updates

What a PoE2 patch can break, what follows it on its own, and what needs a person. Code: `LayoutProbe`,
`InGameFields`, `CompatibilityReport` (plugin); `GameDataSetup` (launcher).

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
| UI manager root slot: holds the root of the elements that point at the manager (2 elements must agree before an unproven manager is trusted; the direct in-game slot is taken only as measured) | focus, last focused, hovered, pad state | `LayoutProbe.AdoptManager` |
| In-game layers by name (`HUD`, `death_screen`, `skill_bar`, `bind_management_display`) or shape (inventory: child `panels_parent`; chat: `textField` and `scrollbar` below) | the objective box (no name or shape) follows the nearest one found | `InGameFields` |
| Entity names: half of 30 monsters, characters and players read a name | — | `LayoutProbe.ProbeRenderNames` |
| Targetable in-play flag: one living monster flagged proves it; 20 unflagged and none flagged means it moved | everything counts as in play rather than nothing | `EntityCatalog.CheckLayout` |
| Pad state: sticks within ±1, triggers within 0..1, else no input (no phantom L3 gestures) | — | `GamepadReader` |
| Area row of the loading state: points at a world-area row with a name and an id shaped like `G1_1` (searched only in game, when it must hold the area) | — | `GameStateService.AreaRowOffset` |

### Control subclasses
Their fields sit after the base control, so when it grows they all move as far as the label text did
(`LayoutProbe.TextShift`); that is where each is tried first.

| Group (judged on a live control of that kind) | Fields | Where |
|---|---|---|
| Combo box: the selected entry reads as text and, while closed, the highlight rests on it (the entries vector has a copy right after it, so the entries alone fit at +0x18 too) | selected, highlight, open flag, both vectors | `UiElementReader.ComboBoxFits` |
| Slider: value within min..max, both within ±1,000,000, value above the minimum (a slider at its minimum fits beside zeros almost anywhere, so it proves nothing) | value, max, min | `UiElementReader.ReadSliderState` |
| Inventory: the cursor cell holds an item element whose entity is under `Metadata/Items` (an empty cell proves nothing) | grid cell size, hovered item, cell, server inventory; item element's entity | `InventoryReader.TryReadGrid` |
| Nothing to check them by: they follow the label text | check box, tab selected, selectable list index, class selector, skill slot binding and details, passive node record and canvas state, panel title, placeholder | `LayoutProbe.TextFollowers` |

A shift other than the expected one is trusted once two controls agree on it (`ControlFields`). Only combo boxes and
the area row are said when lost: a grid is recognised by reading it and a slider at its minimum proves nothing, so
their misses are only logged.

The child order of the in-game layer container changes between sessions: layers are never found by index.

Outcomes are logged once each ("Layout: HUD where measured", "moved from +0x558 to +0x560", "NOT FOUND"); parts not
found are said once together ("Aviso de compatibilidad: no encuentro el chat y la barra de habilidades…"). A new game
process (`OnAttachedChanged`) logs the game exe's size and time and checks everything again.

**Self-test:** dev command `layout` (write it to `Plugins/Poe2Accessibility/debug/command.txt`) starts every search
`LayoutProbe.SelfTestShift` (0x18) off target and logs whether it comes back ("element names ok; … UI manager
FAIL (found 0x…)"). For the area row and the control groups judged so far it lists every other offset within reach
that also fits ("combo boxes FAIL (also fits at +0x18)"); any of them could be taken for the true one after an update.
Run it on each new build of the plugin, at login, in game, in the options and in the inventory (2026-10-24: all ok).

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
(`DatColumns`, logged "where measured" / "moved from … to …"); the PassiveSkills flags beside the name follow its shift.

## Still needs a person after a patch
- GameHelper2 upstream offsets: pull and rebuild (`external/GameHelper2`), then `tools/package/New-TestPackage.ps1`.
- The parts in "Still fragile" below. A wrong one makes that reading fall back (a mod without its name, an item
  without its base name) rather than speak garbage, since every string goes through the decoders' checks.
- Run the `layout` self-test and a short session through login, a menu, the inventory and a fight.

## Still fragile (next session, asked 2026-10-24)
What a patch can still move with nothing to find it again, most player-visible first, each with the idea to try.

1. ~~Monster modifier names~~ and the passive and skill names: found again from the files (`dat-columns`, above).
2. **Flag and key columns** (`PassiveTree.RowKeystone/Notable/IconOnly/JewelSocket/Characters/GraphId` follow the
   passive name's shift, which is right only when the change came before the name; `SkillSlot` level, effect and skill
   keys): need the community schema (poe-tool-dev/dat-schema) or known rows (a known keystone's flag set).
   `BaseComponent.NameOffset` is a std::wstring in GameHelper's in-memory row, not a dat column: check it against the
   item's tooltip title.
3. ~~The launcher's own columns~~ (`ItemSoundExtractor`): kept while the row sizes match and each column still holds
   what it must; else found by content: sound paths are string columns of `Audio/…` text, a key column holds null keys
   or row indices of its target and is ranked by how many item drop samples its keys lead to (0.5.5e: the measured
   ItemVisualIdentity 919 leads to none, the search takes 24 and 140 and writes the same catalog).
4. **Component fields**: `BaseComponent.NameRowOffset` (a pointer to a BaseItemTypes row whose name matches the
   item's tooltip title), `NpcComponent.MarkerRowOffset` (quest marker over NPCs), `TargetableComponent` (a lost flag
   is already noticed and everything counts as in play; could search for the byte that is 1 on living monsters and 0
   on corpses).
5. **Image texture fields** in the base control (`Image.TextureOffset`/`AltTextureOffset`: currency icons, gold): the
   texture's key string contains `Art/`, searchable on image elements like the text anchor.
6. **Control flag bits** (`Element` interactive 0x1B, navigable 0x0E, text input 0x10, enabled 0x0D): check against
   known controls (menu buttons are interactive and navigable, a text box has the text-input bit).
7. **Groups that only follow the label text** could get a check of their own in `ControlFields`: tabs (exactly one
   sibling selected), class selector (index 0..5 with the class id beside it), skill slot binding (unique 0..23
   across the bar), passive node record (a PassiveSkills row whose graph id is the node's), panel title and
   placeholder (a readable string).
8. **Passive tree graph** (`PassiveTree.StateGraphFileOffset`, `GraphContentOffset`, `EdgeListOffset`,
   `AllocatedOffset`): allocated entries must be node ids of the graph, edges must join known nodes.
9. **Pad buttons** (`GameLayout.PadButtons`): only the axes are checked (`GamepadReader.Plausible`); a press seen by
   the game's focus moving could confirm the A bit.
10. **GameHelper2 upstream**: still pull and rebuild by hand; when its state patterns stop matching, the plugin could
    say so instead of staying silent.

Unused, can go: `InGameUi.NpcDialogueLayerOffset`, `AreaTransitionComponent.DestinationRowOffset`.
