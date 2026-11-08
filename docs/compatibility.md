# Surviving game updates

What a PoE2 patch can break, what follows it on its own, and what needs a person. Code: `LayoutProbe`,
`InGameFields`, `ControlFields`, `EntityFields`, `FlagBits`, `PassiveTreeFields`, `CompatibilityReport`,
`WorldLayoutWatch` (plugin); `AreaLayoutSearch`, `AreaInstanceLayout`, `OffsetHealer`, `StructShapes`, `StructRemap`,
`GameStatesSearch` (host, `host/GameHelper`); `GameDataSetup` (launcher). Which fields have how many of these, and
the gaps: [field protection audit](research/field-protection-audit.md).

## What depends on the game's build
| Layer | Examples | After a patch |
|---|---|---|
| The host's reader (`host/`, from GameHelper2) | 6 byte patterns; struct offsets in `GameOffsets` (area, entities, components) | Found again at run time by the searches below; the constants are kept at the current build's values. |
| The plugin's own offsets (`GameLayout`, ~86) | control name and text fields, UI manager focus fields, in-game layer fields, Render name, Targetable flag, dat row columns | The ones below are found again at runtime; the rest are checked only by the readings that use them. |
| Data from the game's files | `catalog/item-sounds.json`, `area-names.json`, `ui-catalog.json`, `dat-columns.json`, the extracted sounds | Regenerated on the player's machine by the launcher when the game's files change. |

## Found again at runtime (host)
The host's offsets are compiled in, so a patch that moved them silenced the whole world until a new host shipped: 0.5.5f
moved `AreaInstance` (level, hash, player +8; entities, terrain +0x28) and enemies, radar and items went quiet while the
menus kept reading. History says this is the common case: `AreaInstance` moved in 0.5.4, 0.5.4c, 0.5.5 and 0.5.5f. The
host's searches (once a patch to GameHelper2, now in `host/`) read those fields wherever they are.

| Field | How it is recognised | Where |
|---|---|---|
| Area: local player | an entity under `Metadata/Characters/` | `AreaLayoutSearch` |
| Area: server data | a player list leading to inventories, before the player | `AreaLayoutSearch` |
| Server data's player list, a player's inventories, an inventory's size and cells | a list of up to four players, the first with a list of 0x18-byte inventory entries, three in four of those inventories X by Y cells with one pointer per cell, the first few items in them entities under `Metadata/Items/` (at least one item). Judged in the sweep; a move makes the server data read its inventories again (it reads them once per area) | `InventoryLayoutSearch`, `OffsetHealer.JudgeInventories` |
| Ground items (`WorldItem.ItemEntityPtr`) | the pointer of up to four ground items leads to an entity under `Metadata/Items/` (none on the ground: not judged) | `StructShapes.IsItemEntity`, `OffsetHealer.JudgeGroundItems` |
| Area: entity lists | two adjacent MSVC maps, sentinel flagged, root's value an entity | `AreaLayoutSearch` |
| Area: terrain | the tile vector holds X × Y records of one size (a multiple of 8, 0x38 in 0.5.5f) and the walkable grid Y rows of tiles, each as many grid rows as a tile spans cells (23), a row two cells to a byte for X tiles; both sizes come from the terrain, and the cells per tile go to every grid reader (`TileStructure.TileToGridConversion`) | `AreaLayoutSearch` |
| Area: level, hash | level 1-100 in one byte (the next can hold a flag), then 8 bytes of flags and three vectors (any empty); a hash too large to be the top of a pointer, at the measured distance | `AreaLayoutSearch` |
| Entity id (the valid flag follows it) | the list's key equals the id, on the root and its child | `AreaLayoutSearch.FindEntityId` |
| Entity path and components (details pointer, path, component vector, lookup, name map) | the details lead to a wide string under `Metadata/`; the first and last component point back at the entity; the lookup's map names components (half its entries or more, one of `Positioned`, `Render`, `Life`…) with indexes inside the vector. Judged in the sweep on the player and four entities; searched whenever an area shows no world, since every area check asks an entity for its path. `ItemStruct` moves with `EntityOffsets` | `EntityLayoutSearch`, `OffsetHealer.JudgeEntityLayout` |
| Life | every vital points back at its component; one shift lands the measured ones on them (the game has seven vitals, GameHelper reads five). Inside a vital: when the measured back-pointer and total find under three vitals, or find them but none full (a total read one field late takes the current for the total), the pair nearest them under which three vitals point back with a total above zero, life above zero on one and, in the second case, two full. Then the living player's life total must be above zero and its life no higher. Every judgment sets the whole remap (intact clears it), except an intact one with no vital full over a code placement, which is not judged | `StructShapes.PlaceVitals`, `StructShapes.VitalLayout` |
| Buffs | a pointer list that ends in a lower-case id such as `herald_of_thunder` (no buff on the player: not judged) | `StructShapes.IsBuffList` |
| Skills, cooldowns | 16-byte entries with a 0x48-byte vector right after | `StructShapes.IsSkillList` |
| Stats | a pointer to key and value pairs with small keys | `StructShapes.IsStatsPointer` |
| Camera | the game keeps the matrix twice, one copy after the other | `StructShapes.IsCameraMatrix` |
| Positions | a point inside the map that projects near the screen's middle (the camera follows the player); no such point while the measured field still holds a point of the map is not judged: the camera is elsewhere, and the field has not moved | `OffsetHealer.JudgePosition` |
| Game state's area and world pointers | the pointer whose target holds a world / a camera matrix | `OffsetHealer.LocateArea` |
| Game states, when their pattern fails | a static pointer to an object whose state stack ends in one of its states (at least 8 states; two of 13 are null until used) | `GameStatesSearch` |
| Game state object: stack and state list (every attach) | 13 slots of (object, control block), the used ones distinct and heap-aligned, and a stack of 16-byte entries whose last is one of them. The measured list is kept while it fits; a moved one takes, of the list found and those a slot either side, the one with more states. A list a whole slot off reads as one too: both roles found the same number of slots off rename every state (`WholeShift`). Not found at attach (the game still starting): looked for again every 2 s, said lost after two minutes | `GameStateLayout.Locate`, `GameStates.JudgeLayout` |
| UI element base (`UiElementBaseOffset`: self, children, parent; position, modifier, scale; flags, scale index; size) | on the UI root, before any walk reads through them: its own address; children that are elements whose parent field points back at it; its size, the base resolution 2560 x 1600. The position group (the root at 0,0 with scale 1) and the flags group (the root visible with scale index 3, its descendants not all alike) have no such mark, so each takes the parent's or the size's shift, whichever fits. The plugin's readers of the struct (`UiElementReader`, `UiTreeReader`, `HostMemoryReader.TryRead`) go through the host's remap (`HostStructs`) | `UiElementLayoutSearch`, `OffsetHealer.JudgeUiElementBase` (called by `LayoutProbe.ProbeElementBase`) |
| Window cull (`GameCullSize` pattern) | a check only: a value that is no cull of this window (negative, half the width or more) reads 0 and is said lost. Nothing in memory gives it: 2560 x 1440 reads 0, so it is not the 16:10 base resolution's bar (that would be 128) | `GameWindowCull.Judge` |
| In-game and area-loading states (not by slot) | in play: the current state whose area pointer leads to an area with a local player (looked at on every state change, and once a second until found); loading: the state that was current while the in-game state's area pointer changed, twice the same (a wait after the load starts with the new area, a menu ends with the old one) | `GameStateRoles`, `GameStates.ApplyRoles` |
| Other components with a vector, map, owner or entity pointer | GameHelper's own verifier, run with no one at the keyboard (`OffsetHelperEngine.RunSweep`): a repair only when it is not ambiguous; a component with none of these (Chest, Shrine, Player, Positioned, Base, WorldItem, Animated, Charges, Stack, Targetable, TriggerableBlockage) reads intact on its header alone | `OffsetHealer.Verified` |

A struct that holds what it must is never touched. One that moved is read through `StructRemap<T>`: `TryReadMemory<T>`
checks a generic static (one null test on the hot path) and only moved structs pay for a rebuild, so no read site of the host
changes. The plugin reads through the host too: `HostMemoryReader` calls the host's public `HostMemory` (no reflection), so
its `TryRead<T>` is the host's `TryReadMemory<T>` and follows the remap (the area name row, `LocalInventories`,
`UiManagerLocator`, `UiTreeReader`); elements parsed from a block already read go through `HostStructs.Read<T>`, and a field
offset taken by hand through `HostStructs.Follow<T>` (an item's path in `InventoryReader`).
A pattern not found no longer stops the rest (`PatternFinder` threw, and a missing key in `Core` could take the other
coroutines of the event with it). `WorldLayoutWatch` runs the sweep on a settled area, in the first three areas of a game
process (80-110 ms each); a field missing is said, and a new build read with nothing lost says "El juego se ha actualizado:
el mod lo ha encontrado todo" (`config/game-build.txt`).

**Two fields are judged apart from the sweep,** in `WorldLayoutWatch`, because they need live evidence a single look cannot give:

- `Actor.AnimationId` (`AnimationFieldSearch`): nothing in one reading says "animation", but the game keeps a base layer far from the
  animation that shows (0.5.5f: +0x380 and +0x8B0; +0x398 holds a second layer, 1088 when there is none). The two agree unless a layer
  plays on top: never apart for a run or a monk's blow, a third of the time while a crossbow shoots (`IdleLayerBaseStatic` under
  `PowerShotPiercing`; +0x8B0 shows the shot, which is the field the mod wants). A pair counts when both rest at zero, both take a row of
  the Animation table, they agree at least as often as not, they lie far apart and they take more than one value: 4100 readings of 25
  actors gave this one pair and no other, and so did the Mercenary. The one nearest the measured place is taken. It is read ten times a second until judged, normally in the first
  fight, and then not again. Confirming the measured place takes 20 readings and two ids; moving a field takes 200 and three, so a
  coincidence cannot move a healthy one. After some minutes of enemies acting (60000 readings) with no pair, a lone field that still
  behaves is kept, otherwise lost and said, and the search goes on so the next fight can repair it. Only enemies count for that: a
  player who only runs shows one id, and judged sooner it gave a false alarm half a second after arriving. Simulated: the constant set to +0x8A8 came back to +0x8B0 with `Run` and `Idle` read through it.
- `Positioned.Reaction` (`JudgeReaction`): judged with five monsters around, which say foe. The player is always a friend, so a player
  read as anything else means the field moved. Only then is it searched, as the first byte around it that says friend for the
  player and foe for four monsters in five, with three values at most (a byte of a pointer differs from monster to monster).
  The first, because a byte 0x1B after it reads the same on both (+0x1FB) and the nearest would take that one after a shift back.
  A crowd of minions cannot trigger it, as nothing but the player does. A wrong byte here would hide every enemy behind `IsFriendly`.
  Simulated: the constant set to +0x1E8 came back to +0x1E0 on arriving among monsters, 93 ms after the area loaded.

**Tooltips** (`UiSemantics.TooltipRoot`): the offset followed the element names' shift, but moved alone every item, skill and
passive would fall silent with nothing said. Until a tooltip has been read, the bytes around it are searched for a vector of 0x18-byte
entries whose first element has the owner as parent yet is not its child. Simulated: +0x118 came back to +0x120 on the first item focused.

**Why not only the verifier:** a simulated patch (compile-time offsets shifted by 8 and by -0x18, the same as the game moving
them) showed it spotting Life, Buffs and Actor but calling their repairs ambiguous, and not seeing Render at all: the player
projected to (-287, -3187) and it said intact. The shapes above recovered all five, in both directions, to the exact offsets.

**How it was tested:** real data first (upstream's stale 0.5.5e constants against 0.5.5f), then the simulations above, the
entity id and both game state pointers moved, and the `Game States` and `GameCullSize` patterns broken on purpose: each time
the world came back, with enemies, items and chests sounding (the cull does not touch the world: that one proved only that a
missing pattern stops nothing else).
 To repeat one: shift a `FieldOffset` in `GameOffsets`, rebuild
the host, read the log's "Struct sweep" line, then `git checkout -- GameOffsets`.

Host repairs are forgotten when the game closes (`OffsetHealer.Forget`), so a host kept open for the next game process judges it from scratch.

**Simulated 2026-11-08** (the searches added for the [audit](research/field-protection-audit.md)'s gaps; each constant shifted,
the host rebuilt with `--no-incremental` and restarted in the Act 2 town, then reverted):

| Search | Shifted | Outcome |
|---|---|---|
| Entity layout | details +8, path +0x18, components +0x18, lookup -0x18, map +8, all at once | no world, so the area check searched: all five back where measured before the first area read; friends, portal and waypoint sounded |
| Entity layout | details -8, path -8, components -8, lookup +8, map -0x18 | the same |
| Game states | stack +8, list -0x18, and `InGameState` swapped with `PreGameState` in the enum (a state inserted) | stack +0x10 and list +0x50 found at attach, the in-game state taken from slot 4 (`slot 5 -> 4`); "En juego", the sweep and the sounds as usual |
| UI focus, layer roots | focus, last focused, hovered +8; both UI roots +8 | roots back at attach; the focus back on the third move in the game menu (the first focus read the lagging slot) |
| UI focus, layer roots | the same -0x18 | the same |
| UI focus (review fix) | focus +8, the passive tree opened first and its free cursor moved over six nodes | nothing judged in the tree (the hovered slot changed, no follower); the game menu then moved it back (`UI focus moved from +0x750 to +0x748`) |
| Life (inside a vital) | total, current and regeneration +4, and `semantic-fields.txt` set to match (so the code is believed) | the code rules moved the six Life, Mana and ES fields at attach (`Code fields: … moved: LifeOffset.Health.Current +0x1E4 to +0x1E0 …`), the sweep `life moved from +0x30 to +0x2C`; the player read 513/513 |
| Life (inside a vital) | back-pointer -8, regeneration, total and current -4, catalog untouched (the code refused: not where its rules were made) | the sweep alone: `life moved from +0x28 to +0x24`, 513/513 |
| Inventories | player list +8, inventories -0x18, size and cells +8 | the sweep put them back (`inventories moved from +0x50 to +0x48`), the server data read its inventories again: main and cursor inventories watched; the inventory panel named its grid |
| Inventories | player list -0x18, inventories +8, size -0x18, cells -8 | the same |
| Ground items | not simulated: no item lies on the ground in town (dropping one there could lose it); unit test only | — |
| Co-op list, icon row, `is_dead` | `PlayerInfo` +8; icon row 0x28 in the plugin, the host and `code-fields.txt`; `is_dead` one past the table in the enum | `local player list ok` (the area search moved the player back, the list with it); `Code fields: … moved: MinimapIcon.Row +0x28 to +0x20` and the waypoint named by its icon in the `entities` dump (before the names were read again, none was); `Stat is_dead: the game says 2423, the host's enum 2424` |
| Co-op list, icon row | `PlayerInfo` -0x18; icon row 0x18 everywhere | the same |
| Terrain | `TileStructure` size 0x40 and 24 cells per tile |



 terrain found as before, tiles `moved from +0x40 to +0x38`, cells `from +0x18 to +0x17`; the wall radar sounded |
| Terrain | size 0x30 (fields packed) and 22 cells | the same, back to 0x38 and 23 |
| UI element base | self, children, parent +8; position group, flags group, size +0x18 |
 tree back +8, rects and flags +0x18 (the size's shift) before the first walk; the inventory read its items; `layout` all ok |
| UI element base | self, children, parent -8; position group, flags group, size -0x18 | the same |
| Game states | stack -8, list +8, `InGameState` swapped with `EscapeState` | the same (`slot 3 -> 4`). The loading state needs two area changes (left town is not allowed in these tests): unit test only |



**Upstream:** the host is no longer a patched GameHelper2 clone (2026-11-08): its reader lives in `host/`, so an offset that
upstream learns is copied by hand into `host/GameOffsets` (upstream had no commit from 2026-09-14 on).

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
| UI focus, on its own: while the player moves through a menu, the slot that takes each element of this manager first and whose previous-focus slot (8 after it) gets that element when the focus moves on; four moves judge it, and the measured slot keeps its place once it moved. The hovered slot, filled by the passive tree's free cursor with nothing focused, has no such follower and is never taken (the inventory grid moves its cursor, not its focus, so a button menu judges it) | last focused, hovered | `LayoutProbe.ObserveFocus` (pure `FocusSlotCheck`) |
| In-game layer roots (InGameState's mouse and gamepad UI roots): an element whose children carry two known layer names; neither slot holds one, the nearest slot that does gives both their shift | — | `LayoutProbe.ProbeLayerRoots` |
| In-game layers by name (`HUD`, `map_overlay`, `death_screen`, `skill_bar`, `bind_management_display`) or shape (inventory: child `panels_parent`; chat: `textField` and `scrollbar` below) | the objective box (no name or shape) follows the nearest one found | `InGameFields` |
| Entity names: half of 30 monsters, characters and players read a name | — | `LayoutProbe.ProbeRenderNames` |
| Targetable in-play flag: set on what is always in play (players, waypoint, stash, checkpoint, living monsters) and, of an NPC's story variants (one Render name), on at most one; the nearest byte that fits when the measured one does not | targeted flag; the measured byte is used while unjudged, everything counts as in play once lost | `EntityFields` (pure `InPlayFlag`) |
| NPC quest marker: the NPC component's dat reference leads to the MiscAnimated row `NPCQuestMarker…` on some NPC (a town with no marker proves nothing; the self-test lists the row ids it sees) | — | `EntityFields.HasMarkerAt` |
| Control flag bits: interactive is on most controls with a click sound and on no plain label; text input on combo boxes and text fields and on no label (some buttons carry it too); navigable on what the in-game controller focus lands on, rarely on its container (grids left out); a bit is kept while half its carriers still have it | — | `FlagBits` (pure `BitSearch`) |
| Enabled flag bit: nothing tells it apart (a disabled control is rare), so it is only noticed lost (under 20% of a screen's controls): then every control counts as enabled | — | `FlagBits.EnabledTrusted` |
| Pad state: sticks within ±1, triggers within 0..1, else no input (no phantom L3 gestures) | — | `GamepadReader` |
| Pad buttons: the left trigger is reported both as a pressure and as a button bit; the bit set in nearly every frame it is down alone and in nearly none with both up gives how far all the buttons moved | every button | `PadButtonCheck` |
| Passive tree graph: the tree state holds the graph file (named `Metadata/PassiveSkillGraph.psg`); its content an edge list whose sampled edges join node records; the state's allocated list holds only graph ids of those records (gathered off the frame thread) | links, allocations | `PassiveTreeFields` |
| Tile grid (GameHelper's `TileDetailsPtr`, read raw): record size = the grid's bytes over its sub-tiles; the file pointer and path where half the sub-tiles name a `.tdt`; the three id bytes where neighbours from one file differ by one step (90% in the Act 1 town) and selectors stay 0..7; searched on every grid, and a layout the search finds wins (the reading's own check looks at the paths and selectors only) | place names, props | `TileGridSearch` (pure), `PlaceNarrator` |
| Item base name (GameHelper's `BaseOffsets`, no static check): a normal or magic item's tooltip title holds it (each item counted once, case ignored; ground labels do not count); two titles in a row that miss start a search of the row pointer and the name nearby, taken once a second title agrees; none anywhere: titles are used instead | — | `BaseNameField` (pure), `InventoryReader` |
| Local player list: entry 0 is GameHelper's player and there are at most two (else no player 2, the partner radar stays silent); read beside the local player field the area search found, so it moves with it (`layout`: "local player list ok") | — | `LocalPlayers` (pure `LocalPlayerList`) |
| Minimap icon row (the host's icon names, which classify NPCs and exits): the code catalog's `layout:MinimapIconComponentRow` moves the host's `MinimapIcon.RowOffset` with the plugin's, and names read before it moved are read again | — | `MinimapIcon.RowOffset` |
| Player 2's focus, hover and pad state: player 1's UI manager fields plus `PlayerStride` (0x240); they move with player 1's (`LayoutProbe`), and the pad is read only while it is plausible | — | `FocusTracker.Seat`, `GamepadReader` |
| Player 2's server inventories: ServerData's player list (+0x48) has a second entry with an inventory list (+0x320); unreadable or missing, player 2 has no pick-up, hand or flask news | — | `LocalInventories` |
| Player 2's skill bar: player 1's located field plus `PartnerSkillBarShift`, named `skill_bar` and another element than player 1's | — | `InGameFields.PartnerSkillBar` |
| Skill bar slots: the class found twice in each of the twelve frames, alone (children 1..12) or in co-op (nested in the first child) | — | `UiSemantics.SlotElements` |
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
| Character creation's Continue: nothing fixed to judge it by, so it follows the text; a wrong byte only adds or loses "Name too short" | grey byte | `UiSemantics.ContinueGrey` |
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

**Dev commands after a patch:** `structs` runs the sweep and logs it ("Struct sweep: 18 intact, 0 moved, 0 lost, 9 not
judged"; those have no field that can be checked, or nothing live to judge it on, such as a ground item in town). `sounds` logs what really played since last asked ("output peak 0,950;
started: enemy 128, boss 42, dodge 5…; beds heard: waypoint"): the oracle for audio, since a log of speech says nothing of it.

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
`GameDataSetup` stamps the install (`app/GameData/game-version.txt`: size and time of `Bundles2/_.index.bin` and of the
exe, `fmod.dll` product version, each step's version) and, when the install changed, rebuilds every catalog made from
the game's files and the sounds from the player's own install (about 35 s, each step independent, old files kept on
failure); a mod update reruns only the steps whose version it raised (`tools/README.md`). The FMOD header version is read from `fmod.dll` (`SoundExtractor`).

`object-sounds` follows the game's own link from an object to its sound (Chests row, `.ao`, FMOD event), so an object a patch
adds has its sound with nobody extending the word list in `Breakables`. If the render fails the objects fall back on the
common break and the other sounds are safe, because it is a separate process. The AOFiles column at +0x15 is read by
offset: a patch moving it leaves the list empty, which costs these sounds and nothing else.

`game-ids` (`GameIdExtractor`) writes the game's numbers for what the mod knows by name, because GameHelper's enums were
generated long ago and a row inserted in a table shifts every name after it. In 0.5.5f the Animation table has four rows the enum
lacks (584, 599, 904, 1087): 500 of its 1084 names were wrong, and every animation the dodge warning sorted by name after row 583
was sorted by another's (Faridun Olroth's `MoltenSlam` read as calm, as did Doryani's beam). Stats moved the same way: Rage read the
stat next to it (`current_rage` is 6996, the enum says 6995). Names now come from the table (`AnimationNames`, `GameIds`), and the
enum only stands in with no catalog. A stat's key is its row plus one, read live: key 1 is `level`, 22270 is
`total_socketed_red_skill_support_gems` with 2, where the enum says a freeze multiplier. Only the stats the mod reads are listed;
the host's dormant-monster check takes `is_dead` from the same list (`Entity.DeadStat`, set by the plugin; 2423 in 0.5.5f, as the enum).

`game-keys` (`GameKeyExtractor`) lists the Ids of the tables a player meets and the `.ui` screens. At start the plugin compares it
with the list it saw last (`config/game-keys-known.txt`, `GameNews`): what a patch added is logged, written to
`config/game-news.txt` with what the mod makes of each new buff, and said once in play ("Novedades de esta actualización del juego:
efectos 9, zonas 23. La lista está en el archivo game-news.txt"). This is what the mod cannot know by itself: a new buff passes
in silence if no rule names it, a new screen needs a look. For whoever keeps the mod the same list is in git: the diff of
`catalog/game-keys.txt` is the patch. The first run has nothing to compare with and says nothing.

A regenerated catalog under half the size of the old one is refused and the old one kept (`GameDataStamp.Collapsed`): a column that
moved with the row size unchanged reads few rows and exits with no error. A step that fails leaves its name in
`catalog/game-data-failed.txt`, and the plugin says it ("parte de los datos del juego tras su actualización"), which until now
stayed in the launcher's log.

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

## Read from the game's code
Some fields have nothing in memory to recognise them by: a level, an opened flag, a rarity. `catalog/code-fields.txt` holds 50
of them with the machine code that reads each, wildcarded so only the operand is taken (`CodeFieldCatalog`, `CodeFieldSearch`,
called from `CheckCodeFields`; 0.6 s at attach, on the exe on disk, nothing touches the game). A value counts only when two
sequences hit exactly once and agree; a miss keeps the constant, and a miss is the usual result for a function a patch
recompiled. What the code settles:

- a host struct field moves through `OffsetHealer.PlaceFromCode`, unless a live judgment already placed it, because memory beats code;
- a plugin offset (`layout:`, `LayoutFields`) is set;
- a `report` field, read where a remap does not reach, is only said ("parte de los datos del juego").

`PlaceFromCode` refuses a field that is not where its signatures were made: when the first run flagged a move on the unchanged
build, it was a wrong line in the catalog (the plugin's skill key is +0x58, the host's +0x48 is another field), so a wrong line
must move nothing. **After changing a `FieldOffset` these fields use, update the value in the catalog**, and on any new build
read the line `Code fields: 51 of 51 read in … ms; moved: none`. A value more than 0x400 from the catalog's is not believed.
Six of the 51 lines (Transitionable state, the status effect times, the stack size data, the tile height) are fields the mod
does not read: they count in the 50 but protect nothing heard.
Simulated 2026-11-06: `PlayerOffsets.Level` at +0x20C went back to +0x204 and the level read 19; `TerrainStruct.BytesPerRow` at
+0x138 went back to +0x130 and the world read as usual; `Shrine.IsUsed` off by 8 gave the spoken warning. None of these fields has moved in a
real patch yet, so this is proved on simulations and on five past builds that all read the same.

## After a real patch
The first patch after this work landed by chance at 02:42 on 2026-11-06, with nothing prepared for it: a 73 MB hotfix that moved no
field. The mod said "El juego se ha actualizado: el mod lo ha encontrado todo", the sweep read 17 intact and 0 lost, and enemies, gold and
the radar sounded. `tools/exe_reader/reader.py` tells what moved from the exe alone, before starting the game ([study](research/exe-layouts.md)).

## Still needs a person after a patch
- Normally nothing: the host follows its own offsets (above). When something is said lost, follow the patch-day
  runbook ([patch day](patch-day.md)). The plugin says what it lost: the world ("lo que hay a tu alrededor, así que
  enemigos, objetos y salidas no sonarán"), a struct by name, or attached two minutes without ever reading a game state
  (`GameStateService`, part "game states": "no encuentro el estado del juego (GameHelper necesita una actualización)").
- A part the self-test reports lost after a patch: each search assumes one change at a time near where the field was
  measured, so a redesign (a new structure, a field moved far) still needs a new measurement. A wrong part makes its
  reading fall back (a mod without its name, an item without its base name) rather than speak garbage, since every
  string goes through the decoders' checks.
- Run the `layout` self-test (above) and a short session through login, a menu, the inventory and a fight.

## What a search cannot recover
Asked 2026-10-24 to leave nothing fragile. This section used to say every field the plugin reads has a check; an audit on
2026-11-06 showed it did not, and the same day closed that list:

- Heavy stun (`PoiseSearch`, `JudgePoise`): the record starts {int 5, float 8.0, int 0} in every monster, so five monsters that
  agree place it, nobody needing to be hit. Live: found where measured; a constant set 8 too low was corrected.
- Walkable grid (`WalkableCheck`, `JudgeWalkable`): 30 cells the player stood on must read walkable, at least 70% of them. It
  can only tell, not repair. Live: passed; with the reading inverted it said the part was lost.
- Name columns: `Characters.Name`, `WorldAreas.Name`, `WorldAreas.IsTown` and the MinimapIcons row size come from
  `dat-columns`, the WorldAreas ones through `PlaceFromCode`. BaseItemTypes and MonsterVarieties names are found the same way by
  their extractors, and in memory the base name has its own search (`BaseNames`).
- Extractors: MonsterVarieties' lists, the skill's animation key and the Chests AOFiles are found by what they hold, starting
  from the measured offset: with every one of those offsets set wrong, `hit-times` came out byte for byte the same. The other
  tables have a row size guard (`HitTimeExtractor.Sized`): a table that changed is left out and the old file stays.
- Code signatures closed the rest: the terrain's six fields (`AreaInstanceLayout.ReadTerrain` and `IsTerrain` follow the remap),
  `ButtonMenu.SelectedIndexOffset`, `NodeInBuildOffset`, `PlayerStride`, and the stun fields as a second source.
- `ModelBounds` needs no signature of its own: the Y and Z reads place the whole tuple. `TextColorOffset` follows `LayoutProbe`.
  `WorldArea` Act is not read by the plugin.

The code also says two things the mod does not use: +0x348 is a 64-bit optional index with a has-value byte at +0x350, so the
index could be stale while unset (the mod reads the int, as tested live), and +0x4E0 is a float, which reads the same at zero.

What else remains is by design:

- A search finds a field moved near where it was measured (within 0x40 to 0x100 bytes, or a bit renumbered), one
  change at a time; a field moved farther, or two fields of one judgment moved at once, is reported lost.
- Parts whose evidence is rare are judged late: the quest marker needs an NPC that shows one, check boxes and
  selectable lists a change by the player; the enabled bit is only noticed lost.
- A structure redesigned rather than shifted (a component split in two, a list that becomes a map): its readings stop and the mod
  says so. No search can promise more; what it promises is that the common patch costs nothing, and that the rest is heard, not
  guessed from silence.
- Of the six byte patterns only `Game States` and `GameCullSize` matter; `Game States` has a search, `GameCullSize` (the
  window cull the UI rects use) only a plausibility check (above). The other four (`File Root`,
  `AreaChangeCounter`, the two terrain rotation ones) were broken on purpose on 2026-11-06: the sweep, enemies, radar and walls went
  on as before, since nothing the plugin reads goes through them. Build with `--no-incremental` to try it: a pattern edit alone
  left `GameOffsets.dll` unbuilt.
