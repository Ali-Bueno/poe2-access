# Named rooms: the room files found on the live tile grid (2026-11-16)

The game's layouts are built from room files (`.arm`), many of them labelled by their designers ("Chapel", "WineCellar",
"ThroneRoom" in Ogham Manor). Walking into one says its name ("Capilla"), once, and "Capilla, ya estuviste aquí" when
come back to after going far; a fork inside it is named after it ("Capilla: sigue a la izquierda"). Nothing is drawn by
hand: the rooms come from the game's files and are found on the tiles the area placed.

## The files

- **`.arm` room** (versions 29, 31, 35, 36; 15,580 files, 49 unreadable): `version N`, the string count and the strings,
  four header lines whose third is the room's **label** (often empty), the room key `k W H …`, a few more lines, then H grid
  rows of W entries, one per 23×23 sub-tile: `k` + 24 numbers = a slot (`w h`, four edges as 1-based string indices, 8
  unknown, four corner grounds, four heights, the **tag** at field 22, one more), `n` = a cell a bigger slot covers, `s` =
  free, `o`, `f i` = a forced tagged tile. No `.arm` names a `.tdt`: a slot is described by its edge and ground types and
  its tag, and the generator picks a tile of the area's tile set (`.tst`, `.gft` fill tiles) with that key.
- **`.tdt` tile key**, after its UTF-16 string table (offsets in characters): a plain tile has two offsets (none, its
  `.tgt` model), the tag, four edges, width and height as bytes (+28), four grounds (+30). A **variant** ("…_elegant",
  "…_chest", "tmo_dupe") names its parent `.tdt` first, then flags and only the fields it changes; its key is found where
  four edges (`.et` or -1), a size and four grounds (`.gt` or -1) read as such, its tag just before them (a variant without one, its tag
  the parent's); a variant that changes only its model or props is its parent. The first reader, with the variant's key at
  a fixed place, failed 1,301 of the 17,052 tiles read; none fail now. `RoomFiles.Read`.

## Cell classes, and their validation

A room slot and a placed tile get the same class from their keys (`RoomFiles.Cells`, `RoomFiles.Class`):
`tag:<tag>` when tagged; `big:<w>x<h>:<edges>:<grounds>` for an untagged tile over several sub-tiles (size sorted, edge and
ground names sorted: turn-proof); `excavation`; `stairs` (a stairs or ledge edge); `wall` (an edge that is not
`rooms.et`); `floor` (a floor ground; on the placed side also a tile with no edges and no black ground, the blank fill
tiles); black ground and wildcard edges are not compared (a placed ledge piece tests stairs before its wildcard edge).
Decorative tags (`doorclosed`, `fireplace`, `window…`, `crest`) are **soft**: the generator may put a wall, a floor or a
closed door there.

**Validated on the Ogham Manor dump** (`tiles-G1_15-20261108-124222.txt`, 15,871 sub-tiles): the class from the decoded
key agrees with the class the first study read from tile **paths** on every sub-tile that has both (the only differences:
the key leaves unclassed the blank fill tiles before the no-edge rule, the untagged 16×15 `arenaholemid_01` before the big
rule and wildcard ledge pieces). The Python matcher on key classes found the same 14 rooms at the same places as on path
classes; with the big rule MidLevelHole, ambiguous before (94 places), is found once. The C# matcher on the shipped
catalog finds all nine spoken Manor rooms where the study placed them (box corners in sub-tiles): top level hole (32,138),
level hole (36,85), chapel (60,144), boss (72,6), boss entry (36,36), throne room (13,90), wine cellar (36,19), torture room
(48,31), study (66,90), in 26 ms. On the other dumps at hand it finds Riverbank's bridge and Clearfell's boss arena (its
bosses stand inside the box), campsite, bridges and footbridge; nothing in G1_3.

## The catalog: `Poe2UiExtract rooms` → `catalog/rooms.json`

Kept: rooms whose label key is spoken (below) and that have 40 comparable cells, or 12 and a **distinctive** tile (a hard
tag or big tile asked for by rooms of at most three labels in the room's folder: the Manor's thin-wall door is the
studies' and the torture room's). 0.5.5f: 947 rooms (841 before the outdoor labels) of 81 labels (boss 150, map boss 95, arena 49, town 39, bridge 36,
ritual 36, stone 30, lair 28…; the incursion temple 156), 8,687 tiles of the 17,139 in their 123 families, 1,503 classes, 11 s
after the index is open. Format: `classes` (names), `soft` and `softAccepts` (class ids), `families` ("woods/manor":
the first two folders under `metadata/terrain`), `tiles` (`{folder: {file: class}}`, paths under `metadata/terrain`), and
`rooms` (`label` key, `set` folder, `w`, `h`, `families`, `runs`: row, first column, length, class — 4 numbers a run).
Only tiles whose class some kept room uses are listed; any other placed tile is no room's cell.

## Run time: `RoomMatcher`

When `PlaceNarrator` builds its `PlaceMap` (off the frame thread) it matches the rooms: the placed sub-tiles with a catalog
class; a room is tried only when one of its families covers a tenth of them and it is under half of them (a town map is the
area itself); anchored on its rarest hard class (else its rarest non-soft class) under the 8 turns and mirrors, cells of
rare classes checked first; no cell wrong, a twentieth for a room of 100 cells or more; a room that fits more than three
places fits anywhere and is dropped; overlapping finds keep the best scored, at most three per label. `PlaceNarrator.RoomAt`
gives the room at a grid cell (the smallest box holding it).

## Outdoor rooms: by the entities they place (2026-11-16)

Outdoors the tiles are generic ground (Clearfell's `campsite` is five plain forest slots and filler); the room is its
objects. An `.arm` object line is `x y … "model.ao" "class" …`, x/y in grid cells from the room's corner, the class the path
its entity has at run time (a monster's `@0` becomes the live `@2`: the level suffix is dropped on both sides).

**Which classes are entities** (`tools/research/rooms/objects.py`: every `.arm` object class against the paths in the 48
debug dumps): seen live were `MiscellaneousObjects/` AreaTransition*, Checkpoint, Waypoint, Stash, HealingWell*, Door,
`Chests/…`, `NPC/…`, `QuestObjects/…` and an area's own `Terrain/…/Objects/…` (controllers, crafting benches, elevator
transitions). Kept (`RoomObjects`): `Chests|NPC|QuestObjects|Shrines/`, those miscellaneous objects (and GuildStash) and
`Terrain/…/Objects/`, without editor and world-map helpers (MapPin, Marker, PatrolPath, Audio, rail and power-line nodes).
Left out: doodads (client-only), monsters and critters (they walk off their spot), flow and patrol helpers. A class that
never wakes costs nothing: a room is placed only by entities seen.

**Catalog**: a room with a spoken label is kept when its tiles tell it (above) or its objects can: two entity objects, or
one whose class only rooms of its label use among all 15,531 room files (`uniqueKeys`: CampsiteChest is the camps',
BossElevatorTransitionDown the boss entries'). The world map's region rooms (`worldmaps/`, map pins) are left out. Each room
carries `objects` (x, y, key; 3 numbers an object) and the catalog `objectKeys` (paths) and `uniqueKeys`; a room told by
objects only has no `runs`. 0.5.5f: 954 rooms of 81 labels, 947 by tiles, 696 with 9,020 objects of 866 keys (656 unique to
a label), 1.4 MB. Labels added for outdoor rooms: mud (Clearfell's Mud Burrow entrance, "Entrada de la madriguera"),
ritual, stone (standing stones), settlement, landmark well, tree root, runestones, sinkhole, oasis; mausoleum wall and
bossman say the mausoleum and the boss arena.

**Run time: `RoomObjectMatcher`** (pure). Candidates, at map build: rooms with objects in a room folder (`set`) of which a
room has a family covering a tenth of the area's sub-tiles, and under half the area. A family filter per room was too
strict: Clearfell's Mud Burrow entrance names only `woods/mudburrow` (a few sub-tiles), its folder's camps name the
area's `woods/slash`. Live: `PlaceNarrator`'s 2 s anchor poll keeps where each entity of a catalog key was first met (by
entity id; entities wake only near the player); when new ones were met, at most every 10 s, the matcher runs: for each
candidate and turn (`RoomMatcher.Turned`, cells), each live entity of an object's key gives the room's corner, kept only on
the sub-tile grid (within 3 cells of a multiple of 23: rooms are placed on it); at a corner the objects whose entity stands
within 3 cells (Chebyshev) agree. Two agreeing entities place the room, or one of a unique key; a lone checkpoint places
nothing. Strongest first; a place resting on an entity a stronger one took is dropped, two equal places sharing one (a room
whose two objects fit it mirrored) are both dropped until another object shows. Box: the room's W×H sub-tiles turned, at the
corner. Finds overlapping a tile find of the same label are left out (`Merge`); the rest join `PlaceMap.NamedRooms`, so
`RoomTracker`, fork naming and the scanner's seen rooms take them as they are. Log: "Rooms by objects: …".

**Validated on the dumps** (the C# matcher, shipped catalog; residuals 0-3 cells, none at 4-5; at 6 a false boss entry
appears): Clearfell `tiles-G1_2-113444` (waypoint, checkpoint, transition) → the Mud Burrow's entrance `mud` at (207, 1104)
6×6, all three objects agreeing (woods_1 and woods_2 variants, same box), on the burrow trail and fallen-tree transition
tiles; `tiles-G1_2-143449` (same instance) → the lumbermill exterior `town` at (138, 138) 27×18 (the way to the
encampment); `entities-130320` (Ogham Manor) → boss entry by the elevator transition alone at (828, 828), the box the tile
matcher finds. Nothing false on any dump: the Act 1 and 2 towns (41-46 objects live; the town rooms are the area), the
G1_3, G1_4, G1_7 and G1_15 dumps. The Hunting Grounds dumps (`entities-145341…150146`): waypoint and checkpoint fit
`hunting_grounds` as filed and mirrored a sub-tile apart, so it is left (ambiguous). Clearfell's campsite is found by its
tiles (897, 276) in those dumps; its objects (CampsiteChest, CampsiteController, ClearfellPosting3) were never awake in a
dump, so the objects did not have to find it. Tests: `RoomObjectTests` (a made-up catalog; both Clearfell dumps).

## Speech

`RoomLabels.Key`: lower case, CamelCase and underscores split, variant and level words dropped (numbers, `v2`, `lvl3`,
single letters) and role words (entrance, exit, end, mid, st, cnr, cc, cv, sink, source, join, overlay, mission, rare,
checkpoint, special, injector). Only the keys of `RoomLabels.Spoken` are said, by `Announcements.RoomName` (10 languages);
stairs are left to the tiles' own names. `ForkNarrator` with the pure `RoomTracker` (setting `AnnounceRooms`, on): walking
into a room's box says its name once (queued, places speech); come back after going two looks away from the box,
"…, ya estuviste aquí". A fork walked into inside the room takes the room's name (unnumbered) and carries the room's news
in one line; a fork there with nothing new, the room already named on the way in, says nothing.

## Left

- **Outdoor rooms by objects: not seen live.** Only three finds on the dumps (few entities were awake in them); the first
  log lines to read are "Rooms by objects:" and the build's "… more its entities may place".
- Rooms whose objects fit them mirrored (the Hunting Grounds' waypoint and checkpoint) stay unplaced until a third object
  wakes; rooms with labels not in `RoomLabels.Spoken` (landmark, feature, encounter, lore, miniboss…: about 1,100 more rooms with
  objects) are not said; monsters and bosses do not place rooms.
- Not walked live yet; the tile boxes are the room's comparable cells, so a doorway cell may fall just outside.

## Tools

`tools/research/rooms/` (Python, the study's): `arm.py` (parser), `tdtk.py` and `keycls.py` (tile keys and their classes
from a folder of extracted `.tdt`), `dump.py` (a tile dump, path classes), `match.py`/`keymatch.py`/`bigsig.py` (the
matcher on path, key and big-tile classes), `keyval.py` (path against key classes on a dump), `census.py`, `labels.py`,
`coverage.py`, `norm.py` (label keys and their counts), `cut.py` (the test fixture: a corner of a dump and the catalog it
needs), `objects.py` (object classes against the dumps' live paths).
