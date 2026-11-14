# Ground types: can the mod tell road from plain ground? (2026-11-14)

Asked by the owner on 2026-11-14: a sighted player follows the path drawn on the ground, since it leads to the places
that matter; could the player's steps say whether they are on it? **Not from the tile data the mod reads today, and
not worth a catalog yet.** Game files only, nothing read from memory.

## What the files hold

- A `.gt` (ground type) is a name and a short row of 0/1 flags: `woods/village/roadfields/roadedge/roadground.gt` is
  `RoadGround`, `1 0 1 1 0`; `woods/canopy/canopy_moss.gt` is `CanopyMoss`, `1 0 1 1`. No material, texture or footstep
  field; the same flag rows serve road and non-road types (97 files read). What the flags mean is not known.
- An `.et` (edge type) is a name, an editor colour and the two ground types it lies between: `woods/road/road_fields.et`,
  `woods/mudburrow/burrower_trail.et`.
- A `.tdt` lists every ground and edge type its tile uses, road and non-road together (`Blacksmith_Large` lists
  `RoadGround.gt` among eight others). Which cells of the tile carry which is in its binary part, **not decoded**.
- An `.arm` room carries its own ground and edge types and a grid of per-cell records:
  `gallows/act1/1_2/rooms/overlay/overlay_road_curve_01.arm` lays `burrower_trail.et` over the tiles. A catalog by tile
  path would miss what such rooms paint.

So "road" can only be guessed from names, per placed tile (23 x 23 cells), never per cell.

## Act 1's outdoor areas

Tiles of each area's `*_tiles.tst` whose name, ground type or edge type is road-like (`roadside`, `roadground`,
`forest_road`, `village_road`, `oldforest_trail`, `roadcliff_trim`):

| Area | Tiles | By name | By ground type | By edge type | Verdict |
|---|---|---|---|---|---|
| Clearfell (`1_2`) | 264 | 85 | 2 | 90 | partial: 17 of 92 rooms paint more road over the tiles |
| The Grelwood (`1_4`) | 197 | 0 | 2 | 0 | no road in the data |
| The Red Vale (`1_5`) | 209 | 0 | 0 | 0 | no road in the data |
| Hunting Grounds (`1_11`) | 199 | 73 | 65 | 62 | good by tile path (`CliffRoad`, `Road`), road and verge not told apart |
| Freythorn (`1_12`) | 54 | 0 | 0 | 0 | no road in the data |
| Ogham Farmlands (`1_13_1`) | 297 | 268 | 17 | 242 | not usable: `RoadFields` names fields and huts too |
| Ogham Village (`1_13`) | 232 | 16 | 6 | 14 | not usable |

Acts 2 and 3 were not measured (by name alone: `desert` 152 of 2,950 tiles, `jungle` 133 of 2,233).

## What would make it possible

The per-cell ground indices in the binary part of `.tdt` and `.arm`, or a per-cell ground type the engine keeps in
memory (not looked for). Either is a reverse-engineering pass of its own; until then the designers' cues the mod already
speaks ([map hints](map-hints.md)) are the nearest thing.

## How it was read

`Poe2UiExtract.exe paths "<game>" "metadata/terrain/"` lists the files and `cat "<game>" <path> <chars>` reads one;
`tools/research/gamedata/dumper` with `EXTS=.tdt,.gt,.et,.arm,.tst,.rs` dumps the `woods`, `forest` and `gallows/act1`
trees (`BIN=1` for the raw bytes). The counts are of each `.tst`'s tiles against the strings of their `.tdt`.
