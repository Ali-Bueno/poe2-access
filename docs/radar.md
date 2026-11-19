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
  pips of 35 ms, 30 ms apart, a little lower downward (`RadarSynth.WithUnexploredTail`). The ray's own line, up to its
  first wall (a blocked run over 7 cells; a small obstacle is stepped over), must leave the seen ground onto ground the
  player can reach within 2 × the 60-cell reveal radius (`ExplorationMap.LeavesTheSeen`, read from the seen map at the
  moment of the cue). Walls were once ignored: a closed nook in the Mawdun foundry was marked toward the unseen hall forty
  cells of wall behind it (owner, 2026-11-16). Pockets count only once nothing
  else is left. Setting `UnexploredMark`.
  Until 2026-11-11 the test was a frontier place of the scanner within the ray's quarter (±45°) and 180 cells: walking
  a straight line reveals a strip 120 cells wide, and its edge, 60 cells to the side, lies 22° off the line 157 cells
  back, so the ray pointing where the player came from was marked ("up is unexplored", then "down is"; user). On the
  line alone, the way back is marked only when the player came more than 30° aslant of it.
- **Exploring a large area without the scanner (2026-11-12).** The user, of Act 2: "iba a un sitio sin explorar y era algo
  sin salida"; in the play log "Zona sin explorar" was asked for 50 times in 77 minutes in La ciudad perdida and 34 times
  (plus 43 nooks) in 62 in the Valle de los titanes. Two causes, both fixed in code (unit-tested; heard live only in the
  Ardura caravan: "Zona sin explorar", "Zona pequeña sin explorar", two nooks, in that order, no slow tick in 75 s):
  - *The size of unseen ground was taken block by block.* Two unseen blocks side by side were one ground, so a corridor
    ending at a wall with unseen ground behind it was "ground that goes on". `ExplorationMap.Survey` now joins unseen
    ground cell by cell (four neighbours, as the walk; a second pass over the walk's own array), and a place carries its
    `UnseenSize`: pocket under half a reveal disc (88 blocks, as before), small under two discs (353), large from ten
    (1767). The last two are first guesses, not measured against the game's areas. The scanner lists ground that goes on
    first, then small, then nooks (`ExplorationMap.Rank`), and the radar's mark and the lost hint's fallback take the
    best rank left (`MarkFloor`). The search's time is logged when it passes 250 ms ("Unexplored ground worked out in").
  - *A dead end is new ground,* so `LostWatch`'s count started again at each and the hint came once in two days.
    `LostWatch.Explored` adds the area's own clock, the time moved through it fighting or not: the sound after
    `LostHintAreaMinutes` (10) and every 3 minutes of moving, and after `LostSpokenHintMinutes` (20) the way in words
    (`EntityCatalog.WayOnCell`, `DescribeWayOn`: where the walk to the objective or to an unfound boss room leaves the seen
    ground; with no such goal only the sound, toward the nearest ground that goes on). 0 turns either off.
  - *Exploring by walking alone* (the user, after the two above: "que sea dinámico, que yo pueda explorar simplemente
    caminando por el mapa"). The radar's mark has two forms: two pips onto ground that goes on, one pip onto ground that
    soon ends (`ExplorationMap.UnseenAlong`: the largest the ray meets within reach; small is under two reveal discs;
    nooks only once nothing else is left; `RadarSynth.WithUnexploredTail(pips: 1)`, volume and glossary line
    `radar_unexplored_small`). And `LostWatch.OverSeenGround`: after `UnexploredHintSeconds` (30) walking with not one
    new block, the hint's sound toward the nearest unexplored ground of the best rank (`EntityCatalog.UnexploredCells`),
    again every 30 s; the objective is not used. Unit-tested; live only the settings and the glossary ("radar_unexplored_small
    1" by the `sounds` command): a town has no lost hint, and no ray met unseen ground in the caravan.
  Not built, offered to the user: a spoken look at the map on a key ("seen 40 percent; much left north and east"), and a
  word when a branch walked into soon ends.
- **Doors not opened** (2026-11-14; the user, of the Lost City: the way to the Buried Shrines "estaba bien escondido dentro de
  la ciudad"). The ground behind a shut door is out of reach, so it was no unexplored ground: nothing marked it, the explored
  share fell from 83 to 65 percent as doors opened, and the door itself had no sound. A door the player opens, seen shut and not
  opened since, is now a place still to explore (`ClosedDoors`, kept for the area; forgotten once opened or gone from beside the
  player): the scanner lists "Puerta sin abrir" after the ground that goes on and before what soon ends (`ExplorationMap.Rank`),
  the glance at the map names it among its stretches, and the hint over seen ground leads to the nearest one once no ground
  goes on (`EntityCatalog.UnexploredCells`). Near it, its own sound ([game audio](research/game-audio.md#a-door-to-open-2026-11-14)).
  Unit-tested; not walked in a game yet. Not done: the radar's own reading of a door on a ray (D2R's double knock), and doors
  kept between visits to an area.
- **Hint when lost** (`LostHint`, `LostWatch`; settings `LostHint`, `LostHintSeconds`, at the object sounds' volume): the
  D2R mod's "unexplored room" recording (supplied by the user for D2R, resampled to the mixer's 44.1 kHz, shipped in
  `assets/loops/`) is not a constant loop any more (user 2026-11-05: the radar is what explores). After 180 s spent
  walking with no new block seen, it plays for 4 s on the side of the way on, panned and pitched like the guide, then
  stops, and the count starts again. Fights, standing still, menus, towns and hideouts do not count, and it stays silent
  while the guide leads. Both times are first guesses, to tune by ear.
  Live (2026-11-08, Clearfell, `LostHintSeconds` 3 for the check): "Lost hint toward cell …" walking back over explored
  ground, and the `sounds` dev command heard the `unexplored_room` bed while it played; at 10 and 30 s it never came in
  Clearfell, since every monster within 1300 units stops the count and the explored stretches between packs were short.
  The radar's mark (`radar_unexplored`) played 41 times on the way in.
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
- **Map hints** use the same walk for their side (`EntityCatalog.SideToward`, [map hints](research/map-hints.md)).
- **No way in the scanner.** Until 2026-11-05 it listed "Way to …" for quest places and boss rooms not seen. The user
  found the Manor with it in four minutes after 40 lost, then judged it too easy, so the way is only heard through the
  hint: a quest place not seen is not listed, unless the game's quest icon marks it. The game's own exit, once in reach,
  is listed as any exit.

## The map's shape: dead ends, forks, narrow passages (2026-11-16)

Asked by the owner on 2026-11-16 ("aún siento que me quedo atascado en algunos sitios, o me cuesta encontrar salidas"; ground
that seemed to end had "una salida súper estrecha"). `MapTopology` works out the area's shape once, off the frame thread, from
the whole walkable grid at full resolution (0.4 s on the Manor Ramparts' 8.4 M cells; not for grids over 24 M cells): its
skeleton's forks, rooms and ends, the bridges between parts and what lies beyond each, and the passages at most 8 cells wide
that join two parts ([study](research/map-topology.md)). A gap of one cell is a way on: connectivity is never coarsened.
`EntityCatalog.Topology` holds it and counts the goals a dead end must not hold every 2 s: exits, what the objective names,
boss rooms not found, doors not opened. **What a sighted player sees, no more:** a dead end is told only when a look shows it
whole (its side at most two reveal radii, its ground at most two reveal discs, `EntityCatalog.SeenWhole`); a larger one is
walked, as anyone would. The owner turned down help levels and a rush mode: the guide is not always on.

- **The radar's one pip** now means unexplored ground in such a dead end, by the shape (`Radar.LeadsToDeadEnd`, probed 30 cells
  along the ray); two pips, unexplored ground otherwise. The size of the unseen ground alone (`UnseenSize.Small`) no longer
  makes one pip: it called rooms whose way on was a narrow gap "soon ends". No new sound (the owner: two marks would be
  duplicates).
- **Forks** (`ForkNarrator` over pure `ForkTracker`, setting `AnnounceForks`): walking into a fork or a room with three ways or
  more that are not nooks says it once an area, after what is being said: "Cruce: sigue arriba y a la izquierda, sin salida a
  la derecha, explorado abajo". The way the player came by (within 60° of where they were 20 cells back) is not told; a way is
  explored when the cell 40 cells along it and the next fork are seen. Nothing is said when no way is new; a fight holds a fork
  back while the player is still in it.
- **Places named, and come back to** (same day, the owner lost in Ogham Manor: no way to tell a loop walked twice): each fork
  of three ways or more and each room is named on the first visit after the most telling map piece in it ("Salón de baile",
  "Zona con chimenea", "Cruce con escaleras de bajada"; a name given twice is numbered), else by kind and order ("Cruce 3",
  "Zona 2": an open place is a "zona", since the map's rooms are as often a stretch of desert as a hall).
  Coming back after going two looks away says "Cruce 3, ya estuviste aquí" and the ways still new; away is measured from where
  the player last stood in the place, not its centre (a desert is one room hundreds of cells across: walking on through it and
  a step out to a path's edge said "Sala 1, ya estuviste aquí", the owner 2026-11-16). A way counts as explored
  once the next place along it and the first stretch of each of its ways were seen (a look from the fork reveals 60 cells of
  every way, so the way's own first cell told nothing).
- **Going round in circles** (the owner, 2026-11-18: "cuando estoy dando muchas vueltas… que diga zona más cercana sin
  explorar, a distancia tal en dirección tal"): a "ya estuviste aquí" said after `UnexploredHintSeconds` of walking without one
  new block of ground (`LostWatch.SinceNewGroundMs`, which the hints do not reset; `LostHint.Circling`, so it needs the lost
  hint on) goes on "Zona sin explorar más cercana, 40 pasos, arriba 3, luego a la derecha" (`EntityCatalog.DescribeNearestUnexplored`:
  the nearest place of the best rank the lost hint would use, with the scanner's route). Steps are said here at the owner's
  word, although distances in speech were turned down on 2026-11-16: this line is asked for, the rest still has none.
- **Named rooms** (setting `AnnounceRooms`, [rooms](research/rooms.md)): a room the game's layout names (Ogham Manor's
  chapel, wine cellar, throne room…) is said on walking into it, "ya estuviste aquí" when come back to; a fork inside it is
  named after it, in one line ("Capilla: sigue a la izquierda").
- **Rays that follow the map** (setting `RadarFollowsMap`): the four rays turn to the screen's diagonals where 85% of the ways
  of the forks within a look run diagonal on screen (a grid-built map: the Manor's corridors run diagonal under the camera),
  and back at 60% or less; said twice running (1 s apart) before turning, and a turn starts the readings afresh. An opening
  beside a corridor walked toward does not turn them (the owner). Diagonal rays pan 0.7 to their side; the upper two breathe
  as ahead, the lower two as behind (`RadarSynth.Diagonal`).
- **Exits that sound** (`ExitBeacons`, `ExitSoundSet`, `ExitScatter`, `ExitMaterial`, `TerrainBiome`; setting `ExitBeacons`, at
  the radar's volume × 0.5): the owner, after the forks: "landmarks inmersivos, pero que a la vez ayuden al jugador". Every
  exit of the rooms and forks within 80 cells is an ambient emitter in its doorway (the place's edge + 6 cells, at least 18),
  the nearest four heard, louder nearer (fade²), panned and pitched by `ScreenProjection.Voice`. Its material: a map piece
  within 30 cells along it (bridge, walkway, stairs, ramp, door, gate, dock), a narrow passage, else the setting of the
  tile 30 cells along (`TerrainBiome` from the tile file's folder: interior, cave, water, desert, city, forest). Each emitter
  is a bed (one of the material's, from a random point) and events of the material's pool at uneven gaps
  (`ExitMaterial.EventGapMs`), never the same twice running, pitch ±4%, level ±3 dB (round robin: "más natural y no tan
  repetitivo"). Explored: the muffled copy (1.5 kHz low-pass) at 0.45; a dead end a look shows whole: silent; a fight: 0.35;
  menus and the option off: stopped. Sounds from `sounds/exits/<material>/bed_*.wav, event_*.wav` (the game's own
  recordings, made on each install by the launcher's `sounds` step from the committed recipes `catalog/exit-sounds.json`: the 22
  audition sets, A and B of each of 11 materials, used together; [game audio](research/game-audio.md#exits-ambient-emitters-2026-11-16)). Candidates and their sources: the
  [audition page](https://claude.ai/artifact/EKpLcz26HBuxcLB5op3N7p).
- **Narrow passages:** "paso estrecho" when walked into, as a map piece (`PlaceNarrator.MoreKindAt`).
- **Pieces ahead** (`LandmarkAhead`, setting `AnnouncePlacesAhead`, **off**): stairs, doors, bridges, narrow passages… named
  with their side as they come within a look. The owner: the distance was too much speech, the sounds are what to learn; the
  pieces are named when walked into.

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

## The parts of the map

Asked by the owner on 2026-11-14 (lost in large maps; the third ritual of Freythorn lay in the last unexplored room
tried): the player's dot on the large map, in words. Walking into another third of the map says it with how it stands,
"Top right corner. Explored 4 percent", or "Middle of the map. Already explored" once only nooks are left unseen in it
(`MapPartWatch`, `ExplorationMap.Parts`, setting `AnnounceMapParts`). The part changes once the player is a look (the
reveal radius) past the line, so a walk along it says nothing twice, and has stayed in the part a second and a half
(a walk across the corner where four parts meet said two within half a second, the owner's first session); a fight
holds the line back until it is over. Not on a Trial of the Sekhemas floor, whose rooms are grounds apart: thirds of the
floor are no parts of a room ("Centro del mapa" was said there, the same session). A map
whose third is narrower than two looks has no parts that way: a corridor map has three, a boss arena none. A part with
less reachable ground than a nook is the fringe of its neighbour and is never listed.

The glance at the map (left trigger held, R3; `EntityCatalog.MapGlance`) is the same look on request: the explored
share, the part the player is in, the parts with ground that goes on and their share seen, then the scanner's first
three unexplored stretches with their way. Standing still was considered as the trigger and dropped for the button
(the owner): a stop for anything else would have spoken.

**Parts finished** (2026-11-19, the owner: "ya exploré toda la parte derecha, aún falta la izquierda", without a key): a part
that had ground going on and now has only nooks left is said once, with the parts still to explore, "Completado: Borde
derecho. Falta: Esquina superior izquierda, Borde izquierdo", or "Todo el mapa explorado" (`MapPartsDone`, read every 2 s
in the same tick and setting as the parts entered; how the map stands on arrival is no news; a fight holds it back).

Whether the steps could tell road from plain ground was looked into and left: [ground types](research/ground-types.md).

## Left behind

Asked by the owner on 2026-11-19: the radar tells what lies ahead, nothing what is left behind ("saber cuando estoy en una
zona que ya exploré, pero falta explorar el rincón que quedó ahí pequeñito desapercibido"), with no key and no constant
sound; the exit beacons stay as they are. Unseen ground that soon ends (a nook or a short branch: `UnseenSize` Pocket or
Small, joined cell by cell) whose edge came within 1.5 looks (90 cells) of the player and was not seen sounds once, from
its side (`ScreenProjection.Voice`), when the player is two looks (120 cells) from it: `LeftBehindWatch` (pure) over
`ExplorationMap.SmallUnseenNear` (the edge blocks within reach, from the last frontier's survey, uncapped: the scanner's
eight places put nooks last), played by `LeftBehind` at the radar's volume. Each nook once an area: telling one quiets
its whole edge within a look; two nooks left together are 3 s apart; a fight holds it back until it is over; a jump (a
portal, a waypoint) forgets what was near. Ground that goes on is not told (it is the normal frontier, and forks and parts
already say it); nor are towns, hideouts and a Trial's floor (Sekhemas or Chaos). Setting `LeftBehindCue`; sound `radar_left_behind`
(`CueSynth.LeftBehind`): a far bell, the owner's pick of four on the [audition page](https://claude.ai/artifact/PmA3oePRSbyCUFd86V3iMm).

## The minimap's arrow (tried and removed, 2026-11-18)

A sighted player sees an arrow at the minimap's edge toward a boss room or a quest mark while within its icon's pointer
reach (MinimapIcons +0x12, read per icon by `MinimapIconLabel.PointerCells`; a boss room known only from the tiles takes 400
cells for a campaign boss, 250 for a map boss's house). On 2026-11-18 a loop (`CueSynth.Halo`) played from that mark in a straight
line while walking, and was released in v2026.11.18. The owner took it out the same day: "el escáner ya lo anuncia y se puede
trazar una ruta hacia el punto de interés… suena mal, no me guía hacia la dirección a la cual tengo que ir". A straight line
through walls is no way to walk; the scanner's entry and the guide's route already cover it. Do not offer it again. What is
left of it: the pointer reach read per icon, which now bounds where a character is listed out of town
([minimap](research/minimap.md#scanner-rule)).

## Not ported yet

A door on a radar ray (no door bit in PoE2's walkable grid; a shut door sounds as an object instead, see Unexplored ground), small-obstacle timbre, wall repeat, the "inside unexplored room" cue. Wall tones (LeAccess `reference/navigation-radar.md`) are the alternative style for later.

## Settings

`Radar` (on, for every read player), `RadarVolume` and `SoundVolume` (percent, 0-200). Silent whenever a panel, menu or dialogue has the
focus, the game window is in the back, or the player is dead.
