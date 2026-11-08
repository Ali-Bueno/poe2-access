# Walking the scanner's routes and the guide beacon live (2026-11-08, 0.5.5f)

Question from the first live run of the day: Clearfell's routes "stopped at tree lines" and the Clearfell Encampment's west
bridge "does not walk through". Is that the mod (grid, search, smoothing), objects the grid does not hold, or the driving
script? Character yarkey (level 22 Mercenary), dev host, `VerboseLogging` on.

## What the walkable grid holds (PROVEN)

`TerrainMetadata.GridWalkableData` (+0xD0 of the terrain struct), two 4-bit cells a byte, is a clearance field: 0 is
blocked, 1-4 the band along every wall (1 against it), 5 open ground. Clearfell's (1656 x 1656 cells): 2 240 013 blocked,
16 705 / 13 776 / 13 752 / 15 142 at 1-4, 442 948 at 5. Trees and rocks are in it: round blocked islands with their 1-4
rings (`routed-*.walkable` from the dev command `terrain`, rendered). Routes take any non-zero cell, as GameHelper's radar.

The dev command `terrain` had written four empty layers since the terrain struct moved (a constant 0x8D0 against 0x8F8 in
0.5.5f); it now reads the struct where the area layout search found it.

## Clearfell (PROVEN: not a mod fault)

- The guide beacon, followed by its ping direction (dev command `beacon`, stick pushed that way 1 s at a time), walked
  from the waypoint to the Grelwood exit (15 400 world units) and back to the Mud Burrow exit (156 steps) through the forest
  without stopping at a tree. Every stop was monsters standing round the character (fixed by shooting them).
- The scanner's spoken route ("43 steps, left 1, then down"), followed leg by leg as a player would, reached Beira's arena
  from the Grelwood exit.
- The earlier run's walker stood still among monsters for the dodge study and fought larvae for most of its time: its
  "tree lines" were not reproduced. Clearfell's layout differs per instance.

**A mod fault found on the way:** in a corridor between two log piles the ping turned the player round four times. The
route is searched again every 700 ms from where the player stands; round a block with two ways of nearly equal length (318
and 347 cells round each side, `dist.py` over the routed grid) the shorter one swaps as the player walks, and each ping
pointed back toward the other side. Fixed by `RoutePlanner.Keep`: the earlier route is kept from the player's place on it
while the player is within 6 cells of it, it is still walkable and ends where the new one does, unless the new one is
shorter by more than 10 % plus 8 cells (a gate that opens). The beacon and the scanner's spoken route use it; walked live
again through the same corridor without a turn back. Unit test `TheWayBeingWalkedIsKeptUnlessClearlyLonger`.

**Spoken direction at an octant edge** (STATUS issue "up 2, then up left" / "up left 2, then up"): the spoken route to
Beira's arena ran at about 199 degrees and alternated "left" and "down left" legs between asks. The scanner now keeps the
first direction it said for the same target until the way is 10 degrees past the octant's edge
(`StickDirection.Octant(…, keep, margin)`, test `ADirectionNearAnOctantEdgeKeepsTheOneSaid`).

## The encampment's west bridge (PROVEN: the game's layout)

- The walkable arm west of the waypoint ends at a broken bridge: the grid is a dead end there (cell 140, 370) and the screen
  shows the bridge collapsed. Walking it reached the end without a stop.
- The encampment has no transition to the Riverbank: its two `AreaTransition` entities both lead to G1_2 (Clearfell), one
  (id 16, cell 325, 190) untargetable behind an `AreaTransitionBlockage` (`Encampment_01_Transition`, the gate the player
  came through). The scanner lists only "Clearfell" as an exit, which is right.
- The Riverbank has no waypoint: its world-map pin offers no "Travel" and holding A on it opens nothing. A character past
  it cannot go back; the Bloated Miller needs a new character (its arena is in the Riverbank).

## Lessons for scripted walking (dev only)

- Follow the beacon (`beacon` dev command → `Beacon aim x,y`), not the spoken legs: a leg is an octant and drifts.
- Shoot (RT) when the cell stops changing: monsters body-block; never stand still among a pack.
- Never stop a script that sends input with a task kill: twice on 2026-11-08 the pad server died with the client and the
  game paused on "Connect a controller". Give the script its own time limit instead.
- The Mud Burrow's Devourer waits in the top-left chamber of the maze (`devourer_bosslight.ao` doodad, `Arena_Blocker_Visual`
  blockers at its two entrances); the tile note "To Devourer" is a path tile in the middle of the maze, not the arena.
- Ogham Manor is three floors in one grid joined by stairs (no walkable link between them): a route across floors is only
  ever partial; the stairs are their own exits ("Stairs") beside the tile notes.
- **Ogham Manor's "Stairs Down" pointed between two floors** (a mod fault): the tile stands on two floors and the landmark
  took the average of all its pieces, a point no way leads to ("Stairs Down, 641 steps, the way stops short"). Named tiles now
  give one place per group of pieces (pieces within two tiles are one place, as the boss arenas): two "Stairs Down", 61 and
  13 957 units away, each where it is.
- **A door the grid held open** (driver only): the driver's distance field, taken from the routed grid at 12:54 while a closed
  door (`Metadata/MiscellaneousObjects/Door`, a `TriggerableBlockage`, untargetable from that side) was still beyond the
  network bubble, led through the door's neck; near it the live grid had the neck blocked and the driver went back and forth
  for its whole time limit. The mod's own route, searched on the live grid, went round it (540 cells, a whole route, "64
  steps, down left 8, then left"), so a player following the scanner is not trapped. The driver now counts no progress
  toward its goal as stuck.

- **Deaths of the evening run** (yarkey, level 22): under Beira after a minute standing still to let her cast, frozen, with a
  ritual cultist's axe on him (the driver beat her on its way back; the casts were audited from the trace); and in Ogham Farmlands (level 12) to a
  werewolf pack while the explorer only walked. A survey script in a level 12 area must fight back; "let the boss cast" needs a
  check of life every few seconds, not one 25 s wait.

Scratch tools (not kept): `walk.py` (beacon follower), `legs.py` (spoken-leg follower), `drive.py` (shortest-way driver over
the routed grid), `dist.py`, `grid.py` (grid renderer).
