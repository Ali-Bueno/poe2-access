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
studies' and the torture room's). 0.5.5f: 841 rooms of 70 labels (boss 149, map boss 95, arena 49, town 39, bridge 36,
lair 28…; the incursion temple 156), 8,479 tiles of the 17,052 in the kept rooms' 122 families, 1,422 classes, 1.1 MB, 7 s
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

## Speech

`RoomLabels.Key`: lower case, CamelCase and underscores split, variant and level words dropped (numbers, `v2`, `lvl3`,
single letters) and role words (entrance, exit, end, mid, st, cnr, cc, cv, sink, source, join, overlay, mission, rare,
checkpoint, special, injector). Only the keys of `RoomLabels.Spoken` are said, by `Announcements.RoomName` (10 languages);
stairs are left to the tiles' own names. `ForkNarrator` with the pure `RoomTracker` (setting `AnnounceRooms`, on): walking
into a room's box says its name once (queued, places speech); come back after going two looks away from the box,
"…, ya estuviste aquí". A fork walked into inside the room takes the room's name (unnumbered) and carries the room's news
in one line; a fork there with nothing new, the room already named on the way in, says nothing.

## Left

- **Outdoor maps** (Act 1's woods, most map areas): their tiles are generic ground and the labelled rooms are told apart by
  the objects they place (`.arm` object lines: chests, NPCs, shrines, transitions), not by tile shapes. The census counted
  2,142 readable-labelled rooms in 330 folders, 770 matchable by tiles; the rest needs an entity matcher (live entities
  against a room's object lines under the 8 turns), not done.
- Not walked live yet; the boxes are the room's comparable cells, so a doorway cell may fall just outside.

## Tools

`tools/research/rooms/` (Python, the study's): `arm.py` (parser), `tdtk.py` and `keycls.py` (tile keys and their classes
from a folder of extracted `.tdt`), `dump.py` (a tile dump, path classes), `match.py`/`keymatch.py`/`bigsig.py` (the
matcher on path, key and big-tile classes), `keyval.py` (path against key classes on a dump), `census.py`, `labels.py`,
`coverage.py`, `norm.py` (label keys and their counts), `cut.py` (the test fixture: a corner of a dump and the catalog it
needs).
