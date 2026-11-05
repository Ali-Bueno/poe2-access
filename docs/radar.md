# Navigation radar

Port of the D2R mod's radar (`d2r-access/docs/radar-readings.md`, `radar-redesign.md`). Code: `Radar` (service),
`PassageScan` (ray scan), `RadarSensor` (state machine), `RadarSynth` (sounds).

## What the player hears

Four rays along the screen's up, left, right and down (the stick directions). Each reads the walkable grid:

| Reading | Sound | Direction |
|---|---|---|
| Wall (the way ends within ~half a second of walking) | low wooden tock (260 Hz) | left/right by stereo (±0.9), down 14% lower |
| Open floor | filtered-noise breath, 180 ms | rises ahead, falls behind, steady to the sides |
| Exit (an area transition in the ray's quarter, even in the wall ahead), or a doorway on the ray | bright tin (1100 Hz) | as the wall |

A ray sounds only when its reading **changes** and the change is **confirmed**: the player walked ~30 world units with
it, or stood still 200 ms and the reading held 100 ms. Walls first, then exits, then floor; one cue at a time with
50 ms between them. Openings keep 500 ms between them and are dropped while the player only backs away from the wall
last heard. The first reading after a reset (area change, menu, a jump, a poll gap over 250 ms) is silent.

## Geometry

- Ray axes: the inverse of the camera projection (world +100 X/Y probed with `WorldToScreen`), so "up" is screen up.
- `PassageScan`: breadth-first over walkable cells, never stepping back along the ray, within ±2 cells of it, up to
  twice the opening depth. The depth is the connected way ahead, so a one-cell post inside the lane does not block.
- Distances are D2R's scaled by walking speed (PoE2 ~400 world units/s, ~10.9 units per grid cell): wall ≤ 200 units,
  open ≥ 250 (hysteresis between), exits within 750 in the ray's quarter, open ground up to 250 short of them
  (`PassageScan.ExitToward`). 125 aside of four lanes missed a door approached on a diagonal, and a wall under 200 hid one.
- The grid is GameHelper's per-area copy; tutorial barriers that open later are not seen (the scanner reads it live).

## Wall bump

`WallBump` (service) over `WallBumpDetector` (pure; LeAccess `navigation-radar.md` wall bump, D2R recipe for the sound).
The left stick pushes at least half way, the character moves less than 15 world units in 200 ms, and the walkable grid
blocks within 45 units along the push (the stick direction through the inverse camera projection): one 90 ms thud
(sine 150 → 70 Hz over a low-passed noise knock), panned to the push's side, at the radar volume. Once per contact;
re-armed after walking 30 units, turning the push more than 45° or 300 ms without pushing into anything. Sliding
along a wall moves the character, so it stays silent. Setting `WallBump`.

## Local co-op: player 2

With both local players read (`ReadPlayer1` and `ReadPlayer2` on, `Seats.Partner`), a second radar follows local player
2 ([local co-op](research/local-coop.md)): the same rays on the shared screen in another timbre (`RadarSynth.Voice.Second`): a plucked string (Karplus-Strong) for
each strike, in the same order (wall 196 Hz, small obstacle 494 Hz, exit 1320 Hz, lower below the player), and a
whistle for the breath, rising an octave ahead (600 to 1200 Hz), falling one behind, level to the sides (800 Hz). Each
second-voice cue is scaled to the loudness (RMS) of the first voice's cue it stands for, up to a peak of 0.8. Both radars share one turn: one cue at a time across the two.
Player 2's radar is silent while player 2 is dead, alone, or when the first player's radar would be (menus, focus).
With player 2 read alone, the one radar follows player 2 in the first voice.

## Unexplored ground (D2R / LeAccess)

- **Mark:** an open-way or exit cue whose ray points toward unseen ground ends in D2R's tail: 30 ms gap, two 900 Hz
  pips of 35 ms, 30 ms apart, a little lower downward (`RadarSynth.WithUnexploredTail`). The ray's quarter (±45°)
  must hold a frontier place of the scanner's Unexplored category within 3 × the 60-cell reveal radius
  (`ExplorationMap.AnyToward`). Pockets count only once nothing else is left. Setting `UnexploredMark`.
- **Hint when lost** (`LostHint`, `LostWatch`; settings `LostHint`, `LostHintSeconds`, at the object sounds' volume): the
  D2R mod's "unexplored room" recording (supplied by the user for D2R, resampled to the mixer's 44.1 kHz, shipped in
  `assets/loops/`) is not a constant loop any more (user 2026-11-05: the radar is what explores). After 180 s spent
  walking with no new block seen, it plays for 4 s on the side of the way on, panned and pitched like the guide, then
  stops, and the count starts again. Fights, standing still, menus, towns and hideouts do not count, and it stays silent
  while the guide leads. Both times are first guesses, to tune by ear.
- **Pockets:** unseen ground under half the reveal disc (88 blocks), joined to nothing else, is a nook with no way on:
  the scanner says "Unexplored nook" and lists it after the ground that goes on (`FrontierPlace.Pocket`). A frontier of
  one block is kept when ground goes on behind it: a narrow gap can be the way.
- **Where the hint points** (`EntityCatalog.LostHintCell`): what the objective names, else the nearest boss room not
  found yet, else the nearest ground that goes on. For the first two, `ExplorationMap.WayOn` gives the frontier cell on
  the shortest walk there, what a sighted player reads off the map's shape: a straight line would send the player at a
  wall. On the Manor Ramparts (2026-11-05, the user's own explored ground and grid) the nearest of four frontier places
  was a pocket, and following the way reached the Manor's tile in 21 hops; 33 ms a call, once per hint. With no unseen
  ground leading there, or the goal's ground seen, the goal itself. The boss room counts because about 40 areas hold one
  and one exit or none in the table, and the objective seldom names the room (Ogham Village's way on lies behind
  Executioner's Block, found only at 43 steps on 2026-11-05).
- **No way in the scanner.** Until 2026-11-05 it listed "Way to …" for quest places and boss rooms not seen. The user
  found the Manor with it in four minutes after 40 lost, then judged it too easy, so the way is only heard through the
  hint: a quest place not seen is not listed, unless the game's quest icon marks it. The game's own exit, once in reach,
  is listed as any exit.

## Another room

D2R's tin means "the route leads out: into another chamber, or to a level exit"; the breath, "floor, it leads nowhere
new". PoE2 has no rooms, so the port kept only exits and the tin was rare. `PassageScan.Doorway` finds the chamber's half
on the grid, D2R's `Rooms::Doorways` along each ray, as PoE2's walls run at any angle: a gap under 200 units with 125 of
wall either side (a pillar has ground behind it), the ground wider before or after it within 250, and the way going on
a gap's width past it. So a hall to a hall, a hall to a corridor and a corridor to a hall each sound, a corridor itself
does not. The doorway last walked through is taken as read (D2R's way back). Setting `RadarRooms`.

Width from walking 1500 steps of two real outdoor maps with the four rays (2026-10-24 grids, 2026-11-05): D2R's two tiles
scaled (23 cells) gave a tin every 5 and 15 s, 20 cells 8 and 16, **18 cells 12 and 15**, 16 cells 18 and 76; Ogham
Village, houses and all, 8 s at 23 cells and 15 s at 18. Inside a building not measured: a door wider than 200 units stays silent.

## Where the map starts

The area's line ends in the part of the map the player is in, by thirds across and down ("Bottom left corner", "Left
edge", "Middle of the map"; `MapCorner`, setting `AreaCorner`, user's idea 2026-11-05): from that corner the way on lies
the other way. Screen directions, the stick's: the camera draws the grid's corners as the screen's edges. Measured over
all the walkable ground, not what is reachable (a closed gate would shrink the map), leaving out 2% at each end for
stray patches. More than a sighted player knows on arrival, as `WayOn` is. Not in towns and hideouts.

## Not ported yet

Doors (no door bit in PoE2's walkable grid), small-obstacle timbre, wall repeat, the "inside unexplored room" cue. Wall tones (LeAccess `reference/navigation-radar.md`) are the alternative style for later.

## Settings

`Radar` (on, for every read player), `RadarVolume` and `SoundVolume` (percent, 0-200). Silent whenever a panel, menu or dialogue has the
focus, the game window is in the back, or the player is dead.
