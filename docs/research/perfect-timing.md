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

## Not known yet

- Where in the channel the window opens (a named clip event, the clip's end, a fixed share of the attack time), and
  whether it moves with attack speed. No source gives it.

## Measuring it (2026-11-11)

`PerfectTimingWatch` logs each channel of the player: the clips it plays with their bounds, rate and named events, and
at each release how long it was held, where the clip stood and which end followed
(`accessibility.log`, lines "Perfect timing: …"). A few perfect and plain releases bracket the window; the cue then
goes at its opening, timed by the clip's clock as the dodge warning is.
