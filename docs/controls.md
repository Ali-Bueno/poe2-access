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
| Double tap | Previous line | Life, mana, energy shield, harmful states, the boss's life, the flasks' uses left, then your level, how far to the next, the area's, your gold, and how much of the ground you can reach you have seen ("Life 80 percent, mana 40 percent. Poisoned. The Bloated Miller 55 percent. Lesser Life Flask, 6 uses, Lesser Mana Flask, 2 uses. Level 4, 35 percent to level 5, area level 2. 113 gold. Explored 40 percent") |
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
  NPC to talk to), exits (area transitions and named places, in the game's language; "Where you died" after a
  death, while that area instance lasts), **places** (named map pieces and props from the game's terrain files:
  bridges, stairs, doors, ruins, graveyards, a cart, a well, an anvil; [places](research/places.md)), **unexplored** (where
  ground never seen begins, one entry per stretch and direction, nearest first: "Unexplored ground, 7 steps,
  left"; ground within about 60 cells of where you walked counts as seen, and a wall with no way through
  hides what lies behind it; forgotten on leaving the area), waypoints and
  checkpoints, characters (NPCs in play; "Renly, has a quest" when the game shows its quest indicator over them,
  with a bell among the object sounds), enemies (with rarity), objects (stash, salvage bench, well, a closed
  barrier: "Closed barrier"), chests
  and shrines, items on the ground, other players. Chests and items stay listed once seen until opened or
  picked up.
- Only what a sighted player knows of (the minimap shows no more): enemies while they are on screen; everything
  else once it has been on screen or on ground you explored; the area's named places once you explored their
  ground; other players always. Restarting the host inside an area forgets what was seen.
- Distance is along the walkable route, in steps of about a quarter second of walking. The direction is the
  stick direction to push now, then how many steps until the route turns and which way: walk "right 3",
  then ask again. With no turn left only the direction is given; "here" means you are on it; "no route"
  means only the straight line is known. "The way stops short: …" leads as close as the ground goes;
  "a closed barrier cuts the way: …" says a closed barrier stands between that point and the target
  (tutorial barriers open on a tutorial step: casting the gem from the Riverbank's Large Chest, spending the
  passive point from the Miller). When one opens near you: "The way opened, 4 steps, left".
- The choice is kept while you walk: tap L3 to hear where it is now. After ten seconds without a question the
  next flick starts again from the nearest entry.

## Guide beacon (gameplay)

Choose a target with the scanner (the Quest category first is the usual one), hold L3 and press the right
stick: "Guiding to Clearfell Encampment, 21 steps…". A short iron chime then repeats, panned toward where the
walkable route goes next (around walls, never through them), higher when the way is up the screen and lower
when it is down, faster as the route shortens. Walk so the ping is centred. "Arrived at …" ends it; the same
gesture on the same target, or changing area, turns it off. With no scanner choice the gesture locks the first
quest target. The ping is silent while a panel or dialogue is open (the lock is kept) and when no route exists
yet: "No route to … yet". When the way stops short it says so once ("A closed barrier cuts the way
to …; guiding as close as it goes") and, at that point, "As close to … as the way goes; it is still
closed"; the ping goes on by itself once the barrier opens.

## Settings menu (gameplay)

Hold L3 for a second and a half: "Mod settings. Help: the L3 button in play, 1 of 76" (five help topics, then the settings, then the sound glossary). Without holding L3,
flick the right stick up or down to move through the list and left or right to change the value (switches
toggle, volumes move in steps of 25 percent, language: same as the game, Spanish, English). Tap L3 to close:
"Settings saved" (written to `config/settings.txt`). Opening a game panel closes it too.

**Speech priority** (D2R): nine "Speech: …" entries choose how each kind of news is delivered — waits its turn,
cuts in when urgent, cuts in, or cuts in and cannot be cut off (later news queues behind it for 1.5 s). Defaults:
a harmful state beginning and an arrival with the guide hold; a state ending cuts in; low life/mana, boss life,
enemy names (aimed and coming into view) and places cut in only when urgent (life at or under `LowLifeBelow`, 25 %, mana at 10 %, a boss at
30 % or defeated, a rare enemy coming into view, a quick sweep of the aim); objectives and picked-up items wait.

**Items heard and named** (D2R loot filter): which items on the ground get their drop sound and are named on
approach — all (default), magic or better, or only currency, gold, gems, quest items, rares and uniques. The
scanner's Items category still lists every item (`LootSounds`: `all`, `magic`, `valuable`).

**Wall tones** (off by default, D2R): a soft, warm hiss per screen direction that swells as a wall closes in
(higher ahead, lower and a little louder behind, the sides panned hard; the D2R recipe and level, well under the cues); "Wall tone range" sets how far ahead it starts (world
units, 600 ≈ 1.5 s of walking). It follows the radar volume and is silent in menus, like the radar.

**Small obstacles** (on by default, D2R): when the radar's wall on one side is a rock, tree or pillar standing alone
(a blocked group no wider than 5 cells, clear of the map's edge), it knocks hollow (640 Hz) instead of the tock: step round it.

**Reach ping** (on by default, D2R): two quick rising notes on the side of the nearest enemy in sight when the gap
between its body and yours falls to "Reach distance" (world units, 40 by default); it sounds again only after
every enemy has stepped back past that distance plus 25.

**Monster modifiers** (on by default): an aimed magic or rare monster is named with its modifiers in the game's own
words and language, as its nameplate shows them ("…, rare, of Venom, 80 percent"); technical and unique-boss
mods without a name are left out.

**Gold** is announced when picked up during play ("Picked up 37 gold", from the inventory's gold counter; sales and
rewards with a panel open say nothing), under the "Picked up items" switch.

The last group is the **sound glossary**: every sound of the mod by name and meaning ("Dodge warning, a rare
or unique monster starts an attack, dodge"); left or right plays it.

## Passive tree

The free cursor (left stick) moves by distance, not along the tree's lines, so each node is read with where its
links go: "Attack Speed, small node, reachable, 3% increased Attack Speed. Links: up, Armour and
Evasion, allocated; down left, …" (clockwise from up). Kinds: small node, notable, keystone, jewel socket. States:
allocated, reachable (linked to an allocated node or the class's start), not linked. Opening the tree reads the skill
points and attributes.

## Radial menu

Hold View: the option the right or left stick points at is read as it changes (up Inventory, up-right Cosmetics,
right Skills, down Bind Screen, down-left Character, left Passives, up-left Quests). Release View to open it.

## Skills and binding

In the Skills panel (beside the inventory: D-pad left from the first column) each skill is read with its tags,
cost and description; entering the panel says first what it shows above and below the list ("0 Spirit. Equipped Gem
Capacity: Str (0/1), Dex (0/2), Int (0/2)"). Manage Sockets: the support sockets are "Support Gem Slot, 1 of 2" (the
greyed places after them are only drawn on the row: a socket added later becomes "3 of 3"). X on a skill opens the bind screen: "Select button to bind Chaos Bolt. Primary Skill Set.
LB, empty"; the D-pad moves between buttons and names the other set when you enter
it; A binds. The Bind Screen (radial menu, down) starts on a button ("Primary Skill Set. A, empty");
A on an empty button (X on a bound one) opens the grid of every skill and action that can go on it, read by name,
column and row, then tooltip ("Use flask in slot 1, column 2, row 2, …"). Its
first cell, "empty the button", leaves the button empty. On a bound button A picks the skill up to move it ("empty.
Place, A. Cancel, B") and Y empties it.

In Cosmetics the toggles above each weapon slot read "weapon set I" and "weapon set II"; the search box is "Search
here..., edit field", the "x" at its end "Clear search, button".

## Other panels

- Character: entering it says the class and the resistances' heading ("Monk. Resistances. DEX, 11, 8 of 14"); in
  the stat list (inspect mode) each section is named as the links move into it ("Armour. Estimated Physical Damage
  Reduction, 0%. physical, link").
- Options: rows the game has greyed out are read with "unavailable" ("Window Resolution, 2560 x 1440, combo box,
  unavailable, 6 of 12") — the game highlights them without giving them the focus.
- Challenges: each category says whether it is open ("The Ritualist, 0/11, collapsed, 1 of 8"); A says "expanded"
  and reads its lines, or "collapsed".
- Quests (world map): the surface/underground toggle says what the map shows ("…, above ground, button"; A: "below
  ground"). In the quest list a quest's reward is named by its icon: "Reward: support gem, gold" (R3 shows the
  items themselves).
- Social: Friends opens with your account name and area after the focus; a Guild or Party page without one reads
  its explanation after the focus ("You are not currently in a guild. …").
- Game menu: its tip (R3 for skills and buffs) and the pause banner are said once a session, after the focus.

## Dialogue

The speaker, then the focused option and its place among all options. D-pad left reaches the lore topics on the
left; a topic already heard (drawn grey) is read "dimmed": "Introduction, dimmed, 1 of 8".

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
- **Places:** walking into a named part of the map says it once, after what is being said ("bridge", "stairs",
  "door"); a bridge of several pieces is one. Then the nearest named prop within two or three steps ("cart",
  "well", "anvil", "notice board"), again after going six steps away. Setting "Name places you walk into".
- **What A uses:** when the game moves its interaction target (the label it marks with the A button) to something
  else, it is named with the button: "Finn, A", then "Stash, A" one step further. A name said this way is not said
  again by the nearby list. Another player must hold the target a second to be named (people walk past). The first
  exit in an area adds the game's own hint: "Clearfell Encampment, A. hold A for options". Setting
  `AnnounceInteractTarget` ("What the interact button uses").
- **Things worth walking to:** the game's own sounds repeat for the nearest chest or shrine, exit or portal, and
  waypoint or checkpoint on screen, and for up to four items at once, on their side, higher when up the screen and
  lower when down, more often as you get closer, silent once you stand on them. An item plays the drop sound the game gives its base (a scroll rustles, an orb clinks, a belt,
  a crossbow, a gem each their own; the rare layer over a rare or unique; the generic item, currency, gold and
  quest sounds only for bases without one). They need the sounds extracted once from your game (see
  `tools/README.md`: `item-sounds`, then `sounds`), then a rebuild.
- **Dodge warning:** a blade-like rising whoosh ending in a ringing snap over a low thud, on the side of a rare or
  unique monster attacking, louder the closer it is: dodge (B). Only animations that are attacks warn (not
  burrowing, hiding, buffs or summons); a rare's blow warns only within about a step and a half, a unique's from
  anywhere on screen. It warns half a second before the blow: from the first attack, by the hit time in the game's
  own animation data (about 70% of monster attacks), and by what it learns from every blow you take, which wins;
  an attack timed neither way warns at its wind-up. A monster that keeps swinging is warned for each swing
  ([dodge timing](research/dodge-timing.md)).
- **Crosshair:** two short blips when the game's target lands on a living enemy.
- The game's own low-life heartbeat and menu sounds are left to the game: the mod has none of its own.
- All stop while a panel, menu or dialogue is open. Settings: `Radar`, `WallBump`, `EnemySounds`, `NpcSounds`,
  `AnnounceNearby`, `AnnounceLevelUp`, `ObjectSounds`, `LootSounds`, `DodgeWarnings`, `CrosshairBeep`, `LowLifeBelow`, volumes
  `SoundVolume`, `RadarVolume`, `EnemyVolume`, `BeaconVolume`, `ObjectVolume` in `config/settings.txt`.

## Warnings and speech

- "Life 50 percent" / "Life 25 percent" when life falls below half and a quarter, once per fall; they
  cannot be cut off by the next phrase for a moment (`AnnounceLowLife`).
- "Lesser Life Flask empty" when a flask has no use left during play, once until it refills (`AnnounceFlasks`,
  delivered like the life warnings).
- "Mana 50 / 25 / 10 percent" when mana falls past those lines (`AnnounceLowMana`).
- A boss's life as it falls past 75, 50, 40, 30, 20, 10 and 5 percent, and "… defeated" (`AnnounceBossLife`).
- Harmful states as they begin ("Poisoned", "Frozen", "Stunned"…) and end ("No longer poisoned")
  (`StatusAlerts`).
- What the right stick aims at, while you aim: "Drowned", "The Bloated Miller, unique, 40 percent"
  (`AnnounceTarget`), read from the game's own target bar at the top of the screen.
- Death: "You have died. Resurrect at Checkpoint", then the focused button (A). Y shows or hides the death
  screen, so a Y meant for a skill can hide it: then "The death screen is hidden: Y shows it".
- Discoveries: the first time the game shows an exit, a waypoint or checkpoint, or a boss in the area: "New
  exit: Clearfell", "New checkpoint, near Clearfell Encampment", "Boss in sight: The Bloated Miller" (`AnnounceDiscoveries`).
- Enemies coming into view, each monster once per area, a pack in one line by name, count and the direction of its
  middle: "Drowned, 3, right. Rotten Wolf, rare, up" (`AnnounceEnemies`; bosses are discoveries).
- Pick-ups: "Picked up: Hermit Garb"; a stack that grew: "Picked up 2 more: …"; gold: "Picked up 37 gold"
  (`AnnouncePickups`).
- An item on the ground whose tooltip the game shows (you stand by it or aim at it) says only its name, rarity and
  quality ("Leather Buckler, Quality: +5%"); its whole text is read in the inventory. Gold says nothing until picked
  up. A shrine's, checkpoint's or portal's text is read whole.
- Item in hand: "In hand: Makeshift Crossbow. Choose a cell or a matching equipment slot to place it"; letting go: "You no longer hold an item", or, into an equipment slot, "Equipped:
  Makeshift Crossbow, Main hand" (`AnnounceHand`). These cut in on what is being read; the next D-pad move cuts them.
- Inventory moves without the hand (`AnnounceMoves`): "Equipped: …, Main hand" and "Unequipped: …" (X on an
  item or a slot); with a panel open, "New in inventory: …" (bought, withdrawn) and "… left the inventory"
  (sold, stored). They follow the focus update the move causes.
- Another player's menu (A on a player): "DrBacamarte, Warrior Lvl 2, Forbidden Rites League. Invite to Party,
  1 of 5", then each button as the D-pad moves ("Trade, 3 of 5"); B closes it.
- Long texts (the terms of use, RB on the login screen): "Long text, 248 paragraphs: scroll it with the stick
  to read it", then the paragraph at the scroll position with its place ("78 of 248. …"); scrolling forward a little reads
  the paragraphs passed, in order.
- The large map (D-pad down in play): "Map. Clearfell Encampment. Forbidden Rites League. Realm São Paulo (BR). Hold
  D-pad down To Interact With Map" (the area, league and realm the map's corner shows), then "Map hidden" when it closes.
- The chat panel (LT in a panel): "Chat. Press-Hold LT to Chat" when shown, "Chat hidden" when hidden.
- Chat channel: in the chat panel D-pad left reaches the channel list ("Global, combo box"); A opens it ("Choosing:
  Global, 2 of 6"), the D-pad reads each channel ("Party, 3 of 6") and A chooses it: "Channel: Party". Holding LT
  types; the field is named by its channel ("Global, #, text field"). X shows the channel filters (check boxes:
  "Global, checked", A shows or hides that channel). Other players' global and trade lines are read only with the
  setting "Global and trade chat" (off by default); NPCs, system notices, whispers and party always are.
- Weapon swap (RT in the inventory, `AnnounceMoves`): "Weapon set 2: Crude Bow, Blunt Quiver" or "Weapon set 2, empty".
- Legends with inline item art name the item: "Identify Scroll of Wisdom x 1, X" (catalog/item-names.json).
- Requirements drawn in red come right after the item name (and price): "Cannot use: Requires Level 5".
- The checkpoint travel map: the game names no checkpoint, so each is said by the named place beside it (an exit,
  waypoint, NPC or object within about 20 steps), where it lies from yours and how far in a straight line:
  "Travel to Checkpoint, near Clearfell Encampment, down, 35 steps away, button, 2 of 2"; yours:
  "You are here, near …". A checkpoint not seen since the host started has only its direction.
- Before the game (login, character selection and creation) the controller hints are read when a screen
  opens, and only the new ones when the focus changes them ("Start Game").
- Character creation: the left stick picks the class ("Witch, 4 of 6", its quote), A selects it. The name field
  then takes typing at once; the mod says how to finish once per visit: Enter creates the character, or B ends the
  typing and A creates it. After B the left stick moves between name, league ("League, Forbidden Rites", A opens the
  list of 12) and Continue. X swaps the class for its variant when it has one ("Witch" ↔ "Sorceress"), said with its
  quote. A name in use is refused with "A character with this name already exists".
- The mod speaks Spanish when the game does (`Language`: auto, en, es).

## Keyboard (development)

`config/settings.txt` keeps keyboard hotkeys for repeat, status and the UI dump; pressing a key switches
the game to keyboard mode, so they are for development only.
