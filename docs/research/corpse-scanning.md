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
the two differ (multipliers 5 mod 20), Game8's spectre list (fetched 2026-11-17, itself partly stale: 43 of 294
checked rows match neither, against multipliers that may have been retuned since) sides with nearest 30 to 5, and the
251 rows where the two agree all hold. The cost is of the corpse's own file, not of its `SpectreOverrides` form:
Faridun Impaler's summoned form (`...EliteSpectre_`) reads 150, which would give 80, not the published 120.
`SpectreCatalog.SpiritFor` is the one place the rule lives; a live tooltip that disagrees fixes it there.

## 4. What was built

- `tools/Poe2UiExtract/SpectreExtractor.cs`, batch command `spectres`, launcher step "spectres" (`spectres.json`,
  optional): the multiplier of every `MonsterVarieties` row by monster file, the column found by the five known rows
  above and the step failing without writing when they do not hold. `Poe2Accessibility/catalog/spectres.json` as
  shipped was made by that step from the installed game on 2026-11-17 (2,794 rows).
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
