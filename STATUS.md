# STATUS — Path of Exile 2 (blind accessibility plugin)

> Per-mod ledger. Open this first. Details live in `docs/research/*.md`; the brief in
> `POE2_ACCESSIBILITY_RESEARCH_BRIEF.md`; agent rules in `AGENTS.md`.

## Identity
- **Game:** Path of Exile 2, Steam (`PathOfExileSteam.exe`, app 2694490), proprietary GGG engine, DX12,
  no kernel anti-cheat (server-side detection). Install: `D:\games\steam\steamapps\common\Path of Exile 2`
  (download started 2026-09-14, ~138 GB). Current patch 0.5.5b.
- **Approach:** external, **read-only** memory reading, no input automation (see brief §3/§25 and `AGENTS.md`).
- **Host:** **GameHelper2** v2.8.0 (`external/GameHelper2`, `net10.0-windows`, GPLv3); game patch 0.5.5e.
  ExileCore2 rejected (binary-only, obfuscated, Discord-gated — `docs/research/exilecore2-study.md`);
  CoreExile2 no longer exists (now `coussiraty/ExileBridge`, an HTTP/MCP API without UI tree).
- **Speech transport:** PRISM through the **Prismatoid 0.4.0** NuGet binding (net10.0; bundles
  `runtimes/win-x64/native/prism.dll`). Plugin ships `Poe2Accessibility.dll + Prismatoid.dll + prism.dll`.
- **Plugin:** `Poe2Accessibility/` (sealed `Poe2AccessibilityCore : PCore<AccessibilitySettings>`).
- **Build:** `dotnet build external/GameHelper2/GameOverlay.sln -c Release` once (host), then
  `dotnet build Poe2Accessibility -c Release` → auto-deploys to
  `external/GameHelper2/GameHelper/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility/`.
- **Run:** double-click `Poe2Access.exe` in the host folder (`tools/Poe2AccessLauncher`, built with the
  host): starts PoE2 through Steam if needed and a hidden, renamed GameHelper copy without UAC
  (`__COMPAT_LAYER=RunAsInvoker`), no console, no update check, no prompt; `restart`/`stop` for testing; errors
  in `Poe2Access.log` + a message box. Dev: `py -B tools/computer_use/launch.py all|host|stop` wraps it.
- **Repository:** private GitHub `Ali-Bueno/poe2-access` (created 2026-10-23).
- **Unit tests (pure logic, no game):** `dotnet run --project tests/Poe2Accessibility.Tests` (screen differ, text edits, chat, gestures, scanner, alarms, enemy cues, radar).
- **Autonomous testing (controller only):** virtual DualShock 4 + speech log oracle — `tools/computer_use/README.md`. Plugin logs: `Plugins/Poe2Accessibility/logs/accessibility.log`;
  UI dumps: `Plugins/Poe2Accessibility/debug/ui-tree-<State>-<timestamp>.txt`;
  settings: `Plugins/Poe2Accessibility/config/settings.txt` (hotkeys by key name).
- **Hotkeys (defaults):** F6 dump UI tree, F7 repeat last announcement, F8 speak status
  (F9/F10/F12 are GameHelper's own keys and the overlay's key-timeout table is process-wide — a collision
  is logged at enable). `settings.txt` is re-read automatically when edited while GameHelper runs.

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
| Screen layers: popups, no-focus panels, closing popups, disabled controls | done (live) | `ScreenWatcher`/`ScreenDiffer`/`ScreenNarrator`; rules in `docs/menu-speech-rules.md` |
| Terms / account windows read | done (code) | layer model; verified on creation screen, terms accepted before it existed |
| Character creation (class, quote, name field) | done (live) | class preview video popup not in UI tree (open) |
| **Boot to gameplay** | done (live) | account `yarkidius`, Witch `Yarkidius`, Act 1 beach (2026-10-23) |
| Hint bar button names (glyph images) | wip | Opus research on `entry_icon` |
| Human labels for id-named controls (gateway, exit, options, motd) | todo | tooltip / localisation |
| Text-field echo, password masking, list position "x of n", tab titles | todo | `docs/menu-speech-rules.md` |
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
| Character panel stats as "label, value" rows | done (code) | merged rows in snapshots; stats navigation untested |
| Area announcement on entering / changing area | done (live) | own read of AreaLoadingState+0xEA0 (GameHelper offset stale) |
| In-game context menus (skill: Manage Sockets…) with "x of n" | done (live) | same-class siblings |
| Hover tooltips (element+0x120): title names id-only controls, short ones spoken, `details` reads all | done (live) | "Support Gem Slot, Socket a Support Gem of any colour …"; skill gem details |
| Panel tabs: tab name + where the focus is (also when it stays, e.g. inventory beside Skills) | done (live) | hidden focus = no focus |
| Passive tree: hovered node (M+0x760) read from its tooltip | done (live) | "Critical Damage, 15% increased Critical Damage Bonus. Unallocated"; no node-to-node navigation yet |
| World map (Quests tab): area under the cursor, map legend | done (live) | no-focus panel narration; finding areas with a free cursor is still hard |
| Character stats (inspect mode): widgets with values, stat-list links with row + definition | done (live) | "DEX, 7, …", "Evasion Rating, 7. Evasion, link. …" |
| Skills panel rows, empty slots, skill bind bar (button + bound skill) | done (live) | "empty, 2 of 10", "B, Dodge Roll/Sprint"; unhovered skills fall back to the icon name |
| Skill bind screen (X on a gem) and skill picker (a slot's grid of skills/actions) | done (live) | "Selecciona el botón al que asignar Descarga de caos. Set de habilidades principal. LB, vacío"; entering the other row names it; picker cells "Exhumar, Costo. 7 de maná…", "Usar frasco de vida…" (no grid position) |
| Cosmetics | done (live) | ordinary buttons once the d-pad moves the focus |
| Quest list | todo | empty at the start of Act 1 |
| Objective / tutorial box (G+0xDD8): new texts read, queued | done (live) | "Equip Item. Press View to Open your Inventory", "Press X to equip weapon." |
| HUD (G+0x558): new texts once per area, never the whole HUD | done (code) | quest tracker, ground item; first live run read flask charges and repeats, fixed after |
| NPC dialogue: speaker + line, every advanced line, Continue, quest accept | done (live) | Renly, Una (Clearfell, 2026-10-23); a line changing in place is spoken (the screen filter drops texts the focus holds); choices not seen yet |
| Chat: NPC shouts, own remarks, system, whispers, party (not global/trade) | done (live) | `ChatWatcher` + `ChatDiff` (text sequence; the game rebuilds line elements, an address-based check re-read old lines >1000 times); AFK notices dropped; `AnnounceChat` |
| Tooltips read automatically when the focus lands (items, equipment, gems) | done (live) | "Main hand, Withered Wand, Wand. Grants Skill: Level 1 Chaos Bolt. … Unequip, X"; `ReadTooltipsOnFocus` |
| Mod button L3 (any controller, from the game's own pad state M+0x768) | done (live) | tap/double tap = next/previous line, hold = whole tooltip; gameplay: repeat, hints, screen; `docs/controls.md` |
| Scanner (hold L3 + right stick flicks; tap = where the choice is now) | done (live) | `EntityCatalog` + `RoutePlanner` + `ScanBrowser`: 8 categories, routed steps, leg + next turn ("Una, 14 steps, right 3, then up right"); walked Riverbank → Miller → Clearfell → town NPCs on it |
| Low-life warnings | done (live) | `VitalAlarm`: "Life 50 percent" / "Life 25 percent" once per fall; the Miller was beaten reacting to them |
| Skill crafting (engrave an uncut gem): grid in reading order, details after the name, Engrave confirm with its buttons | done (live) | "Skeletal Sniper, Level 1, 1 of 24"; "Engrave. Contagion. Confirm, A. Back, B" |
| Skill gems in the inventory named from their tooltip (no base display name) | done (live) | "Unearth, column 1, row 1" |
| Read the whole panel (`screen` command) | done (live) | character detailed stats read fully; summary block order is jumbled |
| Options section names on entering a section | done (live) | "UI. Controller. HUD Mode, Central, combo box, 1 of 8" |
| Vendor window (buy/sell) | done (live) | "tab 3. Suede Bracers, …, 25 gold, not enough. Gloves…"; magic items by full name; gold counter "0 gold"; selling not exercised (own items) |
| Stash | done (live) | "Stash. tab 4. empty, column 12, row 10"; tab switches on LB/RB; moving items untested |
| Waypoint map (Teleport) | done (live) | opens "Teletransporte. acto 1", "Viajar al pueblo, Y", then the act's cartouche and the place under the cursor; the stick moves between areas ("Lodazal. … El devorador", "Viajar, A"); Y travelled to town (2026-10-24) |
| Audio output (winmm mixer, synthesized cues) | done (code) | `AudioCueService`; no new DLL; "Audio cues ready" logged live |
| Enemy sounds (growl + rarity layer, pan, tempo by nearness) | done (live) | "16 pulses, up to 6 enemies in the last 5 s"; sight from the projectile layer; ~9 pulses/s in a pack (tune by ear); `enemies` dev command |
| Navigation radar (D2R: tock wall, breath floor, tin exit, 4 screen rays) | done (live) | `docs/radar.md`; user: "creo que funciona"; wall tones = later alternative |
| Nearby things named on approach (checkpoint, NPC, stash, chest, item, exit) | done (live) | `docs/proximity.md`; "Checkpoint" on respawn, "Clearfell Encampment" at the exit |
| Radial menu (hold View) | done (live) | the highlighted option is the only text of its layer: "Inventory", "Skills", "Passives", "Bind Screen", "Character", "Quests", "Cosmetics", "Not Available" |
| Death screen | done (live) | "You have Died. resurrect at checkpoint", then the button |
| NPC / player tap, wall bump, level-up "Level 3" | done (code) | user to test; passive-point tutorial line was already read |
| Guide beacon (L3 + R3 on a scanner choice) | done (live) | `Beacon`/`BeaconMath`: route off the frame thread, ping 620 Hz panned along the route, "Llegaste a …" |
| Scanner Quest category (objective's proper nouns) | done (live) | "Misión. El molinero hinchado, único, 5 pasos"; `QuestMatcher` |
| Boss life + dodge warning | done (live) | "El molinero hinchado 72 por ciento"; whoosh on GroundSlam/HeavyStrike/… (`BossWatch`, Actor +0x8B0 animation) |
| Mana warnings, vitals on L3 double tap | done (live) | "Maná 50 por ciento"; "Vida 85 por ciento, maná 100 por ciento. El molinero hinchado 65 por ciento" |
| Target name on right-stick aim, crosshair beep | done (code) | GameHelper `MouseOverEntity`; never heard live yet (no monster under the aim during the bot test) |
| Low-life heartbeat, harmful states (Urge speech) | done (code) | `Heartbeat`, `StatusWatch` (buff ids by stem; every new id logged "Player buff seen") |
| Game sounds for loot/chests/exits/waypoints | done (code) | `ObjectSounds` + `GameSounds`; sounds extracted from the game's FMOD banks via its own fmod.dll (`docs/research/game-audio.md`) |
| Spanish/English mod phrases, localized area names | done (live) | `Localization` (game ini), `catalog/area-names.json` from WorldAreas (10 languages) |
| Pre-game hints, delete/creation screens, edit done | done (live) | hints on entry, only new ones after; "¿Estás seguro…? yarkey… cancelar, botón, 1 de 2"; "Zq, edición terminada" |
| NPC dialogue options read one by one | done (live) | `UiSemantics.OptionLists`: confirm glyph marks the option; D-pad left reaches the lore column; a heard topic (drawn grey) reads "Presentación, atenuado, 1 de 8" (Una, 2026-10-24) |
| Spoken settings menu + sound glossary (hold L3 1.5 s) | done (live) | `SettingsMenu`, `SoundGlossary`; 26 settings + 23 sounds |
| Target from the HUD target bar (controller) | done (live) | HUD > entity_info_container > entity_info_display > name_label; MouseOverEntity is 0 with a pad |
| Discoveries, pick-ups, unmet requirements | done (code) | `DiscoveryNarrator` ("Jefe a la vista" heard live), `InventoryWatch` (ServerData MainInventory1 via reflection), red tooltip lines with digits |
| Checkpoint travel map markers | done (live) | `UiSemantics.MapMarkers`: "Viajar al punto de control, abajo, 2 de 2" (direction from the player's own marker) |
| Skill bar readout (after the screen on the gameplay hold) | done (live) | `UiSemantics.SkillBar`: "Set de habilidades principal: LT, Activar set… X, Saeta básica… Vacío: LB, cruceta arriba, Y"; bind screen names skills from their dat rows (`docs/research/skill-bar.md`) |
| Route turn prompts, partial routes, edge knock, controls help in the settings menu | done (live) | "Ahora arriba a la derecha, 10"; knock when the D-pad cannot move in a menu; help topics open the L3 menu |
| Social, Challenges, Cosmetics, Quests, Character, Skills panels | done (live) | filter box as an edit field; Challenges rows by their `selected_*` marker frame ("El relicario, 0/50, 5 de 8", expand reads the goal); free-cursor pages read after the tab; icon buttons named by their tooltip ("Efecto para arma") |
| Passive tree: node state and links | done (live) | `UiSemantics.PassiveTree`: "Armadura y evasión, alcanzable, +10 a la armadura… Conexiones: arriba, Armadura y evasión, asignado; abajo a la izquierda, …"; points and attributes on opening (`docs/research/passive-tree.md`) |
| Speech priority per kind, wall tones, small-obstacle knock | done (code) | `SpeechPriority` (9 "Voz: …" settings), `WallTones` (off by default), `PassageScan.IsSmallObstacle`; unit-tested, not heard live |
| Item in hand (inventory cursor) | done (live) | `InventoryWatch.TickHand` on GameHelper's Cursor1 inventory: "En la mano: Pergamino de sabiduría, 3. Elige una celda…", "Ya no llevas ningún objeto"; pick-ups now named from the item entity (host `Inventory.Items` keys are slot entries, not entities) |
| Monster modifiers on the aimed monster | done (code) | `MonsterMods`: names from the game's Mods.dat rows in the client language (first gender branch); the Miller's mods have no name, so nothing is added, as on its plate; not heard on a magic/rare monster yet |
| Gold pick-ups | done (live) | `GoldWatch`: rise of the inventory's gold counter during play, "Recogido 17 de oro" heard with the panel hidden after the Miller (2026-10-24); was: a rise while hidden not seen yet (no gold dropped) |
| Reach ping (D2R) | done (code) | `EnemyCues` + `ReachGate` (unit-tested): body gap from `Render.ModelBounds`; two rising notes, re-armed past reach + 25 |
| Unexplored-areas scanner category | done (live) | `ExplorationMap` (unit-tested): "Sin explorar. Zona sin explorar, 7 pasos, arriba 4, luego arriba a la izquierda"; forgotten on area change and host restart |
| Highlighted control without focus, league selector | done (live) | `FocusTracker`: a visible child named "hightlight" (the game's spelling) marks its parent pre-game: "Continuar, botón, 3 de 3" after the name field; combo values skip `*_popup` children ("Liga, Forbidden Rites…"), the popup is read after the focus; HUD `paused_frame` ignored |
| Equip / unequip / arrival notices (D2R) | done (live) | `InventoryWatch.TickMoves`: the game makes a new item entity on every inventory change, so moves are departures and arrivals paired once still for 300 ms: "Equipado: Ballesta improvisada, Mano principal", "Desequipado: …"; "Nuevo en el inventario" / "… salió del inventario" with a panel open (vendor/stash untested); the hand's "Ya no llevas…" waits 800 ms and is dropped for an equip |
| Class choice position | done (live) | `UiSemantics.SelectorPosition`: ClassVariationSelector +0x394 index over the first icon row: "Monje, 6 de 6. Mi vida…"; class id +0x390; the second row is the variants (X on the name step) |
| NPC quest bell (D2R NPC "!") | done (live) | `EntityCatalog.HasQuestMarker`: "Personajes. Renly, con misión, 16 pasos…", "Una, con misión"; bell (`CueSynth.Ring`) among the object sounds and in the glossary |
| Item comparison | done (live, the game's own) | R3 "Comparar" opens Modo inspección: the item beside the equipped ones, links walked with the D-pad; an item's text is read once, then only the links ("Set de armas, enlace") |
| Barriers: routes, scanner, opening | done (live) | `BarrierMemory` (unit-tested): closed believed at any distance, walkable only within 120 cells; routes cut by a closed barrier say "una barrera cerrada corta el camino"; scanner "Elementos. Barrera cerrada"; `BarrierWatch` "Se abrió el paso, …"; the guide says "lo más cerca posible" at the end of a partial route |
| Tutorial lessons that freeze the character | done (live) | a hint shown again during play a minute later is said again (the dodge lesson held the Witch stunned until B); in menus hints queue |
| Options page: glyph labels, scroll, slider rows, combos | done (live) | "Encaje de objetos stick izquierdo / cruceta", wrapped labels joined, deadzone "3 de 10", rows scrolling into view not read, value-beside-slider rows keep the list position, combos opened in place: "Eligiendo: …", "Adaptativo (experimental), 2 de 3", "Elegido: …" |
| Login: gateway list | done (live) | "Servidor, Sao Paulo (BR), lista desplegable, 5 de 7"; open list reads the highlighted entry "Japan, 16 de 19" |
| NPC dialogue and vendor (town) | done (live) | speaker first ("Renly. Comprar o vender objetos, 7 de 8"); vendor stock "Coraza oxidada, columna 10, fila 10, 50 oro, no alcanza… Comprar objeto, X"; reward and vendor openings without the gold chrome |

## Derived facts (with source)
- Plugin contract, hotkeys (`Utils.IsKeyPressedAndNotTimeout`), events (`RemoteEvents.AreaChanged` public,
  `StateChanged` internal → poll), logging convention: `docs/research/gamehelper2-plugin-api.md`.
- `UiElementBaseOffset` (GameOffsets): Self 0x08, Children 0x10, Parent 0xB8, RelPos 0x100, StringId 0x128
  (StdWString, readable — StashUtility), Flags 0x168 (visible bit 0x0B), ScaleIndex 0x172, UnscaledSize 0x270.
  In-game root chain: `InGameState+0x2F0 → UiRootStruct+0x340` (controller: `+0x318`). Mouse-over entity
  chain `+0x300 → +0x3F0 → +0xA8`: `docs/research/gamehelper2-ui-tree.md`.
- Rect of any element from a plugin: `PluginUiElementReflection.TryGetAbsoluteRect(addr, out pos, out size)`.
- **Pre-game UI root (validated live 2026-09-14, PoE2 0.5.5b, LoginState):** `LoginState+0x10` → an object holding
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

- **Object sizes by allocator stride (2026-09-15):** slider 0x410, check box 0x700, label 0x700, combo box 0x800; bytes past
  these belong to the neighbouring object (the deep-probe hex must be read with that in mind).

## Code map
- `Poe2AccessibilityCore` — wiring + per-frame tick (GameHelper `DrawUI`); dev commands; no logic.
- Speech: `PrismOutputService` (own thread, logs every line) behind `IAccessibilityOutputService`;
  all spoken strings in `Announcements` (incl. button names, item/slot phrases).
- Focus: `UiManagerLocator` (manager per state, `element+0x90`) → `FocusTracker` (`M+0x748`, value
  polling, layer of the focus) → `UiSemantics` (role/label/value; in game: `InventoryReader` grids/slots,
  keyword links, row text, panel title) → `AccessibleUiNode`.
- In-game archetypes: `UiSemantics.InGame` (inventory, links with row and definition, panel title),
  `.Tooltips` (+0x120), `.Geometry` (value drawn by a sibling), `.Slots` (button-bound slots), `.Lists`
  (selectable lists, `selected_*` marker rows), `.SkillBar` (HUD bar + bind-screen glyphs + dat chain to names),
  `.MapMarkers`, `.OptionLists` (NPC dialogue).
- Screen: `ScreenWatcher` (background snapshots of visible layers: texts, controls, enabled bit, glyph
  pairing, row merge) → `ScreenDiffer` (pure: two-snapshot confirmation, counters, top layer) →
  `ScreenNarrator` (pre-game: popups/top layer; in game: focus layer, tab titles, the objective box and
  new HUD texts). `ChatWatcher` reads NPC/system/whisper/party chat lines.
- Memory layout constants: `GameLayout` (each with provenance). Glyph tables: `ControllerGlyphs`.
- Research: `UiTreeDumper`/`UiTreeReader`, `ResearchSession`, `FocusResearchLog`, `DevCommandFile`.
- Controller: `GamepadReader` (pad state M+0x768) → `L3Gestures` → `ModCommands` (menu lines / gameplay).
- Scanner: `EntityCatalog` (classify, names, landmarks, live grid, `Describe`) → `RoutePlanner` (A*, corners,
  legs) and `ScanModel` (`ScanBrowser`, `StickDirection`); `LandmarkTable`. Warnings: `VitalAlarm`.
- Sounds: `AudioCueService` (winmm mixer) ← `EnemyCues` (`EnemyCueScheduler`, `CueSynth`) and `Radar`
  (`PassageScan`, `RadarSensor`, `RadarSynth`); with `ProximityNarrator` (`ProximityTracker`) gated in
  `Poe2AccessibilityCore.TickSounds`.
- Shop/stash: tooltip `cost` → `CurrencyIcons`, red price via `UiElementReader.IsTextRed`; grid tabs `UiSemantics.GridTab`.
- Gameplay guides: `Beacon` (+ pure `BeaconMath`), `QuestMatcher`, `BossWatch` (boss life + dodge whoosh),
  `TargetNarrator` (aim names + crosshair), `StatusWatch` (+ pure `StatusAilments`), `Heartbeat` (+ `HeartbeatGate`
  in VitalAlarm.cs), `ObjectSounds` + `GameSounds` (game WAVs), `ScreenProjection` (world offset → screen).
  Speech priority: `IAccessibilityOutputService.Urge` (later interrupts queue for 1.5 s).
- Tests: `tests/Poe2Accessibility.Tests` (differ, text edits, chat diff, gestures, scanner, route legs, life
  alarm, beacon, quest matching, cooldown, heartbeat, ailments). Dev tools: `tools/computer_use/`; dev commands
  `entities`, `hints`, `gesture <Tap|DoubleTap|Hold|StickClick|FlickUp…>` (command file works without ResearchMode).

## Next step
The Witch "yarkbruja" (level 2) is in Clearfell with Renly's reward still to take (talk, X on the gem) and the
Devourer quest. Next: Una's quest and the Clearfell map (monster modifiers heard on a magic/rare monster, the
"Insectoide de huesos" heard during the Miller fight is a Witch construct: check it is not announced as a
character), the waypoint map on the Transportador. User test by ear of the 2026-10-23/24 batches (reach ping,
wall tones, dodge whoosh, object sounds, the quest bell) and by use (barrier phrases, combos in Options).

## Known issues / open questions
- Character creation: the league dropdown's list (12 leagues) is not verified open — with the test pad as player 2
  at that screen A does nothing, the mouse does not open it, and Enter submits the form (it answered "Ya existe un
  personaje con este nombre": never press Enter there with a free name). Closed, it reads "Liga, Forbidden Rites,
  lista desplegable, 2 de 3" and then the league's description.
- Landmark labels that are not area names ("Boss - Yama", "Chest") stay English.
- Radar: once, near the encampment exit, Up and Left flipped wall/exit every ~2 s with depths 100 ↔ 500 units while
  the character oscillated between two cells; not reproduced standing still.
- The bot died twice to Rotten Wolves while testing (Clearfell, 2026-10-23): don't fight with scripted input.
- Steam's desktop controller layout turns the DualSense into mouse/keyboard while the game is in the back; a
  drifting stick or the touchpad then disturbs NVDA in other windows (seen 2026-10-23, not the mod).
- L3 is also seen by the game: on the world map it toggles the legend while the mod reads a line.
- Scanner: long routes take ~200 ms on GameHelper's frame thread; a route along an octant boundary alternates
  "up 2, then up left" / "up left 2, then up"; the transition prompt "Press-Hold (Show Options)" is not read.
  A barrier never seen closed and past the network bubble still reads open (nothing to know it by).
- Options Graphics: the two "Límite de FPS" rows (check box beside a number) read "1 de 2", not their row.
- Cosmetics: the weapon-set toggles "I / II" read only "desmarcado, casilla de verificación, 2 de 2" (the numerals
  are drawn, no text in memory).
- After a host restart the first HUD texts ("Nuevo punto de control") and discoveries are announced again.
- After leaving to character selection the virtual test pad became player 2 there ("P2 Presiona A para unirte");
  Enter on the keyboard started the game and the pad was player 1 again in game. Never press A at that prompt.
- Passive tree: allocation checked on two characters (Duelist pending/applied, Witch applied, 2026-10-24); the
  class start is said as "inicio, BRUJA" without a direction (the portrait has no node element).
- Closing the game menu is silent now (D2R); Options opened from it starts with its page.
- Leaving to character selection says "Cargando. Selección de personaje. Cargando área" (a 16 ms pass through
  the selection state is announced).
- GameHelper's own automation plugins (AutoHotKeyTrigger, PickupHelper) were enabled; disabled in
  `configs/plugins.json` on 2026-10-23 (they press keys / click for the player).
- Running from a path containing "poe" triggers GameHelper's launcher warning (its anti-detection advice).
- The UI dump refuses to run while GameHelper's "Show memory diagnostics" toggle is on (the host would
  capture a stack trace per failed read; the root scan expects thousands).
- A `settings.txt` that fails to parse is ignored (spoken warning) and never overwritten with defaults.
- Pre-game UI root unknown to every public tool; our scan heuristic (self-pointer + no parent + children) is unvalidated.
- Native `prism.dll` resolution inside the plugin `AssemblyLoadContext` untested (resolver installed as safety net).
- GGG ToS: read-only tool, still no written approval — ask before any public release (brief §25).
