# Area memory: same instance or a regenerated map

What the scanner remembers of an area (explored ground, exits, waypoints, NPCs, loot, closed barriers, the way in) and
when it is the same map. Code: `Services/AreaMemory.cs`, `EntityCatalog.AreaEntered`/`Rebind`.

## How the D2R mod does it (studied 2026-11-01)

`D:\code\c++\diablo 2 access project\d2r-access`:

- Key `{seed, level, layout digest}` (`src/explored_store.h:19-23`). The seed is `drlg+0x840`, checked against the PRNG
  init seed. The digest is FNV-1a over the sorted room rects (`src/explored_store.cpp:315-325`). There is no game name
  or difficulty in the key.
- A regenerated map has another seed or layout, so it misses the store and starts with nothing
  (`docs/exploration.md:144-157`). The live layout is checked again before the memory is used (`terrain_service.cpp:202-211`).
- Kept on disk (`explored.dat`, 400 entries) with up to 64 places each: exits with their destination, name and kind.
  Places from an earlier game on the same map are listed before their rooms stream in.
- "Seen" means a room streamed in, "visited" means the player stood in it, and "explored" means visited or drawn on the
  automap. PoE2 loads the whole walkable grid at once, so ours counts 8×8-cell blocks within 60 cells of the player
  (`ExplorationMap`).

## Ours

- **Identity = `AreaInstance.AreaHash` + layout.** The layout is FNV-1a over the tile table (`TgtTilesLocations`: tile
  name and grid spot, in sorted order) plus the grid size (`AreaMemory.Layout`). Doors and barriers change the
  walkable grid, not the tiles, so they leave the layout alone.
- **Bound to the instance the game holds**, not to the loading screen. `AreaEntered(name)` comes from the world-area
  row, and `Rebind` runs from it and every `Track` (250 ms). It switches when the hash or the layout changes, so a new
  instance of the same area (no row change) is caught too. A name that comes in before its instance waits for it, and
  one that lags under 15 s names it.
- Recall with a different layout drops the old memory and deletes its file: the player explores again.
- Disk format v2, `config/explored/<hash>.bin`, 40 kept: layout, way in, explored blocks, remembered places, sighted
  ids, closed barriers. v1 files had no layout, so they are ignored once and then overwritten.

## Evidence

- The bug: before this change the town was once bound to Clearfell's hash. The log of 2026-10-26 00:08 shows
  `G1_town F2ACDBD4` right after `G1_2 F2ACDBD4`: the row event fired before GameHelper loaded the new instance.
  Clearfell's memory went into the town and was saved under the wrong key.
- Live test, 2026-11-01:
  - Town v1 file: "new map".
  - Host restart in town: "same map, 8 places and its explored ground restored".
  - Town → Clearfell: new hash, "new map".
  - Back to town: "same map".
  - Back to Clearfell: "same map, 2 places … restored" (B591E437).
  - Game and host closed, then the character logged back into the still-open Clearfell: "same map, 2 places …
    restored" from disk.
  - The layout is stable across host restarts, re-entry and a relog.
- The town keeps hash 3533F5D5 for days (114 entries, 2026-10-25 to 2026-10-31). Each Clearfell visit after a session
  break had a new hash.
- Unit test `AreaMemoryKeepsTheSameMapOnly`.

## A campaign map in a new instance (2026-11-02)

- Testers heard the scanner forget Clearfell's exits. Each visit after the last instance closed (8 to 15 minutes empty,
  or "Sin jugadores" in the instance list held on the exit) is a new instance with a new hash, so nothing was
  recalled; the layout digest differed too.
- Four Clearfell entity dumps (2026-10-23, 10-24, 10-29, 11-02) put the transition tiles
  (`Clearfell_OldForest_Transition_01` at 1311,552, `FallenTreeWoods_in` at 207,1173), the checkpoints (485,534;
  267,1182), the Mud Burrow entrance (249,1207) and the 5184-cell grid at the same spots; about 130 of its 1270 tile
  names changed each time, and Beira's arena stood elsewhere in the next instance. A second Grelwood instance moved its
  Grim Tangle transition (the Red Vale one stayed).
- **Frame** = FNV-1a over the grid size and the tiles under `/AreaTransitions/` (name and spot); 0 when there are none
  (`AreaMemory.Frame`). Kept in the v3 file (v2 files read with frame 0).
- A new instance that knows nothing takes the newest instance left with the same frame (`AreaMemory.Inherit`, memory
  then disk) and brings only its exits to other areas and its waypoints/checkpoints (`AreaMemory.Framed`), with ids from
  `0xB0000000`. Not the explored ground, places or the boss arena: those vary. An inherited entry leaves once the game's
  entity of that category and name is listed within 400 world units, or when the player is within 500 of its place and
  the game has none there (`EntityCatalog.KeepInherited`).
- Live: town → instance list "Sin jugadores" → Clearfell BF37D51 "new instance of the map of D30D6AAA … 6 exits and
  checkpoints kept": "El bosque singular, 194 pasos", three checkpoints and the waypoint at once; later 46210ECF took 8
  from BF37D51 and the guide walked to the forest exit by it. Grelwood D98686FB → 1EFD95CF: other frame, unexplored.
- The game's exit entity and the radar table's named exit tile were two entries ("El bosque singular" at 3 and 8
  steps): a named exit tile within 250 cells of a listed exit of the same name is now dropped (`EntityCatalog.SameExit`;
  the forest transition is a metatile nine 23-cell pieces wide, its entity 65 cells from the named piece).
