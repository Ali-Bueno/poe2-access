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
  NPC to talk to), exits (area transitions and named places, in the game's language), waypoints and
  checkpoints, characters (NPCs in play), enemies (with rarity), objects (stash, salvage bench, well), chests
  and shrines, items on the ground, other players. Chests and items stay listed once seen until opened or
  picked up.
- Distance is along the walkable route, in steps of about a quarter second of walking. The direction is the
  stick direction to push now, then how many steps until the route turns and which way: walk "right 3",
  then ask again. With no turn left only the direction is given; "here" means you are on it; "no route"
  means only the straight line is known (a closed tutorial barrier or door).
- The choice is kept while you walk: tap L3 to hear where it is now. After ten seconds without a question the
  next flick starts again from the nearest entry.

## Guide beacon (gameplay)

Choose a target with the scanner (the Quest category first is the usual one), hold L3 and press the right
stick: "Guiando hacia Campamento de Sierraclara, 21 pasos…". A short ping then repeats, panned toward where the
walkable route goes next (around walls, never through them), higher when the way is up the screen and lower
when it is down, faster as the route shortens. Walk so the ping is centred. "Llegaste a …" ends it; the same
gesture on the same target, or changing area, turns it off. With no scanner choice the gesture locks the first
quest target. The ping is silent while a panel or dialogue is open (the lock is kept) and when no route exists
yet (a closed tutorial barrier): "Todavía no hay ruta hacia …".

## Settings menu (gameplay)

Hold L3 for a second and a half: "Ajustes del mod. Ayuda: el botón L3 en el juego, 1 de 70" (five help topics, then the settings, then the sound glossary). Without holding L3,
flick the right stick up or down to move through the list and left or right to change the value (switches
toggle, volumes move in steps of 25 percent, language: same as the game, Spanish, English). Tap L3 to close:
"Ajustes guardados" (written to `config/settings.txt`). Opening a game panel closes it too.

**Speech priority** (D2R): nine "Voz: …" entries choose how each kind of news is delivered — waits its turn,
cuts in when urgent, cuts in, or cuts in and cannot be cut off (later news queues behind it for 1.5 s). Defaults:
a harmful state beginning and an arrival with the guide hold; a state ending cuts in; low life/mana, boss life,
aimed names and places cut in only when urgent (life at or under the heartbeat line, mana at 10 %, a boss at 30 %
or defeated, a quick sweep of the aim); objectives and picked-up items wait.

**Wall tones** (off by default, D2R): a soft hiss per screen direction that swells as a wall closes in (higher
ahead, lower behind, panned to the sides); "Alcance de los tonos de pared" sets how far ahead it starts (world
units, 600 ≈ 1.5 s of walking). It follows the radar volume and is silent in menus, like the radar.

**Small obstacles** (on by default, D2R): when the radar's wall on one side is a rock, tree or pillar standing alone
(a blocked group no wider than 5 cells, clear of the map's edge), it knocks hollow (640 Hz) instead of the tock: step round it.

The last group is the **sound glossary**: every sound of the mod by name and meaning ("Latido, vida en o bajo el
límite"); left or right plays it.

## Radial menu

Hold View: the option the right or left stick points at is read as it changes (up Inventory, up-right Cosmetics,
right Skills, down Bind Screen, down-left Character, left Passives, up-left Quests). Release View to open it.

## Sounds (gameplay)

- **Radar:** a tock when a wall closes the way up, left, right or down on screen, a breath when it opens again, a tin
  for an exit on that line. Left and right are stereo, down is a little lower. Only changes sound
  ([radar](radar.md)).
- **Enemies:** a short growl for each enemy on screen, faster as it gets closer; magic and rare enemies buzz on top,
  uniques ring ([enemy sounds](enemy-sounds.md)).
- **NPCs:** a light wooden tap for the nearest NPC or other player on screen, faster as you get closer.
- **Wall bump:** a dull thud when you push the stick into a wall and stop, on the side you pushed.
- **Nearby:** what you walk up to or stand on is named once: checkpoints, NPCs, the stash, chests, items, exits
  ([nearby](proximity.md)); the same thing is named again only after a minute.
- **Things worth walking to:** the game's own sounds repeat for the nearest item (gold, currency, rare and
  quest items differ), chest or shrine, exit or portal, and waypoint or checkpoint on screen, on their side,
  more often as you get closer, silent once you stand on them. They need the sounds extracted once from your
  game: `dotnet run --project tools/Poe2UiExtract -c Release -- sounds "<game folder>" Poe2Accessibility\sounds`
  then rebuild (see `tools/README.md`).
- **Dodge warning:** a rising whoosh on the side of a rare or unique monster as it starts an attack or spell:
  dodge (B).
- **Crosshair:** two short blips when the game's target lands on a living enemy.
- **Heartbeat:** loops while life is at or under 25 percent, until it climbs back to 30.
- All stop while a panel, menu or dialogue is open. Settings: `Radar`, `WallBump`, `EnemySounds`, `NpcSounds`,
  `AnnounceNearby`, `AnnounceLevelUp`, `ObjectSounds`, `DodgeWarnings`, `CrosshairBeep`, `LowLifeHeartbeat`,
  `LowLifeBelow`, volumes `SoundVolume`, `RadarVolume`, `EnemyVolume`, `BeaconVolume`, `ObjectVolume`,
  `HeartbeatVolume` in `config/settings.txt`.

## Warnings and speech

- "Vida 50 por ciento" / "Vida 25 por ciento" when life falls below half and a quarter, once per fall; they
  cannot be cut off by the next phrase for a moment (`AnnounceLowLife`).
- "Maná 50 / 25 / 10 por ciento" when mana falls past those lines (`AnnounceLowMana`).
- A boss's life as it falls past 75, 50, 40, 30, 20, 10 and 5 percent, and "… derrotado" (`AnnounceBossLife`).
- Harmful states as they begin ("Envenenado", "Congelado", "Aturdido"…) and end ("Ya no estás envenenado")
  (`StatusAlerts`).
- What the right stick aims at, while you aim: "Ahogado", "El molinero hinchado, único, 40 por ciento"
  (`AnnounceTarget`), read from the game's own target bar at the top of the screen.
- Discoveries: the first time the game shows an exit, a waypoint or checkpoint, or a boss in the area: "Nueva
  salida: Sierraclara", "Nuevo punto de control", "Jefe a la vista: El molinero hinchado" (`AnnounceDiscoveries`).
- Pick-ups: "Recogido: Traje de ermitaño"; a stack that grew: "Recogido 2 más: …" (`AnnouncePickups`).
- Tooltips mark requirements drawn in red: "No cumples: Requiere Nivel 5".
- The checkpoint travel map names each checkpoint by where it lies from yours: "Viajar al punto de control,
  abajo, botón, 2 de 2".
- Before the game (login, character selection and creation) the controller hints are read when a screen
  opens, and only the new ones when the focus changes them ("Iniciar el juego en el pueblo/guarida, LT más A").
- The mod speaks Spanish when the game does (`Language`: auto, en, es).

## Keyboard (development)

`config/settings.txt` keeps keyboard hotkeys for repeat, status and the UI dump; pressing a key switches
the game to keyboard mode, so they are for development only.
