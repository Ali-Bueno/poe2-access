# Controls

The mod reads the controller state the game itself receives, so it works with any pad the game accepts
(Xbox, DualSense, DualShock, with or without Steam Input). It cannot hide a press from the game: the mod
button is L3 because the game does nothing with it in gameplay or the inventory (on the world map it also
toggles the map legend). Turn the controller commands off with `ControllerCommands` in
`config/settings.txt`.

## L3, the mod button

| Gesture | In menus (something has the focus) | In gameplay |
|---|---|---|
| Tap | Next line of the focused element: its name first, then each tooltip line | Where the scanner's choice is now; nothing chosen: repeat the last speech |
| Double tap | Previous line | Life, mana, energy shield, harmful states and the boss's life ("Vida 80 por ciento, maná 40 por ciento. Envenenado. El molinero hinchado 55 por ciento") |
| Hold (half a second) | Whole tooltip (item mods, gem stats, definitions) | Read the screen (quest tracker, HUD texts), then the skill bar: each set's buttons with what they fire, the empty ones last |
| Hold + right stick flick | — (the game navigates with the right stick in menus) | Scanner (below) |
| Hold + right stick press (R3) | — | Guide beacon on the scanner's choice (below); again on the same choice turns it off |
| Hold a second and a half | — | The mod's settings menu (below) |

A new focus starts again from its first line. Tooltips are also read automatically when the focus lands
(`ReadTooltipsOnFocus`, on by default).

## Scanner (gameplay)

Hold L3 and flick the right stick: up and down change the category, left and right move through it, nearest
first; both wrap and empty categories are skipped. The category is named only when it changes:
"Characters. Una, 14 steps, right 3, then up right".

- Categories: **quest** (what the quest tracker names: the exit to the area it asks for, the boss to kill, the
  NPC to talk to), exits (area transitions and named places, in the game's language; "Donde moriste" after a
  death, while that area instance lasts), **unexplored** (where
  ground never seen begins, one entry per stretch and direction, nearest first: "Zona sin explorar, 7 pasos,
  izquierda"; ground within about 60 cells of where you walked counts as seen, and a wall with no way through
  hides what lies behind it; forgotten on leaving the area), waypoints and
  checkpoints, characters (NPCs in play; "Renly, con misión" when the game shows its quest indicator over them,
  with a bell among the object sounds), enemies (with rarity), objects (stash, salvage bench, well, a closed
  barrier: "Barrera cerrada"), chests
  and shrines, items on the ground, other players. Chests and items stay listed once seen until opened or
  picked up.
- Only what a sighted player knows of (the minimap shows no more): enemies while they are on screen; everything
  else once it has been on screen or on ground you explored; the area's named places once you explored their
  ground; other players always. Restarting the host inside an area forgets what was seen.
- Distance is along the walkable route, in steps of about a quarter second of walking. The direction is the
  stick direction to push now, then how many steps until the route turns and which way: walk "right 3",
  then ask again. With no turn left only the direction is given; "here" means you are on it; "no route"
  means only the straight line is known. "El camino se corta antes: …" leads as close as the ground goes;
  "una barrera cerrada corta el camino: …" says a closed barrier stands between that point and the target
  (tutorial barriers open on a tutorial step: casting the gem from the Riverbank's Large Chest, spending the
  passive point from the Miller). When one opens near you: "Se abrió el paso, 4 pasos, izquierda".
- The choice is kept while you walk: tap L3 to hear where it is now. After ten seconds without a question the
  next flick starts again from the nearest entry.

## Guide beacon (gameplay)

Choose a target with the scanner (the Quest category first is the usual one), hold L3 and press the right
stick: "Guiando hacia Campamento de Sierraclara, 21 pasos…". A short iron chime then repeats, panned toward where the
walkable route goes next (around walls, never through them), higher when the way is up the screen and lower
when it is down, faster as the route shortens. Walk so the ping is centred. "Llegaste a …" ends it; the same
gesture on the same target, or changing area, turns it off. With no scanner choice the gesture locks the first
quest target. The ping is silent while a panel or dialogue is open (the lock is kept) and when no route exists
yet: "Todavía no hay ruta hacia …". When the way stops short it says so once ("Una barrera cerrada corta el
camino a …; te guío lo más cerca posible") and, at that point, "Estás lo más cerca posible de …; el camino sigue
cerrado"; the ping goes on by itself once the barrier opens.

## Settings menu (gameplay)

Hold L3 for a second and a half: "Ajustes del mod. Ayuda: el botón L3 en el juego, 1 de 75" (five help topics, then the settings, then the sound glossary). Without holding L3,
flick the right stick up or down to move through the list and left or right to change the value (switches
toggle, volumes move in steps of 25 percent, language: same as the game, Spanish, English). Tap L3 to close:
"Ajustes guardados" (written to `config/settings.txt`). Opening a game panel closes it too.

**Speech priority** (D2R): nine "Voz: …" entries choose how each kind of news is delivered — waits its turn,
cuts in when urgent, cuts in, or cuts in and cannot be cut off (later news queues behind it for 1.5 s). Defaults:
a harmful state beginning and an arrival with the guide hold; a state ending cuts in; low life/mana, boss life,
enemy names (aimed and coming into view) and places cut in only when urgent (life at or under `LowLifeBelow`, 25 %, mana at 10 %, a boss at
30 % or defeated, a rare enemy coming into view, a quick sweep of the aim); objectives and picked-up items wait.

**Wall tones** (off by default, D2R): a soft, warm hiss per screen direction that swells as a wall closes in
(higher ahead, lower and a little louder behind, the sides panned hard; the D2R recipe and level, well under the cues); "Alcance de los tonos de pared" sets how far ahead it starts (world
units, 600 ≈ 1.5 s of walking). It follows the radar volume and is silent in menus, like the radar.

**Small obstacles** (on by default, D2R): when the radar's wall on one side is a rock, tree or pillar standing alone
(a blocked group no wider than 5 cells, clear of the map's edge), it knocks hollow (640 Hz) instead of the tock: step round it.

**Reach ping** (on by default, D2R): two quick rising notes on the side of the nearest enemy in sight when the gap
between its body and yours falls to "Distancia de alcance" (world units, 40 by default); it sounds again only after
every enemy has stepped back past that distance plus 25.

**Monster modifiers** (on by default): an aimed magic or rare monster is named with its modifiers in the game's own
words and language, as its nameplate shows them ("…, raro, de Rabia, 80 por ciento"); technical and unique-boss
mods without a name are left out.

**Gold** is announced when picked up during play ("Recogido 37 de oro", from the inventory's gold counter; sales and
rewards with a panel open say nothing), under the "Objetos recogidos" switch.

The last group is the **sound glossary**: every sound of the mod by name and meaning ("Aviso para esquivar, un
monstruo raro o único empieza un ataque, esquiva"); left or right plays it.

## Passive tree

The free cursor (left stick) moves by distance, not along the tree's lines, so each node is read with where its
links go: "Velocidad de ataque, alcanzable, Velocidad de ataque aumentada un 3%. Conexiones: arriba, Armadura y
evasión, asignado; abajo a la izquierda, …" (clockwise from up). States: asignado (allocated), alcanzable (linked
to an allocated node or the class's start), sin conexión. Opening the tree reads the skill points and attributes.

## Radial menu

Hold View: the option the right or left stick points at is read as it changes (up Inventory, up-right Cosmetics,
right Skills, down Bind Screen, down-left Character, left Passives, up-left Quests). Release View to open it.

## Skills and binding

In the Skills panel (beside the inventory: D-pad left from the first column) each skill is read with its tags,
cost and description. X on a skill opens the bind screen: "Selecciona el botón al que asignar Descarga de caos.
Set de habilidades principal. LB, vacío"; the D-pad moves between buttons and names the other set when you enter
it; A binds. The Bind Screen (radial menu, down) starts on a button ("Set de habilidades principal. A, vacío");
A on an empty button (X on a bound one) opens the grid of every skill and action that can go on it, read by name,
column and row, then tooltip ("Usar frasco de vida, columna 2, fila 2, Bebes de tu frasco de vida equipado"). Its
first cell, "vaciar el botón", leaves the button empty. On a bound button A picks the skill up to move it ("vacío.
Colocar, A. Cancelar, B") and Y empties it.

In Cosmetics the toggles above each weapon slot read "set de armas I" and "set de armas II".

## Dialogue

The speaker, then the focused option and its place among all options. D-pad left reaches the lore topics on the
left; a topic already heard (drawn grey) is read "atenuado": "Presentación, atenuado, 1 de 8".

## Sounds (gameplay)

- **Radar:** a tock when a wall closes the way up, left, right or down on screen, a breath when it opens again, a tin
  for an exit on that line. Left and right are stereo, down is a little lower. Only changes sound
  ([radar](radar.md)).
- **Enemies:** a short growl for each enemy on screen, faster as it gets closer; magic and rare enemies buzz on top,
  uniques ring ([enemy sounds](enemy-sounds.md)).
- **NPCs:** a light wooden tap for the nearest NPC or other player on screen, faster as you get closer.
- **Wall bump:** a dull thud when you push the stick into a wall and stop, on the side you pushed.
- **Lessons that hold you:** some of the game's tutorial lessons keep the character still until you press what
  they ask (low mana: D-pad right; dodge: B; a passive point: View). The lesson is read whole, again when it comes
  back, and again when you push the stick and the character does not move.
- **Nearby:** what you walk up to or stand on is named once: checkpoints, NPCs, the stash, chests, items, exits
  ([nearby](proximity.md)); the same thing is named again only after a minute.
- **What A uses:** when the game moves its interaction target (the label it marks with the A button) to something
  else, it is named with the button: "Finn, A", then "Alijo, A" one step further. A name said this way is not said
  again by the nearby list. Another player must hold the target a second to be named (people walk past). The first
  exit in an area adds the game's own hint: "Campamento de Sierraclara, A. mantén A para opciones". Setting
  `AnnounceInteractTarget` ("Lo que usa el botón de interactuar").
- **Things worth walking to:** the game's own sounds repeat for the nearest chest or shrine, exit or portal, and
  waypoint or checkpoint on screen, and for up to four items at once, on their side, higher when up the screen and
  lower when down, more often as you get closer, silent once you stand on them. An item plays the drop sound the game gives its base (a scroll rustles, an orb clinks, a belt,
  a crossbow, a gem each their own; the rare layer over a rare or unique; the generic item, currency, gold and
  quest sounds only for bases without one). They need the sounds extracted once from your game (see
  `tools/README.md`: `item-sounds`, then `sounds`), then a rebuild.
- **Dodge warning:** a blade-like rising whoosh ending in a ringing snap over a low thud, on the side of a rare or
  unique monster attacking, louder the closer it is: dodge (B). Only animations that are attacks warn (not
  burrowing, hiding, buffs or summons); a rare's blow warns only within about a step and a half, a unique's from
  anywhere on screen. The mod learns from every blow you take when each kind of attack lands and then warns half a
  second before it; an attack never seen hit warns at its wind-up ([dodge timing](research/dodge-timing.md)).
- **Crosshair:** two short blips when the game's target lands on a living enemy.
- The game's own low-life heartbeat and menu sounds are left to the game: the mod has none of its own.
- All stop while a panel, menu or dialogue is open. Settings: `Radar`, `WallBump`, `EnemySounds`, `NpcSounds`,
  `AnnounceNearby`, `AnnounceLevelUp`, `ObjectSounds`, `DodgeWarnings`, `CrosshairBeep`, `LowLifeBelow`, volumes
  `SoundVolume`, `RadarVolume`, `EnemyVolume`, `BeaconVolume`, `ObjectVolume` in `config/settings.txt`.

## Warnings and speech

- "Vida 50 por ciento" / "Vida 25 por ciento" when life falls below half and a quarter, once per fall; they
  cannot be cut off by the next phrase for a moment (`AnnounceLowLife`).
- "Maná 50 / 25 / 10 por ciento" when mana falls past those lines (`AnnounceLowMana`).
- A boss's life as it falls past 75, 50, 40, 30, 20, 10 and 5 percent, and "… derrotado" (`AnnounceBossLife`).
- Harmful states as they begin ("Envenenado", "Congelado", "Aturdido"…) and end ("Ya no estás envenenado")
  (`StatusAlerts`).
- What the right stick aims at, while you aim: "Ahogado", "El molinero hinchado, único, 40 por ciento"
  (`AnnounceTarget`), read from the game's own target bar at the top of the screen.
- Death: "Has muerto. resucitar en el punto de control", then the focused button (A). Y shows or hides the death
  screen, so a Y meant for a skill can hide it: then "La pantalla de muerte está oculta: Y la muestra".
- Discoveries: the first time the game shows an exit, a waypoint or checkpoint, or a boss in the area: "Nueva
  salida: Sierraclara", "Nuevo punto de control, cerca de Campamento de Sierraclara", "Jefe a la vista: El molinero
  hinchado" (`AnnounceDiscoveries`).
- Enemies coming into view, each monster once per area, a pack in one line by name, count and the direction of its
  middle: "Ahogado, 3, derecha. Lobo podrido, raro, arriba" (`AnnounceEnemies`; bosses are discoveries).
- Pick-ups: "Recogido: Traje de ermitaño"; a stack that grew: "Recogido 2 más: …"; gold: "Recogido 37 de oro"
  (`AnnouncePickups`).
- An item on the ground whose tooltip the game shows (you stand by it or aim at it) says only its name, rarity and
  quality ("Broquel de cuero, Calidad: +5%"); its whole text is read in the inventory. Gold says nothing until picked
  up. A shrine's, checkpoint's or portal's text is read whole.
- Item in hand: "En la mano: Ballesta improvisada. Elige una celda o una ranura de equipo compatible para
  colocarlo"; letting go: "Ya no llevas ningún objeto", or, into an equipment slot, "Equipado: Ballesta
  improvisada, Mano principal" (`AnnounceHand`). These cut in on what is being read; the next D-pad move cuts them.
- Inventory moves without the hand (`AnnounceMoves`): "Equipado: …, Mano principal" and "Desequipado: …" (X on an
  item or a slot); with a panel open, "Nuevo en el inventario: …" (bought, withdrawn) and "… salió del inventario"
  (sold, stored). They follow the focus update the move causes.
- Another player's menu (A on a player): "DrBacamarte, Guerrero Niv. 2, Liga Forbidden Rites. invitar al grupo,
  1 de 5", then each button as the D-pad moves ("comerciar, 3 de 5"); B closes it.
- The large map (D-pad down in play): "Mapa. Mantén presionado cruceta abajo para interactuar con el mapa", then
  "Mapa oculto" when it closes.
- The chat panel (LT in a panel): "Chat. Mantén presionado LT para chatear" when shown, "Chat oculto" when hidden.
- Weapon swap (RT in the inventory, `AnnounceMoves`): "Set de armas 2: Arco, Carcaj" or "Set de armas 2, vacío".
- Legends with inline item art name the item: "Identificar Pergamino de sabiduría x 1, X" (catalog/item-names.json).
- Requirements drawn in red come right after the item name (and price): "No puedes usarlo: Requiere Nivel 5".
- The checkpoint travel map: the game names no checkpoint, so each is said by the named place beside it (an exit,
  waypoint, NPC or object within about 20 steps), where it lies from yours and how far in a straight line:
  "Viajar al punto de control, cerca de Campamento de Sierraclara, abajo, a 35 pasos, botón, 2 de 2"; yours:
  "Estás aquí, cerca de …". A checkpoint not seen since the host started has only its direction.
- Before the game (login, character selection and creation) the controller hints are read when a screen
  opens, and only the new ones when the focus changes them ("Iniciar el juego en el pueblo/guarida, LT más A").
- Character creation: the left stick picks the class ("Bruja, 4 de 6", its quote), A selects it. The name field
  then takes typing at once; the mod says how to finish once per visit: Enter creates the character, or B ends the
  typing and A creates it. After B the left stick moves between name, league ("Liga, Forbidden Rites", A opens the
  list of 12) and Continue. X swaps the class for its variant when it has one ("Bruja" ↔ "Hechicera"), said with its
  quote. A name in use is refused with "Ya existe un personaje con este nombre".
- The mod speaks Spanish when the game does (`Language`: auto, en, es).

## Keyboard (development)

`config/settings.txt` keeps keyboard hotkeys for repeat, status and the UI dump; pressing a key switches
the game to keyboard mode, so they are for development only.
