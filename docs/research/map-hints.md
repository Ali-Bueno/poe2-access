# Navigation hints GGG placed in the campaign (patch 0.5.0)

What a sighted player reads off the world, and what the mod can get of it (2026-11-05, 0.5.5e). The radar's own reading
of the map is in [radar](../radar.md). Every area, act by act: [map-hints-by-act](map-hints-by-act.md).

## What the mod says

`MapHintNarrator`, with the cues listed by hand in `catalog/map-hints.json`; dev command `maphints` lists what was found.
Walking within 60 cells of a cue (the ground the mod counts as seen) it is said once, again only 200 cells on
(`MapHintMemory`: the Grelwood's mushrooms are one patch). Every story sign is said, with no row needed.

| Cue | Area | Found by | Side toward |
|---|---|---|---|
| Dead cattle | Clearfell | model `DeadBull_01`, listed at arrival | the boss room |
| Dead cattle | Hunting Grounds | entity `Objects/DeadBull`, near | the boss room |
| Mushrooms | Grelwood | model `FungiCluster`, listed at arrival | The Grim Tangle |
| Glowing roots | Grelwood | entity `Objects/TreeRoot`, near | quest object `TreeOfSoulsRoots`, once the client has shown it: no side before |
| River | Grelwood | tiles `Woods/RedvaleRiver/` | The Red Vale |
| Explorer camp | Infested Barrens, Jungle Ruins, Chimeral Wetlands | model `AbandonedExpedition/Camp` | none: the entrance is added to the scanner |
| Sign | anywhere | `StoryGlyph` icon | the exit of its name, if known |

**Checked live** (2026-11-05, Mercenary, standing at Clearfell's waypoint, dev command `maphints`): 3 cattle spots found, "Reses
muertas, el rastro de sangre sigue arriba a la derecha". The Grelwood's rows only against the logged list of that area: 411
`FungiCluster` models, `TreeOfSoulsRoots` a near entity. Not seen: the roots' and the river's rows, a line said walking, the camps.

**The side is the mod's, not the game's:** where the walk to the goal leaves the seen ground, as the lost hint. The goals are
judgement: nothing in the files ties the bulls to a boss.

**The camps are an approximation.** The transition object holds a shared state `entrance_revealed` whose client script runs
`RevealMinimapInRadius(80)`, but what sets that state is in the server scripts. `TroubledCampLandmarkTitle` fires at radius 50 and
stops on quest flag `JungleOutskirtsLandmarkComplete`, so the reveal may need more than arriving. The mod adds the entrance
when the player reaches the camp: check it in Act 3.

**The road's sign says no side on a first visit:** it names Ogham Village, the exit is Ogham Farmlands, and the mod does not
know where that exit is until it is seen or kept from another instance. `Bramble_Cnr_transition_01` ("Or Deadend") cannot
stand in for it. `AreaTransitionBlockage` doodads mark closed transitions in Clearfell (three, each with an
`AreaTransitionDoodad`, seen live) but none was listed in the Hunting Grounds, so they do not settle it there.

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

Press confirmed by this: the mushrooms really are by the Grim Tangle's entrance. Not settled: the two bulls' link to the
boss (136 cells apart, both far from its ground), and the Grelwood's `Objects/TreeRoot` breadcrumbs, not listed from the
waypoint, so entities that load near.

**The sign:** `MainPost01.ao` was not in that instance's list and the sign was not reached (packs killed the Mercenary
twice on the way). A kept log shows the two facts that matter: on 2026-10-2x the scanner said "Ogham Farmlands Or Deadend,
14 pasos" with "Misión. Tierras de cultivo de Ogham, 83 pasos", so that tile **is a dead end there**, not the exit; and
"Letrero: Pueblo de Ogham, 30 pasos, abajo a la derecha 2, luego derecha" with the real exit "81 pasos, abajo a la
derecha 1, luego derecha": the sign stands on the way. The exit's place on a first visit is still unknown to the mod.
