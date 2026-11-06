# Cinematic subtitles (0.5.5f, 2026-11-06)

Can the mod read the subtitles of the pre-rendered cinematics? Answered from files only, with the game untouched; nothing here
was seen while a cinematic played. **MEASURED** = found in the files; **INFERRED** = consistent, not proven; **OPEN** = unknown.

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

## To check live

A UI dump (F6) while a cinematic plays:
- A new element in the tree: its label, text, parent, and whether it changes with each line.
- The `Art/Videos/...bk2` path on a control: that names the cinematic and marks its start and end.
- If nothing shows: compare `enable_subtitles` off and on (needs a restart); text that stays is burned in.
