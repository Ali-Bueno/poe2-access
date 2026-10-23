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
| Double tap | Previous line | Read the button hints |
| Hold (half a second) | Whole tooltip (item mods, gem stats, definitions) | Read the screen |
| Hold + right stick flick | — (the game navigates with the right stick in menus) | Scanner (below) |

A new focus starts again from its first line. Tooltips are also read automatically when the focus lands
(`ReadTooltipsOnFocus`, on by default).

## Scanner (gameplay)

Hold L3 and flick the right stick: up and down change the category, left and right move through it, nearest
first; both wrap and empty categories are skipped. The category is named only when it changes:
"Characters. Una, 14 steps, right 3, then up right".

- Categories: exits (area transitions and named places), waypoints and checkpoints, characters (NPCs in
  play), enemies (with rarity), objects (stash, salvage bench, well), chests and shrines, items on the
  ground, other players.
- Distance is along the walkable route, in steps of about a quarter second of walking. The direction is the
  stick direction to push now, then how many steps until the route turns and which way: walk "right 3",
  then ask again. With no turn left only the direction is given; "here" means you are on it; "no route"
  means only the straight line is known (a closed tutorial barrier or door).
- The choice is kept while you walk: tap L3 to hear where it is now. After ten seconds without a question the
  next flick starts again from the nearest entry.

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
  ([nearby](proximity.md)).
- All stop while a panel, menu or dialogue is open. Settings: `Radar`, `WallBump`, `EnemySounds`, `NpcSounds`, `AnnounceNearby`, `AnnounceLevelUp`, `RadarVolume`,
  `EnemyVolume`, `SoundVolume` in `config/settings.txt`.

## Warnings

"Life 50 percent" and "Life 25 percent" when life falls below half and a quarter, once per fall
(`AnnounceLowLife`, on by default).

## Keyboard (development)

`config/settings.txt` keeps keyboard hotkeys for repeat, status and the UI dump; pressing a key switches
the game to keyboard mode, so they are for development only.
