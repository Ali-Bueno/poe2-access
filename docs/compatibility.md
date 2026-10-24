# Surviving game updates

What a PoE2 patch can break, what follows it on its own, and what needs a person. Code: `LayoutProbe`,
`InGameFields`, `CompatibilityReport` (plugin); `GameDataSetup` (launcher).

## What depends on the game's build
| Layer | Examples | After a patch |
|---|---|---|
| GameHelper2 (upstream) | 4 byte patterns for the game states; struct offsets in `GameOffsets` (element base, entities, components) | Patterns usually survive. Offsets are updated upstream: pull `external/GameHelper2`, rebuild, repackage. |
| The plugin's own offsets (`GameLayout`, ~86) | control name and text fields, UI manager focus fields, in-game layer fields, Render name, Targetable flag, dat row columns | The ones below are found again at runtime; the rest are checked only by the readings that use them. |
| Data from the game's files | `catalog/item-sounds.json`, `area-names.json`, `ui-catalog.json`, the extracted sounds | Regenerated on the player's machine by the launcher when the game's files change. |

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

## Still needs a person after a patch
- GameHelper2 upstream offsets: pull and rebuild (`external/GameHelper2`), then `tools/package/New-TestPackage.ps1`.
- Dat row columns (`ModsDat.NameOffset`, `PassiveTree.Row*`, `SkillSlot` keys, `BaseComponent`/`NpcComponent` rows)
  and the image texture fields in the base control. A wrong one makes that reading fall back (a mod without its name,
  an item without its base name) rather than speak garbage, since every string goes through the decoders' checks.
  The columns could follow the community dat schema if the launcher fetched it when regenerating the data.
- Run the `layout` self-test and a short session through login, a menu, the inventory and a fight.
