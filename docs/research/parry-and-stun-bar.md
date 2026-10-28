# Parry, the player's stun bar, energy shield and DPS (2026-10-28, PoE2 0.5.5e)

## Game facts (game files)
- **Parry** (KeywordPopups "Parry"): a skill granted by bucklers, targes and dual-wielded swords; blocks a hit and
  retaliates, inflicting **Parried** (the enemy takes more attack damage and cannot evade). ActiveSkills id `parry`;
  GrantedEffects id **`ParryPlayer`** (row 433). Raise Shield is `ShieldBlockPlayer` (row 342).
- Animations: `ParryChannel` 0x2DF, `ParryEnd` 0x2E0, `PerfectParryEnd` 0x2E1 (GameHelper `Animation`).
  KeywordPopups "PerfectTiming": some channelled skills reward a release within a timing window. What
  `PerfectParryEnd` rewards is not known (no source found).
- poe2db Parry (0.5): channelled, 0.75 s attack time, parries the next melee or projectile hit, 400% more stun
  buildup on you.
- **Heavy stun on players** (KeywordPopups "HeavyStun"): players build it only while raising a shield, parrying with a
  buckler or riding; a full bar breaks the guard (heavy stun, ~3 s). The game draws a stun bar for it.
- **DPS**: ClientStrings `SkillPopupDPS` / `SkillPopupAverageDamage`: each skill's DPS in its popup and in the Skills
  panel ("Punch, DPS: 6,1", read since 2026-10-23). No live damage meter exists, so none is spoken.

## Attacks that cannot be blocked or parried (game files)
- KeywordPopups "Block": some boss skills cannot be blocked, shown by a red glow and an audio cue during the wind-up.
  The game has no "parry now" or "dodge now" sign beyond that and the attack's own animation.
- Stats `base_cannot_be_blocked` / `skill_cannot_be_blocked` / `base_cannot_be_parried` / `base_skill_cannot_be_parried`
  sit in a stat key list at GrantedEffectStatSets +0x18 (66 sets, 69 skills, 153 monster varieties; scan 2026-10-28).
  Examples: the Bloated Miller's charge (both), the Executioner's slams and guillotine drop, the Rust King's claw
  cascade, the Devourer's dive push (cannot be blocked), the Chimera's fire spray (cannot be parried).
- `hit-times` writes them as `;noblock,noparry` on each action the skill plays and the actions played with it
  (the charge's ChargeStart, Charge, ChargeEnd); `DodgeTimings.CanParry` keeps the parry cue off them. The launcher's
  "hit times" step is version 2 so installs regenerate it once.

- The unblockable ones (`;noblock`) also sound an extra cue at the wind-up (`CueSynth.Unblockable`, `BossWatch`), the
  user's ask, so the plain dodge warning means "dodge or parry".

## Harmful ground (game files)
- GroundEffectTypes (53 rows of 0x40): +0x20 is the BuffDefinitions row a creature standing in it gets (IgnitedGround →
  `ignited_ground`, CausticCloud → `caustic_cloud`, LivingBlood → `ground_living_blood`, FungalArtillery →
  `ground_fungal_artillery`); key scan 2026-10-28. Helpful types (Consecration, Haste, Blizzard, Oasis, FeatheredGround,
  Leyline), Smoke (its buff is the shared `generic_buff_aura`) and Unused are left out.
- Boss zones are buffs whose English description says the player takes damage while standing in something
  (`demon_claw_boss_beam`, `voodoo_king_maze_debuff`, `perennial_king_sand_storm`, `caustic_cloud_snake_boss_vomit`).
  Ailments read "Debuff inflicts …" and are not caught.
- `hazards` writes the 61 ids to `catalog/hazards.txt` (launcher step "hazards" v1); `HazardGround` matches a player buff
  id without its "_hex" instance suffix, or one that starts with a listed id and "_" (`ground_living_blood_damage`,
  seen in play). `HazardWatch` holds a looping bed (`CueSynth.HazardAlarm`) up while one is on the player. Buffs seen in
  real fights before this (logs): `caustic_cloud`, `ground_fungal_artillery`, `ground_living_blood_damage`, `vortex`.

## Memory (verified live 2026-10-28, yarkidius, Warrior 11)
- Player skills: Actor +0xB08 std::vector of 16-byte entries → ActiveSkillDetails → +0x58 GrantedEffectsPerLevel key →
  +0x0 GrantedEffects key → row +0x0 Id. Logged as "Player skills: EnforcedWalkingPlayer, DodgeRollPlayer, …,
  ShieldBlockPlayer, …, BoneshatterPlayer, ShockwaveTotemPlayer" (`ParrySkill`).
- Player stun bar: Actor +0x4E4 threshold, +0x4E8 buildup, as for monsters: 0/263 in town. A blocked hit filling it
  has not been heard live yet (no combat with scripted input).

## Mod behaviour
- Parry cue (`BossWatch` + pure `ParryTiming`, sound `CueSynth.Parry`): a reverse hit (the user's ask: "viene el
  ataque, páralo"); its dead stop falls `ParryLeadMs` (150) before the blow time the dodge warning uses (learnt or
  from the files plus projectile flight). A blow too quick for the 320 ms swell gets no cue.
- Stun bar (`ShieldWatch` + pure `StunMeter`): "Barra de aturdimiento N por ciento" past 50, 75, 90.
- Energy shield (`PlayerAlerts`): 75, 50, 25 and depleted, for a pool of at least a fifth of life.
