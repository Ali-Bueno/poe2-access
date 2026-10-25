# Named places: map pieces and props (2026-10-25)

Port of the D2R mod's named places (`d2r-access/docs/places.md`): walking into a named part of the map speaks it
without interrupting ("puente", "escaleras", "puerta"), then the nearest named prop in reach ("carro", "pozo",
"yunque"). Everything comes from the game's terrain files; nothing is drawn by hand.

## Offline: `Poe2UiExtract places` → `catalog/places.json`

The bundles hold 85,689 files under `metadata/terrain/`: 23,252 `.tdt` tiles, 15,570 `.arm` rooms, 6,476 `.tmd`
tile prop lists (plus `.ao` models, `.gt`/`.et` ground and edge types). The runtime key `X.tdtx:A-y:B` of GameHelper's
`TgtTilesLocations` is the bundle file `X.tdt` plus "x:A-y:B".

- **`.tdt`** is binary with UTF-16 strings: the `.tgt` model path, then designer tags on some tiles
  (`Slash_Bridge_01.tdt` → `SlashBridge`, `ccms_bridge2.tdt` → `bridge`, the town's `Encampment_01*` → `lumbermill`),
  then `.gt`/`.et` paths. A tile's kind is the first tag, else its file name, split into words (camel case,
  underscores, digits) and matched as whole words against a list (`PlaceExtractor.TileKinds`: bridge, stairs, door,
  gate, ruins, church, graveyard, hut, tower, temple, arena/boss, entrance, dock, waterfall, encampment, mill, …).
  Whole words keep `cave_wall`, `gatewallborder` or `outdoor` out. 2,950 tiles named on 0.5.5e (door 656, stairs 501,
  arena 222, bridge 218, entrance 216, ruins 214, hut 180, …); in Act 1 only bridges, the encampment and arenas.
- **`.tmd`** (UTF-16 text, `version 8`): one object per line, `x y … "model.ao" … "class"`, x/y in grid cells from the
  tile's corner in the file's orientation. 132 tiles have named props (`PlaceExtractor.PropKinds`: cart/wagon, well,
  campfire, anvil, notice board, mill wheel, tent, stall, statue, altar, fountain, boat, throne).
- **`.arm`** (UTF-16 text, `version 36`): an area or room layout; objects as in `.tmd`, x/y from the room's corner.
  Rooms with props and anchors (objects of class Waypoint, Stash, HealingWell, Checkpoint) are kept: 270.

## Run time (`PlaceNarrator`, `PlaceMap`, `PlaceTracker`)

- The tile grid is read directly: `TerrainMetadata.TileDetailsPtr` is one `TileStructure` per sub-tile, row by row
  (`TotalTiles.X` per row, 23 grid cells per sub-tile): tile file (`TgtFilePtr` → path), `tileIdX/Y` = which sub-tile
  of the **placed** tile (0,0 is always its top-left), `RotationSelector`. GameHelper swaps the ids in its key for odd
  selectors; the ids themselves already count in placed space.
- **Tile props turn with the selector.** Measured in the Act 1 town (`G1_town`): `Encampment_01.tdt` placed with
  corner (68, 138), 18 × 15 sub-tiles; its campfire at file (178, 286) is the room's fireplace at (353, 307) and its exit
  block at file (299, 256) the exit entity at (324, 189): x' = y, y' = height − x, with `RotationSelector` 3. Selector 0
  is taken as the file's own orientation; others are left out and logged ("tile props left out for rotations …")
  until measured.
- **Rooms** are placed at the grid origin when their anchors stand where live entities of their kind do (two, or the
  only one): in the town `town_1.arm`'s waypoint (282, 252) and well (349, 237) are exactly the live entities'
  grid cells. Generated areas place rooms elsewhere; those are not matched yet.
- A kind covering more than a quarter of the area's ground (sub-tiles that are not `*blank*` filler) is the area
  itself (the town is one "lumbermill" tile over 270 of 927) and is dropped. Touching sub-tiles of one kind are one piece for the scanner (category "Lugares").
- Speech: a piece's kind is said once seen on two polls running (250 ms) and held over plain ground within one
  sub-tile of the last piece, so a bridge of several pieces is one; then the nearest prop within 25 cells not said
  yet, said again after going 60 cells away. Delivered as `SpeechKind.Places`, after what is being said; only while
  playing (no menu). Setting `AnnouncePlaces`.

## Open

- Other rotation selectors (2, 3, mirrored) for tile props: measure on a tile whose props include an entity.
- Rooms of generated areas (checkpoint rooms, camps): need the room's offset, e.g. from its tiles' pattern.
- Many props are client-only doodads; interactive things (waypoint, stash, NPCs, chests) are entities and the
  proximity narrator already names them.
