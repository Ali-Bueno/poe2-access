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
