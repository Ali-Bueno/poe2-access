# Trial of Chaos (Ultimatum) from the game's files (offline research, 2026-11-14)

Scope: what the mod needs to make the Act 3 Trial of Chaos playable blind, the way the Sekhemas were done
([trials-and-campaign.md](trials-and-campaign.md) §1, §2). No character has been there; everything comes from the files of
the bundle index dated 2026-11-13 15:05 (after 0.5.5f). Confidence: **MEASURED** (seen in a file: table and row, or `.ot`/`.arm`),
**LIKELY** (consistent with the files, not seen running), **UNSURE** (a guess or not in the files).

Sources: the tables under `data/balance/` (and `spanish/`) read with `tools/research/gamedata/dat.py`; the `.ot`/`.act` under
`metadata/terrain/gallows/act3/3_10/`, `metadata/monsters/leagueultimatum/`, `metadata/npc/league/ultimatum/`,
`metadata/terrain/leagues/ultimatum/`; the 61 room `.arm` of `3_10/rooms/` (parsed with `trial-rooms/armlinks.py`; distances by
`tools/research/gamedata/ultimatum/rooms.py`, in cells as in §1.6 of the Sekhemas notes); the shipped catalogs
(`telegraph-skills.txt`, `world-titles.json`, `hit-times.txt`, `boss-mechanics.txt`). Dumps were made in a scratch folder.

Corrections to [trials-and-campaign.md](trials-and-campaign.md) §2: there are 61 room `.arm`, not 9 (`UltimatumRooms` has 9 rows, one per
kind of room, MEASURED); the rounds are not chosen at an altar but by talking to the Trialmaster in each room (LIKELY, §2); the
trap paths in `TrialTraps` do not match what the game spawns (§4).

## 1. Areas and layout

- `G3_10_Airlock` "The Temple of Chaos" / "El templo de Caos" (links `G3_5`, `G3_10`) and `G3_10` "The Trial of Chaos" / "La Prueba de
  Caos", both area level 38 (WorldAreas rows 179, 178, MEASURED).
- Rounds per trial by area level (`UltimatumTrialLength`, MEASURED): level 1+ 4, level 60+ 7, level 75+ 10. The campaign's trial is
  4 rounds (LIKELY, level 38). Quest done = "earned 2 Ascendancy Points" (QuestStates row 679, MEASURED).
- `generate.rs` lists the rooms; `graphs/chaostemple_10room_1.dgr` is `entrance` → edge `ChaosRooms` → `end`, plus `exit_ruin` and
  the two Trialmaster arena graphs (MEASURED). Room files: `encounter_clear_01..08`, `encounter_timer_01..04`, `encounter_cores_01..04,06..08`,
  `encounter_follow_01..08`, `encounter_altar_01..04`, `encounter_idols_01..08`, `boss_frog/chimaera/owl_01..04`, `passage_end_01/02`
  (+`_lowlevel`), `entrance_01/02`, `exit_ruin_01`, `arena_trialmaster` (+`_pristine`), `airlock_01` (MEASURED).
- **One instance per stretch of rounds, rooms joined by lifts** (LIKELY): every encounter room has two levers (`LiftButton`), one linked
  to a lift `LiftMovementController_EntranceDescendOnce` (the way in) and one linked to `LiftMovementController_UpDisabledUntilComplete`
  (the way out, shut until the round is won) (links in the `.arm`, MEASURED). The lifts themselves are `Objects/LiftPlatform[Large]`
  (Transitionable: transitioned = up; states `activated`, `minimap_icon`; minimap icon `UltimatumAltar` shown while active; `WobblePoint
  is_elevator`), placed by the server or tiles, not in the rooms (MEASURED `.ot`; placement UNSURE). Idol and core rooms have up to 8
  levers (several floors joined by lifts, MEASURED counts).
- The reward room `passage_end_*` is the graph's `end` (MEASURED): reward chest, Stash, `ContinuePortal` ("next depth instance", comments
  in its `.ot`, MEASURED), `ExitPortal` and, outside the `_lowlevel` variants, `BossLiftTransition` to the Trialmaster's arena. Its
  pedestal spawner preloads pedestals `Wave4`, `Wave7`, `Wave10`: a stretch ends after round 4, 7 or 10 (LIKELY, matches `UltimatumTrialLength`).
- Never on the client (`server_only` + `no_serialise`, MEASURED): `TrialmasterMarker[Balcony]`, `EncounterTelepadMarker`,
  `PlayerRespawnPosition`, `StatuePathMarker`, `SurviveSpawnerMarker`, `DaemonMarker*`, `RewardMarker`, the pedestal spawner. So the
  statue's path, the arena's centre and where daemons will appear cannot be read at run time; only the spawned entities can.

## 2. The flow, in the order a player meets it

| # | Object (path) | Components and states (MEASURED `.ot`) | Name / text (en / es) | Icon |
|---|---|---|---|---|
| 1 | `Metadata/NPC/League/Ultimatum/UltimatumNPCAirlock` (Temple of Chaos) | NPC; `in_area`, `portal_out`; per-player flags `TrialmasterBackupKeyOffer`, `UltimatumQuestKeyHeld` | "The Trialmaster" / "El maestro de pruebas" (NPCs 165). Gives a new Inscribed Ultimatum after a failure (QuestStates 682: "Take a new Inscribed Ultimatum from The Trialmaster" / "Acepta un nuevo Ultimátum inscrito del Maestro de pruebas") | NPC |
| 2 | `.../3_10/Objects/UltimatumEntrancePedestal` "Temple Offering" | Targetable while `enabled`=1; `activated` (animation); WorldDescription; not in `airlock_01.arm` (placement UNSURE) | "Temple Offering" / "Ofrenda del templo"; body "Offer an Inscribed Ultimatum to undertake the Trialmaster's Challenges" / "Ofrece un ultimátum inscrito para iniciar los desafíos del Maestro de pruebas" (in `world-titles.json`) | none |
| 3 | `.../3_10/Objects/UltimatumEntrance` (airlock, 236 cells east of the waypoint) | portal; `ready` 0 = hidden and untargetable, 1 = shown 2.5 s later | label = area `G3_10`, "La Prueba de Caos" | none |
| 4 | `Metadata/NPC/League/Ultimatum/UltimatumNPC` in each room | NPC; `in_area` 0 not there, 1 approached (comes through a portal), 2 idle: targetable (not in a boss room), minimap icon and NPC marker, 3 on the balcony spawning monsters (untargetable), 4 teleports out; `talking`, `never_leave`, `in_boss_room`, `summon_direction` | "The Trialmaster" / "El Maestro de pruebas" (NPCs 163). Talking to him in state 2 opens the modifier choice (LIKELY: the window's strings, §5). Shouts per round (`UltimatumTrialMasterAudio` ApproachShout, rounds 1-9) | NPC |
| 5 | the round (§3) | | objective = `UltimatumEncounterTypes` name | |
| 6 | `.../3_10/Objects/LiftButton` (the exit lever) | Targetable while `enabled`=1 (1.5 s after); pull-lever animation; links `up`/`down`/`next` set the lift's `called` | "Switch" / "Interruptor" (`ObjectDisplayTextSwitch`) | none |
| 7 | `.../3_10/Objects/UltimatumRewardRoomTitle` | client proximity trigger, radius 90 | title card "Treasure Room of Chaos" / "Sala del tesoro de Caos", "Those who dare and succeed will be rewarded" / "Aquellos que se atreven y lo logran serán recompensados" | |
| 8 | `.../3_10/Objects/UltimatumRewardChest` | blocking; Targetable only for the map owner (`IsMapOwner`); `rewards` 0 not finished (untargetable, icon hidden), 1 can be taken (targetable, icon), 2 taken (icon `...Inactive`), 3 run continued; using it opens the confirmation "Trialmaster's Offer" (Take / Continue) | "Trial Rewards" / "Recompensas de la prueba" (`UltimatumRewardsDescriptionTitle`, in `world-titles.json`) | `UltimatumRewardChestActive` "Trial Rewards" |
| 9 | `Metadata/Terrain/Gallows/Leagues/Ultimatum/UltimatumWaveRewardChest` | Chest, rarity Rare; not in any room (spawned; LIKELY at `RewardMarker`, linked `item_drop_surrogate` to the chest) | "Chest" (Chests 1031) | |
| 10 | `.../3_10/Objects/UltimatumTreasureRoomPedestalWave4/7/10` | extends `AscendancyDevice`; per-player flags `AscendancyDevice2Used` / `AscendancyDevices1And2Used` / `AscendancyDevice3Used`; plays `TrialmasterG3_OnAscending_Random` | the Ascendancy altar's (LIKELY, from the base) | the base's |
| 11 | `.../3_10/Objects/ContinuePortal` | `open_state` 0 shut (hidden), 1 usable (start next stretch), 2 opening, 3 open (enter) | label `WorldAreas:G3_10` | 28 `EntrancePortal` |
| 12 | `.../3_10/Objects/ExitPortal` (transition `entrance2`) | AreaTransition, Transitionable; icon hidden at creation | | 28 `EntrancePortal` |
| 13 | `.../3_10/Objects/BossLiftTransition` (`passage_end_01/02` only) | AreaTransition, teleport delay 1.5 s, interaction 2 s; arrives at `BossLiftTransitionTop` in `arena_trialmaster` | no display text | 18 `Entrance` |
| 14 | `.../3_10/Objects/UltimatumTrialmasterPedestal` (arena) | AscendancyDevice; `boss_dead` 1 = targetable and icon; flag `AscendancyDevice4Used` | | |

Not placed in any room or tile the client has: `UltimatumSelector` ("Switch", `rewardstaken`), `DoorTrialmaster` ("Trialmaster's Tower",
"Insert three fates…", `key_1..3_used`, in `world-titles.json`), `DoorUltimatum`/`DoorExit`/`DoorOpen`, and the PoE1-style altar
`Metadata/Terrain/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable` (MiscObjects 87; has Life, `damaged` 0-3, `encounter_started`,
`encounter_finished` 1 won / 2 lost). Whether the 0.5 trial uses them is UNSURE.

Quest tracker (QuestStates 680-684, MEASURED en / es): "Travel to the Temple of Chaos" / "Viaja al Templo de Caos"; "Find a use for the
Inscribed Ultimatum" / "Descubre cómo puedes utilizar el Ultimátum inscrito"; "Place the Inscribed Ultimatum in the Temple Offering" /
"Coloca el Ultimátum inscrito en la Ofrenda del templo"; "Complete the Trialmaster's Challenges" / "Completa los desafíos del Maestro de pruebas".

## 3. The seven kinds of round

Objective names are `UltimatumEncounterTypes` +0x8 (localized table, MEASURED); the Trialmaster's line is its `NPCText` row of NPCTextAudio.
Distances are from the arrival point (`EncounterTelepadMarker`) in cells. Every encounter room also holds the hazard markers: 9
`DaemonMarkerTurret` (8-10), 6 `DaemonMarkerPetrify` (5-7), 1 `DaemonMarkerCentralObject` and 1 `DaemonMarkerBloodFog` (2 and 3 in
`follow_04/08`), all server-only (MEASURED counts).

| Id | Objective (en / es) | Trialmaster says (en / es) | Rooms | What the player must find or do |
|---|---|---|---|---|
| Slay | "Slay all Monsters" / "Mata a todos los monstruos" | "Leave naught but corpses." / "No dejes nada más que cadáveres." | `encounter_clear_01..08` | monsters only; exit lever 145-387 cells away |
| Survive | "Survive" / "Sobrevive" | "Survive." / "Sobrevive." | `encounter_timer_01..04` | start the **Corruption Dais**, then outlive the clock; 15-24 `LedgeForceField` (a `BossForceFieldDoor`) close the arena; 3 spawner spots 30-120 cells away |
| Collect | "Implant the Soul Cores" / "Implanta los núcleos de alma" | "Powering the monument requires souls..." / "Activar el monumento requiere almas…" | `encounter_cores_*` (7) | pick up 3 **Vaal Soul Cores** (177-414 cells away) one at a time and put each in a **Stone Altar** |
| Escort | "Escort the Statue" / "Escolta a la estatua" | "Follow the path." / "Sigue el camino." | `encounter_follow_01..08` | stay by the **statue** (25-57 cells away at the start) while it floats along a path of 5-13 legs, 521-980 cells long |
| Sacrifice | "Sacrifice Monsters" / "Sacrifica monstruos" | "Chaos demands sacrifice." / "Caos exige sacrificios." | `encounter_altar_01..04` | kill monsters inside each of 3 **blood circles** (53-313 cells away) until it fills; each feeds the **altar** |
| Destroy | "Destroy the Idols" / "Destruye los ídolos" | "Crush their objects of worship." / "Acaba con sus objetos de alabanza." | `encounter_idols_01..08` | destroy the idols: entity NOT found (below); 5-6 goal markers per room, 122-613 cells away, rooms of several floors (3-8 levers) |
| Boss | "Slay the Boss" / "Mata al jefe" | per arena: "Will this creature be your end?" / "¿Será esta criatura tu fin?" (frog arena, Chetza); "I'd be wary of its breath if I were you..." / "Si fuera tú, me cuidaría de su aliento…" (Uxmal); "Don't stare for too long, Challenger." / "No te quedes mirando demasiado tiempo, retador." (Bahlak) | `boss_frog/chimaera/owl_01..04` | the boss; each arena has a `HealingWell` |

The objects (MEASURED `.ot` unless said):

- **Corruption Dais** `.../3_10/Objects/SurvivalEncounter/SurvivalEncounterTimer`: Targetable while `disable`=0; states `disable`,
  `ultimatum_objective`, `alive_monsters`; WorldDescription "Corruption Dais" / "Tarima de corrupción", "Begin the Corruption Trial" /
  "Iniciar la prueba de corrupción" (both in `world-titles.json`). Not in any `.arm`: spawned, LIKELY at the telepad marker.
- **Vaal Soul Core** `.../SoulCoreEncounter/SoulCorePickup`: Targetable, MinimapIcon `UltimatumVaalSoulCoreGround` ("Vaal Soul Core"),
  `display_text` "Vaal Soul Core" / "Núcleo de alma vaal"; Transitionable (transitioned = taken: untargetable, icon hidden); refused while
  one is held ("Cannot pick up Vaal Soul Core while you are already holding one" / "No puedes recoger un núcleo de alma vaal si ya tienes
  uno en la mano"). Carrying gives buff `ultimatum_holding_soul_core` "Vaal Soul Core" / "Núcleo de alma vaal": "Return this to a Stone
  Altar. You are taking escalating Physical Damage over Time." (a race against damage).
- **Stone Altar** `.../SoulCoreEncounter/SoulCorePlinth`: `display_text` "Stone Altar" / "Altar de piedra", MinimapIcon `StoneAltarShort`,
  Targetable while untransitioned; needs a held core (`HasTrialSoulCore`, else "Missing Vaal Soul Core" / "Te falta un núcleo de alma
  vaal"); state `placed`; transitioned = core implanted (icon hidden). In no `.arm`: spawned; how many and where is UNSURE.
- **Statue** `.../StatueEncounter/EncounterStatue`: Life (cannot die), Actor (Move), speed 16, team 0, hidden monster
  (`does_not_count_as_monster`), no Targetable, no MinimapIcon; `no_collision` 1 draws a ring around it (`Interactible_aura2.ao`): the
  area to stand in, LIKELY. The game's word: "Statue" / "Estatua" (`ObjectDisplayTextStatue`; the line calls it a "StoneIdol").
- **Blood circle** `.../BloodAltarEncounter/BloodCircle` (MiscObjects 470): no Targetable, no icon, no name in ClientStrings; variable
  `position` 0/1/2; states `bloodfilled` (1 = full, fades out), `spawning`; beam `UltimatumAltarBloodBeam` to the altar. **Blood altar**
  `.../BloodAltarEncounter/BloodAltar` (MiscObjects 471): states `left`, `middle`, `right` (1 each as its circle fills; which `position`
  is which side is UNSURE) and `complete` (3 = done).
- **Idols**: no MonsterVariety, object, skill or buff named idol exists for the trial (searched names in en/es, ids, the bundle index);
  they are spawned by the server under a name not found (UNSURE). The goal spots are plain `Metadata/MiscellaneousObjects/Doodad` with
  `Marker_Pin_Goal_01.ao`, only in idol rooms (MEASURED), LIKELY editor markers the client does not keep.
- The other columns of `UltimatumEncounterTypes` (600/800 at +0x33, 400 at +0x37) and the quest flags (`UtlimatumMonstersSpawned`,
  `UltimatumEncounterSurvive`, `...Collect`, `...Escort`, `...Sacrifice`, `...Destroy`, `UltimatumBossSpawned`) are not explained by the files.

## 4. Hazards the modifiers put in the room

The entity path the game spawns is the **MonsterVarieties Id** (the shipped catalogs key on it, e.g. `hit-times.txt` has
`Daemon/TrackingSlam2`, `boss-mechanics.txt` `ChaosGodOwlBoss__`, ids no `.ot` file has; LIKELY strongly). Its `.ot` is the variety's
base type. Names below are `UltimatumModifiers` +0x49 (en / es, MEASURED); numbers from `telegraph-skills.txt` and the skills (MEASURED).

| Modifier ids (type) | Name en / es | Entity (MonsterVarieties Id, under `Metadata/Monsters/LeagueUltimatum/`) | How it hurts | Tell / state | Numbers |
|---|---|---|---|---|---|
| `LightningRuneDaemon1-3` (PlayerAttached) | Stormcaller Runes / Runas llamatormentas | `Daemon/StormcallerRuneSpawner1` (summons) → `Daemon/StormcallerRune` (base `LightningRuneDaemon`) | lightning strikes if you stay in a rune | states `charging`, `chargestate`, `rank`; `charging`>0 is the warning (LIKELY) | strikes 6/8/10 after 4000 ms, range 13/20/29, each radius 10 |
| `ExpandingDonutsDaemon1-2` (PlayerAttached) | Impending Doom / Perdición inminente | `Daemon/ExpandingDonut1`, `ExpandingDonut2` (base `Daemon`) | ground rings grow, then explode (physical) | skills `EDSUltimatumDelayedBlastDonut[2]`, `...Circle` (effects `doomcircle.ao`, `doom_sml_circle.ao`, already in `telegraph-skills.txt`) | |
| `BloodOrbsDaemon1-2` (PlayerAttached) | Blood Globules / Glóbulos sangrientos | `Daemon/TrackingSlam`, `TrackingSlam2` | globule tracks you and falls; II leaves blood ground 8 s | skill `UltimatumDaemonTrackingSlam` (animation `GroundSlamMoving`), `CGEUltimatumDaemonBloodGround` | `hit-times.txt` row `255=3237/0` |
| `BindingChainsDaemon1-3` (PlayerAttached) | Heart Tethers / Ataduras de corazón | `Daemon/BindingChains1-3` (base `Daemon`) | a heart spike (`Metadata/Monsters/TrialMaster/Objects/TetherSpike`, beam `TrialmasterTether`) tethers you: slowed 65%; breaking it stuns, +5/10/15% damage taken | skill `UltimatumModifierTetherSpike[2,3]`; buff `trialmaster_tether_debuff_display` "Trialmaster's Tether" / "Yugo del Maestro de Pruebas" | 3-5 s; length 35, width 27 |
| `RotatingPyramidDaemon1-2` (CentralObject) | Pyramid Beams / Haces de pirámide | `Daemon/RotatingLaserPyramid`, `RotatingLaserPyramid2` (base `LaserPyramid`) at the central marker | four rotating lasers, Corrupted Blood | always on; skill `UltimatumPyramidBeams[Reverse][2]` | rotation 5%/s |
| `PetrificationStatues1-3` (AreaDenial) | Petrification Statues / Estatuas petrificantes | `Turrets/PetrificationStatue1`, `PetrificationStatue2` at the petrify markers | petrifies if you stay in its gaze | state `visible`; stance 2 = eyes open (`open_blend_01` → `idle_open_01`), stance 1 = shut; beam `UltimatumPetrificationBeam`; buff `petrification_statue_gaze` "Petrified" / "Petrificado" | beam 600 (MiscBeams, meaning UNSURE) |
| `SpikedGround1-2` (AreaDenial) | Temple Traps / Trampas del templo | NOT found: no Ultimatum spike exists; LIKELY the Sekhemas' `Metadata/Monsters/MarakethSanctumTrial/Hazards/GroundSpike` (UNSURE) | spikes from the floor | Sekhemas spike: `rumble`, `spike` | |
| `FireTurrets1-3` (Turret) | Burning Turrets / Torretas abrasadoras | `Turrets/FireTurret1` (base `ProjectileTurret`) at turret markers | fire projectiles ahead | action `SpellProjectile` (animation `spellcast_01`), skill `MPSUltimatumTurretFireProjectile`; state `fadein` | explosion radius 10 |
| `LightningTurrets1-3` (Turret) | Shocking Turrets / Torretas electrocutantes | `Turrets/LightningTurret1`, `LightningTurret2` | lightning projectiles ahead | `MPSUltimatumTurretLightningProjectile` | |
| `OmnitectDaemon1-3` (SpawnOne) | Vaal Omnitect / Omnitecto vaal | `Omnitect/Omnitect`, `Omnitect2`, `Omnitect3` "Omnitect" / "Omnitecto" | waves, II-III also projectiles | `MDUltimatumOmnitectWave` (cast `SpellAreaOfEffect`), `MPSOmnitectTurretProj`; state `fadein` | wave donut 50×45, 60° |
| `StalkingShade1-3` (SpawnOne) | Stalking Shade / Sombra acechante | `Revenant/Revenant`, `RevenantRank2`, `RevenantRank3` "Revenant" / "Retornado" | invulnerable chaser; hits add Ruin | teleports (`EAAUltimatumRevenantTeleport`), buff `ultimatum_ruin` | Ruin 7 = trial failed |
| `BloodFog1` (SpawnOne) | Blood Mist / Niebla sangrienta | `Daemon/BloodFog` at the fog marker | monsters inside are immune | always on (a zone) | |
| `MonsterVolatileOnDeath*` (BuffOrDebuff) | Volatile Fiends / Enemigos volátiles | `Daemon/VolatileOnDeath[TierN_]` → `Daemon/UltimatumVolatile`, `UltimatumVolatileRare` "Volatile Core" / "Núcleo volátil" | a core chases and explodes | skill `UltimatumVolatileSuicideExplosion[Rare]` | radius 13 |

`ProjectileAreaDebuff` (Lessened Reach, Random Projectiles) is a debuff on the player, not an object. **Coverage today**: `TrialTraps`
keys `LeagueUltimatum/Turrets/ProjectileTurret`, `Turrets/PetrificationStatue`, `Daemon/LaserPyramid` are base `.ot` names, not
variety Ids, so they match nothing the trial spawns (LIKELY); the data step "game names" passes them because the `.ot` exist (LIKELY). The
Sekhemas keys are unaffected (their variety Ids equal their file names, `GroundSpike` included).

Buffs that carry the round's danger (BuffDefinitions, MEASURED en / es): `ultimatum_ruin` "Ruin" / "Ruina", shows a count, "On reaching
7 Ruin, you will fail the Trials" (a sighted player sees `UltimatumRuinIndicator`, `ruin_counter.ao`, over the head); `ultimatum_ruin_over_time`
"Escalating Ruin" / "Ruina creciente"; `ultimatum_vulnerability` "Escalating Vulnerability" / "Vulnerabilidad creciente" (modifier Escalating
Damage Taken); `tethered_debuff` "Tethered" / "Atado". None of them is in `hazards.txt`.

## 5. UI strings (ClientStrings / ClientStrings2, MEASURED en / es)

| Key | English | Spanish |
|---|---|---|
| `UltimatumSelectModifierDesc` | Select A Challenge Modifer / *Challenge modifiers accumulate with each round.* | Selecciona un modificador de desafío / *Los modificadores de desafío se acumulan en cada ronda.* |
| `UltimatumSelectModifierDescNotMapOwner` | Only the Map Owner can select a Challenge Modifier | Solo el dueño del mapa puede seleccionar un modificador de desafío |
| `GamepadLegendUltimatumWindowSelectChallengeModifier` | Select Challenge Modifier | Seleccionar modificador del desafío |
| `UltimatumWindowBeginButton` / `...VoteButton` | Accept trial / Confirm | Aceptar prueba / Confirmar |
| `UltimatumWindowBeginButtonNoSelectionPopup` | Select a new modifier first | Elige un modificador nuevo primero |
| `UltimatumWindowRound` | Round {0}/{1} (with `<ultimatumnumber>`) | Ronda {0}/{1} |
| `UltimatumBarDisplayName` | Challenge Progress | Progreso del desafío |
| `UltimatumActiveModifierLegendsTitle` | Active Modifiers | Modificadores activos |
| `UltimatumTimer` | {0}:{1} | {0}:{1} |
| `HiddenUltimatumModifierName` | Hidden Modifier (Wager of Mystery) | Modificador oculto |
| `UltimatumWindowCurrentRewardsTitle` / `...NextOfferTitle` | Current Rewards / Next Reward | Recompensas actuales / Siguiente recompensa |
| `UltimatumWindowTakeRewardsButton[Singular]` | Take Rewards / Take Reward | Tomar recompensas / Tomar recompensa |
| `UltimatumWindowPreviewRewardsButtonText` | Preview Rewards | Previsualizar recompensas |
| `UltimatumWindowPlusXRewards1/N` | {0} Reward(s) earned | {0} recompensa(s) obtenida(s) |
| `UltimatumConfirmationBoxTitle` | Trialmaster's Offer | La oferta del Maestro de pruebas |
| `UltimatumConfirmationBoxDescription` | Take your Rewards and end the Trial or Continue the Trial to gain more Rewards | Obtén tus recompensas y finaliza la Prueba o continúa con la Prueba para obtener más recompensas |
| `UltimatumTakeRewardsButton` / `UltimatumContinueRunButton` | Take / Continue | Obtener / Continuar |
| `UltimatumWindowTakeRewardsConfirmation` | Taking rewards early will end the Trialmaster's Challenge. Are you sure you want to proceed? | Si te llevas las recompensas antes, finalizará el desafío del Maestro de pruebas. ¿Seguro que quieres continuar? |
| `UltimatumFailedNoticeLabel` / `UltimatumWindowChallengeFailedPopup` | Ultimatum Failed | El Ultimatum ha fallado |
| `UltimatumItemisedTrialLeaveAreaConfirmation` | Leaving this area will abandon the trial. Are you sure…? | Si sales de esta área, abandonarás la prueba. ¿Estás seguro…? |
| `UltimatumErrorNotOwner` | Only the Trial owner can interact with this | Solo el dueño de la prueba puede interactuar con esto |
| `UltimatumErrorCannotEndQuestTrialEarly` | The Quest version of the Trial of Chaos cannot be ended early | La versión de misión de la Prueba de Caos no se puede finalizar anticipadamente |

No string speaks of a time limit on the choice (MEASURED: every Ultimatum key read); the only clock is `UltimatumTimer`, LIKELY the
Survive round's ("MM:SS", which `CountdownWatch` already says if it lands in the HUD). Party voting strings exist, not needed solo.

## 6. Bosses

`catalog/boss-mechanics.txt` already carries them (`boss` + `hide` lines, MEASURED grep): Bahlak `Metadata/Monsters/ChaosGodOwlBoss/ChaosGodOwlBoss__`,
Uxmal `Metadata/Monsters/Ultimatum/ChimeraUltimatumBoss__`, Chetza `Metadata/Monsters/Bird2/MutantBird2` (+ `add`, `form`, `use`), the
Trialmaster `Metadata/Monsters/TrialMaster/TheTrialmaster_` (+ `move|trialmaster_tether_debuff_display|keep`). The room ↔ boss link is
`UltimatumRooms` Boss1 `boss_frog_01` (Chetza's line), Boss2 `boss_chimaera_01`, Boss3 `boss_owl_01`.

## 7. What a sighted player gets by looking (and its signal in the files)

- Where the Trialmaster waits and when he has gone to the balcony: `UltimatumNPC` `in_area` 2 → 3.
- Which lift is live: `LiftPlatform` minimap icon `UltimatumAltar` while `activated`; the levers' `enabled`.
- The core on the player's back: buff `ultimatum_holding_soul_core`; the cores and altars on the minimap (`UltimatumVaalSoulCoreGround`, `StoneAltarShort`).
- The statue's ring and its motion; the path is server-only, so only "where the statue is now" can be told.
- Blood beams from circles to the altar: `BloodCircle.bloodfilled`, `BloodAltar.left/middle/right/complete`.
- The Ruin counter over the head: buff `ultimatum_ruin` count.
- Petrifying gaze open: the statue's stance; turrets' casts; the rune charging.
- Monster spawns: beam `UltimatumMonsterSpawnBeam` (MiscBeams 422); remaining monsters may wear the icon `UltimatumMonsterAlive` (no `.ot` uses it: set by the server, UNSURE).
- The reward room's title card and the chest lighting up (`rewards` 1).

## 8. What the plugin needs

Minimal additions, each keyed on what the files give (paths are exact; `@` instance tails stripped as today):

1. **Fix `TrialTraps` Ultimatum keys** to variety Ids: `Metadata/Monsters/LeagueUltimatum/Turrets/FireTurret1`, `.../Turrets/LightningTurret1`,
   `.../Turrets/LightningTurret2` (Turret); `.../Turrets/PetrificationStatue1`, `.../PetrificationStatue2` (PetrifyingStatue);
   `.../Daemon/RotatingLaserPyramid`, `.../RotatingLaserPyramid2` (Laser). Add `.../Daemon/StormcallerRune` (new kind, firing state `charging`),
   `.../Daemon/BloodFog` (a zone to keep monsters out of). The game-names step should look these up in MonsterVarieties, not as `.ot` files.
   Firing for turrets and statues is an action or stance, not a state: warn on the turret's `SpellProjectile` action and the statue's stance 2
   (needs `TrapWatch` to read the actor, or leave them named only).
2. **Named moving dangers** (enemies list or a hazard voice), names from the modifier rows: `.../Revenant/Revenant[Rank2|Rank3]` "Sombra acechante",
   `.../Daemon/UltimatumVolatile[Rare]` "Núcleo volátil", `.../Omnitect/Omnitect[2|3]` "Omnitecto vaal", `.../Daemon/TrackingSlam[2]` "Glóbulo
   sangriento", `.../Daemon/BindingChains1-3` and `Metadata/Monsters/TrialMaster/Objects/TetherSpike` "Corazón (atadura)".
   `ExpandingDonut*` blasts are already in `telegraph-skills.txt`.
3. **Quest-object sound and names** for the round's objects: add `UltimatumVaalSoulCoreGround` and `UltimatumRewardChestActive` to
   `MinimapIconIds.Quest`; key by path (the icon `StoneAltarShort` is shared): `.../3_10/Objects/SoulCoreEncounter/SoulCorePlinth` while
   untransitioned, `.../3_10/Objects/SurvivalEncounter/SurvivalEncounterTimer` while `disable`=0, `.../3_10/Objects/StatueEncounter/EncounterStatue`
   (today not listed: no Targetable, no icon; name "Estatua"), `.../3_10/Objects/BloodAltarEncounter/BloodCircle` while `bloodfilled`=0 (mod's
   own name, "Círculo de sangre"), `.../3_10/Objects/LiftButton` while `enabled`=1 (the Sekhemas levers' sound), `.../3_10/Objects/LiftPlatform`
   and `LiftPlatformLarge` while `activated`=1. Name `.../3_10/Objects/BossLiftTransition` (no text): "Ascensor al Maestro de pruebas".
4. **Announcements**: the round's objective from `UltimatumEncounterTypes` (add `encounter:` keys, name at +0x8, to `TrialDataExtractor`)
   when the round starts (`UltimatumNPC.in_area` → 3, LIKELY) or when the HUD shows it; "El Maestro de pruebas te espera" at `in_area` → 2;
   Ruin from buff `ultimatum_ruin`'s count ("Ruina 3 de 7", urgent at 6); "Llevas un núcleo de alma" / "Núcleo implantado" from buff
   `ultimatum_holding_soul_core` and the plinth's transition; each blood circle filled and the altar `complete`=3; reward chest `rewards`
   → 1 "Recompensas disponibles"; `ContinuePortal.open_state` → 1/3; petrify gaze buff `petrification_statue_gaze` and tether buff
   `trialmaster_tether_debuff_display` said as they begin.
5. **Hazard alarm**: consider `ultimatum_chaos_degenzone[2|3]` (Choking Miasma), `ultimatum_pale_ape_degen_aura[_ruin]`, `ground_vortex[_lightning]_ruin`
   for `hazards.txt` (harmful-ground buffs of the trial's monsters, not there now; check why the extractor skipped them).
6. **Cannot be had from the files** (needs one look in a run, no planning on the user's play): the idol entity's path; where the soul-core
   altars, the Corruption Dais and the lift platforms appear; which `position` is which side of the blood altar; whether the HUD prints the
   objective line and the timer as text; the choice window's tree (LIKELY three buttons with name and description, read by the generic reader).

## 9. What the plugin does (2026-11-14, unit tests and the data step only; no character has been there)

Built on §8, each rule keyed on a file, state or buff the data step "game names" found in the install (`catalog/game-names.txt`):

- **Hazards** (`TrialTraps.Varieties`): the turrets, the petrifying statues, the laser pyramids, the Stormcaller rune and the blood mist
  by their MonsterVarieties Ids, listed among the points of interest under their names; the rune's `charging` state plays the dodge
  warning within reach (`TrapWatch`). The three old keys (script names) matched nothing.
- **The round's pieces** (`ChaosTrial`, `EntityCatalog.Classify`): what is to be done next rings as a quest object and stands in the
  scanner's Quest category. A Vaal Soul Core while none is in hand; a Stone Altar while one is, with the clock's range and the guide by
  itself (`Timed`, as a Death Crystal: the core in hand does growing damage; setting `GuideTimed`); the statue to escort and each blood
  circle until it is full, though the game gives them no target and no minimap mark (the statue is flagged a hidden monster); a lift's
  lever while it can be pulled; the reward chest once it can be taken (icon `UltimatumRewardChestActive`). The lift to the Trialmaster
  has a name.
- **Said** (`ChaosTrialWatch`, setting `AnnounceTrialProgress`): the round's kind with what it asks as its pieces come into view
  (Collect, Escort, Sacrifice), "Lleva el núcleo a un altar de piedra" as a core is taken, "Círculo de sangre lleno", and Ruin as it
  grows ("Ruina 3 de 7", urgent from 6; the buff's charges, logged raw as "Ruin: …"). The log says each piece's states the first time
  ("Chaos trial piece seen: …").
- **Already there**: the round counter ("Ronda 3 de 10"), the modifier icons' names, the Corruption Dais and the reward chest by their
  world titles, the Trialmaster as a character, the bosses' mechanics, Impending Doom's blasts.

**Left, each for its reason:** the objective of a Slay, Survive, Destroy or Boss round (no piece tells it; said only if the HUD prints
it); the idols (not in the files); the turrets' and statues' strike (an action and a stance, not a state: named only); the choice
window, the timer's text and the reward window (never seen: the generic reader is expected to read them); the trial's harmful-ground
buffs in `hazards.txt` (§8.5). **First things to read in the log of a first run:** "Chaos trial piece seen", "Trial trap seen", "Ruin:".
