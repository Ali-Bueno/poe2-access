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

## A step done in another area (2026-11-25)

Away from the step's area the HUD shows a sentence of its own, not the quest's: ClientStrings `QuestTrackingGoTo1..3`,
"Viaja a {0}", "Viaja a {0} o {1}", "Viaja a {0}, {1} o {2}" (German "Reist nach: {0}", Korean "{0}(으)로 이동하기"). In Clearfell,
with the Farmlands' step, the tracker read "Viaja a Tierras de cultivo de Ogham" (the table's line has "las"), no table knew
it and the Quest category had no exit: 100 such lines in one log. `quest-goals.json` now holds `travel` (the sentences per
language) and `areas` (the connected areas by name); `QuestGoalCatalog.TravelGoalsOf` reads the areas out of the sentence and
only an exit straight to one is the quest's (`NextDoor`). At first the exit on the way counted too ("hacia tu misión, El
bosque singular" with the step four areas beyond); the owner: a long walk nobody makes with a waypoint, the quest is to be
told only by the area it happens in. The committed file was of 2026-11-14, without the `quests` part the code reads since 2026-11-19; regenerated (step v3).

## An exit whose path says "Portal" (Mawdun Quarry, 2026-11-16)

The owner, by the way down to the Mawdun Mine: the reader named it "Portal: Mina Mawdun" and the scanner's quest goal led
to a place with nothing to use ("Llegaste a Mina Mawdun", no prompt). The way down is the quarry's crane,
`Metadata/Terrain/Gallows/Act2/2_10_1/Objects/QuarryCrane_ExitPortal` (entity dump of his session): an AreaTransition
component naming G2_10_2 "Mina Mawdun" and no Portal component, in play and meeting the quest (Targetable +0x69 and
+0x6E both 1). Its path holds "Portal" and not "AreaTransition", so the classifier filed it as a portal in Waypoints.
Two things followed: the exit was not in Exits, so the tile exit of the same name (the centre of the crane's tile
pieces, `EntityCatalog.Tiles`) was not dropped as a copy; and the quest's "leads through" pass takes only Exits of kind
exit, so the Quest goal was that tile point, not the crane, and the beacon arrived there (`Beacon.ArrivalWorld`, 150
world units, from the point as it is).

Now an entity is an exit when its path holds "AreaTransition" or it has an AreaTransition component and no Portal
component (`ExitEntities`, used by the scanner and the radar); the players' and the map device's portals
(`MultiplexPortal`) have a Portal component and no AreaTransition (dumps of 2026-11-05 and 11-07). The crane is then
"Nueva salida: Mina Mawdun", the tile copy within 250 cells is dropped, and the goal leads to the crane itself. Built
and unit-tested; not heard live yet.

## An escort held out of play: Asala at Halani's gates (2026-11-16)

The quest "Encuentra una forma de abrir las puertas con Asala" has the player walk with Sekhema Asala
(`Metadata/Monsters/NPC/MarakethMatriarchGate`, a monster with NPC, InteractionAction and MinimapIcon "NPC") from gate to gate.
An entity dump at the boss beside her: friendly, Targetable in-play `+0x69` = 0, attackable 0, quest state 1. The in-play flag
is how the scanner leaves out an NPC's story variants, so she was never listed and the beacon had nothing to lock on.
A story variant set aside never moves; Asala walks. So a character out of play is listed once it has moved more than half a
step from where it was first seen in the area (`WalkingCharacters`, kept until the area changes). Such a character is an
escort to the beacon (`EntityCatalog.IsEscort`): reaching it does not release the lock. Beside it the ping is quiet, with
"Asala, aquí" said once. It pings again when she is three steps ahead. A second press of the chord releases it.

## A step the tracker moved past: Jiquani's Machinarium's stone altars (2026-11-19)

The area holds four `Metadata/Terrain/Gallows/Act3/3_6_1/Objects/KeyPedestal_{Entrance,Landmark,Exit,Boss}`, all "Altar de
piedra" (minimap icon `StoneAltarTall`), each taking a Small Soul Core (a quest item, `…/QuestItems/…`, in the main
inventory). With the first core the tracker said "Coloca el núcleo de alma pequeño en el Altar de piedra"; once it was
placed it said "Adéntrate más en el Taller de máquinas", and a second core picked up did not bring the step back, so the
next altars were no quest object (the owner: "el primero sí lo tuvo"). A used altar turns untargetable and leaves the
listing. `CarriedQuestSteps` keeps the steps an area instance showed that name a quest item carried then, and while such
an item is carried again adds them to the tracker's texts for `QuestMatcher`; they are kept with the area in
`AreaMemory` (a trailing list, so older files still read), so a host restart does not lose them.The step needs no sighting either: `quest-goals.json` now holds `quests`, per language every tracker line of each quest
(`QuestStates` grouped by `Quest`; launcher step "quest goals" v2), and `QuestGoalCatalog.QuestLinesOf` gives all the lines of
the quests the tracker's lines belong to (a line of two quests gives none). Of those, the ones naming a carried quest item
join the texts. In Spanish the Act 3 quest has 60 lines, two naming the small core ("Coloca el núcleo de alma pequeño en el
Altar de piedra", "Registra el Taller de máquinas de Jiquani para encontrar un núcleo de alma pequeño con el que abrir la
puerta"). Unit tests only.

The Molten Vault (2026-11-19) has a stone altar too, `Metadata/Terrain/Gallows/Act3/3_9/Objects/KeyPedestal_02`, for the
Frost Core ("Núcleo helado", a quest item); its quest's lines name neither ("Despeja la Cámara fundida", "Derrota a Mektul…"),
so no step could make it the quest's and it went unheard. The game's own name for such altars is `KeyPedestal` (the
Machinarium's four as well): a targetable one sounds as a quest object while any quest item is carried (`KeyPedestals`); a
used one turns untargetable (`activate=1`) and leaves the list. Unit tests only.

## Aggorat's Sacrificial Heart and Dagger (2026-11-21)

The owner carried the Corazón sacrificial ("Sacrifica este corazón en el lugar de ritual apropiado para recibir dos puntos de
habilidades pasivas de set de armas"); no quest step names the place, so nothing led there. Seen live at the place: the
quest item **Daga sacrificial** (`Metadata/Items/QuestItems/Gallows/Act3/AggoratDagger`, "Necesaria para sacrificar
ritualmente un corazón en el lugar apropiado") on the ground, the object "Colocar corazón sacrificial", then "Apuñalar
corazón sacrificial", then "Has recibido 2 puntos de habilidad pasiva de set de armas". The radar table names the tile
`AggoratOuterArena_02` "Sacrificial Dagger (+2 Passive)"; its file name made it a boss room, said "Sala del jefe:
Sacrificial Dagger" in English.

- `item-names` now extracts quest items too (BaseItemTypes ids under `Metadata/Items/QuestItems/`, keyed `QuestItems/<tail>`),
  so a label naming one reads in the game's language ("Daga sacrificial (+2 puntos de pasiva)") and is no boss's arena
  (`LandmarkTable.IsQuestItemName`).
- An object the game names after a quest item the player carries ("Colocar corazón sacrificial") sounds as the quest's
  (`KeyPedestals.TakesCarried`). Unit-tested; not heard live yet.

