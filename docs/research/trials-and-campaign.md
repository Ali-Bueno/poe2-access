# Ascendancy trials and the campaign past Act 2 (offline research, 2026-11-08)

Scope: the Trial of the Sekhemas, the Trial of Chaos, ascendancy choice and view, Acts 3 and 4 (the sailing map),
campaign puzzles and quest items, quest rewards, cutscenes and dialogue choices. Patch 0.5.5f, build 26476662.
Confidence per claim: **MEASURED** (game files, a dump or a log in this repo), **LIKELY** (consistent with the files,
not seen running), **UNSURE** (web guides of 0.5 or older, or a guess). Nothing here was seen live this session; the
Sekhemas facts come from the user's seven runs of 2026-11-07 (dumps in `dist/Poe2Access/app/Plugins/Poe2Accessibility/debug/`,
`ui-tree-InGameState-20261107-*.txt`, `entities-20261107-*.txt`). Earlier notes: [endgame-ui.md](endgame-ui.md) §1, §2, §4.

Sources used: the dat tables through `Poe2UiExtract dat-strings / dat-row-strings` (table name, rows, row size and the
offsets measured here, all 0.5.5f), the `.ot` object files (`cat`), ClientStrings, the dumps above, and the web for the
0.5 rules (poe-vault, timesaver.gg, the 0.5.0 thread; poe2wiki and mobalytics blocked the fetches, so every rule from
the web is UNSURE until seen).

## 1. Trial of the Sekhemas

### 1.1 Mechanics (what a sighted player does)
- Hub: Act 2 `G2_13` (The Trial of the Sekhemas, Balbala's). Objects seen there (MEASURED, entities dump 10:41):
  `SanctumRelicDevice` "Altar de reliquias" (relic slots; Small/Medium/Large relics take 2/3/4 slots, UNSURE),
  `SanctumLocker` "Cofre de reliquias" (relic storage), `SanctumEntranceTransition` "Prueba de fuerza" (the run's
  entrance, named after floor 1), `SanctumEntranceAltar` (a server doodad, nameless; LIKELY where the Djinn Barya is
  placed), `CraftingBenchVaalHammer` "Mesa de reforja" (new in 0.5.0 per the patch thread), a Stash, Balbala.
- A run: floors `Sanctum_1..4` (+ `_Foyer_1..3`), all named "Trial of the Sekhemas" in WorldAreas (MEASURED). Floors
  table `SanctumFloors` (4 rows): Caverns "Contains Rattlecage, the Earthbreaker", Ruins "Contains the Terracotta
  Sentinels", Depths "Contains Ashar, the Sand Mother", Abyss "Contains Zarokh, the Temporal" (MEASURED). Floor titles
  in ClientStrings: Test of Strength / Will / Cunning / Time. How many floors a Barya opens depends on its level (UNSURE).
- Rooms: `SanctumRoomTypes` (26 rows, 0x65 bytes; Id +0x0, icon +0x2B, description +0x34; MEASURED): encounters
  Explore, TimerArena (Hourglass: "Survive waves of monsters until the timer expires, killing rare monsters reduces the
  timer", lever `SanctumEncounterTimerLever` "Sand Hourglass" starts it), PortalArena, Lair, Maze, Gauntlet (traps),
  Miniboss, Vault, Puzzle, Boss, Merchant; shrines LegendHonor/Pledge/Water/Curse/Boon/Random ("Awards a Shrine to
  restore Honour", "...that greatly restores Honour and burdens you with an Affliction", "Awards a Pledge..."), fountains
  WaterMajor/WaterMinor, Final, keys BronzeKey/SilverKey/GoldKey and chests Bronze/Silver/GoldKeyChest ("Awards a
  Bronze Cache. Requires a Bronze Key to Open"). The crystal room ("Find and disable all the Crystals, any Crystals still
  active when the timer expires will damage you") is the Escape kind; the Blood Chalice room ("Defeat all Rare Monsters
  in the area to fill the Chalice") the Ritual kind.
- Honour: the run's second life ("[Honour|Honor]: <white>{288/513}" on the HUD; max = `total_sanctum_honour`, 513 =
  max life on the test character; 0.5.0 adds Runic Ward). Zero ends the run with no death screen (MEASURED, log of
  12:17). Honour resistance caps 75% (UNSURE). Buffs that drain it: `sanctum_fire_beam`, `ground_sanctum`,
  `invisible_holy_fire_sanctum`, `sanctum_resolve_vulnerability` (+50% lost) (MEASURED, buff names).
- Sacred Water: the run's currency ("39(+7)" on the HUD; the "(+7)" is UNSURE: pending water of a deferral?). Spent at
  shrines (`interaction_check GetSacredWater() >= interact_cost` in the shrine `.ot`), the merchant, and the Keth Forge
  (`SanctumWaterToKeyConverter`, turns water into keys at the floor's end, LIKELY).
- Afflictions and boons: `SanctumPersistentEffects` (117 rows, 0xAC; Id +0x0 prefixed `Affliction`/`Boon`, name +0x28,
  icon +0x30 and a small icon +0x55, description +0x5D or +0x65; MEASURED). Categories table: Minor/Major Afflictions,
  Minor/Major Boons, Pledges. Examples: AfflictionMonsterMoreDamage "Honed Claws: Monsters deal 30% more Damage",
  BoonMovementSpeed "Hare Foot: 40% increased Movement Speed", AfflictionHideRoom* (map rooms hidden),
  AfflictionHonourLossIsFatal1-3, BoonDefyDeath... They are NOT buffs (none in `buff-names.json`): on the HUD they are
  icons in `SanctumHUDDisplay`, told apart only by their art (LIKELY). The map's room tooltip names the affliction of an
  afflicted room (MEASURED 2026-11-07).
- Shrines (world-titles, MEASURED): Honour Tabana (honour), Ahkeli (honour + a minor affliction), Orbala (a boon),
  Galai (random), Halani (water), "Pledge to Kochai" (`SanctumDealObject`, a major boon with a major affliction), "Offer"
  (`SanctumDeferralObject`, UNSURE what it defers). Fountains: "Sacred Water Fountain" / "Grand ...".
- Keys and caches: plinths `SanctumKeyPlinthBronze/Silver/Gold` (icon `MarakethSanctumKey`) give the key; the caches at
  the floor's end need it ("Requires a Bronze Key to open").
- Merchant: `Metadata/NPC/League/Sanctum/SanctumNPCMerchant` (Valencienne), sells boons/relics for water (LIKELY).
- Traps: `Metadata/Monsters/MarakethSanctumTrial/Hazards/*` (GroundSpike rumble/spike, LaserCrossBeamTurret
  active/fire, ProxMine prime, TimeRewindTrap active, BulletHell*, QuicksandPit, PressurePlate_*, ProjectilePortal;
  MEASURED `.ot`, states not yet seen live). Death Crystals: `Hazards/HourglassLethal` (Targetable, InteractionAction,
  WorldDescription "Death Crystal"; 27 in one dump), the room's HUD line "{0} Death Crystals remaining"
  ("5 cristales de la muerte restantes" in the 11:23 dump, MEASURED) and a "00:19" clock; the crystals left kill about
  4 s after it ends (MEASURED, five deaths).
- Room choice: the Trial Map (`SanctumMapDevice`, "Trial Map", "Interact to select a trial") opens the map window; the
  chosen room's door opens among the three `SanctumRoomSelectorDoor` by the device (StateMachine "open; wall;",
  nameless, untargetable; MEASURED `.ot` + 3 doors and 1 device in the 11:23 dump). The floor door
  `FloorTransitionDoor` (TriggerableBlockage, "Door", title = next floor's name, needs `IsMapOwner`) leads on.
- Rewards: caches, relics (unique relics from Zarokh), ascendancy points (2 per completed trial version: Act 2's one
  floor, then floors 2 and 3 with higher Baryas; UNSURE), the run summary window `SanctumRunSummaryPanel_Gamepad`
  (MEASURED in the window registry), "Reset run" (ClientStrings).

### 1.2 UI (MEASURED unless noted)
- HUD row: four counters (icon + number: water, bronze, silver, gold) then the honour line, all under the HUD layer;
  affliction/boon icons in `SanctumHUDDisplay` (LIKELY their place); the clock "MM:SS"; the crystals line.
- Map window: columns of room buttons (8 on floor 1: 1,3,4,3,5,3,6,1), a canvas with the links (not readable), tooltip
  per room (name, reward/affliction, "Entrar, A" when enterable); a tutorial paragraph (`label:map`).
- The map window mid-run (MEASURED 2026-11-12 on the user's open map, floor 1, the player in column 4 with one room to
  enter; a visible-only dump and `focusinfo` on each element — a full dump runs out of its 20,000 nodes in the passive
  tree before reaching the window):
  - `panels_parent` child 13. The links' canvas is one element with no children: **which room links to which is not in
    the UI tree**.
  - Each column is an element, its rooms buttons of 108 by 108. What a room shows is images among its children:
    its reward or kind (`Sanctum/HealingFountainIcon`, `MoreHealingFountainIcon`, `SilverKeyIcon`,
    `RattlecageBossIcon` on the boss, `MarakethStatueIcon` on the entrance), `CurseRoomFrame` on an afflicted room, and on
    the room the player stands in `ValidPathGlow` with a second image named `public_icon` (the statue again). A room not
    revealed has no children. Rooms walked through kept their icons (key, afflicted fountain); the others behind had none.
  - A room's tooltip exists only once it was hovered: "Otorga una llave de plata | Encuentro desconocido" (the kind
    unknown, the reward known), and "Entrar" on the room that can be entered.
  - The counters row: four pairs of an icon and a number, `Sanctum/GoldIcon` (the water), `BronzeKeyIcon`,
    `SilverKeyIcon`, `GoldKeyIcon`; the numbers alone are texts ("78", "0", "1", "0").
  - Bottom left, two buttons without text: one whose tooltip is "Salir de las pruebas | El progreso de las pruebas se
    guardará y podrás regresar más tarde", one whose tooltip lists the relics' effects ("Efectos de reliquias: Armadura,
    evasión y escudo de energía aumentados un 25% | Los monstruos tienen un 6% de probabilidad de dar el doble de agua
    sagrada"). In the title bar an `Info` button holds the tutorial paragraph. Whether the pad's focus reaches the three
    is not known: the button bar showed only "Cerrar [B]".
- The HUD row past the honour line (same dump): the honour bar's fills (`HonourBarFill`, `HonourBarFillHurt`), then a
  container of five slots, each an icon and its frame (`Sanctum/BoonCurseIcons/MajorBoonIcon` + `MajorBoonIconFrame`,
  `MinorCurseIcon` + `CurseIconFrame`, …), a slot visible while the player carries one of its kind (only the minor
  affliction's was), and a sixth child with the pad hint `RightStickButton2` "+" `DPadLeft`: the game's own shortcut,
  LIKELY to the list of what is carried. The icons are the kind's, not the effect's: **the HUD does not name an
  affliction**, so the names are in that list or nowhere.
- Shrine/merchant/pledge windows, the run summary, the relic altar (relic grid + slots): ordinary windows (LIKELY: the
  generic widget readers; the relic altar dump of 10:41 was taken but not studied here — see the protocol).

### 1.3 What the mod does today (code)
`TrialHud`/`TrialCounterWatch` (water and keys as gained; vitals readout `TrialStatus`), `HonourWatch` (every tenth,
urgent), `CountdownWatch` (MM:SS marks), `TrialMap` + `UiSemantics.TrialRoomPlace` ("columna 2 de 8, sala 3 de 3" and
the standing), `TrialTraps` + `TrapWatch` (dodge warning when a trap's state fires; logs every trap state seen),
`TimedGuide` (the guide from crystal to crystal), `DeathWatch.EndsWithoutDeathScreen`, world-titles names for the
fountains, shrines, map device, relic altar, pledge, offer, Keth Forge, Blood Chalice, Sand Hourglass and Death Crystal
(with its objective), `Switches` for `*Lever*`/`*Switch*` chests.

New this session (commit "Name the trials' icons and counters from the game's tables"):
- `tools/Poe2UiExtract trial-data` → `catalog/trials.json`: icon art → key for 192 effects, 34 modifier families and 16
  room types; per language the names and descriptions of effects, modifiers, room types and ascendancy classes, plus
  the HUD formats `label:round`, `label:crystals`, `label:modifiers`, `label:map`. Columns found again near the measured
  offsets by content (`TrialDataExtractor`); launcher step "trial data" v1 (optional).
- `TrialCatalog` (loads it; `PatternOf` turns "Round <ultimatumnumber>{{{0}/{1}}}" into a pattern) +
  `UiSemantics.TrialIcon`: an icon-only element whose art the catalog knows reads "Desventaja: Garras afiladas. Los
  monstruos infligen un 30% más de daño" (hooked beside the quest-reward art rule).
- `ScreenNarrator.CheckTrialLines` + `TrialLines`/`CounterWatch`: the crystals line said (the game's own text) every
  time it falls, the Chaos round said on change ("Ronda 3 de 10"); setting `AnnounceTrialProgress`.
- `TrialDoors`/`OpenDoorWatch`/`TrialDoorWatch`: a room door whose state turns `open` is said with the way to it
  ("Puerta de la sala abierta: 12 pasos, arriba") or, with `GuideTrialDoors` and the guide free, locked as the beacon's
  target; the first door's states are logged once ("Trial room door seen: open=…, wall=…").

### 1.4 Gaps
- Afflictions/boons gained: no announcement yet, and nothing said the user's on 2026-11-12 (they knew it only by its
  effect, the map's rooms hidden). The HUD's slots give the kind alone (§1.2), so a new slot can be said as "minor
  affliction gained" at once; its name needs the list behind R3 + D-pad left, a window not dumped yet (is it read by the
  generic reader? does it exist hidden while closed, so the name can be read without opening it?).
- Map links: not in the UI tree (§1.2), so they need the map's own data in memory, a layout not known; a picture of an
  open map beside its room buttons' rectangles would be the ground truth to find it by. Until then the standing by
  column, with since 2026-11-12 the player's own room ("tu sala", by `ValidPathGlow`) and "sin camino desde tu sala" for
  the rooms beside the one to enter (they were "aún no puedes entrar", as if they opened later). With no room to
  enter (the map opened before the room is done) every room still reads "aún no puedes entrar": is the glow drawn then?
- The map's counters are bare numbers behind icons, which the screen reading drops, and the map hides the HUD the vitals
  read them from: since 2026-11-12 each is named by the icon before it (`UiSemantics.TrialCounterKind`,
  `ScreenNarrator.TrialMapCounters`), in the screen reading and in the vitals while the map is open. Not heard yet.
- The map's three text-less buttons (leave the Trial, the relics' effects, the tutorial): the pad reaches them (user
  2026-11-12) and leaving works. Both of the first two were read as "Estás viendo la prueba de las sekhemas del dueño del
  mapa, botón, …": a text of the row the game hides from the map's owner, taken as their label (`FindRowLabel` now skips
  a hidden text, so the tooltip's title names them: "Salir de las pruebas", "Efectos de reliquias:"). The tutorial read
  "columna 1, fila 1" between two of its paragraphs (no place is said now for an icon titled by a paragraph). Not heard yet.
- Honour per hit: nothing says what drained it; the honour buffs above could be named as they begin (they are harmful
  buffs: `HarmfulBuffs` may already say them, LIKELY).
- Relic altar and locker: unseen windows (dumps 10:41/10:52 exist); the relic grid is probably an inventory-like grid
  (`InventoryReader`), the slots buttons.
- Run summary: an unseen window; a text-heavy panel, the generic reader should do.
- Trap timing: `TrapWatch` warns at 400 world units on the state names from the `.ot`; the real lead time is unknown.
- The "(+N)" water and what the Offer defers: UNSURE.

Settled on 2026-11-12 (evening, from the game's files and the review of the day's code; none heard in a run):
- **The rewards with no sound** (shrines, fountains, key plinths, the forge, the pledge): they draw icons of their own, not the base
  reward's, and were silent league devices; they sound as quest objects now ([minimap](minimap.md), "The Trial's rewards draw icons of their own").
- **What an object is for**: the game's box has a body under the title (`body_text`, a ClientStrings key): "Derrota a todos los monstruos
  raros del área para llenar el cáliz", "Restaura {0} de honor / Obtienes una desventaja menor aleatoria", "Convierte el agua sagrada que te
  sobra en llaves". Said once an area with the button ([proximity](../proximity.md)). The `{0}` is an amount the game fills in (LIKELY a
  percentage: "Restaure {0} de votre Honneur"), not found in the files: said as "algo". A shrine's cost in sacred water is its shared state
  `interact_cost` (5 in the `.ot`), readable at run time and not said yet.
- **"Honor recuperado" at the start of a run** after one left with honour to spare: the watch starts over in each area (`HonourWatch.Reset`).
- **An affliction or boon said gained again** when its slot read empty for one reading: it is lost only after four readings without it
  (`TrialEffectNews`).

### 1.5 The floor in memory: links, the rooms taken, what the run carries (MEASURED live 2026-11-12, 0.5.5f)
Read-only, on the user's open map (floor 1, columns of 1, 3, 3, 4, 5, 4, 6, 1 rooms), against a picture of the map taken
without focusing the game (`tools/research/trialfloor/bgshot.py`, PrintWindow): 45 links of 45. The decoder with every
check is `tools/research/trialfloor/trialfloor.py`. This solves the first two gaps of §1.4, which stay as the history.
- **The floor object.** The window's content element (the room buttons' fifth ancestor: room, column, columns' container,
  the element holding the canvas and the container, one more, then it) holds its pointer at `+0x3A0`; the HUD's row
  (the honour line's parent) holds the same pointer at `+0x330`. Both were read and agree.
- **Links** (`+0x1F8`): a std::vector of columns of 0x20 bytes, each a std::vector of rooms of 0x38 bytes; a room is a
  small byte vector (begin `+0x0`, end `+0x8`, its three bytes inline at `+0x18`), each byte the row it leads to in the
  next column, ascending. The boss's room has none. The same links, with a room list of the floor's start, stand 0x48
  lower (`+0x1B0`): **that block holds more than the screen shows** (rooms hidden since) and is never to be read.
- **The rooms taken** (`+0x230`): a byte a column, the row taken, `0xFF` from the first column not reached
  (`00 01 02 03 FF FF FF FF` on the user's walk). `+0x238` read 4, LIKELY the next column.
- **What the map shows of each room** (`+0x210`): a std::vector of 0x40-byte entries, one a room that has an icon now
  (column `+0x0`, row `+0x1`, keys to its encounter `+0x8` and reward `+0x18`, both rows of SanctumRooms, to its
  affliction `+0x28`, visited `+0x39`); a room under "Red Smoke" has a null encounter. Not read by the plugin: the
  tooltip says the same.
- **How each link is drawn**: the canvas element holds at `+0x2C8` a std::vector of one 0x18-byte segment a link, in the
  links' order (four floats, then a u32 state at `+0x10`): 0 red, 2 gold (a way still open from the player's room), 3
  taken. The plugin computes the same from the links and the rooms taken (`TrialMap.View`), so it does not read it.
- **What the run carries**: each of the five slots beside the counters (HUD row, and again in the map window) holds at
  `+0x2F0` a std::vector of 0x10-byte keys, each a row of SanctumPersistentEffects then its table; the row's first field
  points to its Id ("AfflictionHideRoomEncounter" = "Red Smoke", "Niebla roja"). A slot is drawn while its vector holds
  something; an empty slot's vector is null. `+0x2D0` read the kind's row plus one (LIKELY). A scan of the whole process
  for that row found no other list. The relics' effects were not found in any of these objects (their text is the
  tooltip of the map's button).
- **In the plugin** (`UiSemantics.TrialMap.cs`, offsets in `GameLayout.TrialFloor`): nothing is believed alone. The links
  must fit the map on screen (`TrialMap.Fits`: as many columns and rooms as the buttons, every target a room of the next
  column, every room led to); the rooms taken must be a walk along them (`TrialMap.Walk`); an effect's Id must be one
  `trials.json` holds. A reading that fails falls back to the standing by column, and a log line says what was read
  ("Trial floor: …"). Each room then says how it stands ("tu sala", "en tu camino", "sin camino desde tu sala"), the
  rooms to enter it is reached through when there is a choice, and the rooms it leads to; the vitals and the map's
  screen reading name what the run carries, and a gain is said as it comes. Not heard in a game yet.
- **After a patch** (`TrialFloorLocator`, 2026-11-12; proven on a memory built in the tests, `TrialFloorLocatorTests`, not on
  a patched game). The links are drawn on a canvas, no element of the window, so memory is their only source; the offsets
  above are where the search starts, not what it depends on. When the links are not where they were measured, they are
  found by shape, once a map: the map on screen says how many columns there are and how many rooms each holds, so a vector
  of that many columns, each a vector of that many rooms, whose ways fit the map is the floor wherever it stands, and a
  column's and a room's sizes are those vectors' spans over the counts on screen. First with the floor as measured and only
  its pointer moved on the element (0x800 bytes of each ancestor are searched; this reads the shown block alone), then by
  shape alone over 0x600 bytes of each floor candidate: there the generated block (`+0x1B0` today) fits too, so the links
  are believed only when every block that fits says the same, and the later one is kept. What is found is used for the
  session and the log says it ("Trial floor: found by shape, GameLayout.TrialFloor is out of date: holder +0x…"): copy it
  into `GameLayout.TrialFloor`. The rooms taken are kept as their distance after the columns (`+0x38`); when they are no
  walk along the links, the player's room is the one the map marks (the glow of §1.2) in the column before the first to
  enter. What a run carries is found the same way when a slot the game draws reads as empty: the one vector on the slot
  whose every key names an Id of `trials.json` ("Trial effects: found by their Ids…").
- **First run heard (user's log, 2026-11-12 17:03, v2026.11.12-3, same floor as the morning's, no patch between).** The
  measured place did not read from C#: the search by shape found the links at holder `+0x3B0`, columns `+0x198` (same
  sizes; the rooms taken 0x38 after them, `000102030402FFFF`, a walk), the 45 links of the morning, and the map said "en
  tu camino" and "sin camino desde tu sala". Thirty seconds and one area later, the map opened to choose a room read
  nothing ("no links fit"): the place found had replaced the measured one and the map was not searched twice. On the next
  floor (17:14 to 17:26, columns 1,3,3,3,5,4,6,1) the search found the measured place again (`+0x3A0`, `+0x1F8`) and it
  read on five maps running, each walk one room longer (`00FF…`, `0002FF…`, … `0002020100FF…`): both places are real. Why
  they differ is NOT known (LIKELY two views of the floor, one a window; the morning's measure was taken with rooms to
  enter on screen). Since then every place that ever read is kept and tried, the last that read first
  (`trialFloorShapes`), and a map that reads nothing is searched again every two seconds, not once.
- **The boss's room shows no tooltip** (same log, 17:06): with the room before it done it was the one room to enter, the
  game put the cursor on it and A entered it, yet no line of it offers to enter, so `TrialRoomIsOpen` is false and, with
  the links unread, every room of the map was "aún no puedes entrar". Now, without links: the player's room is the one
  the game marks (the only room of the map with the glow, one of 27 on the morning's map), the rooms of the next column
  are "columna siguiente" unless one offers to enter (`TrialRoomStanding.Next`), "aún no puedes entrar" is kept for rooms
  two columns on or more, and nothing is said of a room's standing when nothing on screen says where the player is. With
  links, the boss's room is "en tu camino". Whether a room without a tooltip can be entered now is on no element found.
- **The guide to the room door outlived the door** (same log, 17:24, a "find the exit" room). `TrialDoorWatch` put the
  guide on the door the chosen room opened, nine steps away; two steps short of it the way was cut (the Trial shuts it
  behind the player) and no arrival was said, so the guide stayed on through the whole room, leading "as close as it
  goes" to the reachable cell nearest, in a straight line, a door already passed: up through the spikes and two laser
  turrets (burning twice), to a lever by chance, and with its way turned round each time a `SanctumLogicDoor` opened.
  Now a guide the door watch starts ends when its way, once whole, is found cut by two searches running (`ShutWay`,
  `Beacon.Lock(endsWhenShut)`): "El camino a puerta de la sala se cerró; la guía se detiene". **Gap: nothing guides in a
  "find the exit" room.** Its exit is not a target the scanner offers (the next room's stairs read "a 283 pasos, el
  camino se corta antes"), a lever (`SanctumGenericLever`, `Switches`) is not tied to the `SanctumLogicDoor` it opens,
  and nothing knows a lever already pulled. To be found in the game's files before any guide is written (rule 26).
- **A room of crystals with no crystal** (same run, 17:35; the user asked whether an affliction hid them: none of
  `trials.json` names the crystals). A Death Crystal (`…/Hazards/HourglassLethal`) was listed only while its objective
  ("Desactiva los cristales antes de que se agote el tiempo") stood among the tracker's texts, so that crystals of other
  rooms are not called out. The tracker shows two quests a page: here the page held "Ascenso al poder" and "El rastro de
  la corrupción", the Trial's quest was on another, and no crystal was listed, sounded or guided to while the clock ran
  ("6 cristales de la muerte restantes", "00:19"). Now the count of crystals left on the HUD
  (`ScreenNarrator.CrystalsCounted`, the line `CheckTrialLines` already reads) is proof enough of the room
  (`EntityCatalog.CrystalsCounted`). The count appears when the clock starts, a few seconds after the objective does.
- **The lines as a second source** (`TrialMapLines`, 2026-11-12; proven on the morning's 45 lines, which give the 45
  links, not on a running game). Each line ends short of its two rooms' centres by the same distance (64 units on that
  map), so the lines of a pair of columns share their middle and, between columns a fixed distance apart, a line's width
  falls as it steepens by twice that distance: the cut comes out of the lines alone, then each end's centre, and a
  column's centres top to bottom are its rooms. No button's position is needed, nor where or how large the map is drawn.
  The canvas and its vector are found by shape like the floor (`+0x2C8`, 0x18 bytes, is where the search starts; the
  elements searched are the rooms' ancestors and the children of the two above the container). They are read when the
  floor is not found, to say the links anyway (the player's room is then the one the map marks), and to tell which floor
  is the one shown when two that fit differ; with the floor read, the log says once a floor whether the lines agree
  ("Trial lines: the lines the map draws say the floor's links"), so each source proves the other.

### 1.6 The "find the exit" rooms: the exit, the levers and the doors (game files, 2026-11-12; nothing built on it yet)

From the 277 terrain files of the Trial (`metadata/terrain/gallows/leagues/sanctum/`, the rooms' `.arm`) and its tables. Scripts and
tables: `tools/research/gamedata/trial-rooms/` (`armlinks.py` parses a room's objects and links, `rules2.py` checks the colour rule,
`find_the_exit_rooms.tsv` and `wiring_by_room.txt` are their output for 0.5.5f).

- **The rooms** (MEASURED): the objective "Find the Exit" is QuestStates row 661; its flag `Sanctum-PuzzleEntered` is the enter flag of the
  room types Maze, Gauntlet and Puzzle, and only Gauntlet has rooms: 28, seven a floor (Caverns, Ruins, Depths, Abyss).
- **The exit** (MEASURED): one `Metadata/Terrain/Leagues/Sanctum/Objects/SanctumProxGoal` a room, at the room's only opening, on its east
  side. It is `server_only` and `no_serialise`: the client never has it. The player arrives at a point named "telepad", 268 to 864 cells
  from it. On floors 2 to 4 every room has one `SanctumAirlockBlocker` 0 to 23 cells before the goal, which the client has: blocked at
  the start, state `sanctum_completed` 0 then 1. On floor 1 there is none: the exit is the opening toward the reward room's
  `SanctumMapDevice`, which lies 215 cells past the goal on the same line (INFERRED from lining the two rooms' openings up, 28 of 28).
  `SanctumRoomSelectorDoor` is no exit: it stands before each way out of a reward room.
- **Lever to door** (MEASURED): after its objects a room's `.arm` lists links, `<from> <to> "<label>"`, by the objects' order. Always
  lever, then indicator (`SanctumLogicIndicator`), then door (`SanctumLogicDoor`): a door has exactly two indicators, 9 to 16 cells either
  side of it (the third nearest is over 20 away, 78 doors of 78), and a lever one of its own 7 to 13 cells from it. All of a lever's
  indicators draw the same pillar (`KethUnlockPillar01` to `04.ao`) and a room's levers draw different ones: the levers whose own
  indicator matches a door's two are the door's, right for 78 doors of 78. 52 levers, 78 doors, 214 indicators in all.
- **What else a lever does** (MEASURED): 13 start an ambush (`SanctumGenericAmbushTrigger`, never sent to the client), others turn
  turrets, beetle spawners and lasers. A pressure plate never opens a door: it is a trap (gas, darts, beetles).
- **How a door opens** (INFERRED, the logic runs on the server): a lever turns its indicators over and a door is open while both of its
  are lit. Six doors start open, so a pull shuts them; one room's door needs two levers; the Abyss has levers used more than once.
- **What the client can read**: a lever's `lever_pull` (0, then 1 or 2 and no longer targetable), an indicator's `active`, a door's
  `open`, the blocker's `sanctum_completed`.
- **A guide from this**, to be judged before it is built: in a room whose objective is "Find the Exit", lead to the blocker still shut
  (floor 1: toward the next map device); where the way is cut, to the nearest lever not pulled among those of a shut door on the cut,
  then search again. Never to a plate.
- **Needs one look in a run**: whether an indicator's entity tells its pillar (its animated object), else the room is told by the
  distances between its levers and doors (all 28 differ) and the wiring comes from a table; whether the next room's map device is
  among the entities from inside the room; `lever_pull` 1 against 2; an indicator's `active` when the room does not set it.

## 2. Trial of Chaos (Ultimatum)

### 2.1 Mechanics
- Entry: Act 3 Chimeral Wetlands → `G3_10_Airlock` "The Temple of Chaos" → `G3_10` "The Trial of Chaos" (MEASURED
  areas). The "Temple Offering" altar takes an Inscribed Ultimatum ("Offer an Inscribed Ultimatum to undertake the
  Trialmaster's Challenges"; `UltimatumEntryAltar*` strings, MEASURED). The campaign's first trial needs the quest's
  Ultimatum (UNSURE which item in 0.5).
- Rounds: before each round the player picks one of three modifiers ("Select A Challenge Modifer", MEASURED string);
  `UltimatumModifiers` (122 rows, 0x124; Id +0x0, name +0x49 with tiers I-V, icon +0x51 shared by a family, description
  +0x7F; MEASURED): player debuffs (Reduced Recovery, Lessened Reach, Time Paradox, Damaged Defences, Reduced
  Resistances, Drought...), monster buffs (Prismatic/Chaotic/Deadly/Toxic/Resistant/Shielding Monsters, Volatile Fiends,
  Enraged Bosses...), daemons and turrets (Stormcaller Runes, Impending Doom, Blood Globules, Vaal Omnitect, Pyramid
  Beams, Petrification Statues, Stalking Shade, Burning/Shocking Turrets, Temple Traps, Heart Tethers, Blood Mist),
  wagers. Types: BuffOrDebuff, ProjectileAreaDebuff, AreaDenialDaemon, CentralObjectDaemon, TurretDaemon,
  PlayerAttachedDaemon (`UltimatumModifierTypes`).
- Round kinds `UltimatumEncounterTypes` (7): Slay ("Slay all Monsters"), Survive, Collect ("Implant the Soul Cores"),
  Escort, Sacrifice, Destroy (MEASURED ids and two labels). Rooms: 9 `.arm` under `metadata/terrain/gallows/act3/3_10/rooms/`.
- HUD: "Round {0}/{1}" (rendered "Ronda 3/10", LIKELY), the "Challenge Progress" bar (`UltimatumBarDisplayName`), the
  "Active Modifiers" legend (icons), a timer (`UltimatumTimer` has no label text; its format is UNSURE — if "MM:SS",
  `CountdownWatch` already says it).
- Rewards: a reward chest `UltimatumRewardChest` (icon `UltimatumRewardChestActive`, needs `IsMapOwner`), wave chests
  (`UltimatumWaveRewardChest`), "Take Reward(s)", "Preview Rewards" (MEASURED strings). 0.5.5 (UNSURE, web): a chest
  after each room, leaving allowed at any time, two retries per Inscribed Ultimatum. Points: 4 rooms at level 38 for
  the campaign's pair, 7 at 60, 10 at 75, the Trialmaster for the last (UNSURE, pre-0.5 guides).
- Trialmaster: `Metadata/NPC/League/Ultimatum/UltimatumNPC`, the "Trialmaster's Tower" ("Insert three fates to unlock
  the door"), bosses Bahlak/Uxmal/Chetza drop fates (UNSURE), "Corruption Dais" ("Begin the Corruption Trial").
- Errors: "Only the Trial owner can interact with this", "Leaving this area will abandon the trial..." (MEASURED).

### 2.2 What the mod does / gaps
- Today: traps named for `LeagueUltimatum/Turrets/ProjectileTurret`, `PetrificationStatue`, `Daemon/LaserPyramid`
  (`TrialTraps`, no firing state known); the HUD round said on change (new, `CheckTrialLines`); the modifier icons named
  by art when focused (new, `TrialIcon`; also the "Active Modifiers" legend if its icons take the focus or are read as a
  panel's images — UNSURE).
- Gaps: the choice window (three cards: LIKELY buttons with name + description texts, so the generic reader speaks them;
  if a card is icon + tooltip, `TrialIcon` covers the icon), the encounter kind of the round (its objective line is a
  HUD text appearing → said as HUD news, LIKELY), the timer's format, the Soul Core/escort objects (Collect/Escort rounds
  need the scanner to list the cores and the statue: both are entities; check their Targetable), the reward chest's
  "Take Rewards" flow.

## 3. Ascendancy

- Classes: `ascendancy` (37 rows, 0xA5; Id +0x0 "Warrior1", name +0x2C, flavour +0x34, art +0x44; MEASURED). In 0.5:
  Warrior Titan/Warbringer/Smith of Kitava; Ranger Deadeye/Pathfinder; Huntress Amazon/Spirit Walker/Ritualist; Witch
  Infernalist/Blood Mage/Lich (+ "Abyssal Lich" `Witch3b`); Sorceress Stormweaver/Chronomancer/Disciple of Varashta;
  Mercenary Tactician/Witchhunter/Gemling Legionnaire; Druid Oracle/Shaman; Monk Martial Artist/Invoker/Acolyte of
  Chayula; 13 `[DNT-UNUSED]` placeholders. Names and flavour per language are in `trials.json` (`ascendancy:` keys).
- Flow (MEASURED strings, LIKELY order): after the trial an altar says "Interact to choose your Ascendancy" ("Altar of
  Ascendancy", `LabyrinthAscendancyWindowTitle`) → "Choose your Ascendancy Class" window with Next/Back, "Ascend",
  "Choose Later"; later trials: "Interact to gain Ascendancy Passive Skill Points"; "Change" to switch class (gold cost
  UNSURE; needs a trial of at least the points held, UNSURE). "{0} Ascendancy Points Left" on the tree.
- Tree view: 197 hidden nodes with the Ascendancy FK in the same canvas ([passive-tree.md](passive-tree.md)); toggle
  `ConsoleAscendancyGraph`; counters are hidden siblings of the points label. Not implemented: the hidden-node filter
  and the mode announcement wait for a dump with the view on and off.
- Mod today: the selector window → generic buttons (LIKELY), the class art is per base class (TitanAscendancy.dds for
  all three Warrior rows), so `TrialIcon` cannot tell the classes apart by art; the window's texts must do it.

## 4. Acts 3 and 4

- Areas (MEASURED WorldAreas; names already in `area-names.json`): Act 3 `G3_town` Ziggurat Encampment, `G3_1` Sandswept
  Marsh, `G3_2_1` Infested Barrens, `G3_2_2` Matlan Waterways, `G3_3` Jungle Ruins, `G3_4` Venom Crypts, `G3_5` Chimeral
  Wetlands, `G3_6_1/2` Jiquani's Machinarium/Sanctum, `G3_7` Azak Bog, `G3_8` Drowned City, `G3_9` Molten Vault, `G3_10`,
  `G3_11` Apex of Filth, `G3_12` Temple of Kopec, `G3_14` Utzaal, `G3_15` Library of Kamasa, `G3_16` Aggorat, `G3_17` Black
  Chambers. Act 4 `G4_town` Kingsmarch, `G4_ship` "[DNT] Ship", `G4_1_1` Isle of Kin, `G4_1_2` Volcanic Warrens, `G4_2_1`
  Kedge Bay, `G4_2_2` Journey's End, `G4_3_1` Whakapanu Island, `G4_3_2` Singing Caverns, `G4_4_1` Eye of Hinekora,
  `G4_4_2` Halls of the Dead, `G4_4_3` Trial of the Ancestors, `G4_5_1/2` Abandoned Prison / Solitary Confinement, `G4_7`
  Shrike Island, `G4_8a/b` Arastas, `G4_9_` Isle of Decay, `G4_10` The Excavation, `G4_11_1a/b` Ngakanu, `G4_11_2` Heart
  of the Tribe, `G4_13` Plunder's Point, `G4_14` Brinerot Cove.
- Sailing (UNSURE, web of 0.5.5): Makoru on the ship opens the destination map; Rog no longer charters (0.5.5c "Boat
  Charter"). No ship/voyage table exists in the data; the world map's own tables (`worldmapquicktravelbuttons` 9 rows
  with Sanctum and Ultimatum buttons, `worldscreenmappindialogue` 29 rows, `worldpopupicontypes`) point at the world-map
  panel: Act 4 is its tab (`Act4PanelChildPath {22,0,3}`, GameHelper). LIKELY the existing world-map narration applies;
  "Set Sail" (`A4SetSail`) is the confirm.
- Puzzle objects (MEASURED `.ot`): Matlan `WaterLevelLever*` are Vaal ground switches ("Canal Mechanism",
  `VaalGroundSwitch.ao`, icon `VaalPressurePlateActive`: pressure plates, not levers), `WaterwaysUnlockControl` "Stone
  Altar" (InteractionAction + Transitionable); Jiquani `MachinariumGenerator*` "Generator" (icon `Generator`); Molten
  Vault `FrostCore*`; `SoulCorePlinth` "Stone Altar" (icon `StoneAltarShort`); `BloodAltar`; Utzaal/Aggorat
  `SacrificialHeart{Drop,Place,Destroy}` (Place: Transitionable, needs quest flag `SacrificialHeartHeld`); Black Chambers
  `DoryaniArenaCrystalMechanism` (TriggerableBlockage, nameless); Act 4 `PrisonLever`/`TortureLever`/`PrisonBossDoorLever*`
  ("Lever", icon `LabyrinthLever`), `Jungle_Water_Control_SluiceDam` ("Sluice Gate Lever"). Names reach the mod live
  through the Render name (display_text → ClientStrings `ObjectDisplayText*`, 284 rows: Lever, Switch, Timed Crank,
  Valve, Door, Locked Door, Ancient Seal, Canal Mechanism, Drawbridge, Stone Altar, Vaal Soul Core...), as "Puerta" and
  "Mesa de reforja" did in the hub dump; Targetable ones are scanner Objects. Nameless, untargetable mechanisms (the
  Doryani crystals) are not listed: the same pattern as the Sekhemas doors (a per-object StateMachine watch) would do.
- Act 3 flow (UNSURE, web): Venom Crypts' Servi choice (stun/ailment threshold or mana regen, permanent), Machinarium
  Small Soul Cores into sockets, Sanctum two Medium Cores into generators, the Large Core on the Jungle Ruins altar,
  Matlan plates then the final lever, Azak Bog Ignagduk → Flameskin Ritual, Molten Vault Frost Cores on altars then the
  sluice lever and Mektul under a time limit, Apex of Filth Temple Door Idol, Kopec's indoor sun, Utzaal Sacrificial
  Heart → Aggorat altar → Sacrificial Dagger, Doryani. Act 4: Forgotten Bounty map pieces (Plunder's Point), Abandoned
  Prison Chapel Key, Solitary Confinement ballista + rising water, Hinekora's three Tests of Mettle, Halls of the Dead,
  Arastas bells (Regal vs Exalted), the Excavation's maze, Tavakai at the Heart of the Tribe; then three interludes
  (Curse of Holten, ..., Doryani's Contingency) and the Atlas from the Ziggurat Refuge.

## 5. Quest rewards, quest UI, cutscenes, dialogue choices

- Tables (MEASURED): `quest` 138 (Id, name +0xC, icon +0x14), `queststates` 1491 (texts +0x2C/+0x34, label +0x3D,
  hint +0x59; the tracker's lines), `questrewardoffers` 139, `questrewards` 197 (keys only), `questrewardtype` 38 (name
  and description: GoldReward, ArtificersOrb, BookOfSpecialiation, UncutSkillGem...), `questitems` 221, `questtype` 7.
  Strings: "Select One Reward", "Take Reward", "Are you sure you want to take {0} as your reward?", "Next Quest Reward",
  "Complete \"{0}\" in Act {1} to obtain this reward:".
- Mod today: the NPC reward window and the quest list's reward icons (`QuestRewards` by art, 13 icons = all that
  exist), the tracker page (`QuestTrackerPage`), dialogue options one by one (`UiSemantics.OptionLists`), cinematic
  subtitles ([cinematic-subtitles.md](cinematic-subtitles.md)). Choices with consequences (Servi, the Arastas bells,
  Halls of the Dead) are ordinary option lists (LIKELY).
- Quest items: their use is an interaction prompt on the object (Transitionable + quest flag), read as the interact
  target; the inventory tooltip's `ItemFunction*` line says where ("Trial of the Sekhemas" for the Barya, "Temple of
  Chaos" for the Ultimatum) — already read as tooltip text (LIKELY).

## 6. Plan (confidence)

| Item | State | Confidence |
|---|---|---|
| Crystals left said as they fall (game's line) | done (code, tested) | PROVEN need; the line's text MEASURED |
| Chaos round said on change | done (code, tested) | LIKELY (format MEASURED, rendering unseen) |
| Room door opened → said / guided | done (code, tested) | LIKELY (states from the `.ot`; which value means open unseen) |
| Trial icons named by art (afflictions, boons, modifiers, room types) | done (code, tested) | LIKELY (icon elements must expose their texture key, as reward icons do) |
| Affliction/boon gained announcement (diff the HUD icons) | todo after a HUD dump | LIKELY |
| Hidden ascendancy nodes filter + "Ascendancy tree" mode | todo after a dump | LIKELY |
| Relic altar / locker / run summary readers | check with the generic readers first | UNSURE |
| Chaos choice window, timer format, Soul Core / statue entities | check live | UNSURE |
| Act 4 destination map | check the world-map narration live | LIKELY |
| Nameless mechanisms (Doryani crystals) as scanner entries | todo if a puzzle needs them | UNSURE |

## 7. Live protocols

Each: where, what to do, what `accessibility.log` ("Speech") must show. Take an F6 dump (`dump`) at the marked points.

1. **Crystals and the clock** (Sekhemas, an Escape room): on entering, the differ says the HUD line once; disable a
   crystal → the log must show "5 cristales de la muerte restantes" → "4 cristales..." urged at each fall, the clock's
   marks as before, and `GuideTimed` moving the beacon. If nothing: `grep "CheckTrialLines\|label:crystals"`; the
   pattern comes from `trials.json` `es.label:crystals`.
2. **Room doors** (any room's end, standing by the Trial Map): open the map, choose a room, close it → within 0.5 s
   "Guiando hacia puerta de la sala, N pasos, ..." (guide free) or "Puerta de la sala abierta: ..." and one log line
   "Trial room door seen: open=…, wall=…". If the chosen door is not the one said, the open value is inverted: swap
   `TrialDoors.IsOpen` to the `wall` state and record the values here.
3. **Trial icons**: with an affliction gained (an afflicted room), focus the HUD icons if the focus reaches them, else
   `dump` and find under `SanctumHUDDisplay` the elements with `str@…="Art/2DArt/UIImages/InGame/Sanctum/BoonCurseIcons/…"`;
   note the parent's name for the diff announcement (gap 1.4). Expected speech on focus: "Desventaja: <name>. <description>" (the Spanish client's word for an affliction; a boon is "ventaja").
4. **Chaos**: Act 3 Chimeral Wetlands, Temple of Chaos with an Inscribed Ultimatum: `dump` the choice window (three
   cards: are they buttons with texts? icon + tooltip?), accept; the log must show "Ronda 1 de 10" only from round 2 on
   ("Ronda 2 de 10", interrupting); note the timer's rendered form (if not "MM:SS", extend `CountdownWatch`); `dump` the
   HUD mid-round (legend icons' art) and the reward chest window; `entities` dump in a Collect round (Soul Cores'
   paths and Targetable).
5. **Ascendancy**: after the trial, at the altar: expected "Altar de la Ascendencia ... Interactúa para elegir" as the
   interact target; open the selector, `dump`; arrow through the classes: the name must be spoken from the window's
   texts; "Ascend" confirms. Then the passive tree: toggle the ascendancy view, `dump` on and off (the 197 hidden nodes'
   visible bit, the counters).
6. **Act 4 sailing**: Kingsmarch, Makoru: `dump` before and after the destination map opens; expected the world-map
   narration (area under the cursor, "Viajar"/"Zarpar"); if it is a dialogue list, the option reader.
7. **Campaign mechanisms** (Act 3 Matlan, Jiquani, Molten Vault): with the scanner, each plate/altar/generator must be
   listed by its game name ("Mecanismo del canal", "Altar de piedra", "Generador"); interact prompts read; a mechanism
   the scanner never lists → `entities` dump beside it (components, Targetable) for a state watch like the doors'.
