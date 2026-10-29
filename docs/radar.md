# Navigation radar

Port of the D2R mod's radar (`d2r-access/docs/radar-readings.md`, `radar-redesign.md`). Code: `Radar` (service),
`PassageScan` (ray scan), `RadarSensor` (state machine), `RadarSynth` (sounds).

## What the player hears

Four rays along the screen's up, left, right and down (the stick directions). Each reads the walkable grid:

| Reading | Sound | Direction |
|---|---|---|
| Wall (the way ends within ~half a second of walking) | low wooden tock (260 Hz) | left/right by stereo (±0.9), down 14% lower |
| Open floor | filtered-noise breath, 180 ms | rises ahead, falls behind, steady to the sides |
| Exit (an area transition on the ray) | bright tin (1100 Hz) | as the wall |

A ray sounds only when its reading **changes** and the change is **confirmed**: the player walked ~30 world units with
it, or stood still 200 ms and the reading held 100 ms. Walls first, then exits, then floor; one cue at a time with
50 ms between them. Openings keep 500 ms between them and are dropped while the player only backs away from the wall
last heard. The first reading after a reset (area change, menu, a jump, a poll gap over 250 ms) is silent.

## Geometry

- Ray axes: the inverse of the camera projection (world +100 X/Y probed with `WorldToScreen`), so "up" is screen up.
- `PassageScan`: breadth-first over walkable cells, never stepping back along the ray, within ±2 cells of it, up to
  twice the opening depth. The depth is the connected way ahead, so a one-cell post inside the lane does not block.
- Distances are D2R's scaled by walking speed (PoE2 ~400 world units/s, ~10.9 units per grid cell): wall ≤ 200 units,
  open ≥ 250 (hysteresis between), exits within the open way plus 250 ahead and 125 aside.
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
  (`ExplorationMap.AnyToward`). Setting `UnexploredMark`.
- **Loop:** the D2R mod's "unexplored room" recording (supplied by the user for D2R, resampled to the mixer's
  44.1 kHz, shipped in `assets/loops/`) plays without a break at the nearest frontier place, panned and pitched like
  the guide, from full level near to a quarter at 6000 world units, silent beyond (`UnexploredLoop`). It keeps its
  place while the recomputed frontier still holds it within three blocks, unless another is less than half as far
  (`ExplorationMap.Follow`). Silent in towns and hideouts, menus, dialogue, and while the guide leads. Setting
  `UnexploredLoop`, at the object sounds' volume. PoE2 has no rooms: the frontier stands for D2R's unvisited rooms.

## Not ported yet

Doors (no door bit in PoE2's walkable grid), small-obstacle timbre, wall repeat, the "inside unexplored room" cue. Wall tones (LeAccess `reference/navigation-radar.md`) are the alternative style for later.

## Settings

`Radar` (on, for every read player), `RadarVolume` and `SoundVolume` (percent, 0-200). Silent whenever a panel, menu or dialogue has the
focus, the game window is in the back, or the player is dead.
