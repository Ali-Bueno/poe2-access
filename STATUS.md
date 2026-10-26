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
- **Test package:** `tools/package/New-TestPackage.ps1` → `dist/Poe2Access-test-<date>.zip` and `.7z` (~29 MB, fits a chat upload; top folder: `Poe2Access.exe`, LEEME, controls, stop
  shortcut; everything else in `app/`; self-contained, unzip and
  run; sounds extracted from the tester's install on the first start); `tools/README.md`.
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
| Character creation (class, quote, name field) | done (live) | how to create said once on the name field (Enter, or B then A); X variant "Bruja" ↔ "Hechicera" with its quote; league list read (12) |
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
| Skill bind screen (X on a gem) and skill picker (a slot's grid of skills/actions) | done (live) | "Selecciona el botón al que asignar Descarga de caos. Set de habilidades principal. LB, vacío"; entering the other row names it; picker cells (A on an empty button, X on a bound one) "vaciar el botón, columna 1, fila 1", "Usar frasco de vida, columna 2, fila 2…"; picking a skill up reads "vacío. Colocar, A. Cancelar, B" |
| Cosmetics | done (live) | ordinary buttons once the d-pad moves the focus |
| Quest list | done (live) | world map X: "Llegar a Sierraclara, botón, <description>", then "Más información, A. Cerrar, B…"; the map's own legends after its place (2026-10-24) |
| Quest list rewards (R3 "Inspeccionar recompensas") | done (live) | the focused reward icon has no text; `UiSemantics.Describe` names it by its art (`QuestRewards`: "Misiones. Recompensa: mesa de fabricación, 2 de 6" from RewardCraftingBench.dds, 2026-10-26); the NPC's reward window ("Selecciona una recompensa…") was already read |
| Objective / tutorial box (G+0xDD8): new texts read, queued | done (live) | "Equip Item. Press View to Open your Inventory", "Press X to equip weapon." |
| HUD (G+0x558): new texts once per area, never the whole HUD | done (code) | quest tracker, shrines; a ground item's tooltip says only name, rarity and quality (`EntityCatalog.ItemOfTooltip`), gold nothing (user 2026-10-24) |
| NPC dialogue: speaker + line, every advanced line, Continue, quest accept | done (live) | Renly, Una (Clearfell, 2026-10-23); a line changing in place is spoken (the screen filter drops texts the focus holds); choices not seen yet |
| Chat: NPC shouts, own remarks, system, whispers, party (global/trade behind a setting, off) | done (live) | `ChatWatcher` + `ChatDiff` (text sequence; the game rebuilds line elements, an address-based check re-read old lines >1000 times); AFK notices dropped; `AnnounceChat` |
| Tooltips read automatically when the focus lands (items, equipment, gems) | done (live) | "Main hand, Withered Wand, Wand. Grants Skill: Level 1 Chaos Bolt. … Unequip, X"; `ReadTooltipsOnFocus` |
| Mod button L3 (any controller, from the game's own pad state M+0x768) | done (live) | tap/double tap = next/previous line, hold = whole tooltip; gameplay: repeat, hints, screen; `docs/controls.md` |
| Scanner (hold L3 + right stick flicks; tap = where the choice is now) | done (live) | `EntityCatalog` + `RoutePlanner` + `ScanBrowser`: 8 categories, routed steps, leg + next turn ("Una, 14 steps, right 3, then up right"); walked Riverbank → Miller → Clearfell → town NPCs on it |
| Low-life warnings | done (live) | `VitalAlarm`: "Life 50 percent" / "Life 25 percent" once per fall; the Miller was beaten reacting to them |
| Skill crafting (engrave an uncut gem): grid in reading order, details after the name, Engrave confirm with its buttons | done (live; details fix in code) | "Skeletal Sniper, Level 1, 1 of 24"; "Engrave. Contagion. Confirm, A. Back, B"; the details were taken for scrolled rows since 2026-10-24 (only the name read, 2026-10-26): `FocusDetails` |
| Skill gems in the inventory named from their tooltip (no base display name) | done (live) | "Unearth, column 1, row 1" |
| Read the whole panel (`screen` command) | done (live) | character detailed stats read fully; summary block order is jumbled |
| Options section names on entering a section | done (live) | "UI. Controller. HUD Mode, Central, combo box, 1 of 8" |
| Vendor window (buy/sell) | done (live) | "tab 3. Suede Bracers, …, 25 gold, not enough. Gloves…"; magic items by full name; gold counter "0 gold"; selling not exercised (own items) |
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
| Guide beacon (L3 + R3 on a scanner choice) | done (live) | `Beacon`/`BeaconMath`: route off the frame thread, ping 620 Hz panned along the route, "Llegaste a …" |
| Scanner Quest category (objective's proper nouns) | done (live) | "Misión. El molinero hinchado, único, 5 pasos"; `QuestMatcher` |
| Boss life + dodge warning | done (live) | "El molinero hinchado 72 por ciento"; whoosh + snap only on attack animations (`DodgeThreats`), a rare's blow within 150 units, louder when closer; timed half a second before the blow as learnt from the hits taken (`DodgeTimings`, `config/dodge-timings.txt`; MudGolem GroundSlam 1156 ms), at the wind-up until learnt (`docs/research/dodge-timing.md`) |
| League encounter in the vitals readout (L3 double tap): "Encuentro de ritual. 100 Tributo. 0 rituales restantes" | done (code) | `EncounterReader`: visible texts under the HUD's `tertiary_hud_layout` except the quest pages (UI dump 2026-10-26); changes during play were already said as HUD news |
| Culling Strike cue (off by default) | done (code) | `CullWatch` + pure `CullThreshold` (KeywordPopups "CullingStrike": 35/20/10/5% by rarity); falling two-note chime on the enemy's side, rares and uniques named |
| Primed for Stun cue (Boneshatter: heavy stun and shockwave) | done (code) | `EnemyStateWatch` (own switch `PrimedStunCues`) + pure `PrimedThreshold` (KeywordPopups "PrimedStun": 40/50/60/70% by rarity) on `GameLayout.ActorComponent` (threshold Actor+0x4E4, buildup +0x4E8; stun traces 2026-10-26: a hag at 54 of 60 stunned by the next hit); not heard live yet. Freeze / Electrocution / Pin buildups: not found (the second and third 0x1C entries after the stun one stayed 0 against a Warrior; `stuntrace` logs Actor 0x4C0-0x5C0) |
| Enemy state sounds: heavy stunned, frozen, electrocuted, pinned, armour broken, dazed, parried | done (code) | `EnemyStateWatch` + pure `EnemyStates`/`EnemyStateNews`: buffs `frozen`, `electrocute_display_buff`, `pinned`, `broken_armour_debuff`, `dazed`, `parried` and the animations HeavyStun[Back|Forward] / Electrocute*; a sound per state (`CueSynth.StateCue`) on the enemy's side as it begins, rares and uniques named with the game's own term; on by default (only states the party causes happen); heavy stun seen in traces, the others not yet heard live |
| Raise Shield feedback: clank up as the shield rises, down as it falls | done (live) | `ShieldWatch` + pure `ShieldStance` on the player's animation name (game Animation rows 0x1DF-0x1E2: ActiveBlockChannel[Moving] held, ActiveBlockEnd[Moving] lowering); a blocked hit's flicker is ignored for 300 ms; heard live 2026-10-26 (log: raised ActiveBlockChannel, lowered at ActiveBlockEnd or by attacking) |
| Boss weak point heartbeat (the Devourer: strike the head, not the tail) | done (live) | `WeakPointGuide` + pure `WeakPoints`: a unique monster sharing a "Tail" entity's name (MonsterVarieties MudBurrowerTailBoss / DevourerBossDuoTail have no boss bar, read 2026-10-26); silent while the head is out of play (it turns untargetable as it burrows: the log toggled in and out of play through the fight, won 2026-10-26) |
| Cue timbres and mixer | done (code) | beacon an iron chime, dodge a blade swing with a ringing snap and thud, both in a short stone-room tail; growl with a sub-octave; levels raised toward the game's own sounds; soft limiter instead of clipping (`CueSynth.Limit`) |
| Mana warnings, vitals on L3 double tap | done (live) | "Maná 50 por ciento"; "Vida 85 por ciento, maná 100 por ciento. El molinero hinchado 65 por ciento" |
| Vitals: flask uses, level progress, gold, explored share (D2R) | done (live) | "Frasco de vida menor, 6 usos… Nivel 3, 5 por ciento hacia el nivel 4, nivel del área 15. Explorado 41 por ciento" (2026-10-25); `FlaskWatch` (GameHelper FlaskInventory + Charges 60/10), `ExperienceTable` (`catalog/experience.json` from ExperienceLevels), `ExplorationMap.ExploredPercent`; empty-flask warning heard: "Frasco de maná menor vacío" after six drinks in Clearfell, then the game's own "Sin frascos" (2026-10-25; flasks are not drunk in town or at full life/mana); the uses left said after each drink, "Frasco de vida menor, 3 usos" (code, user asked 2026-10-26): flasks tracked by belt slot, since a drink makes a new item entity (`FlaskDrinks`, seen in the log); a use gained and a flask put on the belt said too |
| Loot filter for item sounds and approach names (D2R) | done (code) | `LootFilter`, setting "Objetos que suenan y se nombran": todos / mágicos o mejores / solo monedas, gemas, objetos de misión, raros y únicos; the scanner still lists every item; not heard live (no ground items in town) |
| Target name on right-stick aim, crosshair beep | done (code) | GameHelper `MouseOverEntity`; never heard live yet (no monster under the aim during the bot test) |
| Harmful states (Urge speech) | done (code) | `StatusWatch` (buff ids by stem; every new id logged "Player buff seen"); the mod's heartbeat was removed (the game has its own, user 2026-10-24) |
| Game sounds for loot/chests/exits/waypoints | done (live) | `ObjectSounds` + `GameSounds`; each item its own drop sound from the game's tables (`ItemDropSounds`, `catalog/item-sounds.json`: scroll → `Scroll_Drop_01`); up to four items at once, pitch up/down by screen side; portal = the portal scroll's (`docs/research/game-audio.md`) |
| Mod phrases in the game's 10 languages, localized area names | done (live: es, fr) | `Localization` (game ini or setting) + `Phrase` handler: other languages look the English composite format up in `catalog/lang/<code>.json` (406 phrases, game terms checked against ClientStrings); `tools/localization/phrases.py check`; `catalog/area-names.json` from WorldAreas |
| Area level (its monsters' level) and player levels | done | `Announcements.Area`/`Levels`/`OtherPlayer`: "Área: Sierraclara, nivel 2." on entry, "Nivel 4, nivel del área 2" on the L3 double tap, other players "…, nivel 20" in the scanner; GameHelper AreaInstance +0xBC (log "Area level N") and Player +0x204 (level-ups heard live); checked live 2026-10-25: Clearfell 2, the Act 1 town 15, both as in WorldAreas (the town row really says 15; kept, the user wants towns to say theirs) |
| Named places walked into and props in reach (D2R) | done (live, town + Clearfell) | `PlaceNarrator`/`PlaceMap`/`PlaceTracker`, `catalog/places.json` from `Poe2UiExtract places` (2148 named tiles, tile and room props): "carro", "yunque", "campamento"; scanner category Lugares; tile props turned by RotationSelector (only 3 measured); `docs/research/places.md` |
| Pre-game hints, delete/creation screens, edit done | done (live) | hints on entry, only new ones after; "¿Estás seguro…? yarkey… cancelar, botón, 1 de 2"; "Zq, edición terminada" |
| NPC dialogue options read one by one | done (live) | `UiSemantics.OptionLists`: confirm glyph marks the option; D-pad left reaches the lore column; a heard topic (drawn grey) reads "Presentación, atenuado, 1 de 8" (Una, 2026-10-24) |
| Spoken settings menu + sound glossary (hold L3 1.5 s) | done (live) | `SettingsMenu`, `SoundGlossary`; 26 settings + 23 sounds |
| Target from the HUD target bar (controller) | done (live) | HUD > entity_info_container > entity_info_display > name_label; MouseOverEntity is 0 with a pad |
| Dodge timing before the first blow (game files) | done (unit, values checked against blows timed live) | `catalog/hit-times.txt` from `Poe2UiExtract hit-times` (6929 attacks loaded; 6084 of the 7398 warned of timed; projectiles add their flight at the files' speed, unit not timed live); repeat swings warned each period; `docs/research/dodge-timing.md` |
| Death place "Donde moriste" among the exits (D2R) | done (code) | `DeathWatch.Died` → `EntityCatalog.RememberDeath` (area hash + position); not seen live (no death since) |
| Fresh install (no game config): login read at once, language list, input-method notice | done (live) | off-screen controls do not make a layer the top (the "Deal of the Day" panel hid the login); `visible_area` notices said after the focus; Steam logs in without a form; language re-detected on attach |
| Long scrolled texts (terms of use) by paragraph | done (live) | text over 1024 chars in a frame: `ScreenWatcher.LongText` → `LongTextReader`; scroll = frame `PositionModifier.Y` (+0x10C, negative) against text − frame `UnscaledSize.Y` |
| Map overlay shown / hidden (D-pad down) | done (live) | `map_overlay` (G+0xB88) hint `gamepad_move_map_hint` visible only while open; `MapOverlayWatch` |
| Chat panel shown / hidden (LT in a panel) | done (live) | chat layer visible with its "hold LT to chat" hint; `ChatWatcher.TrackPanel` |
| Chat channel list and filters | done (live) | unnamed combo found by its parts, fields per class (`UiElementReader.ComboBoxShift`); "Eligiendo: Global, 2 de 6", "Canal: Susurros", field "Global, #, campo de texto" (2026-10-25) |
| UI sweep 2026-10-25, every finding fixed | done (live) | disabled option rows ("no disponible"), challenges expanded/collapsed, quest map depth and rewards, panel notes (Skills spirit/capacity, Character class), stat-list sections, socket "1 de 2", small passive nodes, map with area/league/realm, game menu tip, friends/guild lines, clear-search button, capitalised small-caps captions; `docs/research/ui-sweep-2026-10-25.md` |
| Player menu (A on another player) | done (live, header unheard) | popup outside the root, focus stays on it; chosen button int `+0x348` (`GameLayout.ButtonMenu`); `UiSemantics.DescribeButtonMenu`; dev command `dump 0x<address>` dumps such popups |
| Weapon swap "Set de armas 2, vacío" (D2R) | done (live) | GameHelper `Stats.CurrentWeaponIndex` (+0x168) still right in 0.5.5e; `InventoryWatch.TickWeaponSet` |
| Inline item art named ("Identificar Pergamino de sabiduría x 1") | done (unit) | `catalog/item-names.json` (`Poe2UiExtract item-names`, BaseItemTypes Id +0x0 / Name +0x20); `ControllerGlyphs.Plain` |
| What A uses ("Finn, A", "Alijo, A") | done (live) | Targetable `+0x6B` = the game's interaction target; `ProximityNarrator.TrackTarget` (2026-10-24); cuts in since 2026-10-26 (queued it came late and A took the next item), the item's HUD tooltip no longer repeats it, lone tooltip property lines are not HUD news; items walked back to are named again (no minute cooldown) and a near item's sound repeats every 600 ms, not 1.3 s; a magic/rare item's HUD tooltip is no longer read whole (its full name is not the scanner's base name); no screen news while another window is in front (code) |
| Discoveries, pick-ups, unmet requirements | done (code) | `DiscoveryNarrator` ("Jefe a la vista" heard live), `InventoryWatch` (ServerData MainInventory1 via reflection), red tooltip lines with digits |
| Checkpoint travel map markers | done (live); place names (code) | `UiSemantics.MapMarkers` + `CheckpointPlaces`/`MapPlaces`: markers matched to seen checkpoints by one scale, "Viajar al punto de control, cerca de …, abajo, a 35 pasos, 2 de 2" (the game names none) |
| Skill bar readout (after the screen on the gameplay hold) | done (live) | `UiSemantics.SkillBar`: "Set de habilidades principal: LT, Activar set… X, Saeta básica… Vacío: LB, cruceta arriba, Y"; bind screen names skills from their dat rows (`docs/research/skill-bar.md`) |
| Route turn prompts, partial routes, controls help in the settings menu | done (live) | "Ahora arriba a la derecha, 10"; help topics open the L3 menu; the menu edge knock was removed (user 2026-10-24) |
| Game updates: layout found again, data regenerated, losses said | done (every part judged live 2026-10-25: login, character creation, town with a quest marker, options, inventory, passive tree, skill bar, left trigger) | `LayoutProbe` (+ image textures), `InGameFields`, `ControlFields` (+ tabs, skill slots, passive canvas and nodes, class selector, panel title, hint, check box and list seen changing), `EntityFields` (in-play flag, quest marker), `FlagBits`, `PadButtonCheck`, `PassiveTreeFields`, `TileGridSearch`, `BaseNameField`, `GameStateService.AreaRowOffset`, `CompatibilityReport`; `tools/package/Update-GameHelper.ps1`; launcher `GameDataSetup` + `DatContentColumns` (passive flags, skill keys by content); dev command `layout`; `docs/compatibility.md` |
| Scanner limited to what a sighted player sees | done (live) | `EntityCatalog.Sighted`: enemies on screen, minimap-icon things once the client has them, the rest once seen or explored; Quest category at any distance ("Misión. Campamento de Sierraclara, 130 pasos" from unexplored Riverbank); `docs/research/minimap.md` |
| Enemies coming into view | done (code) | `EnemyNarrator`: once per monster per area, packs grouped: "Ahogado, 3, derecha"; uniques stay discoveries |
| Inventory actions cut in | done (code) | "En la mano" held 400 ms (was 1.5 s, which queued the moves after it); equip/unequip/hand empty/arrivals interrupt |
| Social, Challenges, Cosmetics, Quests, Character, Skills panels | done (live) | filter box as an edit field; Challenges rows by their `selected_*` marker frame ("El relicario, 0/50, 5 de 8", expand reads the goal); free-cursor pages read after the tab; icon buttons named by their tooltip ("Efecto para arma") |
| Passive tree: node state and links | done (live) | `UiSemantics.PassiveTree`: "Armadura y evasión, alcanzable, +10 a la armadura… Conexiones: arriba, Armadura y evasión, asignado; abajo a la izquierda, …"; points and attributes on opening (`docs/research/passive-tree.md`) |
| Speech priority per kind, wall tones, small-obstacle knock | done (code) | `SpeechPriority` (9 "Voz: …" settings), `WallTones` (off by default), `PassageScan.IsSmallObstacle`; unit-tested, not heard live |
| Item in hand (inventory cursor) | done (live) | `InventoryWatch.TickHand` on GameHelper's Cursor1 inventory: "En la mano: Pergamino de sabiduría, 3. Elige una celda…", "Ya no llevas ningún objeto"; pick-ups now named from the item entity (host `Inventory.Items` keys are slot entries, not entities) |
| Monster modifiers on the aimed monster | done (code) | `MonsterMods`: names from the game's Mods.dat rows in the client language (first gender branch); the Miller's mods have no name, so nothing is added, as on its plate; not heard on a magic/rare monster yet |
| Gold pick-ups and total | done (live) | `GoldWatch`: rise of the inventory's gold counter during play, "Recogido 17 de oro" heard with the panel hidden after the Miller (2026-10-24); the total with the L3 double tap ("113 oro", 2026-10-26; shared in local co-op), named in the in-mod L3 help |
| Reach ping (D2R) | done (code) | `EnemyCues` + `ReachGate` (unit-tested): body gap from `Render.ModelBounds`; two rising notes, re-armed past reach + 25 |
| Unexplored-areas scanner category | done (live) | `ExplorationMap` (unit-tested): "Sin explorar. Zona sin explorar, 7 pasos, arriba 4, luego arriba a la izquierda"; forgotten on area change and host restart |
| Highlighted control without focus, league selector | done (live) | `FocusTracker`: a visible child named "hightlight" (the game's spelling) marks its parent pre-game: "Continuar, botón, 3 de 3" after the name field; combo values skip `*_popup` children ("Liga, Forbidden Rites…"), the popup is read after the focus; HUD `paused_frame` ignored |
| Equip / unequip / arrival notices (D2R) | done (live) | `InventoryWatch.TickMoves`: the game makes a new item entity on every inventory change, so moves are departures and arrivals paired once still for 300 ms: "Equipado: Ballesta improvisada, Mano principal", "Desequipado: …"; "Nuevo en el inventario" / "… salió del inventario" with a panel open (vendor/stash untested); the hand's "Ya no llevas…" waits 800 ms and is dropped for an equip |
| Class choice position | done (live) | `UiSemantics.SelectorPosition`: ClassVariationSelector +0x394 index over the first icon row: "Monje, 6 de 6. Mi vida…"; class id +0x390; the second row is the variants (X on the name step) |
| NPC quest bell (D2R NPC "!") | done (live) | `EntityCatalog.HasQuestMarker`: "Personajes. Renly, con misión, 16 pasos…", "Una, con misión"; bell (`CueSynth.Ring`) among the object sounds and in the glossary |
| Item comparison | done (live, the game's own) | R3 "Comparar" opens Modo inspección: the item beside the equipped ones, links walked with the D-pad; an item's text is read once, then only the links ("Set de armas, enlace") |
| Barriers: routes, scanner, opening | done (live) | `BarrierMemory` (unit-tested): closed believed at any distance, walkable only within 120 cells; routes cut by a closed barrier say "una barrera cerrada corta el camino"; scanner "Elementos. Barrera cerrada"; `BarrierWatch` "Se abrió el paso, …"; the guide says "lo más cerca posible" at the end of a partial route |
| Tutorial lessons that freeze the character | done (live) | lessons that hold the character until a press: dodge (B), low mana (D-pad right flask), passive point (View), engrave the uncut gem before leaving the camp; the lesson's button line is read ("View para abrir los menús"), a lesson shown again during play is said again after 5 s, and in play it is kept whole against interrupting lines. In code (2026-10-26, user missed the passive point behind inventory moves): in menus a lesson cuts in, is kept whole (`SpeechFloor`: what interrupts meanwhile waits, only the latest follows) and the focus it cut is said after it; a pending lesson back with a panel or menu is reminded after 60 s (`LessonReminder`) |
| Options page: glyph labels, scroll, slider rows, combos | done (live) | "Encaje de objetos stick izquierdo / cruceta", wrapped labels joined, deadzone "3 de 10", rows scrolling into view not read, value-beside-slider rows keep the list position, combos opened in place: "Eligiendo: …", "Adaptativo (experimental), 2 de 3", "Elegido: …" |
| Login: gateway list | done (live) | "Servidor, Sao Paulo (BR), lista desplegable, 5 de 7"; open list reads the highlighted entry "Japan, 16 de 19" |
| NPC dialogue and vendor (town) | done (live) | speaker first ("Renly. Comprar o vender objetos, 7 de 8"); vendor stock "Coraza oxidada, columna 10, fila 10, 50 oro, no alcanza… Comprar objeto, X"; reward and vendor openings without the gold chrome |

## Derived facts (with source)
- Heavy stun buildup is "poise" in the game's data (Stats.dat: `poise_threshold`, `base_poise_threshold`, `poise_decay_delay_ms`, `is_heavy_stunned`, `cannot_accumulate_heavy_stun_poise`). GameHelper's `GameStats` enum is about 3 rows behind the 2026-10-26 Stats.dat (`poise_threshold` 17371+1 there, row 17374 now): compare raw keys, not enum names. Stat keys are row + 1.
- Enemy state buff ids and display names: BuffDefinitions and KeywordPopups (`Poe2UiExtract dat-strings <game> KeywordPopups <Language> 1100`; the language name as the game spells it, e.g. "Spanish", "Traditional Chinese"; run from PowerShell with `[Console]::OutputEncoding = UTF8` or the text is mangled).
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

## Code map
- `Poe2AccessibilityCore` — wiring + per-frame tick (GameHelper `DrawUI`), no logic; partial files `.Gameplay` (service
  ticks isolated per service, vitals and skill bar readouts), `.DevCommands` (command file, status, layout self-test),
  `.Settings` (host panel, applying settings, hotkey checks).
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
  `ScreenNarrator` (`OnScreenChange` dispatches by layer: hint bar, `.InGame` focus layer / objective box / HUD,
  `.PreGame` popups and top layer). `ChatWatcher` reads NPC/system/whisper/party chat lines.
- Memory layout constants: `GameLayout` (each with provenance). Glyph tables: `ControllerGlyphs`.
- Research: `UiTreeDumper`/`UiTreeReader`, `ResearchSession`, `FocusResearchLog`, `DevCommandFile`.
- Controller: `GamepadReader` (pad state M+0x768) → `L3Gestures` → `ModCommands` (menu lines / gameplay).
- Local co-op: `Seats` (followed, partner, asking player; `InputSeat` picks whose pad and cursor, `PlayerStride`), `LocalPlayers`,
  `LocalInventories` (player 2's server inventories), `PlayerAlerts` + `SeatOutput` (named per-player news), `CoopDowns`.
- Scanner: `EntityCatalog` (entries, landmarks, `Describe`; `.Classify` categories and names, `.Terrain` live grid and barriers) → `RoutePlanner` (A*, corners,
  legs) and `ScanModel` (`ScanBrowser`, `StickDirection`); `LandmarkTable`. Warnings: `VitalAlarm`.
- Sounds: `AudioCueService` (winmm mixer) ← `EnemyCues` (`EnemyCueScheduler`, `CueSynth`) and `Radar`
  (`PassageScan`, `RadarSensor`, `RadarSynth`; a second `Radar` follows `Seats.Partner`); with `ProximityNarrator` (`ProximityTracker`) gated in
  `Poe2AccessibilityCore.TickSounds`.
- Shop/stash: tooltip `cost` → `CurrencyIcons`, red price via `UiElementReader.IsTextRed`; grid tabs `UiSemantics.GridTab`.
- Gameplay guides: `Beacon` (+ pure `BeaconMath`), `QuestMatcher`, `BossWatch` (boss life + dodge whoosh, pure
  `DodgeThreats`), `TargetNarrator` (aim names + crosshair), `StatusWatch` (+ pure `StatusAilments`), `EnemyNarrator`, `DeathWatch`,
  `ObjectSounds` + `GameSounds` + `ItemDropSounds` (game WAVs), `CheckpointPlaces` (+ pure `MapPlaces`),
  `ScreenProjection` (world offset → screen, on-screen test), `FlaskWatch`, `ExperienceTable`, `LootFilter`.
  Combat state cues: `CullWatch` (+ pure `CullThreshold`), `EnemyStateWatch` (+ pure `EnemyStates`, `EnemyStateNews`),
  `ShieldWatch` (+ pure `ShieldStance`), `WeakPointGuide` (+ pure `WeakPoints`); research: `StunTrace` (dev `stuntrace`).
  Speech priority: `IAccessibilityOutputService.Urge` (later interrupts queue for 1.5 s), `.Keep` (tutorial lessons; `SpeechFloor` holds what comes meanwhile).
- Tests: `tests/Poe2Accessibility.Tests` (differ, text edits, chat diff, gestures, scanner, route legs, life
  alarm, beacon, quest matching, cooldown, ailments, dodge threats, map markers). Dev tools: `tools/computer_use/`; dev commands
  `entities`, `hints`, `gesture <Tap|DoubleTap|Hold|StickClick|FlickUp…>` (command file works without ResearchMode).

## Next step
Next: user test in combat of the 2026-10-26 enemy state sounds and the Primed for Stun wind-up (Warrior with
Destrozahuesos; is it too frequent, are the seven sounds told apart?); then find the Freeze / Electrocution / Pin buildups
with `stuntrace` against a cold or lightning build. Then: user test (DLL and package of 2026-10-26 12:30 in `dist`) of the 2026-10-26 lesson priority (inventory with a pending passive point) and skill details in
the engrave list. Then: user test of local co-op with two real controllers (both toggles, a revive by holding A; the revive and
respawn lines are unit-tested, not heard live). Then: user test of the 2026-10-25 UI sweep fixes (`docs/research/ui-sweep-2026-10-25.md`) and of the player 2 radar
in real couch co-op (two pads; J2 joins at character selection, "Cooperativo local: lectura para el jugador 2" in the mod's settings). Characters: "kehgan" (Monk, level 2, Clearfell Encampment; a town NPC shows a quest marker), "yarkbruja" (Witch, level 3,
Clearfell Encampment, no NPC marker), "yarkey" (Mercenary, level 4, Clearfell Encampment). Start in town from character selection:
`desktop.py combo l2 cross --seconds 1.8` (away from town the confirmation's Cancel is focused: D-pad right, then A). Next: user test of the 2026-10-25 batch (places and the Lugares category, the other
languages, projectile dodge timing, quieter area loads, the new package layout) and of the 2026-10-24 one (wall tones,
"what A uses", weapon swap, map / chat / player menu). Cinematics and their subtitles (`enable_subtitles=true`) are
untested: voiced NPC lines come as chat lines and are read; a cinematic's text has never been on screen in a test. Places: measure the other tile rotation selectors and place the
rooms of generated areas (`docs/research/places.md`, Open). After a game patch: `pwsh tools/package/Update-GameHelper.ps1`, then the dev command
`layout` through the screens listed in `docs/compatibility.md` (self-test); no part is left unchecked. Not yet heard
live: the loot filter's quieter settings (kehgan carries no normal item; a white drop with "solo valiosos" set
must stay silent).

## Known issues / open questions
- Character creation: the league dropdown's list (12 leagues) is not verified open — with the test pad as player 2
  at that screen A does nothing, the mouse does not open it, and Enter submits the form (it answered "Ya existe un
  personaje con este nombre": never press Enter there with a free name). Closed, it reads "Liga, Forbidden Rites,
  lista desplegable, 2 de 3" and then the league's description.
- GameHelper marks a monster Useless once the game flags it dead (stat `is_dead`) and stops reading it, so its life can freeze mid-hit ("Ezomita antiguo" 16/50 in TakeHitBackward, heard and listed as an enemy that could not be hit, 2026-10-26): `EntityCatalog.IsFrozenDead` makes it dead for enemies and boss defeats.
- League devices with life are typed Monster by GameHelper (Ritual rune, `Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable`, alive and hostile by the Devourer, 2026-10-26): enemies are only paths under `Metadata/Monsters/` (`EntityCatalog.CreatureFolder`; all 89 living hostiles in 47 dev dumps are). The rune is nameless, so the scanner does not list it.
- Landmark labels that are not area names are players' notes from GameHelper Radar: in Spanish their common words and
  reward notes are translated ("Escaleras hacia arriba", "Crop Circle (gema de habilidad de nivel 4)"); the names in
  them (Crop Circle, Kabala) stay as written, since the game's own Spanish name is not known for them.
- Radar: once, near the encampment exit, Up and Left flipped wall/exit every ~2 s with depths 100 ↔ 500 units while
  the character oscillated between two cells; not reproduced standing still.
- The bot died twice to Rotten Wolves while testing (Clearfell, 2026-10-23): don't fight with scripted input.
- Steam's desktop controller layout turns the DualSense into mouse/keyboard while the game is in the back; a
  drifting stick or the touchpad then disturbs NVDA in other windows (seen 2026-10-23, not the mod).
- L3 is also seen by the game: on the world map it toggles the legend while the mod reads a line.
- Scanner: long routes take ~200 ms on GameHelper's frame thread; a route along an octant boundary alternates
  "up 2, then up left" / "up left 2, then up". The exit prompt "Mantener presionado (Mostrar opciones)" is drawn
  outside the UI tree (not in a full dump while on screen): the A target says "mantén A para opciones" instead.
  A barrier never seen closed and past the network bubble still reads open (nothing to know it by).
- Skill tooltips read "Costo." with no value: the game draws the header with nothing under it (Chaos Bolt,
  Punch; seen on screen 2026-10-24), so the readout is faithful.
- Character selection with the virtual pad: "J2 Presiona A para unirte" and A selects nothing (Client.txt: "popup
  without a valid player index"); with the Xbox test pad Enter on the keyboard starts the game and the pad drives
  it (`tools/computer_use/README.md`). Never press A at that prompt.
- With the keyboard as player 1 (entered with the mouse) the HUD's "1 2 Q W E R T" skill bar is read as a panel.
- A text appearing right after a focus move repeats the focus with it ("Accesorio para casco, botón, 10 de 19.
  No tienes cosméticos…"): by design, so the name is never cut off; the first readout is interrupted.
- Passive tree: allocation checked on two characters (Duelist pending/applied, Witch applied, 2026-10-24); a class
  start is said without a direction (the portrait has no node element): "inicio de tu clase, Monje".
- Closing the game menu is silent now (D2R); Options opened from it starts with its page.
- GameHelper's own automation plugins (AutoHotKeyTrigger, PickupHelper) were enabled; disabled in
  `configs/plugins.json` on 2026-10-23 (they press keys / click for the player).
- Running from a path containing "poe" triggers GameHelper's launcher warning (its anti-detection advice).
- The UI dump refuses to run while GameHelper's "Show memory diagnostics" toggle is on (the host would
  capture a stack trace per failed read; the root scan expects thousands).
- A `settings.txt` that fails to parse is ignored (spoken warning) and never overwritten with defaults.
- Pre-game UI root unknown to every public tool; our scan heuristic (self-pointer + no parent + children) is unvalidated.
- Native `prism.dll` resolution inside the plugin `AssemblyLoadContext` untested (resolver installed as safety net).
- Panel notes (`UiSemantics.PanelNotes`) are a heuristic: a titled panel's texts outside navigable elements and outside
  parts holding more than three; checked on Skills ("0 de espíritu", gem capacity) and Character ("Monje.
  Resistencias"), the inventory and stash give none. A panel whose loose lines change while open says them only on
  entry.
- GGG ToS: read-only tool, still no written approval — ask before any public release (brief §25). Emailed
  `contact@grindinggear.com` on 2026-10-26 (permission, an opt-in accessibility log file instead of memory reading,
  a team contact); awaiting a reply.
