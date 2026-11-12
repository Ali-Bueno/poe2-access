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
- The map's three text-less buttons (leave the Trial, the relics' effects, the tutorial): to check whether the pad
  reaches them and what is said there; the relics' effects are worth a line on opening the map or in the vitals.
- Honour per hit: nothing says what drained it; the honour buffs above could be named as they begin (they are harmful
  buffs: `HarmfulBuffs` may already say them, LIKELY).
- Relic altar and locker: unseen windows (dumps 10:41/10:52 exist); the relic grid is probably an inventory-like grid
  (`InventoryReader`), the slots buttons.
- Run summary: an unseen window; a text-heavy panel, the generic reader should do.
- Trap timing: `TrapWatch` warns at 400 world units on the state names from the `.ot`; the real lead time is unknown.
- The "(+N)" water and what the Offer defers: UNSURE.

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
