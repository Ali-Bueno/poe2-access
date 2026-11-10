# Game-data study: what in the files can replace hand-made knowledge (PoE2, bundle index of 2026-11-05 23:40, patch after 0.5.5e)

Read-only study of 2026-11-06. Its scripts and key lists were scratch files, called `FILES/` below and not kept: `tools/Poe2UiExtract`
reads every table named here. What came of it is at the end.

Conventions: dat offsets are `+0xNN` bytes in a row; "row N" is the 0-based row index of the `.datc64` table; stat keys are row+1.
Column names are NOT in the files (`.datc64` has no header); where I name a column it is by content, and I say so.

---------------------------------------------------------------------------------------------------------------------------

## 1. Bundle inventory (`FILES/inventory/`)

* 4,324,290 files in `_.index.bin` (5 paths never parse, the known quirk). Files: `FILES/inventory/all-paths.txt.gz` (path, size).
* By extension (`files-by-ext.tsv`): shader cache and no-extension files 2,885,729; `.header` 440,814; `.tgm` 232,284; `.mat` 206,380;
  `.dds` 152,291; **`.ao` 74,241**; `.ogg` 50,787; `.pet` 37,456; `.sm` 32,136; `.smd` 31,959; `.fmt` 27,218; **`.tdt` 23,270**;
  `.ast` 18,118; `.amd` 17,797; `.tgt` 15,648; **`.arm` 15,582**; `.fxgraph` 12,453; `.tmd` 9,709; `.epk` 8,583; **`.ot` 8,324**;
  `.trl` 5,880; **`.datc64` 2,996**; **`.act` 1,875**; `.et` 1,434; `.mtp` 1,405; `.dgr` 1,321; `.env` 870; `.csd` 589;
  **`.ui` 127**; `.hideout` 84; `.psg` 5; `.json` 4; `.txt` 11; `.xml` 17; `.filter` 1 (`metadata/itemfilters/default.filter`).
  **Absent in this build: `.otc`, `.aoc`, `.uiscript`** (zero files), so entity defaults are in plain `.ot`.
* Top-level folders (`top-folders.tsv`): `art` 1,048,101; five `shadercache*` ~2.9 M; `metadata` 332,952; `audio` 50,914 (all `audio/dialogue`);
  `data` 3,588; `cachedhlslshaders` 3,245; `minimap` 2,810 (rendered minimap `.dds/.mtp` per terrain); `shaders` 197. No `Media/` in the bundles (the install's `Media/` holds textures only).
  `metadata/` splits: effects 188,694; terrain 86,070; items 23,219; particles 10,242; monsters 8,108; pet 2,769; chests 1,231; mapdevices 1,179;
  npc 1,059; shrines 462; projectiles 419; ui 144 (`second-level-folders.tsv`).
* **Data tables: 2,996 `.datc64`. They are all under `data/balance/`.** 1,025 are language-neutral (that folder also holds the English
  strings) and 9 language folders hold 219 text tables each: `german french spanish portuguese russian japanese korean thai
  "traditional chinese"` (1,025 + 9*219 = 2,996). No `simplified chinese` folder (the tool prints "zh-CN: no BaseItemTypes"); simplified Chinese text
  exists only inside `.csd` stat descriptions. Full list with row count and row size: `FILES/inventory/data-tables.tsv` (all) and
  `neutral-tables.tsv` (1,025 neutral: name, rows, row size).
* Largest neutral tables: grantedeffectstatsetsperlevel 41,558 rows; grantedeffectsperlevel 34,301; stats 27,299; npctextaudio 25,452; charactertextaudio 17,983;
  itemvisualidentity 17,260; mods 16,911; npctalkdialoguetextaudio 12,481; passiveskills 9,731; clientstrings 9,661; grantedeffects 8,350; baseitemtypes 5,496;
  buffdefinitions 3,218 (row 0x15F); monstervarieties 2,794 (0x406); npctalk 2,227; animation 1,088 (0x6A); minimapicons 1,050 (0x2A); chests 1,033 (0x19A);
  activeskills 1,148; npcs 981; worldareas 448 (0x2E0); questrewardtype 38; npctalkcategory 38; groundeffecttypes 53; shrines 72; doors 9; traptools 13.
* **No version string, build number or changelog inside the bundles** (searched paths for version/patch/changelog, `ClientStrings`, `LeagueInfoPanelVersions`
  which only lists league names; the exe has no FileVersion; `logs/LatestClient.txt` only has "Engine version 2.10.0"). The patch identity is therefore the
  index itself: size + mtime (what the launcher stamps) or, better, the per-table hash below (`FILES/keylists/_table-hashes.tsv`, 1,025 rows: name, rows, bytes, SHA-1 prefix).

---------------------------------------------------------------------------------------------------------------------------

## 2. Hand-written game knowledge in `Poe2Accessibility/` and its authoritative source

Legend: **OK-live** = already derived from the game; **STALE-RISK** = hand list that silently goes stale; **BROKEN** = already wrong now.

| Where | What is hand-written | Authoritative source I opened | Verdict |
|---|---|---|---|
| `host/GameReader/RemoteEnums/Animation.cs` (cast in `Actor.cs:256`) | 1,084 animation names | `Animation.datc64` col +0x00 string Id, 1,088 rows (section 3) | **BROKEN** for 500 names |
| `.../RemoteEnums/GameStats.cs` (26,943 stats) | stat key -> name | `Stats.datc64` +0x00 Id, 27,299 rows | **BROKEN** after row 4402 (22,541 names off) |
| `.../RemoteEnums/InventoryName.cs` (120) | inventory ids | `Inventories.datc64` +0x00 Id, 121 rows | OK for the first 119 (93 compared agree by row+1... see below), mismatched from value 120 (`UltimatumKey1` vs row `TrialOfChaosBench`), the mod uses only Cursor/Main/Weapon/Offhand/Helm/Ring/Gloves/Boots/Flask (low values) |
| `Services/Combat/DodgeThreats.cs` | 3 word lists (49 "calm", 31 melee, 36 ranged words) matched as substrings of the animation NAME | `Animation.datc64` names; plus per-monster `.act` and `GrantedEffects` (already used by `HitTimeExtractor`) | STALE-RISK (names wrong because of the enum; word lists fuzzy) |
| `Services/Combat/EnemyStates.cs` | 6 buff ids; animation names `HeavyStun*`, `Electrocute*` | `BuffDefinitions.datc64` +0x00 Id: `frozen` row 2, `electrocute_display_buff` 2183, `pinned` 2197, `broken_armour_debuff` 2222, `dazed` 2433, `parried` 2634 all exist. Animations rows 231-233 and 262-265 correct today | OK today; **misses** `HeavyStunForesight` (Animation row 883, not in the mod's list) |
| `Services/Combat/ShieldStance.cs` | `ActiveBlockChannel`/`ActiveBlockEnd` prefixes | `Animation` rows 479-482 = ActiveBlockChannel, ActiveBlockEnd, ActiveBlockChannelMoving, ActiveBlockEndMoving (matches the comment 0x1DF-0x1E2) | OK-live (rows < 584, unaffected by the shift) |
| `Services/Player/StatusAilments.cs` | 14 word groups + 20 "not the state" words matched against buff ids | `BuffDefinitions` Id (+0x00), English description (+0x08), 3,218 rows. Replaying the mod's classifier over all rows: 124 ids classified (Slowed 30, Cursed 22, Ignited 19, Chilled 11, Shocked 9, Poisoned 8, Bleeding 7, Frozen 6, Pinned 3, ...) `FILES/buff_ailment_classification.txt` | STALE-RISK + visible **false positives**: `support_electrocute_lineage_explode_on_death` -> Electrocuted, `life_regeneration_from_curse` and `life_reservation_from_curse` -> Cursed, `slowed_regen_on_hit` -> Slowed, `frozen_in_time`, `sanctum_frozen_hideout` -> Frozen. The description (+0x08, e.g. `[Slow|Slowed].` on `chilled`, "Cannot move or take actions" on `frozen`, "You cannot move." on `pinned`) is a second signal. Several byte-flag columns exist (+0x50: 723 rows incl. ignited, poison, bleeding, hinder, blinded, pinned; +0x15D: only `broken_armour_debuff`, `dazed`), but their names are not in the file |
| `Services/Player/PlayerCharges.cs` | 14 buff ids (`power_charge`, `frenzy_charge`, `endurance_charge`, `spirit_charge`, `*_infusion`, `instilling_*`, `druidic_prowess`, `arcane_surge`, `tempest_bell_combo_tracker`) | All 14 exist in `BuffDefinitions` (checked by id: e.g. row 32 `power_charge`, row 2326 `tempest_bell_combo_tracker`, rows 2608-2610 `instilling_*`) | OK today; a renamed or new charge-like buff is invisible |
| `Services/Player/PlayerCharges.cs` + `ChargeWatch.cs:149-151` Rage | `GameStats.current_rage / maximum_rage / virtual_maximum_rage` | `Stats.datc64`: key = row+1. Real keys today: `current_rage` 6996 (row 6995), `maximum_rage` 6995, `virtual_maximum_rage` 11163, enum values 6995 / 6994 / 11161 | **BROKEN**: the mod reads `maximum_rage`'s slot as "current rage"; key 6995 is `maximum_rage`, 6994 is `gain_rage_on_kill`, 11161 is `is_wielding_claw`. STATUS marks Rage "done (code)", never confirmed live. Also off: `cannot_be_damaged` (enum 18186, real 18189), `is_capturable_monster` (8200 vs 8201) but those two are used only by GameHelper's AutoHotKeyTrigger plugin, not by the mod |
| `Services/Player/BuffIds.cs` | `hidden_monster` | `BuffDefinitions` row 151 `hidden_monster`; flag column +0x6B (61 rows) marks monster-only buffs | OK |
| `Services/Audio/Breakables.cs` | 19 name fragments -> material (pot, urn, barrel, ...), 8 corpse words | `Chests.datc64` +0x00 Id = `Metadata/Chests/...` path, **1,033 rows**. Replaying the mod's lists over those Ids: pot 77, urn 64, barrel 42, boulder 29, bigpot 18, crate 16, statue 11, egg 10, basket 10, bone 8, corpse 33, **715 unmatched** (mostly real chests/strongboxes; 6 odd ones in `FILES/chests_unclassified.txt`). Chest `.ot` files carry `Chest { opening_destroys = false }` in the base only (no `true` anywhere), so "breakable" is NOT a data flag | STALE-RISK: new object names get no material, no error. There are byte-flag columns (Chests +0x50 = strongbox (169 rows, ids under `StrongBoxes/`), +0x13D, +0x8, ...) but unnamed |
| `Services/Scanner/Switches.cs` | file name contains Switch or Lever | 47 `.ot` files with switch/lever in the name; `MinimapIcons` has `LabyrinthLever`; `.ot` of a switch (`metadata/questobjects/four_act2/gates_switch.ot`) has `InteractionAction`, `StateMachine`, `MinimapIcon { icon = "GateMechanismActive" }`, `Render { display_text = "G2GateMechanism" }` (a `ClientStrings` row, 6924) | STALE-RISK; structural signature (components) exists |
| `Services/Inventory/QuestRewards.cs` + `Announcements.QuestReward` (12 hand-translated EN/ES cases) | texture key `Art/2DItems/QuestItems/Reward<Kind>.dds` -> text | **`QuestRewardType.datc64`**, 38 rows: +0x00 Id (`GoldReward`, `ArtificersOrb`, `UncutSkillGem`...), **+0x08 art path**, **+0x10 translated Name** ("Gold", "Artificer's Orb", "Uncut Skill Gem", "Magic Ring"...), +0x18 description. The 9 language folders have the same table | STALE-RISK, and incomplete today: table art paths include `RewardJewellery`, `RewardAmulet`, `RewardBoots`, `RewardGloves`, `RewardArmours`, `RewardGear`, `RewardWeapons`, `RewardMap`, `RewardSkillGemBuff` that the mod does not map (it falls back to the raw kind, `_ => kind`). Only 13 `Reward*.dds` exist in the bundles (all hand-mapped) |
| `Services/Scanner/DeployedObjects.cs` | 3 path prefixes (`monk_bell/` = 145 files, `monk_spectral_bells/`, `FrostWall/OrbOfIce`) | Prefix files exist (`metadata/effects/spells/monk_bell/mtx_*.ot` are cosmetic skins; `miscellaneousobjects/frostwall/orbofice.ot`). Related data: `ActiveSkills` row 724 `tempest_bell`, `GrantedEffects` row 327 `TempestBellPlayer`; `.ot` components `LimitedLifespan` (40), `DiesAfterTime` (54), `DeployedObjectCounter` | **No table lists "deployed objects"**. The link skill -> spawned object is in the skill's `.ao`/`.ot` preload; not extracted today. STALE-RISK, only 3 entries |
| `Services/Scanner/MinimapMarks.cs` | icon names `StoryGlyph`, `RitualRune`, `RitualRuneFinished`, `QuestObject`, `QuestItem`, `MissionTarget`; `*Inactive/InActive/Grey` suffix rule; `/BossRoomMinimapIcon`; tile/label word lists (`boss`, `arena`, `transition`, `stairs`, `door`, `entrance`, `_side`, `medallion`) | `MinimapIcons.datc64` +0x00 Id, **1,050 rows** (mod's `GameLayout.MinimapIconRow` says 1049 rows of 0x2A in 0.5.5e: one icon added). All six names exist (e.g. row 8 `StoryGlyph`, 5 `QuestObject`, 6 `QuestItem`, 15 `MissionTarget`). 214 ids contain Inactive/Grey (many are loot-filter icons, `LootFilter*Grey*`, not "spent" markers). `.ot` files name the icon per object: 654 `MinimapIcon` components, **273 distinct icon names** (`FILES/ot_icons.tsv`, path -> icon) | STALE-RISK; the table gives the full vocabulary, `.ot` gives object path -> icon (e.g. `abyssstrongbox.ot` -> `AbyssChestActive`/`AbyssChestInactive`) |
| `Services/PlayerCharges`, `catalog/hazards.txt` | hazard buffs | already generated (`HazardExtractor`) | OK-live |
| `catalog/map-hints.json` | **hand-written**: 6 areas, 9 rows (`match` object path fragment, `tile`, `sign`, `hint`, `goal`, `reveals`) | areas: `WorldAreas` Id (`G1_2` ...); object paths: `.ot` paths / `Chests`; tiles: `.tdt` names (23,270) | hand-curated by design (route hints); keys can be checked against paths every patch (see section 4) |
| `catalog/landmarks.json` | **hand-written** corrections: 6 areas, 7 entries (tile key "...tdtx:2-y:4" -> label) | tile names in `.tdt`/`.arm` (23,270 / 15,582) | same |
| `catalog/dodge-timings.txt` | measured in play (3 lines) | superseded by `hit-times.txt` (7,117 lines, extracted) | OK |
| Others already extracted (OK-live): `hit-times`, `hazards`, `cull-skills`, `places`, `area-names`, `monster-names`, `item-names`, `item-sounds`, `experience`, `ui-catalog`, `dat-columns` | | | regenerated today by the existing tool: **hit-times, hazards, cull-skills, dat-columns are byte-identical to the committed Oct 24-29 copies**; area-names, places differ (section 4) |
| Hard-coded dat offsets: `HitTimeExtractor` (MonsterVarieties +0x28/+0x38/+0xD0, GrantedEffects +0x71), `AnimationActions` (Animation +0x0A, +0x2B, +0x34, +0x3C, row 0x6A), `ItemNameExtractor` (BaseItemTypes +0x0/+0x20), `GameLayout.MinimapIconRow` (row 0x2A) | | Row sizes now: MonsterVarieties 0x406, Animation 0x6A, MinimapIcons 0x2A | Guarded by `Sized()` row-size check only; a column swap that keeps the row size is not caught |

Notes on evidence:
* `StatusAilments` replay: `FILES/buff_ailment_classification.txt` (ailment, id). Flag columns: `FILES/buff_flag_0x50.tsv`, `buff_flag_0x15D.tsv`.
* `InventoryName` comparison (`FILES/inv_cmp.py`): enum value = `Inventories` row+1 for 93 of 120 names; 26 mismatches all at values >= 120; enum has `Ring3`=128, `RecombinatorInput`=129... that the table (121 rows) does not have, and the table has `TrialOfChaosBench`, `UltimatumEarnedRewards1` that the enum lacks.
* I found **no source** for: the set of ground "harmful" buffs beyond what `hazards.txt` extracts; what makes a Chest "breakable" (no named flag, see above); a "deployed object" list; the map-hint rows (hand knowledge about where bosses are).

---------------------------------------------------------------------------------------------------------------------------

## 3. The Animation question

Compared `host/GameReader/RemoteEnums/Animation.cs` (then in the GameHelper2 clone) (1,084 named values 0x0-0x42B) with `Animation.datc64`
(1,088 rows, row size 0x6A, name = string at +0x00). Script `FILES/cmp_anim.py`, rows `FILES/ids/animation.tsv`, diff `FILES/anim_diff.txt`.

* **Row index = enum value** for the first 584 rows (0-583): all 584 agree, name for name.
* **Four rows were inserted since the enum was written**, so everything after the first insert is shifted:
  * row 584 `AbyssalLivingBomb` (rows 584-597 are shifted by 1: 14 names),
  * row 599 `AbyssalPact` (rows 598-901 shifted by 2: 304 names),
  * row 904 `RemidusDive` (rows 902-1083 shifted by 3: 182 names),
  * row 1087 `FloatingFocusPoseAdjustment` (appended; 1088 - 1084).
* **500 of the 1,084 enum names (46 %) are wrong at their value** (the table has no name the enum lacks except those 4; no enum name is missing from the table).
  Examples: value 600 enum `MoltenBlast`, table `SpinningInferno`; 626 enum `FreezingMarkFlipped`, table `MoltenSlam`; 647 enum `ScatteringCalamity`, table `DoryaniHumanBossBeamBlast`;
  883 enum `CommandSkeletalBrute`, table `HeavyStunForesight`; 938 enum `WildBeastWolf`, table `MeleeDashAttack`.
* The names the mod depends on by exact name: `Idle` 0, `Run` 4, `TakeHit` 5, `Death` 6, `HeavyStun` 231, `HeavyStunBack` 232, `HeavyStunForward` 233, `ElectrocuteForward/Backward/Left/Right` 262-265,
  `ActiveBlockChannel` 479, `ActiveBlockEnd` 480, `...Moving` 481-482: **all under 584, all correct**. (`Freeze` and a bare `Electrocute` do not exist as animations: frozen is a buff, `frozen`, row 2.)
  The mod does not know `HeavyStunForesight` (883).
* **Where it hurts: `DodgeThreats.Of` classifies by NAME**, and the names after row 584 are wrong. Replaying the mod's three word lists over all 500 wrong rows: the threat class (None/Melee/Ranged)
  changes for **127 ids** (`FILES/anim_threat_changes.tsv`: 41 None->Ranged, 36 Ranged->None, 22 Melee->None, 16 None->Melee, 9 Ranged->Melee, 3 Melee->Ranged).
  Against the monsters the dodge warning actually serves (`hit-times.txt`: 7,117 monster|animation entries, 2,462 monsters, 148 distinct animation ids of which 22 are >= 584) the damage is smaller but real:
  **13 entries / 4 animation ids / 12 monsters** (`FILES/anim_hits_out.txt`): Faridun Olroth (all 5 variants) id 626 = `MoltenSlam` (true Melee) is read as `FreezingMarkFlipped` (calm); Doryani (6 variants) id 647 =
  `DoryaniHumanBossBeamBlast` (true Ranged) read as `ScatteringCalamity` (calm); `PackleaderWolfMinion` ids 938/939 (`MeleeDashAttack`/`BiteLungeMelee`) read as `WildBeastWolf/Wyvern` (calm). The timing catalog is NOT affected: it is keyed by animation id.
* **How the plugin turns an animation into a name**: `Actor.Animation` is `(Animation)data.AnimationId` (GameHelper `Actor.cs:256`), then `Enum.GetName(...)` at 6 call sites:
  `ActorTrace.cs:113`, `BossWatch.cs:267` (both via `DodgeThreats.Of(animation, v => Enum.GetName(typeof(Animation), v))`), `EnemyCues.cs:253`, `EnemyStateWatch.cs:95`
  (`EnemyStates.FromAnimation`), `ShieldWatch.cs:58` (`ShieldStance`), `StunTrace.cs:100`. Everything else keys on the raw id (`DodgeTimings.Key(path, id)`).
  The extractor side is already correct: `AnimationActions.cs` and `HitTimeExtractor.cs` read the name from the table (`this.table.String(action, 0)`) and write ids; the Actor's animation id is the table row
  (their own doc comment says so, and rows 479-482 are quoted as 0x1DF-0x1E2 in `ShieldStance.cs`, consistent with this study).
* **Fix that follows from the data**: emit `catalog/animation-names.txt` (1,088 names, ~15 KB) from the same `batch` run and make the 6 call sites use it (fallback to `Enum.GetName` only if the file is missing).
  A name beyond the table is then "new since the extraction", which `DodgeThreats` already treats as Ranged. Same idea for the 12 stat keys of Rage (resolve by Id at load).

GameStats (same class of problem, `FILES/stats_cmp.py`): 26,943 enum names vs 27,299 `Stats` rows; the first divergence is at key 3123 (`power_charge_art_variation` renamed `..._aylardex`), the first
insert at key 4404 (`skeletal_chains_aoe_percentage_health_dealt_as_physical_damage`), 19 diff hunks in all, **22,541 of 26,943 names are off**, 356 net rows added. STATUS.md:220 already knows ("about 3 rows behind").

---------------------------------------------------------------------------------------------------------------------------

## 4. "What is new in this patch" report

### 4.1 Key tables and key columns (all opened; key = string at +0x00 unless noted; `FILES/keylists/<table>.tsv` has row, key, label guess)

| To detect new... | Table (rows today) | Key | Notes |
|---|---|---|---|
| buffs, ailments, hazard buffs, charges | `buffdefinitions` (3,218) | +0x00 Id; +0x08 English description | Report added ids with description; auto-run `StatusAilments.FromBuffId` on each new id and print its class and "unclassified but description mentions Freeze/Shock/Ignite/Stun/Slow/Curse" |
| animations | `animation` (1,088) | +0x00 Id | row index shift = the enum problem; any insert below the end is a re-numbering warning |
| stats (rage, poise...) | `stats` (27,299) | +0x00 Id, key = row+1 | rows moved => every stat key the mod uses must be re-resolved by Id |
| monsters, bosses | `monstervarieties` (2,794), `monstertypes` (1,571), `monsterbonuses` (104) | MonsterVarieties +0x00 path; name +0xF0 (English "Alpha Primate") | Boss: 406 ids contain "Boss"; byte flag +0x303 (409 rows) looks like the boss flag by content; `.ot` side: 202 `extends "Metadata/Monsters/BossBase"` and 110 `BossBaseStandAlone`, 3 with `set_is_act_boss = true` |
| areas | `worldareas` (448) | +0x00 Id (`G1_2`, `Abyss_Boss1`), +0x08 name | Already in `catalog/area-names.json` (10 languages) |
| skills (and "skill granted by gem") | `activeskills` (1,148), `grantedeffects` (8,350), `skillgems` (1,191) | +0x00 Id | ActiveSkills +0x08 DisplayedName |
| NPC dialogue / window types | `npctalkcategory` (38: DISABLED, AtlasMission, Gossip, Quests, QuestReward, BuyItems, SellItems, Crafting, TradeDivinationCards, Music, SelectHideout, RespecPassivePoints, CurrencyExchange, ChooseOneUnique, Recombinate, BuyAsyncTrade, ChangeAscendancy, ...), `npctype` (34 NPCs), `npcs` (981), `npctalk` (2,227: Id at +0 only for 236 rows), `npctalkdialogue` (954), `uitalktext` (98), `npcshop` (54) | +0x00 Id | Category list is the closed set of dialogue option kinds a blind player must be able to hear; a new row = a new kind of dialogue window |
| UI screens | files `metadata/ui/**/*.ui` (127 `.ui`, 144 files incl. xml) | path | already extracted: `catalog/ui-catalog.json` (126 sources) |
| ground effects | `groundeffecttypes` (53: IgnitedGround, ChilledGround, CausticCloud, Tar, Smoke, Desecration, Consecration, ShockedGround, Vortex, Haste, Sandstorm, Blizzard...), `groundeffects` (377; Id only in 71 rows) | +0x00 Id | GroundEffect component exists in 9 `.ot` |
| minimap icons | `minimapicons` (1,050) | +0x00 Id | |
| chests/breakables/shrines/doors/traps | `chests` (1,033), `shrines` (72: Acceleration, Lightning, Cold, Fire, Arc, Criticals, Brutal, Thorns...), `doors` (9), `traptools` (13, no Id) | +0x00 | |
| quest rewards | `questrewardtype` (38) | +0x00 Id, +0x08 art path | |
| tiles / rooms (place narrator) | files `.tdt` 23,270 / `.arm` 15,582 | path | `catalog/places.json` |
| mechanics text | `keywordpopups` (1,030), `clientstrings` (9,661), `.csd` (589 files, 10 languages incl. Simplified Chinese) | +0x00 Id | |
| items | `baseitemtypes` (5,496) | +0x00 Id, +0x20 name | `catalog/item-names.json`, `item-sounds.json` |
| ANY table changed | all 1,025 neutral tables | SHA-1 of the file | `_table-hashes.tsv`: 7 KB gzipped for the whole game |

### 4.2 What the mod silently misses today (no catalog holds the key space)

New buffs/ailments/charges (only the hazard subset is catalogued), new animations, new stats, new boss and monster ids with no attack data, new minimap icons, new chest/object names, new ground-effect types,
new dialogue categories and NPC types, new quest reward types, new keyword text, new `.ot` components. Each ends in the mod's fallback (ignored, raw id, "Ranged" for an unknown animation).

### 4.3 What can be diffed with NO new extraction (committed catalogs vs a fresh run)

| Catalog | Key space | Detects |
|---|---|---|
| `area-names.json` (10 languages) | WorldAreas ids | new / removed / renamed areas |
| `monster-names.json` | only the named subset (6 KB) | partial |
| `item-names.json`, `item-sounds.json` | BaseItemTypes ids (575 / 3,583) | new items |
| `hit-times.txt` | 2,462 monster paths, 148 animation ids | new monsters that attack, changed timings |
| `places.json` | tiles / tileProps / rooms | new tiles and rooms |
| `ui-catalog.json` | controls per `.ui` screen (126 sources) | new screens / controls |
| `hazards.txt`, `cull-skills.txt`, `dat-columns.json`, `experience.json` | small | row/column moves |

**Real run, today vs the committed Oct 24-29 (0.5.5e) copies** (`FILES/regen/`, produced by the existing `batch`, ~30 s):
`hit-times.txt`, `hazards.txt`, `cull-skills.txt`, `dat-columns.json`, `item-names.json`, `item-sounds.json`, `experience.json` identical. `ui-catalog.json` differs only in `generatedAt`.
`area-names.json`: **6 new areas** in every language (`HideoutDivine`, `HideoutExilecon2026`, `HideoutExilecon23`, `HideoutMaze`, `PlayerAtlasHideout`, `PlayerAtlasHideoutInert`) and 7 renames in German
(e.g. `MapCanyon` Schlucht -> Wüstenstadt). `places.json`: 1 new tile (`metadata/terrain/hideouts/maze/tiles/maze_entrance_45.tdt`) and 1 new room. So the 2026-11-05 patch added a hideout
(Labyrinth hideout) and nothing else the committed catalogs can see; that is also why the diff output is short.

### 4.4 Prototype (`FILES/`)

* `tool/` (`Study keylists <game> <outDir> <tables>`): writes `<table>.tsv` (row, key, label) for 40 tables plus `_table-hashes.tsv` for all 1,025 neutral tables in ~15 s.
  Output of `FILES/keylists/`: 40 key-list files 5.3 MB raw (the stats/mods/skill-level tables dominate); **the 14 tables that matter for a blind player** (buffdefinitions, animation, worldareas, monstervarieties,
  minimapicons, chests, shrines, npcs, npctalkdialogue, questrewardtype, groundeffecttypes, activeskills, grantedeffects, keywordpopups) are **22,323 keys = 697 KB raw = 154 KB gzipped**;
  `_table-hashes.tsv` 47 KB raw = **7 KB gzipped**. One file per patch (or the catalog folder itself, committed) is enough.
* `patch_diff.py OLD NEW [out.md]`: per table `+added / -removed / relabelled`, ordered by relevance, plus changed/new/dropped tables from the hashes.
* No earlier full extraction exists, so the demo uses stale sources as the "old" side (`make_old.py`): Animation enum, GameStats enum, area-names.json. Output `FILES/patch_report_demo.md` (2.6 KB):
  `animation +4` (AbyssalLivingBomb, AbyssalPact, RemidusDive, FloatingFocusPoseAdjustment), `worldareas +6` (the hideouts), `stats +367 -11` (incl. `skeletal_chains_aoe_...`, `brequel_reward_no_minion_catalysts`,
  `map_breach_city_biome`, `reveal_weakness...`). It is exactly the list that would have told us the Animation and GameStats enums were stale.
* The `label` column is a content heuristic (first filled non-path string column); it is wrong for `animation` and `monsterbonuses`. Use `DatContentColumns`-style detection (as `dat-columns.json` does for 12 columns)
  for any label you want to trust, and compare ids only otherwise.
* Gaps: tables with no string Id at +0 (`traptools`, `monstermortar`, `monsterprojectileattack/spell`, most of `groundeffects`, `skillgems` (59 of 1,191), `questitems` (6 of 221)) must be diffed by whole-row hash or
  by their foreign key to a table that has an Id.

---------------------------------------------------------------------------------------------------------------------------

## 5. Other sources, and what each could give that the mod does not use yet

| Source | What it contains (opened) | Not used yet / opportunity |
|---|---|---|
| `.ot` (8,324; `extends` chain, UTF-16 with BOM) | Component list with defaults: Positioned 3,745, BaseEvents, Animated, StateMachine 2,738, Actor 1,504, Stats 1,325, MinimapIcon 654, Transitionable 639, InteractionAction 572, TriggerableBlockage 259, Pathfinding 225, Chest 39, AreaTransition 35, GroundEffect 9, Shrine 2, NPC 16, Monster 12, DiesAfterTime 54, LimitedLifespan 40. `Render { display_text = "ObjectDisplayTextChest" }` -> `ClientStrings` (296 `ObjectDisplayText*` rows; 249 distinct display_text values in `.ot`). `MinimapIcon { icon = "..." }` -> 273 icons. Monster `.ot`: `extends BossBase` (202) / `BossBaseStandAlone` (110), `set_is_act_boss`, `set_base_heavy_stun_duration_ms` (677 monsters). 3,250 files `extends "nothing"`, 1,252 `extends Metadata/Monsters/Monster`, 272 `NPC/NPC`, 118 `Chests/Chest`. | Entity path -> kind (chest/switch/exit/shrine/boss/NPC) by component and base class instead of path words; object path -> minimap icon and label; heavy-stun duration per monster; "boss" by inheritance. |
| `.act` (1,875) | per monster map `Animation Id -> clip names` (`Melee = "attack_01, attack_02, combo_01"`, `HeavyStun = "stun_01"`, `ChargeStart/Charge/ChargeEnd`); the Ids are the `Animation` table names | already used by `HitTimeExtractor`; also gives the exact set of animations a monster can play (a new id in game that is not in its `.act` = unexpected state) |
| `.ao` (74,241) | animated objects with clips, events (contact/hit frames, sounds); already read by `ao-sounds`, hit times | per-clip sound events for enemy cues; projectile and effect objects |
| `.ui` (127) + `uisettings*.xml` | screens and controls (already `ui-catalog`) | diff per patch: new screens/controls; `uisettings.<language>.xml` per-language UI strings (not looked at) |
| `Metadata/` config | `itemfilters/default.filter`, `PassiveSkillGraph.psg`, `atlasskillgraphs/*.psg`, `alternateskillgraphs/*.json`, `character_name_profanity*.txt` (not needed), `mapdevices`, `shrines`, `environmentsettings` (1,042 files), `terrain/**/*.tdt/.arm/.tgt/.ecf/.dgr` | passive tree graph is already read (`GameLayout.PassiveTree...GraphFileName`); `default.filter` is the game's own loot-filter text (a source for item rarity/class lists); `.ecf` (288) combine tiles |
| `Data/StatDescriptions/*.csd` (589 files) | stat text in 11 languages, with `[Keyword|display]` markup (e.g. monster `immune_to_curses`: `"Inmune a las [Curse|maldiciones]"`) | already partly used (`MonsterMods` parses mod text); the 25 specific files (`monster_`, `utility_flask_buff_`, `skill_`, `passive_skill_`...) give per-language wording for any stat or buff the player meets |
| Text tables (9 language folders x 219) | `NPCTalk`, `NPCTalkDialogue`, `UITalkText` (98), `ClientStrings`, `QuestRewardType`, `BuffDefinitions` descriptions, `KeywordPopups`, `Achievements`, `Quest` | replaces hand-translated EN/ES strings for game terms (quest rewards, ailments, mechanics) with the game's own text in every language the game supports |
| `Art/` | `Art/2DItems/QuestItems/Reward*.dds` (13 files), icons, textures; `Art/2DArt/UIImages/...` | texture keys are what the mod reads from the UI (`QuestRewardArtPrefix`); the set of valid keys per patch = the file list (diffable, 1 MB) |
| `Media/` (install, not bundle) | textures only | nothing |
| `logs/Client.txt` / `LatestClient.txt` (install) | engine and driver versions, area enter/exit lines | no game version; area names the player just entered are logged (not read) |
| `Audio/dialogue/**.ogg` (50,788) | voice lines by character/NPC | NPC voice cue per speaker; sample name -> `NpcTextAudio`/`NPCTalkDialogueTextAudio` rows (25,452 / 12,481) tie a voice file to its subtitle |
| Not found | any version/changelog string; `.otc/.aoc/.uiscript`; column names of `.datc64` | column names must come from content detection or the community dat-schema; keep `dat-columns.json` approach |

---------------------------------------------------------------------------------------------------------------------------

## Ranking of opportunities for keeping the mod working after a patch

1. **Names from the live table, not compiled enums** (Animation names, Stat keys): `animation-names.txt` + a stat-key resolver by Id (12 stats) in the catalog. Fixes 500 wrong animation names (12 boss/monster variants
   misclassified today) and Rage (3 stats mis-keyed now). Cost: two small catalog files in the existing `batch`.
2. **Per-table hash + key lists as a committed `catalog/_manifest`** (7 KB gz hashes, ~150 KB gz keys): a patch report (`patch_diff.py`) at launcher time, written to the log and spoken once ("12 new buffs, 1 new area").
3. **Buff vocabulary check**: run `StatusAilments`/`EnemyStates`/`PlayerCharges` over every `BuffDefinitions` row and list unclassified-but-suspect ids by description; fix the 5 visible false positives.
4. **QuestRewardType-driven reward names in all languages** (replaces 12 hand cases, covers 9 missing kinds).
5. **Self-checks at start**: row size / row count of each table the extractor or the plugin assumes (Animation 0x6A, MinimapIcons 0x2A and 1,050, MonsterVarieties 0x406...) and presence of every hard-coded id (the 22 buff ids, the 6 icon names).
6. **`.ot` structural classification** (components + base class + `MinimapIcon.icon` + `display_text`) to replace Switches/Boss path words; needs a new extractor. Not needed for breakables: `object-sounds` takes their sound from the Chests row's `.ao` files, and of 285 rows a word names only one disagreed with the game (`SnakeLadyPotionChest`).
7. **NPCTalkCategory / NPCType** set as the closed list of dialogue kinds to cover; new rows are new window types.
8. Keep hand lists only where no source exists (map-hints, landmarks, DeployedObjects), but validate their keys against `.tdt`/`.ot` paths and `WorldAreas` ids on each patch.

## What came of it

Done 2026-11-06: 1 (`game-ids`: animation names and Rage's stats from the tables), 2 (`game-keys` and `GameNews`: the report, plus a check that
every kind the mod names by id still exists), the false positives of 3 (`StatusAilments`), and the extractor's start check for
`game-ids` from 5. See [compatibility](../compatibility.md).

Left, by value: 6 (`.ot` classification) would replace the path words in `Breakables` and `Switches` and is the largest. 4 is worth little: only 13 `Reward*.dds`
exist in the bundles and all are mapped, so the kinds the mod lacks have no icon to show. 7 needs a new dialogue kind to appear first; `game-keys` now reports one.
