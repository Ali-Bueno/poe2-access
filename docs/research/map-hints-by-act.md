# Navigation cues placed by designers, by act (offline sweep of the bundles, 0.5.5e)

Source: the `Bundles2` read with LibBundle3 (every `.arm`, `.ot`, `.ao`, `.rs` under `Metadata/Terrain/Gallows/Act1..4` and
`Act4_Interlude`, indexed by which objects each room places). Offline, 2026-11-05: nothing here was seen in play but
what [map-hints](map-hints.md) records.

## Folder to area id mapping

`Metadata/Terrain/Gallows/Act<N>/<N>_<x>[_<y>]/` is area `G<N>_<x>[_<y>]`, with these exceptions. No dat column links folder and id;
the mapping is by naming and by the title-card/flag names inside each folder, so it is **inferred**:
- Act 1: `1_4a` = Root Hollow (G1_10); `1_13` is the shared Ogham room pool for G1_13_1 (`1_13_1`) and G1_13_2 (`1_13_2`).
- Act 2: `2_4` = Keth (G2_4_1), `2_4_1` = Lost City (G2_4_2), `2_4_2` = Buried Shrines (G2_4_3; its title card is
  `TitleCardBuredShrinesTemptationLandmark`); `2_5` Mastodon Badlands, `2_5_2` Bone Pits; `2_12_1`/`2_12_2` = the two halves of the Dreadnought; `2_3a` a Halani sub-part.
- Act 3 and Act 4: `3_x`/`4_x` map one to one.
- Interlude: `Act4_Interlude/Part1|2|3/P<n>_<k>` = `P<n>_<k>` (checked by flags: `p1_1` places the Blackwood and Altar of the First Ones entrance flags, `p2_1` the Pools of Khatal, Galai Gates and Skullmaw flags).
- Act 5/6 folders hold 1-2 files each: nothing. Towns, hideouts, endgame, world maps skipped.
- Tiles and doodads are shared with maps, so a name alone proves nothing: every row is a placement read from an `.arm` room of that area, or an object file in its `Objects/`.

## Reading the tables

Kind (how the mod can detect it; all **inferred** unless map-hints.md saw it live):
- **D** doodad placed through `.arm` with class `Metadata/MiscellaneousObjects/Doodad*`: listed by the client at arrival for the whole map (live-proven in Hunting Grounds, Grelwood, Clearfell).
- **O** server object from `Objects/` or an NPC entity (`Metadata/NPC/...Glyph`): entity known within about 200 cells.
- **T** tile/edge name in the layout the mod already reads.
- **F** client effect `.ao` that no file references: spawned by server script, so no arrival listing; trigger unknown.
- **S** `server_only` object without client logic: probably never reaches the client.

Confidence: **files** = the file shows the cue and its placement, and "points to" comes from a name/room/flag; **inferred** = judgement.
"Unknown" = no file says.

Two kinds of object recur and are described once:
1. **Story glyph NPCs** (`Metadata/NPC/Four_*/*Glyph`, all extend `NPC/Info/StoryGlyphBase`, like the Hunting Grounds sign). The sign is not unique:
   51 placements in Acts 1-4 and the interlude . Those in `entrance_*`/`exit_*`/`boss_*` rooms label that transition. Kind O.
2. **Title-card trigger objects** (`*Title.ot`, 63 files): server objects with a client `ProximityTrigger` (radius 30-120) calling
   `ShowTitleCard(TitleCard<Name>)` as the player nears an arena entrance, landmark or camp. Kind O; the mod may instead read the card text on screen.

## Act 1

| Area | Name | Cue | Asset path or pattern | Kind | Points to | Evidence | Conf |
|---|---|---|---|---|---|---|---|
| G1_1 | The Riverbank | none (DeadBull_01 x4 in `arena_*` rooms is fight scenery) | - | - | - | rooms read | files |
| G1_2 | Clearfell | Dead bulls and slash trail | `Doodads/Woods/Clearfell/DeadBull_01.ao` (19 placements) in rooms `cow_slash`, `cow_slash_trail`, `cow_woodslash*`, all five `boss_hagwitch_*`, 3 road overlays; edge `Woods/Slash/Trailside/slash_trailside.et` | D, T | Hag boss arena (bull in every `boss_hagwitch_*` room); the trail itself: unknown | rooms | inferred |
| G1_2 | Clearfell | Burrower trail | `Woods/Mudburrow/burrower_trail.et` in `mud_entrance*`, `overlay_road_curve_*` | T | Mud Burrow entrance (room names) | rooms | files |
| G1_3 | Mud Burrow | none (bloodtrail decals in `shrine_1` are decals, not entities) | - | - | - | - | files |
| G1_4 | The Grelwood | Red Vale River tiles | `Woods/RedvaleRiver/*` tiles, room `redvaleriverfacing_source.arm`, graph edge `RedvaleRiver/wideriver_redvaleriver_virtual.et` joining graph node `"redvale"` (12 `oldforest_*.tgr`) | T | Red Vale (node name) | graph node + edge | files |
| G1_4 | The Grelwood | Glowing roots | `Woods/GrimTangle/areatransitions/BreadcrumbTreeRoot01..03.ao`, class `Gallows/Act1/1_4/Objects/TreeRoot`, rooms `tree_root_01..03`, `tree_of_souls` | O | Tree of Souls | map-hints.md, rooms | files |
| G1_4 | The Grelwood | Mushroom scatter | `Objects/Scatter_GrimTangleEntrance.ot` in `rooms/grimtangle.arm`: client script spawns 500 `DoodadNoBlocking` mushrooms (none within 60 cells, dense to 250, fading to 500) | D (live: 411 FungiCluster) | Grim Tangle entrance | script + live | files |
| G1_4 | The Grelwood | Witch hut | `Objects/WitchHutTitle.ot` r30 | O | Witch Hut | title card | files |
| G1_4a | Root Hollow | none | - | - | - | - | files |
| G1_5 | The Red Vale | none | - | - | - | - | files |
| G1_6 | The Grim Tangle | `BreadcrumbTreeRoot01` + `IntroAreaLight` in arrival room `entrance_1`; nothing away from it | D | back to the Grelwood: unknown | room | inferred |
| G1_7 | Cemetery of the Eternals | Husband / Wife entrance glyphs | `NPC/Four_Act1/HusbandEntranceGlyph` in `entrance_mausoleum_1..4`; `WifeEntranceGlyph` in `entrance_tomb_1..5`; `LostSonGlyph`, `LostDaughterGlyph` in `landmark_01` | O | Mausoleum, Tomb of the Consort (room names); landmarks unknown | rooms | files |
| G1_8, G1_9 | Mausoleum, Tomb | none | - | - | - | - | files |
| G1_11 | Hunting Grounds | Signpost glyph | `NPC/Four_Act1/HuntingGroundsSignpostGlyph` in `Road_Cnr_Signpost_01..03`, `CliffRoad_St_Signpost_01` | O | Ogham (text); direction not in data | map-hints.md | files |
| G1_11 | Hunting Grounds | Dead bulls, crows, feathers | rooms `deadbull_01/02`: `Objects/DeadBull`, 8 `Crow.ao`, `Featherpile01/07`; footprint FX `Effects/Environment/Act1_FOUR/G1_11/crowbell_footprint*` inside `DeadBull.ao` | O, D | the Crowbell: no file ties them to its arena (rooms in the general pool, tag `special3`) | map-hints.md | inferred |
| G1_11 | Hunting Grounds | Crowbell arena | rooms `crowbell_full_1/3`: `BellCrowbell`, `danglebell_01.ao`, title `CrowbellArena` r50 | O, D | boss arena | rooms | files |
| G1_11 | Hunting Grounds | Canopy entrance | `NPC/Four_Act1/CanopyGlyph1` + `Objects/CanopyEntranceMarker` in room `entrance_canopy_1` | O | the way up to Freythorn | room name | files |
| G1_11 | Hunting Grounds | Locust trail after a Ritual | none for this area. Only candidate: `Effects/Environment/Act1_FOUR/G1_12/VodooKing_Arena/ritual_direction_01.ao` (a `fwdline` bone: draws a line forward); no `.ot`/`.arm` references it | F | next Ritual altar: unknown (`HuntingGroundsRitualAltar` x5 in rooms `ritual_01..05`) | folder name | inferred |
| G1_12 | Freythorn | `ritual_direction_01.ao` (above); 6 `CanopyRitualAltar` in rooms `ritual_1..3_01/02`; King in the Mists arena title | F, O | line to a ritual: unknown | names | inferred |
| G1_13_1 | Ogham Farmlands | Cattle: 7 `DeadAnimals/DeadBull*.ao` colours + crows in `*_cows_01` rooms; scarecrows in `boss_01`; Una's home title r30 | D, O | unknown | rooms | inferred |
| G1_13_2 | Ogham Village | `VillageGlyph1` (`story_glyph_01`), Executioner arena title r80, Renly's workshop title | O | landmarks by name | titles | files |
| G1_14 | Manor Ramparts | `ManorGlyph3` in `entrance_1`, `ManorGlyph1` in `exit_1/2`, `ManorGlyph2` in `gallows_1`; `captain.ao` + `captainnote.ao` in `exit_*`; Hanging Hallows title r60 | O, D | exit to Ogham Manor (catalog also names tile `Manor_GrandEntrance_Transition_01`) | rooms | files |
| G1_15 | Ogham Manor | `ManorJournalGlyph1` in `boss_entry`, `...2` in `study*`; Candlemass title | O | boss entry | rooms | files |

Act 1 summary: Hunting Grounds and Grelwood hold the real cues; Clearfell has a bull and slash trail whose bulls sit in the Hag arena rooms. Nothing for G1_1, G1_3, G1_5, G1_8, G1_9, G1_10.

## Act 2

Hub (`2_town`, not an area row): `Doodads/Desert/CaravanTrails/<Dest>/<xxx>_caravantrail_{footsteps,footstepsheavy,rhoasteps,wheeltracks}.ao`, one set per destination
(`vst` Vastiri, `agt` Gates, `lck` Keth, `qry` Quarry, `ssp` Spires, `vot` Valley of the Titans, `dnv` Dreadnought, `dnw` Wake, `sni`, `mab` Badlands).
The destination side holds `..._wheeltracks_static.ao` (class Doodad) in the **arrival rooms**. Which way the tracks run is not in the files.

| Area | Name | Cue | Asset path or pattern | Kind | Points to | Evidence | Conf |
|---|---|---|---|---|---|---|---|
| G2_1 | Vastiri Outskirts | Caravan trails `CaravanTrail_RedSand_{StS,CvS,CvL}`; Trading Town wayfarer stone (`Glyph/WayfarerStoneGlyph_set_01.ao` + `CrimsonShoresGlyph`); titles Maraketh Outpost r40, Devastated Camp r80, Rathbreaker arena r120 | `Doodads/Desert/CaravanTrails/CaravanTrail_RedSand_*.ao` in rooms `caravan1`, `tradingtown_01` | D, O | trading town (outpost); that the trail leads to it is inferred | rooms | inferred |
| G2_2 | Traitor's Passage | Five wayfarer stones: `Doodads/Desert/TraitorsPassage/WayFarerStone_dust01_01.ao` + `NPC/Four_Act2/TraitorsPassageGlyph` + a checkpoint, in unique rooms `loreobject_01..05` | D, O | press says a parchment at a fork: no file is named parchment/scroll; fork placement unknown | rooms, `generate.rs:31-35` | inferred |
| G2_2 | Traitor's Passage | Redstone caravan trails in `entrance_01`; wall seals; Sekhema statues title r40; Balbala arena title | `CaravanTrail_RedStone_{CvL,CvS,StL}.ao` | D, O | entrance; boss at `bossentrance_01` | rooms | files |
| G2_3 | Halani Gates | `Effects/Act2/AncientGates/2_3_path_sand_wind_{ew,ns,sn,we}.ao`; graphs `_canyon_trail_*.dgr`; `Colosseum_Tracks*.ao`; sandstorm title r50 | F, T, D | unknown (names only, may be scenery) | names | inferred |
| G2_4_1 | Keth (`2_4`) | `NPC/Four_Act2/DryRuinsGlyph` + wayfarer glyph + static `LCK_CaravanTrail_WheelTracks_Static.ao` in `entrance_01/02`; treasure/shrine titles r50 | O, D | entrance | rooms | files |
| G2_4_3 | Buried Shrines (`2_4_2`) | `WaterGoddessGlyph` + wayfarer glyph in `bossentrance_01/02`; Water Goddess room title r50; Forsaken Son entrance title r50 | O | boss entrance rooms | rooms | files |
| G2_5_1 | Mastodon Badlands (`2_5`) | `Effects/Environment/Act2_FOUR/G2_5_1/Breadcrumbing/Abyss/direction.ao` (`fwdline` rig); only reference: `MiscellaneousObjects/Abyss/AbyssFinalNodeBase.ot` preloads it | F | probably the Lightless Passage: tile edge `abysstrail.et` in rooms `abyss_entrance`, `abysstrail_{st,cnr,end}_01`; `abyss_entrance` holds `LightlessPassageTransition`, `AbyssCheckpoint_01` | names | target files / link inferred |
| G2_5_1 | Mastodon Badlands | Static caravan tracks `CaravanTrail_Badlands_{StS,StL}.ao` in `unique/entrance.arm` | D | entrance | room | files |
| G2_6 | Valley of the Titans | `TitanPose01..09`, `TitanPoseShrine01` (class `Doodad_Distant_3`; heads are inside the poses) in 9 generic `valley/*` rooms, 2 `fills`, `landmark_essence_01` | D | not tied to entrances (generic rooms): the press claim is not supported | rooms | files |
| G2_6 | Valley of the Titans | Ancient seals 1-3: `AncientSeal{1,2,3}Portal` + titles r50 in `unique/titan_shrine_01..03`; Grotto entrance: `TitanGrottoPointOfInterest` (title r120) + `TitanValleyGlyph` + `WayfarerStone03_01` + static wheel tracks in `unique/centre_01` | O, D | seals open the grotto; centre room = Grotto entrance | rooms, titles | files |
| G2_8 | Deshar | `DeadRhoaGlyph` + `DeadRhoa_01.ao` + static `SSP_..._Static.ao` in `entrance_01`; `Featherpile_01..03` (Doodad) in `3open_04`, `unique_01`; duo boss arena title r50 | O, D | entrance; featherpiles unknown (compare Hunting Grounds feathers around bulls) | rooms | inferred |
| G2_10_1 | Mawdun Quarry | static `QRY_CaravanTrail_WheelTracks_Static.ao` in `entrance1_town` | D | entrance | room | files |
| G2_11 | Dreadnought's Wake | tile edge `Desert/Oasis/Tracks/DreadnaughtTracks.et` | T | scenery likely | name | inferred |
| G2_13 | Trial of the Sekhemas | wayfarer stone `WayfarerStone01_01_sanctum.ao` in `entrance_01` | D | entrance | room | files |
| other | G2_4_2, 2_5_2, 2_7, 2_9, 2_9_2, 2_10_2, 2_12 | nothing cue-like (arena titles, flags, ballistae, bones) | - | - | - | files |

Act 2 summary: dependable cues are wayfarer/glyph stones and arrival-room caravan tracks; the Mastodon abyss line is the only line effect. No parchment or fork marker found; Titan heads are generic.

## Act 3

| Area | Name | Cue | Asset path or pattern | Kind | Points to | Evidence | Conf |
|---|---|---|---|---|---|---|---|
| G3_1 | Sandswept Marsh | Expedition camp at arrival (`Doodads/Jungle/AbandonedExpedition/Camp.ao`, `RuinedCity/VictarioNotebook.ao`, `NPC/Four_Act3/JungleExpeditionGlyph01`) in `entrance_01`; `Doodads/Jungle/MarshBarricades/Barricade01..07.ao` + `3_1_exit.env` in `exit_01` | D, O | exit side marked by barricades | rooms | files |
| G3_2_1 | Infested Barrens | **Explorer camp**: `Camp.ao`, `Town_ExpeditionTent_01`, `Objects/TroubledCampLandmarkTitle` (card `InfestedBarrensExplorerCamp`, r50, off once quest flag `JungleOutskirtsLandmarkComplete`) in `landmark_01`, `landmark_01_v2`. Reveal: `Objects/ChimeralWetlandsTransition` (extends `AreaTransition_Animate`; state `entrance_revealed` runs `RevealMinimapInRadius(80)`) in `exit_chimeralwetlands_01` | D, O | next area entrance, revealed on the minimap | object scripts | files |
| G3_3 | Jungle Ruins | Explorer camp: `Camp.ao`, `ExpeditionFieldTools02_01`, `TroubledCampLandmarkTitle` (`JungleRuinsExplorerCamp`) in `landmark_01/02`; `JungleExpeditionGlyph02` in `overlay_lore`; `InfestedBarrensTransition` (same reveal state) in `exit_jungleanterior` | D, O | next area entrance | same pattern | files |
| G3_5 | Chimeral Wetlands | Ravaged camp: tents, notebook, `NPC/Four_Act3/RavagedCampGlyph`, `RavagedCampLandmarkTitle` r70 in `landmark_01/01a`; `ChaosTempleTransition` (reveal state) in `entrance_02`; toxic maze title r120 | D, O | Temple of Chaos entrance | same pattern | files |
| G3_2_2 | Matlan Waterways | `Objects/GlyphPowerLine.ot` (state `activate` swaps glow epk 0000 for 1111) in rooms `waterways_{lake,final,abyss}_01..04`; `WaterLevelLever*`; final lever title r70 | O (state) | the lever route; where a line points: unknown | object state | files / inferred |
| G3_4 | Venom Crypts | none (boss entrance title x2, snake spawners); the press change is not visible in files | - | - | - | files |
| G3_6_1, G3_6_2 | Machinarium, Sanctum | `GlyphPowerLine*` and medallion lines, key pedestals, Machinarium glyph NPCs | O | keys, boss door by name | names | inferred |
| G3_7 | Azak Bog | `Effects/Environment/Act3_FOUR/G3_7/breadcrumb_direction/fire_direction_01.ao` (`fireline.pet`; no file references it); burning-man effigy (`BurningManEffigyCentral/Small`, card `CarverBogEffigyLandmark` r120, room `burningman_01`); rune stone `rune_01` | F, O | effigy room is the only named landmark; line target unknown | folder name | inferred |
| G3_14 | Utzaal | gold idol room titles 1-3, r70 | O | idol rooms | titles | files |
| G3_16 | Aggorat | `BloodTrailTitle` r90 + `AggoratPastAtziriGlyph` in `divider_gate_01`; sacrifice table title | O | the divider gate | room + card | files |
| other | G3_8..G3_12, G3_15, G3_17 | arena/landmark titles only | O | - | - | files |

Act 3 summary: the explorer-camp pattern is explicit: camp doodads + title card + a transition object whose `entrance_revealed` state reveals the next entrance on the minimap (three areas). Matlan has glyph power lines, Azak Bog a bare fire-direction effect, Venom Crypts nothing.

## Act 4

| Area | Name | Cue | Asset path or pattern | Kind | Points to | Evidence | Conf |
|---|---|---|---|---|---|---|---|
| G4_1_1 | Isle of Kin | `Objects/VolcanicWarrensEntranceQuestFlag` in `exit_01/02`: client trigger r80 runs `RevealMinimapInRadius(90)` on the `AreaTransition_Animate` within 100; `NikoGlyph` in `delve_01` | O | Volcanic Warrens | object script | files |
| G4_2_2 | Journey's End | `JourneysEndGlyph01/02` + `JourneysEndKaruiGlyph01.ao` + prison totem in `entrance_01` | O, D | entrance | room | files |
| G4_3_1 | Whakapanu Island | `TropicalSettlementGlyph_01` x3 (`settlement_*`), `TropicalGlyphController` in `entrance_01..04` | O | settlement | rooms | files |
| G4_5_1, G4_5_2 | Abandoned Prison, Solitary | `ChapelPrisonGlyph` (`chapel_01`), `GoddessOfJusticePrisonGlyph` (`entrance_01`), `SolitaryEntranceGlyph` (`boss_01/02`) | O | chapel, entrance, boss | rooms | files |
| G4_7 | Shrike Island | `ShrikeIslandGlyph` x6 in `entrance_01..03` (+flip) | O | entrance | rooms | files |
| G4_11_1a/b | Ngakanu | 1b: `TavakaiBloodFeverGlyph` + `HeartOfTheTribeEntranceQuestFlag` in `exit_01`; 1a: glow totem + `MudTracks02/04` in `entrance_01` | O, D | Heart of the Tribe (flag name) | rooms | files |
| G4_13 | Plunder's Point | `SubAreaEntranceQuestFlag` (empty server object) | S | unknown | - | files |
| G4_14 | Brinerot Cove | `Lanterns/LanternPillarFlies_01..04.ao` in `dunepath_*`, `pier_*` | D | road to the pier / town end: scenery likely | room names | inferred |
| other | G4_1_2, 4_2_1, 4_3_2, 4_4_x, 4_6, 4_8a/b, 4_9, 4_10, 4_11_2, 4_12 | nothing cue-like (arena totems, braziers, mud tracks along cliffs are decor; `4_8b` has path edges `twilight_cliffside_path.et`, `twilight_gorge_path.et` with `cliff_gate_01`/`exit_gate_01`, names only) | - | - | - | files |

Act 4 summary: no navigation assets beyond story glyphs at entrances and one minimap reveal. The press did not cover Act 4 either.

## Interlude (Act4_Interlude Part 1-3)

| Area | Name | Cue | Asset path or pattern | Kind | Points to | Evidence | Conf |
|---|---|---|---|---|---|---|---|
| P1_1 | Scorched Farmlands | `AltarToTheFirstOnesEntranceQuestFlag` (room `stonecircle_transition`), `BlackwoodEntranceQuestFlag` (`block_transition`); `FarmlandsGlyph` | S, O | Stones of Serle, The Blackwood | flag names | files |
| P1_2 | Stones of Serle | `RuneRock_01..06` (+`_source`) and `Maps/Megalith/Monolith01..06` in `landmark`; minimap icon `RuneMarkers` | O | boss summoning | object states | inferred |
| P1_3 | The Blackwood | `HoltenEntranceQuestFlag` in `entrance_02` (S), `BlackwoodGlyph`, 4 `immortal_skull.ao` | S, O, D | Holten | flag name | files |
| P1_4 | Holten | `HoltenEstateEntranceQuestFlag` in `exit_1` (S), `HoltenGlyph` | S, O | Holten Estate | flag name | files |
| P1_5, P1_6 | Wolvenhold, Estate | `WolvenholdGlyph` (`boss_01/02`), `EstateGlyph_01/02` (`landmark_treasure_*`) | O | boss, treasure | rooms | files |
| P2_1 | The Khari Crossing | **Wayfinders**: `Objects/Wayfinder1..4` + `Wayfinder{N}Pointer` (targetable, text `InterludeP2Wayfinder`; `Wayfinder1.ot` has `UpdatePlayerPointer(angle)` attaching `WayfinderPlayer.ao` rotated by the angle) in `entrance_01` (1), `landmark_02` (2), `landmark_03` (3), `exit_01` (4); base doodad `Interlude/Part2/Town/WayfinderBase.ao` | O, D | each is a pointer turned by script toward something unknown | object code | files / inferred |
| P2_1 | The Khari Crossing | Entrance flags (S): `TowerEntrance` (`entrance_01`), `PoolsOfKhatalEntrance` (`exit_01`), `GalaiGatesEntrance` (`exit_02`), `SkullmawStairwayEntrance` (`exit_04`); `KhariCrossingTombLandmarkEntrance` (`landmark_01_interior`) | S, O | Pools of Khatal, Galai Gates, Skullmaw stairway | flag names in `exit_NN` rooms | files |
| P2_2 | Pools of Khatal | `SelKhariSanctuaryEntranceQuestFlag` (`exit`, S), `JadoGlyph`, monolith | S, O | Sel Khari Sanctuary | flag | files |
| P2_6 | Qimah | `AzmadiGlyph` (`entrance_01`), Sekhema shrines 1-7 + `ExitPedestal` (`areatransition_animate_toggleable`) | O | exit unlocked at the pedestal (names) | objects | inferred |
| P3_1 | Ashen Forest | `KriarVillageEntranceQuestFlag` (`exit_01`, S) | S | Kriar Village | flag | files |
| P3_2 | Kriar Village | `KriarGlyph_01` (`entrance_01`), `_02` (`boss_01`); wisp spawners (tormented spirits) | O | entrance, boss | rooms | files |
| P3_3 | Glacial Tarn | `HowlingCavesEntranceQuestFlag` in `lake_01..03` (S) | S | Howling Caves | flag | files |
| P3_4..P3_7 | Howling Caves, Kriar Peaks, Etched Ravine, Cuachic Vault | `HowlingCavesGlyph`, `WispLight` (blue decor), `RavineGlyph` in `checkpoint_*`, `VaultGlyph_01/02`; none directional | O | - | - | files |
| other | P2_3, P2_5, P2_7 | nothing | - | - | - | files |

Interlude summary: the strongest are the Khari Crossing wayfinders and the per-exit flags (server-only; the exit room name carries the destination if the layout is read).

## Areas with nothing found
G1_1, G1_3, G1_5, G1_8, G1_9, G1_10; G2_4_2, G2_7, G2_9, G2_9_2, G2_10_2, G2_12; G3_4 (Venom Crypts), G3_8 to G3_12, G3_15, G3_17; G4_1_2, 4_2_1, 4_3_2, 4_4_x, 4_6, 4_8a/b, 4_9, 4_10, 4_11_2, 4_12; P2_3, P2_5, P2_7, P3_5.
"Nothing" = no asset whose name or placement ties it to a direction (only arena or landmark title cards, decor).

## Not found or not readable
- Press cues with no file: Traitor's Passage parchment, Hunting Grounds locust trail, Matlan/Venom Crypts/Azak Bog "changes".
- Server scripts are not in the bundles, so the targets of `direction.ao`, `fire_direction_01.ao`, `ritual_direction_01.ao` and the wayfinder pointers are unknown.
- Graph (`.dgr`) adjacency was not parsed: "next to a transition" rests on room file names, not on graph nodes.
- Doodads placed by `doodads.ddt`/tile `.tdt` tables (not `.arm`) were not read.
- Folder-to-id mapping is by naming (above), not by a dat link.
