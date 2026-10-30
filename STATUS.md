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
- **Releases:** public repo (2026-10-26); `tools/package/Publish-Release.ps1` builds the package and makes it the latest GitHub release (tag `vYYYY.MM.DD[-n]`, assets always `Poe2Access.zip` / `.7z`), so `https://github.com/Ali-Bueno/poe2-access/releases/latest/download/Poe2Access.zip` is always the newest.
- **Test package:** `tools/package/New-TestPackage.ps1` → `dist/Poe2Access-test-<date>.zip` and `.7z` (~29 MB, fits a chat upload; top folder: `Poe2Access.exe`, README.txt, Controls.md, Stop the mod.bat
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
| Character creation (class, quote, name field) | done (live) | said once on the name field: B leaves it, then the stick reaches league and Continue; Enter or A on Continue creates; X variant "Bruja" ↔ "Hechicera" with its quote; league list read open, all 12 with their descriptions (user, 2026-10-28) |
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
| Quest tracker page always said | done (live) | "Misión: Altares siniestros. Busca los altares de ritual y purifícalos" whenever the shown page is not the last said, held whole, across areas; after loading too (Freythorn's objective came with the HUD and was never said, user 2026-10-29) and on coming back to the game window (`CheckQuestPage`, pure `QuestTrackerPage`) |
| HUD (G+0x558): new texts once per area, never the whole HUD | done (code) | quest tracker, shrines; a ground item's tooltip says only name, rarity and quality (`EntityCatalog.ItemOfTooltip`), gold nothing (user 2026-10-24) |
| NPC dialogue: speaker + line, every advanced line, Continue, quest accept | done (live) | Renly, Una (Clearfell, 2026-10-23); a line changing in place is spoken (the screen filter drops texts the focus holds); choices not seen yet |
| Chat: NPC shouts, own remarks, system, whispers, party (global/trade behind a setting, off) | done (live; fresh-start fix in code) | `ChatWatcher` + `ChatDiff` (text sequence; the game rebuilds line elements, an address-based check re-read old lines >1000 times); AFK notices dropped; `AnnounceChat`. The list is the chat layer's descendant with the most *text* lines: after a fresh game start the history is empty and a textless 74-icon list (`[13,5,1]` in the 2026-10-26 dump) was taken instead, so nothing was read all evening (2026-10-27); lines of a list first found after an empty chat are news. Channel signs dropped when spoken ("@De Dahnte:hola" → "De Dahnte: hola"). Other players' local chat cannot be told from NPC lines by text. The player's own lines (speaker = a local character's name, after a guild tag) are read in every channel whatever the chat settings, as proof they went out (`ChatDiff.IsFrom`; user 2026-10-28) |
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
| Guide beacon (L3 + R3 on a scanner choice) | done (live) | `Beacon`/`BeaconMath`: route off the frame thread, ping 620 Hz panned along the route, "Llegaste a …"; since 2026-10-26 the chord releases any lock whatever is chosen (a co-op friend found or dead left it on; code) |
| Scanner Encounters category: league devices (`Metadata/…/Leagues/…` with a minimap icon) and every ritual altar by its icon (Freythorn's campaign altars were silent, user 2026-10-29: "Altar de ritual", "…, completado", the King's "Altar de ritual del jefe"); a minimap-marked "monster" outside `Metadata/Monsters/` is a device; marks left out are logged once ("Minimap mark not listed") | done (live: a done altar listed) | "Altar de ritual" (icon RitualRune; game strings "Altar de ritual {0}", "Interactúa para ver los favores": the altar is where the tribute is spent); listed while untargetable too; named on approach and with A; `ritual.wav` (RitualEncounter/RituaStart_01) among the object sounds |
| Breakables (rocks, eggs, urns) | done (code) | Chest with no label (`Chest.IsLabelVisible` false, not a strongbox) → kind `breakable`; a sound per material (`Breakables` by path: boulder, rubble, urn, pot, big pot, statue, egg, bone, barrel, crate, basket → `sounds/breakables/`, from each `.ao`'s FMOD event via `Poe2UiExtract ao-sounds`, the softest sample of its bank; crates a name guess), the rest Breakables_01; all low-passed at 2.8 kHz and kept at peak 0.3 (rocks were "ultra shrill", user 2026-10-27); every breakable in range sounds (12 voices, 70 ms apart, not waiting for other groups); a chest named `*Switch*`/`*Lever*` (`Switches`, unit-tested) → kind `switch`, `switch.wav` (a hand on a lever). Bodies searched like chests sounded like chests (user 2026-10-28): `Breakables.IsCorpse` (CorpseChests, the Ogham village's `Metadata/Chests/Impalement01` "Víctimas" seen live 2026-10-28, hangsaw, stocks, witchstake, crucifix, mummified) → kind `corpse`, `corpse.wav` (Corpses/Open_04, brought to RMS 0.1 / peak 0.8: the sample is 11 dB under the chest's); chests and bodies were heard one at a time: every one in range now sounds (6 voices, 70 ms apart within the crowd) |
| NPC and player taps | done (code) | two NPC streams at once; other players a higher double tap (`CueSynth.Layer.Player`); a player down (Life not alive, the local partner too) a soft falling ding-dong at half the cadence, heard off screen and behind walls (`Layer.PlayerDown`); unit-tested; setting `PlayerSounds` silences the players alone (a tester, 2026-10-28: crowded towns) |
| Scanner Quest category (objective's proper nouns) | done (live) | "Misión. El molinero hinchado, único, 5 pasos"; `QuestMatcher`. A name's words also match in the plural ("Busca los altares de ritual" → "Altar de ritual", unit-tested). Places are never targets ("Encuentra la entrada al Valle rojo" led to an "entrada" tile, "Busca a Asinia en la Tumba de la consorte" to a "tumba" prop); a radar tile note that is no area name (`QuestMatcher.TileNote`, `LandmarkTable.IsAreaName`) only by its whole name ("Ogham" in "El tesoro salvaje de Ogham" led to "Ogham Farmlands Or Deadend", a dead end; user 2026-10-28) |
| Boss life + dodge warning | done (live) | "El molinero hinchado 72 por ciento"; whoosh + snap only on attack animations (`DodgeThreats`), a rare's blow within 150 units, louder when closer; timed half a second before the blow as learnt from the hits taken (`DodgeTimings`, `config/dodge-timings.txt`; MudGolem GroundSlam 1156 ms), at the wind-up until learnt (`docs/research/dodge-timing.md`); fuller since 2026-10-28 (sweep floor 0.6, snap 35 ms, far gain 0.8, same peak: a tester found it hard to hear in boss fights) |
| League encounter in the vitals readout (L3 double tap): "Encuentro de ritual. 100 Tributo. 0 rituales restantes" | done (code) | `EncounterReader`: visible texts under the HUD's `tertiary_hud_layout` except the quest pages (UI dump 2026-10-26); changes during play were already said as HUD news |
| Culling Strike cue (on while a skill culls) | done (code; skills read live) | `CullWatch` + pure `CullThreshold` (KeywordPopups "CullingStrike": 35/20/10/5% by rarity); a recorded gong sped up and cut to 0.85 s (`assets/cull.wav`, user's recording 2026-10-30; take G2 of https://claude.ai/artifact/1fXBPHVbfmPkZrh7Dxh669), else the synthesized trembling gong (`CueSynth.Cull`, pick H5 of https://claude.ai/artifact/NVEVcncyDggik2bhT11PnJ) on the enemy's side, rares and uniques named. Setting `CullCue`: with a culling skill (default) / always / never; the skills from `catalog/cull-skills.txt` (`CullSkills`, extractor `cull-skills`) against `PlayerSkills` (KillingPalmPlayer seen live 2026-10-29). Was a switch off by default that a Monk never found |
| Area memory: explored ground, remembered exits/NPCs, way in | done (code) | `AreaMemory` by instance hash (AreaInstance.AreaHash, the same after a portal to town and back and after a host restart, log 2026-10-29): `EntityCatalog.Forget(instance, name)` keeps the instance left whole (8 in memory) and restores the one entered; the explored blocks (`ExplorationMap.Save/Load`) and the way in written to `config/explored/<hash>.bin` on leaving and every 15 s (40 kept). Explored ground was lost on every portal and restart (user 2026-10-29) |
| Quest marker for "cross this area" objectives | done (code) | `QuestMatcher`: an objective that names only the current area and no other proper noun ("Sigue a través del Enredo sombrío") targets every area exit but the one back (`AreaMemory` way in), else the nearest unexplored ground; it named nothing before (user 2026-10-29) |
| Placed-object cue (Tempest Bell, Frozen Locus crystal) | done (live) | `DeployedWatch` + pure `DeployedObjects`: entities by path (`Metadata/Effects/Spells/monk_bell/`, `monk_spectral_bells/`, `Metadata/MiscellaneousObjects/FrostWall/OrbOfIce` seen live 2026-10-29); heard once standing 400 ms; the game's own sound (`tempest_bell` = `Four_TempestBell.bank` TempestBell_FX_OnHit_01, `ice_crystal` = `Four_FrozenLocus.bank` FrozenLocus_FX_Appear_01; launcher sounds step v2), the small bell `CueSynth.Deployed` until extracted, on its side, 700-2200 ms by distance, spaced 180 ms apart, object volume; both heard live 2026-10-29. GameHelper's Actor deployed list (0xC28) was empty, and no Actor vector in its first 0x1400 bytes held the crystal's id (survey 2026-10-29) |
| Charges, infusions, surges, Rage | done (live: power charges) | `ChargeWatch` (in `PlayerAlerts`) + pure `PlayerCharges`: buff stacks of `power/frenzy/endurance/spirit_charge`, `fire/cold/lightning_infusion`, `instilling_fire/cold/lightning/chaos`, `druidic_prowess`, `arcane_surge`, Tempest Bell's hidden `tempest_bell_combo_tracker` (seen live 2026-10-29; its stacks taken as the Combo, 4 to ring the bell); a glass tick per one held, a lower tick per one lost, the count said after 400 ms of quiet (power charges: the game's electric `power_charge`, the Combo the user's `assets/combo.wav` a semitone higher per point from C, both stereo and unpanned; 2026-10-30); Rage from Stats `current_rage` / `virtual_maximum_rage`, said only full or empty; in the vitals readout. Said and heard by separate switches, the Combo apart (`ChargeSpeech`/`ChargeSounds`, `ComboSpeech`/`ComboSounds`, levels "Voz: cargas" / "Voz: puntos de combo"; were under life and mana's, which re-said them before any interruption; user 2026-10-30: too much speech). Heard live 2026-10-29: "1 carga de poder", "sin cargas de poder" (Falling Thunder spends them), "2 cargas de poder". Not yet live: the other kinds, Rage. Enraged: no player buff found by name (the BuffDefinitions "enrage" rows are monsters'); "Player buff seen" will name it |
| Primed for Stun cue (Boneshatter: heavy stun and shockwave) | done (code) | `EnemyStateWatch` (own switch `PrimedStunCues`) + pure `PrimedThreshold` (KeywordPopups "PrimedStun": 40/50/60/70% by rarity) on `GameLayout.ActorComponent` (threshold Actor+0x4E4, buildup +0x4E8; stun traces 2026-10-26: a hag at 54 of 60 stunned by the next hit); not heard live yet. Freeze / Electrocution / Pin buildups: not found (the second and third 0x1C entries after the stun one stayed 0 against a Warrior; `stuntrace` logs Actor 0x4C0-0x5C0) |
| Enemy state sounds: heavy stunned, frozen, electrocuted, pinned, armour broken, dazed, parried | done (code) | `EnemyStateWatch` + pure `EnemyStates`/`EnemyStateNews`: buffs `frozen`, `electrocute_display_buff`, `pinned`, `broken_armour_debuff`, `dazed`, `parried` and the animations HeavyStun[Back|Forward] / Electrocute*; a sound per state (`CueSynth.StateCue`) on the enemy's side as it begins, rares and uniques named with the game's own term; on by default (only states the party causes happen); heavy stun seen in traces, the others not yet heard live |
| Raise Shield feedback: clank up as the shield rises, down as it falls | done (live) | `ShieldWatch` + pure `ShieldStance` on the player's animation name (game Animation rows 0x1DF-0x1E2: ActiveBlockChannel[Moving] held, ActiveBlockEnd[Moving] lowering); a blocked hit's flicker is ignored for 300 ms; heard live 2026-10-26 (log: raised ActiveBlockChannel, lowered at ActiveBlockEnd or by attacking) |
| Parry cue (Parry: buckler, targe, two swords) | done (code; skills read live) | reverse hit whose dead stop falls `ParryLeadMs` (150) before a timed blow (`BossWatch` + pure `ParryTiming`, `CueSynth.Parry`); only when the player's skills list `ParryPlayer` (`ParrySkill`, chain verified live on a Warrior 2026-10-28); never on the 191 attacks the files mark unblockable/unparryable (`hit-times` `;noblock,noparry`, `SkillFlags`; launcher step v2); user picked reverse hit A; [notes](docs/research/parry-and-stun-bar.md) |
| Unblockable attack cue | done (code) | two brass stabs at the wind-up of a rare's/unique's attack marked `;noblock` (`BossWatch`, `CueSynth.Unblockable`, `DodgeTimings.CanBlock`); the plain dodge warning stays for blockable ones (user, 2026-10-28; picked sample A) |
| Harmful ground alarm | done (code) | soft low pulse while a buff of `catalog/hazards.txt` is on the player (`HazardWatch`, pure `HazardGround`, extractor `hazards` from GroundEffectTypes +0x20 and "standing in" descriptions, 61 ids); a tester's ask; user picked sample A; not heard live |
| Stun bar while blocking / parrying | done (code; bar read live 0/263) | `ShieldWatch` + pure `StunMeter`: "Barra de aturdimiento N por ciento" past 50/75/90; a blocked hit not heard live |
| Energy shield warnings | done (code) | `PlayerAlerts`: 75/50/25 and "agotado", pool ≥ a fifth of life; no live DPS meter exists (per-skill DPS already read in the Skills panel) |
| Volumes to 300 %, dodge/parry volume | done (unit) | `AudioCueService`: a gain above 1 drives the voice into `CueSynth.Limit` alone (dodge +5 dB at 200, +7 dB at 300); `DodgeVolume` apart from `EnemyVolume` |
| Boss weak point heartbeat (the Devourer: strike the head, not the tail) | done (live) | `WeakPointGuide` + pure `WeakPoints`: a unique monster sharing a "Tail" entity's name (MonsterVarieties MudBurrowerTailBoss / DevourerBossDuoTail have no boss bar, read 2026-10-26); silent while the head is out of play (it turns untargetable as it burrows: the log toggled in and out of play through the fight, won 2026-10-26) |
| Cue timbres and mixer | done (code) | beacon an iron chime, dodge a blade drawn over a low gong into a bass drop, the hit at 0.2 s as the snap was (`CueSynth.Dodge`; a noise sweep was taken for the radar's open-floor breath and a rising tone sounded like a cartoon; the user picked this from samples, 2026-10-28), both in a short stone-room tail; growl with a sub-octave; levels raised toward the game's own sounds; soft limiter instead of clipping (`CueSynth.Limit`) |
| Mana warnings, vitals on L3 double tap | done (live) | "Maná 50 por ciento"; "Vida 85 por ciento, maná 100 por ciento. El molinero hinchado 65 por ciento" |
| Vitals: flask uses, level progress, gold, explored share (D2R) | done (live) | "Frasco de vida menor, 6 usos… Nivel 3, 5 por ciento hacia el nivel 4, nivel del área 15. Explorado 41 por ciento" (2026-10-25); `FlaskWatch` (GameHelper FlaskInventory + Charges 60/10), `ExperienceTable` (`catalog/experience.json` from ExperienceLevels), `ExplorationMap.ExploredPercent`; empty-flask warning heard: "Frasco de maná menor vacío" after six drinks in Clearfell, then the game's own "Sin frascos" (2026-10-25; flasks are not drunk in town or at full life/mana); the uses left said after each drink, "Frasco de vida menor, 3 usos" (code, user asked 2026-10-26): flasks tracked by belt slot, since a drink makes a new item entity (`FlaskDrinks`, seen in the log); a use gained and a flask put on the belt said too |
| Loot filter for item sounds and approach names (D2R) | done (code) | `LootFilter`, setting "Objetos que suenan y se nombran": todos / mágicos o mejores / solo monedas, gemas, objetos de misión, raros y únicos; the scanner still lists every item; not heard live (no ground items in town) |
| Target name on right-stick aim, crosshair beep | done (code) | GameHelper `MouseOverEntity`; never heard live yet (no monster under the aim during the bot test) |
| Harmful states (Urge speech) | done (code) | `StatusWatch` (buff ids by stem; every new id logged "Player buff seen"); the mod's heartbeat was removed (the game has its own, user 2026-10-24) |
| Game sounds for loot/chests/exits/waypoints | done (live) | `ObjectSounds` + `GameSounds`; each item its own drop sound from the game's tables (`ItemDropSounds`, `catalog/item-sounds.json`: scroll → `Scroll_Drop_01`); up to four items at once, pitch up/down by screen side; portal, exit, waypoint, checkpoint = continuous idle beds (`sounds/loops/`, `AudioCueService.Bed` with pitch; code, not yet heard live) (`docs/research/game-audio.md`) |
| Mod phrases in the game's 10 languages, localized area names | done (live: es, fr) | `Localization` (game ini or setting) + `Phrase` handler: other languages look the English composite format up in `catalog/lang/<code>.json` (406 phrases, game terms checked against ClientStrings); `tools/localization/phrases.py check`; `catalog/area-names.json` from WorldAreas |
| Area level (its monsters' level) and player levels | done | `Announcements.Area`/`Levels`/`OtherPlayer`: "Área: Sierraclara, nivel 2." on entry, "Nivel 4, nivel del área 2" on the L3 double tap, other players "…, nivel 20" in the scanner; GameHelper AreaInstance +0xBC (log "Area level N") and Player +0x204 (level-ups heard live); checked live 2026-10-25: Clearfell 2, the Act 1 town 15, both as in WorldAreas (the town row really says 15; kept, the user wants towns to say theirs) |
| Named places walked into and props in reach (D2R) | done (live, town + Clearfell) | `PlaceNarrator`/`PlaceMap`/`PlaceTracker`, `catalog/places.json` from `Poe2UiExtract places` (2148 named tiles, tile and room props): "carro", "yunque", "campamento"; scanner category Lugares; tile props turned by RotationSelector (only 3 measured); `docs/research/places.md` |
| Pre-game hints, delete/creation screens, edit done | done (live) | hints on entry, only new ones after; "¿Estás seguro…? yarkey… cancelar, botón, 1 de 2"; "Zq, edición terminada" |
| NPC dialogue options read one by one | done (live) | `UiSemantics.OptionLists`: confirm glyph marks the option; D-pad left reaches the lore column; a heard topic (drawn grey) reads "Presentación, atenuado, 1 de 8" (Una, 2026-10-24) |
| Spoken settings menu + sound glossary (hold L3 1.5 s) | done (live; submenus in code) | `SettingsMenu`, `SoundGlossary`; since 2026-10-30 in submenus as D2R (13 with "Volumen de cada sonido") (pure `SettingsCursor`: left/right opens, L3 tap goes back, double tap closes; each reopens where left); the flat list of 128 was "todo por doquier" (user) |
| Volume of each sound ("Volumen de cada sonido", after Volúmenes) | done (code) | every cue and bed names its glossary key; `AudioCueService.SetLevels`/`Level` (settings `SoundLevels`, percent of the group volume, 0-300); ground items by base folder (`ItemSoundGroup`: flask, equipment, gem, currency, gold, quest, other, rare layer), the discovery chime its own; left/right steps 25 % and plays it (user 2026-10-30) |
| Ground items quiet in fights | done (code) | `FightWatch` + pure `FightState`: a rare or unique (default) or any alive enemy within `ObjectSounds.RangeWorld`, held 2 s; drop sounds, approach names, the A target and the HUD tooltip; named once the fight ends; setting `QuietItemsInFights` (user 2026-10-30) |
| Enemies coming into view without direction | done (code) | `EnemyDirections` (on); off: "Ahogado, 3" (user 2026-10-30: too many words while a boss talks) |
| Target from the HUD target bar (controller) | done (live) | HUD > entity_info_container > entity_info_display > name_label; MouseOverEntity is 0 with a pad |
| Dodge timing before the first blow (game files) | done (unit, values checked against blows timed live) | `catalog/hit-times.txt` from `Poe2UiExtract hit-times` (6929 attacks loaded; 6084 of the 7398 warned of timed; projectiles add their flight at the files' speed, unit not timed live); repeat swings warned each period; `docs/research/dodge-timing.md` |
| Death place "Donde moriste" among the exits (D2R) | done (code) | `DeathWatch.Died` → `EntityCatalog.RememberDeath` (area hash + position); not seen live (no death since) |
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
| Route turn prompts, partial routes, controls help in the settings menu | done (live) | "Ahora arriba a la derecha, 10"; help topics open the L3 menu; the menu edge knock was removed (user 2026-10-24) |
| Game updates: layout found again, data regenerated, losses said | done (every part judged live 2026-10-25: login, character creation, town with a quest marker, options, inventory, passive tree, skill bar, left trigger) | `LayoutProbe` (+ image textures), `InGameFields`, `ControlFields` (+ tabs, skill slots, passive canvas and nodes, class selector, panel title, hint, check box and list seen changing), `EntityFields` (in-play flag, quest marker), `FlagBits`, `PadButtonCheck`, `PassiveTreeFields`, `TileGridSearch`, `BaseNameField`, `GameStateService.AreaRowOffset`, `CompatibilityReport`; `tools/package/Update-GameHelper.ps1`; launcher `GameDataSetup` + `DatContentColumns` (passive flags, skill keys by content); dev command `layout`; `docs/compatibility.md` |
| Game data survives mod updates | done (simulated on a copy of the user's `dist`: carry-over 2 s, update restored, one step rerun alone) | `GameDataSetup` + pure `GameDataStamp`: per-step versions, catalogs in `app/GameData` copied over the shipped ones, sounds in the plugin; a game patch reruns all in ~33 s: one extractor `batch` run on one opened index (`Batch.cs`); `tools/README.md` |
| Scanner limited to what a sighted player sees | done (live) | `EntityCatalog.Sighted`: enemies on screen, minimap-icon things once the client has them, the rest once seen or explored; Quest category at any distance ("Misión. Campamento de Sierraclara, 130 pasos" from unexplored Riverbank); `docs/research/minimap.md` |
| Enemies coming into view | done (code) | `EnemyNarrator`: once per monster per area, packs grouped: "Ahogado, 3, derecha"; uniques stay discoveries |
| Inventory actions cut in | done (code) | "En la mano" held 400 ms (was 1.5 s, which queued the moves after it); equip/unequip/hand empty/arrivals interrupt |
| Social, Challenges, Cosmetics, Quests, Character, Skills panels | done (live) | filter box as an edit field; Challenges rows by their `selected_*` marker frame ("El relicario, 0/50, 5 de 8", expand reads the goal); free-cursor pages read after the tab; icon buttons named by their tooltip ("Efecto para arma") |
| Passive tree: node state and links | done (live) | `UiSemantics.PassiveTree`: "Armadura y evasión, alcanzable, +10 a la armadura… Conexiones: arriba, Armadura y evasión, asignado; abajo a la izquierda, …"; points and attributes on opening; name and state at once, stats then links once the game builds the tooltip (code 2026-10-29) (`docs/research/passive-tree.md`) |
| Speech priority per kind, wall tones, small-obstacle knock | done (code) | `SpeechPriority` (11 "Voz: …" settings; charges and combo since 2026-10-30, wait by default), `WallTones` (off by default), `PassageScan.IsSmallObstacle`; unit-tested, not heard live. Messages hold the floor for about their length (0.6–2.5 s, `SpeechFloor.MessageHoldMs`): state titles ("Menú del juego.", "Selección de personaje." were cut by the first focus 300 ms in), the game's refusals ("El objeto no tiene espacio para más mods."), skill-bar alerts ("Inventario lleno" was cut by "Ya no llevas ningún objeto"), move notices; the focus that lands meanwhile follows (live 2026-10-28). Back to play from a panel or an NPC (focus gone 250 ms, only the HUD on top: no screen change marks a dialogue closing) stops what was being read, keeping what was asked after the close (`IAccessibilityOutputService.StopBefore`); the game menu closing stops too. LB/RB that switch a merchant's or the stash's tab with the focus left in the inventory say the tab (`ScreenNarrator.OnTabKey`; "Armaduras", "pestaña 2" live 2026-10-28) |
| Item in hand (inventory cursor) | done (live) | `InventoryWatch.TickHand` on GameHelper's Cursor1 inventory: "En la mano: Pergamino de sabiduría, 3. Elige una celda…", "Ya no llevas ningún objeto"; pick-ups now named from the item entity (host `Inventory.Items` keys are slot entries, not entities) |
| Monster modifiers on the aimed monster | done (code) | `MonsterMods`: names from the game's Mods.dat rows in the client language (first gender branch); the Miller's mods have no name, so nothing is added, as on its plate; not heard on a magic/rare monster yet |
| Gold pick-ups and total | done (live) | `GoldWatch`: rise of the inventory's gold counter during play, "Recogido 17 de oro" heard with the panel hidden after the Miller (2026-10-24); the total with the L3 double tap ("113 oro", 2026-10-26; shared in local co-op), named in the in-mod L3 help |
| Reach ping (D2R) | done (code) | `EnemyCues` + `ReachGate` (unit-tested): body gap from `Render.ModelBounds`; two rising notes, re-armed past reach + 25 |
| Unexplored-areas scanner category | done (live) | `ExplorationMap` (unit-tested): "Sin explorar. Zona sin explorar, 7 pasos, arriba 4, luego arriba a la izquierda"; forgotten on area change and host restart |
| Highlighted control without focus, league selector | done (live) | `FocusTracker`: a visible child named "hightlight" (the game's spelling) marks its parent pre-game: "Continuar, botón, 3 de 3" after the name field; combo values skip `*_popup` children ("Liga, Forbidden Rites…"), the popup is read after the focus; a hovered element only beats the highlight when it moved last (a class info panel stays hovered while the stick is on the name, 2026-10-28); HUD `paused_frame` ignored |
| Equip / unequip / arrival notices (D2R) | done (live) | `InventoryWatch.TickMoves`: the game makes a new item entity on every inventory change, so moves are departures and arrivals paired once still for 300 ms: "Equipado: Ballesta improvisada, Mano principal", "Desequipado: …"; "Nuevo en el inventario" / "… salió del inventario" with a panel open (vendor/stash untested); the hand's "Ya no llevas…" waits 800 ms and is dropped for an equip. Since 2026-10-28 said as the deed (`ItemMoves`, pure, unit-tested; user: not "salió del inventario"): gold that rose with a departure is "Vendido: …, por 9 de oro" (Y at a merchant sells at once), gold that fell with an arrival "Comprado: …"; otherwise the panel beside the inventory names it, "Movido a Alijo" / "Sacado de Alijo" (`UiSemantics.SidePanelTitle`: the shown titled panel under panels_parent that does not hold the main inventory grid, whose order changes when a panel opens: dumps 2026-10-28; the hand remembers the panel it picked up in); gone with only the inventory open is "Tirado" once a new ground item of that name shows within 1.2 s, else "Usado". Live 2026-10-28 (yarkey and yarkidius in Clearfell): sold and bought back magic boots at Finn, rune to the stash and back; "Tirado" not heard (a town refuses drops: "¿Quieres destruirlo?", kept). Held against the focus that follows (`SpeechFloor.MessageHoldMs`); the player's own cursor move (a direction held within 500 ms) ends the hold and follows the notice queued (`SpeechFloor.Move`; user 2026-10-28: cells lagged up to 2.5 s after each sale) |
| Class choice position | done (live) | `UiSemantics.SelectorPosition`: ClassVariationSelector +0x394 index over the first icon row: "Monje, 6 de 6. Mi vida…"; class id +0x390; the second row is the variants (X on the name step) |
| NPC quest bell (D2R NPC "!") | done (live) | `EntityCatalog.HasQuestMarker`: "Personajes. Renly, con misión, 16 pasos…", "Una, con misión"; bell (`CueSynth.Ring`) among the object sounds and in the glossary |
| Item comparison | done (live, the game's own) | R3 "Comparar" opens Modo inspección: the item beside the equipped ones, links walked with the D-pad; an item's text is read once, then only the links ("Set de armas, enlace") |
| Barriers: routes, scanner, opening | done (live) | `BarrierMemory` (unit-tested): closed believed at any distance, walkable only within 120 cells; routes cut by a closed barrier say "una barrera cerrada corta el camino"; scanner "Elementos. Barrera cerrada"; `BarrierWatch` "Se abrió el paso, …"; the guide says "lo más cerca posible" at the end of a partial route |
| Tutorial lessons that freeze the character | done (live) | lessons that hold the character until a press: dodge (B), low mana (D-pad right flask), passive point (View), engrave the uncut gem before leaving the camp; the lesson's button line is read ("View para abrir los menús"), a lesson shown again during play is said again after 5 s, and in play it is kept whole against interrupting lines. In code (2026-10-26, user missed the passive point behind inventory moves): in menus a lesson cuts in, is kept whole (`SpeechFloor`: what interrupts meanwhile waits, only the latest follows) and the focus it cut is said after it; a pending lesson back with a panel or menu is reminded after 60 s (`LessonReminder`) |
| Options page: glyph labels, scroll, slider rows, combos | done (live) | "Encaje de objetos stick izquierdo / cruceta", wrapped labels joined, deadzone "3 de 10", rows scrolling into view not read, value-beside-slider rows keep the list position, combos opened in place: "Eligiendo: …", "Adaptativo (experimental), 2 de 3", "Elegido: …" |
| Login: gateway list | done (live) | "Servidor, Sao Paulo (BR), lista desplegable, 5 de 7"; open list reads the highlighted entry "Japan, 16 de 19" |
| NPC dialogue and vendor (town) | done (live) | speaker first ("Renly. Comprar o vender objetos, 7 de 8"); vendor stock "Coraza oxidada, columna 10, fila 10, 50 oro, no alcanza… Comprar objeto, X"; reward and vendor openings without the gold chrome |
| Trade market (game menu, "Mercado de comercio") | done (live 2026-10-28) | RB searches once a filter is set; a listing (focusable card: seller column, item column with a `cost` block) is read item, price, seller and status, position, then properties and mods (`CardText`, pure, unit-tested; `UiSemantics.Cards`); listings scrolling into view are not mixed in. Its untitled panel had let the panel-title search adopt the root's "HUD" (+0x378) for the session: titles are now searched only below `panels_parent` |
| Auto-update | done (live against the GitHub release 2026-10-28; manifest from v2026.10.30-2) | `Poe2Access.exe` (`Updater`, `ReleaseTag`, `ReleaseManifest`) compares `app/version.txt` (written by `Publish-Release.ps1`) with the latest release's `release.json` asset (tag, zip SHA-256, notes; GitHub's API gave the user's address 403 rate limit all of 2026-10-30, 60 requests an hour per address), asks (message box), downloads the zip, checks its SHA-256, keeps `app/configs/` and the plugin's `config/`, renames itself aside, rolls everything back on a failure (tested with a locked DLL), starts the new launcher; setting `CheckForUpdates`, `Poe2Access.exe update` for a manual check; ported from D4 Assist |
| Minimap marks: signs, boss rooms, points of interest, quest icons | done (signs and tile boss rooms live; icon boss rooms code) | `MinimapMarks` + `MinimapIconLabel` (row +0x1A, `dat-columns` `MinimapIcons.Label`): story glyphs "Letrero: Decreto del conde" among objects, never quest targets (the Hunting Grounds signpost "Pueblo de Ogham" was a character and the quest's village, user 2026-10-28); scanner category Puntos de interés. Boss rooms from the area's tiles (`EntityCatalog.TileLandmarks`: radar-table arenas "Sala del jefe: Asinia", unnamed arena tiles by file name), listed once within the minimap's pointer reach (MinimapIcons +0x12: 400 cells campaign bosses, 250 map boss) or seen, as the game points a sighted player to them (user 2026-10-29: mimic the map, not lost); live in Clearfell, "En el mapa: Sala del jefe: Beira of the Rotten…" at 398 cells. Boss room objects (`BossRoomMinimapIcon`, "…, derrotado") and labelled icons ("Taller de Renly") when the client lists them; `QuestObject`/`QuestItem` icons copied into Quest ([minimap](docs/research/minimap.md)) |
| Unexplored loop and radar mark (D2R) | done (loop live, mark unit) | `UnexploredLoop`: the D2R "unexplored room" recording (`assets/loops/`, shipped) at the nearest frontier place, silent in towns, menus and while guiding; radar open/exit cues toward unseen ground end in two 900 Hz pips; settings `UnexploredLoop`, `UnexploredMark` ([radar](docs/radar.md)) |
| Microtransaction shop | done (live, no purchase made) | `UiSemantics.ShopPage` + highlight markers in game (`FocusTracker.InGameHighlightRoot`): tabs, categories "Pestañas de alijo, 27/27, 2 de 13", items "Pestaña extra de alijo, 30 puntos, 5 de 27", flags, featured banners named from their image ([shop](docs/research/shop.md)) |
| Harmful ground and boss movement rules (Freythorn) | done (unit) | 7 ground buffs whose descriptions do not say "standing" added by the `hazards` extractor (swamp, spores, mud, the Voodoo King's rat tornado); "¡Quédate quieto!" / "¡Sigue moviéndote!" on the Voodoo King's punishment buffs (`StatusAilments`). Freythorn has no falls: the void between its treetop platforms is unwalkable ground the radar already calls wall |
| GameHelper window out of Alt+Tab and the taskbar | done (live 2026-10-28) | `HostWindowHider`: WS_EX_TOOLWINDOW put back twice a second (the overlay library rewrites the whole extended style on click-through changes); Alt+Tab shows only the game and the terminal |

## Derived facts (with source)
- Heavy stun buildup is "poise" in the game's data (Stats.dat: `poise_threshold`, `base_poise_threshold`, `poise_decay_delay_ms`, `is_heavy_stunned`, `cannot_accumulate_heavy_stun_poise`). GameHelper's `GameStats` enum is about 3 rows behind the 2026-10-26 Stats.dat (`poise_threshold` 17371+1 there, row 17374 now): compare raw keys, not enum names. Stat keys are row + 1.
- **Culling Strike skills:** granted effects whose main stat set (GrantedEffects +0xA8 → GrantedEffectStatSets, row 0x65,
  ImplicitStats key list +0x18) holds `kill_enemy_on_hit_if_under_10%_life` (Stats row 579; skill_stat_descriptions.csd
  describes it as "[CullingStrike|Culling Strike]"). Killing Palm's ActiveSkills description does not mention culling. 2026-10-29.
- **Player resources** (BuffDefinitions, 2026-10-29): charges, infusions, surges (`instilling_*`), Druidic Prowess and Arcane
  Surge are player buffs whose stack count is the number held (`power_charge_79` live, GameHelper adds "_79"). Rage, Combo,
  Glory and Valour are not buffs: Rage is Stats `current_rage` (enum 6994+1) / `virtual_maximum_rage`, default maximum 30
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
- **Quest tracker (UI dump 2026-10-27):** `quests_display_pages_root` holds the pages; each quest a `title_label` and its
  `quest_info` lines 5–8 levels down; only the shown page is drawn; `page_cycling_legend` beside it.
- **Freythorn ritual altars (.ot files, 2026-10-29):** `Metadata/Terrain/Gallows/Act1/1_12/Objects/CanopyRitualAltar` and the
  King's `CanopyBossRitualAltar`: typed Monster, icon `RitualRune`, `RitualRuneFinished` once done; state 0 untargetable with
  monsters around, 1 ready, 2 active, 3/4 done. The King's altar opens once the others are done.

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
  `.Tooltips` (+0x120), `.Geometry` (value drawn by a sibling), `.Cards` (trade listings, pure `CardText`), `.Slots` (button-bound slots), `.Lists`
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
  legs) and `ScanModel` (`ScanBrowser`, `StickDirection`); `LandmarkTable`. Map marks: pure `MinimapMarks` (signs, boss
  rooms by icon or tile, pointer reach), `MinimapIconLabel` (an icon's translated label). Exploring: `ExplorationMap`
  (frontier), `UnexploredLoop` (D2R loop, `assets/loops/`), the radar's unexplored mark. Warnings: `VitalAlarm`.
- Sounds: `AudioCueService` (winmm mixer; each sound's own level by glossary key, `SetLevels`) ← `EnemyCues` (`EnemyCueScheduler`, `CueSynth`) and `Radar`
  (`PassageScan`, `RadarSensor`, `RadarSynth`; a second `Radar` follows `Seats.Partner`); with `ProximityNarrator` (`ProximityTracker`) gated in
  `Poe2AccessibilityCore.TickSounds`.
- Shop/stash: tooltip `cost` → `CurrencyIcons`, red price via `UiElementReader.IsTextRed`; grid tabs `UiSemantics.GridTab`.
  Microtransaction shop: `UiSemantics.ShopPage`, `.Cards` `ShopEntryOf` (cards, position), `CardText.ShopImageTitle`
  (featured banners), `FocusTracker.InGameHighlightRoot` (the shop's highlight markers; the game focuses nothing there).
- Gameplay guides: `Beacon` (+ pure `BeaconMath`), `QuestMatcher`, `BossWatch` (boss life + dodge whoosh, pure
  `DodgeThreats`), `TargetNarrator` (aim names + crosshair), `StatusWatch` (+ pure `StatusAilments`), `EnemyNarrator`, `DeathWatch`,
  `ObjectSounds` + `GameSounds` + `ItemDropSounds` (game WAVs), `CheckpointPlaces` (+ pure `MapPlaces`),
  `ScreenProjection` (world offset → screen, on-screen test), `FlaskWatch`, `ExperienceTable`, `LootFilter`, `ItemSoundGroup`,
  `FightWatch` (+ pure `FightState`: ground items quiet in fights).
  Combat state cues: `CullWatch` (+ pure `CullThreshold`), `EnemyStateWatch` (+ pure `EnemyStates`, `EnemyStateNews`),
  `ShieldWatch` (+ pure `ShieldStance`, `StunMeter`), `WeakPointGuide` (+ pure `WeakPoints`); player skills: `PlayerSkills` (granted
  effect ids via `DatKey`, Parry and `CullSkills`), pure `ParryTiming`; research: `StunTrace` (dev `stuntrace`).
  Player resources: `ChargeWatch` (+ pure `PlayerCharges`) in `PlayerAlerts`, beside `StatusWatch`.
  Speech priority: `IAccessibilityOutputService.Urge` (later interrupts queue for 1.5 s), `.Keep` (tutorial lessons; `SpeechFloor` holds what comes meanwhile).
- Tests: `tests/Poe2Accessibility.Tests` (differ, text edits, chat diff, gestures, scanner, route legs, life
  alarm, beacon, quest matching, cooldown, ailments, dodge threats, map markers). Dev tools: `tools/computer_use/`; dev commands
  `entities`, `hints`, `gesture <Tap|DoubleTap|Hold|StickClick|FlickUp…>` (command file works without ResearchMode).
- Launcher (`tools/Poe2AccessLauncher`): `Updater` + pure `ReleaseTag` (auto-update), `HostController`, `GameDataSetup` + pure
  `GameDataStamp` (per-step versions; catalogs kept in `app/GameData`, survives mod updates); the
  plugin's `HostWindowHider` keeps GameHelper out of Alt+Tab.

## Next step
First (2026-10-30, thirteenth batch, not yet in the user's `dist`: the mod was running, copy `Poe2Accessibility.dll` and
`catalog/lang/*.json` once it is stopped): hear "Volumen de cada sonido" (a flask at 50 % against equipment), a boss or
rare fight with drops on the ground (silent until 2 s after, then named), and "Dirección de los enemigos que aparecen"
off ("Ahogado, 3").
First (2026-10-30): the user opens `dist`'s `Poe2Access.exe` (the v2026.10.30-2 launcher, put in alone over v2026.10.29-3):
the log should say "latest v2026.10.30-2" from `release.json` and the update install; that judges the manifest live.
Next (2026-10-29, twelfth batch, deployed into the user's `dist`): hear the culling gong on a Killing Palm Monk (rares named),
the charge ticks and counts (power heard live), Rage full/empty once "Furia al impactar" is taken, the other resources on
other classes, and the sound glossary at raised volumes ("…, Volumen de enemigos"). An Enraged buff id will show in a
"Player buff seen" line. Also in this batch, untested live: the bind screen's spirit panel naming each value's weapon set
("set de armas I: 0 de espíritu", `WeaponSetRows`; which box shows while managing set II, docs/research/skill-bar.md) and
a layout id no longer spoken before a caption ("action binding display", a tester).
Next (2026-10-29, eleventh batch, deployed into the user's `dist`): in Freythorn, hear the remaining altars named on approach
and listed in Misión and Encuentros, then "Altar de ritual del jefe" in the arena; a "Minimap mark not listed" log line is a
gap to classify. The quest page is said on area changes and on returning to the game window (heard live).
Next (2026-10-29, tenth batch: minimap marks, boss rooms, unexplored loop and radar mark, the shop, Freythorn's ground
rules): user test in the Tomb and the Mausoleum ("Sala del jefe: Asinia" and the Mausoleum's arena among Puntos de
interés once within 400 cells, the objective leading there), the unexplored loop and the radar's two pips in a maze,
signs ("Letrero: …") on the road to Ogham, the shop (no purchase was made in tests: hear the buy confirmation), and the
Voodoo King's "¡Quédate quieto!" / "¡Sigue moviéndote!". Not seen live: a `BossRoomMinimapIcon` object (none was in the
client's lists, also not with the Crowbell alive at its arena; such an object without a Render component would be
skipped by the catalog, whose positions come from Render), the "Taller de Renly" icon, the big title cards of area parts.
Before that, (2026-10-28, ninth batch, deployed into the user's `dist`: DLL, translations, `hazards.txt`): the user chose sample A
of the unblockable cue and of the hazard alarm (samples page v3 https://claude.ai/artifact/WZBmymWdgsrWpj5Yd2aRWk, already
in the mod); hear them in a boss fight (the Miller's charge: stabs at the wind-up; caustic or burning ground: the pulse
until stepping out). Before that, (eighth batch, deployed: DLL, translations, flagged `hit-times.txt` in the plugin
catalog and `app/GameData`): the user chose parry reverse hit A; test with a buckler/targe character: does the stop fall
right (tune "Antelación del aviso de Parada"), and is a red-glow attack (the Miller's charge) left to the dodge warning?
Then the stun bar while blocking (Warrior yarkidius, Raise Shield), energy shield news on an ES character, and the dodge
warning at 200-300 % "Volumen de esquiva y Parada". Before that, (2026-10-28, seventh batch): user test while selling: the cell after a "Vendido" follows the notice instead of waiting out its 2.5 s hold. Before that, (2026-10-28, sixth batch): character creation's name, league and Continue read in turn with the stick after B (user, live; a stale hover no longer wins over the highlight); game-patch regeneration 33 s instead of 85 s. Before that, (2026-10-28, fifth batch): mod updates no longer regenerate the game data (only steps whose version was raised; a raised version is how a new sound reaches players now, not `StampFormat`); reaches players with the next release, whose first start carries over the old stamp (nothing for format 7, the sounds alone, ~2 s, for 3-6). Before that, (2026-10-28, fourth batch, deployed into the user's `dist`: DLL): user test of own chat lines read back in every channel; the Huntress skills' missing descriptions in the engrave list need the tester's log (no uncut skill gem on the user's characters to reproduce; listing texts are now filtered only when they lie in sibling rows of the focused list, so a details panel beside a grid is never dropped). Before that, (2026-10-28, third batch, deployed into the user's `dist` in place: DLL, translations): user test of the trade market
listings, the new dodge warning (blade, gong, bass drop) against the radar, "Other players' sounds" off, and the GameHelper window gone from
Alt+Tab; the updater works only from the next release on (its package carries `app/version.txt`). Before that,
(2026-10-28, second batch, deployed into the user's `dist` in place: DLL, translations): user test of the move
notices as deeds (sold, bought, stash, dropped outside town, used), messages held against the cursor, speech stopping
when a panel or NPC closes back to play, and the tab said on LB/RB with the cursor in the inventory. Before that,
(2026-10-28, deployed into the user's `dist` in place: DLL, translations, `sounds/corpse.wav`): user test of every
chest and body sounding at once (bodies with their own rummage: the Ogham village's victims), the trade invitation message
heard whole while in a party, the Quest category without places or tile notes, and the fuller dodge warning in a boss fight.
The dev build now deploys `sounds/loops/` and `sounds/breakables/` too (only top-level and `items/` were copied before). Live
check 2026-10-28 00:35-00:39 (dev host, yarkey in Pueblo de Ogham): sounds load with `corpse`, the impaled victims are
`Impalement01`, the scanner lists exits, checkpoint and "Donde moriste" with nothing false under Quest. yarkey died while the
host was restarted (idle by the checkpoint) and was resurrected there before quitting.
Before (2026-10-27, deployed into the user's `dist` in place: DLL + `sounds/breakables/`): user test of the chat after a
fresh game start (a whisper from a friend read by itself, "De …: …") and of the per-material breakable sounds (all at once,
softer; rocks). Ritual favours window, Act 1 (dump 2026-10-27 22:27, window open): no defer or reroll there. The defer
(`layby_pay_button`), reroll (textless `[…,18,0,2]` / `[…,18,0,12]`) and "ofrecer tributo al rey" (`tribute_button`)
buttons are all hidden; favours offer only "Comprar objeto, X" in their tooltip; the gamepad legend
(GamepadLegendRitualSuspendItem "Reservar objeto", ConfirmPrepaySelection "Confirmar selección") never showed; ClientStrings
has an Act 1 help text of its own (RitualRewardWindowHelpPopupAct1), and reroll is "once per area" for a cost
(RitualRewardWindowRerollPopup). The "Modo de reservas" banner `[…,18,0,13]` is flagged visible with a rect, so it is
read on open as the game shows it. To do when a ritual offers deferral (later act or endgame): an F6 dump to check the
defer legend and the reroll button are read. Crossbow loaded ammo (user wish, low priority): not in the UI as text — the
F6 dump taken while loading (2026-10-27 22:53) differs from the 22:27 one only in XP, life/mana and a status counter
under `skill_bar` `[…,19,1]`; slot icons are not probed strings, and no player buff names the ammo. Next step if wanted: a
live trace of the player's skills / stats while loading ammo (GameHelper Actor skills, Stats) to find the loaded type.
Then: user test of the late 2026-10-26 batch, deployed into the user's `dist` in place (DLL, two sounds, extractor DLL):
the Encounters category on a Ritual altar (is the altar listed and named "Altar de ritual, A" when the favours
open?), the breaking sound on rocks and eggs (if real chests break too, `IsLabelVisible` is not the split), a lone item
named again when walked back to, L3 + R3 releasing the guide, two NPC taps, the player double tap and the down call.
Then user test in combat of the 2026-10-26 enemy state sounds and the Primed for Stun wind-up (Warrior with
Destrozahuesos; is it too frequent, are the seven sounds told apart?); then find the Freeze / Electrocution / Pin buildups
with `stuntrace` against a cold or lightning build. Then: user test (DLL and package of 2026-10-26 12:30 in `dist`) of the 2026-10-26 lesson priority (inventory with a pending passive point) and skill details in
the engrave list. Then: user test of local co-op with two real controllers (both toggles, a revive by holding A; the revive and
respawn lines are unit-tested, not heard live). Then: user test of the 2026-10-25 UI sweep fixes (`docs/research/ui-sweep-2026-10-25.md`) and of the player 2 radar
in real couch co-op (two pads; J2 joins at character selection, "Cooperativo local: lectura para el jugador 2" in the mod's settings). Characters: "kehgan" (Monk, level 2, Clearfell Encampment; a town NPC shows a quest marker), "yarkbruja" (Witch, level 3,
Clearfell Encampment, no NPC marker), "yarkey" (Mercenary, level 12, Pueblo de Ogham; the user's current character). Start in town from character selection:
`desktop.py combo l2 cross --seconds 1.8` (away from town the confirmation's Cancel is focused: D-pad right, then A). Next: user test of the 2026-10-25 batch (places and the Lugares category, the other
languages, projectile dodge timing, quieter area loads, the new package layout) and of the 2026-10-24 one (wall tones,
"what A uses", weapon swap, map / chat / player menu). Cinematics and their subtitles (`enable_subtitles=true`) are
untested: voiced NPC lines come as chat lines and are read; a cinematic's text has never been on screen in a test. Places: measure the other tile rotation selectors and place the
rooms of generated areas (`docs/research/places.md`, Open). After a game patch: `pwsh tools/package/Update-GameHelper.ps1`, then the dev command
`layout` through the screens listed in `docs/compatibility.md` (self-test); no part is left unchecked. Not yet heard
live: the loot filter's quieter settings (kehgan carries no normal item; a white drop with "solo valiosos" set
must stay silent).

## Known issues / open questions
- GameHelper marks a monster Useless once the game flags it dead (stat `is_dead`) and stops reading it, so its life can freeze mid-hit ("Ezomita antiguo" 16/50 in TakeHitBackward, heard and listed as an enemy that could not be hit, 2026-10-26): `EntityCatalog.IsFrozenDead` makes it dead for enemies and boss defeats.
- League devices with life are typed Monster by GameHelper (Ritual rune, `Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable`, alive and hostile by the Devourer, 2026-10-26): enemies are only paths under `Metadata/Monsters/` (`EntityCatalog.CreatureFolder`; all 89 living hostiles in 47 dev dumps are). The rune is nameless and named by its minimap icon (Encounters category). Untargetable in two dumps (13:42, 14:06) and targetable in one (13:18): which ritual states those are is not known.
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
