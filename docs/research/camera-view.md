# Camera view and window independence (2026-11-07)

What the mod says must not depend on the window size, its aspect, the display's scaling, the zoom or the monitor. This page
records what was measured and which geometry is left on purpose.

## The camera draws the ground the same at every window size

Dev command `camera` logs the window, the player's place on it and `ScreenProjection.Axes` (100 world units along +X and +Y)
divided by the window height. Measured in the Trial hub (G2_13), standing still:

| Window | Across per 100 world units | Down per 100 | Player above the centre |
|---|---|---|---|
| 2560x1440 borderless, 1920x1080, 1366x768, 1920x1200, 2560x1080, 1280x1024 windowed | 0.059217 | -0.043852 | 0.0566 |

World +X draws right and up, world +Y left and up: the isometric camera with its tilt. Every size gave the same numbers to five
digits; walking moves them about 1.5% (perspective, and the camera trails the player). `CameraView` holds them.

## The camera's own matrix, with no window

GameHelper reads the game's camera matrix (world to clip space, `CameraStructure.WorldToScreenMatrix`) every frame and keeps it
private (`WorldData.matrixSnap.M`); its `WorldToScreen` only maps clip space onto `Core.Process.WindowArea`. `ScreenProjection`
reads the matrix itself (a compiled field accessor) and `CameraView.Draw` turns a world point into screen heights from the
centre: clip y / w spans the height, clip x / w the width, and the width-to-height ratio is the matrix's own (its across and up
columns' lengths with their part along the view column removed, exact for an off-centre view too). So nothing depends on the
window's size, aspect, a minimised game or a missing window rectangle, and the view follows any camera the game sets.

Checked live with `camera` (2026-11-07, 2560x1440, town G2_town): matrix aspect 1.7778 = window aspect, the player drawn at
-0.0563 heights (measured earlier 0.0566 above the centre), and the matrix read straight and GameHelper's window mapping parted
by 0.00000 heights over all 50 awake entities. A matrix is used only while it draws the followed player within the view
(GameHelper's identity before the first frame does not); otherwise the measured constants below stand in. Unit test:
`CameraViewDrawsFromTheMatrixWhateverTheWindow` (16:9, 5:4 and 21:9 projections of one camera give one place).

## What a sighted player sees is a 16:9 screen around the player

`ScreenProjection.InView` (the scanner's enemies and what counts as seen, `EntityCatalog.Sighted`) and `EnemyCues` (the cut at
the screen's edge, pan and nearness) used to test the point against the window: a 5:4 window heard fewer enemies, an ultrawide
more, and nothing at all without a window rectangle. They now test a 16:9 screen centred where the camera centres, from the
matrix (the measured `CameraView` constants without one). At 1280x1024 one guard 0.500 heights below the centre was in the
16:9 view only, as expected of a 5:4 window.

Directions (pan, octants, the radar's rays, route legs) come from the same matrix in screen heights (`ScreenProjection.Axes`),
which follows the camera wherever it turns, and fall back on `CameraView` when it gives nothing usable.

## The game hides captions in a 5:4 window

Five window sizes were walked with the bot and their speech diffed (`sweep.py`, the same panels and presses): 16:9, 16:10 and
21:9 hid nothing. At 1280x1024 (5:4) the character sheet's captions are kept but hidden (flag 0x5026F1 instead of 0x502EF1:
"FUE", "Vida", "Armadura", "Resistencias", all Fontin Smallcaps), and the world map builds no area details box ("Nivel de
monstruos: 22", the area's lore and type are absent from the tree, not hidden). The sheet reads its numbers from the player's
Stats and names a bare number by its hidden caption (`UiSemantics.HiddenCaption`), so it reads as at 16:9; the map box cannot
be read where the game never builds it.

## Pixel tolerances replaced

- Class icons on one row (character creation): sibling places in the game's unscaled units, within half an icon (was 4 px).
- World and checkpoint map markers: sibling centres in the map's own units; "here" within half the player's marker (was 8 px
  on screen, so zoom and resolution changed it). The match to seen checkpoints fits one scale, so any unit works.

## Geometry kept on purpose

- Passive tree link directions and stick pushes (`PassiveStick`): angles and distance ratios of one frame, which pan and zoom keep.
- Reading order of grids and panel notes: unscaled sibling places or rects compared against the items' own size.
- The off-screen filter of the screen watcher: an intersection with the root, no constant.
- `ScreenWatcher.SameLineTolerance` (8): unscaled units, the same at every window size; the sweeps found no row merged differently.

## Not settled

- The monitor off: `SC_MONITORPOWER` put this PC into modern standby (16:47-20:10, 2026-11-07), so it measured nothing; never
  send it on this machine. Nothing the mod hears depends on the window now, but whether GameHelper's frame loop keeps running
  with the display asleep is not known.
- The mod still goes quiet while another window is in front: chosen earlier (no news while the player is elsewhere).
- Windows display scaling: GameHelper declares PerMonitorV2, so window rectangles are physical pixels; not tried at 125-150%.
