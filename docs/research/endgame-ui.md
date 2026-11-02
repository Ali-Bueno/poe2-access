# Endgame UI screens never seen live (offline research, 2026-11-01)

Offline only: GameHelper2 source, ClientStrings (en/es), `ui-catalog.json`, repo docs, one web search (it gave no
controller bindings, so every button claim below is OPEN). Confidence: **MEASURED** = read from a file in this repo or
a prior live dump; **INFERRED** = consistent with the sources; **OPEN** = unknown until the screen is open.
`G` = GameUi (`S+0x318`). GameHelper paths are child-index paths from `G`
(`ImportantUiElements.cs` = `external/GameHelper2/GameHelper/RemoteObjects/States/InGameStateObjects/ImportantUiElements.cs`).

## 0. Cross-cutting
- MEASURED: the UI catalog has NO layout for any of these screens: its 126 `.ui` sources are mostly tutorial panels;
  the only hits are the ids `atlas_button`, `atlas_skill_button`, `passiveButton`. The extractor cannot help here;
  the first live F6 dump is the only layout source.
- MEASURED: GameHelper's paths are stale for the passive tree in 0.5.5e (`passive-tree.md`: `{24,2}` and `G+0x730`
  point elsewhere). INFERRED: the other hardcoded paths below (`{22,0,x}`, `{25,0}`, `{64,0}`, index 84) may be stale
  too. Treat them as hints, locate by title/fingerprint/name at run time, never by the index.
- MEASURED: GameHelper skips `sekhemasTrialMapPanel`, `passiveskilltreenodes`, `ChatParent` in controller mode
  (`ImportantUiElements.cs:496-511`, set to zero), so the Sekhemas lookup does not work in controller mode at all.
- MEASURED: `IsAnyLargePanelOpen` already ORs WorldMapPanel, AtlasSkillsPanel, TempleConsole, SekhemasTrialMapPanel
  (`ImportantUiElements.cs:298-308`).
- Generic layer (INFERRED to work on all four): panel title from `label_tab_title`/`+0x2E8`, focus reader on buttons and
  slots, `+0x120` tooltips on hovered elements, the world-map narration (area under cursor, legend). Graph canvases are
  the exception: no focus, hover only via `M+0x760`.
- Controller-mode legends in ClientStrings (text empty in the dump, glyph-only; ids are the evidence the actions exist):
  `ConsoleAscendancyGraph`, `ConsoleApplyPassiveChanges`, `ConsoleCancelPassiveChanges`, `ConsoleActionOpenAtlas`,
  `ConsoleWorldScreenShowAtlasPassiveTree` ("Atlas Passive Tree"), `ConsoleToggleMapInformation`,
  `ConsoleToggleMapLegend`, `ConsoleMoveMapLegend` ("Move Map"), `ConsoleExpandMapQueue` ("Expand Map Queue"),
  `ConsoleMapNavigatePrimaryAreas` ("Navigate Primary Areas"), `ConsoleActionTravelToArea` ("Travel"),
  `ConsoleActionPassiveAdvancedAllocate` ("Press-Hold {0} to Select Points Type"), `ConsoleActivateMapDevice`,
  `ConsoleQuickInventorySwitchMapDevice`, `ConsoleCloseLabyrinthMap`, `ConsoleMapPanelSameAreaError`
  ("You are already in this destination"), `ConsoleMapPanelNoWayPointError`.

## 1. Ascendancy view of the passive tree
- MEASURED (`passive-tree.md:24-25,51`): 197 ascendancy nodes sit in the SAME canvas, hidden (visible bit clear), each
  row with `+0x167` FK to `Data/Balance/Ascendancy.dat`; 23 other hidden nodes (special jewel sockets). The same
  record -> PassiveSkills row -> Name chain used for the main tree applies.
- MEASURED: tree state `T+0x180` = AscendancyPassiveSkillOverrides.dat; counters for ascendancy/weapon-set points are
  siblings of the points icon (`passive-tree.md:77,102`). `passive-tree.md:125` lists the ascendancy view as OPEN.
- INFERRED: the view is the same canvas with the hidden nodes shown (and probably the main ones hidden or dimmed) when
  toggled; the toggle is the action `ConsoleAscendancyGraph` (controller) and probably a button in `expanded_frame`.
  Not proven: whether the view is a mode of the same panel P or a second panel.
- Strings: `PassiveInfoAscendancy` ("{0} total Ascendancy Skill Points ({1} allocated)"),
  `PassiveInfoAscendancySkills`, `PassiveTreeAscendancyPointLeft/PointsLeft`,
  `AscendancyClassSelectorWindowDescription` ("Choose your Ascendancy Class"), `...NextButton`, `...BackButton`,
  `...SelectButton` ("Ascend"), `...ChangeButton`, `...ChangeButtonHoverCanChange/CannotChange`,
  `...CloseButtonHover` ("Choose Later"). `LabyrinthAscendancyWindowTitle` = "Altar of Ascendancy" is the old
  (PoE1) altar; PoE2 chooses the class through the class-selector window after the trial (OPEN how it opens).
- Before ascending: INFERRED not viewable as a choice (the selector window shows classes, probably with a preview);
  OPEN whether the toggle exists with 0 ascendancy points.
- Likely works: points counters and tooltip of a hovered node (same `M+0x760` path) once nodes are visible.
  Needs work: a hidden-node filter (only visible ones count; today the plugin may skip them), the mode announcement
  ("Ascendancy tree"), the class-selector window (ordinary buttons plus a description).
- First time live: F6 dump with the view ON and OFF; compare the visible bit of the 197 nodes and of `edge_fade_overlay`;
  hover a node and check `M+0x760` hit; read the selector window (title, class list, Next/Back/Ascend). Find the
  toggle control name and its legend glyph.

## 2. Trial of the Sekhemas and Trial of Chaos
- MEASURED (GameHelper): Sekhemas map panel = child of `G` child 84, first child (`ImportantUiElements.cs:524-529`),
  "visible only during Trial of the Sekhemas" (`:319-323`); mouse mode only. `TempleConsole` = `{64,0}` (`:54`,
  Temple of Atzoatl, not these).
- Sekhemas strings (MEASURED): window `SanctumMapWindowInfo`, floors `SanctumMapFirst..FourthFloorTitle` (Test of
  Strength / Will / Cunning / Time), `SanctumObjectWorldDescriptionTitle/Body` ("Trial Map", "Interact to select a
  trial"), resources `SanctumResolveBarTitle`, `SanctumResolveValue` ("[Honour]: {0}/{1}"), `SanctumShieldValue`
  ("Inspiration {0}"), `OrbHonourValue`, `SanctumChoiceNotEnoughGold/Health` ("Sacred Water"), fountains
  `SanctumFountain*Title/Description` (Tabana, Ahkeli = minor affliction, Orbala = boon, Galai, Halani), vendors
  `SanctumVendorMerchant/LyciaPact/DeferralTitle`, confirm buttons `SanctumVendor*ConfirmButton`, `SanctumVendorRerollHover`,
  run summary `SanctumRunSummary*`, `SanctumOpenRunSummaryActive/InactiveHover`, `SanctumResetRunHover/Confirmation`.
  Relic/key altar: `RelicAltarWindowInfo` (text mentions Trial of the Sekhemas), `ItemFunctionSanctumQuestKey`.
- Chaos (Ultimatum) strings (MEASURED): `UltimatumWindowCurrentRewardsTitle`, `...NextOfferTitle`, `...CraftingTitle`
  ("Trial Sacrifice"), `UltimatumWindowBeginButton` ("Accept trial"), `...VoteButton`, `...TakeRewardsButton(Singular)`,
  `UltimatumWindowPreviewRewardsButtonText`, `UltimatumActiveModifierLegendsTitle`, `UltimatumBarDisplayName`,
  `UltimatumWorldDescriptionBeginButton/InfoButton`, `UltimatumWindowGamblingModeTitle/Description`,
  `UltimatumWindowBeginButtonNotInstanceOwner`. Key: `ItemFunctionUltimatumQuestKey` (Temple of Chaos).
- INFERRED: the room-choice map is a node graph (path from entrance to the boss at the right, per `SanctumMapWindowInfo`),
  with selectable rooms: a dedicated reader, like the atlas. Choice popups (fountain, vendor, pledge, deferral) are
  ordinary windows with buttons: the generic focus reader probably reads them; honour/inspiration bars are HUD labels
  that need a status key (F8) and a change announcement.
- Chaos windows (vote list, rewards, sacrifice slot) look like ordinary windows: likely works.
- First time live: dump with the map open (node element class, which are reachable/current/done, how selection is
  made); dump a fountain, a merchant and the pledge; check that focus lands on the choices; find the HUD honour and
  inspiration labels and their hierarchy; dump the run summary; check the controller lookup of the map panel (index 84
  is likely wrong for the controller UI tree).

## 3. Atlas, Atlas passive tree, map device
- MEASURED (GameHelper, the strongest lead): WorldMapPanel `{22,0}` (`ImportantUiElements.cs:38`), Act tabs
  `{22,0,0..3}` (Act1..Act4) and Interlude `{22,0,5}` (`:42-46`), **Atlas `{22,0,6}`** (`:47`, "Atlas tab under the
  world-travel map panel", `:224-227`), **AtlasSkillsPanel `{25,0}`** (`:48,234`). Controller mode resolves these too
  (`UpdateWorldMapPanelAddresses`, `:544-554`); the Atlas is locked in the campaign (`ui-sweep-2026-10-25.md:23`).
- MEASURED: atlas map nodes are children of the Atlas panel, recognised by flags fingerprints `AtlasMapNodeFp = 0x542EF3`,
  "you are here" `0x502EF3`, mist node `0x442EF3` (`ImportantUiElementsOffsets`-side consts in `ImportantUiElements.cs:87-92`).
  Node fields (`:56-84`): biome id `+0x2BE`, status byte `+0x2BF` (bit0 accessible, bit1 completed), map data row
  `+0x290`, grid position `+0x310`, content name `+0x278`, connections vector `+0x590`, content tokens vector `+0x350`,
  badge vector `+0x368/+0x370`. Sea ships = `EndgameRegionActionButton` children (row `+0x308`, grid `+0x318`,
  ocean region row 2, `AtlasRegionButton.cs`). Public API: `GameUi.AtlasMaps` (`AtlasMapNode`: Index, DisplayName,
  connections), `AtlasMarkers` (player node), `AtlasOceanButtons` (`:244-256`). Credit yokkenUA.
- MEASURED: `Plugins/Atlas2/Atlas2.cs:375` is the reference consumer: `GameUi.Atlas`, visible check, `TotalChildrens`,
  children cache; it shows names, connected counts, content and badges. Reuse its logic read-only: a graph of
  `AtlasMapNode` with edges is ready, so a node-by-node navigator (like the passive-tree one) is feasible without RE.
- INFERRED: the atlas has no stick focus; the plugin needs its own cursor over `AtlasMaps`, speaking name, tier/content,
  accessible/completed, neighbours. Atlas passive tree (`AtlasSkillsPanel`) is a separate canvas; hover via `M+0x760`
  may work as in the main tree; its node records probably point to AtlasPassiveSkills rather than PassiveSkills
  (OPEN, `AtlasNodeMapDataOffset` comment in the source hints the same trap).
- Strings: `AtlasScreenTitle`, `ActionOpenAtlasScreen`, `AtlasScreenSearchLabel` ("Highlight Maps"),
  `AtlasPassiveInfoTotal/Bonus/Quest/QuestTotal`, `GainLoseAtlasPassive(s)`, `GainLoseAtlasRespec(s)`,
  `AtlasModUsesRemaining`, `ConsoleWorldScreenShowAtlasPassiveTree`. Atlas passive tree is therefore reached from the
  world screen by an action (OPEN which button).
- Map device (`ObjectDisplayTextMapDevice`): `MapDeviceWindowTitle` ("Map Receptacle"), `MapDeviceWindowActivateButton`
  ("Activate"), `AddMapToMapDevice` ("Journeys await you"), `MapDeviceWarning*`, `ConsoleActivateMapDevice`,
  `ConsoleQuickInventorySwitchMapDevice`, waystone errors `ItemError*Map*`, tier `ItemDisplayMapTier`
  ("Waystone Tier"). Voidstones: `QuestRewardsWindowTitleVoidstones`/`...TakeVoidstones` ("Socket Voidstones into your
  Atlas to raise the Tier of all Maps"); voidstone slots location OPEN (probably the atlas or its passive panel).
- Likely works: map device window (slots + Activate button are ordinary focus items, waystone tooltips already read as
  items); atlas search box. Needs work: atlas node graph navigator, atlas passive tree reader, sea ship buttons.
- First time live: open the Atlas with a character that has it and dump (root, child count, fingerprints of children,
  one node's name/status/connections); hover an atlas-tree node and compare `M+0x760`; check `AtlasMaps` in GameHelper's
  Data Visualization window, then in the plugin; open the map device with a waystone and dump the window; find voidstone sockets.

## 4. Act 4 sea travel (Kingsmarch, Rog / Makoru)
- MEASURED: `G4_WorldMap` = "Act 4" and `G4_*` area keys exist (`GameHelper/Data/WorldAreaNames.tsv:132-148`, e.g.
  Isle of Kin, Kedge Bay, Whakapanu Island, Brinerot Cove, Plunder's Point). Act 4 is a tab of the same world-map panel:
  `Act4PanelChildPath {22,0,3}` (`ImportantUiElements.cs:45`). String `A4SetSail` = "Set Sail".
- INFERRED: acts 1-3 and 4 share the world-map panel (`WorldMapPanel`), but the NPCs Rog/Makoru probably open it from a
  dialogue/menu on the Act 4 tab rather than from the Quests tab; `map_control` (`GameLayout.WorldMap`,
  `GameLayout.cs:254-261`) likely applies if the same widget class is used. OPEN: is it the Quests panel or the
  waypoint-style World Panel; the sea routes may be different buttons ("Set Sail") instead of area hotspots.
- Likely works: the existing world-map narration (area under the cursor, legend, "Travel" action) if the widget is shared.
  Needs work only if sailing is a button list in a dialogue. Reuse `ConsoleMapPanelSameAreaError` text.
- First time live: stand at Rog, F6 dump before and after opening the destination map; record whether the root/title
  matches the acts 1-3 map (`map_control`), whether Act 4 islands are hotspots or buttons, and what the confirm step says.

## 5. Live, 2026-11-02 (yarkey, Act 1 only)
- MEASURED (`debug/ui-tree-InGameState-20261102-023623.txt`, `dump visible` with the town waypoint open; the full
  `dump` of the same screen ran out of its 20000-node budget inside the hidden passive tree and never reached the map):
  the world map is `panels_parent` child 3, `[0,1,17,2,3]` (frame title "Misiones" at `+0x2E8`), holding `WorldScreen`
  `[0,1,17,2,3,0]` (18 children). GameHelper's `{22,0}` is stale here; the panel is found by its name `WorldScreen`.
  - `header` → `title_label` "Teletransporte"; `act_buttons_layout` → one `ActButton` per unlocked act (label "acto 1"
    only for this character: the Act 2-4, Interlude and Atlas buttons appear as they unlock, so they are the place to
    look for the Atlas and Act 4 entries).
  - `map_control` → a 94-child pin layer: each area is a 110x110 pin (`visual_pin_layout`, some `dialogue_button`) with
    no text; the name, monster level, bosses and "Viajar, A" come from the hovered pin's tooltip, which the plugin
    already reads ("El bosque singular. Nivel de monstruos: 4. … Viajar, A. Administrador de instancias, mantener A").
    An area without its waypoint ("El valle rojo") reads the same minus "Viajar, A".
  - `quest_selector` holds an `endgame_quick_travel_buttons_container` (the endgame's quick travel, empty in Act 1).
  - The right edge button "Selecciona para alternar entre por encima y por debajo del suelo, debajo del suelo" toggles the
    map layer; it is reached by the stick like the pins.
- Act 4 sailing: if Rog/Makoru open this same `WorldScreen` (likely: one widget for every act), the islands read like
  any pin. Still unseen; the first time, `dump visible` (not `dump`) with that map open.
- The passive tree's ascendancy view: no ascendancy toggle among the dumped passive-tree elements (the tree was hidden
  then); still needs a character with an ascendancy.

## Priority for the next live session
1. Atlas (largest unknown, best prepared: GameHelper already has nodes/edges/status; build the navigator offline).
2. Ascendancy toggle (cheap: one dump with the view on/off).
3. Sekhemas map (needs the controller lookup solved), 4. Act 4 map (one dump), 5. Chaos windows (probably fine).
