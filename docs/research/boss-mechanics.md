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
