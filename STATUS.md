# STATUS — Path of Exile 2 (blind accessibility plugin)

> Per-mod ledger. Open this first. Details live in `docs/research/*.md`; the brief in
> `POE2_ACCESSIBILITY_RESEARCH_BRIEF.md`; agent rules in `AGENTS.md`.

## Identity
- **Game:** Path of Exile 2, Steam (`PathOfExileSteam.exe`, app 2694490), proprietary GGG engine, DX12,
  no kernel anti-cheat (server-side detection). Install: `D:\games\steam\steamapps\common\Path of Exile 2`
  (download started 2026-09-14, ~138 GB). Current patch 0.5.5f.
- **Approach:** external, **read-only** memory reading, no input automation (see brief §3/§25 and `AGENTS.md`).
- **Host:** the mod's own headless `Poe2Access.exe` (`host/Poe2Access`, one process with the launcher steps since 2026-11-08; [host](host/README.md)): the
  `GameReader` library (GameHelper2 v2.8.0's memory model, vendored and renamed) with the runtime layout searches and the
  exe's code reader ([compatibility](docs/compatibility.md), [patch day](docs/patch-day.md)), no overlay, no administrator rights,
  60 frames a second (20 behind another window); GameHelper2 itself is no longer used (70% of a core in town; the host 17%).
  ExileCore2 rejected (binary-only, obfuscated, Discord-gated — `docs/research/exilecore2-study.md`);
  CoreExile2 no longer exists (now `coussiraty/ExileBridge`, an HTTP/MCP API without UI tree).
- **Speech transport:** **PRISM v0.18.3** (`third_party/prism`, the release's own files) through the mod's own C-API
  binding `PrismLibrary` (function pointers, `PrismConfig` matched to the shipped `prism.h`, 56 bytes; shared as source by
  the plugin and the startup speech), since 2026-11-08: Prismatoid 0.4.0 carried PRISM 0.17.3 (no Wine bridges) and a
  48-byte config. The plugin folder ships `Poe2Accessibility.dll`, `prism.dll`, the three Wine bridge placeholders beside it
  and `wine/*.dll.so`; under Proton the `wine/` folder must be on `WINEDLLPATH`, which Steam's launch option
  `sh "<mod>/poe2access-proton.sh" %command%` sets before it runs `Poe2Access.exe` under the game's `proton`, and under
  Wine only Orca, Speech Dispatcher or Spiel is taken, never SAPI (mute there; `PrismLibrary.StartBridged`, 2026-11-09,
  from the D2R mod's Proton test; [Proton](docs/proton.md), untested here). Windows
  check 2026-11-08 (`dotnet run --project tests/Poe2Accessibility.Tests -c Release -- prism <plugin folder>`): NVDA picked,
  the line sent (PRISM_OK), no bridge loaded; Orca, Speech Dispatcher and Spiel now in the registry.
- **Plugin:** `Poe2Accessibility/` (sealed `Poe2AccessibilityCore : PCore<AccessibilitySettings>`).
- **Build:** `pwsh tools/build.ps1` (`-SkipTests`, `-Configuration Debug`): builds `Poe2Access.slnx` (executable with
  `tools/Poe2UiExtract` as a library, plugin, tests), runs the tests and `phrases.py check`; the plugin auto-deploys to
  `host/Poe2Access/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility/` (all of `catalog/`,
  [list](Poe2Accessibility/catalog/README.md)). Plain `dotnet build` at the root builds the same, unchecked.
- **Run:** double-click `Poe2Access.exe` in that folder: starts PoE2 through Steam if needed, updates the game data, then
  runs the host loop in the same process (no window; stopped by `Poe2Access.exe stop`, event `Local\Poe2Access.Stop`) until
  the game closes; `restart` (no game start) / `stop` for testing; errors
  in `Poe2Access.log` (one log, `app\` in the package) + a message box. From Steam's Play button: launch options `"<path>\Poe2Access.exe" %command%` (`tools/README.md`).
  Dev: `py -B tools/computer_use/launch.py all|host|stop` wraps it.
- **Releases:** public repo (2026-10-26); `pwsh tools/package/Publish-Release.ps1` (`-DryRun` first) refuses off `main`, dirty or unpushed, then builds, tests, packages and makes it the latest GitHub release (tag `vYYYY.MM.DD[-n]`, assets always `Poe2Access.zip` / `.7z` + `release.json`), so `https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.zip` is always the newest.
- **Test package:** `pwsh tools/package/New-TestPackage.ps1` (build and tests first) → `dist/package/Poe2Access-test-<date>.zip` and `.7z` (zip ~47 MB, 7z ~33 MB since the recorded item sounds; top folder: `Poe2Access.exe`, README.txt, Controls.md, Stop the mod.bat
  shortcut; everything else in `app/`; self-contained, unzip and
  run; sounds extracted from the tester's install on the first start); `tools/README.md`.
- **Repository:** private GitHub `Ali-Bueno/poe2-access` (created 2026-10-23).
- **Unit tests (pure logic, no game):** `dotnet run --project tests/Poe2Accessibility.Tests` (screen differ, text edits, chat, gestures, scanner, alarms, enemy cues, radar).
- **Autonomous testing (controller only):** virtual DualShock 4 + speech log oracle — `tools/computer_use/README.md`. Plugin logs: `Plugins/Poe2Accessibility/logs/accessibility.log`;
  UI dumps: `Plugins/Poe2Accessibility/debug/ui-tree-<State>-<timestamp>.txt`;
  settings: `Plugins/Poe2Accessibility/config/settings.txt` (hotkeys by key name).
- **Hotkeys (defaults):** F6 dump UI tree, F7 repeat last announcement, F8 speak status (the host has no keys of
  its own). `settings.txt` is re-read automatically when edited while the host runs.
- **Cost:** `Poe2Access.log` says what the frames cost once a minute; dev command `costs` logs the plugin's parts.

## Section status
| Section | Status | Notes |
|---|---|---|
| EXP-001 host builds clean | done | 0 errors, 3 upstream warnings (2026-09-14) |
| EXP-002 plugin compiles + deploys | done (live) | loads in GameHelper 2.7.5, PRISM/NVDA speaks |
| EXP-002 state logging (open / login / char select / in game / area / close) | done (live) | `GameStateService` (PreGame → Waiting → Login seen) |
| EXP-006 PRISM behind `IAccessibilityOutputService` | done (live) | dedupe + repeat |
| EXP-003/004 UI root + tree dumper | done (tool) | `UiTreeDumper`: known in-game chain + state-object root scan + wstring probe; one block read per node, cycle guard, 20k-node cap |
| Adversarial code review (opus) | done | 5 bugs fixed 2026-09-14 (hotkey collision, per-read reflection, Self==0, settings clobber, double interrupt) |
| UI text offset (display label) | done (live) | `+0x490` stripped text, `+0x360` with markup, `+0x538` copy |
| Focus / hover / selected tracking | done (live) | UI manager `Z+0x988` (activation target) / `Z+0x990` (last focused) |
| Login-screen UI root discovery | done | parent-chain climb from state-object pointers; dump of 4104 nodes |
| Login-screen focus announcements | done (live) | `FocusTracker` reads the game's activation target (`Z+0x988`, fallback `Z+0x990`); verified by activation |
| Generic UI-manager discovery (any state) | done (code) | `UiManagerLocator`: slot whose target has a parentless element at +0x8C8; in-game fallback via UiRootStruct−0x588 |
| Widget semantics (row label, value, role by click sound) | done (live) | `UiSemantics`; options menu rows read (~40 rows verified) |
| Hands-free research (auto dump per screen + focus context log) | done (code) | `ResearchSession`, `debug/focus-research.log`, setting `ResearchMode` |
| Options menu rows (label, slider, checkbox, combo) | done (live/partial) | rows read; checkbox `+0x60A` (27/27 records); **slider value `+0x37C` in [`+0x38C`,`+0x388`]** (Sharpness 150/1000 = "15%"); **combo: entries vector `+0x688` (16-byte entries), selected index int64 `+0x518`** (16 combos validated) — entry text layout probed live by `ComboBoxReader` (fallback "option i of n") |
| Tooltips in options menu | done (live) | in game the rows own +0x120 tooltips: "Popup Size, 100, slider, Adjusts the size of popups in menus. 2 of 6" |
| Unnamed widget labels (news panel etc.) | done (code) | named ancestor within 2 hops first; row label only from non-interactive thin label columns |
| Appearing texts (dialogs, hint bar) | done (live) | `ScreenTextWatcher`: texts that appear after a focus move (visible-only walk, background); read "Do you want to save the changes..." |
| Offline UI catalog | done | `tools/Poe2UiExtract catalog` → `catalog/ui-catalog.json` (847 controls); used only as last-reference for role |
| Speech on its own thread + every spoken line logged | done (live) | D2R lesson: PRISM speak blocks |
| Autonomous test loop (virtual pad, launcher, peek, dev command file) | done (live) | `tools/computer_use/` |
| World quiet behind a panel without focus | done (live: passive tree, world map; the engrave box in code) | `WorldGate` + `ScreenSnapshot.PanelOver`: the world's sounds and news played over the engrave confirmation and the tree between nodes (user 2026-11-05); [rules](docs/menu-speech-rules.md) |
| Screen layers: popups, no-focus panels, closing popups, disabled controls | done (live) | `ScreenWatcher`/`ScreenDiffer`/`ScreenNarrator`; rules in `docs/menu-speech-rules.md` |
| Terms / account windows read | done (code) | layer model; verified on creation screen, terms accepted before it existed |
| Character creation (class, quote, name field) | done (live) | full sweep 2026-11-06: every silent dead end said (`CreationNameGuide`); [findings](docs/research/ui-layers.md#character-creation-055f-2026-11-06) |
| Pre-game sweep (login, selection, options, terms) | done (live 2026-11-06) | all read; news banner named, L3 filter and LB told, migrations notice read, terms no longer skip paragraphs |
| **Boot to gameplay** | done (live) | account `yarkidius`, Witch `Yarkidius`, Act 1 beach (2026-10-23) |
| Hint bar button names (glyph images) | done | `ControllerGlyphs` renders glyph art and markup as button names; heard in the 2026-10-26 log ("mantener A", "LT más mantener A", "Y") |
| Human labels for id-named controls (gateway, exit, options, motd) | done (code) | `Announcements.ControlLabel` (league, gateway, language, options, collapse); the rest read as the humanised id; promo text is on request |
| Text-field echo, password masking, list position "x of n", tab titles | done (code) | `Announcements.Typed`, `PasswordLength` (the .ui script's `mode = password`, length only; not heard live), `ListPosition` ("4 de 86" in the 2026-10-26 log), `TabTitle`; [rules](docs/menu-speech-rules.md) |
| UI manager per state via `element+0x90`; in-game focus `M+0x748` | done (live) | `UiManagerLocator`; game menu has its own manager |
| In-game layer policy (arrival layers are base, chat ignored via `ChatParent`) | done (live) | quiet on arrival; panels need focus-layer ownership (in-game controls lack bit 0x1B) |
| Inventory: grid cells, equipment slot names, flasks, item base name/rarity/stack | done (live) | `InventoryReader`; "Inventory. Scroll of Wisdom, 3, column 12, row 5", "Gloves, empty" |
| Inspect mode tooltip (full text), keyword links, expanded definitions | done (live) | "Wells, link" → A → definition then focus |
| In-game narration: focus layer only, panel title on entry, id-named containers silent | done (live) | `ScreenNarrator` in-game mode |
| Panel tabs (LB/RB) and options tabs announce the tab with the focus | done (live) | `label_tab_title`; options tab byte +0x391 |
| Game menu, exit confirmation (message + focused button), list positions "x of n" | done (live) | options rows: position within their section ("Channel Count, High, combo box, 2 of 3"); section names not spoken yet |
| Options opened in game: tab + focus on open and on LB/RB, never the page's labels | done (live) | "Graphics. VSync, Locked (Default), combo box" |
| Item details (the item element's own tooltip) via `details` | done (live) | "Stack Size: 3/40. Identifies an item. X to use, then A …"; no controller binding for `details` yet |
| Character selection list (selected entry, position) | done (live) | "Yarkidius, Level 1 Witch, Act 1: The Riverbank, 2 of 2" |
| Character panel stats as "label, value" rows | done (live) | numbers from the player's Stats (`SheetStats`, `PlayerStats`), at any resolution ([notes](docs/research/ui-layers.md)) |
| Window independence | done (live, five sizes) | the camera's matrix for what is seen and heard, laid-out units instead of pixels ([camera view](docs/research/camera-view.md), [data sources](docs/research/data-sources.md)) |
| World map pins from data | done (live, 16:9, 21:9, 5:4) | MapPins/WorldAreas rows the act canvas keeps; told when the popup was not heard (`MapPinNarrator`) |
| Area announcement on entering / changing area | done (live) | own read of AreaLoadingState+0xEA0 (GameHelper offset stale) |
| In-game context menus (skill: Manage Sockets…) with "x of n" | done (live) | same-class siblings |
| Hover tooltips (element+0x120): title names id-only controls, short ones spoken, `details` reads all | done (live) | "Support Gem Slot, Socket a Support Gem of any colour …"; skill gem details |
| Panel tabs: tab name + where the focus is (also when it stays, e.g. inventory beside Skills) | done (live) | hidden focus = no focus |
| Passive tree: hovered node (M+0x760) read from its tooltip | done (live) | "Critical Damage, 15% increased Critical Damage Bonus. Unallocated"; no node-to-node navigation yet |
| World map (Quests tab): area under the cursor, map legend | done (live) | no-focus panel narration; finding areas with a free cursor is still hard |
| Character stats (inspect mode): widgets with values, stat-list links with row + definition | done (live) | "DEX, 7, …", "Evasion Rating, 7. Evasion, link. …" |
| Skills panel rows, empty slots, skill bind bar (button + bound skill) | done (live) | "empty, 2 of 10", "B, Dodge Roll/Sprint"; unhovered skills fall back to the icon name |
| Skill bind screen (X on a gem) and skill picker (a slot's grid of skills/actions) | done (live) | "Selecciona el botón al que asignar Descarga de caos. Set de habilidades principal. LB, vacío"; entering the other row names it; picker cells (A on an empty button, X on a bound one) "vaciar el botón, columna 1, fila 1", "Usar frasco de vida, columna 2, fila 2…"; picking a skill up reads "vacío. Colocar, A. Cancelar, B" |
| Cosmetics | done (live) | ordinary buttons once the d-pad moves the focus |
| Quest list | done (live) | world map X: "Llegar a Sierraclara, botón, <description>", then "Más información, A. Cerrar, B…"; the map's own legends after its place (2026-10-24) |
| Quest list rewards (R3 "Inspeccionar recompensas") | done (live) | the focused reward icon has no text; `UiSemantics.Describe` names it by its art (`QuestRewards`: "Misiones. Recompensa: mesa de fabricación, 2 de 6" from RewardCraftingBench.dds, 2026-10-26); the NPC's reward window ("Selecciona una recompensa…") was already read |
| Objective / tutorial box (G+0xDD8): new texts read, queued | done (live) | "Equip Item. Press View to Open your Inventory", "Press X to equip weapon." |
| Quest tracker page always said | done (live; the wait for a menu in code) | "Misión: Altares siniestros. Busca los altares de ritual y purifícalos" whenever the shown page is not the last said, held whole, across areas, once any menu is closed (`QuestTrackerPage.Waiting`: it held an NPC's options back 7 s, [rules](docs/menu-speech-rules.md)); after loading too (Freythorn's objective came with the HUD and was never said, user 2026-10-29) and on coming back to the game window (`CheckQuestPage`, pure `QuestTrackerPage`) |
| HUD (G+0x558): new texts once per area, never the whole HUD | done (code) | quest tracker, shrines; a ground item's tooltip says only name, rarity and quality (`EntityCatalog.ItemOfTooltip`), gold nothing (user 2026-10-24) |
| NPC dialogue: speaker + line, every advanced line, Continue, quest accept | done (live) | Renly, Una (Clearfell, 2026-10-23); a line changing in place is spoken (the screen filter drops texts the focus holds); choices not seen yet |
| Chat: NPC shouts, own remarks, system, whispers, party (global/trade behind a setting, off) | done (live; fresh-start fix in code) | `ChatWatcher` + `ChatDiff` (text sequence; the game rebuilds line elements, an address-based check re-read old lines >1000 times); AFK notices dropped; `AnnounceChat`. The list is the chat layer's descendant with the most *text* lines: after a fresh game start the history is empty and a textless 74-icon list (`[13,5,1]` in the 2026-10-26 dump) was taken instead, so nothing was read all evening (2026-10-27); lines of a list first found after an empty chat are news. Channel signs dropped when spoken ("@De Dahnte:hola" → "De Dahnte: hola"). Other players' local chat cannot be told from NPC lines by text. The player's own lines (speaker = a local character's name, after a guild tag) are read in every channel whatever the chat settings, as proof they went out (`ChatDiff.IsFrom`; user 2026-10-28) |
| Tooltips read automatically when the focus lands (items, equipment, gems) | done (live) | "Main hand, Withered Wand, Wand. Grants Skill: Level 1 Chaos Bolt. … Unequip, X"; `ReadTooltipsOnFocus` |
| Mod button L3 (any controller, from the game's own pad state M+0x768) | done (live) | tap/double tap = next/previous line, hold = whole tooltip; gameplay: repeat, hints, screen; `docs/controls.md` |
| Scanner (hold L3 + right stick flicks; tap = where the choice is now) | done (live) | `EntityCatalog` + `RoutePlanner` + `ScanBrowser`: 8 categories, routed steps, leg + next turn ("Una, 14 steps, right 3, then up right"); walked Riverbank → Miller → Clearfell → town NPCs on it |
| Low-life warnings | done (live) | `VitalAlarm`: "Life 50 percent" / "Life 25 percent" once per fall; the Miller was beaten reacting to them |
| Skill crafting (engrave an uncut gem): grid in reading order, details after the name, Engrave confirm with its buttons, categories | done (live) | "Skeletal Sniper, Level 1, 1 of 24"; LB/RB say the category first (`category_name_label`, a tab title: "Elemental. Muro de fuego, Nivel 2, 1 de 24", live 2026-10-31); an uncut support goes straight into the skill its category names: "Furia I, engarzada en Campana de tempestad" (`ScreenNarrator.Engraving`, code; user 2026-10-31: where did it go?) |
| Build Planner gear: the build's base and wanted modifiers per slot against the worn item (slot), any wearable item under the cursor (short line), summary on entering the inventory | wip (code + unit tests 2026-11-10; not run live; real catalogs to plug in) | `UiSemantics.BuildGear` + pure `BuildGear`, `Announcements.BuildGear`, `GearCatalogs` (contract); worn pieces from memory: `ItemModLines`, `ModStatCatalog`, `UiSemantics.ItemMods` ([item mods](docs/research/item-mods.md)); [ui layers](docs/research/ui-layers.md#build-planner-gear-055f-2026-11-10) |
| Build Planner gems: the build's skills and supports set against the character's (Skills panel rows, empty slots, sockets; cut gems in the inventory, stash, merchants; engraving choices) | done (live 2026-11-10; engraving choices code only: no uncut gem at hand) | `UiSemantics.BuildGems` + pure `BuildGems`, `GemNames`, `BuildPlanFolder.Active`, `GameLayout.GemSockets`; [ui layers](docs/research/ui-layers.md#build-planner-gems-055f-2026-11-10) |
| Skill gems in the inventory named from their tooltip (no base display name) | done (live) | "Unearth, column 1, row 1" |
| Read the whole panel (`screen` command) | done (live) | character detailed stats read fully; summary block order is jumbled |
| Options section names on entering a section | done (live) | "UI. Controller. HUD Mode, Central, combo box, 1 of 8" |
| Vendor window (buy/sell) | done (live) | "tab 3. Suede Bracers, …, 25 gold, not enough. Gloves…"; magic items by full name; gold counter "0 gold"; selling not exercised (own items); no price in another currency within reach: in Acts 1-2 only the Sekhemas merchant sells for Sacred Water, inside a trial run (2026-11-08, tables) |
| Stash | done (live) | "Stash. tab 4. empty, column 12, row 10"; tab switches on LB/RB; moving items untested |
| Waypoint map (Teleport) | done (live) | opens "Teletransporte. acto 1", "Viajar al pueblo, Y", then the act's cartouche and the place under the cursor; the stick moves between areas ("Lodazal. … El devorador", "Viajar, A"); Y travelled to town (2026-10-24) |
| Audio output (winmm mixer, synthesized cues) | done (code) | `AudioCueService`; no new DLL; "Audio cues ready" logged live |
| Enemy sounds (growl + rarity layer, pan, tempo by nearness) | done (live) | "16 pulses, up to 6 enemies in the last 5 s"; sight from the projectile layer; ~9 pulses/s in a pack (tune by ear); `enemies` dev command |
| Navigation radar (D2R: tock wall, breath floor, tin exit, 4 screen rays) | done (live) | `docs/radar.md`; user: "creo que funciona"; wall tones = later alternative |
| Local co-op: per-player reading (`ReadPlayer1` on, `ReadPlayer2` off; one, the other or both): cursor, pad, inventories, flasks, skill bar, radar (player 2 in its own timbre), named life/level/state news, downs and revivals | done (live, bot) | J2 read alone and both read, tested with two virtual pads in town and Clearfell (inventory, hand, L3 menu, vitals, skill bar, radar, "yarkbruja ha caído"); default and solo online unchanged (`docs/research/local-coop.md`) |
| Nearby things named on approach (checkpoint, NPC, stash, chest, item, exit) | done (live) | `docs/proximity.md`; "Checkpoint" on respawn, "Clearfell Encampment" at the exit |
| Radial menu (hold View) | done (live) | the highlighted option is the only text of its layer: "Inventory", "Skills", "Passives", "Bind Screen", "Character", "Quests", "Cosmetics", "Not Available" |
| Death screen | done (live); fallback (code) | "You have Died. resurrect at checkpoint", then the button; `DeathWatch` reads it from G+0xCE8 when not narrated 2 s after dying, says it hidden ("Y la muestra"): once (11:23, 2026-10-24) the game focused it but it read hidden and nothing was said |
| NPC / player tap, wall bump, level-up "Level 3" | done (code) | user to test; passive-point tutorial line was already read |
| Guide beacon (L3 + R3 on a scanner choice) | done (live) | `Beacon`/`BeaconMath`: route off the frame thread, ping 620 Hz panned along the route, "Llegaste a …"; since 2026-10-26 the chord releases any lock whatever is chosen (a co-op friend found or dead left it on; code) |
| Guide without confirming (D2R) | done (live) | setting `AutoGuide`, off by default: `Beacon.Follow` on every scanner answer, nothing extra said; the chord still releases it |
| Scanner Encounters category: league devices (`Metadata/…/Leagues/…` with a minimap icon) and every ritual altar by its icon (Freythorn's campaign altars were silent, user 2026-10-29: "Altar de ritual", "…, completado", the King's "Altar de ritual del jefe"); a minimap-marked "monster" outside `Metadata/Monsters/` is a device; marks left out are logged once ("Minimap mark not listed") | done (live: a done altar listed) | "Altar de ritual" (icon RitualRune; game strings "Altar de ritual {0}", "Interactúa para ver los favores": the altar is where the tribute is spent); listed while untargetable too; named on approach and with A; `ritual.wav` (RitualEncounter/RituaStart_01) among the object sounds |
| Breakables (rocks, eggs, urns) | done (live: 110 breakable sounds walking Clearfell, 2026-11-08; materials not told apart by ear) | Chest with no label (`Chest.IsLabelVisible` false, not a strongbox) → kind `breakable`; a sound per material (`Breakables` by path: boulder, rubble, urn, pot, big pot, statue, egg, bone, barrel, crate, basket → `sounds/breakables/`, from each `.ao`'s FMOD event via `Poe2UiExtract ao-sounds`, the softest sample of its bank; crates a name guess); an object no word names plays the event the game gives it (`object-sounds`, 617 objects, lookup proved live on real entities, not heard by ear), the rest Breakables_01; all low-passed at 2.8 kHz and kept at peak 0.3 (rocks were "ultra shrill", user 2026-10-27); every breakable in range sounds (12 voices, 70 ms apart, not waiting for other groups); a chest named `*Switch*`/`*Lever*` (`Switches`, unit-tested) → kind `switch`, `switch.wav` (a hand on a lever). Bodies searched like chests sounded like chests (user 2026-10-28): `Breakables.IsCorpse` (CorpseChests, the Ogham village's `Metadata/Chests/Impalement01` "Víctimas" seen live 2026-10-28, hangsaw, stocks, witchstake, crucifix, mummified) → kind `corpse`, `corpse.wav` (Corpses/Open_04, brought to RMS 0.1 / peak 0.8: the sample is 11 dB under the chest's); chests and bodies were heard one at a time: every one in range now sounds (6 voices, 70 ms apart within the crowd) |
| NPC and player taps | done (code) | two NPC streams at once; other players a higher double tap (`CueSynth.Layer.Player`); a player down (Life not alive, the local partner too) a soft falling ding-dong at half the cadence, heard off screen and behind walls (`Layer.PlayerDown`); unit-tested; setting `PlayerSounds` silences the players alone (a tester, 2026-10-28: crowded towns) |
| Scanner Quest category (objective's proper nouns) | done (live) | "Misión. El molinero hinchado, único, 5 pasos"; `QuestMatcher`. A name's words also match in the plural ("Busca los altares de ritual" → "Altar de ritual", unit-tested). Places are never targets ("Encuentra la entrada al Valle rojo" led to an "entrada" tile, "Busca a Asinia en la Tumba de la consorte" to a "tumba" prop); a radar tile note that is no area name (`QuestMatcher.TileNote`, `LandmarkTable.IsAreaName`) only by its whole name ("Ogham" in "El tesoro salvaje de Ogham" led to "Ogham Farmlands Or Deadend", a dead end; user 2026-10-28) |
| Boss life + dodge warning | done (live) | "El molinero hinchado 72 por ciento"; whoosh + snap only on attack animations (`DodgeThreats`), a rare's blow within 150 units, louder when closer; timed half a second before the blow as learnt from the hits taken (`DodgeTimings`, `config/dodge-timings.txt`; MudGolem GroundSlam 1156 ms), at the wind-up until learnt (`docs/research/dodge-timing.md`); fuller since 2026-10-28 (sweep floor 0.6, snap 35 ms, far gain 0.8, same peak: a tester found it hard to hear in boss fights) |
| League encounter in the vitals readout (L3 double tap): "Encuentro de ritual. 100 Tributo. 0 rituales restantes" | done (code) | `EncounterReader`: visible texts under the HUD's `tertiary_hud_layout` except the quest pages (UI dump 2026-10-26); changes during play were already said as HUD news |
| Culling Strike cue (on while a skill culls) | done (code; skills read live) | `CullWatch` + pure `CullThreshold` (KeywordPopups "CullingStrike": 35/20/10/5% by rarity); a recorded gong sped up and cut to 0.85 s (`assets/cull.wav`, user's recording 2026-10-30; take G2 of https://claude.ai/artifact/1fXBPHVbfmPkZrh7Dxh669), else the synthesized trembling gong (`CueSynth.Cull`, pick H5 of https://claude.ai/artifact/NVEVcncyDggik2bhT11PnJ) on the enemy's side, rares and uniques named. Setting `CullCue`: with a culling skill (default) / always / never; the skills from `catalog/cull-skills.txt` (`CullSkills`, extractor `cull-skills`) against `PlayerSkills` (KillingPalmPlayer seen live 2026-10-29). Was a switch off by default that a Monk never found |
| Area memory: explored ground, remembered exits/NPCs, way in | done (live) | `AreaMemory` by instance hash + map layout (D2R: seed + level + room digest), bound to the instance the game holds, not the loading screen (the town was once saved under Clearfell's hash); a regenerated map starts unexplored; everything kept on disk (v3); live 2026-11-01: restart, town ↔ Clearfell "same map"; since 2026-11-02 a new instance of a campaign map whose area-transition tiles stand where they stood (`AreaMemory.Frame`: Clearfell yes, the Grelwood no) inherits the last one's exits and checkpoints, not its explored ground (testers heard Clearfell's exits forgotten; live: "El bosque singular, 194 pasos" on arrival) ([area memory](docs/research/area-memory.md)) |
| Disenchanting (a trade window of the game's) | done (live) | `TradeOfferWatch`: the other side's offer said when it changes, the pad cannot reach that grid; [rules](docs/menu-speech-rules.md#trade-windows-disenchanting) |
| Salvage bench | done (live, except a real salvage) | bench found by its label (`UiSemantics.FirstLabel`: no framed title, so its salvages went unsaid), its text before the inventory cell (`ScreenNarrator.SidePanel`), "Reciclado: X. Recibes: Y, 2" (`SalvageOutcome`, unit-tested); A salvages, Y is drop, B closes it; [salvage bench](docs/research/salvage-bench.md) |
| Quest marker for "cross this area" objectives | done (code) | `QuestMatcher`: an objective that names only the current area and no other proper noun ("Sigue a través del Enredo sombrío") targets every area exit but the one back (`AreaMemory` way in), else the nearest unexplored ground; it named nothing before (user 2026-10-29) |
| Placed-object cue (Tempest Bell, Frozen Locus crystal) | done (live) | `DeployedWatch` + pure `DeployedObjects`: entities by path (`Metadata/Effects/Spells/monk_bell/`, `monk_spectral_bells/`, `Metadata/MiscellaneousObjects/FrostWall/OrbOfIce` seen live 2026-10-29); heard once standing 400 ms; the game's own sound (`tempest_bell` = `Four_TempestBell.bank` TempestBell_FX_OnHit_01, `ice_crystal` = `Four_FrozenLocus.bank` FrozenLocus_FX_Appear_01; launcher sounds step v2), the small bell `CueSynth.Deployed` until extracted, on its side, 700-2200 ms by distance, spaced 180 ms apart, object volume; both heard live 2026-10-29. GameHelper's Actor deployed list (0xC28) was empty, and no Actor vector in its first 0x1400 bytes held the crystal's id (survey 2026-10-29) |
| Charges, infusions, surges, Rage | done (live: power charges) | `ChargeWatch` (in `PlayerAlerts`) + pure `PlayerCharges`: buff stacks of `power/frenzy/endurance/spirit_charge`, `fire/cold/lightning_infusion`, `instilling_fire/cold/lightning/chaos`, `druidic_prowess`, `arcane_surge`, Tempest Bell's hidden `tempest_bell_combo_tracker` (seen live 2026-10-29; its stacks taken as the Combo, 4 to ring the bell); a glass tick per one held, a lower tick per one lost, the count said after 400 ms of quiet (power charges: the game's electric `power_charge`, the Combo the user's `assets/combo.wav` a semitone higher per point from C, both stereo and unpanned; 2026-10-30); Rage from Stats `current_rage` / `virtual_maximum_rage`, said only full or empty; in the vitals readout. Said and heard by separate switches, the Combo apart (`ChargeSpeech`/`ChargeSounds`, `ComboSpeech`/`ComboSounds`, levels "Voz: cargas" / "Voz: puntos de combo"; were under life and mana's, which re-said them before any interruption; user 2026-10-30: too much speech). Heard live 2026-10-29: "1 carga de poder", "sin cargas de poder" (Falling Thunder spends them), "2 cargas de poder". Not yet live: the other kinds, Rage. Enraged: no player buff found by name (the BuffDefinitions "enrage" rows are monsters'); "Player buff seen" will name it |
| Primed for Stun cue (Boneshatter: heavy stun and shockwave) | done (code) | `EnemyStateWatch` (own switch `PrimedStunCues`) + pure `PrimedThreshold` (KeywordPopups "PrimedStun": 40/50/60/70% by rarity) on `GameLayout.ActorComponent` (threshold Actor+0x4E4, buildup +0x4E8; stun traces 2026-10-26: a hag at 54 of 60 stunned by the next hit); not heard live yet. Freeze / Electrocution / Pin buildups: not found (the second and third 0x1C entries after the stun one stayed 0 against a Warrior; `stuntrace` logs Actor 0x4C0-0x5C0) |
| Enemy state sounds: heavy stunned, frozen, electrocuted, pinned, armour broken, dazed, parried | done (code) | `EnemyStateWatch` + pure `EnemyStates`/`EnemyStateNews`: buffs `frozen`, `electrocute_display_buff`, `pinned`, `broken_armour_debuff`, `dazed`, `parried` and the animations HeavyStun[Back|Forward] / Electrocute*; a sound per state (`CueSynth.StateCue`) on the enemy's side as it begins, rares and uniques named with the game's own term; on by default (only states the party causes happen); heavy stun seen in traces, the others not yet heard live |
| Raise Shield feedback: clank up as the shield rises, down as it falls | done (live) | `ShieldWatch` + pure `ShieldStance` on the player's animation name (game Animation rows 0x1DF-0x1E2: ActiveBlockChannel[Moving] held, ActiveBlockEnd[Moving] lowering); a blocked hit's flicker is ignored for 300 ms; heard live 2026-10-26 (log: raised ActiveBlockChannel, lowered at ActiveBlockEnd or by attacking) |
| Parry cue (Parry: buckler, targe, two swords) | done (code; skills read live) | reverse hit whose dead stop falls `ParryLeadMs` (150) before a timed blow (`BossWatch` + pure `ParryTiming`, `CueSynth.Parry`); only when the player's skills list `ParryPlayer` (`ParrySkill`, chain verified live on a Warrior 2026-10-28); never on the 191 attacks the files mark unblockable/unparryable (`hit-times` `;noblock,noparry`, `SkillFlags`; launcher step v2); user picked reverse hit A; [notes](docs/research/parry-and-stun-bar.md) |
| Unblockable attack cue | done (code) | two brass stabs at the wind-up of a rare's/unique's attack marked `;noblock` (`BossWatch`, `CueSynth.Unblockable`, `DodgeTimings.CanBlock`); the plain dodge warning stays for blockable ones (user, 2026-10-28; picked sample A) |
| Harmful ground alarm | done | grinding stabs (73.5 + 78 Hz, the user's pick on https://claude.ai/artifact/2E1BvgiEmJS2QYoCtmRV8P; [notes](docs/research/game-audio.md#harmful-ground-alarm-2026-11-04)) while a buff of `catalog/hazards.txt` is on the player (`HazardWatch`, pure `HazardGround`, extractor `hazards` from GroundEffectTypes +0x20 and "standing in" descriptions, 61 ids); a tester's ask; user picked sample A; fired live: "Hazard ground: vortex_79" in the 2026-10-28 log |
| Stun bar while blocking / parrying | done (code; bar read live 0/263) | `ShieldWatch` + pure `StunMeter`: "Barra de aturdimiento N por ciento" past 50/75/90; a blocked hit not heard live |
| Energy shield warnings | done (code) | `PlayerAlerts`: 75/50/25 and "agotado", pool ≥ a fifth of life; no live DPS meter exists (per-skill DPS already read in the Skills panel) |
| Own volume beside its switch: power charge, combo, harmful ground | done (live) | the same levels Sound lists (`SettingsMenu.OwnVolume`), which the user never opened: "Volumen: Punto de combo, 100 por ciento, 6 de 9" (2026-11-05) |
| Volumes to 300 % in steps of 5, dodge/parry volume | done (unit) | `AudioCueService`: a gain above 1 plays a copy limited by `PeakLimiter.Drive` (dodge +4 dB at 200, +5 dB at 300, no clipping); the mix goes through a `PeakLimiter` too (the old soft clip saturated, tester 2026-11-01); `DodgeVolume` apart from `EnemyVolume` |
| Boss weak point heartbeat (the Devourer: strike the head, not the tail) | done (live) | `WeakPointGuide` + pure `WeakPoints`: a unique monster sharing a "Tail" entity's name (MonsterVarieties MudBurrowerTailBoss / DevourerBossDuoTail have no boss bar, read 2026-10-26); silent while the head is out of play (it turns untargetable as it burrows: the log toggled in and out of play through the fight, won 2026-10-26) |
| Cue timbres and mixer | done (code) | beacon an iron chime, dodge a blade drawn over a low gong into a bass drop, the hit at 0.2 s as the snap was (`CueSynth.Dodge`; a noise sweep was taken for the radar's open-floor breath and a rising tone sounded like a cartoon; the user picked this from samples, 2026-10-28), both in a short stone-room tail; growl with a sub-octave; levels raised toward the game's own sounds; soft limiter instead of clipping (`CueSynth.Limit`) |
| Mana warnings, vitals on L3 double tap | done (live) | "Maná 50 por ciento"; "Vida 85 por ciento, maná 100 por ciento. El molinero hinchado 65 por ciento" |
| Vitals: flask uses, level progress, gold, explored share (D2R) | done (live) | "Frasco de vida menor, 6 usos… Nivel 3, 5 por ciento hacia el nivel 4, nivel del área 15. Explorado 41 por ciento" (2026-10-25); `FlaskWatch` (GameHelper FlaskInventory + Charges; uses only once two drinks showed what one costs, the charges until then: the base type's cost ignores the flask's modifiers, code 2026-11-10), `ExperienceTable` (`catalog/experience.json` from ExperienceLevels), `ExplorationMap.ExploredPercent`; empty-flask warning heard: "Frasco de maná menor vacío" after six drinks in Clearfell, then the game's own "Sin frascos" (2026-10-25; flasks are not drunk in town or at full life/mana); the uses left said after each drink, "Frasco de vida menor, 3 usos" (code, user asked 2026-10-26): flasks tracked by belt slot, since a drink makes a new item entity (`FlaskDrinks`, seen in the log); a use gained and a flask put on the belt said too |
| Minions in the vitals readout (D2R) | done (code) | `MinionReport` + pure `MinionGroups`: friendly living creatures without the NPC component within object range, by kind with average life ("Esbirros: 3 Esqueleto arquero, 80 por ciento"); a party member's minions count too |
| Loot filter for item sounds and approach names (D2R) | done (code) | `LootFilter`, setting "Objetos que suenan y se nombran": todos / mágicos o mejores / solo monedas, gemas, objetos de misión, raros y únicos; the scanner still lists every item; not heard live (no ground items in town) |
| Target name on right-stick aim, crosshair beep | done (code) | GameHelper `MouseOverEntity`; never heard live yet (no monster under the aim during the bot test) |
| Harmful states (Urge speech) | done (code) | `StatusWatch` (buff ids by stem; every new id logged "Player buff seen"); the mod's heartbeat was removed (the game has its own, user 2026-10-24) |
| Overheated crossbow (Rapid Shot) | done (heard 2026-11-05) | `StatusAilment.Overheated` from buff `overheated`: "Arma sobrecalentada", then "Arma lista" (user 2026-11-05); under the "Estados alterados" switch |
| Game sounds for loot/chests/exits/waypoints | done (live) | `ObjectSounds` + `GameSounds`; each item its own drop sound from the game's tables (`ItemDropSounds`, `catalog/item-sounds.json`: scroll → `Scroll_Drop_01`); up to four items at once, pitch up/down by screen side; portal, exit, waypoint, checkpoint = continuous idle beds (`sounds/loops/`, `AudioCueService.Bed` with pitch; code, not yet heard live) (`docs/research/game-audio.md`) |
| Mod phrases in the game's 10 languages, localized area names | done (live: es, fr) | `Localization` (game ini or setting) + `Phrase` handler: other languages look the English composite format up in `catalog/lang/<code>.json` (406 phrases, game terms checked against ClientStrings); `tools/localization/phrases.py check`; `catalog/area-names.json` from WorldAreas |
| Area level (its monsters' level) and player levels | done | `Announcements.Area`/`Levels`/`OtherPlayer`: "Área: Sierraclara, nivel 2." on entry, "Nivel 4, nivel del área 2" on the L3 double tap, other players "…, nivel 20" in the scanner; GameHelper AreaInstance +0xBC (log "Area level N") and Player +0x204 (level-ups heard live); checked live 2026-10-25: Clearfell 2, the Act 1 town 15, both as in WorldAreas (the town row really says 15; kept, the user wants towns to say theirs) |
| Named places walked into and props in reach (D2R) | done (live, town + Clearfell) | `PlaceNarrator`/`PlaceMap`/`PlaceTracker`, `catalog/places.json` from `Poe2UiExtract places` (2148 named tiles, tile and room props): "carro", "yunque", "campamento"; scanner category Lugares; tile props turned by RotationSelector (only 3 measured); `docs/research/places.md` |
| Pre-game hints, delete/creation screens, edit done | done (live) | hints on entry, only new ones after; "¿Estás seguro…? yarkey… cancelar, botón, 1 de 2"; "Zq, edición terminada" |
| NPC dialogue options read one by one | done (live) | `UiSemantics.OptionLists`: confirm glyph marks the option; D-pad left reaches the lore column; a heard topic (drawn grey) reads "Presentación, atenuado, 1 de 8" (Una, 2026-10-24) |
| Spoken settings menu + sound glossary (hold L3 1.5 s) | done (live; tree live 2026-10-31) | `SettingsMenu`, `SoundGlossary`; nested submenus (pure `SettingsCursor` over a tree: left/right opens, L3 tap goes up one level, at the top closes, double tap closes; each reopens where left): Combat holds Enemies, Bosses and enemy states, Defence, Charges and skills, Quiet in fights; Sound holds the master volume, one submenu per volume kind with that volume and each of its sounds' own, and the glossary split the same way (user 2026-10-31: two volume sections made no sense) |
| Volume of each sound (inside Sound, by kind) | done (live) | every cue and bed names its glossary key; `AudioCueService.SetLevels`/`Level` (settings `SoundLevels`, percent of the group volume, 0-300); ground items by base folder (`ItemSoundGroup`); left/right steps 5 % and plays it |
| Loot quiet in fights | done (code) | `FightWatch` + pure `FightState` (`QuietsInFight`): items, chests, bodies, breakables and league altars (user 2026-10-31) stop sounding and being named while a rare or unique (default) or any enemy in play is within `ObjectSounds.RangeWorld`, held 2 s; named once the fight ends; setting `QuietItemsInFights`. A boss waiting in its arena is alive, hostile and untargetable: it counted, and Lachlann kept a ritual altar 56 cells away silent, before and after the ritual, with nothing named around (dump 2026-11-04 18:41, `BitterGuyBoss@9`). `FightRoster` counts only enemies in play, and for 20 s (not measured) one that left play, for a boss between phases; dev command `fight` lists who counts. Draven's drops gone after a death were the game: dying resets a boss encounter with its loot (log 2026-10-30 18:40-18:43) |
| Characters' chat lines quiet in fights | done (code) | `ChatDiff.IsLocalSpeech` (speaker, no channel sign: NPC shouts, a boss's taunts, local chat) skipped while `FightWatch.InFight(QuietChatInFights)` (rares by default); whispers, party, system and own lines stay; boss mechanic calls come from buffs, not chat (user 2026-10-30) |
| Monsters the game hides are out of play | done (live) | buff `hidden_monster` (`BuffIds`, `EntityCatalog.IsHidden`, inside `IsInPlay`): no fight, enemy cue, scan entry, target name or boss life; the hidden Count Geonor kept the Wolf's drops quiet (user 2026-11-05) |
| A boss of several bodies heard once | done (unit; not heard in a fight) | `SpeechRepeats`: an urgent line equal to the last within 300 ms is held back; urgent lines used to skip the duplicate check, so "Congelado" came 7 times (user 2026-11-05) |
| Enemies coming into view without direction | done (code) | `EnemyDirections` (on); off: "Ahogado, 3" (user 2026-10-30: too many words while a boss talks) |
| Target from the HUD target bar (controller) | done (live) | HUD > entity_info_container > entity_info_display > name_label; MouseOverEntity is 0 with a pad |
| Dodge timing before the first blow (game files) | done (unit, values checked against blows timed live) | `catalog/hit-times.txt` from `Poe2UiExtract hit-times` (6929 attacks loaded; 6084 of the 7398 warned of timed; projectiles add their flight at the files' speed, unit not timed live); repeat swings warned each period; `docs/research/dodge-timing.md` |
| Dodge precision: aim, reach, on screen, real clock | done (live: normal monsters, rares, bosses) | the clip's position and speed (animated object's AnimationController), its events and the action's target (Actor +0x238), protected by 12 semantic rules and six live checks (all "where measured" live); `AttackReader` + `BossWatch.Dodge` + `DodgeGate`. Live 2026-11-08 (Clearfell, Mud Burrow; [results](docs/research/dodge-timing.md#live-results-2026-11-08-055f), audit `tools/computer_use/dodge_audit.py`): three misses fixed (clip events never judged, back-to-back and alternate swings, a cast's contact at 0.0), recall 52-85% → 98.4% of 1558 attacks, false 0.9%, timing p90 35 ms live / 48 ms nominal where the full 500 ms lead fits; `DodgeWarningsAllMonsters` stays off (2-7 warnings a second in packs). Evening ([checks](docs/research/dodge-timing.md#evening-checks-2026-11-08-yarkey-dodgewarningsallmonsters-on)): Beira's five spears each warned, p90 39 ms; the urchins' leap learnt live ("carries its user"), then warned at its wind-up; a bow or crossbow monster's basic attack is a shot (it was gated as melee: 14 of 24 bolts unwarned; `hit-times` v4, archers warned live) |
| Death place "Donde moriste" among the exits (D2R) | done | `DeathWatch.Died` → `EntityCatalog.RememberDeath` (area hash + position); heard live: "Donde moriste, 40 pasos" in the 2026-10-26 log |
| Fresh install (no game config): login read at once, language list, input-method notice | done (live) | off-screen controls do not make a layer the top (the "Deal of the Day" panel hid the login); `visible_area` notices said after the focus; Steam logs in without a form; language re-detected on attach |
| Long scrolled texts (terms of use) by paragraph | done (live) | text over 1024 chars in a frame: `ScreenWatcher.LongText` → `LongTextReader`; scroll = frame `PositionModifier.Y` (+0x10C, negative) against text − frame `UnscaledSize.Y` |
| Map overlay shown / hidden (D-pad down) | done (live) | `map_overlay` (G+0xB88) hint `gamepad_move_map_hint` visible only while open; `MapOverlayWatch` |
| Chat panel shown / hidden (LT in a panel) | done (live) | chat layer visible with its "hold LT to chat" hint; `ChatWatcher.TrackPanel` |
| Chat channel list and filters | done (live) | unnamed combo found by its parts, fields per class (`UiElementReader.ComboBoxShift`); "Eligiendo: Global, 2 de 6", "Canal: Susurros", field "Global, #, campo de texto" (2026-10-25) |
| UI sweep 2026-10-25, every finding fixed | done (live) | disabled option rows ("no disponible"), challenges expanded/collapsed, quest map depth and rewards, panel notes (Skills spirit/capacity, Character class), stat-list sections, socket "1 de 2", small passive nodes, map with area/league/realm, game menu tip, friends/guild lines, clear-search button, capitalised small-caps captions; `docs/research/ui-sweep-2026-10-25.md` |
| Player menu (A on another player) | done (live, header unheard) | popup outside the root, focus stays on it; chosen button int `+0x348` (`GameLayout.ButtonMenu`); `UiSemantics.DescribeButtonMenu`; dev command `dump 0x<address>` dumps such popups; after "Comerciar" the focus goes back to the party panel and waits 300 ms for its layer: it cut "Esperando a que el jugador acepte la invitación para comerciar…" off with the party's lines (monster life per player count; log 2026-10-27 14:25). A waiting focus is now queued after a narration that cut in meanwhile (`ScreenNarrator.cutInAt`) |
| Weapon swap "Set de armas 2, vacío" (D2R) | done (live) | GameHelper `Stats.CurrentWeaponIndex` (+0x168) still right in 0.5.5e; `InventoryWatch.TickWeaponSet` |
| Inline item art named ("Identificar Pergamino de sabiduría x 1") | done (unit) | `catalog/item-names.json` (`Poe2UiExtract item-names`, BaseItemTypes Id +0x0 / Name +0x20); `ControllerGlyphs.Plain` |
| What A uses ("Finn, A", "Alijo, A") | done (live) | Targetable `+0x6B` = the game's interaction target; `ProximityNarrator.TrackTarget` (2026-10-24); cuts in since 2026-10-26 (queued it came late and A took the next item), the item's HUD tooltip no longer repeats it, lone tooltip property lines are not HUD news; items walked back to are named again (no minute cooldown; a lone item kept the game's target, so since 2026-10-26 a target named and then left beyond the re-arm distance is named again within reach, code) and a near item's sound repeats every 600 ms, not 1.3 s; a magic/rare item's HUD tooltip is no longer read whole (its full name is not the scanner's base name); no screen news while another window is in front (code) |
| Discoveries, pick-ups, unmet requirements | done (code) | `DiscoveryNarrator` ("Jefe a la vista" heard live), `InventoryWatch` (ServerData MainInventory1 via reflection), red tooltip lines with digits |
| Checkpoint travel map markers | done (live); place names (code) | `UiSemantics.MapMarkers` + `CheckpointPlaces`/`MapPlaces`: markers matched to seen checkpoints by one scale, "Viajar al punto de control, cerca de …, abajo, a 35 pasos, 2 de 2" (the game names none) |
| Skill bar readout (after the screen on the gameplay hold) | done (live) | `UiSemantics.SkillBar`: "Set de habilidades principal: LT, Activar set… X, Saeta básica… Vacío: LB, cruceta arriba, Y"; bind screen names skills from their dat rows (`docs/research/skill-bar.md`) |
| Skills ready after their cooldown ("Grito infernal disponible" + a bolt's click; speech, sound and speech level in the settings) | done (live) | `SkillCooldownWatch`: cooldown entries at `Actor+0xB20` keyed by the skill id; heard on a warcry (8 s) and a flash grenade (5 s), switches checked (`docs/research/skill-cooldowns.md`) |
| Route turn prompts, partial routes, controls help in the settings menu | done (live) | "Ahora arriba a la derecha, 10"; help topics open the L3 menu; the menu edge knock was removed (user 2026-10-24) |
| Game updates: layout found again, data regenerated, losses said | done (every part judged live 2026-10-25: login, character creation, town with a quest marker, options, inventory, passive tree, skill bar, left trigger) | `LayoutProbe` (+ image textures), `InGameFields`, `ControlFields` (+ tabs, skill slots, passive canvas and nodes, class selector, panel title, hint, check box and list seen changing), `EntityFields` (in-play flag, quest marker), `FlagBits`, `PadButtonCheck`, `PassiveTreeFields`, `TileGridSearch`, `BaseNameField`, `GameStateService.AreaRowOffset`, `CompatibilityReport`; `tools/package/Update-GameHelper.ps1`; launcher `GameDataSetup` + `DatContentColumns` (passive flags, skill keys by content); dev command `layout`; `docs/compatibility.md` |
| Names from the game's tables, what a patch added | done (live, simulated update) | `GameIds`, `AnimationNames`, `GameNews`: animation names and Rage's stats come from `catalog/game-ids.txt`, 500 of GameHelper's 1084 names were wrong; `catalog/game-keys.txt` says what an update added; [compatibility](docs/compatibility.md#regenerated-by-the-launcher-packaged-builds), [study](docs/research/game-data-sources.md) |
| Host follows its own offsets after a patch | done (live on 0.5.5f; simulated; the state anchor with its pattern broken on purpose, 2026-11-10) | `AreaLayoutSearch`, `OffsetHealer`, `StructRemap`, `GameStatesSearch` + `GameProcess.ProveGameStates`, `PatternScan`, `WorldLayoutWatch`, `tools/Check-GameExe.ps1`; 0.5.5f silenced enemies, radar and items until this; [compatibility](docs/compatibility.md#found-again-at-runtime-host) |
| Content changes such as 1.0 (smaller Atlas, new tree) | done (code; offline: every changed extractor byte-identical on today's install; stick self-check and Atlas catalog check unseen live) | `DatColumnSearch` + per-table finders (`EndgameExtractor.Columns`, `ProjectileColumns`, `BuffColumns`, `HazardColumns`, `AnimationColumns`), `SkillGraphFile` version check, `StickCheck`, `StickRuleCode`; [compatibility](docs/compatibility.md#content-changes-such-as-10) |
| Game data survives mod updates | done (simulated on a copy of the user's `dist`: carry-over 2 s, update restored, one step rerun alone) | `GameDataSetup` + pure `GameDataStamp`: per-step versions, catalogs in `app/GameData` copied over the shipped ones, sounds in the plugin; a game patch reruns all in ~33 s: one extractor `batch` run on one opened index (`Batch.cs`); `tools/README.md` |
| Scanner limited to what a sighted player sees | done (live) | `EntityCatalog.Sighted`: enemies on screen, minimap-icon things once the client has them, the rest once seen or explored; Quest category at any distance, a place not seen yet as the way toward it (next row); `docs/research/minimap.md` |
| Unexplored nooks (no gamble at forks) | done (live) | "Rincón sin explorar" for dead ends, listed last (`FrontierPlace.Pocket`). "Camino a …" was walked live and removed on 2026-11-05, too easy by the user's call: a quest place not seen is not listed; [radar](docs/radar.md) |
| Enemies coming into view | done (code) | `EnemyNarrator`: once per monster per area, packs grouped: "Ahogado, 3, derecha"; uniques stay discoveries |
| Inventory actions cut in | done (code) | "En la mano" held 400 ms (was 1.5 s, which queued the moves after it); equip/unequip/hand empty/arrivals interrupt |
| Social, Challenges, Cosmetics, Quests, Character, Skills panels | done (live) | filter box as an edit field; Challenges rows by their `selected_*` marker frame ("El relicario, 0/50, 5 de 8", expand reads the goal); free-cursor pages read after the tab; icon buttons named by their tooltip ("Efecto para arma") |
| Passive tree: node state, links, stick, zoom, Build Planner | done (live 2026-10-31) | `UiSemantics.PassiveTree`: links named by the push that lands on them (pure `PassiveStick`: the exe's own rule at RVA 0x9B9BA0, a linked node within 20° first, else the lowest (5 − 4 cos) × distance within 45° among the nodes drawn in the window, its constants read from the exe on disk (`StickRuleCode`) and checked against each push (`StickCheck`), live 8/8 and 6/6 on 2026-11-08; "…, sin paso directo" when none does; user 2026-10-31: a link said "right" took the stick elsewhere); "Zoom x1,9" on LT/RT; the active build's nodes (element +0x70C) "en la build" / "siguiente de la build" and the pushes to the next one; with its `.build` file (`BuildPlanFolder`, pure `BuildPlan`/`BuildNotes`) step, weapon set, note, attribute by colour ([notes](docs/research/passive-tree.md)) |
| Speech priority per kind, wall tones, small-obstacle knock | done (code) | `SpeechPriority` (11 "Voz: …" settings; charges and combo since 2026-10-30, wait by default), `WallTones` (off by default), `PassageScan.IsSmallObstacle`; unit-tested, not heard live. Messages hold the floor for about their length (0.6–2.5 s, `SpeechFloor.MessageHoldMs`): state titles ("Menú del juego.", "Selección de personaje." were cut by the first focus 300 ms in), the game's refusals ("El objeto no tiene espacio para más mods."), skill-bar alerts ("Inventario lleno" was cut by "Ya no llevas ningún objeto"), move notices; the focus that lands meanwhile follows (live 2026-10-28). Back to play from a panel or an NPC (focus gone 250 ms, only the HUD on top: no screen change marks a dialogue closing) stops what was being read, keeping what was asked after the close (`IAccessibilityOutputService.StopBefore`); the game menu closing stops too. LB/RB that switch a merchant's or the stash's tab with the focus left in the inventory say the tab (`ScreenNarrator.OnTabKey`; "Armaduras", "pestaña 2" live 2026-10-28) |
| Item in hand (inventory cursor) | done (live) | `InventoryWatch.TickHand` on GameHelper's Cursor1 inventory: "En la mano: Pergamino de sabiduría, 3. Elige una celda…", "Ya no llevas ningún objeto"; pick-ups now named from the item entity (host `Inventory.Items` keys are slot entries, not entities) |
| Monster modifiers on the aimed monster | done (code) | `MonsterMods`: names from the game's Mods.dat rows in the client language (first gender branch); the Miller's mods have no name, so nothing is added, as on its plate; not heard on a magic/rare monster yet |
| Gold pick-ups and total | done (live) | `GoldWatch`: rise of the inventory's gold counter during play, "Recogido 17 de oro" heard with the panel hidden after the Miller (2026-10-24); the total with the L3 double tap ("113 oro", 2026-10-26; shared in local co-op), named in the in-mod L3 help |
| Reach ping (D2R) | done (code) | `EnemyCues` + `ReachGate` (unit-tested): body gap from `Render.ModelBounds`; two rising notes, re-armed past reach + 25 |
| Unexplored-areas scanner category | done (live) | `ExplorationMap` (unit-tested): "Sin explorar. Zona sin explorar, 7 pasos, arriba 4, luego arriba a la izquierda"; kept per instance by `AreaMemory` |
| Highlighted control without focus, league selector | done (live) | `FocusTracker`: a visible child named "hightlight" (the game's spelling) marks its parent pre-game: "Continuar, botón, 3 de 3" after the name field; combo values skip `*_popup` children ("Liga, Forbidden Rites…"), the popup is read after the focus; a hovered element only beats the highlight when it moved last (a class info panel stays hovered while the stick is on the name, 2026-10-28); HUD `paused_frame` ignored |
| Equip / unequip / arrival notices (D2R) | done (live) | `InventoryWatch.TickMoves`: the game makes a new item entity on every inventory change, so moves are departures and arrivals paired once still for 300 ms: "Equipado: Ballesta improvisada, Mano principal", "Desequipado: …"; "Nuevo en el inventario" / "… salió del inventario" with a panel open (vendor/stash untested); the hand's "Ya no llevas…" waits 800 ms and is dropped for an equip. Since 2026-10-28 said as the deed (`ItemMoves`, pure, unit-tested; user: not "salió del inventario"): gold that rose with a departure is "Vendido: …, por 9 de oro" (Y at a merchant sells at once), gold that fell with an arrival "Comprado: …"; otherwise the panel beside the inventory names it, "Movido a Alijo" / "Sacado de Alijo" (`UiSemantics.SidePanelTitle`: the shown titled panel under panels_parent that does not hold the main inventory grid, whose order changes when a panel opens: dumps 2026-10-28; the hand remembers the panel it picked up in); gone with only the inventory open is "Tirado" once a new ground item of that name shows within 1.2 s, else "Usado". Live 2026-10-28 (yarkey and yarkidius in Clearfell): sold and bought back magic boots at Finn, rune to the stash and back; "Tirado" not heard (a town refuses drops: "¿Quieres destruirlo?", kept). Held against the focus that follows (`SpeechFloor.MessageHoldMs`); the player's own cursor move (a direction held within 500 ms) ends the hold and follows the notice queued (`SpeechFloor.Move`; user 2026-10-28: cells lagged up to 2.5 s after each sale); the skills panel beside takes and gives back only gems (`ItemMoves` `sideTakes`; anything else dropped or used there was "Movido a Habilidades", user 2026-10-31) |
| Class choice position | done (live) | `UiSemantics.SelectorPosition`: ClassVariationSelector +0x394 index over the first icon row: "Monje, 6 de 6. Mi vida…"; class id +0x390; the second row is the variants (X on the name step) |
| NPC quest bell (D2R NPC "!") | done (live) | `EntityCatalog.HasQuestMarker`: "Personajes. Renly, con misión, 16 pasos…", "Una, con misión"; bell (`CueSynth.Ring`) among the object sounds and in the glossary |
| Item comparison | done (live, the game's own) | R3 "Comparar" opens Modo inspección: the item beside the equipped ones, links walked with the D-pad; an item's text is read once, then only the links ("Set de armas, enlace") |
| Barriers: routes, scanner, opening | done (live) | `BarrierMemory` (unit-tested): closed believed at any distance, walkable only within 120 cells; routes cut by a closed barrier say "una barrera cerrada corta el camino"; scanner "Elementos. Barrera cerrada"; `BarrierWatch` "Se abrió el paso, …"; the guide says "lo más cerca posible" at the end of a partial route |
| Tutorial lessons that freeze the character | done (live) | lessons that hold the character until a press: dodge (B), low mana (D-pad right flask), passive point (View), engrave the uncut gem before leaving the camp; the lesson's button line is read ("View para abrir los menús"), a lesson shown again during play is said again after 5 s, and in play it is kept whole against interrupting lines. In code (2026-10-26, user missed the passive point behind inventory moves): in menus a lesson cuts in, is kept whole (`SpeechFloor`: what interrupts meanwhile waits, only the latest follows) and the focus it cut is said after it; a pending lesson back with a panel or menu is reminded after 60 s (`LessonReminder`) |
| Options page: glyph labels, scroll, slider rows, combos | done (live) | "Encaje de objetos stick izquierdo / cruceta", wrapped labels joined, deadzone "3 de 10", rows scrolling into view not read, value-beside-slider rows keep the list position, combos opened in place: "Eligiendo: …", "Adaptativo (experimental), 2 de 3", "Elegido: …" |
| Login: gateway list | done (live) | "Servidor, Sao Paulo (BR), lista desplegable, 5 de 7"; open list reads the highlighted entry "Japan, 16 de 19" |
| NPC dialogue and vendor (town) | done (live) | speaker first ("Renly. Comprar o vender objetos, 7 de 8"); vendor stock "Coraza oxidada, columna 10, fila 10, 50 oro, no alcanza… Comprar objeto, X"; reward and vendor openings without the gold chrome |
| Trade market (game menu, "Mercado de comercio") | done (live 2026-10-28) | RB searches once a filter is set; a listing (focusable card: seller column, item column with a `cost` block) is read item, price, seller and status, position, then properties and mods (`CardText`, pure, unit-tested; `UiSemantics.Cards`); listings scrolling into view are not mixed in. Its untitled panel had let the panel-title search adopt the root's "HUD" (+0x378) for the session: titles are now searched only below `panels_parent` |
| Sounds die away instead of cutting | done (live: bed heard to 18 steps; the rest measured offline) | `ProximityFade`, `Bed` release, cues let go over 20 ms, recordings cut at 0.9 s fade over 0.3 s; not heard by the tester ([notes](docs/research/game-audio.md#how-sounds-end-2026-11-07)) |
| Passive tree finder (D-pad) | done (live) | `PassiveMap`, `PassiveFinder`, `UiSemantics.PassiveFinder`: keystones, notables, jewel sockets and reachable nodes by points, the way on every node, "a 7 puntos" on locked ones; followed live to a keystone ([notes](docs/research/passive-tree.md#finder-and-what-the-pad-does-in-the-tree-055f-2026-11-07-malgdhler)) |
| Item sound sets (D2R) | done (code; samples load live) | setting `ItemSounds`: Path of Exile 2 (default) or Diablo IV (`ItemSoundSet`, `assets/diablo4/`, the D2R loops, one continuous voice per thing heard); items, chests, bodies, shrines; a unique has its own sound in both sets (`item_unique`, launcher sounds step v5); [notes](docs/research/game-audio.md#the-diablo-iv-item-set-assetsdiablo4-shipped-2026-11-04); not heard in play |
| Start from Steam's Play button | done (Steam's command run by hand on the real game; Steam's own launch options not tried) | `GameInstall.SplitArguments`, `GameLauncher.Start`/`WaitForExit`; double click still starts the game through Steam; `tools/README.md` |
| Auto-update | done (live against the GitHub release 2026-10-28; manifest from v2026.10.30-2) | `Poe2Access.exe` (`Updater`, `ReleaseTag`, `ReleaseManifest`) compares `app/version.txt` (written by `Publish-Release.ps1`) with the latest release's `release.json` asset (tag, zip SHA-256, notes; GitHub's API gave the user's address 403 rate limit all of 2026-10-30, 60 requests an hour per address), asks (message box), downloads the zip, checks its SHA-256, keeps `app/configs/` and the plugin's `config/`, renames itself aside, rolls everything back on a failure (tested with a locked DLL), starts the new launcher; setting `CheckForUpdates`, `Poe2Access.exe update` for a manual check; ported from D4 Assist |
| Korean client (Kakao Games, `PathOfExile_KG`) | done (live by a Korean player 2026-10-31, PR #1 from dnz3d4c) | pure `GameInstall` (Steam first when both are installed; start from the Kakao website); data from `Content.ggpk` (`Poe2UiExtract` `GameContent`); the client denies its memory to non-administrators, so the launcher restarts as administrator while it runs or while an administrator GameHelper is left (`ProcessAccess`, `HostController.HostNeedsAdministrator`; the leftover case is code only). Not checked: whether `Content.ggpk`'s time stamp moves without a patch (would regenerate the data each start) |
| Minimap marks: signs, boss rooms, points of interest, quest icons | done (signs and tile boss rooms live; icon boss rooms code) | `MinimapMarks` + `MinimapIconLabel` (row +0x1A, `dat-columns` `MinimapIcons.Label`): story glyphs "Letrero: Decreto del conde" among objects, never quest targets (the Hunting Grounds signpost "Pueblo de Ogham" was a character and the quest's village, user 2026-10-28); scanner category Puntos de interés. Boss rooms from the area's tiles (`EntityCatalog.TileLandmarks`: radar-table arenas "Sala del jefe: Asinia", unnamed arena tiles by file name), listed once within the minimap's pointer reach (MinimapIcons +0x12: 400 cells campaign bosses, 250 map boss) or seen, as the game points a sighted player to them (user 2026-10-29: mimic the map, not lost); live in Clearfell, "En el mapa: Sala del jefe: Beira of the Rotten…" at 398 cells. Boss room objects (`BossRoomMinimapIcon`, "…, derrotado") and labelled icons ("Taller de Renly") when the client lists them; `QuestObject`/`QuestItem` icons copied into Quest ([minimap](docs/research/minimap.md)) |
| Lost hint and radar mark (D2R) | done (live 2026-11-08: hint at a 3 s setting, its bed heard by `sounds`; mark played walking in) | `LostHint`, `LostWatch`: after `LostHintSeconds` (180) walking without new ground, the D2R "unexplored room" recording (`assets/loops/`, shipped) plays 4 s toward `WayOn` of the objective, else an unseen boss room, else the nearest frontier; radar open/exit cues toward unseen ground end in two 900 Hz pips; settings `LostHint`, `LostHintSeconds`, `UnexploredMark` ([radar](docs/radar.md)) |
| Map hints (GGG's cues) | done (live: "Reses muertas, el rastro de sangre sigue…" said walking past, 2026-11-08) | `MapHintNarrator`, `MapHintTable`, `catalog/map-hints.json`: said once on passing, with the side by the walk; signs anywhere; Act 3 camps add the entrance to the scanner (inferred trigger); settings `MapHints`, `MapHintSides`; dev command `maphints` ([map hints](docs/research/map-hints.md)) |
| Microtransaction shop | done (cards live; item page and purchase from the owner's buy 2026-11-10, its fixes unit-tested, not yet heard) | `UiSemantics.ShopPage` + highlight markers in game (`FocusTracker.InGameHighlightRoot`): tabs, categories "Pestañas de alijo, 27/27, 2 de 13", items "Pestaña extra de alijo, 30 puntos, 5 de 27", flags, featured banners named from their image; item page: "Comprar X, 150 puntos, tienes 300 puntos", its confirmation, the points on opening and after a purchase ([shop](docs/research/shop.md)) |
| Harmful ground and boss movement rules (Freythorn) | done (unit) | 7 ground buffs whose descriptions do not say "standing" added by the `hazards` extractor (swamp, spores, mud, the Voodoo King's rat tornado); "¡Quédate quieto!" / "¡Sigue moviéndote!" on the Voodoo King's punishment buffs (`StatusAilments`). Freythorn has no falls: the void between its treetop platforms is unwalkable ground the radar already calls wall |
| No mod window in Alt+Tab or the taskbar | done (2026-11-08) | the own host has no window at all (the GameHelper overlay and the plugin's `HostWindowHider` are gone) |
| Inline pictures in texts (gold, spirit, tribute, portals, revives, tick, check box, deferred, weapon set) | done (unit) | `InlineIcons` from catalog/inline-icons.txt (uisettings MarkupImages, extractor inline-icons); pad glyph tokens and tooltip price units |
| Table rows under column headings | done (live 2026-11-08: Instance Manager rows, every cell, the clock not ticking) | `TableHeadings` + `UiSemantics.Panels.HeadedCells`; Instance Manager, ladder, exchange; hidden headers and control rows skipped |
| Icon-only item controls | done (unit) | `ArtCaptions` from currency-art.json, only when nothing else names the control |
| Unseen panels inventory | done (research) | docs/research/ui-unseen-panels.md: 27 seen, 14 reachable now, 19 later, 50 not in PoE2 |
| Trial of the Sekhemas: Death Crystals said as they fall, room door opened said/guided | done (code, tested) | `TrialLines`/`CounterWatch`, `TrialDoors`/`TrialDoorWatch`; settings AnnounceTrialProgress, GuideTrialDoors; not seen live (docs/research/trials-and-campaign.md §7) |
| Trial icons named from the game's tables | done (code, tested) | catalog/trials.json via `trial-data` (242 icons, 10 languages); `UiSemantics.TrialIcon`; `TrialCatalog.PatternOf` |
| Trial of Chaos: round said on change | done (code) | `CheckTrialLines` on "Round {0}/{1}" |
| Ascendancy selector and tree view | todo (blocked: no ascended character) | needs one UI dump with it open and one closed (trials-and-campaign.md §3). 2026-11-08: yarkey's passive tree shows its class art in the centre and no ascendancy view or prompt (Y, LB, RB, X and the d-pad tried); the selector comes from the altar after the Trial of the Sekhemas, which no test character has done |
| Acts 3-4 puzzle objects and the sailing map | todo (blocked: the character is in Act 2) | trials-and-campaign.md §4 |
| Class resources from the game's tables | done (code; live: Heat with its maximum, vitals) | `ResourceWatch` + `ResourceTracker`/`ClassMechanicCatalog`, catalog class-mechanics.json; a maximum no player stat holds is read from the skills' level rows (`SkillStatSets`, live 2026-11-08) and, not being live, only marks where the pool is told as a count ("Calor: 31"), never as full; resources and limits the mod has no name for are logged, not spoken (code 2026-11-10) |
| Endgame data from the game files | done (code) | `endgame` step → catalog/endgame.json; docs/research/endgame.md |
| Map objective content in the scanner (breach, abyss, essence, Vaal beacon, detonator) | done (code) | Encounters, game's keyword names |
| Atlas map and tree nodes | done (code, not seen live) | name, content icons, asks/unlocks, links with directions, tower reach; dev `atlasnode` |
| Boss fight turns (vanish/back, invulnerable+adds, phase/form, enrage) | done (unit) | `BossMechanicsWatch` + `BossPhaseTracker`, catalog/boss-mechanics.txt; docs/research/boss-mechanics.md; a boss back at full life after the player's death starts over (`BossLifeSteps.FightRestarted`, unit) |
| Way out of a boss's danger area | done (unit) | `BossCounterplay`: "¡Sal!", "¡Al centro!", "¡Detrás!", "¡Muévete!", stick way, at the wind-up |
| Boss mechanic lines through the fight mute; movement rules from buff texts | done (unit) | 41 line groups, 10 languages; 15 stop/keep-moving buffs |
| Telegraph data | done (data) | GeometryAttack/Trigger/EffectDriven/SpawnObject/Mortar → telegraph-skills/effects catalogs; docs/research/attack-telegraphs.md |
| Patch coverage report | done (code) | `GameNews` + `GameNewsNotes`: per table new/gone, covered or not, spoken summary |
| Names for new content (nameless objects, league devices, harmful buffs, currency icons) | done (code; live partly) | `ObjectNames`, MinimapIconLabel, buff-names.json, currency-art.json |
| One executable | done | host/Poe2Access: launcher + host + extractor in one process; spoken regeneration; 52 s first regeneration |
| Radar labels in the game's language | done (live) | 92 of 211 labels with a game name; "En el mapa: Sala del jefe: Beira, de la manada putrefacta (10% de resistencia al frío)" near her arena (2026-11-08) |
| Popup lists with no focus (options' popup notifications) | done (live) | `NoFocusList` + pure `MovingIndexWatch`: the list whose index (`GameLayout.HighlightList` +0x398) moves with the stick under the last focus's layer; "…, marcado, casilla de verificación, 8 de 31", A toggles ([notes](docs/research/ui-layers.md)) |
| One dodge cue for blows together | done (live) | `DodgeCueMerge`: blows within 250 ms share one cue, rarer then nearer plays, a rare's never covered by a lesser cue; Mud Burrow swarm 2.87 → 1.66 cues a second, seconds with 3+ cues 500 → 117, recall unchanged ([table](docs/research/dodge-timing.md#one-cue-for-blows-together-2026-11-08)) |
| Every monster warned inside a trial | done (unit; not seen live: no character has entered a trial) | `TrialDodgeScope`: the HUD's honour or round line (the game's strings) turns `DodgeWarningsAllMonsters` on, back to the setting on leaving; setting `DodgeWarningsAllMonstersInTrials` (on), 10 languages |
| Scanner routes | done (unit) | jump point search, 2-10 ms on 1600x1600 grids |

## Derived facts (with source)
- **Rapid Shot heat:** player buffs `heat_79` then `overheated_79` 2.4 s later ("Player buff seen", log 2026-11-05 20:46); BuffDefinitions rows 2162 "Heat"/"Calor" and 2855 "Overheated"/"Sobrecalentado". Gem text at level 5: maximum heat 30, no firing or reloading for 10 s at the maximum. Measured "Arma sobrecalentada" to "Arma lista": about 10.4 s (log 2026-11-05 21:36-22:02: the 10 s plus `EndHoldMs`).
- **Skill level rows (2026-11-08, dat files + live):** `ActiveSkillDetails+0x70` holds the 16-byte key of the skill's GrantedEffectStatSetsPerLevel row (found by the table it names); that row keeps its additional stats as (count, pointer) to Stats keys at +0x81 and their int32 values at +0x2C (rows 0xB9 bytes). Rapid Shot's `maximum_number_of_crossbow_heat_stacks` = 25 + gem level. Stats keys are rows + 1.
- **Area row (2026-11-08, live, dev command `arearow`):** the host's WorldData chain (`[[WorldData+0x98]+0x98]`) reads 0 in 0.5.5f; `[AreaInstance+0xA0]` and the loading state's row (+0xEA0) hold the WorldAreas row (G1_town).
- **Count Geonor (Ogham Manor, `Metadata/Monsters/Baron/`):** the Count (`BaronBossHumanForm`) turns into the Wolf (`BaronBossCorruptedWolfForm`) near 49 % and stays behind alive for good: life 4333/9193, animation stuck on `SpellAreaOfEffectChaos`, Targetable `+0x69` still 1; only the buff `hidden_monster_79` tells him apart (`stuntrace`, 2026-11-05). The Wolf is 7 entities on one cell, consecutive ids 2764-2770 (`entities` dump); every life and ailment line came 7 times at once, the Count's too, though one Count remains. Why 7 is not known.
- Heavy stun buildup is "poise" in the game's data (Stats.dat: `poise_threshold`, `base_poise_threshold`, `poise_decay_delay_ms`, `is_heavy_stunned`, `cannot_accumulate_heavy_stun_poise`). GameHelper's `GameStats` enum is about 3 rows behind the 2026-10-26 Stats.dat (`poise_threshold` 17371+1 there, row 17374 now): compare raw keys, not enum names. Stat keys are row + 1.
- **Culling Strike skills:** granted effects whose main stat set (GrantedEffects +0xA8 → GrantedEffectStatSets, row 0x65,
  ImplicitStats key list +0x18) holds `kill_enemy_on_hit_if_under_10%_life` (Stats row 579; skill_stat_descriptions.csd
  describes it as "[CullingStrike|Culling Strike]"). Killing Palm's ActiveSkills description does not mention culling. 2026-10-29.
- **Player resources** (BuffDefinitions, 2026-10-29): charges, infusions, surges (`instilling_*`), Druidic Prowess and Arcane
  Surge are player buffs whose stack count is the number held (`power_charge_79` live, GameHelper adds "_79"). Rage, Combo,
  Glory and Valour are not buffs: Rage is Stats `current_rage` / `virtual_maximum_rage` (keys from `catalog/game-ids.txt`, not the stale enum), default maximum 30
  (KeywordPopups); Combo and Glory are per skill. No buff counts crossbow rounds.
- **Sound glossary volumes:** each glossary sound plays at its game volume (`SoundGlossary.VolumeSetting`); it played all at
  100 %, so a tester saw the harmful-ground alarm and the wall bump "not go above 100 %" (they do in play: +9.6 / +8.2 dB
  from 100 to 300 %, measured on the mixer's code 2026-10-29).
- Enemy state buff ids and display names: BuffDefinitions and KeywordPopups (`Poe2UiExtract dat-strings <game> KeywordPopups <Language> 1100`; the language name as the game spells it, e.g. "Spanish", "Traditional Chinese"; run from PowerShell with `[Console]::OutputEncoding = UTF8` or the text is mangled).
- Plugin contract, hotkeys (`Utils.IsKeyPressedAndNotTimeout`), events (`RemoteEvents.AreaChanged` public,
  `StateChanged` internal → poll), logging convention: `docs/research/gamehelper2-plugin-api.md`.
- `UiElementBaseOffset` (GameOffsets): Self 0x08, Children 0x10, Parent 0xB8, RelPos 0x100, StringId 0x128
  (StdWString, readable — StashUtility), Flags 0x168 (visible bit 0x0B), ScaleIndex 0x172, UnscaledSize 0x270.
  In-game root chain: `InGameState+0x2F0 → UiRootStruct+0x340` (controller: `+0x318`). Mouse-over entity
  chain `+0x300 → +0x3F0 → +0xA8`: `docs/research/gamehelper2-ui-tree.md`.
- Rect of any element from a plugin: `PluginUiElementReflection.TryGetAbsoluteRect(addr, out pos, out size)`.
- **Pre-game UI root (validated live 2026-09-14, PoE2 0.5.5b, LoginState; read on every start since, last 2026-11-08; no public tool documents it):** `LoginState+0x10` → an object holding
  element pointers at `+0x2B8, +0x2C0, +0x2C8, +0x2D0, +0x328…+0x438` (20 slots); climbing `ParentPtr` from any of
  them reaches the same root (`root_control`, rect 0,0 2560x1440, 12 children, 4104 nodes). The in-game chain
  (`InGameState+0x2F0`) is null pre-game. Evidence: `docs/research/evidence/ui-tree-LoginState-20260914-223105.txt`.
- Element fields confirmed on the login dump (4104 nodes): `+0x98` std::wstring = **control name** from the UI
  layout (`login_button`, `options_button`, `exit_button`, `gateway_combobox`, `language_combobox`, `textField`,
  `motd_frame`, `build_info_label`…; 69 distinct), `+0xC0` std::wstring = **font name** (Fontin / Fontin
  Smallcaps / OptimusPrincepsSemiBold). `StringId` (+0x128) empty on all nodes. Flag bits: `0x1B` = interactive
  widget (buttons, comboboxes, text fields), `0x10` = text input, `0x12` = hit-testable, `0x0B` = visible.
  `rect=(none)` also happens for zero-size labels.
- **Display text (validated on login dump 2, 5474 nodes):** label elements are a 0x700-byte subclass with
  `kids=0`; text is a std::wstring at **`+0x490` (primary, keeps trailing spaces)** with identical copies at
  `+0x360` and `+0x538` (one of them is TextNoTags — decide with a markup-bearing dump). Login button caption
  `[0,5,1,2,0]` reads "Log In". Element strides seen: 0x2E0/0x300 plain, 0x410 button-ish, 0x700 label.
  No tooltip/localisation-key string field found. Controller hint bar = root child `[0,12]`.
  **`+0x360` = text WITH markup ("save <<xbox_button_x>>"), `+0x490` = stripped text (spoken), `+0x538` = copy.**
  Text fields (`textField`) use the same fields ("Sao Paulo (BR) (Auto-selected)"). Buttons hold click sounds at
  `+0x2D8/+0x2F8`; their caption is child 0 (a label). All in `GameLayout.cs`.
- **Focus (8 login dumps diffed while moving the joystick):** `LoginState+0x2B8` → global UI manager `Z`;
  `Z+0x8C8` = root element, **`Z+0x988` = element under the gamepad cursor = activation target** (null right
  after the screen opens), `Z+0x990` = last focused / default target (copy at `Z+0xBB8`, lags one step — the
  first implementation used it and announced the previous control; confirmed wrong by live activation),
  `Z+0x9A0` = unknown (only ever options_button). `LoginState` also holds typed control pointers: `+0x2E8 login_button`,
  `+0x340 gateway_combobox`, `+0x348 language_combobox`, `+0x368 exit_button`, `+0x370 options_button`,
  `+0x378 patch_notes_button`. Hypothesis to test in game: `InGameState+0x2F0` (GameHelper's UiRootStructPtr)
  == `Z+0x588` (root at +0x340 = Z+0x8C8), so focused would be `UiRootStructPtr+0x408`. Login screen = Steam single "Login" button + gateway/language combos;
  the terms/account popup is `[0,11] link_account_prompt_window` (hidden at dump time).
- Raw reads from a plugin: reflection into `Core.Process.Handle` (`HostMemoryReader`), StashUtility pattern.
- Game element layout hints (ExileCore2): `Text`, `TextNoTags`, `Tooltip`, `isHighlighted`, `Type` ushort;
  `IngameState.UIHover/UIHoverElement/FocusedInputElement`: `docs/research/exilecore2-study.md`.
- Terrain/entities/landmarks (for later): `docs/research/poe2radar-study.md`.
- Local players (couch co-op): std::vector at GameHelper's `LocalPlayerStruct`+0x08, entry 0 = `AreaInstance.Player`,
  entry 1 = player 2; J2's pad and menu cursor never reach the mod; co-op HUD has one `skill_bar` per player
  (`docs/research/local-coop.md`).
- **Entities (2026-10-23, 0.5.5e, `entities` dev command):** Render `+0x160` std::wstring = display name ("Una",
  "Stash", "Clearfell"; for players the class). GameHelper's Targetable flags (`+0x51…`) read zero for everything;
  **`+0x69` = targetable/in play** (1: active NPC variant, stash, waypoint, bench, exit, players; 0: story variants
  not in play — the town holds RenlyIntro and RenlyAfterIronCount, Una ×4, HoodedMentor ×3). AreaTransition
  `+0x48` → WorldAreas row (`+0` id "G1_2", `+8` name). Other players: GameHelper `Player.Name`.
- **Walkable grid is edited in place:** GameHelper copies `TerrainMetadata.GridWalkableData` once per area; the game
  rewrites the same vector when tutorial barriers open (266 cells differed by the Miller). Routes read it live.
- **Distances:** `WorldToGridConvertor` ≈ 10.87 world units per grid cell; a spoken step = 100 world units ≈ a
  quarter second of walking. A* over 1800 cells ≈ 200–320 ms (on the frame thread for now).
- **Named places:** GameHelper Radar's `important_tgt_files.txt` (area id → tile key → label; 91 areas, no G1_1)
  plus our `catalog/landmarks.json` (same format; G1_1 → Clearfell Encampment). The area id is the WorldArea row id.

- **Offline UI definitions (2026-09-14):** the game's menu layouts are plain UTF-16 scripts in the bundles,
  `metadata/ui/{pregamestate(23), ingamestate(100), loadingstate(3)}` — extracted to `external/poe2-ui/` with
  `tools/Poe2UiExtract` + `ooz`-based `oo2core.dll` (see `tools/README.md`). Format: `begin <Class> id = <name>;
  text = { <LocKey> }; can_focus_with_controller = true; tab_index = n; ... end`, with `include` of common files.
  The `id` is exactly the control name we read at `+0x98`, and the class (Login_Button, FourComboBox,
  MOTDDisplay...) gives the widget type offline. Analysis in `docs/research/ui-layouts.md` (in progress).

- **Layers, manager, in-game root, enabled bit 0x0D, placeholder +0x798 (2026-10-23):** `docs/research/ui-layers.md`.

- **Terrain layers (2026-10-23, `terrain` dev command):** four nibble grids of the same size at Terrain `+0xD0/+0xE8/+0x100/+0x118`
  (Terrain = AreaInstance+0x8D0). `+0xD0` walkable = distance to the nearest blocked cell (0 blocked … 5 far). `+0xE8`
  (GameHelper's `GridLandscapeData`) is 0 on 0.89 M of the 2.2 M unwalkable cells and 5 on the rest (water, chasms):
  read as the projectile/sight layer for enemy sounds. `+0x100/+0x118` were all 5 in Clearfell.

- **Vendor / stash / tooltip price / text colour `+0x6D0` / off-screen panels (2026-10-23):** `docs/research/ui-layers.md` (last section).

- **Mods.dat row (2026-10-23):** +0 Id, +0xA ModType key, +0x1E stat keys, +0x5E domain, **+0x62 Name** (client
  language, gender branches "<if:MS>{de Rabia}<elif:FS>{…}"); row size 0x2B5 (table walk from a monster's
  ObjectMagicProperties mod row; `entities` dev command prints each monster's mod rows and spoken names).
- **Inventory panel G+0x378 (2026-10-23):** full-screen root with `panels_parent`; the gold counter is the amount
  text whose child image is `CoinPile`, at panels_parent[0][0][0][8][0], and it keeps its text while hidden.
- **Render.ModelBounds** X/Y read as body size in world units: player 30, Drowned 52, Miller 85 (2026-10-23).
- **Network bubble (2026-10-24, Riverbank):** the live walkable grid is only true near the player. TutorialBlocker3's
  footprint (x 474–494, y 890–945, 595 cells) read walkable from 250+ cells away and closed at ~200: the guide
  flipped between two routes. Closed readings are trusted at any distance (`BarrierMemory`). Evidence: the
  `terrain` and `routed-*.walkable` dumps compared offline with `astar.py`.
- **TriggerableBlockage +0x30** = IsBlocked (GameOffsets; 1 → 0 seen live). Riverbank tutorial barriers carry
  Counter (+0x18 = 1 while closed), ProximityTrigger, Timer, Transitionable (+0x120 state). They open on tutorial
  steps, not kills: TutorialBlocker3 by the Large Chest once its gem (Exhumar) is socketed and cast,
  TutorialBlocker4 at the Miller's arena once the passive point from his kill is spent and applied (X).
- **Item Base name** is a std::wstring at name row (Base +0x10) +0x30: names under 8 characters are inline (SSO);
  GameHelper reads it as a pointer and returns "" ("Exhumar", a gem on the ground, was "objeto desconocido").
- **Chest +0x168** (GameHelper IsOpened) is right: 0 → 1 on opening GallowsTutorialChest2; +0x16D is 1 in both states.
- **Combo box +0x538** byte = 1 while open; +0x528 the highlighted index moves with the D-pad (gateway list);
  in-game option combos open in place (no list on screen).
- **NPC +0x50** = dat reference {row, file} to MiscAnimated.dat: row +0 → "NPCQuestMarker" on NPCs with the quest
  indicator (RenlyIntro, Una, Renly at the forge), null on the others (Clearfell, 2026-10-24).
- **Passive tree:** applying changes gives the canvas (+0x3D8) a new state object T; T+0x1A8 then lists the
  applied node (44871, Escudo de energía, Witch). Node element +0x64C = 1 on it.
- **Inspect mode / compare:** R3 on an item (glyph `RightStickButton2.dds`, legend "Comparar"); the inventory's
  R3 legend is "Comparar", a gem's "Inspeccionar".
- **Uncut gem tier** label is named `tier_label` ("I").
- **Element flags bit 14** (`NavigableFlagBit`) is set in game on what the controller cursor lands on: equipment
  slots, Skills rows, the spirit panel, bind screen slots, skill picker cells; in-game controls lack bit 0x1B
  (bind screen and Skills dumps, 2026-10-24).
- **Buried larvae** (`HuhuGrubLarvaeEmergeSummoned`, Mud Burrow) are ordinary alive, targetable monsters; GameHelper's
  "untargetable" in the entity dump is its broken flag (+0x51), not ours (+0x69).
- **Tutorial lessons freeze the character** until their action: the flask lesson ("Has recibido daño. Presiona
  cruceta izquierda…") held a fight for 20 minutes until D-pad left (Mud Burrow, 2026-10-24).
- **Bind screen actions** on a bound button: A picks the skill up (hint bar "Colocar, A. Cancelar, B"), X opens
  the picker ("Cambiar habilidad"), Y empties it ("Quitar"); A on an empty button opens the picker. The picker's
  red cross is `UIImages/InGame/Cross.dds` and empties the button (chosen on RB, Chaos Bolt bound back, 2026-10-24).
- **Cosmetics weapon-set toggles** draw their numeral as a child's `…/Cosmetics/WeaponSetIcon1.dds` / `…2.dds`.
- **State objects can end near an unreadable page:** SelectCharacterState sat 0x1CF0 bytes before one, so a
  0x2000-byte read failed whole; `UiManagerLocator` reads it in 0x100 chunks (2026-10-24).
- **Input method** ("Haz clic para moverte" / WASD / "Control/Mando") only changes at the main menu or character
  selection; entering with the mouse binds the keyboard as player 1 and a pad plugged in afterwards is ignored.

- **Object sizes by allocator stride (2026-09-15):** slider 0x410, check box 0x700, label 0x700, combo box 0x800; bytes past
  these belong to the neighbouring object (the deep-probe hex must be read with that in mind).
- **Quest tracker (UI dump 2026-10-27):** `quests_display_pages_root` holds the pages; each quest a `title_label` and its
  `quest_info` lines 5–8 levels down; only the shown page is drawn; `page_cycling_legend` beside it.
- **Freythorn ritual altars (.ot files, 2026-10-29):** `Metadata/Terrain/Gallows/Act1/1_12/Objects/CanopyRitualAltar` and the
  King's `CanopyBossRitualAltar`: typed Monster, icon `RitualRune`, `RitualRuneFinished` once done; state 0 untargetable with
  monsters around, 1 ready, 2 active, 3/4 done. The King's altar opens once the others are done.

## Code map
- Sources: `Poe2Accessibility/Services/<folder>/` by subject, one namespace ([folders](Poe2Accessibility/Services/README.md)); `Announcements*.cs`, `GameLayout.cs` and the core's parts at the plugin's root.
- `Poe2AccessibilityCore` — wiring + per-frame tick (GameHelper `DrawUI`), no logic; partial files `.Gameplay` (service
  ticks isolated per service, vitals and skill bar readouts, `QuestTexts`), `.States` (game states), `.Input` (the pad),
  `.Creation` (pre-game help, `CreationNameGuide`), `.DevCommands` (command file, status), `.Compatibility` (layout
  self-test, compatibility report), `.Settings` (host panel, applying settings, hotkey checks).
- Patch survival, offline: `CheckCodeFields` + `CodeFieldCatalog` (`catalog/code-fields.txt`) → `OffsetHealer.PlaceFromCode`;
  live: `WorldLayoutWatch.JudgePoise`/`JudgeWalkable` (`PoiseSearch`, `WalkableCheck`);
  objects' sounds: `ObjectSoundExtractor`, `ObjectSoundLinks`, `AoSoundEvents`, `EventRenderer` → `sounds/breakables.txt` → `Breakables`.
- Speech: `PrismOutputService` (own thread, logs every line) behind `IAccessibilityOutputService`, over `PrismLibrary`
  (the PRISM binding, also linked into the executable's `StartupSpeech`; `PrismTests` checks it against the header);
  all spoken strings in `Announcements` (incl. button names, item/slot phrases; `.Gameplay`, `.Passives`, `.Settings`, `.Compatibility`,
  `.Combat` and the rest by subject), translated in `catalog/lang` (`Localization.Pick` for the catalogs' own texts).
- Focus: `UiManagerLocator` (manager per state, `element+0x90`) → `FocusTracker` (`M+0x748`, value
  polling, layer of the focus) → `UiSemantics` (role/label/value; in game: `InventoryReader` grids/slots,
  keyword links, row text, panel title) → `AccessibleUiNode`.
- In-game archetypes: `UiSemantics.InGame` (inventory, links with row and definition, panel title),
  `.Tooltips` (+0x120), `.Geometry` (value drawn by a sibling), `.Cards` (trade listings, pure `CardText`), `.Slots` (button-bound slots), `.Lists`
  (selectable lists, `selected_*` marker rows), `.SkillBar` (HUD bar + bind-screen glyphs + dat chain to names),
  `.MapMarkers`, `.OptionLists` (NPC dialogue).
- Screen: `ScreenWatcher` (background snapshots of visible layers: texts, controls, enabled bit, glyph
  pairing, a tab bar's end buttons by `TabBar`, row merge) → `ScreenDiffer` (pure: two-snapshot confirmation, counters,
  a panel control's availability, top layer) →
  `ScreenNarrator` (`OnScreenChange` dispatches by layer: hint bar, `.Focus` the focused control, `.InGame` focus layer / objective box / HUD,
  `.PreGame` popups and top layer). `ChatWatcher` reads NPC/system/whisper/party chat lines.
- Memory layout constants: `GameLayout` (each with provenance). Glyph tables: `ControllerGlyphs`.
- Research: `UiTreeDumper`/`UiTreeReader`, `ResearchSession`, `FocusResearchLog`, `DevCommandFile`.
- Controller: `GamepadReader` (pad state M+0x768) → `L3Gestures` → `ModCommands` (menu lines / gameplay); a D-pad press that
  moves nothing says the control again: pure `BlockedMove`, `Poe2AccessibilityCore.WatchBlockedMove` (heard live 2026-11-10: the guild tab, a dialogue list's end, an item page).
- Local co-op: `Seats` (followed, partner, asking player; `InputSeat` picks whose pad and cursor, `PlayerStride`), `LocalPlayers`,
  `LocalInventories` (player 2's server inventories), `PlayerAlerts` + `SeatOutput` (named per-player news), `CoopDowns`.
- Scanner: `EntityCatalog` (entries, landmarks, `Describe`; `.Classify` categories and names, `.Terrain` live grid and barriers) → `RoutePlanner` (A*, corners,
  legs) and `ScanModel` (`ScanBrowser`, `StickDirection`); `LandmarkTable`. Map marks: pure `MinimapMarks` (signs, boss
  rooms by icon or tile, pointer reach), `MinimapIconLabel` (an icon's translated label). Exploring: `ExplorationMap`
  (frontier, pockets, `WayOn`), `MapCorner` (the part of the map, for the area's line), `LostHint` + `LostWatch` (the sound for a lost player, `assets/loops/`), `MapHintNarrator` + `MapHintTable` (GGG's cues), the radar's unexplored mark. Memory: `AreaMemory` (instance + layout; `Frame`/`Inherit`/`Framed`: a fixed map's exits into a new instance). Warnings: `VitalAlarm`.
- Sounds: `AudioCueService` (winmm mixer; each sound's own level by glossary key, `SetLevels`) ← `EnemyCues` (`EnemyCueScheduler`, `CueSynth`) and `Radar`
  (`PassageScan`, `RadarSensor`, `RadarSynth`; a second `Radar` follows `Seats.Partner`); with `ProximityNarrator` (`ProximityTracker`) gated in
  `Poe2AccessibilityCore.TickSounds`.
- Salvage bench: `InventoryWatch.Salvage` (+ pure `SalvageOutcome`), `UiSemantics.FirstLabel` (side panel without a framed title).
- Shop/stash: tooltip `cost` → `CurrencyIcons`, red price via `UiElementReader.IsTextRed`; grid tabs `UiSemantics.GridTab`;
  a search's lit items `SearchHighlightWatch` (+ pure `SearchHighlightNews`, `InventoryReader.HighlightOf`/`ItemsOf`); an unmet requirement pure `UnmetRequirements`.
  A grid read whole (L3 long hold): `ModCommands.WholeGrid` → `UiSemantics.ReadGrid` (`UiSemantics.GridReading.cs`) + pure `GridReading`.
  A merchant's or stash's opening line: `ScreenNarrator.SayPageWithFocus` / `SayHeldPage` (the gamble's stock label, the tab, the focus, then the gold).
  The build's steps over the marked nodes: pure `BuildPlan.StepsOver` ([two counts](docs/research/passive-tree.md#the-builds-two-counts)).
  Microtransaction shop: `UiSemantics.ShopPage`, `.Cards` `ShopEntryOf` (cards, position), `CardText.ShopImageTitle`
  (featured banners), `FocusTracker.InGameHighlightRoot` (the shop's highlight markers; the game focuses nothing there);
  item page `UiSemantics.ShopPage.cs` (`ShopItemPageOf` title and text, `ShopBuyButtonOf`) + pure `ShopPurchase`, points `ShopBalanceWatch` (the account's and the guild's), the chosen card's root `ShopHighlightRoot`.
- Gameplay guides: `Beacon` (+ pure `BeaconMath`), `QuestMatcher`, `BossWatch` (boss life + dodge whoosh, pure
  `DodgeThreats`), `TargetNarrator` (aim names + crosshair), `StatusWatch` (+ pure `StatusAilments`), `EnemyNarrator`, `DeathWatch`,
  `ObjectSounds` + `GameSounds` + `ItemDropSounds` (game WAVs), `CheckpointPlaces` (+ pure `MapPlaces`),
  `ScreenProjection` (the camera's matrix read from GameHelper; + pure `CameraView`: places in screen heights, the 16:9
  view, the measured fallback), `MapPinNarrator` (+ `UiSemantics.WorldMapPins`), `FlaskWatch`, `ExperienceTable`, `LootFilter`, `ItemSoundGroup`, `ItemSoundSet`,
  `FightWatch` (+ pure `FightState`: ground items and characters' chat lines quiet in fights).
  Combat state cues: `CullWatch` (+ pure `CullThreshold`), `EnemyStateWatch` (+ pure `EnemyStates`, `EnemyStateNews`),
  `ShieldWatch` (+ pure `ShieldStance`, `StunMeter`), `WeakPointGuide` (+ pure `WeakPoints`); player skills: `PlayerSkills` (granted
  effect ids via `DatKey`, Parry and `CullSkills`), pure `ParryTiming`; research: `StunTrace` (dev `stuntrace`).
  Skills ready: `SkillCooldownWatch` (+ pure `SkillCooldowns`) in `PlayerAlerts`; dev command `cooldowns`.
  Player resources: `ChargeWatch` (+ pure `PlayerCharges`) in `PlayerAlerts`, beside `StatusWatch`.
  Speech priority: `IAccessibilityOutputService.Urge` (later interrupts queue for 1.5 s), `.Keep` (tutorial lessons; `SpeechFloor` holds what comes meanwhile); `SpeechRepeats` holds back a line repeating the last.
- Static data from the game's files ([file catalogs](docs/research/file-catalogs.md)): `PassiveTreeCatalog` (`catalog/passive-tree.json`: nodes, links, class
  starts, names; files first, the rows and the edge list in memory as fallback and cross-check, pure `CatalogCrossCheck`), `GemCatalog`
  (`catalog/gems.json`) with `GemIndex`/`GemInfo` (a gem's identity by any of its Ids or its name, for the Build Planner) and pure
  `SkillNameCheck` (the game's skill names first, the files' when its read is no text or judged moved); pure `StatKeyCheck` (the stat key rule on the character's level).
- Passive tree: `UiSemantics.PassiveTree` + pure `PassiveStick` (the game's stick pick), `BuildPlanFolder` + pure `BuildPlan`/`BuildNotes`
  (Build Planner files, the tree's match kept per character; the build's gems: `UiSemantics.BuildGems` + pure `BuildGems`, `GemNames` over `GemIndex`, `ServerInventory`, dev command `inventories`; the build's gear: `UiSemantics.BuildGear` + pure `BuildGear` (file entries, piece per level, slot by id folder, modifier match) over `IItemBases`/`IModTexts` (`GearCatalogs.cs`), set at `BuildGear.Bases`/`BuildGear.Texts`); finder `UiSemantics.PassiveFinder` + pure `PassiveMap`/`PassiveFinder`; dev commands `passivenear` (nodes around the hovered one, build marks), `passivemap` (points from each start). Minions: `MinionReport` + `MinionGroups`.
- Values that must not go stale ([audit](docs/research/robustness-audit-2026-11-10.md#stale-values-2026-11-10)): pure `FocusNews` (a number or a row changing under
  the cursor, a Skills row's captioned figure), `BareHover` (a control under the free cursor with no tooltip, read by `FocusTracker`), `ItemReadings` (moved items), `GroundTooltip`, `BeaconMath.Gone`, `ScanBrowser.InArea`, `SpeechFloor` readings, `MinimapMarks.RoomDefeated`.
- Tests: `tests/Poe2Accessibility.Tests` (differ, text edits, chat diff, gestures, scanner, route legs, life
  alarm, beacon, quest matching, cooldown, skills ready, ailments, dodge threats, map markers). Dev tools: `tools/computer_use/`; dev commands
  `entities`, `hints`, `gesture <Tap|DoubleTap|Hold|StickClick|FlickUp…>` (command file works without ResearchMode).
- Start-up (`host/Poe2Access`, `Launcher/`): `Updater` + pure `ReleaseTag` (auto-update), `Instance` (mutex, stop event, strays), pure `AppFolders`/`AppCleanup`/`CommandLine`, pure `GameInstall`
  + `ProcessAccess` (Korean client), `GameDataSetup` + pure `GameDataStamp` (per-step versions; catalogs kept in `app/GameData`, survives mod updates).

## Next step
Done (2026-11-10, late, 22:10 to 22:58, unattended on yarkey and malgdhler): the audit's N1 and N2, the slider at its minimum, the plugin-missing line, the dropdown latch and the item modifiers in English checked live; the Cosmetics slots, the Options' "Guardar X" and the tab bar's hints fixed on the way (Known issues, Fixed). Not released.
Done (2026-11-10, evening): every open entry of the Known issues and of the audit settled as fixed, not a fault or blocked (below); item modifiers without the tooltip merged and checked live on two characters; rare and unique items named without their tooltip; the game's patch of 21:15 taken with the mod (data regenerated, no field moved). Not released.
Done (2026-11-10, infrastructure and readings, live on yarkey in G2_town): dead and duplicated code out of the plugin, the host, the extractor and the tools, the big files split; `tools/build.ps1` builds and checks in one command and the release refuses a dirty tree; stashes and wells sound and a quest's name no longer makes the well its objective; a stash's search says what it lights; an item that cannot be used says which requirement is missing; the tree's stick check no longer reports a game update, the Build list says its progress and a new list walks what is taken outside the build; cells that read empty for a moment are not said ([audit](docs/research/robustness-audit-2026-11-10.md), [ui layers](docs/research/ui-layers.md#items-in-a-grid-search-highlight-unmet-requirements-055f-2026-11-10)). Released **v2026.11.10-7**.
Characters: every fight was on **yarkey** (level 23 Mercenary, a spear with Whirling Slash since 2026-11-09). The user deleted **`acceso_prueba`** and **frajtnir** on 2026-11-09; left: hairin (Huntress 4), kehgan, malgdhler (Druid 8), yarkey, yarkidius.
Next: (1) **Bosses by hand** — the Bloated Miller with a new low character (a minion gem first; its tutorial pause times the 300 ms lead), Count Geonor, the King in the Mists and Act 2's bosses: the script cannot play them without looking like a bot (user, 2026-11-08). (2) **Steam Deck / Proton:** speech is ready (PRISM 0.18.3 and its Wine bridges ship through the mod's own binding, NVDA still picked on Windows; [Proton](docs/proton.md)) but untested; a Linux player must confirm the speech, the start from Steam's launch option `poe2access-proton.sh` (2026-11-09: `protontricks-launch` drops `WINEDLLPATH`, and SAPI is mute, per the D2R mod's test) and the memory reading under Wine. (3) **Blocked, each for its reason:** a vendor price in another currency and the Trial of the Sekhemas (a Djinn Barya and a run yarkey survives); the trials, the Atlas, maps and Acts 3-4 (no character there yet); the fields left with one protection or none ([audit](docs/research/field-protection-audit.md) §2: no function reads them alone, or only a mouse judges them); the Mercenary's crossbow ammunition in memory (not needed for now, user 2026-11-08); GGG's written approval (emailed 2026-10-26).

Earlier chunks, newest first: [status history](docs/status-history.md).

## Known issues / open questions
Settled on 2026-11-10 (evening, unattended, yarkey in the Act 2 town; the realm closed for a patch from 21:01 to 21:25):
every entry is **fixed** (how it was verified), **not a fault** (why) or **blocked** (by what, and the one action that
unblocks it). The audit's own lists, entry by entry:
[robustness audit](docs/research/robustness-audit-2026-11-10.md#fourth-pass-2026-11-10-the-whole-code-in-four-parts).

### Fixed
- **A worn weapon said twice on its first focus** (base name, then the tooltip's name 170 ms later). An item its
  tooltip names waits up to 300 ms for a tooltip not up yet (`TooltipSettle.NameWaitMs`; a normal item never waits) and
  a rare or unique item is named from its own words at once. Heard, one line on each set: "Mano principal, Filo
  quimérico, raro, Build: 0 de 3 modificadores. …" and "Segundo set de armas, mano principal, Lanza de guerra dentada,
  mágico, Build: 1 de 3 modificadores. …".
- **Rare and unique items named by their base where no tooltip was read.** The item keeps its name as Words.dat keys in
  its Mods component (`GameLayout.ItemNameWords`, [item mods](docs/research/item-mods.md#the-items-own-name-without-the-tooltip-2026-11-10),
  [evidence](docs/research/evidence/item-name-words-2026-11-10.txt)). Seen live: with no tooltip read, yarkey's worn
  pieces by their names ("Égida de glifo, raro", "Estallido repentino, único"…), then "56 titles agree, 0 differ" (dev
  `layout`); on malgdhler "Estrella quimérica", "Banda de kraken", "Zarpa de apocalipsis" and "212 titles agree, 0
  differ" (his helm first read "Estrella quimérico": a singular noun's row gives its gender alone, fixed the same
  evening). Ground labels and moves use the same call; none was seen (no item on the ground in a town, moves not
  allowed). A magic item keeps no words: by its base until its tooltip is read.
- **Item modifiers without the tooltip** (the other agent's work, merged and checked live; procedure and results in
  [item mods](docs/research/item-mods.md#live-check-2026-11-10)). With no slot focused since the mod started the
  inventory opens with "Build: 3 de 6 piezas con su base, 3 de 15 modificadores, leídos en 6 de 6 piezas. Más lejos de
  la build: Mano principal", and the line is the same after every slot was focused. `itemmodscheck`: yarkey 15 items,
  54 lines equal, 1 differ (a charm's own "No te pueden aturdir", the base's line), 2 uncertain (the two spears' skill
  grant, outside the description file); malgdhler 10 items, 27 equal, 0 differ, 0 uncertain; hairin 9 items, 20 equal,
  2 differ, 3 uncertain, her belt equal after the fix; 12 stash items, no difference logged. Four faults found and
  fixed on the way: a spear's skill grant made the whole piece "not compared" (the summary read "3 de 9" before the
  slots were focused); "+1.4%" and "+1.12%" (hundredths) were in doubt; 201 a minute was written "3.4" where the
  tooltip shows "3.3"; a regeneration rounding to 3.0 was written "3.0" where it shows "3". A unique helm's line whose
  stat is in two blocks of the description file is certain since both blocks write it alike (`ModTextCatalog.AlikeBlock`,
  2026-11-10); the spears' grant stays in doubt: no file links `local_display_grants_spear_throw_skill` to a skill
  ([item mods](docs/research/item-mods.md)). Still unseen: a piece with a rune socketed, an
  enchanted or corrupted item, one stat on an implicit and an explicit, a modifier of three values, a number over 999.
- **The passive list's "4 más de la build no están en este árbol".** They are the four nodes of the file's ascendancy
  ("ascendancy": "Mercenary3": `AscendancyMercenary3Small1`, `…Notable1_`, `…Small4`, `…Notable4`; 60 + 4 = the file's
  64 nodes; every ascendancy row of the game's files is named `Ascendancy…`, 669 of them). Heard: "Build, 16 de 60
  asignados, 13 puntos fuera de la build, 4 más de la build son pasivas de ascendencia, que este árbol aún no muestra";
  the ids are logged once (`Build nodes not on this tree (4 of an ascendancy): …`).
- **`SpeechPriority.IsReading` ageing events** under a reading kind: only a number that is not urgent and not marked
  as something that happened is a reading, at every level (`OnlyANumberIsAReadingAtEveryKindAndLevel`). Unit tests.
- **The mod ended when the game window was not up in 240 s.** It waits while Steam or the game runs and says so.
  Heard on the patch of 2026-11-10 21:17, while Steam verified the install: "Sigo esperando la ventana del juego" at
  30 s, then every 60 s (`Poe2Access.log`: `Spoken (0): Sigo esperando la ventana del juego`, five times).
- **A game restarted patched under the running mod** left it on the old build's game data without a word: the plugin
  says "El juego se actualizó. Reinicia el mod para actualizar sus datos" (`GameBuildChange`), the host logs it. Unit
  tests; on that evening's patch the mod was started again with the game, which regenerated the data ("Actualizando
  los datos del juego tras el parche; tarda unos minutos", 28 steps in 30 s, `Code fields: 52 of 52; moved: none`).
- **The launcher silent while a data step hangs.** "Sigo trabajando en <paso>, unos N min" after 30 s without the
  count moving, then every 60 s (`SilenceReminder`). Unit tests; not heard, no step stalled on that evening's patch.
- **`InventoryItemStruct` log bursts on area load.** A cell that cannot be read is skipped without a line
  (`Inventory.UpdateData`). Code only; no such line is in the logs at hand (the host log is rewritten at each start).
- **The Beacon's "way stops short" when the search only ran out of budget**, and **an object named by its file's
  English words**: decided and built, see "Decisions the owner can overrule". Unit tests; the second heard in the Act 2
  town's scanner: "Objeto sin nombre, Chair, 9 pasos, arriba a la izquierda 2, luego arriba a la derecha".
- **The status line named the last character's area at the login screen** ("Inicio de sesión. Área: La caravana de los
  ardura", heard): the area is said only in a game (`TheAreaIsSaidOnlyInAGame`).
- **A beside another player opened that player's menu with no word that it is a player.** Heard: "Jugador:
  Tejetormentas Niv. 27, Liga Forbidden Rites, Bleidz. Invitar al grupo, 1 de 5" (`Announcements.ButtonMenuContext`,
  when a player of that name is in the area). In a crowded town a player can step onto the spot between the A target's
  line and the press: the menu then says whose it is, and B closes it.
- **The character's name after a change of character:** yarkey, malgdhler, yarkey in one session, each time the build
  summary of the character in play. **The stash, the salvage bench and a merchant in turn:** each title on its first
  line ("Alijo, pestaña 1. Inventario. …", "Mesa de reciclaje. Aquí puedes reciclar … Orbe de transmutación, 6, columna
  1, fila 1", Zarka's "pestaña 1. Inventario. Pergamino de sabiduría, 8, columna 1, fila 2, 16 oro", then "4.776 oro").
  **No screen snapshot cut short** with the tree at every zoom from x0,4 to x1,9. **The tree's place after a restart**
  of game and mod, on hairin, who follows no build: "Evasión, asignado, Donde lo dejaste, Armadura: izquierda, 2"
  (with a build active the build's next node leads instead, by design).
- **A slider at its minimum focused first could be read with another number.** Read where measured, nothing searched
  (`UiElementReader.ReadSliderState`). Heard (late session): the ground labels' size taken from 100 to its minimum, 80
  (no slider outside Sound goes to 0), and the mod started on it, the first slider that process read: "Interfaz.
  Escalado de la IU. Tamaño de las ventanas emergentes de los objetos del suelo, 80, control deslizante, 3 de 6, …";
  put back and heard, "…, 100, control deslizante, 3 de 6".
- **Exit tins flapping to "open" and back:** a confirmed exit holds for 1.5 s (`RadarSensor.ExitHoldMs`). Unit tests.
  Beside Clearfell's exit in the encampment (malgdhler, dev `sounds`): 22 s standing, no radar cue at all; 16 small
  steps to and fro over 3 cells in 26 s, `radar_exit` once, `radar_wall` 7, `radar_open` 6.
- **Late session (2026-11-10, 22:10 to 22:58), found on the way and fixed, each heard:**
  - **The Cosmetics slots were read as a trial's rooms** ("Efecto para arma, botón, columna 4 de 31, sala 1 de 1, aún
    no puedes entrar"): 31 columns of one button each fitted the Trial map's shape. A map has a choice of rooms
    (`TrialMap.IsMap`, unit-tested). Now "Efecto para arma, botón, 11 de 19".
  - **Options: "Guardar X" turning usable was never said** (only "Cancelar B"); its text does not change. A panel's
    control whose availability flips is news, the HUD's never (`ScreenDiffer`, `APadButtonTurningUsableIsNews`). After
    a change: "96", "Guardar X. Cancelar B"; back: "100", "Guardar X, no disponible. Cerrar B".
  - **The hints named the tab and heading beside the tab bar's shoulder icons** ("Entrada, RB. Idioma, LB"), which
    also dropped them from the panel's reading. The two icons a tab control holds at its ends are their own legends
    (`TabBar`, `ScreenWatcher`; two new phrases in the ten languages): "Pestaña anterior, LB. Pestaña siguiente, RB.
    Seleccionar, A. …", and the Options read "… Cerrar B. Entrada. Sonido. Juego. Gráficos. Interfaz. Escalado de la
    IU. …".
  - **A plugin that is missing:** the development host started with its plugin DLL set aside said "El plugin de
    accesibilidad falta o no se cargó, así que el juego no se leerá" (`Poe2Access.log`: `plugin missing`, `Spoken (0)`).
  - **The dropdown latch** with two owners: `Combo entry layout resolved` by the chat's channel list ("Global"),
    `settled` by the Options' renderer list ("Vulkan"); the language list then read all ten entries.
  - **Item modifiers on the English client:** the same counts as in Spanish
    ([item mods](docs/research/item-mods.md#live-check-on-the-english-client-2026-11-10)).
- **A Skills row's figure that changes under the cursor** (audit N2). The rows read live in both languages: "NIVEL: 8,
  Lanzamiento de lanza, DPS: 205,1", "NIVEL: 5, Heraldo de trueno, Daño: 16,1", "NIVEL: 5, Atrición", "vacío" ("LVL:
  8, Spear Throw, DPS: 205,1"); none holds a clock or a cooldown. A `Panel` row's captioned figure that changes is said
  alone ("DPS: 211,3"; `FocusNews.ChangedNumbers`, `StaleValueTests`); a panel's other numbers stay quiet. Not heard:
  see Blocked.
- **From the audit's lists, code and unit tests** (each named there): `ui-catalog` failing when it loses more than 2
  files in 100; a language lost from `gems` or `passive-tree` refusing the catalog; a spoken line when the plugin does
  not load; one log line for a burst of failed reads; a timed-out batch stopped; the parallel map walk bounded; the
  GGPK closed; the dropdown latch by name, not address; the screen watcher's reset race; the route planner's memory;
  checkpoints named past the pairing budget; object cues without the game's sounds; a grace for charges after an
  area's first reading; the skill stat key searched again; the wall-clock tests; the tests that did not test; a known
  row from the game's files for every key column of the endgame and skill tables; the data step "game names", which
  fails naming any buff, animation, trap or door state or boss tail a rule matches that a patch removed; the rules of
  host-bound classes lifted into tested pure code (the guide's way lines, boss life, the target's name, the data step's
  plan, the update offer, the game process's watch, the layout's loss counters, flag bit rounds, skill name check).
- **Character selection after a quick login** (the login's bar said again after the character): the bar is dropped
  when the state changes (`2840bbb`); heard again that evening, the bar once, before the character.
- **Microtransaction shop, too few points:** "Comprar puntos, Esta compra requiere 250 puntos, tienes 150 puntos,
  botón" (heard 2026-11-10).
- **The quest tracker between characters:** `QuestTrackerPage.Reset` runs at character selection
  (`ScreenNarrator.CharacterChanged`); it was listed as open by mistake.

### Not a fault
- **Closing a full-screen panel reads the whole quest tracker again:** not inside one run. Of 113 quest pages in two
  play logs none repeats an unchanged page without a mod start in between (the audit's stale-values part); a mod
  started under an open panel must still say the objective.
- **An item's first focus is said about 100 ms later than before** (`TooltipSettle`): the price of a tooltip read
  whole; an item whose tooltip was seen once is said at once.
- **A cell read whole again when its value changed during the 300 ms wait:** the line it replaces had not been spoken.
- **Skill tooltips read "Costo." with no value:** the game draws the header with nothing under it.
- **A text appearing right after a focus move repeats the focus with it:** by design, so the name is never cut off.
- **Character selection with the virtual DS4** ("J2 Presiona A para unirte"): the test tool's limit, not the mod's;
  the Xbox pad plugged before the game starts works (`tools/computer_use/README.md`). Never press A at that prompt.
- **The keyboard as player 1 reads the HUD's "1 2 Q W E R T" bar as a panel:** the mod is for a controller; the
  keyboard HUD is reached only by entering with the mouse.
- **A barrier never seen closed, past the network bubble, reads open:** the game sends nothing to know it by.
- **Panel notes are a heuristic** (`UiSemantics.PanelNotes`: a titled panel's texts outside navigable elements) and a
  panel whose loose lines change while open says them only on entry: by design, checked on Skills and Character.
- **Radar-table labels and landmark notes that stay in English** (61 of 211): players' notes and names the game has no
  row for; every piece the game names is said as the game names it (`LandmarkTable.Localize`).
- **A class start said without a direction** ("inicio de tu clase, Monje"): the portrait has no node element.
- **The dev command file read whatever `ResearchMode` says:** it only reads and speaks, and needs write access to the
  mod's own folder.

### Blocked
- **The tree's header "Set de armas: 1, (4)".** Read as shown (two rows, one per set, each the caption, "1" and
  " (4)"; no tooltip). What the first number counts is not proven: it read "4, (4)" on both rows on 2026-11-08 and
  "1, (4)" on both on 2026-11-10 with "Puntos de habilidad: 0" each time, and of yarkey's 16 build nodes taken, three
  carry weapon set 1 in the build file (`attack_speed2_`, `dexterity82_`, `attack_speed1`) and two set 2
  (`dexterity66`, `dexterity65_`). That fits "1 left of 4" (three spent in set I) and also "1 spent of 4" (only
  `attack_speed1` is of a set alone). The bracket is the same on both days: the points owned. **Unblocks:** the owner
  allocates or refunds one weapon-set node and reads the header before and after; then the words are one phrase
  ("1 sin asignar de 4" or "1 asignado de 4"). The game's popup key `WeaponSetPassiveSkillPoints` exists
  (`catalog/game-keys.txt`), its text is not extracted.
- **Which gems are Spirit gems, and the engraving window with the build's lines.** The files give the Meta kind (44
  gems) and the Persistent tag (193), neither says which uncut gem engraves a gem. Neither yarkey nor malgdhler holds
  an uncut gem, nor hairin (dev `inventories`, the three read that evening), so the window could not be
  opened. **Unblocks:** an uncut gem in a bag (the owner keeps one instead of engraving it), then its window opened and
  read without confirming.
- **The cosmetics shop's purchase confirmation.** Only the owner may spend points (150): he presses A on a buy button
  once and says what he hears, or allows one purchase.
- **Item modifiers on kinds of item not at hand:** a piece with a rune or soul core socketed (is the rune's line in a
  list, or would a runed piece be said to lack it until focused?), an enchanted and a corrupted item, one stat on an
  implicit and an explicit, a modifier of three values, a number over 999. **Unblocks:** one such item rested on once
  and dev `itemmodscheck` (yarkey holds a Runa de piedra menor; socketing it is the owner's).
- **Build Planner gear:** two wanted lines answered by one line repeat it word for word; the inventory's summary comes
  before the focused item's name. Both are wording choices in `BuildGear*`: the owner's choice between (a) as now and
  (b) the item's name first and a joined line "…, pedido dos veces". Charms, quivers and foci are not compared and a
  unique is judged only when its name equals the author's: needs a build file that uses them.
- **Build Planner supports:** "Engarzadas", "En otra habilidad" and "Fuera de la build" have unit tests only: needs a
  build support socketed at the build's tier on yarkey (the owner's gems). The Sockets component's second socket
  (+0x38) is unmeasured for the same reason: one support per skill.
- **A sale said as "used"** when the gold lags the inventory by more than 300 ms (`InventoryWatch.SettleMs`), and
  "Vendido: …" kept: need a sale, the owner's.
- **The cull cue for a culling support, passive or item:** the mod reads no live stat that says the player culls
  (`game-ids.txt` has the two threshold stats only): needs the stat found with a Culling Strike support socketed.
- **Fights:** a boss with forms said defeated at its first form; the boss growl while it cannot be targeted (the
  owner's choice, in the audit); `DefeatConfirmMs`; the reader's second pass in a fight (state-machine name cache, buff
  name check, entity re-identification, the collector's pauses: `Frames:` and `costs`); flask uses and costs; Parry;
  Heat said as a count ("Calor: 31", never "al máximo") and whether every count is wanted; the lost hint's 180 s in a
  monster-dense area; the ritual rune's states. **Unblocks:** the owner's next boss or area with the log kept
  (AGENTS rule 25: no scripted fights).
- **The radar's flip near the encampment exit** (Up and Left between wall and exit every 2 s while the character
  swayed between two cells, seen once): the 1.5 s exit hold shortens it and does not end it. Not met again beside
  Clearfell's exit that night (see Fixed: standing still no cue, stepping to and fro the exit once). **Unblocks:** met
  again with the log kept, standing between two cells beside an exit.
- **Second player:** a trade swapped for an item that reads the same, the trade window without the focus, whether
  other players see yarkey's own chat lines ("¡Abajo!").
- **No character there yet:** the ascendancy view of the tree, the trials (Sekhemas, Chaos; a vendor price in another
  currency), the Atlas and the map device, Act 4's destination map.
- **The passive stick after an attribute popup** (the multiple-choice redirect): needs a node allocated, the owner's.
- **Fields on one protection or none** (the tree record's edges 0x38/0x50, PassiveSkills row byte 0x66, the hovered
  pin 0x2D0, the skill-slot offsets 0x40/0x58, the mouse hover chain, the 39 fields only the exe's code checks): each
  needs a live invariant designed with the game open in a fight or with a mouse
  ([audit](docs/research/field-protection-audit.md) §2). Extractor offsets left unvalidated: [patch day](docs/patch-day.md).
- **The Mercenary's crossbow clip in memory:** set aside by the owner (2026-11-08), yarkey now uses a spear.
- **The stash's and the well's sounds** were picked by measurement: the owner's ear on the
  [audition page](https://claude.ai/artifact/HKNYM3jD9bvzBA1ECTK4Ww); another pick is one line of
  `SoundExtractor.Cues`.
- **Steam Deck / Proton, the Korean client, the updater on a real release:** need a Linux player, a Kakao client and a
  published release.
- **GGG's written approval:** emailed `contact@grindinggear.com` on 2026-10-26, no reply; ask before any public release.
- **A Skills row's figure heard changing** (audit N2; fixed in code, see Fixed): in a town a row's level or DPS moves
  only when a gem levels or the gear or its supports change, and toggling the weapon set (RT, tried and put back: "Set
  de armas 2: …", "Set de armas 1: Filo quimérico, raro, Égida de glifo, raro") moves no row. **Unblocks:** the owner
  levels a gem with the cursor on its row and hears "NIVEL: 9" and no more.
- **A control with no tooltip under the free cursor, heard** (audit N1; the rule is under Decisions): none exists on
  the panels yarkey opens. **Unblocks:** a character at the Atlas or the map device, swept with `VerboseLogging` on
  (`Hover without tooltip` lists each one).
- **"La ruta es larga, sigo buscando" heard:** no town can run the search out: the Ardura caravan has 164,423 walkable
  cells (dev `terrain`) and the budget is 600,000 expansions (`RoutePlanner.MaxExpansions`). **Unblocks:** an area
  larger than the budget, met in play with the log kept.
- **A language lost from `gems` or `passive-tree` at the start:** needs the game's data set aside, which is the
  game's files.
- **From the audit, each with its reason there:** a start with `oo2core.dll` renamed (the owner's leave to rename a
  game file);
  a way back from a release that does not work, the "ended" lines after a respawn and a quiet buff (the owner's choice
  between the options named there).

### Decisions the owner can overrule
Taken on 2026-11-10 where the audit had left "the owner's call"; each is one constant or one phrase.
- **The guide when the search ran out of budget:** "La ruta es larga, sigo buscando", once, and it keeps leading along
  the part it has (it searches again every 700 ms from where the player stands); the scanner says "…, la ruta es
  larga: …". A way truly cut keeps "el camino se corta antes". Left aside: silence until the whole way is found.
- **An object no table names, outside English:** "Objeto sin nombre, Chair" (the file's word kept so two can be told
  apart). Left aside: not saying it, or "Objeto sin nombre" alone.
- **An exit holds 1.5 s** after the ray last saw it (`RadarSensor.ExitHoldMs`); a true change is said that much later.
- **The launcher's reminders:** first at 30 s, then every 60 s, for the wait for the game window and for a step that
  does not move; the wait ends after 240 s only with neither Steam nor the game running.
- **`ui-catalog` fails past 2 lost files in 100** (`UiLoss.MaxLostPerHundred`).
- **Charges and Rage are a baseline for 1.5 s** after an area's first reading (`BaselineGrace`).
- **A control under the free cursor that never gets a tooltip** (audit N1) is said by its own caption once the cursor
  has rested on it 500 ms (`BareHover`): in game only, a button, check box, tab, list, slider or text field, a caption
  of its own up to 80 characters, never one only a control id names; one line, as any focus. The sweep found no such
  control (Cosmetics, Character, Social and Challenges move a focus, the shop a highlight; the world map's one
  control and the tree's nodes have tooltips), so it is unheard. Out of the game it stays silent: the only one ever
  logged was the login's Exit under the mouse pointer, which is not where the pad is. Left aside: no rule until the
  Atlas is swept.
- **A Skills row's figure that changed again within 2 s** is moving by itself and is not said again
  (`FocusNews.MovingFigureMs`); the first change is. Left aside: the last value once it holds still.
- **A panel's control turning usable or unavailable is said** with the panel's other news ("Guardar X"); the HUD's
  controls never. Left aside: only the pad buttons of a panel.

### Notes
- yarkey (Mercenary 23, Act 2 town) follows "Campaign Progression - [0.5.5] Twister G" (16 of 60 taken, 13 points
  outside it, the earlier guide's route); his gear was set for the build on 2026-11-10 (unique ruby ring and movement
  boots from the stash, a Ridged Buckler in the second set's off hand). Taking a flask off the belt empties it.
- `catalog/landmarks.json` corrects the Radar table's labels that were no area name by a typo ("Oghams Manor") and
  G1_14's "Entrance".
- The scanner's long routes are a jump point search over a cached cell map (2-10 ms on 1600x1600 grids, measured
  offline). The exit prompt "Mantener presionado (Mostrar opciones)" is drawn outside the UI tree: the A target says
  "mantén A para opciones".
- Passive tree: allocation was checked on two characters (2026-10-24). The tree's first reading takes 440 ms at the
  first opening of a session and 635-754 ms when the mod starts with the tree open (`Slow tick: focus`).
- malgdhler (Druid 8) was started in his town, the Campamento de Sierraclara, on 2026-11-10 for the checks (the game's
  own prompt "Aparecer en el pueblo o la guarida" confirmed): he had been left in El enredo sombrío; hairin (Huntress 4)
  likewise, left in Sierraclara. Both now stand in the Act 1 town. The development build's `config/passive-resume.txt`
  holds `hairin	armour6` from the restart check.
