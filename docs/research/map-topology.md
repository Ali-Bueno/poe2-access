# Map topology and the terrain's own names (2026-11-16)

Asked by the owner on 2026-11-16: more spatial information while walking ("una escalera hacia dirección tal"), more
landmarks, and help telling ways on from dead ends, so a blind player can explore or rush without getting lost, and
without pressing keys. Two offline studies, nothing read from the running game: the shape of the walkable grid, and what
the terrain files name. Scripts in `tools/research/topology/` (scratch copies; they expect the dumps named below).

## 1. The walkable grid as a graph (prototype, `topo.py`)

Input: the `terrain-<stamp>.walkable` dumps of 2026-11-08 (raw `+0xD0` nibble layer, **low nibble = left cell**, bytes per
row in the matching `.txt`; 0 blocked, 1-5 walkable). Manor Ramparts (G1_15) is `terrain-20261108-124237` (with
`tiles-G1_15-20261108-124222.txt`); three cave-like grids `115707`, `133607`, `134540`.

Method (cells; f = coarsening): 2x2 blocks walkable at 50%; keep the player's component; **fill enclosed obstacles under
2,500 cells²** before thinning (pillars otherwise make pairs of false forks; the radar keeps them); Euclidean distance
transform, skeleton; prune end branches shorter than 1.5 × the junction's half-width + 12, re-thinning, 2-3 rounds;
rooms = cores where the distance is ≥ 24 (area ≥ 1,200, peak ≥ 40), their skeleton collapsed to one node; merge edges
shorter than width + 6; **dead end = a bridge edge whose far side holds under half the area and not the player**, area by
the nearest skeleton pixel (multi-source walk), classes by reveal discs (π·60² = 11,310 cells): nook < 1, small < 3, large.

| Grid | Rooms | Forks (plain junctions) | Dead ends nook/small/large | Python time |
|---|---|---|---|---|
| Manor Ramparts | 6 | 20 (14) | 16 / 0 / 4 | 0.27 s |
| 115707 | 5 | 5 (1) | 6 / 0 / 1 | 0.30 s |
| 133607 (largest piece) | 4 | 3 | 10 / 1 / 2 | 0.21 s |
| 134540 | 4 | 4 (1) | 4 / 0 / 0 | 0.34 s |

- **Manor Ramparts reads as a person would draw it:** corridors, T-junctions, halls, alcoves; 2-3 alcove forks that are
  really nooks, dropped by counting only forks with three ways that are not nooks ("real forks", 12 of 20).
- **Caves give no useful forks:** open ground becomes one or two "rooms" around rock islands; what is useful there is the
  dead-end lobes behind a throat. Corridor width differs by map (median half-width ~12 cells in the Manor, 18-26 in caves):
  room thresholds must come from the map (about twice the median), not constants.
- **Chattiness:** walking the Manor's longest path met a real fork every 2.4 s (median); over the whole skeleton 6-9 forks a
  minute. Saying every fork is too much; only forks with a way not yet walked, once.
- **A dead end may hold the exit or the objective:** the blue regions are bridges by area only. A branch is "no way on"
  only when it holds no area transition, no quest target and no unfound boss room.
- The player's component is not the whole map (Manor 317k of 732k cells; in `133607` the player stood in a 13.5k pocket):
  teleports and waypoints join pieces the grid does not.
- C# port: Felzenszwalb EDT, Zhang-Suen or Guo-Hall thinning, labelling, union-find, Tarjan bridges, a tree pass on the
  bridges for "area beyond this branch" per (junction, branch). Expected 0.2-0.5 s per area at f = 2, off the frame thread.

## 2. What the terrain files name

From a dump of `metadata/terrain/gallows/` (10,128 files) and the Manor's `.tdt`s.

- **Room graphs** `*.dgr` / `*.tgr` (1,321 + 148, text `version 25`): header `Size`, `MasterFile "…master.tsi"`, `Nodes`,
  `Edges`; a node is `x y nEdges <edge ids> "NAME" <rotation> …`, an edge `a b … "rooms.et" … P|V` (`V`/`void.et` helper
  links; edge semantics medium confidence). Ogham Manor (`gallows/act1/1_15/graphs/`, 32 files) has three storeys
  (`master.tsi` `StoreyHeight 16`, `master_midlevel.tsi`, `master_basement.tsi`) and named nodes: `entrance`, `Chapel`,
  `windowend`, `StairsDown1/2`, `StairsUp1/2`, `TopLevelHole`, `MidLevelHole`, `ThroneRoom`, `study`, `WineCellar`,
  `tortureroom`, `boss_entry`, `boss`. **The graphs are templates with variants; which one the game picked, and where each
  room landed, is not in the files** ([places](places.md) "rooms of generated areas" is still open).
- **Room labels** (`.arm`, the line after the size): 2,677 labelled rooms, 780 labels (`Chapel`, `study`, `libraryh`,
  `armoury_lvl1`, `prison_gate`, `throneroom`, `winecellar`; generic ones dominate: `tile_overlay` 377, `mission` 165).
  Room file names carry their openings: `1open`, `2open_cr`, `2open_st`, `3open`, `4open`; Kamasa's library
  `corridors/{st,cc,tri,x,end}_NN.arm`.
- **Tile name codes** describe wall and edge shapes, not corridors: `st` straight, `cv` convex, `cc` concave (Act 3 spells
  them out), `tri`/`quad` three- and four-way joins, `end`/`cap` an end cap (high confidence); `cr` corner (medium).
- **Tile words `TileKinds` misses** (per area tile sets): Manor `ballroom`, `skylight`, `fireplace`, `window`, `organ`,
  `oven`, `cell`, `ledge`, `excavation` (ramp), `hole` (`ArenaHoleMid/Upper`), and the stairs' way: `ThickWall_StairsDown_Cc_01`
  (tag `TransitionDown`), `grand_stairs_lower/upper` (`StairLower`, `Stairs_Upper`); elsewhere `walkway`, `platform`,
  `cart`, `throne`, `ziggurat`, `pillar`/`column`, `chasm`, `crevice`, `stairsup`/`stairsdown`.
- **Height** is not in the stairs' `.tdt` (no cell grid; the mesh is the `.tgt`'s). Up or down comes from the name or tag
  only; the compass way of a stair comes from where its placed tile sits on the walk (section 1).
