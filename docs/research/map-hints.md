# Navigation hints GGG placed in the campaign (patch 0.5.0)

What a sighted player reads off the world, and what the mod can get of it (2026-11-05, 0.5.5e). The radar's own reading
of the map is in [radar](../radar.md). Every area, act by act: [map-hints-by-act](map-hints-by-act.md).

## What the mod says

`MapHintNarrator`, with the cues listed by hand in `catalog/map-hints.json`; dev command `maphints` lists what was found.
Walking within 60 cells of a cue (the ground the mod counts as seen) it is said once, again only 200 cells on
(`MapHintMemory`: the Grelwood's mushrooms are one patch). Every story sign is said, with no row needed. The cues that run
somewhere (bold) are trails, below.

| Cue | Area | Found by | Leads to |
|---|---|---|---|
| Dead cattle | Clearfell, Hunting Grounds | model `DeadBull_01`; entity `Objects/DeadBull` near | the boss room |
| Mushrooms | Grelwood | model `FungiCluster`, listed at arrival | The Grim Tangle |
| Glowing roots | Grelwood | entity `Objects/TreeRoot`, near | the Tree of Souls' tile |
| **River** (trail) | Grelwood | tiles `RedvaleRiver/`, `WideRiver/`, `Woods_WideRiver/` | The Red Vale |
| **Burrower's trail** | Clearfell | tiles `Mudburrow/trail` | Mud Burrow |
| **Road** | Hunting Grounds | tiles `Woods/Road/`, `Woods/CliffRoad/` | Ogham Farmlands, the end no bramble closes |
| **Road** | Ogham Farmlands; Ogham Village | tiles `RoadFields/RoadEdge/Road`; `.../RoadOpen` | Ogham Village; Renly's workshop |
| **Swarm** | Hunting Grounds; Freythorn | the ritual altar's `current_state` reaching 3 | Freythorn; the nearest altar left, then the boss |
| **Maraketh seals** | Traitor's Passage | objects `Act2/2_2/Objects/WallSeals` | Balbala's arena |
| **Cracks** | Mastodon Badlands | doodads `AbyssNew/Scatter/g2_5_abyss` | Lightless Passage |
| **Paved street** | Drowned City; Utzaal | tiles `VaalExterior/SunkenStreet/`; `VaalExterior/Street/` | Apex of Filth; Aggorat's door, in Viper's room |
| **Gold channel** | Molten Vault | tiles `TreasureVault/GoldChannel` | the boss |
| **Coast** | Kedge Bay; Whakapanu Island | tiles `Island/ShoreLine/`; `TropicalIsland/Coastline/` | Journey's End; Singing Caverns |
| **Cliff by the sea** | Journey's End | tiles `Island/WalkableCliffOcean/` | the boss |
| **River** | Khari Crossing | tiles `SevenWaters/Tiles/River/` | The Galai Gates |
| Explorer camp | Infested Barrens, Jungle Ruins, Chimeral Wetlands | model `AbandonedExpedition/Camp` | the entrance, added to the scanner |
| Sign | anywhere | `StoryGlyph` icon | the exit of its name, if known |

A place's line is followed by the walk to where it leads, unless that is in view ("Reses muertas, el rastro de sangre sigue
arriba. Sala del jefe: …, 120 pasos, …").

## Trails (2026-11-23)

Asked by the owner on 2026-11-23: the river was said "ya prácticamente cuando la he encontrado"; wanted which way it goes,
whether one is at its beginning, middle or end, a sound perhaps, something easy to follow.

**Why the river came late:** its row matched only `Woods/RedvaleRiver/`, whose tiles lie on the far side of the water in most
of the map; within 60 cells of them lay 1% of the Grelwood's walkable ground, against 21% with the wide river it flows into
(live grid and tiles, 2026-11-23). The whole bank belongs to it: up to 1,300 cells of walk from the entrance.

**How a trail is followed** (rows with `line` in `catalog/map-hints.json`; `HintTrails`, pure `TrailField`, `TrailFollower`,
`RitualProgress`):
- *Met* (a spot of it within the 60 cells a player sees): "Río, a mitad, hacia El valle rojo, 60 pasos, derecha 5, luego
  arriba a la derecha": the part (thirds of the walk between its two ends), where it leads, the scanner's route there.
- *Followed*: its sound (`CueSynth.Trail`, every 1.6 s, at the objects' volume, setting `TrailCue`) from where the walk to
  the goal has got 40 cells on, only while the player nears the goal along it (and 5 s after it is met). Said again at the
  next part.
- *Wrong way* (40 cells farther from the goal) and *left* (3 s off it, with the side it lies): once, 20 s between two
  warnings, not with enemies about nor while the guide leads. Met again only after a minute off it.
- *In the scanner*: "Rastro: río, hacia El valle rojo" in Quest after the quest's own places, so the guide leads along it (on
  its own when the quest names nothing here); and in the glance at the map. Until the goal's ground is seen.

The walk is a breadth-first field from the goal over the routed grid, off the frame thread. The swarm's trail has no spots: it
leads from where the ritual was won. The player does not see the game's own swarm effect (`VoodooRitualDirection`, preloaded
by the altars): its target is worked out, the nearest altar left (poe2way: "the nearest remaining ritual").

**Heard live** (2026-11-23, malgdhler, dev host): Grelwood "Río, cerca del final, hacia El valle rojo, 24 pasos, derecha 2…",
the sound played 4 times; Ogham Farmlands at the waypoint "Camino, al principio, hacia Pueblo de Ogham, 255 pasos…" and on its
way "a mitad…". In the Hunting Grounds, the Traitor's Passage and the Badlands only that the spots are found at arrival (167,
42, 16; 6 brambles). Not seen: the swarm's line (needs a ritual won), Ogham Village's road, the two street rows (files only).

## The level graphs (2026-11-23)

Every campaign area is generated from a few authored graphs (`metadata/terrain/gallows/<act>/<area>/graphs/*.tgr|*.dgr`; 1 to
33 an area): named nodes with their place (`.tgr`: cells; `.dgr`: rooms, stretched) and edges of a ground type between them.
`tools/research/topology/graphs.py` joins each edge type's nodes over all of an area's graphs; what holds in every one is a
rule of the map, which no file says elsewhere:

| Area | Edge | Joins, in every graph |
|---|---|---|
| Clearfell | `burrower_trail` | its end cap and the Mud Burrow (33 of 33) |
| Grelwood | `wideriver_redvaleriver_virtual` | the Red Vale, from the river (12 of 12) |
| Hunting Grounds | `road_fields` | the road to Ogham Farmlands and a dead end, over the signposts (24 of 24) |
| Ogham Farmlands | `RoadUnwalkable` | the entrance, hamlet, Una's house and Ogham Village (32 of 32) |
| Ogham Village | `RoadOpen` | the entrance and the blacksmith's (12 of 12) |
| Mastodon Badlands | `AbyssTrail` | the Lightless Passage (12 of 12); `BonePassage` the Bone Pits |
| Drowned City, Utzaal | `flooded_street` | the entrance, the blockade and Aggorat's door; the boss (6 of 6) |

| Molten Vault | `channel_rooms` | the gold's source, the double block, the boss (2 of 2) |
| Kedge Bay | `island_shoreline` | the entrance, the map piece, the boss, Journey's End (8 of 8) |
| Journey's End | `island_cliffedge` | the entrance, the boss (9 of 9) |
| Whakapanu Island | `tropical_shoreline` | the entrance, Crab Cave, the boss, the caverns (8 of 8) |
| Khari Crossing | `seven_waters_river` | the boss and the Galai Gates (one graph) |

The last five are no cue of GGG's that the press or the guides name; the graph makes them one.

**Checked without entering the areas** (2026-11-23, the owner's characters not to be moved; `tools/research/topology/sweep.py`):
a graph's edge is often a *room's* edge (`flooded_street.et`), so the ground it means is the tile edge its rooms hold and the
others do not: Drowned City's street rooms hold `vaal_streetwall.et` in 38 of 41 (3 of 45 others), Utzaal's in 38 of 40 (0 of
48), Whakapanu's shore rooms the coastline in 23 of 23 (1 of 68), Kedge Bay's the shore line in 46 of 46 (8 of 43), Journey's
End's cliff rooms `island_walkable_cliff_ocean` in 64 of 64 (0 of 54). The `.tdt` files of the area's tile set that name that
edge give the folder of the row. A unit test keeps every goal a label of the landmark table. The goals: the rooms of each graph node
force the tag of the goal's tile (Utzaal's `boss_01` forces `atziri_entrance_metatile`, Aggorat's door; the Molten Vault's
`boss_channel_end`, the Drowned City's `atziri_transition`, the Badlands' `abysshole`, Whakapanu's `cove`/`crabcave`); Kedge
Bay's exit and the Farmlands' village gate hold no tag, the gate was seen live. Qimah's riverbed was left out: its landmark
table names the arena's way in "Entrance". Unproven until entered: how the trails run there.

**The road's two ends** carry the same tile, `Bramble_Cnr_transition_01`. The three dead-end rooms (`road_end_01..03`) hold
5 to 9 `HuntingGrounds/Bramble/Bramble_*` doodads, the two entrance rooms none, and doodads are listed at arrival: on
2026-11-23 the west end had six within 160 cells, the south one none.

**Freythorn's altars** are known from arrival: four `Leagues/Ritual/RitualRuneObject` entities, at the altar places of its
three ritual rooms (by their checkpoint totems, `CanopyBundle_01`) and at the arena's centre.

## Checks and limits

**The side is the mod's, not the game's:** where the walk to the goal leaves the seen ground, as the lost hint. The goals are
judgement: nothing in the files ties the bulls to a boss.

**The camps are an approximation.** The transition object holds a shared state `entrance_revealed` whose client script runs
`RevealMinimapInRadius(80)`, but what sets that state is in the server scripts. `TroubledCampLandmarkTitle` fires at radius 50 and
stops on quest flag `JungleOutskirtsLandmarkComplete`, so the reveal may need more than arriving. The mod adds the entrance
when the player reaches the camp: check it in Act 3.

Left out: arrival-room caravan tracks (they mark where the player came from), Khari Crossing's wayfinders (no target
known), the line effects (`direction.ao`, `fire_direction_01.ao`, `ritual_direction_01.ao`: spawned by server script), title cards.

## Sources

- GGG: "Streamlining the Campaign in Return of the Ancients", forum post of 2026-05-13, thread 3931502; 0.5.0 went live
  2026-05-29. Its only readable sentence is "implemented some changes to the campaign in Path of Exile 2 to make
  navigating areas easier"; the examples are images or a video that were not retrieved.
- Press on that video (the per-area list below): GameWatcher (`gamewatcher.com/news/path-of-exile-2-s-return-of-the-ancients-update-0-5-0-adds-environmental-cues-to-streamline-campaign-navigation`),
  Kami Labs (`kami-labs.fr/en/path-of-exile-2/poe2-return-of-the-ancients-refonte-campagne/`). Acts 1 to 3 and endgame
  only; no wiki list per area was found; no later patch note extending it was read in full.
- Game files: the bundles (4.27M paths), read with LibBundle3.
- Live, 2026-11-05, on the Mercenary with dev command `paths`: the section "Seen live" below.

## The cues

| Area | Cue (press) | Points to | In the files |
|---|---|---|---|
| Grelwood (G1_4) | River, upstream | Red Vale entrance | not found |
| Grelwood | Glowing roots | Tree of Souls | `Objects/TreeRoot` entities from `tree_root_01..03.arm`, `BreadcrumbTreeRoot01..03.ao` |
| Grelwood | Mushrooms | Grim Tangle entrance | not looked for |
| Hunting Grounds (G1_11) | Signpost on the road | Ogham Village (or Farmlands) | `Metadata/NPC/Four_Act1/HuntingGroundsSignpostGlyph`, row 964 of `Npcs`, "Pueblo de Ogham" |
| Hunting Grounds | Slaughtered cattle | The Crowbell | `Rooms/DeadBull_01/02.arm`, `Objects/DeadBull.ot/.ao`, eight blood footprints over about 55 cells |
| Hunting Grounds, Freythorn, endgame | Locust trail after a Ritual | Freythorn, the next Ritual | not looked for |
| Traitor's Passage | Maraketh parchment at a fork | Balbala, the exit | not looked for |
| Titan Valley | Titan heads | Entrances, a seal | not looked for |
| Act 3 jungle | Explorer camps | The next area, revealed on the minimap | not looked for |

Also in the files, not in the press: Clearfell `cow_slash_trail.arm`; Act 2 hub `Doodads/Desert/CaravanTrails/<Dest>/`
(wheel tracks, footsteps, one set per destination); Mastodon Badlands `Breadcrumbing/Abyss/direction.ao`, the only place
a path names the feature; Azak Bog `breadcrumb_direction/fire_direction_01.ao`, spawned by script.

## What the mod can get

- **No hint table.** No dat table, tile tag or folder lists them: each cue is an asset of its own, listed by hand.
- **The sign is one of 51 story glyphs** (`StoryGlyphBase`), not the only one, as read here at first: many stand in
  `entrance_`, `exit_` and `boss_` rooms and label that transition, so they are worth a line when near.
- **The sign has no direction in the data.** One NPC id, one text, one model for every post, no arrow variants. The
  placement has a rotation, but GameHelper reads none (`Render.cs:24-25` commented out, none in `Positioned.cs`), and
  it would say how the post is turned, not what its arrow shows. The posts sit on the graph's `post_cnr` road nodes.
- **The sign's rooms are not in the tile layout:** `Road_Cnr_Signpost_01.arm` has the same tiles as the plain road
  room. The glyph is an entity, known within about 200 cells; the mod already says "Letrero: Pueblo de Ogham".
- **The trail is no data either.** The footprints are client effects inside `DeadBull.ao`: no tile, no entity. The bull
  is an entity. Its rooms are tagged `special3`, in the general room pool; no file ties them to the Crowbell's arena,
  so the files do not show that a trail really leads there (inferred from nothing referencing it).
- **The direction can be worked out instead:** the mod knows the destination's tile, so a sign can say which way the
  place it names lies, by the route (`ExplorationMap.WayOn`). That is the mod's answer, not the arrow. For the sign the
  tile is `Bramble_Cnr_transition_01`, which the radar table calls "Ogham Farmlands Or Deadend": check it live first.

## Seen live

The client lists the area's static doodads from the moment it loads, across the whole map, among the sleeping entities
(ids from 0xC0000000): 236 in the Hunting Grounds (up to 1816 cells away), 1798 in the Grelwood, 317 in Clearfell. They
share paths such as `MiscellaneousObjects/Doodad`; `Animated.ModelPath` names the `.ao`. So a cue placed as a doodad is
readable at arrival, unlike what the game's files alone suggested.

| Area | Seen | Where |
|---|---|---|
| Hunting Grounds | `Metadata/Terrain/Gallows/Act1/1_11/Objects/DeadBull`, two; seen as an entity from 135 cells, not from 950 | 656,519 and 792,522 |
| Hunting Grounds | 8 `Crow.ao` and 3 `Featherpile` around each bull, known at arrival: they mark the bull from afar | around each bull |
| Hunting Grounds | `danglebell_01.ao`, `Bestiary*.ao`, `AnimalBed`: the Crowbell's ground | 283,1207; 238-357,932-1234 |
| Grelwood | 411 `FungiCluster*.ao`, all in one 250-cell box; the Grim Tangle's transition tile centres at 1000,1368 | 750-1000,1250-1500 |
| Grelwood | `TreeOfSoulsRoots`, `TreeOfSoulsBodies`, `TreeOfSoulsNailStake1..3`, `SummonAlly`, `Scatter_GrimTangleEntrance` | about 694,1198 |
| Clearfell | 3 `DeadBull_01.ao`, 2 `Slash07.ao` | 1243,844; 388,1434 |

**Said walking past** (2026-11-08, a new Clearfell instance, dev host): the guide walked to the first cattle spot (487,1129)
and within reach the mod said "Reses muertas, el rastro de sangre sigue arriba a la derecha" unasked; after a death the
respawn beside another spot said "…sigue abajo a la izquierda". The cue's place differs per instance (`maphints` lists it).

Press confirmed by this: the mushrooms really are by the Grim Tangle's entrance. Not settled: the two bulls' link to the
boss (136 cells apart, both far from its ground), and the Grelwood's `Objects/TreeRoot` breadcrumbs, not listed from the
waypoint, so entities that load near.

**The sign:** `MainPost01.ao` was not in that instance's list and the sign was not reached (packs killed the Mercenary
twice on the way). A kept log shows the two facts that matter: on 2026-10-2x the scanner said "Ogham Farmlands Or Deadend,
14 pasos" with "Misión. Tierras de cultivo de Ogham, 83 pasos", so that tile **is a dead end there**, not the exit; and
"Letrero: Pueblo de Ogham, 30 pasos, abajo a la derecha 2, luego derecha" with the real exit "81 pasos, abajo a la
derecha 1, luego derecha": the sign stands on the way. The exit's place on a first visit is still unknown to the mod.

## Fixed and variable maps (2026-11-23)

`WorldAreas` lists for each area the rows of `Topologies` it may be drawn from (0.5.5f: the list at +0x60, the graph file at
+0x8; both found by content; extractor `area-layouts`). A graph's nodes stand at fixed cells, so one graph is one layout: 40 of
the campaign's 103 areas (the Waterways, the Black Chambers, the Eye of Hinekora, most of the interlude). A graph can hold
others (`"graph"` nodes): one naming a file is fixed, one naming a group of the master file's `FileGroups` is drawn (the
Dreadnought's single graph holds one of four). The Trials put their rooms together anew; left out.

**Several graphs do not mean a map that changes** (the first version said so; the owner: Clearfell and the Manor Ramparts never
changed). The game draws only from the graphs that fit the area's side of the act's world map, turned or mirrored when need be
(Hunting Grounds met: `HuntingGroundsW_N` mirrored and `E_S` mirrored, both east to north; Clearfell always `felling_S_W_E`,
Halani Gates always `gates_SN`), and that side is the same for a league's every instance (guides: Halani Gates one pattern in
all leagues, turned one way in softcore, another in hardcore and SSF; Arastas one layout a league). Dungeons and dead ends are
free. The rule that picks among the fitting graphs is not known, so what counts is what was met: `catalog/area-maps.json`.

| Evidence | How |
|---|---|
| Instances generated (`tools/research/topology/instances.py`) | the client's `logs/Client.txt`: `Generating level 2 area "G1_2" with seed ...`, 1805 since 2026-10-23 |
| Where things lay in them | `config/known-places.txt` (exits, waypoints, checkpoints, five characters), `config/explored/*.bin` |
| Fixed: at least 5 seeds, every exit at one place | Clearfell (65 seeds), Halani Gates (8), Mawdun Mine (5); Manor Ramparts (8), whose door to the Manor has two places, 12 tiles apart, all else one |
| Varies: an exit, waypoint or stairs at several | 33 areas; Hunting Grounds and Mud Burrow exactly two |

Anything else with several graphs "may change". At run time "the same as last time" compares where the exits lie
(`AreaLayouts.Exits`: the landmark table's tiles named by an area and the transition tiles) with the instances kept in
`config/area-exits.txt`, twelve an area, whatever the character. What lies in the rooms is drawn anew in a fixed map too, and
another league's map may be a turned one.

## The signal seals of El cruce de Khari (2026-11-26)

Asked by the owner in P2_1 ("había un sello… y no sonaba como quest"; "sigue la guía hasta el templo"). From the game's files
(0.5.5f bundles; poe2db "Recruit the Maraketh"):
- **Only P2_1 has them** (no other `Wayfinder*` object in the bundles; the blood trail, river and Ogham road are other cues).
  `Objects/Wayfinder{2,3}Pointer` stand in the fixed rooms `landmark_02`/`landmark_03`; `Wayfinder1Proxy` in `entrance_01`;
  `Wayfinder4` (invisible, no pointer) in `exit_01`, the room of the exit to the Pools of Khatal (`exit_oasis`, placed per layout).
- **The guide** is no entity: using a seal (`StateMachine` `active` 0 → 1, which also disables Targetable) makes the server call
  `UpdatePlayerPointer(angle)` (`wayfinder1.ot`), which turns `Town/WayfinderPlayer.ao`, a compass effect attached to the player.
  The angle is not in the seal's data. The Water Guard's temple is not in P2_1: it is reached through the Pools of Khatal (P2_2).
- **The tracker's words differ from the object's** in Spanish ("roca de señalización" against "Sello de señalización").

What the mod does: a named object of the area's own `…/<area id>/Objects/` folder counts as the quest's
(`EntityCatalog.IsAreaOwnObject`), whatever the tracker says; a seal seen used within a look of the player starts a trail
("Rastro: Guía del sello, hacia …", the trail sound) to the nearest seal still unused, else to the P2_1 row's goal (`"after": "Objects/Wayfinder"`, "Pools of Khatal": the
step "Sigue la guía hasta el Templo…" has no goal in the quest tables, live 2026-11-26), else to the exit the quest's step
leads through, seen or not (`EntityCatalog.QuestExit`:
here the radar table's `Dregs_Transition` tile, "Pools of Khatal", known at arrival; the room `exit_01` holds both it and
`Wayfinder4`) (`MapHintNarrator.Wayfinders`). A seal seen turning used starts a trail, and so does the first seen already
used while none is on; the guide being on is kept per character and area in `config/wayfinders.txt`, so a new instance or
a restart of the mod far from the seals resumes it, until the end of the walk is within a look.
