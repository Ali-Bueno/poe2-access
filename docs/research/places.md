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
  then `.gt`/`.et` paths. A tile's kind comes from its file name only, split into words (camel case, underscores,
  digits) and matched as whole words against a list (`PlaceExtractor.TileKinds`: bridge, stairs, door,
  gate, ruins, church, graveyard, hut, tower, temple, arena/boss, entrance, dock, waterfall, encampment, mill, …).
  Stairs split into `stairs_down`/`stairs_up` by the name's words (`StairsDown`, `stairsup`, `lower`, `upper`) or a .tdt string that is
  only the way (`TransitionDown`); no way, or both, stays `stairs`. Manor and other words added 2026-11-16: ballroom, fireplace,
  window, organ, oven, cell, hole, walkway, platform, chasm, pillar/column, excavation (ramp); walkway, pillar, platform and hole
  drop tiles with a wall/trim/edge/cliff word. Rejected: ledge (684 cliff edges), crevice (206 cave edge pieces), ziggurat
  (outer walls); throne names no tile. Whole words keep `cave_wall`, `gatewallborder` or `outdoor` out. The tags are not used: the binary's other strings
  named 395 tiles against their file names (`ledgesand_abysswall_*` → door, `shoreline_*overlay_*` → boat), and the
  useful tags repeat the name. 2,663 tiles named on 0.5.5e (stairs 445 of which 19 down and 15 up, door 289, walkway 230, entrance 181, bridge 180,
  ruins 154, hut 154, tomb 150, …); in Act 1 only bridges, the encampment and arenas.
- **`.tmd`** (UTF-16 text, `version 8`): one object per line, `x y … "model.ao" … "class"`, x/y in grid cells from the
  tile's corner in the file's orientation. The tile's props are in the `.tmd` its `.tdt` names in its UTF-16 strings
  (`TmdReference`), the same-name file when it names none: 902 tiles name another file than their own. 1,255 tiles have
  named props, 4,106 props (0.5.5e).
  **Prop kinds** (`PlaceExtractor.PropKinds`) are whole camel-split words of the model's file name, first match wins:
  the first 13 kept their keys (cart, well, campfire, anvil, noticeboard, waterwheel, tent, stall, statue, altar,
  fountain, boat, throne) and 28 were added (barrel, crate, table, pillar, urn, corpse, banner, cage, bench, gallows,
  skeleton, ladder, bridge, deadtree, chair, bell, hut, weaponrack, fence, machine, tomb, totem, gate, forge, haystack,
  bed, stump, bookshelf, tower, cannon; counts in the census: crate 693, urn 564, corpse 324, pillar 298, table 268,
  barrel 218, banner 174, skeleton 171, bench 135, cart 133). Left out: objects of class Chest, Shrine, Door,
  HealingWell, Waypoint, Stash, Checkpoint, `/NPC/`, AreaTransition*, *Spawner, *Emitter, FlowBlocking* and the
  Distant backdrops (named as entities or out of reach); models with a word of `NotAProp` (cactus, tracks, overlay,
  roof, trigger, connector, marker, glow, eyes, plane, decal: "BarrelCactus" was 866 false barrels, "KaruiTotemEyes_glow"
  all the totems). Dropped words: `petrified` (239 "PetrifiedTerror" statues of people, not dead trees), `seat`
  (cushions), `market`, `library` and every vocabulary word of a plant or rubble (not a place to name).
- **`.arm`** (UTF-16 text, `version 36`): an area or room layout; objects as in `.tmd`, x/y from the room's corner.
  Rooms with props and anchors (objects of class Waypoint, Stash, HealingWell, Checkpoint) are kept: 270.

## Run time (`PlaceNarrator`, `PlaceMap`, `PlaceTracker`)

- The tile grid is read directly: `TerrainMetadata.TileDetailsPtr` is one `TileStructure` per sub-tile, row by row
  (`TotalTiles.X` per row, 23 grid cells per sub-tile): tile file (`TgtFilePtr` → path), `tileIdX/Y` = which sub-tile
  of the **placed** tile (0,0 is always its top-left), `RotationSelector`. GameHelper swaps the ids in its key for odd
  selectors; the ids themselves already count in placed space.
- **Tile props turn with the selector** (all 8). (x, y) is the prop in the tile file's frame, Wp × Hp the placed
  footprint in cells (sub-tiles × 23; odd selectors swap the file's width and height), added to the placed tile's
  corner: 0 (x, y); 1 (Wp−1−y, x); 2 (Wp−1−x, Hp−1−y); 3 (y, Hp−1−x); 4 (Wp−1−x, y); 5 (Wp−1−y, Hp−1−x);
  6 (x, Hp−1−y); 7 (y, x). Evidence: pairs of the tile's walkable pattern against the live grid over 5 sessions gave
  25-46 unique-correct pairs per selector; selector 3 also matches the Act 1 town measurement (`Encampment_01.tdt`,
  corner (68, 138): campfire at file (178, 286) is the fireplace at (353, 307)) within one cell (that one wrote
  x' = y, y' = height − x, one cell more than the cell-index form above). Selectors above 7 are left out and logged.
- **Rooms** are placed at the grid origin when two of their anchors stand where live entities of their kind do (one
  is any checkpoint near a room's corner by chance: 189 rooms have a single checkpoint): in the town `town_1.arm`'s waypoint (282, 252) and well (349, 237) are exactly the live entities'
  grid cells. Generated areas place rooms elsewhere; those are not matched yet.
- A kind covering more than a quarter of the area's ground (sub-tiles that are not `*blank*` filler) is the area
  itself (the town is one "lumbermill" tile over 270 of 927) and is dropped. Touching sub-tiles of one kind are one piece for the scanner (category "Lugares").
- Speech: a piece's kind is said once seen on two polls running (250 ms) and held over plain ground within one
  sub-tile of the last piece, so a bridge of several pieces is one; then the nearest prop within 14 cells (right beside or over it) not said
  yet, said again after going 45 cells away; one kind remembered nearby is not repeated (a row of barrels is one "barril"). Delivered as `SpeechKind.Places`, after what is being said; only while
  playing (no menu). Setting `AnnouncePlaces`.

## Open

- Rooms of generated areas (checkpoint rooms, camps): need the room's offset, e.g. from its tiles' pattern.
- Many props are client-only doodads; interactive things (waypoint, stash, NPCs, chests) are entities and the
  proximity narrator already names them.
