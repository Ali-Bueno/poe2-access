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
| Double tap | Previous line | Life, mana, energy shield, harmful states, charges and Rage held, the boss's life, the flasks' uses left, then your level, how far to the next, the area's, your gold, the league encounter the HUD shows (tribute, rituals left), and how much of the ground you can reach you have seen ("Life 80 percent, mana 40 percent. Poisoned. The Bloated Miller 55 percent. Lesser Life Flask, 6 uses, Lesser Mana Flask, 2 uses. Level 4, 35 percent to level 5, area level 2. 113 gold. Ritual encounter. 100 Tribute. 0 rituals remaining. Explored 40 percent") |
| Hold (half a second) | Whole tooltip (item mods, gem stats, definitions) | Read the screen (quest tracker, HUD texts), then the skill bar: each set's buttons with what they fire, the empty ones last |
| Hold + right stick flick | — (the game navigates with the right stick in menus) | Scanner (below) |
| Hold + right stick press (R3) | — | Guide beacon on the scanner's choice (below); while it guides, the same gesture turns it off |
| Hold a second and a half | — | The mod's settings menu (below) |

A new focus starts again from its first line. Tooltips are also read automatically when the focus lands
(`ReadTooltipsOnFocus`, on by default).

## Scanner (gameplay)

Hold L3 and flick the right stick: up and down change the category, left and right move through it, nearest
first; both wrap and empty categories are skipped. The category is named only when it changes:
"Characters. Una, 14 steps, right 3, then up right".

- Categories: **quest** (what the quest tracker names: the exit to the area it asks for, the boss to kill, the
  NPC to talk to, and whatever the minimap marks with the quest's own icon; never a place or prop the sentence happens
  to mention, such as "entrance" or "tomb", nor a sign that names a place), exits (area transitions and named places, in the game's language; "Where you died" after a
  death, while that area instance lasts), **points of interest** (what the minimap marks: a boss's arena
  as "Boss room: Asinia" from the moment the minimap points to it, within about 400 cells for a campaign boss and 250
  for a map boss, or once you have seen its place; the quest naming that boss then leads there; "Boss room, defeated"
  when the game's own icon shows the boss dead; secrets and quest landmarks such as "Renly's Workshop", which the game
  draws only near; each said once as "On the map: …"), **places** (named map pieces and props from the game's terrain files:
  bridges, stairs, doors, ruins, graveyards, a cart, a well, an anvil; [places](research/places.md)), **unexplored** (where
  ground never seen begins, one entry per stretch and direction, nearest first: "Unexplored ground, 7 steps,
  left"; ground within about 60 cells of where you walked counts as seen, and a wall with no way through
  hides what lies behind it; forgotten on leaving the area), waypoints and
  checkpoints, **encounters** (league devices: "Ritual altar", where a ritual starts and, once done, where its
  tribute is spent: A on it says "view favours"; listed during the ritual too), characters (NPCs in play; "Renly, has a quest" when the game shows its quest indicator over them,
  with a bell among the object sounds), enemies (with rarity), objects (stash, salvage bench, well, a closed
  barrier: "Closed barrier", readable signs and plaques: "Sign: Ogham Village"), chests
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
gesture while it guides, whatever the scanner has chosen, or changing area, turns it off. With no scanner choice the gesture locks the first
quest target. The ping is silent while a panel or dialogue is open (the lock is kept) and when no route exists
yet: "No route to … yet". When the way stops short it says so once ("A closed barrier cuts the way
to …; guiding as close as it goes") and, at that point, "As close to … as the way goes; it is still
closed"; the ping goes on by itself once the barrier opens.

## Settings menu (gameplay)

Hold L3 for a second and a half: "Mod settings. Help, submenu, 1 of 13". The settings come in submenus (D2R):
Help (five controls topics), Your character (life, mana, energy shield, flasks, states, charges and combo, placed
objects, level up), Enemies, Defence (dodge, parry, shield, stun bar, harmful ground), Navigation (radar, walls,
unexplored ground, turns, places), Things and people nearby, Items, Chat, Speech priority, Volumes, Volume of each
sound, General (language, local co-op, updates) and the Sound glossary. Without holding L3, flick the right stick up or down to move
and left or right to open a submenu, change a value or play a sound (switches toggle, volumes move in steps of 25
percent up to 300, language: same as the game, Spanish, English). Tap L3 to go back to the submenus, on the one just
left; tap it there, or double tap anywhere, to close: "Settings saved" (written to `config/settings.txt`). The menu and
each submenu reopen where they were left. Opening a game panel closes it too.

**Speech priority** (D2R): eleven "Speech: …" entries choose how each kind of news is delivered — waits its turn,
cuts in when urgent, cuts in, or cuts in and cannot be cut off (later news queues behind it for 1.5 s). Defaults:
a harmful state beginning and an arrival with the guide hold; a state ending cuts in; low life/mana, boss life,
enemy names (aimed and coming into view) and places cut in only when urgent (life at or under `LowLifeBelow`, 25 %, mana at 10 %, a boss at
30 % or defeated, a rare enemy coming into view, a quick sweep of the aim); objectives, picked-up items, charges and
combo points wait.

**Items heard and named** (D2R loot filter): which items on the ground get their drop sound and are named on
approach — all (default), magic or better, or only currency, gold, gems, quest items, rares and uniques. The
scanner's Items category still lists every item (`LootSounds`: `all`, `magic`, `valuable`).

**Silence items on the ground in fights** (Items): while an enemy it counts is alive within about a screen (the range
the item sounds carry) and for 2 s after, items on the ground neither sound nor are named, on approach, as what A uses,
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
("Flask on the ground, 75 percent"); left or right moves it 25 percent, 0 to 300, and plays it at the new level.
Items on the ground are grouped by kind: flasks, equipment (armour, weapons, quivers, jewellery, jewels), gems,
currency, gold, quest items, other items, and the rare layer. Kept in `config/settings.txt` as `SoundLevels`
(`{"item_flask": 75, "dodge": 150}`; a sound not listed is at 100).

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
  ([radar](radar.md)). An open way or exit that leads toward ground you have not seen yet ends in two short pips
  (D2R's unexplored mark; setting "Radar: unexplored mark"). In local co-op with both players read, player 2 has a radar of their own: the same
  meanings in other sounds, plucked strings for walls, obstacles and exits, a whistle for the open way.
- **Unexplored area loop:** the D2R mod's "unexplored room" sound plays without a break where the nearest ground you
  have not seen yet begins, on its side and quieter when far (up to about 60 steps), so you always hear which way is
  left to explore; silent in towns, menus and dialogue and while the guide leads (setting "Unexplored area loop").
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
  `AnnounceInteractTarget` ("What the interact button uses").
- **Things worth walking to:** the game's own sounds repeat for every chest in range at once (a body to search, such
  as the Ogham village's victims, with the game's rummage through a corpse instead), the nearest shrine (a hand on a
  lever for switches), ritual altar, every breakable in range at once (each with its material's breaking sound,
  softened and kept low), and for up to four items at once, on their side, higher
  when up the screen and lower when down, more often as you get closer, from the centre once you stand on them. The
  nearest exit or portal and the nearest waypoint or checkpoint sound without a break (a portal's hum, a waypoint's
  low drone, a checkpoint's bright ring, and for an exit the game's area-crossing sound held still), louder as you
  get closer. An item plays the drop sound the game gives its base (a scroll rustles, an orb clinks, a belt,
  a crossbow, a gem each their own; the rare layer over a rare or unique; the generic item, currency, gold and
  quest sounds only for bases without one). They need the sounds extracted once from your game (see
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
  del punto débil del jefe" (on by default), volume with the enemy sounds.
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
- **Harmful ground:** a soft, low pulse (110 Hz, a little over two a second) that repeats while you stand in harmful
  ground or an enemy's damaging zone — burning, caustic, desecrated or bloody ground, a vortex, a boss's flames or
  beam — and stops as you leave it: silence means safe ground. Known by the state the game puts on you there (61 from
  the game files, `catalog/hazards.txt`). Setting "Alarma de suelo peligroso" (on), volume with the enemy sounds.
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
  `AnnounceNearby`, `AnnounceLevelUp`, `ObjectSounds`, `LootSounds`, `DodgeWarnings`, `ParryCues`, `ParryLeadMs`, `CrosshairBeep`,
  `LowLifeBelow`, volumes `SoundVolume`, `RadarVolume`, `EnemyVolume`, `DodgeVolume` (dodge warning and parry cue),
  `BeaconVolume`, `ObjectVolume` and each sound's own `SoundLevels` in `config/settings.txt`, each 0-300 percent: above
  100 a sound is driven into a soft limit on its own, so it grows louder (the dodge warning about 5 dB at 200, 7 dB at
  300) without clipping the rest.

## Warnings and speech

- "Life 50 percent" / "Life 25 percent" when life falls below half and a quarter, once per fall; they
  cannot be cut off by the next phrase for a moment (`AnnounceLowLife`).
- "Lesser Life Flask, 3 uses" each time you drink a flask, "Lesser Life Flask refilled, 2 uses" each time one gains
  a use, "Lesser Life Flask empty" when one has no use left, once until it refills, and "New flask: …" when one is put
  on the belt during play (`AnnounceFlasks`, "Flask uses", delivered like the life warnings).
- "Mana 50 / 25 / 10 percent" when mana falls past those lines (`AnnounceLowMana`).
- "Energy shield 75 / 50 / 25 percent" as it falls past those lines and "Energy shield depleted" when it is gone,
  once per fall (`AnnounceEnergyShield`, "Avisos de escudo de energía", delivered like the life warnings). Only for a
  pool of at least a fifth of your life: a little energy shield from one item would break in every fight.
- "Stun bar 50 / 75 / 90 percent" as the heavy stun bar rises while you block with a raised shield or parry
  (blocked and parried hits fill it; full, the guard breaks and you are heavily stunned), the real value, once per
  rise (`AnnounceStunBar`, "Barra de aturdimiento al bloquear"; from 75 it cuts in when urgent).
- A boss's life as it falls past 75, 50, 40, 30, 20, 10 and 5 percent, and "… defeated" (`AnnounceBossLife`).
- Harmful states as they begin ("Poisoned", "Frozen", "Stunned"…) and end ("No longer poisoned")
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
  exit: Clearfell", "New checkpoint, near Clearfell Encampment", "Boss in sight: The Bloated Miller" (`AnnounceDiscoveries`).
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
  (any panel beside the inventory, by its own title: the stash, the skills), "Dropped: …" (it lands on the ground),
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
  its place among all the cards of the page. A opens a category; on an item A is the game's "Buy". The
  featured banners are pictures only and are named from their image ("Early Access Supporter Packs, featured, 4 of
  5"); A opens what they show. Your points are read on opening.
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
  even with chat reading off, so you know it went out ("yarkey: WTS boots").
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
