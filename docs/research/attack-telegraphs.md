# Attack telegraphs: what the game draws before a monster skill lands, and how the mod can read it

Research of 2026-11-08 on the current files (bundle index of 2026-11-06, build 26476662), read-only. Goal: warn the blind
player exactly when a sighted one sees the warning. Confidence: **PROVEN** (read in the files or the exe, or seen in an
entity dump), **LIKELY** (consistent data, not yet seen live), **UNSURE**. The animation side (progress, target, reach)
is the actor study in [dodge-timing.md](dodge-timing.md); this note is the data side and the entity side.

Conventions as in [game-data-sources.md](game-data-sources.md): dat offsets are `+0xNN` bytes in a row, column names
come from the community dat-schema's `poe2/` overrides (poe-tool-dev/dat-schema, cloned 2026-11-08; row sizes of this
build match: GrantedEffects 0x129, GeometryAttack 0x152, GeometryTrigger 0xEA, SpawnObject 0x56, EffectDrivenSkill
0x77, MonsterMortar 0x88) or from content where the schema leaves a column unnamed, and I say which.

## 1. Summary

PoE2 has **no single "telegraph" table**. The warning a sighted player sees is drawn by five mechanisms, and four of
them are described by data the mod can extract and, at run time, match against memory:

| Mechanism | Share of monster skills | What defines the danger area | What the client draws | How the mod can see it |
|---|---|---|---|---|
| **Geometry skills** (`geometry_attack`, `geometry_spell`; GA/GS ids) | 2112 skills; 1329 of 2793 monsters, 323 of 388 "Boss" ids | `GeometryAttack` row: shape (circle, cone, rectangle, donut), radius/length, width, angle, delay, range | the row's `Animation` (a MiscAnimated `.ao`, 1523 skills) played at the area as a `ServerEffect` object | the monster's current skill → the row (shape and size); the effect entity's `.ao` → the catalog |
| **Geometry triggers** (`geometry_trigger`; GT ids) | 527 skills | `GeometryTrigger` row: N hits after a delay (ms), spread, radius, angle | `GroundMarker` `.ao` placed first (54 rows: ice spikes, meteors, lightning strikes) | the marker entity's `.ao` → catalog → hit `delay` ms later |
| **Effect-driven skills** (`effect_driven_*`; EDS ids) | 115 skills | the skill's effect `.ao` has a server `EffectDrivenEvent` block: shapes with `time` (s), `length`, `width`, `angle`, waves | that same `.ao`, spawned as a `ServerEffect` entity | the entity's `.ao` → catalog → shapes and times |
| **Spawned objects** (`spawn_object`; SO ids) | 155 skills | `SpawnObject` row → `MiscObjects` → `.ot` paths (158 rows) | the object itself (entity with its own `.ot`) | the entity's metadata path (as `mudburrower_chasm` in the dumps) |
| **Mortars** (`monster_mortar`, `geometry_mortars`) | 102 skills | `MonsterMortar` row: projectile, `ImpactAnimation` | landing visual at the target | UNSURE: whether a landing marker exists before impact; live protocol |
| Animation-only telegraphs | 33 monster `.ao` files (49 clips) | a `StandardEventType` event named `danger_rectangle`, `telegraph`, `marker`…; 17 monster `.ot` attach decals in client handlers | a decal/particle attached to the monster | only through the animation clock (actor study); times are in the files |
| AI-spawned objects (no skill) | 208 of 2806 monster `.ot` preload 201 objects, 104 with `Positioned` | the AI script (`.ais`, not shipped) spawns them | the object (e.g. the Devourer's `mudburrower_chasm`) | the entity's path; timing only from the object's own animation |

Melee (`melee`, 2685 monster skill slots) and plain projectiles draw nothing but the monster's animation: their timing
stays with `hit-times.txt` and the animation clock. Everything that *is* drawn is covered by the rows above.

## 2. Evidence

### 2.1 Entity dumps (PROVEN)

`debug/entities-*.txt` (host and dist copies, 13,776 lines) hold effect objects as ordinary entities with `Positioned`:
`Metadata/Effects/Spells/monsters_effects/Act1_FOUR/MudBurrower/mudburrower_chasm` (5 times, components
`Animated,Positioned,Render,StateMachine,Transitionable`, bounds 30,30,0), `mudburrower_chasm_body`,
`.../CannibalPict/bow/ao/BloomPod` (`Buffs,LimitedLifespan,...`), `Metadata/Effects/PermanentServerEffect` (4 times,
`Animated,BaseEvents,Positioned,Render`) and `Metadata/Monsters/Balbala/ArenaObjects/BalbalaFogMarker`. So
server-spawned effects reach the client's entity list, which the host already reads; the host's
`Animated` component reader (`host/GameReader/RemoteObjects/Components/Animated.cs`) gives an entity's `.ao` path.
The dumps never caught a `Metadata/Effects/ServerEffect` because they are snapshots between fights; the live protocol
(§6) is for that.

### 2.2 Generic server effects (PROVEN files, LIKELY use)

`metadata/effects/servereffect.ot`, `sleepableservereffect.ot` (and `permanentservereffect.ot`, seen live) are
`Positioned { object_size = 0 } + Animated + LimitedLifespan + client Render`: a bare carrier whose only content is the
`.ao` the server names. The exe keeps `Metadata/Effects/ServerEffect`, `SleepableServerEffect` and
`Data/Balance/GeometryAttack.dat` in the same string block as `GeometryAttackBase`'s errors (`PathOfExileSteam.exe`
.rdata 0x33FB7xx), and `Loaders::GeometryAttack`, `Loaders::GeometryTrigger`, `Loaders::EffectDrivenSkill`,
`Loaders::SpawnObject`, `Loaders::MiscAnimated` exist in the client. That the client spawns the geometry visual as a
ServerEffect entity is LIKELY; §6 confirms it.

### 2.3 Geometry skills (PROVEN data)

* ActiveSkills kinds `geometry_attack`, `geometry_spell`, `geometry_attack_channelled`, `geometry_spell_channelled`
  (rows 321, 322, 472, 473); the skill's row is reached through **GrantedEffects `SkillID` (i32 at +0x9B) ==
  `GeometryAttack.Id` (i32 at +0)**: 1132 of 1132 geometry attacks and 980 of 980 geometry spells resolve (geom3.py).
  The same rule links `geometry_trigger` → `GeometryTrigger` (527/528), `spawn_object` → `SpawnObject` (155/170),
  `effect_driven_*` → `EffectDrivenSkill` (115/117), `monster_mortar`/`geometry_mortars` → `MonsterMortar` (102/102).
  `SkillID` is 0 for most other skills, so the lookup is only valid for a skill of the kind the table serves.
* `GeometryAttack` columns (schema names the first three; the rest by content over 2736 rows):
  `+0x0 Id`, `+0x4 Animation [MiscAnimated]` (visual, 2004 rows), `+0x14 HitEffect [MiscAnimated]`,
  `+0x28 shape` (1 circle 1655 rows, 2 cone 455, 3 rectangle 564, 4 donut 61, 5 triangle 1: named by the skill ids and
  visual names — "Cone"/"Breath" skills are 2, "Slash"/"Beam"/"Rectangle" 3, "Nova"/"Slam"/"Circle" 1, "Donut" 4; every
  cone has a non-zero `+0x71 angle` and every rectangle a non-zero `+0x34 width`), `+0x30 length or radius`,
  `+0x34 width` (rectangles; inner radius of donuts), `+0x71 angle` (degrees, cones), `+0x7A delay` ms (0, 33 = one
  server tick on 1114 rows, 100, 1000…), `+0x24 range` (world units, 0 … 6000), `+0x7F BuffDefinition`,
  `+0xC8 GeometryChannel`, `+0x101 ExecuteGeal`, `+0x122 GroundEffect`, `+0x142 MonsterVariety []`. Unknown enums at
  `+0x2C`, `+0x39`, `+0x3D` (2…13) match the exe's `GetFrom`/`GetToward`/`GetCentre` modes (`Default`, `Random`,
  `SelfOnce`, `AwayFromSelf`, `InFront`, `ContactPoint`): where the shape is anchored. `MonsterSkillsShapeValues::Triangle`
  is the exe's enum name.
* Examples (`telegraph-skills.txt`): `MudGolemSlam|geometry|shape=circle;length=15;delay=33;range=66|…/MudGolem/slam.ao`;
  `GABaronWolfDashDangerRectangle|geometry|shape=rectangle;width=15;delay=33;range=250` (the wolf's dash lines: its
  effect-driven detonation `blood_dash_detonate.ao` lists two rectangles 120 × 13 at ±90°, so the unit is the world
  unit, 10.87 to a grid cell); `SwollenMillerChargeEnd|shape=cone;length=27;angle=180;delay=60;range=300`;
  `GABaronHumanLacerateSlamLeft|shape=rectangle;length=92;width=8;delay=33;range=200|…/icewall_lacerate_L.ao`.
* The parent attack and its telegraph are separate skills: `GABaronBossHumanImpalingSword` (the hit) and
  `GABaronBossHumanSwordDangerRectangle` (the drawn rectangle, `shape=rectangle;width=5`, no visual of its own, no
  damage stats), both `Triggerable`, triggered from the Baron's animation event `danger_rectangle` at 0.47 s
  (`metadata/monsters/baron/phase2/baronphase2wolf.ao`). The GrantedEffects text columns `+0xC0`/`+0xD8` of the
  rectangle name the parent skill (UNSURE which column is which; both read `GABaronBossHumanImpalingSword`).

### 2.4 Geometry triggers (PROVEN data)

`GeometryTrigger` (799 rows): `+0x4 GrantedEffect` (378 rows), `+0x14 Animation`, `+0x24 count`, `+0x28 delay ms`,
`+0x34 spread`, `+0x38 radius`, `+0x3C angle`, `+0x73 GroundMarker [MiscAnimated]`, `+0xAE Stats`, `+0xBE StatValues`.
`GTIceStrongboxIceMarkers`: 15 markers, 2000 ms, spread 80, radius 15, marker `…/TheExperimets/IceSpikeMarker.ao`;
`GTCyclopsSpiderBossDangerZone`: 10, 1000 ms, spread 280, radius 140, no marker (the zone is drawn by its parent).
Where a marker exists the warning is exact by construction: marker entity appears → hit `delay` ms later.

### 2.5 Effect-driven skills (PROVEN data)

217 effect `.ao` files carry a server `EffectDrivenEvent { animation = "animate" shape = "circle" time = 0.750
length = 10 width = 10 … }` block (2453 shapes: circle 2177, cone 128, rectangle 101, donut 47; `time` in seconds
from the effect's start, median 0.75 s, max 9 s; 152 with `wave_time`/`wave_interval`, 76 with
`orientation_to_effect`, 15 `orientation_to_target`, 33 with a `monster`/`rarity` filter). The exe parses the same keys
(`EffectDrivenSkillBase::CollectTargets`, `shape_location`). 115 monster skills use them; their `.ao` is the entity's
`.ao`, so start, place, shape and hit time are all in one file: the most exact telegraph the game has.

### 2.6 Spawned objects (PROVEN data, PROVEN entities)

`SpawnObject` (171 rows, `+0x4 MiscObjects []`, GEAL variable name at `+0x45`: `pods`, `flank_markers`,
`meteormarker`…) → `MiscObjects` (`+0x8` `.ot` path): 158 rows name objects such as
`Metadata/Monsters/BeehiveBehemoth/Objects/FungusPod1`, `…/Expedition/ExpeditionBoneCultistEgg`,
`…/PictFemaleBow/BloomPod` (the dump's `BloomPod` entity). Of the 848 non-monster `.ot` with `Positioned` in the
monster/effect folders, 24 have `LimitedLifespan`, 11 `DiesAfterTime`, 13 `GroundEffect`, 52 `ControlZone`.

### 2.7 Animation-only telegraphs (PROVEN data)

Monster `.ao` events whose name says warning: `fissure` 19, `cracks` 7, `danger_rectangle` 6, `telegraph` 4,
`anchor_marker` 4, `marker*` 9, `meteor_marker` 3, `spawndeathzone` 2, `start_telegraph`, `spawn_target_marker`,
`guillotine_marker`, `indicator` (33 files, 49 of 10,224 clips). 17 monster `.ot` attach a decal or AoE `.ao` in
client handlers (`bitterguywifeghost.ot` `on_transition` → `circleAoE.ao`; `hellscapepalefodder2.ot`
`on_state_bomb_charge_1` → `ice_bomb_aoe.ao`; `deliriumteleportandexplodedaemon.ot` → `telegraph_general_high.ao`).
These are client-side and never entities; their time is the event time inside the clip, usable only with the
animation clock. Rare: the drawn warnings are overwhelmingly the geometry visuals of §2.3.

### 2.8 What is NOT a telegraph

`GenericSkillIndicator` (13 rows: `arsonist_show_indicator`, cull/freeze/pin icons over monsters) and
`IndicatorConditions` are the player's skill hints; `MiscBeams` `*TelegraphBeam` and `TriggerBeam` (102 rows) are beam
visuals between caster and target (not entities; UNSURE); `GroundEffects` (377) is the harmful ground the hazard alarm
already covers through buffs. The melee `damage_patterns` of monster `.ao` (shape, key frames, width, length, angle)
are the server's hit test; nothing in the files says the client draws them.

## 3. Coverage (current files)

Over `MonsterVarieties` (2793 monsters with a path; `GrantedEffects` list at `+0xD0`), by the ActiveSkills kind of each
skill slot (coverage1.py / geom4.py): `geometry_attack` 2903 slots, `melee` 2685, `geometry_spell` 2125,
`empty_action_spell` 1567 (an empty action the AI fills with triggered skills), `geometry_trigger` 1076,
`empty_action_attack` 746, `monster_projectile_spell` 733, `execute_geal` 453, `monster_projectile_weapon` 444,
`monster_cast_ground_effect_location` 444, `spawn_object` 364, `dash_to_target` 284, `effect_driven_spell` 221,
`trigger_beam` 162, `monster_mortar` 121.

* 1329 monsters (48 %) have a geometry skill; 1145 have one whose row names a visual or a ground marker.
* Of the 388 ids containing "Boss": 323 have geometry skills, 303 a drawn visual or marker.
* The prototype wrote 3011 skill lines (2112 geometry, 527 trigger, 155 spawn, 115 effect, 102 mortar), 1432 effect
  files (147 with EffectDrivenEvent shapes; 1206 visuals, 133 effects, 53 impacts, 35 markers) and 1497 monsters.
* Not drawn by data: melee, plain projectiles, `empty_action*` parents (their triggered children are), `execute_geal`
  scripts (the script text is in `ExecuteGEAL +0x60`, e.g. `this.my_next_flank_marker.Delete()`; a reader could follow
  `SpawnObject(...)` calls, not done).

## 4. Run-time detection design

Two independent paths, both read-only, both feeding the existing dodge services through a new `TelegraphWatch`
(not written: the dodge files are being changed by another agent).

**A. Effect entities (exact by construction, no animation needed).** Each frame the mod already walks the entity
list. A new entity whose path is `Metadata/Effects/ServerEffect`, `SleepableServerEffect`, `PermanentServerEffect`
(or whose path is a `spawn` object of `telegraph-skills.txt`) is a candidate; its `Animated` component gives the `.ao`
(host `Animated.ModelPath`, already read for Expedition markers). Look the `.ao` up in `telegraph-effects.txt`:
- role `effect` with shapes → warning now, hit at the earliest `time`, area = the shapes (circle radius `length`,
  rectangle `length × width` along the effect's orientation, cone `angle`); the entity's `Positioned` gives the centre
  and the rotation (`Positioned.Rotation` is in the host's component);
- role `marker` → warning now, hit after the trigger's `delay` (the skill line's `delay`), radius `radius`;
- role `visual` (a geometry visual) → the hit is immediate or `delay` ms later; shape and size from the skill line of
  the skill whose row names this `.ao`. One `.ao` can serve several skills (12 `impact,visual`, 5 `effect,visual`): take
  the largest area, or the skill the nearest monster owns (its `telegraph-monsters.txt` line).
- `spawn` objects → warning now; size = the object's own `.ao` EffectDrivenEvent if any, else `Render` bounds.
The entity's distance to the player, not the monster's, decides the warning's loudness; the owner is the nearest
hostile monster whose skills include that line (for the "which monster" word).

**B. Current skill (needs the actor study).** When the monster's action exposes its skill (GrantedEffects row, or the
`SkillID`), `telegraph-skills.txt` gives shape, size, anchoring (toward the target, in front, at self) and `delay`; with
the monster's position, facing and target location the mod knows whether the player stands inside the area the game
is about to draw, before the visual entity appears (the geometry's visual plays at the same tick the skill starts, so
A and B coincide in time; B adds the "am I inside" answer, A needs no actor fields).

**Failure modes.** A `.ao` shared by a harmless effect (impact flashes after the hit: role `impact` must not warn);
`PermanentServerEffect`s that live for the whole fight (arena fog, pools: no `LimitedLifespan`; warn once, then treat
as ground); effects spawned by the player's own skills (only hostile `Positioned` reactions count; a ServerEffect's
reaction flag must be checked live); visuals that outlive the hit (`delay` passed → the area is over); several entities
for one attack (15 ice markers: one warning, not fifteen: dedupe by `.ao` + 200 ms); a skill with no visual and no
marker (589 geometry rows "area only": path B only). Unit scale: sizes are world units (10.87 per grid cell) by the
Baron cross-check; §6 measures it.

**Patch-proofing.** Tables are opened by name, guarded by row size (`Sized`), and every list or path column is found
by content near its measured offset (`DatContentColumns.Nearest`); `SkillID` is found as the i32 column whose value is
a `GeometryAttack.Id` for every geometry skill; the shape column must hold only 1…5. A moved column fails the whole
step and keeps the old catalog (the launcher's rule). At run time nothing is hard-coded: entity paths come from the
catalog, `.ao` names from memory; a `ServerEffect` whose `.ao` is unknown is logged ("telegraph effect unknown") so
the next patch report lists it. The generic carriers' paths (`Metadata/Effects/*ServerEffect`) are the only literal
names, and `telegraph-effects.txt` can carry them too once confirmed live.

## 5. Extractor prototype

`tools/Poe2UiExtract/TelegraphExtractor.cs` (+ pure `TelegraphShapes.cs`, tested in `tests/…/TelegraphTests.cs`):
`TelegraphExtractor.Run(index, outDir)` writes `telegraph-skills.txt`, `telegraph-effects.txt`,
`telegraph-monsters.txt` (formats in the file headers). Not wired into `Program.cs`/`Batch` yet; run from a scratch
host on 2026-11-08 in 26 s on the live install (the index copied, since the running game holds `_.index.bin` for
reading only: `SharedBundles.Open` cannot open it while the game runs, an issue for the launcher too). Output size
856 KB (skills 390 KB; the mod needs only the monsters' skills, about a third).

## 6. Live protocol (to run with the game, no input from me)

Fight the Mud Golems / Bloated Miller (Riverbank, Act 1) and the Devourer or Count Geonor, with the dev host dumping:
1. **Entity dumps every 100 ms** for 10 s around each boss attack (`entities` dump extended with the `Animated` `.ao`
   path, `Positioned` rotation and the reaction flag for every entity whose path starts with `Metadata/Effects/` or
   is in `telegraph-skills.txt`'s spawn column): expected a `Metadata/Effects/ServerEffect` with `.ao`
   `…/MudGolem/slam.ao` when `MudGolemSlam` starts, lifetime about one swing.
2. **Timeline per attack**: entity first seen (t0), monster animation id change (from `trace`), player life drop (the
   blow). Measures: t0 − animation start (expected 0–33 ms for geometry visuals; the `GroundMarker` delay for
   triggers) and blow − t0 (expected `delay` or the clip's contact).
3. **Unit scale**: stand at measured distances (grid cells from the radar) from a Miller `SwollenMillerChargeEnd`
   (cone length 27) and a Golem `MudGolemSlam` (radius 15) and note hit/miss; expected hit under 15 world units ≈ 1.4
   cells for the slam.
4. **Markers**: an Ice Strongbox or the Carrion Crone's ice spikes: count marker entities (`IceSpikeMarker.ao`,
   `icespikemarker.ao`) and the time to the hit (expected 2000 ms / the GT `delay`).
5. **Noise check**: a 5-minute walk through a town and a map with no fights: list every `Metadata/Effects/*` entity
   seen (should be waypoint/doodad effects only) to set the ignore list.
Record the dumps under `debug/` and append the measured numbers to this file.

## 7. Links

- Actor side (animation progress, target, reach): [dodge-timing.md](dodge-timing.md) (another agent's study).
- Hit times from the animation files: `HitTimeExtractor`, `catalog/hit-times.txt`.
- Harmful ground by buffs: `HazardExtractor`, [parry-and-stun-bar.md](parry-and-stun-bar.md).
- Scratch tooling of this study (not kept): a C# REPL over the bundles (index copy + install bundles), Python decoders
  over saved `.datc64` (geom1–4.py, corpus1–2.py), exe string scans; the numbers above are theirs.
