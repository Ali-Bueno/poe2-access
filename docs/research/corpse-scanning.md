# Corpse scanning for Bind Spectre (2026-11-17)

Scope: ND7 of [d2r-gap-analysis.md](../d2r-gap-analysis.md) ("usable-corpse cue … needs RE of corpse entities"), made
concrete by one skill: a blind player with Bind Spectre must walk to the corpse of a named monster and bind *that* one.
A sighted player presses the skill near corpses and reads a tooltip over each: the monster's name and the Spirit a
spectre of it reserves (poe2wiki, "Bind Spectre", read 2026-11-17). The scanner now lists those corpses in their own
category, named with that cost, and the guide locks on one like on anything else. Confidence: **MEASURED** (read in a
file or in the host's code), **LIKELY** (consistent, not seen running), **UNSURE**.

Repo state: local copy at `v2026.11.12-4-242-g094a1f39`; upstream release `v2026.11.16-5` is newer and was not merged
for this work. Nothing here touches code the release notes are known to have changed, but rebase before shipping.

## 1. What a corpse is in memory (MEASURED, host code)

There is no corpse entity and no corpse component. A dead monster is the same `Entity`, still in
`AreaInstance.AwakeEntities`, still `IsValid`, typed `EntityTypes.Monster`, under `Metadata/Monsters/`:

- Dead by the game's own word: `CalculateEntityState` (host `Entity.cs:732-746`) sets `EntityStates.Useless` when the
  `is_dead` stat (`Entity.DeadStat`, set by the plugin from the game's table) reads over 0, or, without that stat, when
  `Life.IsAlive` (host `Life.cs:51`, health current over 0) is false.
- The host then stops refreshing it (`Entity.cs:397-417`, a wake-up check every 30 frames), so its components keep the
  values they died with: `Render` name (read at `GameLayout.RenderComponent.NameOffset`, the same read the scanner uses
  for the living) and world position are those of the corpse where it fell, and its life can read frozen over 0 when
  the stat killed it — which is why death must be asked as "`Useless`, or life at nothing", never as life alone
  (`EntityCatalog.IsFrozenDead`, and its note of 2026-10-26).
- The mod already met corpses while excluding them: `TargetNarrator.HudTarget` reads every awake entity's name and
  drops the dead ones ("A corpse or a monster set aside is not what the game targets", `TargetNarrator.cs:158`).
- A monster that rises again is refreshed and unmarked by the host (`ShouldForceMonsterRefresh`), so it leaves the
  corpse category and is an enemy again by itself.

Not the same thing: the scanner's kind `"corpse"` under **Chests** is a body searched for loot (a `Chest` entity,
`Breakables.IsCorpse` on its file, said as "Body to search"). Unchanged; a dead monster's kind is `"monster_corpse"`.

## 2. What may be bound (poe2wiki, read 2026-11-17)

"the corpse of almost any non-unique monster", killed without its corpse being destroyed. Known exclusions the mod
cannot read off the entity: monsters spawned from strongboxes (0.2.0 hotfix), and single monsters GGG turned off by
patch note (Frozen Mandibles and others, off and on again between 0.2.0 and 0.4.0). The scanner therefore lists every
dead non-unique, non-friendly creature that is not a character (`EntityCatalog.IsCorpse`): a corpse the game refuses
can still be listed — the skill's own tooltip is the last word — but a unique, a character or the player's own minion
never is. No `corpse cannot be used` flag was found: the stats table holds `corpse_cannot_be_destroyed` and
`necromancer_revivable_even_if_corpse_unusable` (host `GameStats.cs`), neither of which says a corpse is unusable now,
and no component of the reader's carries such a state. Whether the game marks a consumed or shattered corpse before
deleting it is **UNSURE**: nothing in the repo reads it, and a deleted corpse simply leaves the list.

## 3. The Spirit a spectre reserves (MEASURED inputs, formula LIKELY)

The dat files hold no spirit column. The community dat schema (poe-tool-dev/dat-schema, release `latest`, fetched
2026-11-17) names no such column in `MonsterVarieties` (127 columns), `MonsterTypes`, `ActiveSkills` or
`GrantedEffects`; `SpectreOverrides` only maps a monster to the variety summoned in its place. Path of Building
(PoE2)'s export derives its `spectreReservation` from the variety's **experience multiplier**
(`src/Export/Scripts/minions.lua`: `floor(((ExperienceMultiplier/100) ^ 0.75) * 50)`) — but those numbers are not the
game's: against the costs poe2wiki lists from play they read 51 where the game asks 50, 84 where it asks 100.

Measured on the installed game (2026-11-17, `Poe2UiExtract dat-kinds`/`dat-rows` on `data/balance/monstervarieties.datc64`,
2,794 rows of 0x406 bytes): the experience multiplier is the i32 at **+0xA8** (the schema's column order gives the same
offset; the life multiplier at +0x11C matches it on most rows but not all — `SerpentClanCasterBoss` reads 200 against
266, which tells the two apart). No column of the row holds the published costs themselves (checked every even offset
of seven known rows). What holds on every published cost is **half the multiplier, to the nearest ten**:

| Monster (file) | Multiplier | Half, nearest ten | Published cost |
|---|---|---|---|
| Vaal Guard, grenade (`VaalGuardMortarLiving`) | 100 | 50 | 50 (poe2wiki) |
| Vaal Guard, spear (`VaalGuardSpearLiving`) | 110 | 60 | 60 (poe2wiki, bow/spear) |
| Priest of the Sun (`SunVaalApparition`) | 200 | 100 | 100 |
| Venomous Crab (`QuillCrabPoison`) | 70 | 40 | 40 |
| Quill Crab (`QuillCrabTropical`) | 70 | 40 | 40 |
| Serpent Shaman (`SerpentClanCaster`) | 200 | 100 | 100 |
| Gargoyle Demon (`GargoyleGolemRed`) | 200 | 100 | 100 |
| Azak Fledgling (`VaalSavageDelinquent`) | 100 | 50 | 50 |
| Doryani's Elite (`VaalHumanoidCannonLightning`) | 140 | 70 | 70 |
| Vaal Embalmed Bearer (`ProcessionBanner`) | 120 | 60 | 60 |
| Ribrattle (`PitifulFabrication02`) | 80 | 40 | 40 |
| Faridun Impaler (`MutewindWomanSpearCorrodedElite`) | 230 | 120 | 120 (IGN) |

"Nearest ten" against "rounded up to ten" cannot be told apart on those twelve (their halves end in 0 or 5). Where
the two differ (multipliers 5 mod 20), Game8's spectre list (fetched 2026-11-17, itself partly stale) sides with
nearest 30 to 5; of its 294 checked rows, 216 are where the two rules agree and hold, 30 hold on nearest alone, 5
on rounding up alone, and 43 match neither (against multipliers that may have been retuned since) — 251 rows
matching at least one rule. The cost is of the corpse's own file, not of its `SpectreOverrides` form:
Faridun Impaler's summoned form (`...EliteSpectre_`) reads 150, which would give 80, not the published 120.
`SpectreCatalog.SpiritFor` is the one place the rule lives; a live tooltip that disagrees fixes it there.

## 4. What was built

- `tools/Poe2UiExtract/SpectreExtractor.cs`, batch command `spectres`, launcher step "spectres" (`spectres.json`,
  optional): every monster row of `MonsterVarieties` by monster file — its experience multiplier, and (§7) its
  main hand's item class and melee/ranged tag on the flagged rows. The four columns (§7's table) are each found
  again by what they hold, judged on the five known rows above and the three Vaal Guards, and the step fails
  without writing when they do not hold. `Poe2Accessibility/catalog/spectres.json` as shipped was made by that
  step from the installed game on 2026-11-17/18 (2,767 monster rows; the table's escort summons and its `Any`
  row, no corpses to bind, were dropped from it and from the step at the 2026-11-18 review).
- `SpectreCatalog` (plugin): the file read at start, `SpiritOf(path)` the cost or nothing.
- `ScanCategory.Corpses`, after `Players` so the values the area memory writes to disk keep their numbers; a corpse
  `CanVanish`es, so a guide locked on one that is used up under the player reads as reached. `EntityCatalog.Classify`
  lists a corpse past the in-play gate (nothing targets one) with kind `monster_corpse` and the name said as
  "Vaal Guard corpse, 50 Spirit" (`Announcements.CorpseOf`), without a cost when the catalog lacks the monster.
  Sighted as anything else is: on screen, or on ground already explored — where the fight was. Not remembered when
  the player walks out of the game's awake range, and silent (no object sound, no proximity speech): the scanner and
  the guide are the way to it, on ask.
- Tests: `SpectreCatalogTests` (the rule on the published costs, the file read, the shipped catalog's known rows).

## 5. Still to learn live

- Whether a corpse reached and bound leaves the list at once, and whether the game's Bind Spectre targeting (its
  tooltip up) makes `InteractTarget` name the corpse under the player — it would, unchanged, if the game flags the
  corpse as the target.
- Whether a raised-again monster (one that was `Useless` and wakes) is ever bindable while it lies there; listed
  until it rises, either way.
- One tooltip against the rule on a multiplier the twelve do not cover (any 5-mod-20 corpse: say what the scanner
  said, then what the gem reserves once bound).
- ND7's other half (a cue when usable corpses come near, for Offering and kin) is not built: it wants a new sound,
  which wants the user's ear first (repo rule 23), and a corpse count the category now makes possible.
## 6. First live session (2026-11-18, the owner in Utzaal on the dev build)

The category, the guide's lock and its arrival all worked first try ("Corpses. Chaotic Zealot corpse, 4 steps…",
"Beacon locked on Vaal Guard corpse (Corpses)", "Arrived at Vaal Guard corpse"), and a spectre was bound from a corpse
the guide led to. Two findings:

- **No cost was said**: an entity's path carries its level after an '@' ("…/VaalGuardBowLiving@40"), which the
  table's Id has not, so every `SpiritOf` missed while the catalog held 2,794 rows. Stripped in `SpiritOf` now (the
  same cut `BossMechanicsCatalog.MonsterId` makes), and a corpse still without a cost is logged once a file
  ("Corpse without a spirit cost: …"). Not yet heard with the fix: needs a rebuild and restart.
- The bound gem reads **Spirit Reservation 47** — neither 50 (grenade, multiplier 100) nor 60 (bow/spear, 110), so
  the owner's reservation efficiency stands between the base cost and what the gem shows ([INFERENCE]: 50 at ~6%
  efficiency or 60 at ~28%). Which of the three Vaal Guards was bound the log cannot say: all read "Vaal Guard", and
  bow and spear share name *and* cost — the game's own tooltip tells those two no further apart either; only the
  corpse's look does, which speech has not got. Grenade (50) against bow/spear (60) the cost does tell apart, once said.

## 7. Which variety it is (2026-11-18, at the owner's word: say it, do not make him earhole it)

Monsters of one name differ in what a corpse of them gives, and the game's tooltip tells bow from spear no further
than its name and cost — only the corpse's look does. The data does, though. The four `MonsterVarieties` fields
this work reads, all in the table's 0x406-byte rows:

| Field (dat-schema name) | Offset | Holds | Provenance | Judged on |
|---|---|---|---|---|
| `ExperienceMultiplier` | +0xA8 | i32, the multiplier of §3 | the schema's column order; measured 2026-11-17 (`dat-kinds`/`dat-rows`) | the five known rows of §3; `SerpentClanCasterBoss` (experience 200, life 266 at +0x11C) tells it from the life multiplier |
| `Tags` | +0x98 | key-list into `Tags` (its `ranged`/`melee` rows) | the schema's column order; measured 2026-11-18 | the three Vaal Guards: grenade and bow ranged, spear melee (a leaper); a row carrying both tags is left with its reach unsaid, not guessed — the step counts such rows in its output from the 2026-11-18 review on, so how often that occurs is recorded at the next making |
| `Name` | +0x110 | text, the name monsters are grouped by | the schema's column order; measured 2026-11-18 | the three Vaal Guards all reading "Vaal Guard" |
| `MainHand_ItemClass` | +0x154 | foreign key into `ItemClasses` | the schema's column order; measured 2026-11-18 | the three Vaal Guards: grenade none, bow "Bow", spear "Warstaff" — its spear *is* a warstaff in the data |

No offset is trusted as written: the `spectres` step finds each column again by what it holds, nearest the
measured offset, and fails without writing when a judged row does not hold (§4). The step writes the weapon and
the reach only on flagged rows, beside the multiplier every row carries: the flag is on every row of a name
whose varieties differ in cost, weapon or reach (1,024 of the rows as first shipped, when the step compared the
raw multiplier; since the 2026-11-18 review it compares the derived cost, so varieties within one cost band no
longer flag each other and the count falls at the next making — the English name groups them either way;
another language's names may group a pair otherwise). A flagged corpse is named with both words it has, each
its own: "Vaal Guard corpse, bow, ranged, 60 Spirit", "Vaal Guard corpse, quarterstaff, melee, 60 Spirit",
"Vaal Guard corpse, ranged, 50 Spirit" (the grenade one: no weapon, and the cheaper cost); a flagged corpse
with neither word is told from its namesakes by its cost alone. Weapon words are the mod's for the weapon
classes (the game's class names are plurals: "Quarterstaves", in Spanish "Bastones"); unarmed is said (the
Baron's wolf form, told from its sword form by it), a held class with no word of the mod's says nothing.

Heard live the same night (the owner again in Utzaal): "Vaal Guard corpse, bow, ranged, 60 Spirit",
"Vaal Guard corpse, quarterstaff, melee, 60 Spirit", "Vaal Guard corpse, ranged, 50 Spirit",
"Chaotic Zealot corpse, spear, melee, 60 Spirit" (a real Spear in hand there). The owner disenchanted the §6 spectre,
engraved a fresh Bind Spectre and bound from the field: the gem reads "Reservation: 50 Spirit" and costs him 45 —
the scanner's 50 for the grenade guard, his efficiency on top of it: one live case (100 → 50) matches §3
end to end; it is a case the published twelve already held and where nearest-ten and rounding up agree, so
§3's formula stays LIKELY and §5's 5-mod-20 tooltip test remains open. One
flaw the log showed: a corpse was also copied into the Quest category ("Quest. Chaotic Zealot corpse"), its name
matching the tracker's words; corpses are now left out of quest matching — an objective never asks for one.
Built and committed; deployed when the game next stops.

## 8. Evidence kept, and not kept

Kept: this document's tables and the derived `catalog/spectres.json`; the step revalidates its columns against
the installed game at every making. Not kept (2026-11-18 review): no snapshot of the dat-schema (fetched at its
floating `latest` release), no dump of the `dat-rows`/`dat-kinds` scans behind §3 and §7, no copy of Game8's
list behind §3's counts, and the live sessions' `accessibility.log` stayed on the owner's machine — §§6-7 quote
it, but nothing of it is under `Poe2Accessibility/debug/` or `logs/`. What is re-derivable is re-derived by the
step; what is not (the Game8 counts, the schema as it read that day) rests on the numbers recorded here.
