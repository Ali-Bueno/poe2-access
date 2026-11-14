# League and side mechanics from the game's files (offline research, 2026-11-14)

Scope: what the mod needs so a blind player can find, start, follow and finish the league and side mechanics met in maps and
in the campaign, the way the Trial of Chaos was done ([trial-of-chaos.md](trial-of-chaos.md)). No character of ours has met
most of them, so everything comes from the files of the bundle index in the install on 2026-11-14 (the one the Trial of Chaos
notes used). Confidence: **MEASURED** (seen in a file: table and row, or a `.ot`), **LIKELY** (consistent with the files, not seen
running), **UNSURE** (a guess or not in the files). One web search gave context only; such lines say "(web)".

Sources: the tables under `data/balance/` and `data/balance/spanish/` read with `tools/research/gamedata/dat.py` (from a caller
that points `dat.S` at a scratch dump); every `metadata/**/*.ot` (8,325 files); `KeywordPopups` (the game's own glossary: a
term and its definition, en / es) for what each mechanic is. Bosses are left to [boss-mechanics.md](boss-mechanics.md).

What the plugin already does is not repeated: a non-monster object with a league or objective minimap icon is listed under
"Encounters" (`EntityCatalog.LeagueDeviceIcon`, [endgame.md](endgame.md) §3). Section 0 says which objects that rule misses.

## 0. What the game ships

| Mechanic | League (en) | Folders (`.ot` count) | Tables |
|---|---|---|---|
| Expedition, rework "Runes of Aldur" (0.5.0) | `LeagueStandardSSFExpedition2` "SSF Runes of Aldur" (ClientStrings 9189, MEASURED) | `MiscellaneousObjects/Expedition/` (21), `.../Expedition2/` (13), `Terrain/Leagues/Expedition/` (11), `Terrain/Gallows/Leagues/Expedition/` (118: logbook areas, boss sub-areas), `Monsters/LeagueExpedition[New]/` (134) | `Expedition*` (14), `Expedition2*` (13: Recipes 322 rows, Runes 34, RemnantRewardPerArea 25) |
| Delirium (+ Simulacrum) | - | `Terrain/Gallows/Leagues/Delirium/` (49), `MiscellaneousObjects/Delirium/` (15), `Monsters/LeagueDelirium/` (66), `Monsters/LeagueAffliction/` (8) | `Affliction*` (13) |
| Abyss | - | `MiscellaneousObjects/Abyss/` (53), `Chests/Abyss/` (6), `Terrain/Gallows/Leagues/Abyss/` (29: Well of Souls, depths, pinnacle), `Monsters/LeagueAbyss/` (54) | `Abyss*` (5) |
| Breach (+ Breach Hives) | - | `MiscellaneousObjects/Breach/` (4), `.../Brequel/` (22), `Terrain/Leagues/Breach/` (12), `Chests/Breach/` (5) | `Breach*` (6) |
| Azmeri spirits | - | `Monsters/TormentedSpirits/` (37), `Monsters/LeagueAzmeri/` (3), `Chests/LeagueAzmeri/` (3), `MiscellaneousObjects/Azmeri/` (3) | `TormentSpirits` (22), `AzmeriFeatureRooms` |
| Ritual, rework "Forbidden Rites" (0.5.5) | `LeagueStandardSSFRitualGroups` "SSF Forbidden Rites" (ClientStrings 9618, MEASURED) | `Terrain/Leagues/Ritual/` (6), `Monsters/LeagueRitual/` (41) | `RitualWorldAreaGroups` (41 campaign areas), `RitualAreaCompletionFlags` (38) |
| Incursion / Vaal temple, Strongboxes, Shrines, Rogue exiles, Essence | §6-§10 | | |

"Fate of the Vaal" has no league string in ClientStrings (MEASURED: no match); the `Incursion2*` tables are its temple (LIKELY).
Leftovers of PoE1 leagues (Hellscape = Scourge/Crucible, Heist, Delve, Blight, Legion, Betrayal; `hellscape*` tables have 0 rows)
are out of scope.

**Map objectives and their icons** (`EndgameMapObjectives` +0x20 → MinimapIcons, MEASURED): Breach `Breach`, Expedition
`ExpeditionDetonator`, Delirium `AfflictionInitiator`, Ritual `RitualRune`, Abyss `AbyssPitActive`, Shrines `Shrine`, Strongboxes
`Strongbox`, Essences `Essence`, RogueExiles `RogueExile`, AzmeriSpirits `SpiritActivated`, StoneCircles `RuneMarkersActive`, Incursion
`IncursionArchitectUpgrade`. **No icon**: `Expedition2` "Collect the Verisium Remnants", `Breach2` "Complete all Breach Hives", `AbyssDepths`.

**What today's rule misses** (path has no `/Leagues/`, icon is not an objective's, or the thing is a monster): the Verisium
Remnant (§1), Expedition markers, charges and remnant relics (§1), Delirium mirror shards and the Grand Mirror (§2), Abyss cracks
and Abyss chests' icons (§3; the chests are listed as chests), Breach Hive objects except the stabiliser (§4), the Azmeri spirit
(§5: under `Metadata/Monsters/`).

**What the host cannot read yet** (host `GameReader/RemoteObjects/Components/`, MEASURED): there is no `ControlZone` component
(Breach's ring, §4); `Transitionable` reads only its header, although `TransitionableOffsets.CurrentStateEnum` (+0x120) is declared
(Abyss cracks and pits, §3, are Transitionable-only); the `StateMachine` reader lists `(name, value)` pairs, and whether the
client-only states (`define_state` in a `client` block) are among them is UNSURE (Expedition's `in_placing_range`, §1.2).
`PlayerStats.Of(seat, name)` reads player stats by name (Delirium's fog stat, §2) and `Mods` reads monster mods (Azmeri, §5).

## 1. Expedition (Runes of Aldur)

Two forms (KeywordPopups 345-347, MEASURED): an **Expedition** (detonator, a chain of charges, buried markers) in maps and the
larger **Grand Expedition** (Ocean biome nodes revealed with Logbooks, `GrandExpedition` "Place Explosives & Activate the Detonator" /
"Colocar explosivos y activar el detonador"); and the **Verisium Remnant** of the 0.5 league, alone in campaign areas
(`Expedition2RemnantRewardPerArea`: 25 areas, `G1_4` … `G4_11_1b`, each with a guaranteed reward and a quest flag) and in maps
(stat `map_spawn_expedition2_encounter`). Since 0.5.4 the map Expedition is no longer a side encounter (web, LIKELY).

### 1.1 Objects, in the order met (MEASURED `.ot` unless said)

| Object (path under `Metadata/`) | Components and states | Name (en / es) | Icon | Listed today |
|---|---|---|---|---|
| Detonator `MiscellaneousObjects/Expedition/ExpeditionDetonator` (+`...TreasureIsland`; boss sub-areas `Terrain/Gallows/Leagues/Expedition/SubArea_*_Boss/Objects/ExpeditionDetonator_BossEntrance`) | Targetable, InteractionAction; `explosives_placed` 0 = title "Detonator", 1 = "Detonate Explosives" (red, pulsing: all charges placed); `activated` 1 = detonated (untargetable, sound `DetonateExpedition`); `light_colour` 0/1 orange, 2 red (meaning UNSURE) | `ExpeditionDetonatorWorldDescription` "Detonator" / "Detonador"; ready: "Detonate Explosives" / "Detonar explosivos" | `ExpeditionDetonator` | yes, by world title |
| Buried marker `MiscellaneousObjects/Expedition/ExpeditionMarker` (extends `ExpeditionHighlightable`) | empty MinimapIcon and art: both set by the server from `ExpeditionMarkersCommon` (14 kinds: RegularMonster, EliteMonster, Armour/Currency/Gem/League/Map/Trinket/Unique/WeaponChest, GenericChest1-3, TreasureDestinationChest); client states `in_placing_range`, `in_range` (§1.2) | none | one of `ExpeditionRegularMonster`, `ExpeditionEliteMonster`, `ExpeditionLeagueSpecificMonster`, `ExpeditionBoss`, `ExpeditionChest1/2/3` (MinimapIcons 578-584; which kind gets which: UNSURE) | no |
| Verisium Remnant `MiscellaneousObjects/Expedition2/Expedition2Encounter` (campaign G1_2: `...Expedition2EncounterQuest`) | blocking, Life + Stats (cannot be stunned, pinned…), Targetable with `gamepad_priortise_interact_over_skills`; interaction distance 50 (console 18); `activated` 0-8 (§1.4), `sockets` (rune slots: reward tier at ≥4, ≥6), `is_rerolled`; client `disable_expedition_highlight` | KeywordPopups `ContainsExpedition2` "Verisium Remnant" / "Remanente de verisium" (no object name) | `Expedition2RemnantActive` → `Expedition2RemnantDeactivated` at 7/8 | no |
| Logbook relic `MiscellaneousObjects/Expedition/ExpeditionRelic`, `Terrain/Leagues/Expedition/Objects/ExpeditionBeastSkin`, Logbook remnants (`ExpeditionRelics`, 14 rows: "Ezomyte Runestone", "Dormant Burrower"…) | `expedition_relic`, `encounter_started`, `exploded`, `minimap_icon` 1/2; Beast Skin has a WorldDescription | Beast Skin: `ExpeditionBeastSkinLabel` "Beast Skin" | `ExpeditionPOI` | Beast Skin yes (`/Leagues/` path, world title); the relic no |
| Charge being placed `MiscellaneousObjects/Expedition/ExpeditionPlacementIndicator` | client-only object (no server part, LIKELY); `blocked` 0 = can be placed (`marker.ao`), 1 = cannot (`marker_fail.ao`) | - | - | no |
| Placed charge `MiscellaneousObjects/Expedition/ExpeditionExplosive`; fuse beam `ExpeditionExplosiveFuse` | `activated` 1 = blown; `light_colour`; fuse `activated` 1 = lit | - | `ExpeditionDynamite` | no |
| Excavated monsters, base `Monsters/LeagueExpeditionNew/ExpeditionMonsterEmerge` (factions under `.../Expedition2/AntFaction/`, `ArachnidFaction/`, `CrustaceanFaction/`, `HumanoidFaction/`, `Fodder/`) | `emerge` 1/2 = still buried (not rendered), `effect` 1/2 = breaks out | MonsterVarieties names | - | enemies |
| Excavated chests `Chests/LeaguesExpedition/*` (+ `Chests/StrongBoxes/*StrongboxExpedition`), shatter props `Terrain/Leagues/Expedition/Objects/ExpeditionShatterDoodad` (a Chest), `ExpeditionMapChest`, `ExpeditionEncasedMonster` | chests: Chest; props: `expedition_detonated` 1 = blown open | "Excavated Chest" / "Cofre excavado" (Chests, 80 rows with Expedition in the id) | - | as chests |
| Cavern entrance `MiscellaneousObjects/Expedition2/Expedition2BlackKnightEntrance`; geode `...Expedition2BlackKnightVerisiumGeode` | entrance: `expedition_detonated` 1 = cap shattered (TriggerableBlockage); geode: a Chest, Transitionable | geode `StarfallKnightBossVerisiumGeode` "Verisium Geode" / "Geoda de verisium" | `ExpeditionCavernEntrance`; `ExpeditionStarfallKnightVerisiumGeodeActive` | no |
| Verisium Sentry `Monsters/LeagueExpeditionNew/Sentinels/DroneBase` (+`DroneBlue`…) | follows the player, adds Runic modifiers to monsters (KeywordPopups 991); `activated`, `tags_remaining`, `drone_total_lifetime` | "Verisium Sentry" / "Centinela de verisium" | - | no |

### 1.2 Placing charges (the part a sighted player does by eye)

- **Entering placement** (ClientStrings, MEASURED en / es): keyboard `generic_league_key` ("Press N to begin placing explosives");
  controller action `GamepadExpeditionActionTitle` "Place Explosives (Expedition)" / "Colocar explosivos (expediciones)", whose
  binding `gamepad_place_explosive` can be unbound (`GamepadExpeditionHelpStartPlacementModeUnbound` "Bind to place Explosives").
  While placing: banner "Placing Explosives" / "Colocando explosivos", help "Placement Mode" / "Modo de colocación".
- **Moving and dropping a charge** (controller): `GamepadExpeditionMoveExplosive` "Adjust Placement" / "Ajustar colocación" (the
  stick moves the charge, LIKELY), `ConsoleActionPlaceExplosive` "Place Explosive" / "Colocar explosivo", "Undo Placement" /
  "Deshacer ubicación", `GamepadActionStopPlaceExplosive` "Exit Placement Mode" / "Salir del modo de colocación".
- **Limits**: HUD `SideEncounterExpeditionPlacedCount` "Explosives Placed: {0}/{1}" / "Explosivos colocados: {0}/{1}" (count and
  maximum); help `GamepadExpeditionHelpOutOfRange` "Out of range" / "Fuera del alcance" when the charge is beyond reach;
  `GamepadExpeditionHelpPlacedAllExplosives` "Return To Detonator" / "Volver al detonador" when all are placed. Each charge must be
  within reach of the previous charge or the detonator (LIKELY: the fuse beams join them; the PoE1 rule). The numbers move with
  map stats `map_expedition_explosives_+%`, `map_expedition_explosives_+`, `map_expedition_maximum_placement_distance_+%`,
  `map_expedition_explosion_radius_+%`, `map_expedition_only_one_explosive`, `map_expedition_logbook_only_X_explosives` (Stats,
  MEASURED); the base values are LIKELY in `ExpeditionBalancePerLevel` (41 rows, columns unnamed: e.g. +0x09 = 13 at level 53 and 15
  at 83, +0x19 = 135, +0x05 = 22); which column is count, reach or radius is UNSURE.
- **What a charge will blow** (the decisive signal, MEASURED `ExpeditionHighlightable.CheckHighlight`): every marker, relic and
  remnant has client states `in_placing_range` (1 while the charge being placed would blow it: glow `marker_targets.epk`) and
  `in_range` (1 once a placed charge covers it: `marker_targets_placed.epk`); objects with their own blast (`inherent_explosion_radius`,
  e.g. the Oil Derrick "Explodes when detonated / Subsequent explosives have a larger detonation radius") draw that ring.
- Remnant modifiers apply to everything dug up by the charge that destroys the remnant and every later one
  (`ExpeditionRemnantModifiersFunctionText`); "View Remnant Modifiers" / "Ver mods. de remanentes" lists them for the last charge.

### 1.3 The detonation

Interact with the detonator when `explosives_placed` = 1 (`ExpeditionReadyToDetonate` "Detonator Ready" / "Detonador listo") →
`activated` 1 → charges blow (`ExpeditionExplosive.activated` 1, fuses `activated` 1), props in range get `expedition_detonated` 1,
monsters go `emerge` → `effect`, chests appear; help "Explosives Detonated" / "Explosivos detonados" (MEASURED states and strings).
The order along the chain and its timing are not in the client files (UNSURE; LIKELY from the detonator outward).

### 1.4 The Verisium Remnant (`Expedition2Encounter.activated`, the code's own comment, MEASURED)

| Value | Meaning | Targetable |
|---|---|---|
| 0 | (area only) initial encounter ongoing | no |
| 1 | initial encounter done: interact to open the **Runeshape Tome** | yes |
| 2 | (Expedition only) a recipe is chosen, waiting for the detonation; can be re-chosen | yes |
| 3 | (Expedition only) detonation started while a recipe was pending | no |
| 4 | (Expedition only) detonation started without a recipe, bricked (unused) | no |
| 5 | recipe running: waves of Verisium monsters after an animation delay | no |
| 6 | encounter done, reward ready ("Shatter Remnant") | yes |
| 7 | all finished (icon `Expedition2RemnantDeactivated`) | no |
| 8 | (Expedition only) detonated while bricked | no |

The Tome window: "Runeshape Combinations" / "Combinaciones de formas rúnicas", search "Search unlocked Recipes", "Combine Runes" /
"Combinar runas", "Shatter Remnant" / "Romper remanente", undiscovered recipes "Undiscovered" / "Por descubrir"; tutorial steps
"Open the Runeshape Tome" / "Abre el Tomo de runas", "Choose a combination to inscribe" / "Elige una combinación para inscribir",
"Inscribe the Runeshapes" / "Inscribe las formas de runas"; error "You are not a valid owner of this Remnant". Recipes:
`Expedition2Recipes` (322 rows: rune list, 1-10 runes, reward item); runes `Expedition2Runes` (34: Fire … Bait, each a monster mod
`ExpeditionSentinelModRune*` and an enemy health-bar badge). Hidden buff `expedition2_is_near_death_remnant` exists (whether the
remnant can be hurt in its waves: UNSURE).

### 1.5 Dangers with a tell

| Object | Tell | Player buff while in it (en / es) | In `hazards.txt` |
|---|---|---|---|
| `MiscellaneousObjects/Expedition2/Objects/ElementalTornadoes` (an InvisibleFire daemon, moves) | - | `expedition_ele_fire_tornado` "Fire Tornado" / "Tornado de fuego", `_cold_` "Frost Tornado" / "Tornado de escarcha", `_lightning_` "Storm Tornado" / "Tornado de tormenta" | no |
| `.../Objects/TidalRuneGeysers` | `status`; art `storm_indicator.ao` (a warning drawn first, LIKELY) | - | - |
| `.../Objects/RebirthRuneFlower` | `status` 0 emerge, 1 fade out, 2 surge (what the surge does: UNSURE) | - | - |
| rune "protective" | - | `expedition2_protective_rune` "Temporal Bubble" / "Burbuja temporal" (slowed) | no |
| Vaal Forgemaster's `ForgemasterFissureLine[Start/End]`, `ForgemasterVolcano` (MiscObjects 1008-1011) | a rare's ground skill | - | - |

### 1.6 Not in the files

Which marker kind gets which icon; the base charge count, reach and radius (unnamed columns); the blast order and timing; whether
the host's StateMachine reader sees the client states `in_placing_range` / `in_range` and whether the placement indicator is in the
entity list (one entity dump while placing settles both); the Tome window's tree.

## 2. Delirium and the Simulacrum

### 2.1 In a map (MEASURED `.ot` unless said)

| Object (path under `Metadata/`) | Components and states | Name (en / es) | Icon | Listed today |
|---|---|---|---|---|
| Mirror `Terrain/Gallows/Leagues/Delirium/Objects/DeliriumInitiator` | no Targetable: walked through; `interacted` 1 = used (icon hidden) | objective "Activate the Delirium Mirror"; tracker `MissionTrackMapDelirium` "Open the Delirium Mirror." / "Abre el Espejo de delirio." | `AfflictionInitiator` | yes, "Delirio" |
| The fog | **no object** (none of the 8,325 `.ot`); player stat `is_in_affliction_fog` (Stats 10258), monster stat `monster_is_in_affliction_fog` (10259), `affected_by_delirium` (8327) | `MapDeliriumFogBankName` "Delirium Fog" / "Niebla del delirio" | - | - |
| Mirror shards (the bar's events) `MiscellaneousObjects/Delirium/DeliriumShardBoss`, `DeliriumShardBoss2`, `DeliriumMirroredBoss` (orange), `DeliriumShardSeethingChyme` (red: `detonate` 1 opens a portal), `DeliriumShardEliteUpgrade` (purple), `DeliriumShardPauseFog`, `DeliriumAddDeliModsToRares`, `DeliriumBossSpawnsGrandMirror` (teal) | `visible` 1 = rises (reveals the minimap 80 around), `detonate` 1 = shattered (used); server only; no client Targetable (PauseFog has an InteractionAction): how one is set off is UNSURE | no name (the HUD shows icons only, `AfflictionSpecialEventVisuals` 9 rows: MapBoss, BossOmniphobia, BossKosis, PortalSeethingChyme, SpawnEliteUpgrade, MirroredBoss, PauseFog, AddDeliModsToRares, BossSpawnsGrandMirror) | `DeliriumShard{Orange,Red,Purple,Teal}Active` (+`…Deactive`, MinimapIcons 1000-1007, 1027-1028) | no |
| Grand Mirror `MiscellaneousObjects/Delirium/DeliriumGigaMirror` (+ `TangmazuGigaMirror`) | `enabled` 1 = targetable; `active`, `shatter`, `targetable` | `MapDeliriumGigaMirrorName` "Grand Mirror" / "Espejo grande"; objective `MapObjectiveGigaMirror` "Investigate the Grand Mirror" / "Investiga el Espejo grande" | `GrandMirrorActive` / `GrandMirrorDeactive` | no |

How the encounter moves (KeywordPopups 344, MEASURED text): the fog ring rolls across the area from the mirror; the player must stay
inside it or the encounter ends; rares/uniques in it may manifest demons; shards appear at set depths. `AfflictionConstants`
(MEASURED): `FogBankInitialDepth` 10, `BaseFogBankMaxDepth` 100, shards 30-100 from the fog's place (`SpecialEventShardSpawnDonut*`),
at least 2 events 100 apart, Deliriousness +1% per rare, +3% per unique, +10% per map completed. The reward bars of PoE1 are gone
(`AfflictionRewardTypeVisuals` has one row, "Delirium"). End early: action `GamepadDeliriumActionTitle` "End Delirium" / "Finalizar
delirio" (`<<gamepad_end_delirium>>`, can be unbound), `DeliriumFastForwardButtomText` "Select to end the Delirium Encounter ({0})".
Debuffs from fog monsters' hits end "when you stop being Delirious": `affliction_debuff_*` "Wasting / Paralysing / Eroding / Diluting
Touch" ("Toque debilitador / paralizador / erosionador / diluidor").

### 2.2 The Simulacrum

Area `Delirium_Act1Town` (+`_Quest`) "Simulacrum of Delusion" / "Simulacro de alucinación" (WorldAreas 406-407); a map node opens
at 100% Deliriousness (`DepthToUnlockSimulacrum` 100; else `MapSimulacrumNotEnoughFogDepth`); a failed one becomes "Fading
Simulacrum". **7 waves** (`AfflictionConstants.SimulacrumWaves`, MEASURED).

- Controller `Terrain/Gallows/Leagues/Delirium/Objects/DeliriumSimulacrumObject` (icon `AfflictionInitiator`, so listed today as
  "Delirio"): `active` 0 = idle, shown and targetable (interact to start the next wave), 1 = wave running (hidden, untargetable),
  2 = over; `wave` = wave index; `hello` 1-7 = the room (Tangmazu's line per room); `goodbye`, `play_interlude`, `deli_boss_spawned`.
  Its label is LIKELY `ObjectDisplayAfflictionatorWaveCount` "Wave {0}/{1}" / "Oleada {0}/{1}".
- Seven rooms: server markers `room_teleport_location_1..7`, `chest_and_portal_teleport_location_1..7`, `loot_teleport_location_1..7`
  (each wave in its own room, reached by teleport: LIKELY).
- Choices between waves `.../Objects/SimulacrumEscalationShardBoss|Elite|Mods`: WorldDescription "Apex Predators" / "Depredadores
  alfa" (spawn an additional boss), "Pure Emotions" / "Emociones puras" (additional monsters), "Escalating Threats" / "Amenazas
  crecientes" (adds an area modifier); `visible` 1 = targetable with icon (orange / purple / teal), `detonate` 1 = chosen (shatters),
  2 = not chosen (drops). Listed today by their world titles (a `/Leagues/` path with an icon, LIKELY). Wave modifiers:
  `AfflictionEndgameWaveMods` (14).
- End: `Terrain/Gallows/Leagues/Delirium/Act1Town/Objects/SimulacrumEndMirror` (`simulacrum_over` 2 = targetable), `SimulacrumEndTangmazu`.

### 2.3 Dangers

Hidden monsters dressed as props, `Monsters/LeagueDelirium/DoodadDaemons/DoodadDaemon{Eyes1-3,Spikes1-2,ShardPack1-8,BoneRibcage,
GoatFillet1-2,RhoaFillet,Pimple1}` (base `LeagueAffliction/DoodadDaemons/DoodadDaemonBase`: `visible`, `blocking`; a Proximity
variant): they burst into monsters as the player nears (LIKELY). Chasers `Monsters/LeagueDelirium/Volatile/DeliriumVolatile`
(`invisible`) and `DeliriumVolatileStationary`. Ground buffs `affliction_demon_cold_degen` "Frozen Ground" / "Suelo congelado",
`affliction_corpse_degen` "Putrefaction" / "Putrefacción" (not in `hazards.txt`).

### 2.4 Not in the files

Where the fog's edge is or how long until it passes (no object; only "inside or not" through the player stat, LIKELY); how a shard is
set off; the HUD bar's numbers (whether it prints a percentage).

## 3. Abyss

The chain (KeywordPopups 351, MEASURED text): a pit with fissures branching from it; monsters spawned near the Abyss are weakened;
killing them closes the fissures; when they are closed up to the pit, the pit opens and spawns Abyssal monsters; killing those closes
it; each closed pit may raise an Abyssal Trove; the last one may open the Abyssal Depths.

| Object (path under `Metadata/`) | States (Transitionable value, MEASURED comments) | Name (en / es) | Icon | Listed today |
|---|---|---|---|---|
| Start `MiscellaneousObjects/Abyss/AbyssStartNode` (a crack base) | as the crack | - | `Abyss` | no |
| Fissure segment `MiscellaneousObjects/Abyss/AbyssCrack` (base `AbyssCrackBase`, position serialised) | 1 closed; 2 opening 1/6 "wait for player"; 3-7 opening 2/6-6/6; **8 fully open, waiting**; 9-11 closing slowly; 12-14 closing slowly reverse; 15 closing quickly; 16 closing quickly reverse (9, 12, 15, 16 fire `close_crack`) | - | `AbyssCrackActive` shown from 8; `AbyssCrackInactive` once closing | no |
| Pit `MiscellaneousObjects/Abyss/AbyssNodeBase` (last: `AbyssFinalNodeBase`) | 1 initial; **2 opening and spawning**; 3 closed, failed; 4 closed, succeeded (icon hidden) | objective "Complete all Abysses" | `AbyssPitActive` → `AbyssPitInactive` (3) | yes, "Abismo" while active |
| Trove `Chests/Abyss/AbyssPitChestBase`, `AbyssFinalChestBase`, `AbyssStrongbox` (spawned by `MiscellaneousObjects/Abyss/Chests/AbyssSpawner*`) | 1 emerging (untargetable), 2 targetable, 3 opened | "Abyssal Trove / Coffer / Arsenal / Armoury / Cache" ("Tesoro / Cofre / Arsenal / Armería / Alijo abisal", Large…, …Strongbox) | `AbyssChestActive` → `AbyssChestInactive` | as chests |
| Way down `MiscellaneousObjects/Abyss/AbyssSubAreaTransition` (AreaTransitionToggleableReverse + Timer) | targetable when its timer finishes | areas `Abyss_Depths1-3` "Abyssal Depths" / "Profundidades abisales" | - | as a transition |
| Depths chests `Chests/Abyss/AbyssDepthsChestBase` | `unlocked`; 1 emerging, 2 targetable (after the final rare dies, LIKELY: KeywordPopups 577), 3 opened (offers `AbyssReturnToSurface`) | "Chest" / "Cofre", "Large Chest" | - | as chests |

Monsters: `Monsters/LeagueAbyss/` (factions Lightless, Blackblooded, Legion of the Pit). What marks the weakened monsters is UNSURE
(hidden buffs `abyssal_touched`, `abyss_monsters_damage_taken` exist; no `.ot` names them). The Well of Souls hub (`Abyss_Hub`) has
`AbyssJumpInteractable` "Jump In" / "Saltar"; `AbyssalEndObjectDisplayText` "Embrace Abyssal Power?" / "¿Aceptas el poder abisal?".

Dangers: ground buffs `abyss_desecrated_ground` "Desecrated Ground" / "Suelo profanado", `monster_mod_abyss_mana_siphoner[_outer]`
"Soul Siphoning of Enfeebling / Exhaustion" ("Succión de almas de debilitamiento / agotamiento"), `abyss_mod_apparition_beam`
"Kulemak's Desecration" / "Profanación de Kulemak" (none in `hazards.txt`); `abyssal_wasting` is an on-hit debuff.

Not in the files: how many segments a fissure has and their order (positions only at run time); the weakened-monster mark.

## 4. Breach and Breach Hives

| Object (path under `Metadata/`) | Components and states | Name (en / es) | Icon | Listed today |
|---|---|---|---|---|
| Unstable Breach `MiscellaneousObjects/Brequel/BrequelInitiator` (the maps' breach, LIKELY: "Open all Unstable Breaches") | ControlZone (the ring); `encounter_state` 0 inactive, **1 expanding**, **2 enraged** (stabilised: stronger monsters from the centre), 3 closing enraged, 4 closing not enraged (icon hidden at 3/4) (code comment, MEASURED) | "Fisura" (keyword) | `Breach` | yes, "Fisura" |
| Older breach `MiscellaneousObjects/Breach/BreachObject`, `EndgameBreach` | Transitionable: transitioned = opened; ControlZone | | `Breach` | yes |
| Clasped hands `Chests/Breach/BreachChest`, `BreachChestLarge`, `BreachChest6Large`, `BreachBoxChest01` | Chest; `chest_open` | "Clasped Hand" / "Puño cerrado", "Breachlord's Clasped Hand", "Xesht-Ula's Clasped Hand", "Grasping Coffers" / "Arcas atrapantes" | - | as chests |
| `MiscellaneousObjects/Brequel/LotusHand` | `exists` 1 = blooms, then targetable | `ObjectDisplayTextGraspingMailPlant` "Grasping Orchid" / "Orquídea acechante" | - | no |
| Hive: stabiliser `.../Brequel/BrequelStabiliser` (+`Fortress`) | InteractionAction; `interaction_state` 1 = used (icon hidden), `bomb_defence` 2 = done | "Summon Ailith" / "Invocar a Ailith" | `Breach` | yes, "Fisura" |
| Hive: Ailith `Monsters/Breach/NPC/ChayulaFarmerWild[Follow…]` | the one to defend (Life) | "Ailith" | - | ally |
| Hive: walls `.../Brequel/BrequelBlocker` | TriggerableBlockage; `destroyed` 1 burning, 2 gone | - | - | no |
| Hive: skill objects `.../Brequel/BrequelSkillObject` | `ready` 1 = targetable; `activated`; "Activate the closest Breach Skill" | Ailith's skills (KeywordPopups 980-988: "Xesht's Fervour", "Dreamer's Sight / Inspiration / Gift / Might"…) | - | no |

Progress (MEASURED strings, en / es): `BrequelBreachEnragedNotification` "Breach Stabilised" / "Fisura estabilizada",
`BrequelUnstableBreachCollapsedNotification` "Unstable Breach Collapsed" / "Fisura inestable colapsada", `BrequelStableBreachCollapsedNotification`
"Stable Breach Collapsed" / "Fisura estable colapsada"; hive: `BrequelBreachBombDefenceWaves` "Wave {0}/{1}" / "Oleada {0}/{1}",
"Breach Hive Successfully Destroyed" / "Colmena de fisura destruida", "Failed to Defend Ailith" / "No has logrado defender a Ailith",
"Escape the Hive Fortress" / "Escapa de la fortaleza de la colmena". Player buffs: `breach_walk_buff` "Into the Breach" / "A la fisura",
`brequel_hive_immune_and_damage` "Dreamer's Might", `rarity_vs_brequel_hive_monsters` "Dreamer's Gift".

Dangers: `breach_hand_mage_storm_lightning_aura` "Esh's Storm" / "Tormenta de Esh" (taking lightning damage), `breach_waking_nightmare`
"Waking Nightmare", `BreachWithered` (count), `breach_boss_spider_tether` "Webbed" / "Atrapado en la telaraña" (none in `hazards.txt`).

Not in the files: the ring's radius and how fast it grows or shrinks (it is the ControlZone, unread by the host); how long until it
closes (server).

## 5. Azmeri spirits and wisps

- **The spirit** (KeywordPopups 426, MEASURED text): a wisp that flees as the player approaches; Normal/Magic monsters it touches
  become Spirit-Influenced, a Rare/Unique it reaches becomes Possessed; the more influenced monsters killed first, the richer the
  possessed one. Entity: MonsterVarieties `Metadata/Monsters/TormentedSpirits/TormentedSpiritofthe<Animal><Type>` (`TormentSpirits`,
  22 rows: Owl, Serpent, Primate = Primal; Bear, Boar, Ox = Wild; Wolf, Stag, Cat = Vivid; Rabbit, Fox = Sacred; `…Viper` copies;
  `TormentedSpiritoftheAbyss`), base `TormentedSpirits/TormentedSpiritBase`: an AbstractDaemon with Actor (`MoveForced`, `FollowPath`),
  Pathfinding, Life, **no Targetable**, states `active` (1 = icon shown) and `wisp_time`, MinimapIcon `SpiritActivated` (hidden at
  spawn). Names: "Spirit Of The Owl" / "Espíritu del búho", "… Bear" / "… del oso", etc. (MonsterVarieties, en / es).
- **What tells which monster**: influenced monsters carry the mod `SpiritOf<Animal>Touched` ("Owl-Touched" / "Tocado por el búho",
  "Hare-Touched" / "Tocado por la liebre"…, Mods Name) plus `SpiritTouched{Int,Str,Dex,Sacred}`; possessed ones `SpiritPossessed*` and
  the buff `tormented_spirit_power` "Possessed by an Azmeri Spirit" / "Poseído por un espíritu azmeri" (BuffDefinitions 2720), and
  their possessing daemon `…PossesedDaemon`. Released spirits can manifest again (KeywordPopups 1028).
- Map content around them: Spirit Guide, Azmeri Energisation, Spirit Migration, Sacred Spirit, Azmeri Champion, Swarming Spirits,
  Azmeri Bloodline (`EndgameMapContent` 14, 20-22, 31, 43, 48).
- **Wisps that are not spirits**: `Monsters/LeagueRitual/RitualWispDaemon` "Ritual Wisp" / "Fuego fatuo de ritual" (hidden, cannot be
  damaged; buff `ritual_wisp_buff` "Wisp's Blessing" / "Bendición del fuego fatuo": more Tribute; KeywordPopups 339: within 3 metres);
  the Viridian Wildwood's `Monsters/LeagueAzmeri/GuidingLight/GuidingLightDaemonMaze_` (follows a path; fuel `MiscellaneousObjects/Azmeri/
  AzmeriFuelResupply`, `AzmeriLightBomb`; debuff `azmeri_darkness_debuff` "Ebon Affliction" / "Aflicción de ébano": banished at 10
  stacks; `azmeri_swamp_ground` already in `hazards.txt`; entrance `Terrain/Leagues/Azmeri/Objects/WildwoodEntrancePortal`, `enabled` 1).
- Not in the files: the spirit's speed and flee distance; whether its Life or position stops when it possesses (LIKELY it ends inside
  the possessed monster as the daemon).

Sections 6-10 come from a second pass over the same dumps (spot-checked against the `.ot`). The `MissionTrack*` strings
some of them quote are PoE1 Kirac mission lines still in ClientStrings (LIKELY unused in PoE2); the PoE2 map objectives are the
`EndgameMapObjectives` texts ("Open all Strongboxes", "Activate all Shrines", "Free all Essence Monsters", "Defeat all Rogue Exiles",
"Energise the Vaal Beacons").

## 6. Strongboxes

- Every box is a row of `Chests` under `Metadata/Chests/StrongBoxes/` with no `.ot` of its own; all inherit `Metadata/Chests/StrongBoxes/Strongbox`
  (Chest, Targetable `untargetable_while_player_in_sector`, TriggerableBlockage, InteractionAction; icon `Strongbox`, MinimapIcons 813).
  States (MEASURED handlers): `started` 1 = opened by the player: packs are summoned and the box turns untargetable 2 s later; `started`
  0 = targetable again (fight over, LIKELY); `opened` 1 = looted, icon hidden; `robot` 1 = not rendered. Drops wait for the summoners
  (`chest_delay_drops_until_daemons_finish`, mods 8079/8081/8085: 5-10 packs, map box 6-12, Solidified Corruption 10-20).
- Kinds (`Strongboxes` 50 rows × tiers `Low` 0-44, plain 45-64, `High` 65+, `Expedition` 65+; Chests names, MEASURED): `MartialStrongbox`
  "Blacksmith's Strongbox" / "Caja fuerte del herrero", `CasterStrongbox` "Arcane Strongbox" / "Caja fuerte arcana", `ArmourerStrongbox`
  "Armourer's Strongbox" / "Caja fuerte del armero", `JewellerStrongbox` "Jeweller's Strongbox" / "Caja fuerte del joyero", `ResearchStrongbox`
  "Researcher's Strongbox" / "Caja fuerte del investigador", `MapStrongbox` "Cartographer's Strongbox" / "Caja fuerte del cartógrafo",
  `LargeStrongbox` "Large Strongbox" / "Caja fuerte grande", `BasicStrongbox` "Strongbox" / "Caja fuerte", `OrnateStrongbox` "Ornate Strongbox" /
  "Caja fuerte ornamentada", `GemStrongbox` "Solidified Corruption" / "Corrupción solidificada". Uniques `.../Unique/UniqueVaalStrongbox`,
  `UniqueEzomyteGrave`, `UniqueEtchedBeetle`, `.../VentorStrongbox/UniqueVentorsMysteryBox` (icons 876, 875, 851; names not in Chests: UNSURE).
  Abyss boxes: §3. `StrongboxVarieties` (102) is art only.
- The ambush: monsters `Metadata/Monsters/Strongbox/{Base,Ezomyte,Karui,Maraketh,Vaal,VaalUnique,Corruption,Abyss}StrongboxMonsterBase`
  (Transitionable: transition = emerge; state `epk`); hidden summoners `Metadata/Monsters/Strongbox/Daemon/*` carry counters `monsters_left`
  / `packs_left` (MEASURED names; readable only if the client keeps those daemons: UNSURE). Ezomyte box: `SingleRevivingMonster` revives until
  a final beam. Hazard daemons `.../Daemon/StrongboxChaosDaemon` (ground runes), `StrongboxFlameVolatileDaemon`, `StrongboxIceSpikeDaemon`
  (`IceSpike`, timer `thrust`), `StrongboxLightningDaemon` (which box uses which: UNSURE).
- Strings: `ItemErrorStrongboxLocked` "Target Strongbox is locked." / "La caja fuerte objetivo está cerrada.", `GamepadInteractInfoStrongboxAltOnly`
  "Hold: Apply Currency" / "Mantener presionado: Aplicar objetos monetarios". No HUD counter for one box.

## 7. Shrines

- The world shrine `Metadata/Shrines/Shrine` (+`Shrine_Trigger`) is generic: `Shrine` component, Targetable, Life 100, no StateMachine;
  `ObjectDisplayTextShrine` "Shrine" / "Santuario"; icon `ShrineActive` (778) (MEASURED). `LesserShrine` (Transitionable: `disappear`,
  `DiesAfterTime`), `DarkShrine` ("Darkshrine" / "Santuario oscuro", button "Sacrifice" / "Sacrificio"; transition = used, icon hidden).
- **Which kind it is**: the `Shrines` row is chosen by the server, not the path. The host's `ShrineOffsets` reads `IsUsed` (+0x24) and has the
  Shrines row pointer commented out at +0x30 (`ShrineDatRowPtr`, GameHelper's): reading it would name a shrine before use (offset UNSURE on
  0.5). Otherwise the kind is known after use from the player's buff (MEASURED rows, all 45 s):

| Shrines row: name en / es | Player buff | Effect en / es |
|---|---|---|
| 53 Meteoric Shrine / Santuario de meteoritos | `shrine_fire_2` | You gain Damage as Fire Damage and Meteors rain down around you. / Ganas el daño como daño de fuego y caen meteoritos a tu alrededor. |
| 54 Tempest Shrine / Santuario de tempestad | `shrine_lightning_2` | …as Lightning Damage and bolts strike the ground around you. / …como daño de rayo y unas descargas golpean el suelo a tu alrededor. |
| 55 Freezing Shrine / Santuario de congelamiento | `shrine_cold_2` | …as Cold Damage and freezing shards fire around you in a spiral. / …como daño de hielo y unas esquirlas congelantes se disparan… |
| 56 Gloom Shrine / Santuario de penumbra | `shrine_gloom_2` | …as Chaos Damage and slain enemies can explode… / …como daño de caos, y los enemigos derrotados pueden explotar… |
| 57 Acceleration Shrine / Santuario de aceleración | `shrine_acceleration_2` | increased Skill and Movement Speed / velocidades de habilidades y movimiento aumentadas |
| 58 Divine Shrine / Santuario divino | `shrine_godmode_2` | You cannot be Damaged. / No te pueden infligir daño. |
| 59 Seeking Shrine / Santuario de búsqueda | `shrine_magicfind_2` | increased Rarity of Items Found / rareza de objetos encontrados aumentada |
| 60 Enduring Shrine / Santuario de fortaleza | `shrine_massive_2` | increased Life and Defences / vida y defensas aumentadas |
| 61 Replenishing Shrine / Santuario de recarga | `shrine_regeneration_2` | regenerating Life and Mana / regeneras vida y maná |
| 62 Enlightening Shrine / Santuario de instrucción | `shrine_experience_2` | increased Experience / experiencia aumentada |
| 63 Greed Shrine / Santuario de avaricia | `shrine_goldfind_2` | Monsters you kill only drop gold. / Los monstruos que matas solo arrojan oro. |
| 64 Resistance Shrine / Santuario de resistencia | `shrine_resistance_2` | additional Resistance to Elemental Damage / resistencia adicional al daño elemental |
| 65 Diamond Shrine / Santuario de diamante | `shrine_critical_2` | You always deal Critical Hits. / Siempre infliges impactos críticos. |
| 66 Corrupting Shrine / Santuario corruptor | `shrine_corrupt_2` | All item drops are Corrupted. / Todos los objetos que caen están corruptos. |
| 71 Greed Shrine / Santuario de avaricia | `shrine_noequipment_2` | All Equipment found converted to other items. / Todo el equipamiento que cae se convierte en otros objetos. |

  Lesser shrines (rows 24-29, 20 s): `shrine_acceleration`, `shrine_brutal`, `shrine_regen`, `shrine_massive`, `shrine_resistance`, `shrine_defence`
  ("Lesser Acceleration / Brutal / Replenishing / Massive / Resistance / Impenetrable Shrine"). Each shrine also gives nearby monsters the same
  buff (`shrine_*_2_monster`, MEASURED columns; area UNSURE). The elemental shrines' effect spawns `Metadata/Monsters/Daemon/Shrines/Shrine{Fire,
  Lightning,Cold}Daemon` (the player's own, not a danger).

## 8. Rogue exiles

`Metadata/Monsters/RogueExiles/` (base `RogueExileBase`, extends `Monsters/Monster`; `RogueExiles` 14 rows = MonsterVarieties 2184-2197):
`Str/ExileWarrior1-2` "Taua, the Ruthless" / "Taua, el despiadado", "Raok, the Bloodthirsty"; `Dex/ExileRanger1-2` "Bronnach, the Manhunter",
"Adrienne, the Malignant Rose"; `Dex/ExileHuntress1-2` "Nisha, the Fierce", "Izel, Atziri's Chosen"; `Int/ExileWitch1-2` "Vasa, of the Death
Akhara", "Ciara, the Curse Weaver"; `Int/ExileSorceress1-2` "Nyassa, the Flaming Hand", "Hesperia, the Arcane Tempest"; `StrDex/ExileMercenary1-2`
"Ulfred, the Afflicted", "Drusian, the Artillerist"; `DexInt/ExileMonk1-2` "Sondar, the Stormbinder", "Doran, the Deft" (Spanish names in the
same rows). Icon `RogueExile` (854) is shown only while `boss_life_bar` = 1 (static boss life bar on) and hidden at 0 and on death; states
`disable_life_bar`, `light`, `no_talk`, `fleeing`, `lock_on` (MEASURED handlers). They use player skills (`PlayerClass`; `RogueExileModBonuses`
456 rows); engage lines are audio only. Map content: Hunting Grounds, Surprising Alliances, Fleeing Exile, Azmeri Bloodline (possessed
on a spirit's death), Rites of the Rogues. `EntityInfoRogueExileUniqueItem` "Has {0} Equipped" / "Tiene equipado {0}".

## 9. Incursion: Vaal Beacons and the temple (Fate of the Vaal)

- **Beacon in a map** (`IncursionInfoPopup`: energise Beacons to collect 6 Energised Crystals that open Atziri's Temple):
  `Metadata/MiscellaneousObjects/LeagueIncursionNew/IncursionPedestalEncounter` (icon `IncursionArchitectUpgrade`, listed today as "Foco
  vaal"): `activated` 0-1 untargetable (idle / encounter running), **2 targetable** (`ready_to_interact_01`), **3 done** (icon
  `IncursionEncounterComplete`, 993); `open` 1 = portal to the Vaal Ruins open; counters `alive_mons`, `alive_bosses`, `progress`, flags
  `wave_encounter`, `mini_encounter`, `secondary_encounter` (MEASURED handlers; what moves 1 → 2 is server side). Crystals
  `.../IncursionPedestalCrystal_1..6` (`activated` 1 = lit). `.../AtziriShrineEncounter`: title "Ancient Beacon" / "Foco antiguo", "Vaal Beacon" /
  "Foco vaal" once the player has flag `IncursionSourceVaalKnown`; body `ObjectDisplayTextIncursionTokensAcquired0..6` "Energised Crystals
  Acquired: N …" / "Cristales energizados obtenidos: N …" (at 6: "Activate to open a portal to the Vaal Ruins" / "Actívalo para acceder a las
  Ruinas vaal"); `activated` 2/3 ready / claimed (LIKELY); ProximityTrigger radius 100.
- Fight: blood-bather packs, mini-bosses "Quadrilla Sergeant" / "Sargento cuadrila", "Corrupted Abomination", "Royal Sentinel" / "Centinela real",
  "Royal Colossus" / "Coloso real", "High Priest" / "Alto sacerdote", "Unchained Beast" / "Bestia desencadenada" (MonsterVarieties 2662-2675);
  HUD `IncursionBarDisplayName` "Monsters Killed" / "Monstruos matados"; no timer in the scripts.
- **The temple**: hub `IncursionHub` "Vaal Ruins" / "Ruinas vaal", temple areas `IncursionTemple` "Atziri's Temple" / "Templo de Atziri",
  `IncursionTemplePresent` "Lost Temple" (WorldAreas 429-431). Hub objects `Metadata/Terrain/Gallows/Leagues/Incursion/Objects/`: `HubInteractible`
  = the **Temple Console** ("Temple Console" / "Consola del templo"; states `usable`, `insertarmready`, `open`, `active` (1 = untargetable),
  `backonline`), `HubTransition`, `HubDoor`, `AccessChamberRitualBowl` ("Access Chamber Ritual" / "Ritual de la Cámara de acceso", targetable when
  `disconnected` = 0). The console's window places **room cards** on a grid (`Incursion2Rooms` 37 rows: Garrison / Guarnición, Commander,
  Armoury, Smithy, Generator, Spymaster, … Architect's Chamber, Atziri's Chamber, vaults; tiers 2-4 renamed). Rooms in the temple show minimap
  icons `Incursion<Room>Active/Inactive` (939-990, biome rooms 1029-1040), doors `IncursionDoorLocked/Open` (39/40), benches `IncursionCraftingBench*`
  (41-43; states `active` 1 used, `busy`, `single_use_consumed`), medallions (992). Window strings (en / es): "Temple Console", "Room Cards" /
  "Cartas de sala", "Temple Mods" / "Mods. del templo", "Place a room" / "Colocar una sala", "Click to face Atziri" / "Haz clic para enfrentarte a
  Atziri", "Temple Complete" / "Templo completado", "Completed Temple Rooms ({0}/{1})" / "Salas del templo completadas ({0}/{1})", "Energise {0} more
  Beacons to enter the Vaal Ruins" / "Da energía a {0} focos más para acceder a las Ruinas vaal", `GamepadShowIncursionTempleUseDesc` "View Temple Map" /
  "Ver mapa del templo"; errors "Place up to 3 Restricted Rooms before continuing.", "Temple is not yet complete. Are you sure you want to leave the area?",
  "Atziri's Chamber is not connected to the rest of the temple."
- Dangers: ground buffs `architect_ground_blood*` "Boiling Blood" / "Sangre hirviente" (in `hazards.txt`), `vaal_cloud` "Corrupting Smoke" / "Humo
  corrupto"; wall turrets `Monsters/LeagueIncursion/WallTurrets/WallTurret*`. Bosses (Xipocado, Atziri): [boss-mechanics.md](boss-mechanics.md).
- Not in the files: the grid's layout and connections (window data), what the beacon waves are and how long.

## 10. Essence

- Monolith `Metadata/MiscellaneousObjects/Monolith` (+`Monolith_Landmark`, big `SuperMonolith` with `SuperMonolithController.fight_status` 1 started,
  2 over; small `MiniMonolith` around each prisoner, no icon): icon `Essence` (27), Targetable while `disabled` = 0; **Transitionable 1-4**: 1 intact,
  2-3 cracking (`CrackLevel1/2`), **4 shattered** (untargetable, icon hidden, deleted 5 s later, sound `OpenEssence`); `num_essences`, `has_greater`,
  `has_corrupted`, `is_corrupted` (2 = Vaal shatter) (MEASURED). Listed today as "Esencia: <monster>".
- The prisoner's essence is its monster mod (`Essences.MonsterMod`; `Mods.Name` e.g. `MonsterEssenceModFire1` "Essence of Flames" / "Esencia de
  llamas"): Body / cuerpo, Mind / mente, Enhancement / mejora, Abrasion / abrasión, Flames / llamas, Ice / hielo, Electricity / electricidad, Ruin /
  ruina, Battle / batalla, Sorcery / hechicería, Haste / celeridad, the Infinite / infinito, Seeking / búsqueda, Insulation, Thawing, Grounding,
  Alacrity, Opulence, Command; corrupted Hysteria, Delirium, Horror, Insanity; Abyss, Breach. Freed: hidden buff `monolith_monster_freedom` (694).
  Text `MonolithHelperText` "The monster is imprisoned by powerful Essences." / "Este monstruo está aprisionado por Esencias poderosas."
- Dangers (ground objects of essence monsters, MEASURED paths under `Metadata/Monsters/EssenceModDaemons/`): `EssenceModCold1/ColdEssenceDeliveryObject`,
  `EssenceModFire1/FireEssenceModSolarOrb`, `EssenceModMana1/EssenceManaBlastZone`, `EssenceModSpeed1/SpeedEssenceTimeTrap`, `EssenceModChaos1/ChaosEssenceVine`,
  `EssenceModDefense1/EssenceBoneWall` (`tilted`), `EssenceModHysteria/HysteriaOrbDaemon`, `EssenceModInsanity/EssenceInsanityProxTrigger`.

## 11. Ritual groups (Forbidden Rites, 0.5.5)

Rituals now come in groups per campaign area: `RitualWorldAreaGroups` (41 areas, e.g. `G1_2` boss `Metadata/Monsters/Hags/UrchinHagBoss`,
`G1_3` `MudBurrower/MudBurrowerHeadBoss`; flag `<area>_GroupRitualCompleted`) and `RitualAreaCompletionFlags` (38,
`ForbiddenRitesAreaComplete<area>`) (MEASURED). The altar is unchanged (`Terrain/Leagues/Ritual/RitualRuneInteractable`: `current_state`
1 ready, 2 running, 3 done → icon `RitualRuneFinished`; already listed). New lines: "You have completed all of the Rituals in the Area" /
"Has completado todos los rituales del área" (`RitualHUDButtonPopupCompleted`), "You have already completed this area's Rituals before."
Ritual wisps: §5.

## 12. What the plugin needs

Minimal additions, ordered by value to a blind player. Paths are exact (`@` tails stripped as today); "sure" is how sure the signal is.

**Host first** (each unlocks several rows below): (a) expose `Transitionable.CurrentStateEnum` (+0x120 is declared in `TransitionableOffsets`;
verify on one Abyss pit or Essence monolith); (b) check in one entity dump whether the `StateMachine` list carries client-only states (Expedition
`in_placing_range`) and whether `ExpeditionPlacementIndicator` is in the entity list; (c) try `ShrineOffsets` +0x30 (Shrines row pointer).

| # | Mechanic | Rule (what to key on) | What to say / sound | Sure |
|---|---|---|---|---|
| 1 | Verisium Remnant (campaign and maps) | List `Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter` (+`Expedition2EncounterQuest`) in Encounters while `activated` ∈ {0,1,2,5,6} (icon `Expedition2RemnantActive`); name "Remanente de verisium" (KeywordPopups 346) | `activated` → 1 "Remanente: abre el tomo de runas"; → 5 "Oleadas del remanente"; → 6 "Remanente: recompensa lista"; → 7 drop it. The Tome window is a window (generic reader; its strings in §1.4) | MEASURED states (code comment) |
| 2 | Expedition markers | New Encounters entries (or an "Expedición" category) for `Metadata/MiscellaneousObjects/Expedition/ExpeditionMarker` by its minimap icon: `ExpeditionChest1/2/3` "cofre", `ExpeditionEliteMonster` "élite", `ExpeditionRegularMonster` "monstruos", `ExpeditionLeagueSpecificMonster`, `ExpeditionBoss` "jefe"; `ExpeditionRelic` (`ExpeditionPOI`) "reliquia"; drop when `expedition_detonated`/icon gone | where the treasure is before placing charges | MEASURED paths and icons; kind ↔ icon LIKELY |
| 3 | Expedition placement | While placing (indicator entity present, or HUD "Colocando explosivos"): on each move say how many markers have `in_placing_range` = 1, by kind ("cubre 2 cofres, 1 élite"); `ExpeditionPlacementIndicator.blocked` = 1 → "no se puede colocar"; distance and side of the indicator from the player and from the last `ExpeditionExplosive`; a soft tick per marker entering range. After each placement read the HUD "Explosivos colocados: X/Y". Fallback without client states: markers within a radius of the indicator (radius to measure live) | the blast's reach, the chain's end, the count left | paths/states MEASURED; readability of client states UNSURE |
| 4 | Expedition detonator | `ExpeditionDetonator.explosives_placed` → 1 "Detonador listo"; `activated` → 1 "Detonando"; each `ExpeditionExplosive.activated` → 1 a muffled thud at its place | the chain's progress | MEASURED |
| 5 | Delirium fog | Player stat `is_in_affliction_fog` 1 → 0: urgent "Fuera de la niebla" (and 0 → 1 "En la niebla"); while in the encounter, a short tick every few seconds outside it | the one way to keep the encounter alive | stat MEASURED; that it is set on the player LIKELY |
| 6 | Delirium shards and Grand Mirror | List `Metadata/MiscellaneousObjects/Delirium/DeliriumShard*`, `DeliriumMirroredBoss`, `DeliriumAddDeliModsToRares`, `DeliriumBossSpawnsGrandMirror` while `visible` = 1 and `detonate` = 0 (mod's names: "fragmento: jefe", "portal", "élite", "pausa la niebla", "modificadores"); `DeliriumGigaMirror` while `enabled` = 1 "Espejo grande" | where the events are | MEASURED |
| 7 | Simulacrum | `DeliriumSimulacrumObject`: `active` → 0 "Oleada N de 7 lista: interactúa con el espejo"; → 1 "Oleada N"; → 2 "Simulacro completado"; `wave` gives N | wave progress | MEASURED states; label text LIKELY |
| 8 | Azmeri spirit | List MonsterVarieties ids starting `Metadata/Monsters/TormentedSpirits/TormentedSpiritofthe` (not `…PossesedDaemon…`) while `active` = 1, named by MonsterVarieties ("Espíritu del búho"); an enemy with buff `tormented_spirit_power` is said "poseído"; with a `SpiritOf*Touched` mod "tocado" | follow the fleeing wisp; know which rare it reached | MEASURED ids, state, buff, mods |
| 9 | Abyss | With (a): list `MiscellaneousObjects/Abyss/AbyssCrack` segments in state 8 (open) as a trail to the pit ("grieta"), nearest first; `AbyssNodeBase` state → 2 "El abismo se abre", → 4 "Abismo cerrado", → 3 "Abismo fallido"; troves are chests already | follow the fissure, know when the pit fights | MEASURED comments; reader needs (a) |
| 10 | Breach | `BrequelInitiator.encounter_state` → 1 "Fisura abierta", → 2 "Fisura estabilizada: monstruos en el centro" (if the HUD line is not already read), → 3/4 "Fisura cerrada"; hive: list `Brequel/BrequelSkillObject` while `ready` = 1 ("habilidad de fisura"), say Ailith's life when low (`Monsters/Breach/NPC/ChayulaFarmerWild*`) | progress; the ring's edge stays unknown (ControlZone) | MEASURED states |
| 11 | Shrines | Name before use from the Shrines row (c), else after use from the player's new `shrine_*` buff (§7 table: name and effect) | what the shrine did | buff ids MEASURED; row pointer UNSURE |
| 12 | Strongboxes | `Metadata/Chests/StrongBoxes/*`: name from `Chests.Name` (§6); `started` → 1 "Emboscada"; `started` → 0 with `opened` = 0 "Caja fuerte lista para abrir"; `opened` → 1 drop | when the box can be opened | handlers MEASURED, meaning LIKELY |
| 13 | Rogue exiles | Enemies starting `Metadata/Monsters/RogueExiles/`: say "Exiliado: <nombre>" once when `boss_life_bar` → 1 | who and when the fight starts | MEASURED |
| 14 | Vaal Beacon | `IncursionPedestalEncounter.activated` → 2 "Foco listo", → 3 "Foco completado", `open` → 1 "Portal a las Ruinas vaal"; read the `AtziriShrineEncounter` world body (crystal count) on approach | beacon progress | MEASURED handlers |
| 15 | Essence | With (a): monolith state → 4 "Esencia liberada"; name the prisoner's essence from its `MonsterEssenceMod*` mod name | when the fight starts | MEASURED |
| 16 | Hazard alarm | Add to the hazards extractor or a league list: `expedition_ele_fire_tornado`, `expedition_ele_cold_tornado`, `expedition_ele_lightning_tornado`, `affliction_demon_cold_degen`, `affliction_corpse_degen`, `abyss_desecrated_ground`, `monster_mod_abyss_mana_siphoner` (prefix covers `_outer`), `abyss_mod_apparition_beam`, `breach_hand_mage_storm_lightning_aura` (check why the extractor skipped them) | standing in harm | MEASURED buffs |
| 17 | Ritual groups | Altar `current_state` → 3 "Ritual completado"; the area line "Has completado todos los rituales del área" is HUD text | group progress | MEASURED |

Cannot be had from the files (one look in a run settles each; no planning on the user's play): the Expedition charge count, reach and radius
values and the blast order; Delirium's fog edge distance and how a shard is set off; Breach's ring radius; how many fissure segments an Abyss has;
the Tome and Temple Console windows' trees; which strongbox summons which packs; the weakened-Abyss-monster mark.
