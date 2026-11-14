# Controls

The mod reads the controller state the game itself receives, so it works with any pad the game accepts
(Xbox, DualSense, DualShock, with or without Steam Input). It cannot hide a press from the game: the mod
button is L3 because the game does nothing with it in gameplay or the inventory (on the world map it also
toggles the map legend). Turn the controller commands off with `ControllerCommands` in
`config/settings.txt`. A `settings.txt` that fails to parse is ignored (the mod says so) and is never overwritten with
defaults; fix the line and it is read again, as it is whenever it is edited while the host runs.

Closing the game menu is silent (the focus underneath is not read again); Options opened from the game menu starts with
its page. If Steam's desktop controller layout is active, the DualSense acts as mouse and keyboard while the game is in
the background, and a drifting stick or the touchpad can disturb the screen reader in other windows: turn that layout
off in Steam's controller settings for Path of Exile 2.

## L3, the mod button

| Gesture | In menus (something has the focus) | In gameplay |
|---|---|---|
| Tap | Next line of the focused element: its name first, then each tooltip line | Where the scanner's choice is now; nothing chosen (none is kept from another area): repeat the last speech |
| Double tap | Previous line | Life, mana, energy shield, harmful states, charges and Rage held, the boss's life, the flasks' uses left, then your level, how far to the next, the area's, your gold, the league encounter the HUD shows (tribute, rituals left), and how much of the ground you can reach you have seen ("Life 80 percent, mana 40 percent. Poisoned. The Bloated Miller 55 percent. Lesser Life Flask, 6 uses, Lesser Mana Flask, 2 uses. Level 4, 35 percent to level 5, area level 2. 113 gold. Ritual encounter. 100 Tribute. 0 rituals remaining. Explored 40 percent") |
| Hold (half a second) | Whole tooltip (item mods, gem stats, definitions) | Read the screen (quest tracker, HUD texts), then the skill bar: each set's buttons with what they fire, the empty ones last |
| Hold + right stick flick | — (the game navigates with the right stick in menus) | Scanner (below) |
| Hold + right stick press (R3) | — | Guide beacon on the scanner's choice (below); while it guides, the same gesture turns it off |
| Hold a second and a half | On a grid (the bag, a stash tab, a merchant's tab): the whole grid, below | The mod's settings menu (below) |

A new focus starts again from its first line. Tooltips are also read automatically when the focus lands
(`ReadTooltipsOnFocus`, on by default). A count or a level that changes under a resting cursor is said by itself ("2"
when a stack of 3 loses one, "Level 8" when a gem levels); a clock ticking in a row is not. A D-pad press that moves
nothing — a page's only control, the end of a list — says the control again a moment later (0.6 s), so silence never means
"nothing happened" (`RepeatOnBlockedMove`, on by default; settings menu, Items); not while something is still being said
(estimated), since the repeat cut an item's own tooltip short at the bag's edge.

With an item in the hand the cursor moves cell by cell (without one it jumps from item to item), its cell being where the
held item's top left corner would go. Each cell says what lies where the item would be put, then the cell: "Vacío, columna
10, fila 1" (A puts it down), "sobre Mirada de odio, raro, columna 6, fila 2" (one item under it: A swaps them, whatever
the game's own legend says — it showed "Colocar" there), "sobre 3 objetos: …" (it cannot be put there; the legend offers
no A). No tooltip nor build lines are read then. Equipment slots and the flasks' row read as ever.

## Scanner (gameplay)

Hold L3 and flick the right stick: up and down change the category, left and right move through it, nearest
first; both wrap and empty categories are skipped. The category is named only when it changes:
"Characters. Una, 14 steps, right 3, then up right".

- The Trial of the Sekhemas' map reads each room's place: "column 2 of 8, room 3 of 3", columns running from the entrance to the boss, the rooms it leads to in the next column ("leads to rooms 3, 4": the lines the game draws),
  through which of the rooms you can enter it is reached when there is a choice, and how it stands: your room, on your way (a way still leads to it from your room),
  no way to it from your room, left behind. Where the links cannot be read it says whether it is your room, your column,
  left behind, in the next column, not reachable yet (two columns on or more) or, beside the rooms you can enter, without a way from your room; a room you can enter says so with its button, one not revealed is "Hidden room".
  The boss's room never says it can be entered (the game shows no description for it): once the room before it is done it is "on your way", or "next column", and A enters it.
- In a Trial room that asks to disable the crystals the guide locks by itself on the Death Crystal quickest to reach and on the next once it is disabled, along a route that goes
  round walls, and lets go when the room is done; you walk, and your own target is left alone (setting "Guide to each Death Crystal by itself").
  The crystals are those you can walk to from where you stand, whatever quest the tracker shows; it takes a couple of seconds after the stairs.
- Taking a room's stairs says the room you arrive in, by the kind you chose on the Trial's map: "Room: Escape Trial" (with the Deceptive Mirror the game may take you to another). Under the switch "Trial rounds, crystals left and room doors".
- In the Trial of the Sekhemas an affliction or boon is said by name and effect as you gain it; the vitals (double tap L3) say your honour, sacred water, keys and
  every affliction and boon you carry, and with the Trial's map open the water, the keys and what you carry. The sacred water monsters drop is named as such.
- In the Trial of the Sekhemas your honour is said as it falls past 75, 50, 25 and 10 percent: at nothing the run ends (setting "Honour in the Trial").
- A clock falling on the HUD, such as the Trial's, is said when first seen and as it passes 60, 30, 20, 10 and 5 seconds (setting "Countdown timers").
- Categories: **quest** (what the quest tracker names: the exit to the area it asks for, the boss to kill, the
  NPC to talk to, and whatever the minimap marks with the quest's own icon, such as "Risu's cage", which the game leaves
  unnamed and which sounds like a quest item; never a place or prop the sentence happens
  to mention, such as "entrance" or "tomb", nor a sign that names a place; a place you have not seen yet is not listed:
  it comes once you see it, or once the game marks it with the quest's icon), exits (area transitions and named places, in the game's language; "Where you died" after a
  death, while that area instance lasts), **points of interest** (what the minimap marks: a boss's arena
  as "Boss room: Asinia" from the moment the minimap points to it, within about 400 cells for a campaign boss and 250
  for a map boss, or once you have seen its place; the quest naming that boss then leads there; "Boss room, defeated"
  when the game's own icon shows the boss dead, for a room known from the map's pieces too, which then drops its reward note; secrets and quest landmarks such as "Renly's Workshop", which the game
  draws only near; each said once as "On the map: …"), **places** (named map pieces and props from the game's terrain files:
  bridges, stairs, doors, ruins, graveyards, a cart, a well, an anvil; [places](research/places.md)), **unexplored** (where
  ground never seen begins, one entry per stretch and direction, named by how much lies behind it, as a look at the
  map tells a sighted player: "Large unexplored ground" (a good part of the area) and "Unexplored ground, 7 steps, left"
  where it goes on, nearest first; then "Small unexplored ground", a room or a short branch that soon ends; then
  "Unexplored nook" where one look from its edge would show it all, a dead end. The size is of the ground you would
  reach without crossing what you have seen, wall by wall: a corridor that ends at a wall is small whatever lies behind
  the wall. It never says where the exit is; ground within about 60 cells of where you walked counts as seen, and a wall with no way through
  hides what lies behind it; forgotten on leaving the area), waypoints and
  checkpoints, **encounters** (league devices: "Ritual altar", where a ritual starts and, once done, where its
  tribute is spent: A on it says "view favours"; listed during the ritual too; "Essence: Blood Cretin", a crystal holding a
  rare monster: A frees it, and it drops the essence when slain; walking up to it says it too), characters (NPCs in play; "Renly, has a quest" when the game shows its quest indicator over them,
  with a bell among the object sounds), enemies (with rarity), objects (stash, salvage bench, well, a closed
  barrier: "Closed barrier", one that opened on its own and you have not walked to yet: "Opened way", readable signs and
  plaques: "Sign: Ogham Village"), chests
  and shrines (rocks, eggs, urns and other breakables have their own breaking sound, bodies to search their own
  rummage), items on the ground, other players. Chests and items stay listed once seen until opened or
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
gesture while it guides, whatever the scanner has chosen, or changing area, turns it off. A target that disappears near you counts as reached only if
using it removes it (a chest, an item, a portal); a boss or an NPC that hides, dies or leaves is "… is no longer in sight", the guide goes on to
where it was and ends there with "Arrived where … was last seen". With no scanner choice the gesture locks the first
quest target. The ping is silent while a panel or dialogue is open (the lock is kept) and when no route exists
yet: "No route to … yet". When the way stops short it says so once ("A closed barrier cuts the way
to …; guiding as close as it goes") and, at that point, "As close to … as the way goes; it is still
closed"; the ping goes on by itself once the barrier opens.

**Guide without confirming** (setting "Guide to the scanner's choice without confirming", Navigation, off by default): the
ping starts on whatever the scanner names, as in the D2R mod, and the next flick moves it to the next choice. Nothing extra
is said. The same gesture turns it off.

## Glance at the map (gameplay)

Hold the left trigger (L2, LT) and press the right stick (R3), without L3: "Explored 60 percent. You are at: Middle of
the map. Left to explore: Top right corner, 4 percent; Right edge, 30 percent. Large unexplored ground, 40 steps, left
3, then up". It is what a look at the large map tells: how much of the ground you can reach you have seen, the part of
the map you are in (thirds across and down, as on entering the area), the parts where ground that goes on is left with
how much of each you have seen, and the three nearest unexplored stretches with their way. A small map has no parts and
says the share and the stretches alone. The left trigger alone only shows the second skill set, as ever.

Walking into another part of the map says it by itself: "Right edge. Explored 12 percent", or "Middle of the map. Already
explored" when only nooks are left there. Not in towns nor in a Trial of the Sekhemas; during a fight it waits until the fight is over (setting "Say
the part of the map you walk into", Navigation, on by default).

## Settings menu (gameplay)

Hold L3 for a second and a half: "Mod settings. Help, submenu, 1 of 10". The settings come in submenus (D2R), some
with submenus of their own:

- Help (six controls topics); Your character (life, mana, energy shield, flasks, states, level up);
- Combat: Enemies (coming into view, aim, enemy sounds, melee reach), Bosses and enemy states (boss life and weak
  point, enemy states, primed for stun, Culling Strike), Defence (dodge, parry, shield, stun bar, harmful ground),
  Charges and skills (charges, combo, placed objects), Quiet in fights (loot, chests and altars; characters' lines);
- Navigation (radar, walls, unexplored ground, turns, places); Things and people nearby; Items; Chat; Speech priority;
- Power charges, combo points and harmful ground also have their own volume right after their switch, in Charges and
  skills and in Defence: the same volume Sound lists;
- Sound: the master volume, then a submenu per kind of sound — Enemies, characters and combat; Dodge and parry; Radar
  and walls; Guide; Objects and places; Charges and shield — each with its volume first and then every sound's own
  volume, and the Sound glossary split the same way;
- General (language, local co-op, updates).

Without holding L3, flick the right stick up or down to move and left or right to open a submenu, change a value or
play a sound (switches toggle, volumes move in steps of 5 percent up to 300, language: same as the game, Spanish,
English). Tap L3 to go back up one level, on the submenu just left; tap it at the top, or double tap anywhere, to
close: "Settings saved" (written to `config/settings.txt`). The menu and each submenu reopen where they were left.
Opening a game panel closes it too.

**Speech priority** (D2R): eleven "Speech: …" entries choose how each kind of news is delivered — waits its turn,
cuts in when urgent, cuts in, or cuts in and cannot be cut off (later news queues behind it for 1.5 s). Defaults:
a harmful state beginning and an arrival with the guide hold; a state ending cuts in; low life/mana, boss life,
enemy names (aimed and coming into view) and places cut in only when urgent (life at or under `LowLifeBelow`, 25 %, mana at 10 %, a boss at
30 % or defeated, a rare enemy coming into view, a quick sweep of the aim); objectives, picked-up items, charges and
combo points wait.

**Items heard and named** (D2R loot filter): which items on the ground get their drop sound and are named on
approach — all (default), magic or better, or only currency, gold, gems, quest items, rares and uniques. The
scanner's Items category still lists every item (`LootSounds`: `all`, `magic`, `valuable`).

**Item sound set** (Sound, after the master volume; D2R item sound sets): the sounds of items on the ground, chests, searchable bodies and shrines:
Path of Exile 2, the game's own (default), or Diablo IV, the D2R mod's set, which plays without a break while the
thing is in range and where equipment sounds by rarity; up on screen it rises only two semitones, since these
recordings are bright (`ItemSounds`: `poe2`, `diablo4`). Exits, waypoints, breakables and the rest keep the game's sounds. The sound glossary
plays the chosen set, and lists the two Diablo IV sounds the game's set has no row for.

**Silence items on the ground in fights** (Combat): while an enemy it counts is alive and in play within about a screen
(the range the item sounds carry; a boss still waiting in its arena does not count, one between phases does) and for 2 s
after, items on the ground neither sound nor are named, on approach, as what A uses,
or by their tooltip; once the fight is over, what dropped is named as usual. Against rares and uniques (default), against
any enemy, or never (`QuietItemsInFights`: `rares`, `any`, `never`). The scanner still lists them.

**Direction of enemies coming into view** (Enemies, on by default): off, the packs are named without where they are,
"Drowned, 3. Rotten Wolf, rare" (`EnemyDirections`), fewer words while a boss talks.

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

**Local co-op** (two controllers on one PC; the last two settings): "Local co-op: read for player 1" (on) and "read for
player 2" (off) choose whom the mod reads for — one, the other or both ([local co-op](research/local-coop.md)).
- Player 1 alone (default): as playing alone; player 2's controller and cursor are not read.
- Player 2 alone: everything follows player 2 — its cursor and controller (from its first press; before it joins at
  character selection the mod reads player 1's), its radar, life, level, states, inventory, flasks and skill bar.
- Both: menus and the mod's commands follow whoever pressed last (so each hears its own inventory, vitals and skill
  bar); each hears its own life, mana, level, states, pick-ups and flasks with its character name first; player 2 has
  its own radar sounds. Enemy, object and place sounds, the scanner and the guide stay player 1's: on a shared screen
  the two are never far apart.
- In every mode: "yarkbruja is down. Go to their body to revive them" when the other player falls (hold A beside the
  body: "Revive, A"), "You are down. The other player can revive you", and who is back on their feet. The NPC tap leaves
  the other local player out; the scanner's Players category lists them.

The last group is the **sound glossary**: every sound of the mod by name, meaning and the volume setting that sets it
("Dodge warning, a rare or unique monster starts an attack, dodge, Dodge and parry volume"); left or right plays it
as loud as the game would (that volume, its own, then the master volume). The harmful-ground alarm and the Culling Strike
cue follow the enemy volume, the wall bump the radar volume.

**Volume of each sound** (after Volumes): the same sounds, each with its own volume, a percentage of its group's
("Flask on the ground, 75 percent"); left or right moves it 5 percent, 0 to 300, and plays it at the new level.
Items on the ground are grouped by kind: flasks, equipment (armour, weapons, quivers, jewellery, jewels), gems,
currency, gold, quest items, other items, and the rare layer. Kept in `config/settings.txt` as `SoundLevels`
(`{"item_flask": 75, "dodge": 150}`; a sound not listed is at 100).

## Passive tree

The free cursor (left stick) moves by distance, not along the tree's lines, so each node is read with the push of
the stick that reaches each of its links: "Attack Speed, small node, reachable, 3% increased Attack Speed. Links: up,
Armour and Evasion, allocated; down left, …" (clockwise from up). A link no single push reaches (another node lies
nearer that way) is said "…, not in one push"; one that only a push halfway between two directions reaches is
said by that push ("between left and up left"). Kinds: small node, notable, keystone, jewel socket. States: allocated,
reachable (linked to an allocated node or the class's start), not linked. Opening the tree reads the skill points and
attributes; LT/RT say the zoom ("Zoom x1,4"); the directions hold at any zoom. A node not linked yet says what it costs
("not linked, 7 points away": the nodes to take along the links from what you have).

**Finder (D-pad, which does nothing else in the tree;** setting "Passive tree finder on the D-pad", Navigation, on by
default): D-pad down and up walk the lists Build (with a Build Planner build active: its keystones, notables and jewel
sockets not taken yet, in the build's order; the list opens with how far along you are, "Build, 16 of 60 taken, 13 points
outside the build, 4 more of the build are ascendancy passives, not on this tree yet": the count is of the nodes the tree
shows, and the file's nodes it does not show are said apart, as ascendancy passives when their ids say so), Taken in the build (the build's nodes you already
have, small ones too, in the build's order, each with its step over those same nodes: "Build: step 3 of 60. Attack Speed,
…": the way made so far, to walk again), Taken outside the build (what
you have that the build does not: the points a refund would bring back to it), Keystones, Notables, Jewel sockets and Reachable now, and left and right walk a
list, those costing fewest points first. What it names is the target, with nothing to confirm: "Keystones. Scarred Faith, up
left, middle ring, 8 points away. From here: up, 10 pushes". Then every node ends with the way, "Toward Scarred Faith: up left,
4 pushes"; push the left stick that way and it counts down to "Arrived at Scarred Faith", where the game reads what it does.
If it is not yours yet, the guide then leads to the points to spend for it, one by one: "Arrived at Scarred Faith, 8 points
to go: allocate Attribute: down left, 12 pushes"; every node ends with "Scarred Faith, 8 points to go: allocate Attribute:
…" (", on the way" on a node of that line), and on that node "allocate this one". Press A there and the guide moves on to the
next node ("7 points to go: allocate …: up, 1 push") until "You have Scarred Faith. Guide off". The line taken is the one
costing fewest points (among those as cheap, the one through the build's nodes, then through notables); toward a node of the
build it keeps to the build's nodes. Nothing is allocated for you. Back at the first list, Position, it says where the
cursor is on the tree ("Position: down right, outer ring, 7 points away") and drops the target. While a target is chosen
it takes the place of the build's "Next in the build" line. Y centres the view on what you have allocated.

**Where you left off:** the game opens the tree at the class's start. The mod keeps the node each character's cursor was
last on (`config/passive-resume.txt`, across sessions), and when the tree opens with no Build Planner build leading (the
build's next node is where you left off then), the guide leads back there: "Where you left off, Attack Speed: down right,
12", every node counting down to "Back where you left off". Allocating a node elsewhere first, or choosing another target
with the D-pad, drops it. Ways are counted up to 60 pushes; beyond, the direction is said ("far, up").

The right stick slides the tree under the cursor, which stays in the middle of the screen: each node passing under it
is read, and on release the view settles on the node in the middle. The links and the finder's ways count
left-stick pushes: after the right stick, the first left push may land elsewhere than said, and
from there the ways hold again.

Keystones need no level: a node only needs a line of allocated nodes reaching it. The nearest are 9 to 13 points from any
class's start, notables 5; the game's "You do not meet the requirements" on a node means it is not linked yet.

**Build Planner** (the game's, patch 0.5): put a build guide's `.build` file in `Documents\My Games\Path of Exile
2\BuildPlanner` (or subscribe on the website), then in the tree press L3 ("Filters/Build Planner"), D-pad right twice
to "Build Planner", A, D-pad down to "Active Build" and pick it. Its nodes are then read "in the build", the next one
to take "next in the build", and every node ends with the way there: "Next in the build: Attribute, not linked: down
left, down". With the file in that folder (not for a website-only build) each build node also says its step, weapon
set and the author's note ("Build: step 2 of 8. Strength +5 is recommended"; a node the file names again for a weapon
set or an attribute is one step, its first); an attribute node whose note shows the
attribute only by colour gets it named ("Recommended attribute: Strength").

**Brief guide** (setting "Brief passive tree guide", Navigation, on by default; user 2026-11-10: "es muy verboso"): while
the finder's target or the build's next node leads, a node passed on the way is its name and the way on, the goal named
only when it changes: "Attribute, in the build. Next in the build: Attack Speed: left, 3", then "Armour and Evasion.
left, 2" (the count falling by one is the sign of a right push; "in the build", or "on the way" on the finder's trail,
that the node is on the guide's line); what the node does follows, for the next push to cut short (the build's step and
note only on the node to take). The node to take says it at once ("Attack Speed, small node,
reachable, next in the build"; "…, allocate this one"), then its stats, step and note, without the links or the game's
"Select, A" hints; L3 held reads everything, links included. Allocated there: "Attack Speed, allocated. Next in the
build: Attack Speed: left, 1".


## Radial menu

Hold View: the option the right or left stick points at is read as it changes (up Inventory, up-right Cosmetics,
right Skills, down Bind Screen, down-left Character, left Passives, up-left Quests). Release View to open it.

## Skills and binding

In the Skills panel (beside the inventory: D-pad left from the first column) each skill is read with its tags,
cost and description; entering the panel says first what it shows above and below the list ("0 Spirit. Equipped Gem
Capacity: Str (0/1), Dex (0/2), Int (0/2)"). Manage Sockets (A on a skill) reads the supports socketed in it: "Fury I, 1
of 2", an empty one "Support Gem Slot, 1 of 2" (the greyed places after them are only drawn on the row: a socket added
later becomes "3 of 3"). Engraving (X on an uncut gem): LB/RB change the list's category, said first ("Staff. Thunder
Strike, Level 2, 1 of 21"; the build planner's recommendations are a category of their own). For an uncut support gem
each category is one of your skills: the support chosen there is engraved straight into that skill, said when done
("Fury I, socketed in Tempest Bell"). X on a skill opens the bind screen: "Select button to bind Chaos Bolt. Primary Skill Set.
LB, empty"; the D-pad moves between buttons and names the other set when you enter
it; A binds. The Bind Screen (radial menu, down) starts on a button ("Primary Skill Set. A, empty");
A on an empty button (X on a bound one) opens the grid of every skill and action that can go on it, read by name,
column and row, then tooltip ("Use flask in slot 1, column 2, row 2, …"). Its
first cell, "empty the button", leaves the button empty. On a bound button A picks the skill up to move it ("empty.
Place, A. Cancel, B") and Y empties it.

**The build's gems** (the Build Planner's active build, when its `.build` file is in the game's folder): the game shows
them only in the engraving window, so the mod sets the file against your own gems. Entering the Skills panel says how far
along you are and what comes next: "Build: 2 of 9 skills, 0 of 20 supports. Next skill: Fangs of Frost. Next support: Rage
II for Whirling Slash". Each skill then says its place: "in the build. Build supports: Rage II, missing; Martial Tempo II,
you have Martial Tempo I; Bludgeon, socketed" (also "in your inventory", "socketed in Twister"), or "outside the build".
An empty skill slot lists the build's skills you still lack, those for a later level last ("Build skills to get: Fangs of
Frost; Barrage; Combat Frenzy, from level 26"). In Manage Sockets a socketed support says "in the build", "the build asks
for Martial Tempo II" or "outside the build", and an empty socket what goes there ("The build puts here: Rage II,
missing; …"). A cut gem in the inventory, the stash or a merchant's stock says "in the build, for Twister" or "outside the
build", and so does each choice of the engraving window. The build is known without opening the passive tree (the game's
settings name it per character).

**The build's gear** (the same file's piece per equipment slot, at your level). On an equipment slot, after the item's
own lines: "Build: Shielded Helm, armour, another base" — the base by its name in the game's language, the defence its
own values give (armour, evasion, energy shield or a mix), "from level 33" while the piece is still to come, "same
base" or "another base, yours gives evasion" — then each modifier the author wants, in his order, against the worn
item's own line: "+29 to maximum Life, the build asks for 40", "+16% to Fire Resistance, met", "Missing +16% to
Lightning Resistance". An empty slot says what goes there ("Asks for: …"). A unique the build names is said by the
author's name ("Build: unique Headhunter, it is not this one"). On any other item that can be worn — in the inventory,
the stash or a merchant's stock — one short line comes right after its name, per slot it would go in: "Build, helmet: 2
of 3 modifiers, another base, more than the worn one, which has 1" ("fewer than", "same as the worn one", "nothing worn
there"; an item of another class than the build's, a staff where it names a spear, says "another kind of item, the build
asks for Ironhead Spear" instead of "another base"); the modifiers one by one come after the item's own lines, as the rest of the tooltip does (tap for the next
line, hold for all of it). A worn piece is compared from the start: by its tooltip's lines once its slot has been
visited in the session, before that by its modifier lines made from the game's memory (a line the mod cannot make with
certainty is left out, and nothing is then called missing on that piece). Entering the inventory says how the worn gear
stands: "Build: 3 of 12 pieces on its base, 5 of 14 modifiers, read on 12 of 12 pieces. Furthest from the build: helmet"
— not again when coming back from the stash or a merchant's stock. What the game's files cannot name or tell apart is said in the author's own words and never counted
as had or missing ("Not compared: …"). Charms, quivers and foci are not compared yet. The game's own note stays: a slot
the build names offers "Show Build Planner notes, hold R3".

In Cosmetics the toggles above each weapon slot read "weapon set I" and "weapon set II"; the search box is "Search
here..., edit field", the "x" at its end "Clear search, button".

**A merchant or a stash opening** is one line: the tab of the stock, where the cursor is and its item ("tab 1.
Inventory. Orb of Transmutation, 6, column 1, row 1, 180 gold. …"), then your gold ("4,776 gold"), then the buttons. A
stock that is the gamble (Finn's "Buy or sell items" in Clearfell: the window has no title, its items are "Random Spear"
with a price) opens with "Gamble, random items, stock 100/100", the counter the game shows under it; the build says
nothing of a random item.

**Search in a stash or a merchant** (L3, "Edit search"; the game lights what matches and dims the rest): once typed, the
mod says what is lit with each cell, "3 highlighted: Sombre Gloves, rare, column 3, row 1. …" (six named, the rest
counted), "Nothing highlighted", and "Search cleared" when you hold L3 to clear it (the mod's own reading on a held L3
stays quiet there). A lit item under the cursor says "highlighted" after its name. A stash opened with a search still in
its field says what it lights as the cursor enters the grid.

**An item you cannot use** says which requirement is missing right after its name: "Doom Chant, rare, column 3, row 3,
Cannot use: 21 Int", then the whole tooltip.

## Other panels

- Trials: the mod says honour each time it falls past a tenth, sacred water and keys as they are gained, and the L3 double tap adds them to the vitals ("Honour 474 of 513. Sacred Water 39, plus 7. Keys: bronze 1, silver 0, gold 0"). A trap about to strike plays the dodge warning from its side; those in the open are in the scanner and named on approach. Both are switches in Surroundings.
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
- Teleport map (A at a waypoint): the D-pad moves between the places; D-pad up from the places under the title
  reaches the act tabs ("Teleport. Act 2, button, 2 of 2"); left and right move between the acts and A shows that
  act's map with the cursor back on its places, the town first.
- Social: Friends opens with your account name and area after the focus; a Guild or Party page without one reads
  its explanation after the focus ("You are not currently in a guild. …").
- Game menu: its tip (R3 for skills and buffs) and the pause banner are said once a session, after the focus.

## Dialogue

The speaker, then the focused option and its place among all options. D-pad left reaches the lore topics on the
left; a topic already heard (drawn grey) is read "dimmed": "Introduction, dimmed, 1 of 8".

## Sounds (gameplay)

- **Radar:** a tock when a wall closes the way up, left, right or down on screen, a breath when it opens again, a tin
  for an exit on that line. Left and right are stereo, down is a little lower. Only changes sound
  ([radar](radar.md)). An open way or exit that leads toward ground you have not seen yet ends in two short pips
  (D2R's unexplored mark; setting "Radar: unexplored mark"). Two pips: that side leads to unexplored ground that goes
  on. One pip: to unexplored ground that soon ends, a room or a short branch (a nook only once nothing else is left).
  So walking tells a dead end from the way on, with no list to open. In local co-op with both players read, player 2 has a radar of their own: the same
  meanings in other sounds, plucked strings for walls, obstacles and exits, a whistle for the open way.
- **Another room:** the radar's tin also marks a doorway ahead: the way narrows between walls and leads to another
  space, a hall, a corridor, it is not only floor (setting "Radar: tin at the way into another room").
- **Hint when lost:** after about three minutes walking without finding new ground (fights, menus and standing still
  do not count), the D2R mod's "unexplored room" sound plays for four seconds on the side the way on lies, then stops.
  Sooner than that, after thirty seconds walking over ground you have already seen, the same sound points at the
  nearest unexplored ground that goes on, and again every thirty seconds until you find new ground: what a look at the
  map would tell you, not where the objective is (setting, Navigation; 0 for never).
  A dead end is new ground too, so the same sound also comes once you have moved ten minutes through an area, whatever
  you found, and every three minutes of moving after that; after twenty minutes the way is said in words with its route,
  "Hint: the way goes on, 40 steps, up 3, then right": where the walk to the objective leaves the ground you have seen,
  not the objective. Nothing is said once the objective's ground has been seen. Both times are settings (Navigation;
  0 for never).
  It points to where the walk to what the quest asks leaves the ground you have seen, else toward a boss room not found
  yet, else to the nearest ground that goes on. Silent in towns and while the guide leads (settings "Sound hint when
  lost" and "Sound hint: seconds walking without new ground", 60 to 600).
- **Map hints:** walking past a cue the game's designers placed on the map, the reader says it once: "Dead cattle, the
  blood trail goes to the right", "Mushrooms, thicker down", "Glowing roots, running up", "River, upstream to the left",
  and any sign ("Sign: Ogham Village"). In Act 3 an explorer camp also adds the next entrance to the scanner: "Explorer
  camp. New exit: Chimeral Wetlands". The side is the mod's own working out, by the walk to where the cue leads, not something
  the game shows; a sign says none while the place it names is unknown (settings "Map hints when passing them" and
  "Map hints: say which way they go").
- **Enemies:** a short growl for each enemy on screen, faster as it gets closer; magic and rare enemies buzz on top,
  uniques ring ([enemy sounds](enemy-sounds.md)).
- **NPCs and players:** a light wooden tap for each of the two nearest NPCs on screen, a quicker, higher double tap
  for the nearest other player, faster as you get closer (setting "Other players' sounds" silences the players
  alone, for a crowded town). A player down to revive calls with a soft falling ding-dong, also off screen and
  behind walls: go to the body and hold A.
- **Wall bump:** a dull thud when you push the stick into a wall and stop, on the side you pushed.
- **Lessons that hold you:** some of the game's tutorial lessons keep the character still until you press what
  they ask (low mana: D-pad right; dodge: B; a passive point: View). The lesson is read whole, again when it comes
  back, and again when you push the stick and the character does not move.
- **Nearby:** what you walk up to or stand on is named once: checkpoints, NPCs, the stash, chests, items, exits,
  a ritual altar ([nearby](proximity.md)); the same thing is named again only after a minute (an item whenever you walk back to it).
- **Places:** walking into a named part of the map says it once, after what is being said ("bridge", "stairs",
  "door"); a bridge of several pieces is one. Then the nearest named prop within two or three steps ("cart",
  "well", "anvil", "notice board"), again after going six steps away. Setting "Name places you walk into".
- **What A uses:** when the game moves its interaction target (the label it marks with the A button) to something
  else, it is named with the button: "Finn, A", then "Stash, A" one step further. A name said this way is not said
  again by the nearby list. Another player must hold the target a second to be named (people walk past). The first
  exit in an area adds the game's own hint: "Clearfell Encampment, A. hold A for options". Setting
  `AnnounceInteractTarget` ("What the interact button uses"). An object the game explains in a box of its own says, the first
  time in an area, what the game writes it is for: "Blood Chalice, A. Defeat all Rare Monsters in the area to fill the Chalice".
- **Things worth walking to:** the game's own sounds repeat for every chest in range at once (a body to search, such
  as the Ogham village's victims, with the game's rummage through a corpse instead), the nearest shrine (a hand on a
  lever for switches), ritual altar (not one already done, which the scanner still lists), every breakable in range at once (each with its material's breaking sound,
  softened and kept low), and for up to four items at once, on their side, higher
  when up the screen and lower when down, more often as you get closer, from the centre once you stand on them. The
  nearest portal and the nearest waypoint or checkpoint sound without a break (a portal's hum, a waypoint's low drone,
  a checkpoint's bright ring), louder as you get closer; the nearest exit is a low rush of air from the game, repeated
  faster and louder as you get closer, so it stands out beside a checkpoint. The nearest stash sounds with its lid and the
  nearest well with its water, the sounds each makes when you use it; the nearest sign to read sounds with pages being
  turned. A way that opens on its own (a room's gate in the middle of a fight) sounds once as it opens, from its side, and
  then keeps sounding from two screens away until you have walked to it. Walking off any of them, the sound dies away
  over about six steps past the edge of the screen. An item plays the drop sound the game gives its base (a scroll rustles, an orb clinks, a belt,
  a crossbow, a gem each their own; the rare layer over a rare or unique, the quest sound over a quest item, over whatever the quest's icon marks and on the campaign's named barriers, such as the Ancient Seal; the generic item,
  currency, gold and quest sounds alone only for bases without one). They need the sounds extracted once from your game (see
  `tools/README.md`: `item-sounds`, then `sounds`), then a rebuild.
- **Culling Strike** (setting "Aviso de golpe de gracia": by default only while one of your skills culls, else always or
  never): a gong struck from the side of an enemy that has just dropped to its culling threshold, as the game defines
  it (35% life normal, 20% magic, 10% rare, 5% unique): a recorded gong, sped up and cut short (`assets/cull.wav`; the
  synthesized trembling gong when it is missing); a rare or
  unique one is also named ("Beira, de la manada putrefacta: golpe de gracia"). The skills that cull come from the game
  files (`catalog/cull-skills.txt`: Killing Palm, Reap, Cull the Weak, Crushing Fear, Mist Raven, His Winnowing Flame);
  culling from a support gem, an item or a passive needs "siempre". A raised threshold (Enhanced Culling) is not taken
  into account.
- **Placed by a skill** (setting "Sonido de lo que colocan tus habilidades", on by default): the game's own sound of
  what your skills place to be struck, Tempest Bell's toll (and its skins') and Frozen Locus's ice crystal forming (a
  small high bell until the sounds are extracted), on its side, more
  often and louder as you get closer, from the centre once you stand at it (object volume); several are heard one after
  another, never at once. Each new kind is logged
  ("Placed by a skill: …"); others are added by their entity path in `DeployedObjects`.
- **Primed for Stun** (setting "Sonido de listo para el aturdimiento", on by default; for Boneshatter and other
  heavy hits): a wind-up rising an octave and a half from the side of an enemy whose heavy stun buildup has reached
  the game's threshold (40% normal, 50% magic, 60% rare, 70% unique), once until it drops below again or is stunned;
  a rare or unique one is also named ("Beira, de la manada putrefacta: listo para el aturdimiento"). Every build's
  hits build stun, so it has its own switch.
- **Enemy states** (setting "Sonidos de estados del enemigo", on by default): a short sound of its own, from the
  enemy's side, as an enemy enters a state that skills pay off on — heavy stunned (a low gong), frozen (three icy
  sparkles rising), electrocuted (a crackling buzz), pinned (two nail knocks), armour broken (a gritty crack
  falling), dazed (a wobbling tone sinking), parried (a bright ting). One sound per state at a time, so a nova that
  freezes a pack chimes once. A rare or unique enemy is also named with the game's term ("Beira, de la manada
  putrefacta: aturdimiento total"). Only the states your party causes ever happen, so the sounds follow the build.
  All of them are in the sound glossary.
- **Shield raised / lowered:** a short metal clank gliding up as you raise your shield (Raise Shield, held) and a
  quieter one gliding down as it comes down, by the player's animation (ActiveBlockChannel / ActiveBlockEnd).
  Setting "Escudo levantado y bajado" (on by default). The stun bar that blocking fills is spoken (Warnings below).
- **Boss weak point:** a heartbeat (two low thumps) from the side of a boss's head when its tail is not the part to
  strike (the Devourer); faster as you near the head, slow and soft once you are within melee reach of it, silent
  while it is buried. The first time in an area: "El devorador: golpea la cabeza, sigue el latido". Setting "Latido
  del punto débil del jefe" (on by default), volume with the enemy sounds. The same heartbeat leads to a boss that hides
  until you find it (Balbala in her poison fog): "El jefe se esconde: sigue el latido, abajo a la izquierda", then the
  beat from her side until she is back ("El jefe ha vuelto"); with the setting off the line gives the direction alone.
  And to the way out of a boss's walls (Kabala's bone cage, before her blast): "Muros: sigue el latido hasta la salida,
  arriba", or with no way out on foot "Muros por todos lados: sigue el latido y rompe uno, izquierda" (the beat leads to
  the nearest wall); "Fuera de los muros" once you are out. Not heard in the game yet.
- **Dodge warning:** a blade drawn over a low gong, diving into a deep bass drop on the moment to dodge (metal and
  bass, so it is not taken for the radar's breath of open floor; the drop needs headphones or speakers with some
  bass, the blade is heard anywhere), on the side of a rare or
  unique monster attacking, louder the closer it is (and never faint far off in a boss arena): dodge (B). Only animations that are attacks warn (not
  burrowing, hiding, buffs or summons); a rare's blow warns only within about a step and a half, a unique's from
  anywhere on screen. It warns half a second before the blow: from the first attack, by the hit time in the game's
  own animation data (about 70% of monster attacks), and by what it learns from every blow you take, which wins;
  an attack timed neither way warns at its wind-up. A monster that keeps swinging is warned for each swing
  ([dodge timing](research/dodge-timing.md)).
- **Unblockable attack:** two low, harsh brass stabs a tritone apart, from the attacker's side, at the wind-up of a
  rare's or unique's attack the game marks as impossible to block (its red glow): neither a shield nor Parry stops it,
  so dodge; the dodge warning then times the roll as usual. Attacks you can block or parry keep the plain dodge
  warning. Setting "Aviso de ataque imbloqueable" (on), volume "Volumen de esquiva y Parada".
- **Harmful ground:** two harsh stabs of low notes that grind against each other, kin to the unblockable cue, that repeat while you stand in harmful
  ground or an enemy's damaging zone — burning, caustic, desecrated or bloody ground, a vortex, a boss's flames or
  beam — and stops as you leave it: silence means safe ground. Known by the state the game puts on you there (61 from
  the game files, `catalog/hazards.txt`). Setting "Alarma de suelo peligroso" (on), volume with the enemy sounds, and its own under Sound.
- **Parry cue** (only for a character with the Parry skill: a buckler, a targe or two swords): a reverse hit, a
  struck buckler played backwards, that swells in for a third of a second and stops dead, from the attacker's side;
  press Parry on the stop, which falls 150 ms before a blow you can parry. Strikes from any monster within about a
  step and a half, and a rare's or unique's projectiles; only blows whose time is known (the same timings as the dodge
  warning) and that leave time for the whole swell, at most one cue per 0.75 s (Parry's own attack time), and never
  for an attack the game marks as impossible to block or parry (a boss's red-glowing wind-up: the Bloated Miller's
  charge, the Executioner's slams), which only the dodge warning announces. Settings
  "Aviso de Parada" (on) and "Antelación del aviso de Parada, milisegundos" (0-600: raise it if you press late, lower
  it if you press early)
  ([parry and the stun bar](research/parry-and-stun-bar.md)).
- **Crosshair:** two short blips when the game's target lands on a living enemy.
- The game's own low-life heartbeat and menu sounds are left to the game: the mod has none of its own.
- All stop while a panel, menu or dialogue is open. Settings: `Radar`, `WallBump`, `EnemySounds`, `NpcSounds`, `PlayerSounds`,
  `AnnounceNearby`, `AnnounceLevelUp`, `ObjectSounds`, `LootSounds`, `ItemSounds`, `DodgeWarnings`, `ParryCues`, `ParryLeadMs`, `CrosshairBeep`,
  `LowLifeBelow`, volumes `SoundVolume`, `RadarVolume`, `EnemyVolume`, `DodgeVolume` (dodge warning and parry cue),
  `BeaconVolume`, `ObjectVolume` and each sound's own `SoundLevels` in `config/settings.txt`, each 0-300 percent: above
  100 a sound is raised under a look-ahead limiter of its own, so its body grows louder (the dodge warning about 4 dB at
  200, 5 dB at 300) while its peaks are held under full scale, without the distortion of clipping; the whole mix goes
  through the same kind of limiter.

## Warnings and speech

- "Life 50 percent" / "Life 25 percent" when life falls below half and a quarter, once per fall; they
  cannot be cut off by the next phrase for a moment (`AnnounceLowLife`).
- A percentage or a count (life, mana, a boss's life, charges, combo, the guide's "Now up right, 4") that had to wait
  more than two seconds behind a lesson or a message being kept is not said late: the L3 double tap gives the present
  values. What happened (a pick-up, a state beginning, an arrival) is said however late.
- "Lesser Life Flask, 3 uses" each time you drink a flask, "Lesser Life Flask refilled, 2 uses" each time one gains
  a use, "Lesser Life Flask empty" when one has no use left, once until it refills, and "New flask: …" when one is put
  on the belt during play (`AnnounceFlasks`, "Flask uses", delivered like the life warnings).
- "Mana 75 / 50 / 25 / 10 percent" when mana falls past those lines (`AnnounceLowMana`); 75 is said again only once
  the mana has been back to 95, the others 30 points above their line.
- "Energy shield 75 / 50 / 25 percent" as it falls past those lines and "Energy shield depleted" when it is gone,
  once per fall (`AnnounceEnergyShield`, "Avisos de escudo de energía", delivered like the life warnings). Only for a
  pool of at least a fifth of your life: a little energy shield from one item would break in every fight.
- "Stun bar 50 / 75 / 90 percent" as the heavy stun bar rises while you block with a raised shield or parry
  (blocked and parried hits fill it; full, the guard breaks and you are heavily stunned), the real value, once per
  rise (`AnnounceStunBar`, "Barra de aturdimiento al bloquear"; from 75 it cuts in when urgent).
- A boss's life as it falls past 75, 50, 40, 30, 20, 10 and 5 percent, and "… defeated" (`AnnounceBossLife`).
- Harmful states as they begin ("Poisoned", "Frozen", "Stunned"…) and end ("No longer poisoned"); a crossbow at Rapid Shot's maximum heat: "Weapon overheated", then "Weapon ready"
  (`StatusAlerts`).
- What you build up and spend, said and heard by separate switches, all on by default: `ChargeSpeech` / `ChargeSounds`
  ("Decir…" / "Sonidos de cargas, infusiones, sobrecargas y furia") and, for the Combo alone, `ComboSpeech` /
  `ComboSounds` ("Decir…" / "Sonidos de puntos de combo"); their speech levels are "Voz: cargas" and "Voz: puntos de
  combo" (see Speech priority). Power,
  Frenzy, Endurance and Spirit charges, the Sorceress's Fire, Cold and Lightning Infusions, the Fire, Cold, Lightning
  and Chaos Surges of bows and crossbows, Druidic Prowess, Arcane Surge and the Monk's Tempest Bell Combo (4 ring the
  bell; "Combo: 4", "sin combo" when it drains after 8 s). A power charge gained plays an electric crackle (the game's
  own, from an electric charge cosmetic); a combo point plays the mod's combo sound, a semitone higher for each point
  held, the first on C. Both are your own character's, so they play in stereo from the middle, not from a side. Gaining any other plays a glass tick for each one now held (Spirit highest, then Frenzy,
  Endurance lowest; the others share one pitch); spending one or losing it plays one softer, lower tick. The count is said once it stops changing, so a chain of Killing Palm kills
  is one "3 cargas de poder"; the others by name and count ("Infusión de fuego: 2"), Arcane Surge by name. Rage
  moves with every hit, so it is said only as it fills ("Furia al máximo", a low tick) and as it runs out ("sin
  furia"). A build without any of them never hears it. The partner's are said, not ticked.
- What the right stick aims at, while you aim: "Drowned", "The Bloated Miller, unique, 40 percent"
  (`AnnounceTarget`), read from the game's own target bar at the top of the screen.
- Death: "You have died. Resurrect at Checkpoint", then the focused button (A). Y shows or hides the death
  screen, so a Y meant for a skill can hide it: then "The death screen is hidden: Y shows it".
- Discoveries: the first time the game shows an exit, a waypoint or checkpoint, or a boss in the area: "New
  exit: Clearfell, up left", "New checkpoint, near Clearfell Encampment, down right", "Boss in sight: The Bloated
  Miller, right" (`AnnounceDiscoveries`). The direction is where it lies on the screen, in a straight line, and the
  chime comes from that side, higher above you and lower below.
  Said once, so it holds through a fight for a moment; other spoken news waits behind it.
- Where you start: the area's line ends in the part of the map you are in, in screen directions: "Area: The Manor
  Ramparts, level 14. Bottom left corner." From there the way on lies the other way (setting "Say the map corner on
  entering an area"; not in towns and hideouts).
- Enemies coming into view, each monster once per area, a pack in one line by name, count and the direction of its
  middle: "Drowned, 3, right. Rotten Wolf, rare, up" (`AnnounceEnemies`; bosses are discoveries); without the
  direction when `EnemyDirections` is off.
- Pick-ups: "Picked up: Hermit Garb"; a stack that grew: "Picked up 2 more: …"; gold: "Picked up 37 gold"
  (`AnnouncePickups`).
- An item on the ground whose tooltip the game shows (you stand by it or aim at it) says only its name, rarity and
  quality ("Leather Buckler, Quality: +5%"); its whole text is read in the inventory. Gold says nothing until picked
  up. A shrine's, checkpoint's or portal's text is read whole.
- Item in hand: "In hand: Makeshift Crossbow. Choose a cell or a matching equipment slot to place it"; letting go: "You no longer hold an item", or, into an equipment slot, "Equipped:
  Makeshift Crossbow, Main hand" (`AnnounceHand`). These cut in on what is being read; the next D-pad move cuts them.
- Inventory moves (`AnnounceMoves`) are said as what you did: "Equipped: …, Main hand", "Unequipped: …", "Sold:
  Padded Boots, for 9 gold" (Y at a merchant), "Bought: …, for 9 gold", "Moved to Stash: …" and "Taken from Stash: …"
  (any panel beside the inventory, by its own title: the stash, the skills — which take and give back only gems:
  anything else that leaves beside them was dropped or used), "Dropped: …" (it lands on the ground),
  "Used: …" (a currency's last orb, a rune socketed), "Obtained: …" (a ritual favour, a reward). The focus update the
  move causes waits for them instead of cutting them off.
- Messages the game or your action produces ("Inventory full", "The item has no room for more mods", "Game menu",
  "Character selection") are no longer cut off by the cursor's next stop: it follows them. Closing a panel or an NPC
  back to play stops what it was reading (a long tooltip); the world's first words after it are kept. LB/RB that
  switch a merchant's or the stash's tab while the cursor stays in the inventory say the new tab ("Armour").
- Trade market (game menu, "Trade Market"): pick filters, then RB searches. Each listing is read as a sighted player
  scans it: the item, its price, the seller and whether you can buy at once or the player is offline, when it was listed and the gold
  fee, its place in the list; then the item's properties and mods. Listings scrolling into view are not read until
  the cursor gets there.
- Microtransaction shop (game menu, "Microtransaction Shop"): LB/RB change the tab ("Shop", "Special Offers"); the
  D-pad moves the game's highlight, read as the card it lands on: a category ("Stash Tabs, 27/27, 2 of 13"), an item
  with its price in points and its flags ("Extra Stash Tab, 30 points, 5 of 27", "…, NEW!", "You save 435 points!"),
  its place among all the cards of the page ("3 variants" for an item sold in variants, "You already have it" once
  owned). A opens a category; on an item A opens its page, which does not buy yet: its text is read, then the buy
  button, "Buy Ghost Dog Pet, 150 points, you have 300 points". A there asks to confirm on the same button ("Confirm,
  Confirm your purchase of 150 points, Ghost Dog Pet, you have 300 points"); A again buys, B closes the page. Below it,
  "Equip". The featured banners are pictures only and are named from their image ("Early Access Supporter Packs,
  featured, 4 of 5"); A opens what they show. Your points are said when the shop opens and after a purchase.
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
  setting "Global and trade chat" (off by default); NPCs, system notices, whispers and party always are, without
  the channel's sign: "From Dahnte: hi", "To Dahnte: look". What you send yourself is read back in every channel,
  even with chat reading off, so you know it went out ("yarkey: WTS boots"). In a fight (as for items on the ground:
  against rares and uniques by default, against any enemy, or never), what characters say aloud nearby, a boss's
  taunts, NPC shouts and local chat, is skipped, not kept for later; whispers, party, guild, system notices and your
  own lines are still read ("Silence characters' lines in fights" in Combat, `QuietChatInFights`).
- Weapon swap (RT in the inventory, `AnnounceMoves`): "Weapon set 2: Crude Bow, Blunt Quiver" or "Weapon set 2, empty".
- Legends with inline item art name the item: "Identify Scroll of Wisdom x 1, X" (catalog/item-names.json).
- Requirements drawn in red come right after the item name (and price): "Cannot use: Requires Level 5".
- The checkpoint travel map: the game names no checkpoint, so each is said by the named place beside it (an exit,
  waypoint, NPC or object within about 20 steps), where it lies from yours and how far in a straight line:
  "Travel to Checkpoint, near Clearfell Encampment, down, 35 steps away, button, 2 of 2"; yours:
  "You are here, near …". A checkpoint not seen since the host started has only its direction.
- Before the game (login, character selection and creation) the controller hints are read when a screen
  opens, and only the new ones when the focus changes them ("Start Game").
- Login on a first start: the game waits for the keyboard and mouse and shows no controller buttons; the mod says "The
  game is waiting for the keyboard and mouse. Press a button on the controller to play with it." once.
- Login: the news banner is a picture, read as "News, button, opens in a web browser" (A opens it). LB opens the patch
  notes in the browser; Steam counts the game as running until that browser is closed. Exit quits at once, with no question.
- Character selection: Create Character and the characters are entered by holding A. The left stick click opens the
  league filter ("League filter, [Show all], combo box, expanded"), the stick walks it, A picks, B closes.
- Character creation: no class is highlighted when the screen opens and A does nothing until the left stick has moved:
  the stick picks the class ("Witch, 4 of 6", its quote), A selects it. The name field then takes typing at once; the mod
  says how to finish once per visit: Enter creates the character, or B ends the typing and A on Continue creates it.
  After B the left stick moves between name, league ("League, Forbidden Rites", A opens the list of 12) and Continue,
  and A on the name ("A to type") goes back to typing. X swaps the class for its variant when it has one ("Witch" ↔
  "Sorceress"), said with its quote, and goes back to typing too.
- Names take 3 to 23 letters or underscores. The game ignores the rest in silence, so the mod says it: "Not allowed:
  letters and underscore only" for a number, a space or a hyphen; "Name full: 23 characters at most"; "Name too short:
  at least 3 letters" on Enter, A or B while Continue is off (the stick cannot reach Continue then); "Not typing: go to
  the name and press A" for keys pressed outside the field; and A inside the field says how to create. A name in use is
  refused with "A character with this name already exists"; the league can no longer be changed after that refusal.
- The mod speaks Spanish when the game does (`Language`: auto, en, es).

## Keyboard (development)

`config/settings.txt` keeps keyboard hotkeys for repeat, status and the UI dump; pressing a key switches
the game to keyboard mode, so they are for development only.
