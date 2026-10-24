# Dodge timing (2026-10-24, 0.5.5e, Mud Burrow)

Goal: warn a moment before a monster's blow lands, not at its wind-up.

## What memory shows (dev command `trace [seconds]`, `ActorTrace`)
Every frame, for each monster within a screen that is acting, the Actor bytes +0x780..+0xB80 that changed, the object
at Actor +0x8B8 (0x300 bytes) and the skills' use stages, beside the player's life. Mud golems, flesh larvae and
ranged larvae traced through Melee, GroundSlam, SpellProjectile and SpellAreaOfEffect:

- An attack start writes +0x8B0 (animation id), +0x8B8 (pointer), +0x8BC, +0x8C0/+0x8C4; nothing in the window changes
  again until the next animation. +0x850 is a float clock (+0.033 per frame), not the attack's progress.
- The object at +0x8B8 is a set of std::vector triples (event lists of the animation?); it does not change during the
  attack either. Its contents were not decoded.
- ActiveSkillDetails use stage (+0x08) and cast type (+0x0C) stayed 0 on these monsters.

No readable attack progress was found, so the timing is learnt instead.

## Learnt timing (`DodgeTimings`, `BossWatch`)
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

## Hit times from the game files (2026-10-24)
The +0x8B8 object turned out to be the parsed `.act` entry of the action, static and shared by every monster using that
`.act` (same pointer for all larva Melee); it holds no time. A repeat swing writes nothing: the animation id stays and
the blows come one animation length apart (golem melee 661, 2125, 3656… = 661 + k × 1500).

The game's files do hold the hit frame (`tools/Poe2UiExtract hit-times` → `catalog/hit-times.txt`):
- The Actor's animation id (+0x8B0) is the row of `Animation.datc64` whose Id names the action (1 Melee, 13 GroundSlam,
  62 DualStrike, 71 HeavyStrike).
- `MonsterVarieties` +0x00 Id, +0x28 ACTFiles, +0x38 AOFiles, +0xD0 GrantedEffects; `GrantedEffects` +0x71 the skill's
  Animation row (inferred from 8 rows). Actions: Melee plus each skill's animation.
- `.act` (UTF-16LE) maps an action to its animations (`GroundSlam = "ground_slam_01";`, `@Alias`, `:options`, stance
  blocks); the `.ao` AnimationController lists events per animation, and `contact` is the hit (seconds); without one, the
  `.ao`'s Melee section `damage_patterns[].key_frames` first value is the fraction of the animation; `.amd` gives each
  animation's length in ms. Parents (`extends`) are followed.
- Checked against blows timed in game: golem GroundSlam 1169 (1156–1172 seen), golem Melee 661 (672), larva Melee 430
  (437–469), Miller GroundSlam 1803 (1781–1828), HeavyStrike 578 (625). Miller DualStrike says 930 against 781 seen: the
  measured seed wins over the files.
- 6019 of 8552 monster attacks with animations get a time; the rest are mostly projectiles, spells and charges, whose
  blow depends on travel. Normal monsters play at speed 1.0; hasted or slowed ones are not traced (a learnt blow
  corrects them).

The mod: `DodgeTimings.Delay` is the learnt median, else the files' hit; `Swing` the animation length. An attack that
keeps its animation warns again one swing later, and a blow felt in a later swing is learnt modulo the swing.
