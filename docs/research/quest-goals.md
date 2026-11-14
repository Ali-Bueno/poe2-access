# Where a quest step leads (game tables, 0.5.5f, 2026-11-14)

The scanner's Quest category took its targets from the tracker's words (`QuestMatcher`). In the Lost City the tracker said
"Encuentra el Santuario en el corazón de Keth" and the exit back to Keth became the target: "Misión. Keth, 245 pasos", and the
guide walked the user back to the stairs he had come down (play log of 2026-11-14, 11:04). The lost hint was right all the
while (the user: "el sonidito sí sonó en el sitio correcto"); only the Quest category was wrong.

## What the tables say (MEASURED with `tools/research/gamedata/dat.py`)

- `QuestStates` (1491 rows, 208 bytes): `Order` +0x10, `FlagsPresent` +0x14 (keys into `QuestFlags`), the tracker's text
  +0x59 (lines apart by "\n"). A quest's active state is the one of lowest `Order` whose flags are set, so a step ends when
  the state of the next lower `Order` becomes true.
- `WorldAreas.FlagOnEntered` +0x3C: the flags entering an area sets (`Visited-G2_4_3` on `G2_4_3`).
- So **a step whose next state asks for an area's entered flag ends on entering that area.** BurnTheWaterGoddess: order 10
  "Find the entrance to Keth buried somewhere in the sands" → `G2_4_2`; order 9 "Find the Sanctum at the heart of Keth" →
  `G2_4_3`; MudBurrower order 4 → `G1_3`; TreeOfSouls order 31 "Travel through Clearfell to the Grelwood" → `G1_4`. 353 of
  the 1408 states with tracker text have such a goal.
- `WorldAreas.Connections` +0x16 (row indexes into the same table): `G2_4_2` → `G2_4_1`, `G2_4_3`. 112 areas have any.
- `QuestStates.MapPinsKeys` → `MapPins.WorldArea` is where the world map draws the quest's pin: the area the step is done
  in (`G2_4_2` for the step above), not where it leads. Not used.

## What the mod does

- `tools/Poe2UiExtract quest-goals` → `catalog/quest-goals.json`: per language, tracker line → goal areas (a line whose
  states disagree is left out: 6 to 8 a language), and every area's connections. Columns found by content and checked
  against the two Keth steps before anything is written; launcher step "quest goals" (optional). No table in zh-CN.
- `QuestGoalCatalog.GoalsOf` (the tracker's lines, the client's language, English behind it) and `StepsToward` (the areas
  next door on a shortest way to a goal). `EntityCatalog.QuestLeadsTo` names them as their exits are named
  (`LandmarkTable.AreaNameOf`) and logs "Quest step leads through: …" once per objective and area.
- `QuestMatcher.Targets(leadsTo)`: when the tables name the step's exits, those exits are the targets (the game's own or
  the radar table's tile, seen or not) and no other exit is, whatever the text mentions; characters, bosses and objects the
  text names stay targets. With nothing from the tables, the words decide as before. An exit not seen yet is not listed, as
  before: it is what the lost hint leads to.

Unit tests (`QuestGoalTests`, `ExplorationHintTests`); not heard in a game. UNSURE: a quest whose states branch may have a
next state that is not the one the player reaches; the log line above tells which exits were taken.
