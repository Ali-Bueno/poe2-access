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
  1. `contact` in the `.ao`'s (server) AnimationController, seconds; parents (`extends`) followed;
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
  speed 1.0; hasted or slowed ones are not traced (a learnt blow corrects them).

The mod: `DodgeTimings.Delay` is the learnt median, else the files' hit; `Swing` the animation length. An attack that
keeps its animation warns again one swing later, and a blow felt in a later swing is learnt modulo the swing. A
projectile attack (`ProjectileSpeed`) lands its flight later: `TravelMs` = the gap between the bodies at the attack's
start ÷ speed, added to the release before the 500 ms lead; the warning still sounds after the monster turns away while
the projectile flies (`CanStillHit`), and a blow is learnt as its release (the flight taken off), since the flight
differs with each distance.
