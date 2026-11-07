# Cinematic subtitles (0.5.5f, 2026-11-06)

Can the mod read the subtitles of the pre-rendered cinematics? First answered from files; "Seen live" is the first look at one
playing. **MEASURED** = found in the files or the logs; **INFERRED** = consistent, not proven; **OPEN** = unknown.

## What the files say

- MEASURED: no subtitle text or timings on disk. The bundles (4.3M paths) hold no `.srt`, `.vtt` or `.ass`, and no `.dat` table has
  subtitle text; no `.ui` layout or past UI dump has a subtitle control.
- MEASURED: the videos are loose Bink 2 files in `<install>\Art\Videos\` (65 entries): `Act1`, `Act2`, `Act3`, `Act4_Interlude` and
  `Interlude_Endgame` `_Transition.bk2` (28-39 s), `FortressCinematic.bk2` (about 28 s), `cinematic_video.bk2` (about 5 min), `GGG.bk2`.
- MEASURED: the executable imports `BinkLoadSubtitles` and `BinkCurrentSubtitle`, and holds `ShowCinematicState`/`HideCinematicState`.
- MEASURED: the option exists: "Enable Subtitles", `enable_subtitles` in `poe2_production_Config.ini`, on for the user.
- MEASURED: past dumps show Bink videos as picture controls with the `Art/Videos/...bk2` path as a string, none of them a cinematic.
- MEASURED: `NPCTextAudio` ids ending `_Cinematic` (`Tecrod_ArbiterOfAsh_Cinematic`) are voiced lines with text. OPEN: whether they
  play over the videos.
- INFERRED: the game gets the subtitles through Bink and draws the current one. OPEN: from what source, and when each video plays.

## Ways to read them, best first

1. The text as the game draws it: a label in the UI tree, or the string Bink returns from `BinkCurrentSubtitle`.
2. If they are burned into the image there is no data: a narration written per cinematic, keyed by the video's `.bk2` path.

## Seen live (2026-11-06, creating a Druid)

A cinematic plays right after creating a character. Evidence: the F6 dump `ui-tree-CreateCharacterState-20261106-222102.txt` taken
during it, and the screen watcher running through it (a snapshot every 150 ms; a text counts after two).
- MEASURED: it has no state of its own: `CreateCharacterState` holds from "Esperando respuesta..." to `LoadingState`, 22:20:58 to
  22:21:11.9, about 13 s. OPEN: whether the user skipped it; the transition videos last 28-39 s.
- MEASURED: on screen, only `root_control`, `visible_area` and `Skip_Cutscene` (`[0,13]`, also at state+0x2B8) with its label
  "Presiona <<xbox_button_a>> para saltar". No subtitle label in the tree.
- MEASURED: the watcher saw no other text appear in those 13 s, so this is not one unlucky snapshot between two lines.
- MEASURED: no `.bk2` path for the cinematic; the only one in the dump is the class preview
  `Art/Videos/CharacterCreation/DruidShapeshifting.bk2`. The client log names no video.
- INFERRED: `Skip_Cutscene` visible marks a cinematic. Subtitles, if any, do not come through the UI tree: Bink or burned in.

## To check next

- Let one run to its end without skipping: a line heard with no text in the log rules the UI tree out for good.
- Name the video: `LastAccessTime` of `Art\Videos\*.bk2` from PowerShell right after it plays. Git Bash `ls` there overwrites those
  times (it did here, so this look named nothing).
- Burned in or not: a screenshot during a spoken line, or `enable_subtitles` off and on (needs a restart).
