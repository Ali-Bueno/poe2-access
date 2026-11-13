# Boss mechanics: what a blind player is told in a boss fight, from the game's data

Research and implementation of 2026-11-08 (files of 2026-11-06, build 26476662, patch 0.5.5f), offline: the game's files
read through the extractor, no game running for this work. Confidence: **PROVEN** (read in the files, or in memory by the
existing readers), **LIKELY** (consistent data, not yet seen live), **UNSURE**. Related: [attack-telegraphs.md](attack-telegraphs.md)
(danger areas), [dodge-timing.md](dodge-timing.md) (the dodge gate this builds on).

## 1. Summary

| A blind player needs | Where the game keeps it | What the mod says now | Bosses covered (of 216 names) |
|---|---|---|---|
| The boss left the fight (burrowed, flew off, faded into fog, hidden) and came back | Targetable flag, `hidden_monster` buff, script states that stop drawing it | "El jefe ha desaparecido" / "El jefe ha vuelto", once each, after 0.8 s | all (flags); 30 with a boss-specific hide state |
| It cannot be hurt now (and its adds must die) | buffs whose stats hold `base_cannot_be_damaged` (32) | "Invulnerable, 4 esbirros" / "Vuelve a ser vulnerable" | all (runtime buff check) |
| A new phase / form | script states that set a `*Phase` music parameter (18 monsters); another boss-bar monster it preloads (29 links) | "Fase 2", "Fase 2: Geonor, el Lobo pútrido" | 14 phase states, 16 forms |
| It enraged | buff ids with "enrage" (44), states named enrage(d) | "Enfurecido" | 8 by state, any by buff |
| Adds | monsters its script preloads (203 links) | counted with "Invulnerable"; "3 esbirros" only while the enemy news (which names them) is off | 44 |
| Where to go inside a danger area | the current skill's GeometryAttack/GeometryTrigger row | one word at the wind-up: "¡Sal!", "¡Al centro!", "¡Detrás!", "¡Muévete!", or the stick way "¡Izquierda!" | 198 have area skills |
| Stand still / keep moving | buff descriptions ("Stop moving or…", "Keep moving…", "Moving breaks the vines") | "¡Quédate quieto!" / "¡Sigue moviéndote!" (21 buffs; was 2 by hand) | King in the Mists, Kulemak, vines, tethers |
| The boss hides until the player finds it | a state whose handler adds the effect pack `fade_by_player_dist` (4 monsters, all Balbala) | "El jefe se esconde: sigue el latido, abajo a la izquierda" and the weak point's heartbeat from its side | Balbala / Pirasha ([§11](#11-hide-and-seek-balbala-2026-11-11)) |
| The boss's line announcing a turn | `PlayTextAudio(line)` in its script, on a skill start or a function | let through the fight's chat mute (41 line groups, 97 variants, 10 languages) | 27 |

## 2. The bosses

- **PROVEN.** 409 MonsterVarieties rows carry the boss life bar (`BossHealthBar`, +0x303), 216 distinct names; 406 ids contain
  "Boss" (266 overlap: "Boss" ids include adds, totems and daemons). `StandaloneBosses` (71 rows) is a subset. The boss
  scripts extend `Metadata/Monsters/BossBase.ot` (`is_boss = true`), which documents its shared states: `hidden` (unaggro +
  `hidden_monster`), `bb_visible`/`bb_collidable` (shared skill events), `disable_life_bar`, `introsequence`, `dead`.
- Campaign bosses met so far (web guides, [§10](#10-sources); names checked against the files): Act 1 the Bloated Miller
  (`SwollenMillerBoss`), Beira (`UrchinHagBoss`), the Devourer (`MudBurrowerHeadBoss`), the Brambleghast, the Rust King,
  the Rotten Druid (`FungusBehemoth`), Draven and Asinia (`HusbandMonster`/`WifeMonster`), Lachlann (`BitterGuyBoss`), the
  Crowbell, the King in the Mists (`VoodooKingBoss2`, treant form `VoodooKingBoss3`), the Executioner
  (`GuillotineExecutioner`), Candlemass (`ManorGargoyleBoss`), Count Geonor (`BaronBossHumanForm` → `BaronBossCorruptedWolfForm`);
  Act 2 Rathbreaker, Rudja, Balbala, Jamanra (Risen King, Abomination), Iktab and Ekbab, Kabala, Azarian, Zalmarath, Tor Gul;
  Acts 3-4, interludes, trials (Zarokh, the Trialmaster) and pinnacles (Arbiter of Ash, Xesht, Olroth, the King in the Mists).

### Mechanic types and counts

Over the 216 boss names, from the data (**PROVEN**): area skills by shape (a boss counted once per shape) circle 194,
ground markers (trigger with several hits) 131, rectangle/line 126, cone 125, donut 42, triangle 3; skill kinds melee 182,
geometry_attack 167, geometry_spell 153, empty_action (triggers) 151, geometry_trigger 133, projectiles 85, ground effects 69,
spawn_object 66, `summon_specific_monsters*` 47, dash 45, stance changes (`change_to_stance*`, form/phase changes) 27,
channelled geometry 11, teleport 9. From the scripts: 214 define states, 200 have a state that stops drawing them (most
the shared `bb_visible`), 132 play voice lines, 59 preload monsters.

From the guides (lower bounds over ~60 campaign/endgame bosses, LIKELY): adds ~30, lines ~28, circles ~28, markers ~18,
cones ~15, objects to break or use ~14, phases ~14, following markers ~10, invulnerable windows ~8, arena changes ~8,
channels ~6, donut/safe spot ~5, hide behind ~3, stand still/keep moving 1 boss, enrage ~2, grabs ~2. The deadliest are
arena-wide blasts with one gap (Arbiter of Ash), Zarokh's hourglass trial, Doryani's Fist and Silverfist's sunder.

## 3. How each type shows in data and memory

- **Hidden / untargetable — PROVEN.** `EntityCatalog.IsInPlay`: the `hidden_monster` buff and the Targetable flag (the
  Devourer toggles it as it burrows; the Count is set aside with `hidden_monster` once the Wolf takes over). Scripts add
  the state values whose client handler calls `DisableRendering()`: 458 (boss, state=value) pairs, 30 bosses with one of
  their own (`emerge_hide`, `tele_hide`, `faded` = the Wolf's fog, `invis`, `hide_me`, `pre_emerge_hidden`). The host's
  `StateMachine` component reads state names and values (as `TrapWatch` does).
- **Invulnerable — PROVEN data, LIKELY use.** BuffDefinitions Stats (+0x1A) with `base_cannot_be_damaged` and without
  `is_hidden_monster`: 32 buffs (`god_mode`, `cannot_be_damaged`, `hallowed_ground_boss_invulnerability`, `atziri_demon_summoning`,
  `aod_boss_divine_buff`…). Which boss gets which is decided by the AI scripts (`.ais`, not shipped): read at run time.
- **Phases — PROVEN data, UNSURE meaning.** Life thresholds live in the AI scripts, not shipped. What the client knows: a
  state whose handlers set a music parameter named `*Phase` (`BaronPhase`, `DoryaniPhase`, `TavakaiPhase`, `AtziriPhase`,
  `DivinityPhase`, `BodachPhase`, `SacredFlamePhase`): 18 monsters. Its value rising is the fight's next part (UNSURE whether
  the first step comes with aggro: held 3 s after the first hit). A form change: a boss-bar monster the script preloads
  (`preload_monster_variety`), 29 links (Count → Wolf, King in the Mists → treant, Draven ↔ Asinia, Doryani, Tavakai).
- **Adds — PROVEN.** `preload_monster_variety` in the boss's script chain: 203 (boss, monster) links over 44 bosses, after
  leaving out monsters named "Daemon" (invisible skill carriers: Balbala's fog, Ketzuli's black hole). `SummonedSpecificMonsters`
  links only 28 of 214 summon skills by SkillID; the preload list is the reliable source.
- **Arena objects and things to break — PROVEN data, not spoken.** `preload_object` lists them (188 bosses, most generic
  effects); breakables that are monsters (Kabala's Bone Wall, Hartlin's Ghost Cannon) are adds and are named by the enemy
  news. Nothing in the data says "hide behind this"; not implemented.
- **Channels, enrage — PROVEN.** Channelled kinds (`geometry_*_channelled`) and skills named "Channel"; enrage buffs (id word
  `enrage`) and states (`enraged`, `enrage`: Crowbell, Rudja, the Sekhemas sentinels).
- **Voice lines — PROVEN.** NPCTextAudio has no link to skills; the boss's `.ot` does:
  `on_effect_start_GSBaronBossColdBreathChannel = { PlayTextAudio( CorruptWolf_Skill_IceBreath_Random, … ) }`. A `…Random`
  id picks among its numbered siblings (`CorruptWolf_Skill_IceBreath_2…6`). Of 2167 calls (841 from animation events, 554
  functions, 474 skill starts, 274 deaths, 24 states), most are attack cries ("Split you!"). Kept as mechanics: lines whose
  id or skill id names a phase, summon, enrage, transformation, channel or hex, or whose skill kind is a summon, a stance
  change or a channel, never ids with flavour words (intro, emerge, death, aggro, taunt, player…): 41 groups, e.g. "The air
  grows colder." (the Wolf's fog phase), "Protect your queen!" (Kabala's summon), "Keep running!" (Ritual of Dance).
- **Movement rules — PROVEN.** BuffDefinitions descriptions: "Stop moving / Stand still" → stop (2), "Keep moving / Moving
  breaks…" → keep (13).

## 4. Counterplay by area shape

The area is the action's skill row (path B of the telegraph study): shape, size, angle, oriented from the boss toward its
target place. `BossCounterplay.Of` returns the shortest way out; directions are the stick's eight, relative to the screen
as the camera draws it (`ScreenProjection.Octant`, as every other direction the mod speaks).

| Shape (GeometryAttack) | Player inside | Said |
|---|---|---|
| circle, centred on the player (within ⅓ of its radius) | any way out | "¡Sal!" |
| circle, off-centre | away from its centre | "¡Derecha!" … the stick way |
| donut (+0x34 = hole radius) | nearer the hole | "¡Al centro!"; nearer the rim: out, as a circle; in the hole: nothing |
| rectangle / line | off the nearer long side, or the far end if nearer | "¡Izquierda!" … |
| cone, triangle under 180° | across the nearer edge, or out past its reach | the stick way |
| cone of 180° or more | behind the boss when nearer than its reach | "¡Detrás!" |
| trigger with several markers | inside their spread | "¡Muévete!" |

Delivery (speech-priority rules): the word is urgent news tied to one attack, so it cuts in (`Urge`, held 0.7 s). It is
said at the wind-up, the first frame the dodge gate is not silent (aimed at the player or its area holds the player, in
reach, on screen) and the area holds the player: the earliest moment a sighted player sees the marking, leaving the dodge
whoosh to time the roll. Once per attack; two words at least 0.8 s apart; none when the blow is under 250 ms away. Only for
uniques (bosses). A distinct cue per shape was rejected: eight cues to learn against one word in the player's language.

## 5. Implementation

- Extractor `boss-mechanics` (`tools/Poe2UiExtract/BossMechanicsExtractor.cs`, pure `BossScripts.cs`, columns `BossColumns.cs`
  found by content with known rows: the Wolf, its werewolf, `god_mode`, `CorruptWolf_Skill_IceBreath_3`,
  `GSBaronBossColdBreathChannel`) → `catalog/boss-mechanics.txt` (244 KB; formats in its header). Batch command, launcher step
  "boss mechanics" version 1, deploy and package lists.
- Plugin: `BossMechanicsCatalog` (pure; `Shared`), `BossPhaseTracker` (pure, per boss: news after 0.8 s, 3 s for a boss with
  forms, nothing before the first hit, phase rises held 3 s after it), `BossCounterplay` (pure), `BossMechanicsWatch` (fed
  by `BossWatch` each 150 ms life poll and each attack decision; speaks `SpeechKind.BossLife`, urgent; boss named only when
  two or more fight), `Announcements.Bosses.cs`, `Poe2AccessibilityCore.Bosses.cs`. Hooks: `BossWatch` (Observe, EndPoll,
  Reset, Attack), `ChatWatcher.KeepInFight`, `StatusAilments.FromBuffId` (catalog rules first), `AttackArea.Count`.
- Logs: `Boss mechanics: N bosses, M mechanic lines, K movement rules` at load; `Boss turn: <text>`; `Boss facts: <monster>
  BossFacts {…} states […] buffs […]` once per distinct reading; `Boss counterplay: …` (Debug).
- Tests: `BossMechanicsTests` (18).

## 6. False-warning risks and guards

| Risk | Guard |
|---|---|
| Short hides (teleport slams, `tele_hide`) said as "vanished" | 0.8 s settle; both directions; LIKELY enough, the protocol measures hide lengths |
| The Count's hide before his Wolf appears said as "vanished" | bosses with forms wait 3 s; the form's "Fase 2: name" replaces it; the old body says nothing more |
| A boss of several bodies (the Wolf is 7 entities) | one text per poll, one form news per monster id; `SpeechRepeats` holds repeats |
| Phase music stepping up at aggro | rises in the first 3 s of the fight are the start (UNSURE values; logged) |
| Area units wrong (sizes taken as world units, Baron cross-check) | a word only when the area holds the player and the gate is not silent; the protocol measures |
| A skill read that is the parent, not its triggered area | no area → no word (never a wrong word); coverage gap logged by the gate |
| Every attack talks | areas only (not melee swings), uniques only, once per attack, 0.8 s apart |
| Lines let through being noise | 41 groups of fight turns only; attack cries stay muted |
| Short line keys matching other speech ("Rise!") | matched only for local speech during a fight |
| Adds said twice | counted aloud only while `AnnounceEnemies` is off |

## 7. Live protocol (the current character: level 22 Mercenary in Act 2)

Read-only; the dev host as usual, `VerboseLogging` true. After each fight: `grep -E "Boss turn|Boss facts|Boss counterplay|Dodge gate|Boss mechanics" logs/accessibility.log`.

1. **Load:** `Boss mechanics: 409 bosses, 936 mechanic lines, 15 movement rules` (distinct texts over 10 languages).
2. **The Bloated Miller** (Riverbank checkpoint, re-entered through a new instance of the area): stand still under his
   slams and in front of his charge. Expect "¡Sal!" or a stick way at each slam's wind-up and "¡Detrás!" or a side for the
   180° charge end (`SwollenMillerChargeEnd`), never for a swing that misses. Note per word: said / hit or not.
3. **Beira** (Clearfell): her frost sigils and the ice-spike ring (donut, 1): "¡Al centro!" or out; the corpse markers: "¡Muévete!".
4. **The Devourer** (Mud Burrow): each burrow "El jefe ha desaparecido", each emergence "El jefe ha vuelto"; no line for a
   burrow under 0.8 s. Count burrows against lines.
5. **Count Geonor** (Ogham Manor): the human form's rectangles (impaling dash, lacerate) → sidestep words; near 50 %:
   "Fase 2: Geonor, el Lobo pútrido" and no "ha desaparecido" for the Count; the Wolf's fog: "The air grows colder" read
   (Spanish line) and the fog fade (`faded=1`) as "ha desaparecido" / "ha vuelto"; its ice breath line and cone.
6. **The King in the Mists** (Freythorn, if reached): "¡Quédate quieto!" / "¡Sigue moviéndote!" with the ritual lines
   ("Keep running!"), the treant form as "Fase 2: …".
7. **Act 2** (as reached): Rudja's enrage ("Enfurecido"), Jamanra's sandstorm (invulnerable and the scarabs counted),
   Kabala's summon line.
8. **Read off:** per boss, every `Boss facts` line (which states and buffs changed when), whether each word fit the marking
   on screen (from the video), the hide lengths, and any `Invulnerable` buff seen. Append the numbers here; tune
   `SettleMs`, `EngageGraceMs`, `WordGapMs`, `MinWordLeadMs`.

### Live runs of 2026-11-08

Step 1 passed: `Boss mechanics: 409 bosses, 936 mechanic lines, 15 movement rules` on the development host and on the installed
package (after its game data update). The morning's scripted walk reached no arena; why, and what in the mod was fixed on the
way, is in [route walking](route-walking.md). In the afternoon the bosses were reached by waypoint and new instances. Every
fight below was on **yarkey (level 22 Mercenary)**, the development host with `VerboseLogging`, `fight_trace.py` beside it.

| Boss | How | Lines | Right / wrong |
|---|---|---|---|
| Beira (`UrchinHagBoss`), Clearfell, new instance, area level 2 | script, standing still under her, 11:47-11:48 | one `Boss facts` (engaged; no hide, invulnerability, enrage or phase), `Boss defeated`; no `Boss turn`, no word | nothing wrong said, and nothing said for her attacks: `ice_spear_combo` (cold projectiles), `charged_cleave` and `detonate_corpses` have no area in the tables (the corpse detonation is an ExecuteGEAL parent with no geometry row), so no word can follow: a coverage gap (telegraph path A, §8), not a fault |
| The Devourer (`MudBurrowerHeadBoss`), the Mud Burrow, two fights | script, standing still, 12:15-12:19 and 12:26 | 7 burrows said ("El jefe ha desaparecido"), each with its return ("El jefe ha vuelto"): 4 of the first fight's 6, 3 in the second; 3 "¡Muévete!" (`circle -> Move`, the goop `MudBurrowerGoopGT`), blow in 2.0-2.7 s | no wrong line. The goop word checked against the trace hit the standing player 2.7 s later, as said. The first fight's two unsaid burrows were short (the flag down 0.6 and 0.95 s, at the 0.8 s settle). Each return was said 0.93-0.95 s after the boss was back |
| Beira again, Clearfell | **the user, by hand**, 13:11 | one `Boss facts`, defeated in 3 s | nothing to judge |
| A ritual's Devourer (`MudBurrowerHeadBossCampaignSTANDALONE_`, Clearfell's ritual altar) | the user, by hand, 13:13-13:14 | "Jefe a la vista: El devorador"; "¡Sal!" twice (`circle -> Out`, blow in 2.1 s), "¡Muévete!" once (2.0 s); one burrow said 0.94 s after it began and its return **0.47 s** after it ended; life steps (63, 40, 12 %), "Listo para el aturdimiento", "Aturdimiento total", "derrotado" | the player, moving, took no hit at the words' blow times |
| A ritual's Horror of Thorns (`CarnivorousPlantEaterOldForestCampaignSTANDALONE`) | the user, by hand, 13:14-13:15 | "¡Muévete!" three times (blow in 0.9 s), "Congelado", life steps, "derrotado" | one hit of 8 life at the third word's blow time (the player moving through it) |
| A ritual's Rotten Druid (`BeehiveBehemoth/FungusBehemothCampaignSTANDALONE`) | the user, by hand, 13:15-13:17 | dodge warnings only, no counterplay word | his mortar (`MMSFungusBehemothProjectile`, MonsterMortar) landed three times unsaid: the telegraph table had no area for any mortar (fixed after, below); he never used his shaped areas (`GAFungusBehemothVineSlam`, `GSFungusBehemothRockSmash`) |
| A ritual's Beira (`Hags/UrchinHagBossCampaignSTANDALONE`) | the user, by hand, 13:17-13:19 | "¡Muévete!" twice (`raise_freeze_01`, `ice_nova_01`, blow in 0.66 and 0.85 s) | both fit (the player stood on the aimed place at the word, 23 and 8 units from it; no life lost). Her corpse detonation (`EGCarrionCroneCorpseDetonate`, an ExecuteGEAL parent, and `HagBossDetonateDead`) has no size in any table (no base radius stat, no geometry row, no link from ExecuteGEAL), so no word can follow it from the data: §7's "¡Muévete!" for the corpse markers stays unmet |
| Count Geonor (Ogham Manor) | not fought | | the user stopped the scripted run: too much for a script (the Manor's level 15 packs killed the level 22 character once on the way) |
| The Bloated Miller (Riverbank) | not reached | | yarkey cannot go back: the Riverbank has no waypoint and the encampment no way to it ([route walking](route-walking.md)); it needs a new character |
| The King in the Mists, Act 2 bosses | not reached | | |

**Tuning from these runs.**
- `BossPhaseTracker.BackSettleMs` = 400 (new; a return was waiting the 0.8 s settle): the user's run said the return 0.47 s
  after the boss was back, against 0.95 s before.
- `SettleMs` stays 800: the two unsaid burrows were the only shorter ones, and the guard against teleport hides has no
  counter-evidence yet.
- `EngageGraceMs`, `WordGapMs`, `MinWordLeadMs` unchanged: no word came within 3 s of the first hit, none was held by the
  800 ms gap, and every word had 0.9 s of lead or more.
- The dodge warning in these fights: [dodge timing, rares and bosses](dodge-timing.md#rares-and-bosses-2026-11-08).

**The user's ritual run (13:12:30-13:19:30, yarkey, Clearfell's ritual altar: four bosses in turn).** 8 counterplay words, 2 boss
turns. Each word, checked against `fight_trace.py`: the player stood within 8-36 units of the aimed place when it was said, and the
skill had a ground area (`bite_vomit_01`/`GAMudBurrowerHeadSlam` circle → "¡Sal!", `spit_epic_01` and the Horror's
`root_cascade_01` (`GTAzmeriCarnivorousPlantVineCascade`) and Beira's frost skills → "¡Muévete!"). The "¡Sal!" lead of 2.1 s is the
clip's own: `bite_vomit_01` lands its `contact` at 2.098 s and the word came at clip position 0.08. The player then moved out every
time but once (one hit of 8 life from the Horror's vines).
- **Mortars now say "¡Muévete!"** (`66d5e59`): every `mortar` line is a circle around its aimed place (the action's target place),
  scattered (count 2), so the player standing there is told to move; its size is whatever the line gives. No table holds a mortar's
  blast: MonsterMortar's columns, its projectile, its stat sets (only a scatter, `projectile_spread_radius`, on 40 of 56) and the
  impact `.ao` were searched; 9 of the 56 share their impact visual with a geometry skill whose size the extractor could copy later.

## 8. Not done

- Objects to hide behind or to break that are not monsters (no data says so); arena-wide blasts with a gap; following
  markers (a moving ServerEffect: path A of the telegraph study, not wired).
- Telegraph path A (effect entities) would give areas for skills the action does not expose; `BossWatch.Telegraph` is the hook.
- Life thresholds of phases (AI scripts are not shipped).

## 9. Scratch tooling (not kept)

Python decoders over saved `.datc64` with the community dat-schema (`poe2/_Core.gql`), a dump of every monster `.ot`
(2806 files), survey/coverage scripts; the numbers above are theirs and the extractor's.

## 10. Sources

poe-vault.com act 1-4, interlude and pinnacle boss guides; maxroll.gg act 1 boss guide; timesaver.gg Arbiter guides (0.3-0.4
era, thresholds not checked against 0.5). poe2wiki and poe2db refused the fetch.

## 11. Hide and seek: Balbala (2026-11-11)

The user's two deaths to Balbala on yarkidius (Warrior 20, the play install's log, 22:42 and 22:44): "El jefe ha desaparecido",
then nothing for 22 s and 17 s while the life fell.

- **PROVEN (log).** Each time she took `hidden_monster` (her states unchanged at that poll: `visible=1`), and 5-8 s later the
  player had `caustic_cloud_balbala` from `Metadata/Monsters/Balbala/BalbalaFog` ("Hazard ground", the hazard cue). Nothing
  else made her leave the fight in either run (her clone strike, `BalbalaFlickerCloneStrike`, did not).
- **PROVEN (files).** `Balbala.ot` (the same in `BalbalaMAP_`, `BalbalaSTANDALONE_`, `BalbalaCampaignSTANDALONE_`): states
  `visible = 1; hiding; multi_stab; knives_visible = 1; collidable = 1`; client `on_or_create_state_hiding_1 = { AddEffectPack(
  '…/Balbala/fade_by_player_dist.epk' ) }`, removed at `hiding_0`: she is drawn by the player's distance. Stance 4 idles in
  `disappear_idle_01` and its GroundSlam is `disappear_slam_01`. Skills `BalbalaHideAndSeekDisappear` (cannot be interrupted or
  stunned) → `BalbalaHideAndSeekSlam` → `BalbalaHideAndSeekSlamImpact` (circle, length 21, fire, `base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked`,
  strongest at its centre). The fog is the daemon `BalbalaFog` (states `fog`, `leaking`; art "centerfog": it grows from the
  room's centre), its buff `caustic_cloud_balbala` "Caustic Ground" (chaos damage). No other monster script uses the pack.
- **LIKELY (guides: maxroll, game8).** From 90 % life: she vanishes, the gas seeps from the centre, she waits at a place of the
  arena and coming near her ends it with her slam.
- **PROVEN (live, 2026-11-12, the development host's log 06:25-06:28, the user playing by ear).** Three hidings in one fight,
  won: `hidden_monster` first, "El jefe se esconde: sigue el latido, <dirección>" 0.9 s later, `hiding=1` one poll after that
  and through the phase (`visible` drops for a poll and is back to 1 while she hides, so it tells nothing). She was back in play
  after 5.7 s, 7.3 s and 6.4 s (17 s and more than 22 s the night before, without the line and the beat). Her entity stayed
  within the boss watch's range all along.
- **PROVEN (live): she does not hide where she vanishes.** Said at 193, 162 and 253 world units from the player (she vanishes
  out of the melee), she was back 890, 907 and 478 units from that place, 96, 337 and 307 units from the player. So the spoken
  direction may be that of the place she left; the beat follows her entity at every poll.
- **UNSURE:** when she moves to her hiding place (`Boss hiding: … moved to x,y` now logs each 150 units of it), which decides
  whether the direction should wait for her to stop; and her reveal slam: no dodge-gate line for it in the three reveals
  (attacks of a `hidden_monster` are not judged, `BossWatch.WatchNear`), so no "¡Sal!" follows it.

**Implementation.** Extractor: `seek|monster|state` for a non-zero state whose handler adds that pack (`BossScripts.SeekStates`;
launcher step "boss mechanics" 3). Plugin: such a boss out of play, or in that state, is hiding (`BossMechanicsCatalog.HidesToBeFound`,
`IsSeekState`): its leaving is said as `Announcements.BossHiding` with the screen direction, and `BossMechanicsWatch.Hiding` gives
its place to `WeakPointGuide`, whose heartbeat leads there (setting `BossWeakPoint`, no new sound) until it is back in play.
The log has `Boss hiding: … at x,y`, `… moved to x,y` and `… back after N ms …`, and `Boss facts:` for each reading while away.

## 12. Boss by boss: the mechanics still unspoken (sweep of 2026-11-12)

After Balbala the user asked for the other bosses' own mechanics: the work of §1-§9 covered the shared types, not each
boss's script. Two sweeps: the guides (poe-vault for every act, maxroll for Act 2 and the pinnacles; mostly one source, so
LIKELY at best) and the game's files (every state the boss scripts define, their arena objects, the buff texts). The dumps
were scratch files and are gone; the signals below are what to read again. Nothing here is implemented unless it says so.
The user's reveal-slam of Balbala needs no word: the game sounds it (the user, 2026-11-12).

**Done from this sweep.** Six buffs whose text says to move away to break them had no `move` rule (`toxic_grasping_vines`,
`sanctum_binding_chains`, `legionnaire_arrow_tether`, `flask_draining_lasso`, `archnemesis_voodoo_doll_debuff`, and the unused
`tumour_spider_goo_tether`): the extractor's keep-moving pattern now takes "(move|run) … to break" (21 rules, step 4).

| Boss | What the guides say the player must do | Signal in the files (PROVEN unless marked) | State |
|---|---|---|---|
| Draven and Asinia (Act 1) | walk over the souls drifting to Draven; break a wall of Asinia's bone cage (75 %) | state `spirits` (meaning UNSURE); cage not looked up | todo |
| King in the Mists | totems; red circle = stand still, purple = keep moving | `voodoo_king_punishment_*` | done (move rules) |
| Count Geonor | the Wolf jumps out of the freezing fog after each poem line; a sigil freezes for 5 s | `faded` (hide state), voice lines | partly (vanish/back, lines); sigil todo |
| Crowbell | the arena breaks away in three stages | states `first/second/third_arena_complete` run `TriggerBracket(arena_sector_destruction, n)` | todo |
| Jamanra, the Risen King (Act 2) | at 25-30 % immune under a sandstorm: stand in the small circle by the sword, kill the scarabs | `Monsters/PerennialKing/PerennialKingHuman`: `end_fight`, `corrupting` (untargetable, bar hidden), `retreat_caravan`; player buff `perennial_king_tornado_degen` | todo: the safe circle's entity not found yet |
| Kabala | bone cage at ~70 % with one opening, then an arena-wide blast | Bone Wall is a monster (an add); the opening not looked up | todo |
| Zalmarath | a beam over the whole arena at 50-65 %: stand behind the dagger | `Monsters/TitanBoss`: blockages `Shatter1-4Blocking`, `RoofTarget1-4`, `BrainStalagmite` state `shattered` (LIKELY the cover) | todo |
| Jamanra, the Abomination | sandstorm at ~85 %: stand behind Sekhema Asala's shield | not looked up (the shield's entity) | todo |
| Azarian | the arena shrinks; censers | `Monsters/ForsakenSon/BossArenaObjects/ForsakenSonCenser`: `transition_on_damage_taken=1`, state `hits` 0..3; boss states `flaming_censer_l/r` (use LIKELY) | todo |
| Xyclucian (Act 3) | from 90 % it perches on one of 4 pillars: break the pillar | `Monsters/ChimeraWetlandsBoss/ChimeraPillar` (Life, state `damage` 1/2) | todo |
| Mektul | lava fills the arena; he stands in it at 75 % and 40 % | `Monsters/VaalForgeMaster/Objects/VaalForgemasterSluceLever` (interactable, `lever_pulled`), `ForgemasterDam` state `opening` | todo |
| Queen of Filth | (guides: nothing) | player buffs `queen_of_filth_life_curse` / `_mana_curse`: skills reserve life or mana | named by the harmful-buff reading |
| Ketzuli | (guides: nothing) | states `sun_wall_count`, `summon_sun_wall`; objects `SunWall*`, `BlackHole/OrbitSun` (LIKELY) | todo |
| Viper Napuatzi | invulnerable while the arena shrinks: stand where the guards' spears glow | not looked up | todo |
| Doryani | stay inside the lightning cage | not looked up | todo |
| Torvian (Act 4) | make him throw his boulder at the scaffolding to stop the priests' buffs | boss buffs `guard_boss_offensive/defensive/utility_buff`, whose text says exactly that; state `boulder`=1 while he carries one | todo: the only buffs whose text is an instruction |
| The Prisoner | use the ballista to stop his regeneration (50 %, 20 %) | not looked up | todo |
| Diamora | her gaze sweeps half the ring at 65 % and 35 %; 5 looks petrify | not looked up | todo |
| Benedictus | two pillars shrink the arena at 60 %, then a maze of earth walls | `TwilightOrderOverlord`: states `break_pillar`, `maze_start`; objects `PillarLeft/Right`, `MazeRock` (`shatter`), `LightningRod` (`armed`) (LIKELY) | todo |
| Yama | at 75 % clones on the pillars: run to the one that does not react; at 65 % to the clone with the circle | `chosen_one` clone state, not read | todo |
| Tavakai | which totem the player stands nearest decides his power; the room burns | state `room_burning_progress` 0..100; `KaruiCaptainBossJadeWall` (Life); `GodTotem` state `current_phase` | todo |
| Siora (Ogham interlude) | at 90 % the arena goes dark and hurts: run to the glowing monolith | not looked up | todo |
| Zarokh (Sekhemas) | at 60 % and 30 %: enter the centre, collect every hourglass before the clock turns | state `timetrialprogress`; entities `TimeTrialCollectable`, `Hourglass`, `ArenaCentreButton` (`readytoactivate`); player buff `sanctum_chronomancer` | todo |
| Tawhani | (files only) invulnerable while the ancestral warriors live; its element | buff `cannot_be_damaged_sudoku_boss_display`; state `element` 1 fire, 2 cold, 3 lightning | invulnerable: done; element todo |
| The Trialmaster | stay inside the heart's circle | `trialmaster_tether_debuff_display` | done (move rule) |
| Bodach (Ritual) | (files only) a darkness pulls in; Viridi's Sanctum is safe | state `darkness_vacuum_range`; player buffs `darkness_vacuum_aura_damaging`, `ritual_wendigo_tree_sanctuary`; `RitualWendigoViridiTree` | todo |
| Arbiter of Ash | stand in the Flame Seed's circle; two arena-wide blasts with one gap; orbs | state `infuse_buff_type` 1 fire, 2 ice; `no_portals_sector_enabled`; the gap not found | todo |
| Kulemak and the Abyss bosses | (files only) no damage in the darkness; only targets inside the Void take damage | player buffs `kulemak_boss_darkness`, `abyss_lightless_well` | named by the harmful-buff reading |

**Patterns worth a generic rule (files).** Arena objects by component: Life + Pathfinding = something to break (the Baron's
ice walls, the jade wall, bone walls, the Chimera's pillar); `TriggerableBlockage` (42 objects) = walls that rise or shatter;
`InteractionAction` on a fight's object = something to use (the Forgemaster's lever, the Aberration's `BarrierKeyhole`).
`*_arena_complete` / `retreat_caravan` = the arena breaking away. 13 bosses forbid teleports in a sector (`blocking`,
`enable_no_portal_sector`, `no_portals_sector_enabled`). Nothing in the data marks cover ("hide behind this"): UNSURE.

**Not determined.** What Blackjaw's `out_of_breath`, the Vaal statue's `powered` and the King in the Mists' `bubble_shield`
mean for the player; which boss grants which buff (only Torvian's script names its own); thresholds and timings (AI scripts
are not shipped). Not found in the guides: Ashar, Rattlecage, Atziri, the Arbiter of Divinity, the other Sekhemas floors.

**Order.** The user's Warrior is in Act 2 past Balbala: Kabala, Zalmarath, Azarian and both Jamanra fights come first, each
needing its place to go to (an entity's position, as Balbala's) read from the files before the user meets it (`AGENTS.md` rule 26).

### Act 2, from the files (second pass, 2026-11-12)

Thresholds and triggers are in the AI scripts, not shipped; skill-to-animation pairings below are by name (LIKELY).

- **Kabala** (`Monsters/SerpentClanMonster/SerpentClanCasterBoss`). PROVEN: the cage is made of monsters,
  `…/SerpentClanCasterBossBoneWall` (script `SerpentClanMonster/BoneWall.ot`: Life, blocking, state `tilted` cosmetic), from the
  skills `GSSerpentCasterBossBoneBarrierEG` / `GTSerpentBossWall`; the blast is LIKELY `EASSerpentBossCurseWallNuke` (animation
  `spin_detonate_01`, voice line "Kabala_Succumb"). UNSURE: how the open segment differs (probably no wall there; a server-only
  `Objects/BoneWallGapFinder` exists and nothing refers to it). To read live: the walls' places as they rise.
- **Zalmarath** (`Monsters/TitanBoss/TitanBoss`). PROVEN: the guides' "dagger" is the entity `Monsters/TitanBoss/BrainStalagmite`
  (art `TitanSword.ao`, a `TriggerableBlockage` that starts blocked), spawned by `SOTitanBossStalagmite`; the beams are
  `EASTitanBossLaserBeam` / `EASTitanBossLaserBeamRoof` (animations `eye_beam_01`, `eye_beam_long_01`, `roof_laser_01`), their
  targets `Objects/StationaryTitanBossLaserTarget`; `RoofTarget1-4` / `Shatter1-4` state `shattered`. LIKELY: it is the cover.
  UNSURE: whether the beam is stopped by it, and which of several to take.
- **Jamanra, the Risen King** (`Monsters/PerennialKing/PerennialKingHuman`). PROVEN: state `corrupting`=1 makes him untargetable
  (stance 3, `corruption_channel_01`; animation events `sandstormstart` / `sandstormend`); player buffs `perennial_king_sand_storm`
  ("physical damage over time while standing in the Sandstorm") and `perennial_king_corrupted_ground`; totems
  `Monsters/PerennialKingTotem/PerennialKingTotem` (state `pulsing`); scarabs LIKELY `Monsters/EtchedBeetles/VolatileBeetle`.
  UNSURE: the safe circle by the sword, found in no script (a ground effect or the terrain). To read live.
- **Jamanra, the Abomination** (`Monsters/PerennialKingStage2/PerennialKing`). PROVEN: the shelter is Asala, the NPC
  `Monsters/NPC/MarakethMatriarchDreadnought`, state `sandstorm_defence` (no client handler: its meaning is by name), with the
  object `Monsters/PerennialKingStage2/Objects/MatriarchSandstormDefence` and the marker
  `Terrain/Gallows/Act2/2_12_2/Objects/PerennialKingArena1SandstormDefenceMarker`. The boss enters stance 5 with
  `CTS5PerennialKingSandstorm` and leaves with `CTS1PerennialKingEndSandstorm`. LIKELY: the player's buff `asala_sand_shield`
  means "sheltered". Zombies `PerennialKingCorpseFriend1-13`.
- **Azarian** (`Monsters/ForsakenSon/ForsakenSon`). PROVEN: `BossArenaObjects/ForsakenSonCenser` goes out after three hits
  (`hits`=3 removes its collision and light); fire is `BossArenaObjects/ForsakenSonFlameWall` (state `die`) and the ground
  `CGEForsakenSonBurningGround`. UNSURE: what a censer out does for the player, and any shrinking signal.
- **Tor Gul, Rathbreaker.** Nothing beyond attacks and hazards (Tor Gul's `caustic_cloud_snake_boss_vomit`).

**The arena log (`BossArenaLog`).** Silent evidence to confirm a finding with, never the way to one (the user refused
"fight it and I read the log", `AGENTS.md` rule 26): while a boss is fought, every entity within 2000 units of it is logged once (`Boss arena: + path #id
(type) at x,y, N from the boss states […]`, 24 per path) and again at each new reading of its states (`Boss arena: path #id …`),
800 lines a fight at most.

## 13. Where a boss's mechanics are written (third pass, 2026-11-12)

The user will not fight a boss so that its log can be read (`AGENTS.md` rule 26): every mechanic is found beforehand. The
first two passes read only the monsters' `.ot` scripts and the buff texts. These sources hold the rest; sizes in the skill
tables are grid cells (× 250/23 = world units). Scripts and the ExecuteGEAL table: `tools/research/gamedata/`.

### The sources

- **The skill chain — PROVEN** (community `dat-schema` layouts, row sizes checked on this patch). `MonsterVarieties.GrantedEffects`
  (+0xD0) → `GrantedEffects` (ActiveSkill +0x4F, Animation +0x71, StatSet +0xA8, SkillID +0x9B). The SkillID points into a
  table chosen by the skill's kind: `geometry_*` → GeometryAttack.Id; `geometry_trigger` → GeometryTrigger; `spawn_object` →
  SpawnObject (MiscObjects +0x4, whose `EffectVirtualPath` is the entity's path); `move_daemon` → MoveDaemon
  (MonsterVarieties +0x18); `trigger_beam` → TriggerBeam; `effect_driven_*` → EffectDrivenSkill; `execute_geal` →
  ExecuteGEAL by its text id. Summons carry the stat `alternate_minion` (a SummonedSpecificMonsters id); ground effects
  `ground_fire_art_variation` (GroundEffects +0x10 → its buff +0x65). Stats with values: `GrantedEffectStatSets`
  ConstantStats +0x28 / values +0x38, ImplicitStats +0x18; per-level stats are empty for monster skills.
- **ExecuteGEAL — PROVEN, the source the earlier passes missed.** 335 rows of 0xDF bytes: "this skill's area does this to
  that object". GeometryAttack +0x101 is a row index into it (96 rows have one; two have no skill at all, the areas the
  server casts for Asala's shelter). Columns: MetadataIDs +0x7B (the object kinds acted on), Script +0x60 (run on the
  caster), ScriptCommand +0x8B (run on each object hit), more commands at +0x9B / +0xAB; +0x24 LIKELY the search radius.
  It says what is broken or used, what ends a phase and what shelters the player.
- **GeometryTrigger — PROVEN.** +0x24 count, +0x28 delay, +0x34 spread, +0x38 radius, +0x3C angle (0 and 360 both on full
  rings). The enum at +0x2C of both geometry tables (anchor: 2 self, 3 towards the target within range, 4 the point handed
  to the skill, 12 the object that triggered it) is LIKELY, from naming.
- **Animations — PROVEN.** `GrantedEffects.Animation` → the monster's `.act` (action → animation) → the monster's `.ao`
  (that animation's timed events): the lead between a cast's start and its hit.
- **Arena room files — PROVEN.** WorldAreas.Bosses → `Metadata/Terrain/…/Rooms/**/*.arm`, UTF-16 text: object lines (`x y z
  rot … "ao" "object path"`), named points (`x y rot "name"`), sectors (`"arena" x1 y1 x2 y2`), in cells of the room. The
  arena markers (ArenaMiddle, SandstormDefenceMarker) are server-only and LIKELY never reach the client; `BossRoomMinimapIcon`
  does and sits within a cell of the arena's middle: the run-time anchor, though a room may be placed rotated.
- **The game's words — PROVEN, weak.** NPCTextAudio (Text +0x18) through the scripts' `PlayTextAudio`; `BuffVisuals.BuffDescription`
  (+0x69), a second buff text the mod does not read (e.g. `corrupted_blood_rain`: "Move to the safe area to escape the rain").
- **NOT USABLE.** Effect files (`.ao`/`.pet`/`.epk` of arena effects hold emitters, no radius). AI scripts: `MonsterVarieties.AISFile`
  (+0xE0) names them and none of the 1,438,656 bundled paths is an `.ais`; poe2db's "AI script" block is state names and flags.
  So life thresholds, the order of phases and where the server puts its own objects cannot be known offline.
- **Outside the files.** poe2db.tw (`https://poe2db.tw/us/<Name>%2C_<Title>`): each boss's skills with internal ids, cast
  times, cooldowns and "cannot be dodged/blocked" flags; reliable for ids. poe2wiki blocks fetches; maxroll and poe-vault
  are short prose (thresholds come only from there).

### The method, for any boss (to become an extractor step)

1. Its MonsterVarieties row: skills, `.act` / `.ao`, the `.ot` chain (states, lines, preloads). 2. Each skill through its
kind's table: shape, size, anchor, count, delay; the monster it summons, the object it spawns, its daemon, its ground buff;
the cannot-be-dodged flags. 3. Each GeometryAttack with an ExecuteGEAL: the objects it acts on and what it does; and the
ExecuteGEAL rows no skill uses. 4. Skill animation → `.act` → `.ao` events: the warning's lead. 5. The arena's `.arm`: the
placed objects around the `BossRoomMinimapIcon`, the sectors. 6. Texts: both buff descriptions, the scripts' lines. 7. Per
mechanic, the run-time signal: a state, a buff id, an entity path, the skill's animation.

### Act 2, answered

- **Kabala's cage.** `EASSerpentBossCurseWallNuke` (animation `spellcast_curse_01`, event `walls` at 0.74 s); `GTSerpentBossWall`:
  80 points, 200 ms apart, spread 52, radius 50 (Asinia's `GTWifeMonsterBoneWall`: 38 at 26/27). 80 walls of size 4 on a ring
  of ~51 cells stand ~4 cells (4.5°) apart, a closed ring: the opening is walls that are missing. Which ones is the
  server's choice. **At run time:** the wall monsters (`…/SerpentClanCasterBossBoneWall`; script extends
  `Metadata/Utility/InanimateObject` with `Positioned { blocking = true }` and Life) lie on a circle of ~545-565 world units;
  the widest hole between neighbours is the way out. The blast after it: `spin_detonate_01` (`detonate` at 2.86 s) throws 15
  orbs within 25 cells, then `GASerpentCasterBossTailSwipeDetonation` (360°, length 120, cannot be dodged, evaded or blocked)
  sets them off. Also `GSSerpentCasterBossBoneBarrierEG`: each nearby `SkeletonSnakeSerpentCasterMinion` beams into her and
  she gets `serpent_boss_bone_armour` (blocks from every side).
- **Jamanra, the Risen King.** The room (`2_3/Rooms/Unique/boss_endcap.arm`) has the arena's middle, 27 totem markers and no
  sword or circle. The sandstorm phase is stance 3 (`sandstormstart` at 0.48 s): 30 beetles over 15 s within 50 cells
  (`GTPerennialKingVolatileBeetles`), totems casting corrupted ground (`perennial_king_corrupted_ground`); it ends when Asala's
  `EGPerennialKingAsalaStun` sets `corrupting` to 0 (ExecuteGEAL 1025). The guides' "circle around the sword, the arena
  explodes outside it" is another skill, not found: `GAPerennialKingSwordSlam` is a plain circle of 20 at the target, not to
  be dodged, evaded or blocked. Open.
- **Jamanra, the Abomination.** The shelter is two server-cast areas, GeometryAttack 10796 (circle, radius 25 = 272 world
  units) and 10797 (rectangle 200 × 20 = 2174 × 217), whose ExecuteGEAL rows 246/247 run `triggerer.AddBuff( asala_sand_shield,
  0.15 )`: the buff on the player means sheltered, refreshed every 0.15 s. Asala's state `sandstorm_defence`=1 plays her line
  `AsalaWildGatesTen` ("Quickly! He conjures the very sands against us!") and her barrier (`shields_up` at 1.43 s). The room
  (`2_12_2/…/boss_cart.arm`) has 8 defence markers in two rows ~315 world units either side of the middle. **At run time:**
  lead to Asala while `sandstorm_defence`=1; say the shelter reached when the buff appears.
- **Zalmarath.** The sword is `SOTitanBossStalagmite` → `Metadata/Monsters/TitanBoss/BrainStalagmite` (a blockage). The
  blast is `GSTitanBossMegaBlast`, a 90° cone of length 150 on `sword_blast_0x` (`plant_sword` at 1.29 s, `nuke` at 5.77 s,
  then a hit every 0.1-0.2 s); the eye beam's hits (`GSTitanBossLaserImpact`) "can be blocked from all directions".
  `GATitanBossRockFallLarge` shatters the platforms `shatter1-4`; `EGTitanBossSpawnWisps` starts the 8 `VolatileSpawner`.
  Whether the sword stops the blast is the server's (two guides say to stand behind it).
- **Azarian.** Four `ForsakenSonCenser` in a square around the arena's middle. ExecuteGEAL 1021 on a censer: `TriggerSkill(
  CGEForsakenBurningGround, triggerer ); triggerer.Delete();` and on the boss `AddLifeThreshold( -20 )`: a censer that goes
  out leaves lasting fire where it stood and moves his next threshold. Keep away from the censers' corners.

### Implemented from §13: the way out of a boss's walls (2026-11-12, not heard live)

Extractor: `wall|boss|monster` for an add with no boss bar and no skill whose scripts extend `Metadata/Utility/InanimateObject`
and block the ground (`Positioned { blocking = true }`); on 0.5.5f only Kabala's Bone Wall (the King in the Mists' death
effigy and the Breach bell are inanimate and blocking too, and have a skill: no walls). `MonsterVarieties.GrantedEffects`
(+0xD0) is found like the other columns (`BossColumns.MonsterSkills`); launcher step 5. Plugin: `BossMechanicsWatch` hands the
walls in play to `BossWallsWatch`; `WallRing.Read` (pure, tested) fits the circle they stand on, closes around each wall
0.75 of their usual spacing, and walks the walkable grid from the player until it is 6 cells beyond the ring: that cell is
the way out (`Open`); none means `Closed`, and the place is the nearest wall. Said once the walls' count and the answer
have held 0.6 s: "Muros: sigue el latido hasta la salida, <dirección>" / "Muros por todos lados: sigue el latido y rompe
uno, <dirección>", then "Fuera de los muros"; the weak point's heartbeat leads there (`BossMechanicsWatch.WayOut`). Log:
`Boss walls: N walls, Open|Closed|Out, to x,y, …`. **Unknown until a fight shows it** (the log will, no one is asked to
play for it): how many of the 80 places get a wall, and whether the grid's walkable cells match the arena's real openings.

## 14. The census: every boss's mechanics, by class (2026-11-12)

The method of §13 run over all 409 boss-bar rows: 183 fights (variants merged by script name once Map / STANDALONE /
Invasion / Spring is removed, forms joined to the boss that preloads them), 154 with their arena room, 1,503 rows of
(fight, mechanic). Data: `tools/research/gamedata/census/` (`mechanics.tsv`, `cards/`, `content.txt`), rebuilt by `cards.py`.
Each class is a **data signature** the extractor can apply, so one rule and one handler cover every fight that shares
it. "Linked buff" = a buff reached from the fight's own data (a skill's area buff, a ground-effect stat, a summon buff, an
ExecuteGEAL `AddBuff`, `preload_buff`, a `*Buff(x)` call, an `on_gain_x` handler), or LIKELY one whose id holds the script's name.

| Class | Signature | Fights | Run-time signal → what the mod does | State |
|---|---|---|---|---|
| H gone/back | a state handler calling `DisableRendering(` or `DisableTargetable(` | 147 | the state → "desaparecido" / "ha vuelto" | done (`hide\|`); `DisableTargetable` not yet |
| NP no teleport | a room sector `hide_teleports` / `disable_instant_portals` / `no_portals`, or a `*portal*` state | 104 | the player inside the sector | not worth a word |
| U unavoidable | skill implicit `base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked` | 71 | the skill's action → the area's word | check against the unblockable cue |
| BR boss acts on an object | a boss skill's ExecuteGEAL listing non-monster objects | 53 | the object's state (shatter, explode, destroyed) | todo |
| A adds | preloads, summon stats | 45 | done (`add\|`) | done |
| US use | an arena or preloaded object with `InteractionAction` / `Interactable` | 37 | the entity → lead to it | todo (the scanner names some) |
| G hazard ground | a linked buff with `*_damage_taken_per_minute` | 32 | the buff on the player | done (hazard cue) |
| OB break | an object or skill-less add with `Life`, `transition_on_damage_taken=1` or `attackable` | 26 | the entity and its states → lead to the nearest | todo (LIKELY: breaking is not always good, Azarian's censers) |
| D hidden attack | a skill whose action is in a stance the boss takes while out of play | 23 | not judged today (`BossWatch.WatchNear` skips hidden monsters) | todo |
| AR arena change | a state handler with `TriggerBracket(` (not on death), or a state named arena, burn, dark, flood, lava, collapse, shrink, sandstorm, maze, retreat, platform | 17 | the state → "the arena…" | todo |
| D daemon attack | an area skill owned by an invisible helper or move-daemon monster | 16 | the attacker is not the boss: not judged today | todo |
| MG progress | a state named progress, collect, timetrial, readytoactivate, count | 14 | the value → say it | todo |
| P phase / form | `SetGlobalAudioParam(*Phase)`; a preloaded boss-bar monster | 9 / 9 | done | done |
| I enrage | a state or buff whose last word is enrage(d) | 8 | done | done |
| GR grab | a skill id with Grab / Cocoon / Entomb / Devour / Imprison, or a linked buff "imprisoned" | 7 | the action or the buff → "atrapado" | todo |
| CV cover | the boss's `spawn_object` makes a `TriggerableBlockage` that starts blocked (and it has a skill of length ≥ 60) | 7 | the entity → lead to its far side from the boss | todo |
| EM empowered | a linked buff saying "are empowering", "protected by", "while … live"; or a GEAL on adds that changes the boss | 7 + 2 | the buff on the boss → say its text | todo |
| EL element | a state named element / infuse | 5 | the value → the damage to come | todo |
| GZ buff zone | a GEAL `AddBuff(x)` on whoever is inside, no object list | 4 | the buff on the player | todo |
| SH shelter | a GEAL `AddBuff` of a shield buff, or "You are within …, gaining …" | 3 (+2) | the buff = sheltered; lead to its source | todo |
| W wall | a skill-less add extending InanimateObject, `blocking = true` | 3 (Asinia, Kabala, Lady Elswyth by the census's looser reading; the catalog has Kabala) | done (`wall\|`) | done; see why the other two are missing |
| ST stacking | a linked buff with `ShowCount=1` and stack text | 3 | its count on the player | todo |
| M stop / keep | the description patterns | 2 / 3 | done | done |
| CL clones | a state named clone, chosen, real, fake, decoy (Yama's `YamaFutureClone`: `chosen_one`) | 3 | the clone holding it → lead to it | todo |
| TE tether | buff text tether / chain / leash | 2 | the buff | partly (`move\|`) |
| DZ damage rule | "deal no Damage while in …", "only … inside …" | 1 | the buff on the player | named by the harmful buffs |
| S seek | `fade_by_player_dist` | 1 | done | done |

`BuffVisuals.BuffDescription` holds three instructions in the whole game, one a monster's and linked to no boss: no rule.
Invulnerability cannot be tied to a boss offline (the server decides); it stays a run-time buff check.

**Fights with such mechanics, by content** (letters as above; without = only A, H, NP, U).
Act 1: Asinia W P US, Candlemass US, Count Geonor BR OB P, Draven BR P US, Gorian BR OB, Lachlann OB, the Crowbell AR GR I,
the Devourer BR OB, the Executioner D OB US, the Rotten Druid OB, the Rust King P TE; without: Anundr, Beira, the Bloated
Miller, the Brambleghast, Tierney. Act 2: Azarian G OB US, Balbala S BR G US, Jamanra the Abomination AR BR D EM G SH, the
Risen King AR D EM G PH SH TE, Kabala W AR BR EM OB, Rathbreaker D, Rudja BR I US, Tor Gul BR US, Zalmarath BR CV D G OB US;
without: Ekbab. Act 3: Bahlak G, Blackjaw BR, Chetza BR P US, Doryani BR D G P, Doryani's Triumph BR P, Ignagduk BR CV D MG,
Ketzuli MG, Mektul BR CV D US, Oloton BR, the Queen in the Mists D, Rootdredge BR, the Queen of Filth BR MG OB US, Viper
Napuatzi D, Xalapa G MG, Xilozoma OB, Xyclucian OB, Yaota BR MG OB US, Zicoatl BR D TE; without: Silverfist, Uxmal, Zekoa.
Act 4: Benedictus AR BR CV G TE, Hartlin TE, Diamora BR, Tavakai AR BR G MG OB P, the Blind Beast BR D I, the Prisoner D G OB
TE US, Torvian EM G OB, Yama CL D G; without: the Great White One, Krutog. Interludes: Akthi D MG US, Azmadi D EM OB P US,
Elzarah G TE, Godwin I OB US, Heldra BR, Lady Elswyth W BR D, Lythara D, Oswin BR CV GZ OB US, Rakkar AR BR G, Sigbert BR I OB
US, Siora D TE, Stormgore AR, Thane Wulfric G, Vornas BR, Zelina AR EL MG, Zolin AR BR D EL MG; without: Isolde, the Yeti.
Sekhemas: Ashar D MG US, Hadi I MG OB US, Rafiq BR I MG OB US, Zarokh GZ MG TE US; without: Rattlecage. The Trialmaster BR
(the census filed him under Act 3 by his area). Pinnacles: the Aberration D US, the Arbiter of Ash AR BR D EL EM G P US, the
Arbiter of Divinity AR BR D P US, the Bodach AR D G P SH US, the Raven Trickster BR CL D TE US. Breach: Esh BR G, Tul AR G,
Vruun and Xesht G GR M TE. Abyss: Igrulog US, Tasgul AR G TE US, Vandroth US, Kulemak DZ G M US. Delirium: Kosis D OB TE,
Omniphobia OB ST. Expedition: Olroth GR, Styrn CL US, Tetzcatl BR D TE, the Improbable Spawn D, Uhtred BR ST, Vorana G.
Hellscape: the Eater of Flesh D GR MG OB, the Skittermind G OB. Incursion: both Atziri P, Cualli BR CV D, Huitzil BR D EL,
Kulka BR, Menmek BR D TE, the Royal Colossus BR, Xipocado AR, both Xolotl BR. Maps: Gorian BR OB, Ignatia BR, Iktab BR, the
Lord of the Pit BR, Phyx G, the Precursors TE, the Black Crow AR GR I, the King in the Mists EM G GR GZ M P ST US, Torrek TE,
Varloch G. Unplaced: Tawhani EL EM GZ, Phanos D G, the Immured Fury D G; fifteen rogue exiles, nothing.

**Lost today because only the boss's own current attack is judged.** Daemon attacks: Jamanra's obsidian storm, Zalmarath's
laser target, Menmek's and Tetzcatl's lasers, Kosis's weapon slams, the Bodach's mothballs, Siora's `DarknessDaemon`, the
Forgemaster's lava sunder, the Raven Trickster's spells, the King in the Mists' rat tornado. Attacks made while out of
play (23 fights, UNSURE): Ashar's and Akthi's `burrow_leap_01`, Azmadi's `chrono_leap_slam_end_01`, Doryani's mech stance.

**Not knowable offline.** Which buff the server gives to whom and when; where it puts its own objects (Kabala's gap,
Asala's shelter among 8 markers, Yama's chosen clone, Zarokh's hourglasses: the files give the candidates and the paths);
whether a server-only object with a client section reaches the client; whether a cover stops a blast; a room's rotation.

**Order of work** (most fights first, each a rule plus a handler): 1. D: judge the attacks of a fought boss's daemons and
of a boss out of play (39 fights). 2. EM: say the text of a boss's buff that has one (the game's own words; Torvian,
Tawhani, Azmadi, the Arbiter). 3. AR: the arena changing, by its state. 4. Places to lead to, one handler: SH shelter, CV
cover, CL the true clone, US use, OB break (each said with the object's own name). 5. MG, EL, ST, GR: values and holds.

## 15. The census's rules in the extractor (2026-11-13)

`Poe2UiExtract boss-mechanics` (launcher step 6) now writes the census's classes for every boss-bar row (the census merged a
fight's variants; the catalog has a line per variant). Code: `BossCensus*.cs` gathers a fight (`BossFight`: its own script
chain, its skills and the skills their triggers fire, its adds, objects, the server's ExecuteGEAL rows named for its people, its
linked buffs); `BossCensusRules` / `BossStateRules` (pure, `BossCensusTests`) judge it; `BossCensusColumns` finds the new
columns by content, each checked on a known row (Azarian's censer GEAL, Kabala's bone barrier, Zalmarath's sword, Omniphobia's
stew, Jamanra's obsidian storm, Siora's storm, Asinia's wall). A table or column not found leaves the census's classes out, said,
and the rest of the catalog is written. Shared bases (a script extended by 25 or more, as `scripts.py` judged) give no state,
handler or preload to a fight. Arena rooms: every `.arm` is read (the step went from ~1 s to ~7 s); a fight's rooms are those of
its WorldAreas (Bosses +0x9C, Topologies +0x60 on 0.5.5f) and those placing its objects or an object of its folder, scored as
`cards.py` did, the boss rush's areas (bosses of more than 4 folders) left out. Paths are written as the game writes them; three
(the Bodach's buildable effigies, found only beside his script) only in the index's lower case. Buffs named by `empowered`,
`shelter`, `zone`, `stack`, `grabbed` get `text|language|buff|name|description` lines (links as shown), 10 buffs × 10 languages.

| Class | Rule as implemented | Lines (variants) | Against the census (fights matched / census fights) |
|---|---|---|---|
| D | an add (preloaded, summoned, a skill's move-daemon, an NPC sharing the boss's skill family) named "", Invisible or Daemon, with a helper's word in its path, or a move-daemon, that has a GeometryAttack/Trigger skill; 4th field its own skills | 64 (45) | 18/18; 5 more fights by their move-daemons, which the census only took when helper-named (Hartlin's and Torrek's Volatile Core, the Bodach's Darkness Arm, the Rotten Druid's vine, Vruun's and Xesht's hand portals, Esh's corpse) |
| AR | a state handler calling `TriggerBracket(`, or a state whose words start with arena, burn, dark, flood, lava, collapse, shrink, sandstorm, maze, retreat, platform; kind from the words, else change | 33 (21) | 8/17: left out the fight's end (`dead`, `fight_complete`, `on_defeat`: Jamanra, Tavakai, Zelina, Zolin), a non-state handler (Xipocado's skill event), a measure (`darkness_vacuum_range`), words outside the list (fog, sector, no_portal: Rakkar, Stormgore, the Arbiters, Tul) |
| EM | a linked buff saying "are empowering", "empowered by", "protected by", "while … live/remain" (not "while standing") | 3 (3) | 2/8: Torvian, Tawhani. The census's other 11 rows were ground hazards ("while standing in the Sandstorm") |
| EM consume | a GEAL on monsters whose text reaches back to the boss (`this.`, `boss.`, `*_boss`), not on the boss itself | 10 (5) | 2/2 (Kabala's snakes, the Parasite Host's parasites), one line a monster |
| SH | a GEAL with no object list adding a shield-named, harmless buff; or a harmless linked buff "you are within … / while in …" that regenerates or protects | 9 (9) | 3/5 = the census's SH rows (asala_sand_shield in both Jamanra fights, the Bodach's sanctuary); its looser `shelter|` rows (SH shelter?) not ported |
| refuge | the add named by a shelter buff's first word (Sekhema Asala) whose script plays a line or animation on a non-zero state named for a defence, and who is in the fight: brought, placed in its arena, or its state named by one of the fight's objects | 4 (4) | new: only Asala, `MarakethMatriarchDreadnought_`, `sandstorm_defence`, for the four Abomination rows (the object `PerennialKingStage2/Objects/MatriarchSandstormDefence` names the state); not for the Risen King, who shares her skills but not her arena |
| CV | a spawned object that is a `TriggerableBlockage` and blocks from the start (`start_blocked` or `blocking`), in a fight with an area ≥ 60 long | 8 (8) | 3/7: Zalmarath, Benedictus, the Beast; Ignagduk's effigy, Mektul's and Cualli's spikes, Oswin's ice spike are no blockage (the last three have no long area either) |
| CL | an object or add whose name says clone, illusion, decoy, mirror, fake or copy, and the state it declares marking the true one (chosen, real, true, original, fake, decoy) | 65 (5) | 2/4: Yama's 13 clones; the census's boss states (Styrn's `clone_slam`, the Raven's `mirror_minions_on_cooldown`) mark no clone |
| US | an object with InteractionAction / Interactable / Usable, not a way out, loot or NPC (the census's list, now case-insensitive) | 122 (88) | 31/39; the rest were the census's lower-case paths slipping past its own list (loot proxies, transitions, an entrance) and Candlemass's and the Sentinels' glyphs, on that list too |
| OB | an object with Life, `transition_on_damage_taken=1` or `attackable` that the client sees; a named skill-less add with Life that is no wall | 89 (71) | 22/34; the rest were bases of the boss's own scripts and a regional copy (`….japan`) the census took for objects |
| MG | a state of the boss named progress, collect, timetrial or count; 4th field the largest number a handler compares it with (none on 0.5.5f) | 12 (12) | 5/14; objects' states (`readytoactivate`, `anim_progress`, the blood orb's `progress`) have no place in the line |
| EL | per value of a state named element / infuse, the one element its handler names (fire, cold, ice, frost, lightning, chaos, physical); `state|-` when none does | 14 (10) | 5/5: Tawhani 1 fire 2 cold 3 lightning, the Arbiter of Ash 1 fire 2 cold, Zelina, Zolin, Huitzil `elemental|-` |
| ST | a linked buff the game counts (ShowCount +0x2F) whose words say stacks | 8 (8) | 5/6; Uhtred's lowered resistance says no stack |
| GR | a skill id with Grab, Cocoon, Entomb, Encase, Devour, Swallow, Imprison, Prison; `grabbed`: a linked buff saying "you are imprisoned / held / trapped / entombed …" | 7 (7); 0 | 4/4; `grabbed` none on 0.5.5f: the census's rows were a warning ("Stop moving or you will be imprisoned", already `move|`) and Xesht's web, which slows |
| GZ | a GEAL with no object list adding a buff that is no shelter | 12 (12) | 5/5 (Tawhani's horn buffs her allies) |
| U | a skill whose implicit or constant stats hold `base_skill_cannot_be_avoided_by_dodge_roll_or_evaded_or_blocked` | 436 (204) | 79/79, the same 173 skills |
| W | the old wall rule, applied to every add, summoned too | 11 (11) | 3/3 |

**Walls.** Asinia's `WifeMonsterBoneWall` and Lady Elswyth's `VampireQueenBoneWall` are walls by the files: no skill, extend
`InanimateObject`, `blocking = true`. They were missing because the wall rule read only the boss's preloaded monsters and these
two are a skill's summons (`alternate_minion` → SummonedSpecificMonsters); the rule now takes any add.

**U against the unblockable cue.** Of the 144 skills, 17 already reach `hit-times.txt` with `noblock` (they also carry
`base_cannot_be_blocked`); 51 have a timed line without it, because `SkillFlags` reads only `base_cannot_be_blocked`,
`skill_cannot_be_blocked` and the parry stats (a one-line fix: map this stat to `noblock` too, then regenerate hit-times); 73
have no animation of their own (areas a trigger fires, as the Crowbell's double slam), so hit-times has no line to flag; 4 have
an animation the boss's `.act` lacks (Tavakai's hook toss and stab spirals, Xipocado's repulse slam).

**D against hit-times.** Of the 21 daemons, 13 have no animated skill (their skills have no Animation; hit-times holds only the
basic Melee of their shared `.ao`, e.g. `MDTitanBossLaserTarget|1=400/1000`, `MDPerennialKingObsidianStorm_|1=-`,
`RatTornadoMoveDaemon|1=-`); 5 play a timed attack of their own (Esh's corpse 6352 ms, Xesht's hand portal 1367 ms, the Eater of
Flesh's tentacle 796 ms, the Bodach's Darkness Arm 666 ms); 3 have an animated skill with no timed event (the Bodach's two
mothballs, Siora's `DarknessDaemon`). Every daemon has at least one skill with a `telegraph-skills.txt` area line, so the
plugin can time most daemons only by their distance to the player and their skills' areas, not by an animation.

**Left out, as loose in the census:** SH/area object?, SH shelter?, NB named buff with no text, TE beam link, HL heal, PH
phase-break (a GEAL on the boss itself), DZ damage rule, MK mark, BT buff text, BR the boss acting on an object (no line in the
contract), NP no-teleport, D hidden attack.

## 16. The census in the plugin (2026-11-13; unit tests and the play log, none heard in a fight)

One handler per class, fed by the catalog lines of §15. `BossMechanicsWatch.Census.cs` wires them; each rule is a pure
class with tests (`BossFightTests`), and `TheShippedCatalogHoldsTheFightsThePlayLogMet` holds the shipped catalog to what
the play log saw.

| Class | Signal at run time | What is said or sounded | Where |
|---|---|---|---|
| D a boss out of play | the fought boss, with the game's `hidden_monster` buff | its attacks are judged as before (dodge cue, way out); a boss not yet fought stays silent | `BossMechanicsWatch.StrikesForBoss`, `BossWatch.WatchNear` |
| D helpers | a `daemon\|` entity near the fought boss whose own skill's area (`telegraph-skills.txt`), centred on it, holds the player; with no area in the files, within 150 world units | the dodge cue from its side and the way out ("¡Sal!", "¡Muévete!"), once per approach, again after 1.5 s clear of it | `BossPlacesWatch.NoteHelper`, pure `BossHelperWarnings` |
| U unavoidable | the attack's skill is an `unavoidable\|` one (also `;noblock` in the hit times since step 5) | the unblockable cue at the wind-up | `BossWatch.WatchAttack` |
| EM | an `empowered\|` buff on the fought boss; `consume\|` adds coming into play | "El jefe: Fe de la Orden del crepúsculo. Los sacerdotes…", "… se acabó"; "Sus esbirros lo fortalecen" (at most every 8 s) | pure `BossSignalTracker`, `NoteFeeders` |
| AR | an `arena\|` state of the boss rising from 0 | "La arena arde / se derrumba / cambia", "Tormenta de arena", "El jefe se retira"; the same state no sooner than 3 s | `BossSignalTracker` |
| MG, EL | a `progress\|` state moving; an `element\|` state value | "Progreso 3" (every 3 s at most; by quarters where a top is written, none on 0.5.5f); "Elemento: fuego", "Cambia de elemento" | `BossSignalTracker` |
| SH | a `refuge\|` entity with its state set; a `shelter\|` buff on the player | "Refugio junto a Asala: sigue el latido, arriba", the heartbeat until "A cubierto"; "Fuera del refugio" while it is still up | pure `BossPlaces`, `BossPlayerBuffs` |
| CL | a `clone\|` entity with its state set | "El verdadero: sigue el latido, …", the heartbeat while it is | `BossPlaces` |
| CV | a `cover\|` entity | "Cobertura: sigue el latido, …", to a body's width behind it as seen from the boss, until stood there once | `BossPlaces.Target` |
| US, OB | a `use\|` / `target\|` entity that has a name and can be targeted | "Incensario, se puede romper, izquierda", once a fight per name, **not urgent and never led to** | `BossPlaces` |
| GZ, ST | a `zone\|` buff beginning on the player; a `stack\|` buff's count held 400 ms | the buff's name and description from the `text\|` lines, the description once a fight (only the description when the harmful buffs' news already names it); "Arcada explosiva, 3" | `BossPlayerBuffs` |
| GR | the boss's attack with a `grab\|` skill, aimed at the player | "¡Agarre!" | `BossMechanicsWatch.GrabWord` |

**What the play log of 2026-11-12 showed** (the owner's fights with Zalmarath, Azarian, Balbala, Jamanra; `Boss arena:` lines
of `BossArenaLog`), and what was built on it:
- A helper is its own entity the host cannot type (`Metadata/Monsters/InvisibleFire/MDPerennialKingObsidianStorm_`,
  `…/MDTitanBossLaserTarget`: a Render component, state `move_daemon` 1 then 2), spawned half a second after the **boss's**
  action `MoveDaemon` (skill `MDPerennialKingObsidianBall`, which the dodge gate already judged) and then walking toward
  the player for 16 s and more. So a helper is not judged as an attacker: it warns by where it is.
- Zalmarath's sword (`TitanBoss/BrainStalagmite`) came twice a fight and has **no Render component**, as have most arena
  markers. Its place is the cell its Positioned component holds, at an offset found live from entities that have both
  ([compatibility](../compatibility.md#found-again-at-runtime-plugin): +0x444 on 0.5.5f, `EntityPlaces`, dev command `positioned`).
- Why things to use or break are not led to: the catalog's `target\|` lines hold Azarian's censers (breaking one leaves
  lasting fire, §13) and Zalmarath's fissure lines (72 in the log, not strikable); the files cannot tell a help from a trap.

**Not done, each for its reason:** BR (the boss acting on an object: "plataforma rota") has no line in the catalog; the
Risen King's sword circle is still not found in the files (§13); a helper's blow is not timed (13 of 21 helpers play no
animation, §15); the counters' tops are in no script; which of the eight markers Asala shelters at is the server's.
