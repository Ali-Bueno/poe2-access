# Field protection audit

Which fields the mod reads have fewer than two independent ways to be found again after a game patch. Read-only audit of the
code at commit `e9fdbd2`, 2026-11-08 (build 0.5.5f offsets); the game's exe was not analysed. Sources: `docs/compatibility.md`,
`Poe2Accessibility/catalog/code-fields.txt` (`Lnn` below is its line `nn`), `host/GameOffsets/Objects/**`, `host/GameHelper/**`,
`Poe2Accessibility/**`.

## How to read the tables

- **n** is how many fields share the row (same struct, same protection).
- **N** is the number of independent ways that can *move the field* to its new place. It counts: **SIG** (byte signature in
  `code-fields.txt`; two sequences must agree, `CodeFieldSearch.cs:40`), **LIVE** (a shape or value judged on live memory, named),
  **NAME** (anchored on a name the game holds), **DAT** (dat-columns extractor), **FOLLOW** (shifts with another field's anchor, so
  not independent of it; marked `(f)` and counted as 1).
- Not counted, listed so the picture is complete: **CHECK** (the reading is validated, so a break is noticed, but nothing repairs
  it), **REPORT** (signature that only says the field moved, `code-fields.txt` target `report`). A break of a CHECK-only field makes
  the reading fall back or go silent without a repair.
- **If lost**: *world silent* (enemies, items, exits, radar stop), *menus silent*, *one feature* (named), *cosmetic*.
- Fields that have no run-time protection are marked `none`. Where the doc and the code disagree, see section 3.

## 1. Audit table

### 1.1 Game states and static pointers

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| G1 | static "Game States" pointer | 1 | `StaticOffsetsPatterns` | everything | pattern; LIVE `GameStatesSearch` only when the pattern is absent (`GameProcess.cs:272`) | 2 | world and menus silent | said after two minutes with no state (`GameStateService`) |
| G2 | `CurrentStatePtr` +0x10 (second-last stack entry) | 1 | `GameStateOffset` | current state (login, select, loading, in game) | CHECK: `GameStatesSearch.IsStateList` validates it, at the measured offset, on the fallback path only | 0 | menus and world mode silent | no relocation: the search takes the measured offsets as given (`GameStatesSearch.cs:45-71`) |
| G3 | `States` +0x50 and slot indexes (0 = AreaLoading, 4 = InGame) | 2 | `GameStateOffset` | state addresses, `InGameStateObject.Address` | none | 0 | world silent | indexes are constants (`GameStates.cs:85-86`); a reorder of the 13 states is not noticed |
| G4 | `GameCullSize` static (black-bar width) | 1 | pattern only | UI rect x (`PluginUiElementReflection.cs:133`) | pattern | 1 | cosmetic: UI rects shifted on non-16:9 windows | no search exists (doc says one does, 3.1 item 1); feeds on-screen tests and passive-tree geometry |

Not audited (not read by the mod): the other four patterns (`File Root`, `AreaChangeCounter`, the two terrain rotation ones;
`Core.cs:170-250`, the preload scan is left off in `UpdateFilesData`, `Core.cs:183-196`), `AreaLoadingStateOffset` (`IsLoading`, `TotalLoadingScreenTimeMs`,
`CurrentAreaDetailsPtr`: only `LoadedFiles.cs:107` reads them), `WorldAreaDatOffsets.Act/HasWaypoint`, `Transitionable.CurrentState`,
`StatusEffect.TimeLeft/TotalTime`, `Stack.StackSizeDataPtr/MaxStack`, `TerrainStruct.TileHeightMultiplier`, `AreaInstance.Environments`
(L14, L19, L20, L25, L26, L54 are signatures for fields the plugin never reads).

### 1.2 InGameState and area

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| A1 | `AreaInstanceData` +0x290 | 1 | `InGameStateOffset` | the whole world | LIVE `OffsetHealer.LocateArea` (a block that holds a world) | 1 | world silent | runs only after 8 s with no entities, every 3 s (`WorldLayoutWatch.cs:95-97`) |
| A2 | `WorldData` +0x368 | 1 | `InGameStateOffset` | camera, area details | LIVE `JudgeCamera` then `LocatePointer` (`OffsetHealer.cs:322`) | 1 | one feature: screen projection (radar falls back to a fixed view, `ScreenProjection.cs:52-60`) | |
| A3 | camera matrix (+0x98 then +0x108) | 1 | `WorldDataOffset` | direction cues | LIVE `IsCameraMatrix` (matrix kept twice) | 1 | one feature: as A2 | |
| A4 | `WorldAreaDetailsPtr` +0x98 and `WorldAreaDetailsRowPtr` +0x98 | 2 | `WorldDataOffset`, `WorldAreaDetailsStruct` | `InTownOrHideout` (`Poe2AccessibilityCore.Gameplay.cs:122,124,341`) | none | 0 | cosmetic: lost hint and map hints not suppressed in town | `WorldData.cs:150-154`; `JudgeCamera` remaps from the matrix offset only |
| A5 | `UiRootStructPtr` +0x2F0, `GamepadUiRootStructPtr` +0x318 | 2 | `InGameStateOffset` | in-game layer container (`UiManagerLocator.cs:103-112`) | CHECK `IsElement` | 0 | one feature: every in-game panel (HUD, inventory, skill bar, chat, death screen) | the manager is still found by a state scan (A7), the layer container is not |
| A6 | `MouseOverHostPtr` +0x300, `MouseOverSubPtr` +0x3F0, `EntityPtr` +0xA8 | 3 | `InGameStateOffset`, `MouseOver*Struct` | mouse-hover target (`TargetNarrator.cs:105`) | none | 0 | one feature: hover narration (controller target display is independent) | `InGameState.cs:94-105` |
| A7 | `InGameState.UiManagerOffset` 0x310 | 1 | plugin | UI manager | direct slot (validated by `IsManager`); scan of the state object for element pointers whose manager two elements agree on (`UiManagerLocator.FromStateElements`) | 2 | menus silent | |
| A8 | `CurrentAreaLevel` +0xC4 | 1 | `AreaInstanceOffsets` | area level speech | LIVE `AreaLayoutSearch.IsHeader` | 1 | cosmetic | `Poe2AccessibilityCore.Gameplay.cs:224` |
| A9 | `CurrentAreaHash` +0x11C | 1 | `AreaInstanceOffsets` | area identity (explored map, death place, map hints) | FOLLOW level at the measured 0x58 distance; CHECK value >= 0x10000 | 1 (f) | one feature: area memory and guide | if the distance changes it is not found (`AreaLayoutSearch.cs:117-120`) |
| A10 | `LocalPlayerPtr` (+0x5B8+0x20) | 1 | `LocalPlayerStruct` | player entity | LIVE `IsLocalPlayer` (path under `Metadata/Characters/`) | 1 | world silent | |
| A11 | `ServerDataPtr` (+0x5B8) | 1 | `LocalPlayerStruct` | flasks, inventory news | LIVE `IsServerData` (falls back to the player gap if nothing fits) | 1 | one feature: flask and inventory news | `AreaLayoutSearch.cs:231-236` |
| A12 | `PlayerServerDataPtr` +0x48, `PlayerInventories` +0x320, `InventoryArrayStruct` layout | 3 | `ServerDataOffsets`, `ServerDataStructure` | `LocalInventories`, `ServerData.PlayerInventories` | CHECK: criteria of `IsServerData`, nothing relocates them | 0 | one feature: pick-up, hand, flask news, salvage | plugin reads them with the raw reader (3.1 item 6) |
| A13 | `TotalBoxes` +0x150, `ItemList` +0x170, `InventoryItemStruct` (Item, SlotStart, SlotEnd) | 3 | `InventoryStruct` | `FlaskWatch`, `InventoryWatch` | none | 0 | one feature: flask belt, pick-up and salvage news | not a sweep component; no check even of the `X*Y*8` length invariant |
| A14 | `Entities` (awake and sleeping maps) +0x718 | 1 | `AreaInstanceOffsets` | all entities | LIVE `IsEntityLists` | 1 | world silent | the MSVC node shape (nil flag 0x19, key 0x20, value 0x28) is a constant of that check |
| A15 | `TerrainMetadata` +0x8F8 | 1 | `AreaInstanceOffsets` | walls, routes, exits | LIVE `IsTerrain` | 1 | world silent | |
| A16 | local player list `PlayerInfo`+0x08 | 1 | plugin | co-op partner (`LocalPlayers.cs:17-18`) | CHECK `LocalPlayerList` (at most two) | 0 | cosmetic: no player 2 | built from the compile-time struct, not from `area.Layout.Fields`, so it does not follow the area search |

### 1.3 Terrain

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| T1 | `TotalTiles` +0x18 | 1 | `TerrainStruct` | `IsTerrain`, grid | SIG L49; `IsTerrain` relation (tile bytes = X*Y*0x38) is detect-only | 1 | world silent | `AreaLayoutSearch.cs:302-320` follows the remap |
| T2 | `TileDetailsPtr` +0x28 | 1 | `TerrainStruct` | `IsTerrain`, place names | SIG L50; `IsTerrain` | 1 | world silent | |
| T3 | `GridWalkableData` +0xD0 | 1 | `TerrainStruct` | walls, routes, sight | SIG L51; `IsTerrain`; `WalkableCheck` (detect only) | 1 | world silent | |
| T4 | `BytesPerRow` +0x130 | 1 | `TerrainStruct` | every grid index | SIG L53; `IsTerrain`; `WalkableCheck` | 1 | world silent | |
| T5 | `GridLandscapeData` +0xE8 | 1 | `TerrainStruct` | enemy line of sight (`EnemyCues.cs:309`) | SIG L52 | 1 | one feature: sight layer | not part of `IsTerrain` |
| T6 | `TileStructure` size 0x38 and `TileToGridConversion` 0x17 | 2 | `TileStructure` | `IsTerrain` (`AreaLayoutSearch.cs:94,318-319`) | none (`TileGridSearch` finds a record size, but only for place names; it does not feed `IsTerrain`) | 0 | world silent: terrain never recognised | |
| T7 | `Stride` 0x38, `FileOffset` 8, `PathOffset` 8, `IdsOffset` 0x34 | 4 | plugin `TileGrid` | place names, props | LIVE `TileGridSearch` | 1 | cosmetic | |

### 1.4 Entity

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| E1 | `Id` +0x88 | 1 | `EntityOffsets` | every entity | LIVE `AreaLayoutSearch.FindEntityId` | 1 | world silent | |
| E2 | `IsValid` +0x8C | 1 | `EntityOffsets` | every entity | FOLLOW `Id` (`OffsetHealer.cs:151-158`) | 1 (f) | world silent | doc lists it as found by the key (3.1 item 4) |
| E3 | `ComponentListPtr` +0x10 | 1 | `EntityOffsets` | component access | LIVE sweep vector anchor (`OffsetHelperEngine.cs:983`, `OffsetHealer.Verified`) | 1 | world silent | |
| E4 | `EntityDetailsPtr` +0x8 and `EntityDetails.name` +0x8 (the path) | 2 | `ItemStruct`, `EntityDetails` | classification of everything, `IsLocalPlayer` | none (a break shows as "no world") | 0 | world silent | `GameMemory.EntityPath` (`AreaInstanceLayout.cs:129`) |
| E5 | `ComponentLookUpPtr` +0x28, `ComponentsNameAndIndex` +0x28, name/index entry | 3 | `EntityDetails`, `ComponentLookUpStruct` | every `TryGetComponent` | none (component names are NAME anchors for *which* component, the table's place is unprotected) | 0 | world silent | `Entity.cs:298-310` |

### 1.5 Components the plugin reads

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| C1 | `CurrentWorldPosition` +0x138 | 1 | `RenderOffsets` | every distance and direction | LIVE `JudgePosition` | 1 | world silent | not judged while the camera is off the player |
| C2 | `CharactorModelBounds` X/Y/Z +0x144 | 3 | `RenderOffsets` | body-edge gaps, weak-point guide | SIG L31 (Y), L32 (Z); X follows | 1 | one feature: enemy and boss gaps | X (the one used) is never read by the signatures |
| C3 | `RenderComponent.NameOffset` 0x160 | 1 | plugin | entity names | LIVE `ProbeRenderNames` | 1 | cosmetic | judged once per game |
| C4 | `Health` +0x1B0 | 1 | `LifeOffset` | `IsAlive` (33 uses, gates every enemy), player life | LIVE `StructShapes.PlaceVitals` | 1 | world silent | excluded from the verifier (`OffsetHealer.cs:69-73`) |
| C5 | `Mana` +0x208, `EnergyShield` +0x248 | 2 | `LifeOffset` | mana and shield alerts | LIVE `PlaceVitals` (same measurement) | 1 | one feature: alerts | |
| C6 | `VitalStruct` back-pointer +0x8, `Total` +0x2C, `Current` +0x30 | 3 | `VitalStruct` | `IsAlive`, percentages | CHECK: parameters of the shape (`StructShapes.cs:85-98`) | 0 | world silent | a change inside the vital reads as "no vitals found" |
| C7 | `Reaction` +0x1E0 | 1 | `PositionedOffsets` | `IsFriendly` (every enemy) | SIG L30; LIVE `JudgeReaction` | 2 | world silent | |
| C8 | `AnimationId` +0x8B0 | 1 | `ActorOffset` | dodge and parry warnings, enemy states | LIVE `AnimationFieldSearch` | 1 | one feature: combat warnings | judged in the first fight |
| C9 | `ActiveSkillsPtr` +0xB08 | 1 | `ActorOffset` | skill names, cooldown pairing | LIVE `IsSkillList` | 1 | one feature: skills | `SkillsOffset` follows (`WorldLayoutWatch.cs:277-278`) |
| C10 | `CooldownsPtr` +0xB20 | 1 | `ActorOffset` | cooldown cues | LIVE `IsSkillList` (vector right after the skills, multiple of 0x48) | 1 (f) | one feature: cooldown cues | follows the skills' shift; no own check |
| C11 | `SkillEntrySize` 0x10 | 1 | plugin | skill list stride | CHECK `IsSkillList` (bytes % 0x10) | 0 | one feature: skills | |
| C12 | cooldown entry: `EntrySize` 0x48 (L43), `Uses` 0x10 (L39), `MaxUses` 0x30 (L40), `Time` 0x34 (L41), `SkillId` 0x3C (L42) | 5 | plugin | `SkillCooldownWatch` | SIG | 1 | one feature: cooldown cues | |
| C13 | `ActiveSkillDetails.SkillId` +0x40 (host and plugin) | 2 | `ActiveSkillDetails` | skill identity | SIG L27, L28 | 1 | one feature: skill names | |
| C14 | `LevelKey` +0x58 | 1 | plugin | skill names | SIG L29 | 1 | one feature | |
| C15 | `PoiseThreshold` 0x4E4, `Poise` 0x4E8 | 2 | plugin | heavy-stun bar | SIG L44-45; LIVE `PoiseSearch` | 2 | one feature: stun bar | |
| C16 | `StatusEffectPtr` +0x160 | 1 | `BuffsOffsets` | ailments, hazards, enemy states, hidden-monster filter | LIVE `IsBuffList` (not judged with no buff on the player) | 1 | one feature family: statuses | |
| C17 | `BuffDefinationPtr` +0x8 and `BuffDefinitions.Name` +0x0 | 2 | `StatusEffectStruct` | buff ids | CHECK: the chain `IsBuffList` walks | 0 | one feature family: statuses | |
| C18 | `StatusEffectStruct.Charges` +0x40 | 1 | `StatusEffectStruct` | charge speech | SIG L18 | 1 | one feature: charges | |
| C19 | `StatsChangedByItemsPtr` +0x160 | 1 | `StatsOffsets` | rage and stat readouts, stun trace | LIVE `IsStatsPointer` | 1 | one feature: stats | |
| C20 | `StatsChangedByBuffAndActions` +0x1C8, `CurrentWeaponIndex` +0x168 | 2 | `StatsOffsets` | stats, weapon set | FOLLOW the +0x160 segment; the index also CHECK 0 or 1 (`InventoryWatch.cs:296`) | 1 (f) | one feature: stats, weapon-set news | |
| C21 | `StatsStructInternal.Stats` +0xF8 and the key/value pair layout | 2 | `StatsStructInternal` | stats | CHECK: parameters of `IsStatsPointer` | 0 | one feature: stats | |
| C22 | `Name` +0x1B0, `Xp` +0x1D8, `Level` +0x204 | 3 | `PlayerOffsets` | level-up, other players' names, XP | SIG L15-17; level range check | 1 | cosmetic | |
| C23 | `ObjectMagicProperties.Rarity` (+0x144) | 1 | `ObjectMagicPropertiesOffsets` | monster rarity cues, target narration | SIG L5 | 1 | one feature: rarity cues | the sweep's vectors start after it (+0x150) and do not cover it |
| C24 | `Mods.Rarity` +0x94 | 1 | `ModsOffsets` | item rarity speech | SIG L6 | 1 | one feature: item rarity | |
| C25 | `ImplicitMods` +0x150, `ExplicitMods` +0x168 | 2 | `ObjectMagicPropertiesOffsets` | monster mod names | SIG L7-8; LIVE sweep vector anchor | 2 | cosmetic | |
| C26 | `ModArrayStruct.ModsPtr` +0x28 and stride 0x40 | 2 | `ModArrayStruct` | monster mod names | REPORT L36-37 | 0 | cosmetic | `Values` stride 4 is hard-coded (`ObjectMagicProperties.cs:94`) |
| C27 | `IsTargetable` byte 0x69 | 1 | plugin | in-play filter | SIG L33; LIVE `InPlayFlag` | 2 | one feature: in-play filter (everything counts as in play when lost) | |
| C28 | `IsTargeted` byte 0x6B | 1 | plugin | interaction target | SIG L34; FOLLOW the flag's shift (`EntityFields.cs:173`) | 2 (f) | one feature: interaction target | |
| C29 | NPC marker row 0x50 | 1 | plugin | quest marker | LIVE `EntityFields.JudgeMarker` (needs an NPC that shows one) | 1 | cosmetic | |
| C30 | `ChestsDataPtr` +0x160 (L9), `IsOpened` +0x168 (L10) | 2 | `ChestOffsets` | chest radar | SIG | 1 | one feature: chests | |
| C31 | `IsLabelVisible` +0x21 (L11), `StrongboxDatPtr` +0x50 (L12) | 2 | `ChestsStructInternal` | chest, breakable, strongbox kind | SIG | 1 | one feature: chests | |
| C32 | `Shrine.IsUsed` +0x24 | 1 | `ShrineOffsets` | shrine radar | REPORT L38 | 0 | one feature: used shrines still announced | |
| C33 | `IsBlocked` +0x30 | 1 | `TriggerableBlockageOffsets` | barriers, doors | SIG L13 | 1 | one feature: barriers | |
| C34 | `StatesValues` +0x160 | 1 | `StateMachineComponentOffsets` | trial traps | LIVE sweep vector anchor | 1 | one feature: traps | entry layout hard-coded (`StateMachine.cs:12,32`: +0x10, 0xC0) |
| C35 | charges: `InternalPtr` +0x10 (L22), `current` +0x18 (L21), `PerUseCharges` +0x18 (L23) | 3 | `ChargesOffsets` | flask charges | SIG | 1 | one feature: flasks | |
| C36 | `Stack.Count` +0x18 | 1 | `StackOffsets` | stack counts in item speech and pick-up news | SIG L24 | 1 | cosmetic | |
| C37 | `BaseItemTypesRowPtr` +0x18, `DisplayNameRowPtr` +0x10 | 2 | `BaseOffsets` | item base name | LIVE `BaseNameField` (tooltip titles are the fallback, not a re-find) | 1 | cosmetic | |
| C38 | `ItemEntityPtr` +0x28 | 1 | `WorldItemOffsets` | ground items (radar, labels) | none (the plugin validates the entity path afterwards) | 0 | one feature: loot | |
| C39 | `AnimatedEntityPtr` +0x280, `ModelInfoPtr` +0x358, `ModelFileRecordPtr` +0x18 | 3 | `AnimatedOffsets` | map-hint model path | none | 0 | cosmetic | |
| C40 | minimap icon row pointer +0x20 (host `IconName`) | 1 | `MinimapIcon.cs` | NPC and exit classification (`EntityCatalog.Classify.cs:45,333`) | none: hard-coded in the host (`MinimapIcon.cs:60`); SIG L35 only moves the plugin's `MinimapIconLabel` | 0 | one feature: icon-based classification | 3.3 |

### 1.6 UI element base (host `UiElementBaseOffset`) and plugin element fields

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| U1 | `Self` +0x08, `ChildrensPtr` +0x10 | 2 | `UiElementBaseOffset` | every tree walk | CHECK `IsElement` | 0 | menus silent | the sweep probe exists but its result is dropped (`OffsetHealer.cs:69-73,106`) |
| U2 | `ParentPtr` +0xB8 | 1 | `UiElementBaseOffset` | roots, panels, 36 reads | CHECK | 0 | menus silent | |
| U3 | `Flags` +0x168: visible bit 0x0B, position-modifier bit 0x0A | 2 | `UiElementBaseOffset` | `IsVisibleFlag` (74 uses), rects | none (`FlagBits` judges only the plugin's own bits) | 0 | menus silent or reading hidden controls | |
| U4 | `RelativePosition` 0x100, `PositionModifier` 0x108, `LocalScaleMultiplier` 0x118, `ScaleIndex` 0x172, `UnscaledSize` 0x270 | 5 | `UiElementBaseOffset` | on-screen rects (`PluginUiElementReflection.cs`) | CHECK finite and size > 0 | 0 | one feature: positions, on-screen tests, passive-tree geometry | |
| U5 | `Vtable` +0x0 | 1 | `UiElementBaseOffset` | list item kinds (`UiSemantics.Lists.cs:488`) | CHECK | 0 | cosmetic | |
| P1 | `Element.NameOffset` 0x98 | 1 | plugin | roles, layers, every name test | LIVE `ProbeNames` | 1 | menus silent | the layer search (L-rows) depends on it |
| P2 | `ManagerOffset` 0x90 | 1 | plugin | manager lookup | FOLLOW name | 1 (f) | menus silent | also `AdoptManager` |
| P3 | `FontOffset` 0xC0 | 1 | plugin | text-field hint test | FOLLOW name | 1 (f) | cosmetic | |
| P4 | `TooltipsOffset` 0x120 | 1 | plugin | item, skill, passive tooltips | FOLLOW name; LIVE `TooltipRoot` search (`UiSemantics.Tooltips.cs:54`) | 2 | one feature: tooltips | |
| P5 | text: tagged 0x360, text 0x490, copy 0x538 | 3 | plugin | all label text | LIVE `ProbeText` (one measurement) | 1 | menus silent | |
| P6 | `TextColorOffset` 0x6D0 | 1 | plugin | colour words | FOLLOW text | 1 (f) | cosmetic | |
| P7 | `Image.TextureOffset` 0x240 | 1 | plugin | icon-only elements | LIVE `ProbeTextures` | 1 | one feature: image-only items | |
| P8 | `AltTextureOffset` 0x238 | 1 | plugin | smaller image class | FOLLOW texture | 1 (f) | cosmetic | |
| P9 | flag bits: interactive 0x1B, navigable 0x0E, text input 0x10 | 3 | plugin | roles on pre-game screens | LIVE `FlagBits` | 1 | one feature: control roles | |
| P10 | enabled bit 0x0D | 1 | plugin | disabled controls | CHECK: only noticed lost (`FlagBits.EnabledTrusted`) | 0 | cosmetic: all read enabled | |
| P11 | `UiManager.RootOffset` 0x688 | 1 | plugin | manager root | LIVE `AdoptManager` | 1 | menus silent | |
| P12 | `CurrentOffset` 0x748, `LastFocusedOffset` 0x750, `HoveredOffset` 0x760 | 3 | plugin | focus tracking (the core of menu reading) | FOLLOW the root slot; nothing checks them on their own | 1 (f) | menus silent | if they move without the root they are not found |
| P13 | `PadStateOffset` 0x768 | 1 | plugin | L3 and stick gestures | FOLLOW root; CHECK sticks within +-1 | 1 (f) | one feature: pad input | |
| P14 | pad button bit layout (`Shift`) | 1 | plugin | every pad button | LIVE `PadButtonCheck` (left trigger seen twice) | 1 | one feature: pad input | |
| P15 | `PlayerStride` 0x240 | 1 | plugin | co-op focus | SIG L48 | 1 | cosmetic | |

### 1.7 In-game layers

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| L1 | `HudLayerOffset` 0x558, `MapOverlayOffset` 0xB90, `DeathScreenOffset` 0xCF0, `SkillBarOffset` 0x610 | 4 | `GameLayout.InGameUi` | HUD, map, death screen, skill bar | NAME (`HUD`, `map_overlay`, `death_screen`, `skill_bar`) | 1 | one feature each | needs P1 and A5 |
| L2 | `InventoryPanelOffset` 0x378, `ChatLayerOffset` 0xA60 | 2 | `GameLayout.InGameUi` | inventory, chat | LIVE shape (`panels_parent`; `textField` + `scrollbar`) | 1 | one feature each | |
| L3 | `TutorialLayerOffset` 0xDD8 | 1 | `GameLayout.InGameUi` | tutorial box | FOLLOW the nearest layer found | 1 (f) | cosmetic | no name, no shape |
| L4 | `BindDisplayOffset` 0x788 | 1 | `GameLayout.InGameUi` | skill picker | NAME (`bind_management_display`) | 1 | cosmetic | a miss is not reported |
| L5 | `PartnerSkillBarShift` 0x1A0 | 1 | `GameLayout.InGameUi` | co-op skill bar | LIVE (named `skill_bar`, another element) | 1 | cosmetic | |

### 1.8 Control subclasses

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| K1 | combo box: items 0x688, parallel 0x6A0, selected 0x518, highlight 0x528, open 0x538 | 5 | `GameLayout.ComboBox` | option dropdowns | LIVE `ComboBoxFits` | 1 | one feature: dropdowns (the only group said when lost) | |
| K2 | slider: value 0x37C, max 0x388, min 0x38C | 3 | `GameLayout.Slider` | option sliders | LIVE `ReadSliderState` | 1 | one feature: sliders | a slider at its minimum proves nothing |
| K3 | inventory grid: cell size 0x33C, hovered 0x350, cell 0x358, server inventory 0x6F8, item entity 0x4E0 | 5 | `GameLayout.InventoryGrid`, `InventoryItem` | inventory readout | LIVE `InventoryReader.TryReadGrid` | 1 | one feature: inventory | |
| K4 | `TabButton.SelectedOffset` 0x391 | 1 | plugin | tabs | LIVE (weak: needs a second bar) | 1 | one feature: tabs | |
| K5 | `BindingIndexOffset` 0x370, `SkillDetailsOffset` 0x2D8 | 2 | `GameLayout.SkillSlot` | skill bar | LIVE (binding indexes 0..23; details lead to a GrantedEffectsPerLevel row) | 1 | one feature: skill bar | |
| K6 | passive canvas state 0x3D8, node record 0x2C8 | 2 | `GameLayout.PassiveTree` | passive tree | LIVE `TreeState`, `NodeRecord` | 1 | one feature: passive tree | |
| K7 | `NodeInBuildOffset` 0x70C | 1 | `GameLayout.PassiveTree` | build planner | SIG L46 | 1 | one feature: build planner | |
| K8 | graph: state file 0x160, content 0x28, edge list 0x58, allocated 0x1A8 | 4 | `GameLayout.PassiveTree` | links, allocations | LIVE `PassiveTreeFields` | 1 | one feature: passive tree | |
| K9 | `EdgeToOffset` 0x8, `AllocatedEntrySize` 4 | 2 | `GameLayout.PassiveTree` | links, allocations | CHECK: shape of the edge list | 0 | one feature: passive tree | |
| K10 | `ButtonMenu.SelectedIndexOffset` 0x348 | 1 | plugin | button menus | SIG L47; CHECK index >= 0 | 1 | one feature: button menus | read without a text shift (`UiSemantics.Lists.cs:108`) |
| K11 | class selector id 0x390, index 0x394 | 2 | `GameLayout.ClassSelector` | character creation | LIVE `SelectorAgrees` | 1 | one feature: class selection | |
| K12 | panel title 0x2E8 | 1 | plugin | panel names | LIVE `TitleAt` (weak) | 1 | cosmetic | |
| K13 | text-field hint 0x798 | 1 | plugin | text fields | LIVE `Placeholder` | 1 | cosmetic | |
| K14 | check box state 0x60A | 1 | plugin | toggles | LIVE `Changing` (judged only when the player toggles) | 1 | one feature: toggles | |
| K15 | selectable list index 0x388 | 1 | plugin | lists | LIVE `Changing` | 1 | one feature: lists | |
| K16 | options section active 0x368, row index 0x370 | 2 | plugin | options | LIVE `IsSectionRow` (weak) | 1 | cosmetic | |
| K17 | world-map depth byte 0x566 | 1 | plugin | world map | LIVE `Changing` | 1 | cosmetic | |
| K18 | `ContinueGreyOffset` 0x3FA | 1 | plugin | name too short | FOLLOW text shift (`UiSemantics.Selectors.cs:79`) | 1 (f) | cosmetic | |
| K19 | world-map pins: hovered 0x378, current 0x358, `MapPinRow.ElementPinOffset` 0x2D0 | 3 | plugin | world-map pins | CHECK `DatKey.Row` table name; no text shift applied (`UiSemantics.WorldMapPins.cs`) | 0 | one feature: world-map pins | |
| K20 | dat key layout: `KeyFileOffset` 0x8, `FileNameOffset` 0x8 | 2 | `GameLayout.SkillSlot` | every dat foreign key | CHECK table name (`DatKey.cs`) | 0 | one feature family: skill, passive, class and pin names | |

### 1.9 Dat columns

| ID | Field | n | Struct | Used by | Protections | N | If lost | Notes |
|---|---|---|---|---|---|---|---|---|
| D1 | 20 columns of `dat-columns.json` the plugin applies: Mods.Name, MinimapIcons Label and RowSize, Characters.Name, ActiveSkills.DisplayedName, GrantedEffectsPerLevel.GrantedEffect, GrantedEffects.ActiveSkill, PassiveSkills x7, MapPins x4, WorldAreas AreaLevel and IsTown | 20 | plugin `GameLayout.*Row`, `DatColumns` | names, passive flags, pins | DAT `DatColumnExtractor` / `DatContentColumns` (measured offset stays if the file lacks it) | 1 | cosmetic for names; one feature for passive flags and pin content | plugin applies 20 of them (`DatColumns.cs:21-42`); `WorldAreas.Name` goes to the host only |
| D2 | `WorldAreaDatOffsets.NamePtr` +0x8, `IsTown` +0x14 (host remap) | 2 | `WorldAreaDatOffsets` | area name, town flag | DAT via `PlaceFromCode` (`Poe2AccessibilityCore.DevCommands.cs:617-633`) | 1 | cosmetic | the plugin's own read of `NamePtr` (`GameStateService.cs:172`) bypasses the remap (3.1 item 6) |
| D3 | `WorldAreaDatOffsets.IdPtr` +0x0 | 1 | `WorldAreaDatOffsets` | area id shape | none | 0 | cosmetic | |
| D4 | row id at +0 assumed: `CharacterIdOffset`, `EffectIdOffset`, `RecordSkillKeyOffset` | 3 | `GameLayout.PassiveTree`, `SkillSlot` | class, effect and skill ids | none | 0 | one feature: skill and passive names | |
| D5 | `AreaLoadingState.CurrentAreaRowOffset` 0xEA0 | 1 | plugin | area name on load | LIVE `GameStateService.AreaRowOffset` | 1 | cosmetic | said when lost |

## 2. Gap list (fields with 0 or 1 protection, most impactful first)

Counts by ID are in the totals below. A single LIVE shape on a world-critical field is listed as one group (rank 8): each is a sound
shape, but a second, different anchor (a signature from code that reads the field) would survive a patch that breaks the shape.

| Rank | Fields | N | Impact | Suggested anchor |
|---|---|---|---|---|
| 1 | E4, E5: entity path (`EntityDetailsPtr`, `name`) and the component table | 0 | world silent | live shape: a block whose first pointer leads to a UTF-16 `Metadata/...` string, and a lookup table whose entries name known components (`Render`, `Life`); plus signatures from the function that builds an entity's path |
| 2 | G2, G3: `GameStateOffset` fields and state indexes | 0 | world and menus silent | make `GameStatesSearch` relocate `CurrentStatePtr`/`States` (stack ends in one of the states); identify AreaLoading and InGame by content (the state that owns the area pointer) instead of index |
| 3 | U1-U4: whole `UiElementBaseOffset` (self, children, parent, visible flag, rects) | 0 | menus silent | live shape: `Self == address`, children vector whose elements point back to the parent, a flags word whose visible bit differs between a shown and a hidden panel; judge the sweep's own UI probe instead of dropping it |
| 4 | T6: `TileStructure` size 0x38 and the 23 sub-tiles per tile | 0 | world silent | let `IsTerrain` take the record size from the vector (bytes / tiles) and the walkable ratio from `GridWalkableData` / `BytesPerRow`, as `TileGridSearch` already does |
| 5 | P12: focus fields (current, last focused, hovered) | 1 (f) | menus silent | live shape: the focused element is a valid element whose manager is this manager and which changes when the player moves focus (`ChangingField`) |
| 6 | A5: `UiRootStructPtr`, `GamepadUiRootStructPtr` | 0 | in-game panels silent | scan the state object for the element whose children carry the layer names (`HUD`, `skill_bar`), the way `FromStateElements` does for the manager |
| 7 | C6, C4: vital internals (`Total`, `Current`, back-pointer) and `Health` | 0 / 1 | world silent (`IsAlive` gates every enemy) | signature from a function that reads life; shape already finds the vital, but a change inside the vital is only "lost" |
| 8 | A1, A10, A14, A15, E1, E3, C1: area pointer, local player, entity lists, terrain, entity id, component list, position (one shape each) | 1 | world silent | add a signature per field from the code that reads it, as the terrain fields have |
| 9 | C38: `WorldItem.ItemEntityPtr` | 0 | loot radar silent | shape: pointer to an entity whose path starts with `Metadata/Items` (what `PluginUiElementReflection.cs:100-110` already checks), searched around +0x28 |
| 10 | A12, A13: server data player list, `PlayerInventories`, `InventoryStruct` | 0 | pick-up, hand, flask news, salvage silent | live shape: vector of 8-byte pointers to structs whose inventory vector has 0x18-byte entries (id, pointer) and whose item list length equals X*Y*8 |
| 11 | C40: minimap icon row (host `IconName`) | 0 | icon-based classification | make the host read `GameLayout.MinimapIconRow.ComponentRowOffset` so SIG L35 also protects it |
| 12 | K19, K20, D4: world-map pins, dat key layout, id-at-0 assumptions | 0 | pins and all dat-key names | label check exists (table name); add a search around the key for the table name string, as `DatKey` already tests |
| 13 | C17, C21, C11, C34 entry layouts, C26, C32 | 0 | statuses, stats, traps, mod names, shrine radar | a signature from the function that reads the entry (buff id chain, stat pairs, state entry stride), or a value check on a known buff/stat |
| 14 | A4, A6, A16, C39, D3, U5, P10 | 0 | cosmetic or co-op | low priority; A16 should at least read `area.Layout.Fields.LocalPlayer` |
| 15 | single-protection features (C2, C5, C8-C10, C12-C14, C16, C18-C20, C22-C24, C29-C37, T1-T5 beyond world, K1-K18) | 1 | one feature each | a second anchor where a function reads the field (signature) or a value check on a known value (level, charges) |
| 16 | G4: `GameCullSize` | 1 | UI rect offset on non-16:9 | live check: `Window.Width - 2*cull` equals the 16:10 or 16:9 content width the UI draws, or a signature from the code reading it |

## 3. Doc and code mismatches

### 3.1 The doc says something the code does not do

1. `docs/compatibility.md:272-273` says `GameCullSize` "has a search". There is none: `Core.cs:160-166` takes the pattern's
   address, and only `Game States` has a fallback (`GameProcess.cs:272-275`). Line 75-76 (the world came back with `GameCullSize`
   broken) is true but proves nothing, because the cull value does not affect the world.
2. `docs/compatibility.md:6-7,20,79-81,229-233` cite `tools/package/gamehelper.patch`, `Update-GameHelper.ps1` and "the patch leaves the
   `FieldOffset` lines alone". Neither file exists (`tools/package/` holds `New-TestPackage.ps1`, `Publish-Release.ps1`, `README.txt`);
   `host/README.md:15` says the patch was merged into `host/`, and `e9fdbd2` edits `AreaInstanceOffsets` directly. `docs/status-history.md:143`
   repeats the old command. The "Still needs a person" steps (lines 229-233) therefore name a script that is gone.
3. `docs/compatibility.md:38` ("Any other component: GameHelper's verifier ... repair") and `:144-146` ("17 intact"): the verifier can fail
   only on a vector, a map, the owner back-pointer, an entity pointer or a vital with supplied hints (`OffsetHelperEngine.cs:983-996`;
   a bad generic pointer is `Weak`, `:1115-1140`). A component with no vector (Chest, Shrine, Player, Positioned, Base, WorldItem,
   Animated, Charges, Stack, Targetable, TriggerableBlockage, RenderItem, Transitionable) is "intact" on its header alone, so its
   fields are not checked by the sweep. Their protection is the signatures only (or nothing: WorldItem, Animated, Base's live search aside).
4. `docs/compatibility.md:29` lists "Entity id, valid flag" as both found by the key. Only `Id` is searched (`AreaLayoutSearch.FindEntityId`);
   `IsValid` moves along with it (`OffsetHealer.cs:151-158`).
5. `docs/compatibility.md:96` says the in-play flag makes "everything count as in play while unjudged or lost". While unjudged the measured
   flag is used (`EntityFields.cs:53` starts `InPlayTrusted = true`); everything counts as in play only once lost (`EntityFields.cs:178`,
   `EntityCatalog.Classify.cs:354-357`).
6. `docs/compatibility.md:40-41` ("no read site changes") holds for the host's `TryReadMemory<T>` (`SafeMemoryHandle.cs:119`). The
   plugin's `HostMemoryReader.TryRead<T>` (`HostMemoryReader.cs:71-80`) reads raw bytes, so every plugin read of a host struct skips the
   remap: `GameStateService.cs:172` (`WorldAreaDatOffsets`), `LocalInventories.cs:61,68,73` (`ServerData*`, `InventoryArrayStruct`),
   `UiManagerLocator.cs:105` and `UiTreeReader.cs:133,139` (`InGameStateOffset`, `UiRootStruct`). The doc's `report` fields (lines 212-213) admit
   this for three catalog entries only.
7. `docs/compatibility.md:249-250` says `WorldAreas.Name` is followed through `PlaceFromCode`; true for the host's `WorldAreaDat`, not for
   the plugin's own `AreaRowAt` read (item 6).
8. `Poe2Accessibility/Services/LayoutProbe.cs:10-11` (comment: "element base fields follow the control name") and `OffsetHealer.cs:68`
   (comment: `UiElementBaseOffset` is judged "by the plugin's UI probes") overstate: the probes move only `GameLayout.Element.*`
   (`LayoutProbe.cs:106-109`); nothing remaps `UiElementBaseOffset`.

### 3.2 The code does something the doc does not say

1. `catalog/code-fields.txt` L14, L19, L20, L25, L26, L54 carry fields the plugin never consumes (Transitionable state, status
   effect times, stack size data, tile height multiplier). Harmless, but they count in "50 of 50".
2. `docs/compatibility.md:94` omits `map_overlay` from the in-game layers found by name; `InGameFields.cs:44` has it.

### 3.3 Code that defeats its own protection

- `Poe2AccessibilityCore.cs:71` maps catalog target `layout:MinimapIconComponentRow` to `GameLayout.MinimapIconRow.ComponentRowOffset`, but the
  icon names that classify entities come from the host's `MinimapIcon.cs:60` (hard-coded `+0x20`). The signature fixes `MinimapIconLabel` only.

### 3.4 Unsure

- `OffsetHealer.FollowEntityId` (`:152-158`) calls `Remap(EntityOffsets, ...)`, which replaces the struct's segments; `Verified` does the same
  (`:528`). If a patch moves the entity id and `ComponentListPtr` by different amounts in one build, whichever runs second would drop
  the other's segment. Not simulated.
- `GameStates.cs:85-86` indexes the 13 state slots as constants; I did not check whether `GameStateHelper` validates the mapping elsewhere.
- Whether `Verified` would repair `Rarity` (C23) when `Details1` moves: its vector anchors start at +0x150, after the field, and a
  segment applies upward from its own offset (`OffsetHealer.cs:528`, `StructRemap.cs`), so I treat it as not covered.

## 4. Totals

Counted from the tables of section 1 (sum of `n` per `N`; a `(f)` follower counts as 1; recheck by summing the `n` and `N` columns).

| N | Fields | Rows |
|---|---|---|
| 0 | 63 | 31 |
| 1 | 149 (of which 15 are 1 only because they follow another field's anchor: A9, E2, C10, C20, P2, P3, P6, P8, P12, P13, L3, K18) | 83 |
| 2 or more | 10 | 8 (G1, A7, C7, C15, C25, C27, C28, P4) |
| Total audited | 222 | 122 |

History says where to look first: the area and entity structs moved in 0.5.4, 0.5.4c, 0.5.5 and 0.5.5f
(`docs/compatibility.md:18-19`), and no other field has moved in a real patch yet (`docs/compatibility.md:220-221`).
