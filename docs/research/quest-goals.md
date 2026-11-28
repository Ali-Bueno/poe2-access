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

## The world map's quest marker (2026-11-27)

A player on a DualSense, in Clearfell Encampment with "The Mad Wolf of Ogham" ("Infiltrate the Manor beyond the Village") and
"Grave Tidings" open, heard the Quest category lead to the Clearfell exit (log "Quest step leads through: Clearfell (from
G1_town)"), the start of an eight-area walk, and asked whether the waypoint map shows where the quest ends. It does. A
sighted player sees an exclamation mark over Ogham Manor's "?" pin on the Quests map and on the Teleport map, and the
active waypoint of The Manor Ramparts right under it. With the quest list open (Square), the focused quest's mark turns gold
and its pin gets a blue ring. Grave Tidings has no mark. The map legend calls the mark "Quest Location" (ClientStrings
`WorldScreenLegendQuestMarker`, `MapLegendQuestLocation`). The player chose to hear the mark on the place under the
cursor, a look at the marks with their nearest waypoint when the map opens, and the place a quest of the list is marked
on; built 2026-11-28 (`UiSemantics.QuestMarks`, `MapPinNarrator`, [controls](../controls.md)).

MEASURED live, Act 1 Teleport map, with two `dump visible`, `datkeys` and `hex` (list closed, then open on the quest, then
closed again):

- The marked "?" pin is G1_15 (MapPins `G1_15` at the element's +0x2D0, WorldAreas at +0x2E0). The pin right under it is
  G1_14, known, entered and waypoint active; the one to its right G1_13_2, the same.
- No element is drawn where the mark is. The mark belongs to the pin's own picture.
- +0x31A is 1 on the four G1_15 pins (`G1_15`, `_Underground`, `_Candlemass`, `_IronCount`) and 0 on the other 48 pins the
  visible dump lists (the pin layer has 94). G1_15 is also the act's only area never entered, so the byte may be the mark or
  the "?" picture. UNSURE. It is settled by one `hex` of the G1_15 pin once the Manor has been entered: orders 3 to 6 still pin
  G1_15, so the mark stays while the "?" goes. Outside +0x318 to +0x31F no plain byte or field sets G1_15 apart from G1_14 and
  G1_13_2 (the UTF-16 scraps in its first 0x400 bytes are stale memory).
- +0x31C is 1 on `G1_town` alone, where the player stood, so it is the current area.
- Opening the list and focusing the quest changed no byte of G1_15, G1_14, G1_13_2 (0x400 each) or of the act's canvas
  (0x600). The ring and the gold come from the list's side and were not found.
- The crosshair at the top of the list is `gamepad_Toggle_mode_button`. Its popup's header is "Active" (ClientStrings2
  `Active`). What it toggles is UNSURE; ClientStrings has `ConsoleActionRecenter` "Recenter" and no string to track, pin or
  locate a quest.
- The mod reads that header after the focused quest, "The Mad Wolf of Ogham, button, 1 of 12, … Infiltrate the Manor. Active,
  unavailable", which sounds as if the quest were the active one. It made the player ask how to set an active quest. A
  bug to fix: the header belongs to the toggle, not to the quest.
- The session's D-pad route on the Teleport map reached neither Hunting Grounds, Ogham Village, The Manor Ramparts nor
  Ogham Manor (log, 23:06), so a pin far from the cursor is hard to reach without directions.

MEASURED live, the key chain the mod reads (`hex`, `datkeys`, list closed):

- Each quest button of `quest_selector` (selector > frame > list > button) keeps a dat key to its Quest row at +0x2C8 and
  one to the QuestStates row of the step it is on at +0x2D8. The Mad Wolf's: `TheIronCount`, the order 7 step. The ten
  finished quests' +0x2D8 is empty; the separator between active and finished has no key.
- A loaded QuestStates row keeps the file's layout: +0x45 is (count, pointer) to 16-byte keys. The Mad Wolf's step had
  one, MapPins `G1_15`; Grave Tidings' step count 0.
- A loaded WorldAreas row's +0x16 is (count, pointer) to bare 8-byte pointers at WorldAreas rows, with no file beside them:
  G1_15's one item was G1_14's row (rows 0x2E0 apart in memory, the file's row size). The mod takes a neighbour only when
  it is the area of a pin the shown map draws.
- Still open: QuestStates +0x71, a second list of MapPins keys on 285 rows, whose meaning is unknown.

MEASURED from the tables (a scratch copy of `tools/Poe2UiExtract`, 0.5.x):

- `QuestStates.MapPinsKeys` is at +0x45, a list of MapPins keys. Found by content, since every key lands in MapPins and the
  pins' areas match the steps' places (GraveyardFight gives G1_7, G1_8 and G1_9). TheIronCount order 7, "Infiltrate the
  Manor beyond the Village / Find Count Geonor", gives MapPins row 90, `G1_15`, the pin the live map marks. Orders 3 to 6 give
  G1_15 too.
- Grave Tidings (`RitualGroupLeague1`) has no pin, nor do the other ten `RitualGroupLeague` quests, the endgame Ritual, the
  class quests, Incursion or Act 5. Of the 1408 states with tracker text 645 have a pin, side quests among them
  (GraveyardFight, 23 of 23).
- No main or side flag was found in the Quest table. A quest seems to get a mark when its state has a pin. UNSURE: the
  client's code was not traced.
- The bundles hold no `.ui` file of the world map. Among the textures under
  `art/textures/interface/2d/2dart/uiimages/ingame/worldmap/` are `worldscreenareanodequest.dds` and
  `areanodeunknown.dds`; which one draws the mark is UNSURE.

After a game patch (the player expects v1.0 in about two weeks from 2026-11-27, with quests changed), the launcher runs `quest-goals` again on the new
build (`GameDataStamp`). The extractor finds its columns by content but judges them on two BurnTheWaterGoddess states by
their 0.5.5f English text, Order and goal (`QuestGoalExtractor.KnownStates`). If the patch rewords or reorders those two, the
step fails and writes nothing; it is optional, so the 0.5 catalog stays. Then a v1.0 tracker line that changed matches
nothing and the Quest category falls back to the tracker's words, while a line kept word for word whose quest moved still
leads where 0.5 led, which is worse than silence. The marks avoid that weakness. They are read from the game's loaded rows, the button's
step and that step's pins, so v1.0's quests are read as v1.0 has them, and no catalog or quest wording is involved. What a
patch can move is the layout. The button's keys are a `DatKeySite` found again by the Quest table's name, the step's key
follows the quest key, and each pin key is checked to name MapPins. `QuestStates.MapPinsKeys` and
`WorldAreas.Connections` are followed by `dat-columns` (`DatContentColumns`), found by content with no named row: the
connections as the row list whose links mostly run both ways (247 of 252 on 0.5.5f; the table's other row list, none),
the pins as the MapPins key list filled on the most rows, at least 200 and twice the next (645 against +0x71's 285). Both
are optional, so a patch that blurs them costs the marks only. A read that does not check out says nothing.

### Made safe to merge (the owner's review, 2026-11-28)

A second reader went through the branch before it was merged; what it found is changed on top of it:

- **"No active waypoint near it" could be false on a healthy game.** The walk between areas keeps only the areas whose pin
  the map shows, so an area in between not shown yet, or a list that did not read, ended it short. It is said only of a
  walk that left nothing out (`Neighbours`' `whole`); otherwise nothing is said of waypoints, as on a travel map.
- **The step's key failed with nothing said.** It follows the quest key's proven move, so a patch that parts the two
  made every quest look finished. The list keeps its quests in hand first, then a separator that holds nothing, then the
  finished ones (the Interlude's list on 0.5.5h: four, the separator, five): when a list has quests before its separator
  and none reads a step, on two different lists, it is said ("el paso en que está cada misión de la lista del mapa del
  mundo, así que el mapa no dirá ningún lugar de misión"). Each list logs `Quest marks: 3 quests in hand of a list, 3 on a
  step, 1 marking a place` once, which also tells a mark the game does not draw from one the mod did not read.
- **The pin list could be the wrong one.** `QuestStates` holds a second list of MapPins keys (+0x71) that passes every
  check the plugin makes; it lost to +0x45 by 2.3 to a threshold of 2. The extractor now takes the fullest only when no
  other list of pins lies nearer where the column was measured, else none, and the plugin judges its own offset.
- **`QuestStates.RowSize` was never written** by the extractor (its table has only an optional column, and the row sizes
  were listed from the others): with the column not found, the plugin could not judge its offset and would have said
  "some readings may be wrong" at every start. The sizes are listed from every column's table. Run on the installed
  game (26744644), the branch's extractor wrote 37 entries, without it; the changed one writes the 38 of the shipped file.
- The key lookup that learns runs only on a button where the list keeps them (quest_selector > frame > list > button),
  and one reading of a canvas's marks and shown areas serves a second: the focused quest asked for the whole map every
  150 ms.

Live with these changes, 2026-11-28 (0.5.5h, build 26744644, unattended):

- **hairin (Huntress 4, Clearfell Encampment), Act 1's Teleport map.** On opening: `Quest marks: 3 quests in hand of a
  list, 3 on a step, 1 marking a place`, `Layout: quest marks where measured`, and "Lugar de misión: Secretos en la
  oscuridad, El bosque singular, Sin visitar. Transportador activo más cercano: Sierraclara, a una zona, abajo a su
  izquierda en el mapa". The screenshot agrees: a gold exclamation mark over the "?" pin at the top right, the one blue
  waypoint below it to the left. The list: "Secretos en la oscuridad, marcada en El bosque singular, botón, 1 de 7, …",
  and "Legado de los precursores" and "Presagios sombríos" with no place, with no "Activo, no disponible" after any. The
  cursor on the place: "El bosque singular. Nivel de monstruos: 4. … Lugar de misión. Sin visitar".
- **kehgan (65, the refuge), the Interlude's and Act 4's maps.** `Quest marks: 4 quests in hand of a list, 4 on a step, 0
  marking a place` and nothing said, which is right: the game draws no quest mark there. What looked like an orange "!"
  beside El cruce de Khari in a half-size capture is, cropped at full size, a glowing pillar: the icon of that area's
  "Santuario fundido, Evento notable con mejora", which the pin's own reading says. Its ritual quests ("Completa rituales
  en estas áreas: …") mark no place on either character.

Not heard: "no active waypoint near" (a mark more than six areas from any waypoint) and the loss warning, which need a
game no test here can make; the first is unit-tested (`QuestMarkTests`), the second is the witness rule every other part
uses.
