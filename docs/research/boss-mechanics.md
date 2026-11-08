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
| Stand still / keep moving | buff descriptions ("Stop moving or…", "Keep moving…", "Moving breaks the vines") | "¡Quédate quieto!" / "¡Sigue moviéndote!" (15 buffs; was 2 by hand) | King in the Mists, Kulemak, vines, tethers |
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

### Live run of 2026-11-08 (not reached)

Step 1 passed: `Boss mechanics: 409 bosses, 936 mechanic lines, 15 movement rules` on the development host and on the installed
package (after its game data update). Steps 2-7 were not reached: the test drove the character by script through the Mud Burrow
(20 minutes of its loops without the Devourer's arena coming into view), Clearfell (two instances, Beira's arena not seen; the
route stopped at tree lines) and the Clearfell Encampment (its west bridge toward the Riverbank does not walk through), so no
`Boss turn`, `Boss facts` or `Boss counterplay` line was produced and no tuning was done. The dodge part of these fights was
measured on normal monsters instead ([dodge timing, live results](dodge-timing.md#live-results-2026-11-08-055f)).

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
