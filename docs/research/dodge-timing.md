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
