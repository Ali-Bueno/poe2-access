# Dodge timing and gating

Goal: the dodge warning sounds only for an attack that really comes at the player, that reaches the player, that a sighted
player sees, timed by the attack as it really plays. Study of 2026-11-08 (build 26476662, 0.5.5f hotfix) first; the
first study (2026-10-24, 0.5.5e) and the files' hit times follow it, corrected where the new one proves them wrong.

Confidence: **PROVEN** = the game's own code names it (a GEAL getter or a labelled dump) and it reads the same on the five
archived builds (25244054, 25740136, 26261440, 26473991, 26476662), or it was read live and checks itself; **LIKELY** =
code in one build, or live on players only; **UNSURE** = inferred, needs the live protocol below.

## The pipeline today and where it misses

`BossWatch.Tick` (each frame) walks the awake monsters within `WatchWorld` (1400 world units, a circle) of the followed
player; for an alive, not hidden monster with an Actor it calls `WatchAttack` with the animation id (Actor +0x8B0) and the
body gap (centre distance minus both `Render.ModelBounds.X`). A new animation id is an attack start: the kind from the
skill tables (`DodgeTimings.Threat`, "Kinds from the data" below), else `DodgeThreats` on the animation's name, gives
Melee, Ranged or None; the key `monster path|animation id` gives a delay from `DodgeTimings` (the median of the last five learnt delays from
`config/dodge-timings.txt` and the shipped `catalog/dodge-timings.txt`, else `catalog/hit-times.txt`), plus a projectile's
flight (`TravelMs` = gap ÷ the files' speed). The warning (`CueSynth.Dodge`) plays at start + delay − `LeadMs` (500), at
once when untimed, only for rares and uniques (`Warns`), and a rare's melee only within a 150 gap (`MeleeDangerGapWorld`);
a repeat swing warns again one files' swing later. `LearnFromBlow` turns a ≥4% life drop into a delay for the one attack
under way in reach. `EnemyCues` (presence pulses, the reach ping through `ReachGate`, 40 units) is apart: it does test
`ScreenProjection.InView`, `IsInPlay` and line of sight; `BossWatch` does none of these.

| Miss | Evidence |
|---|---|
| No target: an attack at a minion, an ally, a totem or another player warns like one at the player | `WatchAttack` never reads what the action aims at; the Actor's action was not known (below) |
| Ranged attacks are always "in reach" | `inReach = Threat != Melee \|\| Unique \|\| gap <= 150` (BossWatch.cs) |
| Not "on screen": the 1400 circle reaches far past the 16:9 view, and walls are ignored | the measured camera (`CameraView`) shows about ±1060 units across, 715 up and 900 down from the player; 1400 is 500-700 units past the top and bottom edges |
| Speed 1 assumed: a hasted rare lands earlier than warned, a slowed one later | "hasted or slowed ones are not traced" (below); at +25% speed a 1300 ms hit lands at 1040 ms and the 500 ms lead shrinks to 240 ms |
| One time per action, though an action plays one of several clips | the files' time is "the earliest first among an action's animations"; the Miller's DualStrike: 930 in the files, 781 felt |
| Learnt delays are polluted | `config/dodge-timings.txt` 2026-11-07: FungusBehemoth animation 38 (SpellProjectileChaos) 297, 1406, 110, 578, 1281 ms; animation 35 (SpellProjectileFire, no projectile speed in the files, so its flight is in the delay) 3000, 1844, 2578, 2094, 2578; RaisedBranchMinion Melee 453, 140, 469, 219, 172; in the log's 300 "Dodge timing" lines FungusBehemoth's animation 35 alone went from 125 to 3000 ms over 18 blows |
| Repeat swings assumed one swing length apart | `Attack.Swing` from the files; the real position was not readable |
| 1314 attacks warned of are untimed | the files' events are bespoke (below) |

## Findings of 2026-11-08

### 1. Real playback: the AnimationController (PROVEN)

The clip a monster plays and how far it is live in the **AnimationController** component of its *animated object*, not
in the Actor (which is why the first study found no progress in Actor +0x780..+0xB80):

- `Animated` +0x350 is the animated object: an entity-like object of the `.ao` (details path `Metadata/…/X.ao`, its own
  components `AnimationController`, `Melee`, `ClientAnimationController`, `BaseAnimationEvents`, …). Code: the GEAL helper
  0x18CAFC0 ("Requires an Animated Object (or a Game Object with one)") reads `[GetComponent<Animated>()+0x350]`; live on
  the player, two other players and NPCs. (The host's `AnimatedEntityPtr` at 0x280 is stale, exe-layouts.md.)
- `GetComponent<AnimationController>` is 0x153CC0 (name string). Its fields, from the GEAL getters and the component's
  debug dump 0x1D54F20 (labels in quotes):

| Offset | Field | Code (26476662) |
|---|---|---|
| +0x178 | float global speed, "Global Animation Speed Multiplier: "; taken as 1 while +0x1D4 is set | GetGlobalAnimationSpeed 0x2073320 |
| +0x180 | animation data: the .amd and every clip's events | GetTimeToNextEvent helper 0x2056F20 |
| +0x190 | int current clip index, -1 for none | CurrentAnimationIndex 0x207B5B0 |
| +0x1A4 | float position, seconds into the clip, "Current Animation Position: " | GetCurrentAnimationPosition 0x2073720 |
| +0x1B0 | float clip speed, "Current Animation Speed: " | GetCurrentAnimationSpeed 0x2073530 |
| +0x1B8, +0x1BC | float clip start and end (length = end − start) | GetCurrentAnimationLength 0x2073950 |
| +0x1D4 | byte: global speed off | GetGlobalAnimationSpeed |

- The game times its own events this way (GetTimeToNextEvent, 0x2056F20): `(event time − position) ÷ (global × speed)`,
  a rate at or below 0.001 counting as 1. "Playing Animation Speed (incl global)" in the dump is that product. So the
  attack/cast speed of a hasted or slowed monster is in +0x1B0 (or the global), and the time left to the blow is exact.
  `AttackClock` implements it.
- Live (town, 2026-11-08): the player's clip 47 `idle_relaxed_crossbow_01` at speed 1.0, end 6.4 s, position 3.330 →
  3.627 → 3.924 s at 0.3 s intervals; NPC clips 1.6-6.7 s long ([evidence](evidence/dodge-live-2026-11-08.jsonl)).
  Monsters in an attack, a hasted monster's speed above 1 and a slowed one's below: **UNSURE until the live protocol**.

### 2. The clip's own events, from memory (PROVEN layout, LIKELY content)

- Animation data (+0x180): +0x00 the .amd file object (wide path at +0x08; +0x28 → clip table, a vector at +0x38 of
  0x138-byte clips beginning with a `std::string` name; +0x08 replaces that table when set; name lookup 0x1D46F70 also
  tries two override lists at +0x78 and +0x90); +0x10 a vector of one 0x18-byte vector per clip index, holding event
  pointers (event lookup 0x1D47110).
- An event: +0x08 kind (0 = a named StandardEventType), +0x0C time (s), +0x18 `std::string` name, +0x38 its id, +0x40 the
  next event of its kind (sorted by time). The id is the game's hash of the name (0x1D48E10: `h = h·0x1000193 ^ byte` from
  0 over the name up to '#', folded `(h >> 16) ^ h`, top bit set): `AttackClock.EventId`. Live: the player's clip 0
  `aim_crossbow` holds `DownContact` (id 0x858E) and `UpContact` (0xD10A) at 0.0 s, both equal to the hash. So every event
  read checks itself.
- The client parses the `.ao`'s AnimationController `events` itself (the parser 0x1D408B0 loads `events`, `melee_data` and
  `damage_patterns`), so an attack clip's `contact`, the hit-times extractor's first source, should be in the list:
  **LIKELY** (players' .ao carry no events; to confirm on a monster live). Then the warning reads the blow of the very clip
  playing, at its speed, with no table, and any clip with a `contact` is timed even where `hit-times.txt` is not.
- The five-build check reads the same fields in the helper and the lookup on all five builds: helper 0x190, 0x180, 0x1A4,
  0x178, 0x1D4, 0x1B0; lookup +0x10, +0x18, +0x08, +0x38, +0x40.

### 3. The current action: type, skill, target (PROVEN)

`GetComponent<Actor>` 0x153470 → Actor **+0x238** is the current action (null when idle), **+0x2A0** bit 0x2 "an action
under way" (IsCurrentAction). In the action:

| Offset | Field | Code (26476662) | Live (2026-11-08, another player in town) |
|---|---|---|---|
| +0x120 | ushort action type: the hash of an ActionTypes.dat Id, without the top bit; ActionTypes +0x08 keeps the same value | IsCurrentAction 0x2079580, its factory 0x204B8A0 hashes `action_name` | 0xC266 `Interaction`, 0xED4C `DodgeRoll` |
| +0x148 | target entity pointer (GEAL value type 1, a game object), null for none | GetTarget 0x2074BB0 | the NPC `MarakethMystic` (id 18), the Stash (158) |
| +0x150 | target place, two ints in grid cells (the type GetLocation returns from Positioned +0x444) | GetTargetLocation 0x2074DE0 | (389, 240) beside the player at (389, 261) |
| +0xF8 | the skill instance (its stat sets at +0x128, defaults at +0x170) | the stat accessor 0x1980850 | one pointer per skill used |
| +0x19C | byte: the skill's stat set index for the action's stats | GetCurrentActionStat 0x207A320 | 0 |
| +0xF0 → +0x08 | the initiator entity | GetSwingRange's helper 0x20608E0 | — |
| vtable +0x98 | "is a MeleeAttackAction" | GetSwingRange 0x2086130 | — |

- A player's dodge roll kept (345, 246) as its place while the player moved from 335 to 303: for movement skills the place
  is not a destination. For a monster's attack, the target and place at the player when it attacks the player, at a
  minion when it attacks a minion: **UNSURE until the live protocol**.
- So the mod can tell an attack on the player (target = the player) from one on a minion, an ally, a totem or another
  player, and a ground-targeted spell by its place; a projectile flies from the monster through the place.
- Positioned (Entity +0x98): +0x444 grid place (GetLocation), +0x44C orientation (radians; GetOrientation), +0x1F8 object
  size (GetObjectSize), all three on five builds; +0x1FA targeting size (GetObjectTargetingSize, one build).

### 4. Reach and area (PROVEN data, UNSURE geometry)

- Melee: an `.ao`'s `Melee` section lists per clip `damage_patterns` (shape Circle 1719, Rectangle 1252, Cone 1001 of 3972
  patterns in 2636 clips of 912 monster `.ao`; sweep of 3684 files, 2026-11-08): length, width, angle, offset, key frames
  (the fraction of the clip the pattern sweeps, median start 0.42). The farthest a pattern reaches from the monster's
  centre is median 170 world units (p10 100, p90 333, max 1910), against today's fixed 150 gap. The client keeps them:
  the AO's `Melee` component +0x180 points at the playing clip's patterns, 200-byte records (GetSwingRange 0x20608E0), laid
  out by the parser 0x1D47990: +0x00 shape (1 circle, 2 cone, 3 rectangle, donut also parsed), +0x08 key frames, +0x20
  width, +0x24 length, +0x28 angle, +0x2C x scale, +0xB4 local offset, +0xBC offset, +0xC4 rotation (one build).
- Blows with a place: 9157 of the 10589 `contact` events carry a position in the monster's frame (median 104 units from
  it, p90 314; the Miller's slam lands 247 in front). That point plus the skill's radius is where an area lands.
- Area radius: GetPrimaryRadius 0x2085BC0 asks the action's stats for stat 17381 `active_skill_area_of_effect_radius`
  (base 17380, `active_skill_base_area_of_effect_radius`; secondary and tertiary follow). Those stats are computed through
  virtual calls, so the mod should take the base radius from the skill's stat set in the files and allow slack.
- Facing: Positioned +0x44C is the orientation, but the model frame's axes (the patterns' offset and the contact's
  position, forward seemingly −Y) are not measured against it, so `AttackReach` ignores facing: a pattern counts all
  around the monster (UNSURE, safe side: more warnings, not fewer).

### 5. On screen

- `ScreenProjection.Draw` (the camera's own matrix, `CameraView.Draw`) gives any world point's place in screen heights;
  `CameraView.InView` is the 16:9 screen around the player (camera-view.md). Reused, nothing new measured.
- Rule (`AttackSight`): an attacker is seen when any of its body (centre ± its radius drawn) is on the 16:9 screen. From
  off screen, what a sighted player sees first is its projectile entering the screen (a fraction of the flight from
  `AttackSight.EntryFraction`), or the ground telegraph of an area on the player (the telegraph study,
  [attack-telegraphs.md](attack-telegraphs.md)); with neither, nothing shows until the hit, so no warning.

### 6. The game's own warning moment

- No animation event names a warning: the 34,867 events of all monster `.ao` (2026-11-08 sweep) hold `contact` 10589,
  `footstep` 4451, `move` 3903, `start_target_update` 1284, `start_face`/`end_face`, `poise`, `lock_cancelling`,
  `interrupt_skill`, bespoke names (`fireball`, `proj`, `zap`, `slam`, `tele` = teleport), and no `warn`, `telegraph`,
  `indicator` or `windup`. The visible tells (red glow, ground marks) come from the `.ao`'s client effects, the telegraph
  study's subject.
- The tutorial's own dodge moment: only `swollenmiller.ao` (the Bloated Miller, the dodge tutorial) has `tutorial_pause`.
  Its `slam_01` tracks the target (`StartTargetUpdateEventType` 0.0-1.500 s), pauses at 1.504 s and lands its `contact` at
  1.803 s (live 1781-1828 ms): the game pauses the fight **300 ms before the blow, as the monster stops turning to follow
  the player**. `charge_start_01` pauses at 0.96 s, `charge_01` every 0.1 s of the charge.
- Target tracking ends before the blow: of 519 clips with both a `contact` and a target update, the contact comes a median
  105 ms after the tracking ends (p25 −508 for long multi-hit clips, p75 282, p90 497). A roll before the tracking ends is
  followed; a roll after it escapes. 500 ms before the contact (today's lead) falls before most tracking ends; the tutorial
  chose 300. Lead tuning is for the live test.

## Design: gating and timing

Pure, in new files, tested (`DodgeGateTests`, 26 tests):

- `AttackClock`: `Rate`, `IsPlausible`, `MsToNext(reading, events, "contact")`, `MsToNominal(reading, files' hit ms)` (a
  table time from the clip's real position and rate), `NewSwing`, `EventId`, `ActionId`.
- `AttackAim.Of(aim, playerId, player, playerRadius, attacker, areaRadius, pathHalfWidth)`: Player when the target is the
  player; PlayerArea when the place (or another body's place) is within the area radius + the body + 40 of slack, or the
  player stands on the projectile's path; Elsewhere otherwise; Unknown when nothing was read.
- `AttackReach`: `Farthest(patterns)` from the clip's damage patterns, `MeleeReaches`, `AreaCatches`.
- `AttackSight`: `BodySeen`, `EntryFraction`, `SeenAt`.
- `DodgeGate.Decide(facts, leadMs)`: Silent for a calm animation, an attack aimed elsewhere, a blow out of reach (unknown
  reach: a ranged attack aimed at the player reaches; a melee blow or an unknown aim keeps the old rarity and gap rule), an
  attacker off screen with nothing of it coming into view, a blow already past or seen too late; else Warn or Wait
  `max(blow − lead, until it comes into view)`; untimed, at the wind-up (or as the projectile shows). The reason is logged.
- `AttackFieldCheck`: the live checks below, with an Unjudged → Intact/Lost verdict after 20 comparable readings.
- GameOffsets: `AnimationControllerOffsets`, `AnimationDataOffsets`, `AnimationEventOffsets`, `AmdFileOffsets`,
  `AmdClipTableOffsets` (AnimationController.cs), `ActorActionOffsets` (ActorAction.cs), `ActorOffset.CurrentActionPtr`
  0x238 and `ActionFlags` 0x2A0, `AnimatedOffsets.AnimatedObjectPtr` 0x350. Nothing reads them yet.

Per monster each frame the integration reads: the action (type, target id, target place → world through the area's
`WorldToGridConvertor`, skill), the AnimationController reading and the playing clip's events (cached by data pointer
and clip index), the AO's path and the clip's name (for the files' patterns and radius). Then:

1. Threat: the data-driven kind (other agent's work); None → silent.
2. Aim: `AttackAim.Of`. Elsewhere → silent.
3. Reach: melee: `Farthest` of the clip's patterns (files by `ao path|clip`, or the Melee component) against the centre
   distance; area: the contact's place or the target place with the base radius; unknown → as above.
4. Sight: `BodySeen` of the attacker's drawn body, else the projectile's entry.
5. Time: `MsToNext(contact)` live; else `MsToNominal(hit-times)` at the clip's rate; else the learnt delay; else the
   wind-up. A projectile adds its flight to the release (as today).
6. Warn when the verdict says Warn; each `NewSwing` re-arms the monster; `WarningGapMs` and the gain, pan and parry cue as
   today. Learning from blows is then needed only for clips with no event and no file time.

## Protections and what a patch does

Every new field has a live rule; four also have the game's code:

| Field | Live rule (`AttackFieldCheck`) | From the exe |
|---|---|---|
| Animated +0x350 | the object's details path ends in `.ao` and its name map holds `AnimationController` | — (2,564 code sites; a signature can be made) |
| AnimationController position, speed, global, clip | the position advances at the rate between frames (`ClockAdvances`, 25% slack) on 20 readings; `IsPlausible` | `semantic`: index ×2 (geal CurrentAnimationIndex, GetCurrentAnimationLength), speed ×2 (geal, label), global ×2 (geal, label), global off (geal), position (label "Current Animation Position: ") |
| clip events (+0x180 → +0x10; +0x0C, +0x18, +0x38, +0x40) | each event's id equals the hash of its name (`EventLayoutHolds`) | — (five-build helper check above) |
| Actor +0x238 | readable pointer while acting; its type known (below) | `semantic`: geal GetTarget, GetTargetLocation, GetCurrentActionStat (all 0x238) |
| Actor +0x2A0 | bit 0x2 set while +0x238 is set | `semantic`: geal IsCurrentAction |
| action +0x120 | one of ActionTypes.dat's +0x08 values (`ActionTypeKnown`) | five-build GEAL check |
| action +0x148 | null, or an entity whose id (+0x88) is in the awake map at that same address | five-build GEAL check |
| action +0x150 | a place within a few screens of its monster (`TargetPointPlausible`) | five-build GEAL check |

The twelve `semantic` rules read the right value on all five builds with the host's own reader
([evidence](evidence/dodge-semantic-rules.txt)); they are not in `catalog/semantic-fields.txt` yet (that catalog's owner
adds them). A field judged Lost is not read again that session and its part falls back: no clock → the files' time from
the animation id's change at speed 1 (today); no events → `MsToNominal`; no action → aim Unknown (today's reach rule); no
animated object → today's behaviour whole. Each part goes to the compatibility report like the other parts ("el reloj de
los ataques", "a quién apuntan los ataques"), so a patch that moved one is said, not heard as silence or false alarms.

## Live protocol

Read-only: `tools/computer_use/fight_trace.py` records per frame each monster's animation id, action (type, target id,
place), clip (index, name, position, speed, global) and place, each clip's events once, and the player's life and place,
with `t` and `wall` (UTC ms) stamps; `--shots DIR --shot-ms 100` adds frame-timed screenshots.

1. Setup: dev host with the mod; `VerboseLogging` on (the "Dodge warning" lines); the game windowed 16:9. A 60 fps
   recording of the window: `ffmpeg -f gdigrab -framerate 60 -i title="Path of Exile 2" -c:v libx264 -preset ultrafast fight.mp4`,
   started with the trace; the trace's first line holds `start_wall` and ffmpeg logs its start, which lines them up.
2. Fights (each at least 20 attacks of a kind): the Bloated Miller (Riverbank; on a new character its tutorial pause is a
   ground truth for the 300 ms), a pack with a hasted rare (mod "Hasted"), the player slowed (a chill or slow), monsters
   fighting the player's minions or companion, archers firing from beyond the screen's edge, a boss with ground slams.
3. Per attack, from the trace: its start (new action or clip position near 0), its target id and place, its contact (clip
   start + contact ÷ rate), the hit (life drop), whether the attacker was on screen (`ScreenProjection.Draw` of its place);
   from the video, the first frame where the wind-up is visible (marked by hand on a sample of 50).
4. Field checks to read off: positions advance at speed × global; speed > 1 on the hasted rare and < 1 when slowed;
   attack clips list a `contact` matching `hit-times.txt` where one clip plays; the target id is the player's for attacks on
   the player and a minion's otherwise; ground spells' places near the player; the life drop 50-150 ms after the contact
   (network).
5. Metrics: recall = warned attacks at the player on screen ÷ such attacks (goal ≥ 95%); false warnings = warnings for
   attacks aimed elsewhere, out of reach, off screen or calm ÷ all warnings (≤ 5%); timing error = warning − (contact −
   lead) (p90 ≤ 50 ms), warning vs the visible wind-up and vs the life drop, split by timing source (live, nominal, learnt,
   wind-up).

## Integration plan

For the owners of `BossWatch`, the dodge services, the extractor and the semantic catalog:

1. A memory-side reader (plugin, beside `HostMemoryReader`): per monster the action and the AnimationController reading
   through the GameOffsets structs, pointers validated, offsets through `OffsetHealer.Follow` once judged; clip events
   cached per (data, clip); nothing read for a part judged Lost.
2. `AttackFieldCheck` verdicts in the compatibility sweep and report; the twelve semantic rules in the catalog.
3. `BossWatch.WatchAttack`: build `DodgeFacts` each frame and act on `DodgeGate.Decide`; `WatchWorld` replaced by the
   sight rule; learning only for untimed clips.
4. Extractor: one more output keyed by `ao path|clip` (contact times and places, damage patterns, the skill's base radius
   by `monster|animation`) from the same `.ao` parse as `hit-times`.
5. `ActorTrace`: log the action and clock fields, or retire it for `fight_trace.py`.
6. Then run the live protocol, tune `LeadMs` (tutorial: 300 ms before the blow; today 500), and the slack constants.

## First study (2026-10-24, 0.5.5e, Mud Burrow)

### What memory shows (dev command `trace [seconds]`, `ActorTrace`)
Every frame, for each monster within a screen that is acting, the Actor bytes +0x780..+0xB80 that changed, the object
at Actor +0x8B8 (0x300 bytes) and the skills' use stages, beside the player's life. Mud golems, flesh larvae and
ranged larvae traced through Melee, GroundSlam, SpellProjectile and SpellAreaOfEffect:

- An attack start writes +0x8B0 (animation id), +0x8B8 (pointer), +0x8BC, +0x8C0/+0x8C4; nothing in the window changes
  again until the next animation. +0x850 is a float clock (+0.033 per frame), not the attack's progress.
- The object at +0x8B8 is a set of std::vector triples (event lists of the animation?); it does not change during the
  attack either. Its contents were not decoded.
- ActiveSkillDetails use stage (+0x08) and cast type (+0x0C) stayed 0 on these monsters.

No readable attack progress was found, so the timing is learnt instead. (Wrong place, 2026-11-08: the progress is in the
animated object's AnimationController, finding 1; the action is Actor +0x238, finding 3.)

### Learnt timing (`DodgeTimings`, `BossWatch`)
- Every monster within a screen is watched (rarity does not matter: a rare uses its kind's animations). When the
  player's life falls by 4% of its maximum in one frame and exactly one monster in reach has an attack under way (or
  ended within 300 ms), that attack's delay is recorded: key = monster metadata path without "@level" + animation id.
- The median of the last five delays is the attack's timing; saved in `config/dodge-timings.txt` after each blow learnt,
  so it carries across sessions.
- A rare's or unique's attack warns 500 ms before its learnt blow (at once when the blow comes sooner), at the wind-up
  while unknown. A rare's melee warns only if it is within reach when the warning is due.

Seen live: MudGolemWetEncased1 GroundSlam 1156 ms twice; HuhuGrubLarvaeEmergeSummoned1_ Melee 437 / 453 ms.
Melee samples of the golem varied (391 / 2203 / 2641 ms) with a 2% threshold (damage over time, later swings).
The Bloated Miller (SwollenMillerBoss, Riverbank, 2026-10-24, won with the scripted fight): GroundSlam (13) 1828 / 1781 ms,
DualStrike (62) 781 ms, HeavyStrike (71) 625 ms, ChargeEnd (11) 344 ms, Charge (10) 360 / 78 ms — a charge lands when
it arrives, so its delay follows the distance and the warning comes at once.

The timings measured in development ship as `catalog/dodge-timings.txt`, loaded before the player's own
`config/dodge-timings.txt`: a tester's first Miller fight is already timed, and their own blows push the shipped
samples out of each list of five.

### Hit times from the game files (2026-10-24)
The +0x8B8 object turned out to be the parsed `.act` entry of the action, static and shared by every monster using that
`.act` (same pointer for all larva Melee); it holds no time. A repeat swing writes nothing: the animation id stays and
the blows come one animation length apart (golem melee 661, 2125, 3656… = 661 + k × 1500).

The game's files do hold the hit frame (`tools/Poe2UiExtract hit-times` → `catalog/hit-times.txt`):
- The Actor's animation id (+0x8B0) is the row of `Animation.datc64` whose Id names the action (1 Melee, 13 GroundSlam,
  62 DualStrike, 71 HeavyStrike).
- `MonsterVarieties` +0x00 Id, +0x28 ACTFiles, +0x38 AOFiles, +0xD0 GrantedEffects; `GrantedEffects` +0x71 the skill's
  Animation row (inferred from 8 rows). Actions: Melee plus each skill's animation. Other offsets below are the community
  dat-schema's (poe-tool-dev, `schema.min.json`, 2026-10-25), whose row sizes matched every table read; the extractor
  stops using a table whose row size moves.
- `Animation` (0x6A-byte rows): +0x0A (schema "Mainhand_AnimationKey") is the action played when an `.act` lacks one,
  a chain ending at the generic action (SpellAreaOfEffectFire → SpellProjectileFire → SpellProjectile →
  SpellAreaOfEffect; ChargeEndLeft → ChargeEnd; LeapAttack → Melee); +0x2B / +0x34 the actions before and after
  (Charge: ChargeStart, ChargeEnd), +0x3C others it plays (Charge: ChargeEndAlt; LeapSlam: LeapSlamNear, LeapSlamMoving).
  The Miller's charge skill names ChargeStart and showed Charge (10) and ChargeEnd (11) live, so these follow-ups are
  timed too. Which id the Actor shows for a fallback is not traced: both get the time.
- `.act` (UTF-16LE) maps an action to its animations (`GroundSlam = "ground_slam_01";`, `@Alias`, `:options`, stance
  blocks). The hit of an animation, earliest first among an action's animations:
  1. `contact` in the `.ao`'s AnimationController (the client parses it too, finding 2), seconds; parents (`extends`) followed;
  2. else the `.ao`'s Melee section `damage_patterns[].key_frames` first value, a fraction of the animation (settings
     such as `offset_scale` may precede its list);
  3. else a `contact` baked into the `.amd` (each animation: name, once/loop, length ms, event count, then name / ms /
     position per event; versions 1–5 alike). The `.amd` and `.ao` contacts almost never overlap (15 animations of all
     `.ao`); the baked one agrees with the damage pattern's start where both exist (60 of 76 within 100 ms, median +59).
- Projectile speed (`/speed` on the line): the skill's main stat set (`GrantedEffects` +0xA8 → `GrantedEffectStatSets`
  +0x28 ConstantStats / +0x38 values) gives `monster_projectile_variation`, the Id (+0x00) of a `MonsterProjectileSpell`
  row for a spell or of a `MonsterProjectileAttack` row for an attack (`ActiveSkills` +0x40 types, via `GrantedEffects`
  +0x4F), whose Projectile (+0x04) row of `Projectiles` holds ProjectileSpeed (+0x38). Checked: 577 of the 599 skills
  with the stat share a name with their projectile (SnipeFallenKaruiArcher → FallenKaruiArcherSnipe), spell values are
  only found among spell rows and attack values among attack rows. The unit is taken as world units a second, that of
  the `.act` run speeds (Character.act 333–600) and the walk the radar measured (about 400); not timed live. A
  projectile with UseTargetLocation (+0x3C, mortars) gets no speed: its flight is not the distance at that speed. An
  action gets a speed only when every skill playing it fires one (the fastest is kept).
- Checked against blows timed in game: golem GroundSlam 1169 (1156–1172 seen), golem Melee 661 (672), larva Melee 430
  (437–469), Miller GroundSlam 1803 (1781–1828), HeavyStrike 578 (625). Miller DualStrike says 930 against 781 seen: the
  measured seed wins over the files.
- Coverage (2026-10-25, 0.5.5e): 6771 of 8919 monster attacks with an animation are timed (was 6019 of 8552): 3994 by
  an `.ao` contact, 2372 by a damage pattern, 405 by an `.amd` contact; 352 reached through a fallback action, 360 with
  a projectile speed; plus 258 follow-up actions (charge, leap and channel stages). Of the 7398 the mod warns of
  (`DodgeThreats` not calm) 6084 are timed. Untimed: 1314 warned of (280 animations with no server event, 1034 whose
  events hold no contact) and 834 calm ones (dodges, buffs, turns); 939 actions have no animation in their `.act`, even
  through fallbacks (skills cast without one, Rogue Exiles on the player's wildcard `Character.act`).
- Why the rest stays untimed: their server events are bespoke (`slam1`, `proj1`…`proj3`, `skull1`…`skull8`,
  `geotrigger`, `hit_ground`, `swipe`) and nothing in the client binds them to the damage: the names are in no dat table
  and in no `.ot`, and the AI scripts (MonsterVarieties AISFile) are not shipped. A name rule was tried and rejected:
  where an animation has both, the earliest hit-like event lies within 150 ms of the contact only 44% of the time
  (`hit_ground` 67%, `slam` 35%, `proj` 16%). The Miller's ChargeEnd (`swipe` 215 ms) was felt at 344 ms. These keep the
  wind-up warning until a blow is learnt.
- Lines may end in `;noblock` / `;noparry` (or both): the player cannot block or parry that attack (`SkillFlags`,
  [parry notes](parry-and-stun-bar.md)); an untimed attack with flags is written as `=-;…`. 7117 lines on 2026-10-28,
  191 flagged; the timings themselves did not change.
- `catalog/hit-times.txt` had 7042 lines; the mod keeps 6929: 111 hits under 50 ms (mostly a contact at 0) are not
  believed, and a hit past 4000 ms only within its own swing (41 long boss attacks kept, 2 without a length dropped;
  before, every hit past 4000 ms was dropped, which with the hits under 50 ms made 5866 of 6019). Normal monsters play at
  speed 1.0; hasted or slowed ones are not traced (a learnt blow corrects them). (Their speed is AnimationController +0x1B0,
finding 1.)

The mod: `DodgeTimings.Delay` is the learnt median, else the files' hit; `Swing` the animation length. An attack that
keeps its animation warns again one swing later, and a blow felt in a later swing is learnt modulo the swing. A
projectile attack (`ProjectileSpeed`) lands its flight later: `TravelMs` = the gap between the bodies at the attack's
start ÷ speed, added to the release before the 500 ms lead; the warning still sounds after the monster turns away while
the projectile flies (`CanStillHit`), and a blow is learnt as its release (the flight taken off), since the flight
differs with each distance.

## Kinds from the data (2026-11-08, 0.5.5f)
Until now the *kind* of an attack (a blow beside the monster, a hit from afar, or nothing) came only from English words
in the animation's name (`DodgeThreats`), and the files only timed it. Now `hit-times` also asks the game's skill tables
what each monster skill does (`SkillThreats`), and the mod reads the name only where the tables are mute.

- Sources, all read-only and regenerated after a patch: `MonsterVarieties.GrantedEffects` → `GrantedEffects.ActiveSkill`
  (+0x4F) → `ActiveSkills` Id and ActiveSkillTypes (+0x40); the main stat set's implicit (+0x18) and constant (+0x28)
  stats. The ActiveSkills Id is the game's own kind of skill: `melee`, `geometry_attack` (a shaped blow beside the
  monster: slams, cleaves), `geometry_spell`, `monster_projectile_spell`/`_weapon`, `monster_mortar`, `table_charge`,
  `dash_to_target`, `teleport`, `monster_dodge`, `summon_specific_monsters_*`, `empty_action_spell`/`_attack`… (244 kinds
  among monster skills). No `ActiveSkillTargetTypes` table exists in PoE2, the `.ais` AI scripts are not shipped, and
  the ActiveSkillTypes of monster skills are too sparse to tell a buff from an area attack (both plain `Spell`).
- Verdict per skill, in this order: a projectile stat (`base_is_projectile`, `monster_projectile_variation`) or a known
  projectile speed → **ranged**; a Movement/Travel/Blink type on a melee or damaging skill (charges, leaps) → **ranged**;
  the `melee`/`player_melee*` kind → **melee**; a damage stat (`is_area_damage`, `spell_minimum_base_*_damage`,
  `base_chaos_damage_to_deal_per_minute`) → **melee** for `geometry_attack*`, else **ranged**; `base_deal_no_damage`/
  `deal_no_damage` → **calm**, except on carrier kinds (mortar, projectile, trigger, invoke, beam, geometry, channel,
  execute: their payload is another skill's, the Ram Giant's wheel throw and Volatile Dead's detonation among them);
  a calm kind by the game's own name (`teleport*`, `monster_dodge*`, `dodge_roll`, `change_to_stance*`, `transform`,
  `summon_*`, `revive*`, `raise_*`, `*_warp`, `add_buff*`, `unload_ammo`, warcries) → **calm**; anything else → **mute**.
  An action combines its skills: ranged wins, then melee, calm only when every skill is calm, mute otherwise; the basic
  Melee action (no granted skill) is melee; follow-up actions (Charge, ChargeEnd) inherit the kind as they inherit the
  block flags. The mute kinds are mostly `empty_action_*` (an animation whose damage the AI triggers through skills with
  no animation of their own, `…TriggerGA`), `execute_geal`, `geometry_trigger` and dashes whose landing slam is a
  separate skill (55 of 68 LeapSlam dashes have one): the name decides there, as before.
- A "contact" event is not a blow by itself: 473 actions the name calls calm have one (DodgeForward, Teleport, SpellBuff,
  Revive, RaiseSpectre, SummonMinion), so "anything with a hit event warns" would have brought the grub's Submerge back.
  A calm action is written `=-;calm` without its time.
- `hit-times.txt` lines now carry `;melee|ranged|calm[,noblock,noparry]`; 9025 lines (was 7117: untimed attacks with a
  kind are written too; launcher step "hit times" v3). `DodgeTimings.Threat(key)` gives the kind; `BossWatch` takes it
  before the name, and logs each attack it had to read by name once ("Dodge kind from the animation's name: … -> Melee";
  `NamedThreats` counts them). An animation id with no name at all now stays quiet instead of warning as ranged.
- Counts on the current files (monster attacks with an animation in their `.act`, 8967): kind from the data 5604 (62 %,
  5162 of them timed), calm by the data 218, left to the name 3145 (2139 the name warns of, 1006 it calls calm). Before,
  the name decided all 8967 and warned of 7692. Bosses (406 varieties with "Boss" in the path, 3011 attacks): data 1590
  (1414 timed), calm 80, name 1341. `hit-times <game> <out> <audit.tsv>` writes every disagreement with the name (668).
- Judged from the audit: the data **adds** 160 warnings the name missed and they look right: the Giant Starfish's four
  tentacle attacks play `SpellBuff*` animations with the `melee` kind; the Chimera's lightning breath plays `SpellBuffCold`
  and is a projectile; `RollingMagma`, `MagneticSalvo`, `FreezingSalvo`, `Spearstorm`, `WindstormLance`,
  `DoryaniHumanBossLightningZap` carry projectile or damage stats; Quadrilla's commando roll (`DodgeForward`) and the
  Mutewind spear flicker (`Teleport`) are `melee` skills. The data **silences** 66: summons on `SpellAreaOfEffect*`
  animations (Lachlann's swarm and wife, the Rust King's skeletons), Balbala's flicker clones (`teleport`), the
  Ancestral Turtle's bubble (`add_buff_to_target`). Kind swaps: 288 melee by the data (mostly `geometry_attack` slams on
  `SpellAreaOfEffect*` names: a rare's slam now needs it within 150 units, a unique's still carries) and 166 ranged
  (slams with `is_area_damage` through `empty_action_attack`/`geometry_spell`, the Hag's ice shard `Puncture`, the Risen
  Arbalest's basic shot on the `Melee` animation).
- Needs a live check: the Devourer's and Mud Burrower boss heads' `Submerge` (`minotaur_burrow`, `is_area_damage`,
  "MudBurrowerBurrowSpam": the data says the burrow hurts and warns every dive); geometry attacks as melee on rares
  (a far cleave may now go unwarned); the `melee`-kind dodge rolls and teleport strikes (warn only within reach).
  The grub's Submerge/Hide are not skills of its row: no data, the name keeps them calm.
- Agreement with blows timed live (363 "Dodge timing" log lines, 60 attacks, plus `dodge-timings.txt`): the files time
  53 of 60; median error 156 ms, 25 within 150 ms, 34 within 300 ms. Many live samples are a life drop attributed to
  the one attack in reach (values under 100 ms, a FungusBehemoth multi-hit at 125–3000 ms): on attacks with two or more
  samples of 100 ms or longer the median error is 86 ms (23 timed, 14 within 150 ms). Untimed yet felt: the Miller's
  Charge and ChargeEnd (bespoke events), FungusBehemoth 36/38, RustedSoldierBow 448.
- The per-patch news (`GameNews`, `GameNewsNotes`, `config/game-news.txt`) now says, for each new monster, its boss mark
  and `DodgeCoverage` ("7 attacks: 5 timed, 6 kinds from the data, 1 from the name"); for a buff its rule or hazard;
  for an area whether `area-names.json` names it; for a minimap icon its kind; for a screen whether the UI catalog has
  it; and counts the unclassified per table in the spoken line ("monstruos 12, todo cubierto; efectos 9, 3 sin
  clasificar").
