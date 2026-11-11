# Perfect Timing (Perfect Strike, Snipe, Gathering Storm)

Goal: a sound at the moment the Perfect Timing window opens while the player holds the skill (user 2026-11-11: the
game marks it only on the screen).

## Known

- The three skills are channelled; a release inside the window is "perfectly timed", outside it a plain hit. The game
  shows a visual cue on the character and plays no dedicated sound (forum reports; poe2db "Perfect Timing").
- Window: `perfect_strike_timing_window_base_ms` = 300 (poe2db, Perfect Strike); modified by
  `perfect_timing_window_ms_positive_percentage` (Window of Opportunity −35%, the helm Assailum and the flask
  Opportunity longer); the player's computed value is `virtual_perfect_strike_timing_window_ms` (`GameStats`).
- Animations (game Animation table): `PerfectStrike` 0x1CD while held, then `PerfectStrikeEnd` (plain) or
  `PerfectStrikePerfectEnd` (perfect); `SnipeChannel` / `SnipeChannelEnd` / `SnipeChannelEndPerfect`;
  `GatheringStorm` / `GatheringStormEnd` / `GatheringStormPerfectEnd`. The end's name tells how a release went
  (`PerfectTimingChannel`, pure, unit-tested; the three channel names are in `catalog/game-names.txt`).
- The player's clip clock reads like a monster's ([dodge timing](dodge-timing.md), findings 1-3):
  `AttackReader.Playing` on the player's Animated component, judging none of the fields (a held pose does not
  advance).

## Measured (2026-11-11, yarkey, Perfect Strike, 13 releases in play)

- The channel plays one clip (index 21 of the player's object, 2.016 s long) at rate 0.635, 3.2 s of real time; its
  named events: `footstepright` 0.250, `leeway` 1.470, `max_stage` 1.600, `release` 2.000.
- Released at 1.496, 1.550, 1.761, 1.802, 1.845, 1.866 (twice), 1.950 and 1.957 s of the clip: `PerfectStrikePerfectEnd`
  every time. Held to the end (1.990-1.992, where `release` lets go by itself): `PerfectStrikeEnd`, four times.
- With the cue (18:05, six more releases): 1.475, 1.492, 1.517, 1.559 and 1.580 perfect, and one let go early at 1.342,
  before `leeway`: `PerfectStrikeEnd`.
- So the window runs from `leeway` to `release`, 0.53 s of clip, about 830 ms at this attack speed.
- The 300 ms of the stat is not that span; what it measures is not known.

## The cue

`PerfectTimingWatch` plays `CueSynth.PerfectTiming` (one hammer blow on an anvil, 587 Hz bar modes, 0.16 s) the frame
the clip passes `leeway`, or `max_stage` in a clip without one (`PerfectTimingWindow`, pure, unit-tested); setting
`PerfectTimingCue`, glossary key `perfect_timing`. It logs each clip once with its events and each release with where
the clip stood and which end followed ("Perfect timing: …"), so Snipe's and Gathering Storm's clips can be read from
a session's log the same way. Heard in play by the user the same evening: five of six releases after it were perfect,
90 to 170 ms behind the blow.

The user picked A (2026-11-11), from candidates A (587 Hz, in the build), B (392 Hz) and C (784 Hz) beside the shield, parry,
dodge, cull and radar sounds on https://claude.ai/artifact/EoUvrHP4BeZyJiEDixyBLq (rendered with `CueSynth.PerfectTiming(rate, hz)`).
