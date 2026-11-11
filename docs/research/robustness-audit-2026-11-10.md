# Robustness audit (2026-11-10)

A read of the plugin for what makes it say something untrue, leak an internal name, or lose a feature to one bad read,
with the play logs of 2026-11-08 to 2026-11-10 beside it. What was fixed that day is listed first; the rest is open, most
player-visible first, each with where it lives.

## Fixed on 2026-11-10

- The passive tree's stick self-check reported a game update when it lost the rule (four times in two days, each "7 hit,
  3 missed"): no longer reported; it also skips pushes it cannot judge (a push it expects to move nothing, one whose pick
  changes ten degrees to either side, the first after the zoom or Y) (`UiSemantics.PassiveTree.JudgeStickLanding`).
- "lejos" for any way the rule could not count: now only when the way is longer than counted (`PassiveStick.Route`'s
  `ranOut`); a stuck or untrusted way is its side alone.
- A part found again before it was said missing was still said missing (`CompatibilityReport.Note`).
- One failed read of the allocated list read as "nothing allocated" (kept for two seconds now); a failed edge search
  silenced the tree for the session (three tries, ten seconds apart, now); the class's own start cached as none.
- "poise" missing after thirty bad ticks in total, however many areas (two areas must agree now); "walkable" on one
  window of thirty cells (two windows now) (`WorldLayoutWatch`).
- A held item said by its file path when its entity was already gone (`InventoryWatch.TickHand`): tried again for a
  second, then "unknown item".
- The scanner's "sin ruta" at a cut the player stood on (`EntityCatalog.Describe`): said as cut.
- A quest's name making a town's well the objective (`QuestTexts`, [ui layers](ui-layers.md)).

- Second pass, the same day (live on yarkey in G2_town, each heard in the log before and after):
  the build mark byte is distrusted when over a quarter of the nodes read marked (`BuildPlan.MarksPlausible`, reported as "passive build marks");
  L3 where the game uses it (a stash's or a merchant's "Edit search", the tree's filters) no longer says the new focus a second time
  (`L3Gestures.MovedFocus`, `ModCommands.OnGesture`); a merchant's tab changed with LB/RB from the inventory's side is said (`ScreenNarrator.TickTabKey`);
  a search kept across a tab change said "Search cleared" and never the new page's matches (`SearchHighlightWatch`: the page shown in place of a hidden one);
  a stash page with support gems said a bare "I", the tier drawn on the icon (`ScreenNarrator.IsTierNumeral`); gems under `Metadata/Items/Gem/` (Twister,
  Attrition, 593 more) were no gems to the move news and the loot filter (`BuildGems.IsGemId`). A merchant's search was tried and works.

## Fixed on 2026-11-10, second pass

Each with a unit test in `RobustnessTests` unless said.

- `PassiveTreeFields.JudgeAllocated`: the list where measured is kept while it names the graph's nodes with at most one id in
  ten unknown (a class override node may stand there under an id the records do not give; `AllocatedListCheck`); it is moved
  only when under one id in ten is known and another list of the state holds three or more ids, each once, all known; said lost
  on the second such reading, ten seconds apart. A list in between is not judged. Ids gathered from under half the records
  (rows not readable yet) judge nothing. The shown row's graph id is still not known to the check: that needs
  `UiSemantics.GraphIdOf` to give both rows' ids (not done: another owner's file).
- `BossWatch.WatchLife`: a boss is defeated when it read not alive and no living reading came for half a second
  (`BossLifeSteps.Defeated`, `ConfirmDefeats`; a corpse the game removed in between counts); a unique whose `Positioned` says
  friend is not tracked (no unit test: a host entity).
- `AttackFieldCheck`: the six verdicts of the dodge fields start over with each game process (`BossWatch.ResetLayout`, called
  beside `FlagBits.Reset`).
- `MonsterMods.Name`: a row is cached only once its name read, or its first character is the terminator (no unit test: reads
  through the host).
- `TradeOfferWatch`: where measured first (grid, grid, title, title); in a body of twelve children or fewer laid out another
  way, the only two grids (`TryReadGrid`) and the only two texts, paired in order (`TradeOfferNews.Sides`).
- `FlagBits.EnabledTrusted`: judged on every pre-game screen until one shows the bit working; lost only when two different
  screens have under 20% of their controls enabled, and given back by any screen that shows it (`EnabledBitCheck`).
- `LayoutProbe` (entity names): settled only by a batch that reads; missing once two different batches read none, ten
  seconds apart or more (`LossWitnesses`).
- `CullSkills.Load`, `HazardGround.Load`: a file in use loads empty. `GameNews` is not guarded here (another owner's file).

Also that day, beyond the Open table ([surviving game updates](../compatibility.md)): the searches of a whole object when
nothing is near where a field was measured; second checks for the element's manager pointer, a node record's edge vectors,
the skill level key and the skill ids; live invariants of the code-only fields in the struct sweep; and every spoken
"lost" made to need two looks, two areas or two witnesses ("When a part is said lost").

## Open

Settled in the fifth pass (2026-11-10, evening); nothing is left merely "open".

| Where | What could go wrong | Outcome |
|---|---|---|
| `UiSemantics.PassiveTree` (the stick step) | The multiple-choice redirect (`row+0x8B`, `elem+0x648`) is not modelled: the first push after an attribute popup lands one node off | **Blocked:** the redirect must be read from the game while an attribute popup is up, which needs a node allocated — the owner's to do (one attribute node, with dev `passivenear` before and after). Such pushes are not judged meanwhile |
| `Beacon` | A search that spent its budget ended as a partial way and was said as "the way stops short" | **Fixed**, unit tests only (`ASearchThatRanOutOfBudgetSaysSo`, `TheWayIsSaidClosedOnlyWhenItIs`). Decision, the owner's to overrule: `RoutePlanner.FindRoute` says when the budget was spent, and the guide then says "La ruta es larga, sigo buscando" once and keeps leading along the part it has (it searches again every 700 ms from where the player is); the scanner's line says "la ruta es larga" instead of "el camino se corta antes". A way truly cut, or cut by a known barrier, keeps its words. The alternative left aside: silence until the whole way is found, which would leave a far target with no guide at all |
| `EntityCatalog.Classify` (`ObjectNames.FromPath`) | An object no table names was said by its file's words, in English ("Chair") | **Fixed and heard** (`AFileWordIsAnUnnamedObjectOutsideEnglish`; in the Act 2 town's scanner: "Objeto sin nombre, Chair, 9 pasos, arriba a la izquierda 2, luego arriba a la derecha"). Decision, the owner's to overrule: outside English it is said as "Objeto sin nombre, Chair" (the phrase in the ten languages), so the English word is heard as a file's word and two such objects can still be told apart; in English it stays "Chair". The alternatives left aside: not saying such objects at all (a thing in the way would go unnamed), or "Objeto sin nombre" alone (every such object the same) |

Closed on 2026-11-10, third pass (with the [file catalogs](file-catalogs.md)):

- `UiSemantics.GraphIdOf`: the allocated list is judged by every graph id of the game's files plus, from the records, the
  id of the row a class sees in each node's place (`GraphIdsOf`, `AllocatedListCheck.AllIds`).
- `PlacesOf`: the places are made again when the count of shown node elements changes (counted every 2 s while a node is
  read, and at once when the hovered node has no place); the finder's atlas and the stick's origin go with them.
- `StickLinksOf`: a node whose row holds the key at +0x66 (an ascendancy's, by the files) has no linked stage, as the game's
  step has none for it, and a push from it is not judged (its sub-canvas shift is not modelled). Over a third of the
  shown nodes carrying it is a misread and is ignored (log `Passive stick: … read as skipping the linked step`).
- `ComboBoxReader`: a layout read on one dropdown is that dropdown's alone until a second reads with the same (`AgreedChoice`).
- `Beacon`: "no route yet" needs two searches in a row that fail or find nothing; the way in use stands through the first.
- `GameNews`: not a fault: both callers guard its file reads (`CheckGameNews`, `RememberGameKeys`).

Not faults, by the logs: the vitals' thresholds re-arm with a margin (154 "Vida 50 por ciento" in two days of fights);
a targeted monster's name is said each time the target changes; the terms of use read their 248 paragraphs.

## Stale values, 2026-11-10

A second audit the same day looked for values said with confidence after the game changed them: a table standing in for a
live value, a cache that outlives what it caches, a line said after its subject changed. Each entry: what was wrong, the
rule now in force, where.

### Player, combat, host

- **Flask uses (A1).** The use count divided the charges by the base type's cost, which ignores the flask's modifiers
  (a magic flask that "consumes 8 of 75 charges" was said as 7 uses and called empty with a drink left). Rule: what a
  drink costs is the drop seen in the charges when the flask is drunk, kept per belt slot and flask name
  (`FlaskDrinks`). A drop counts as a drink when no other drop comes within 3 s on either side and no flask-drain
  effect is on the player (BuffDefinitions has three: `generic_flask_drain`, `monster_flask_drain_aura`,
  `flask_draining_lasso`); two such drinks in a row must agree before the cost is believed or changed, so a charge
  gained from a kill between two reads cannot shift it. Until then the flask is said by its charges ("Frasco de vida
  grandioso, 67 cargas") and is empty only at 0 charges; refills are not said while the cost is unknown. The costs
  survive an area change and are forgotten at character selection. Not covered: a drain slower than one tick in 3 s
  whose ticks repeat, and another flask of the same name put in the same slot (corrected by its second drink). Log:
  `Flask cost: <name> in slot N, 8 charges a drink as seen (was unknown), …`; dev command `flasks` shows base and seen.
- **Boss life in the vitals readout (A2).** `BossWatch.Status` said the first engaged record's last polled percentage:
  after a death and a respawn at the checkpoint it kept "40 por ciento" for a boss the game had reset, a boss that left
  alive kept its last value, and the first record could mask the boss being fought. Rule: the readout reads each engaged
  boss's life from its entity when asked (`LifeNow`); one that is not among the entities, dead or hidden is not said, nor
  one read back at full life after the player's death (the reset the poll would notice on meeting it); of several, the
  one whose life changed last (`BossLifeSteps.Spoken`). Log when an engaged boss is left out: `Boss life not said: …`.
- **The target's name and life (A3).** With a controller the target is the nearest entity carrying the name the HUD
  shows; in a pack of one name its life, rarity and modifiers were the nearest one's, not the targeted one's. Names
  were kept per entity address for the area, so a monster allocated at a dead one's address kept the dead one's name
  and a name read empty stayed empty. Rule: life, rarity and modifiers are said only when one living, in-play entity
  carries the name (or the game's own mouse-over entity gives the target); otherwise the name alone, once while the
  pack stays under the aim (`TargetMatch.Pick`). Corpses and hidden monsters neither are the target nor share its name.
  Names are kept per address and entity id, never empty (`EntityNameMemo`). Log (debug): `Target -> … (name shared:
  life and rarity not said)`.
- **Per-area state follows the instance (A4).** The roughly thirty resets of `OnAreaChanged` ran only when the
  world-area row changed. A loading screen into another instance under the same row (a trial's next room; the play
  logs show one hash per instance, the same hash across respawns) kept everything keyed by entity id, and ids start
  over there: a boss record, the monsters already named, an open trial door, the cull and weak-point sets. Rule:
  `GameStateService` raises `InstanceChanged` when the host's `AreaInstance.AreaHash` becomes another than the one the
  last area change found (`AreaInstanceGate`); the handler runs the same resets (`ResetAreaState`) and names no area.
  The HUD's text memory and the scanner are left out: the first is not keyed by entity, the second binds to the
  instance by itself. A respawn (same hash) resets nothing; a row that changes before the host reads the new instance
  is followed by one more reset when it does. Log: `Instance: <hash> of <area> (<id>), another than the one entered`.
- **Entities the host cannot type (A5).** `Entity.UpdateData` marks an entity it cannot type as Useless and returned
  at once for a Useless non-monster, so the components of barriers, doors, transitions, ground items and trial traps
  were read only at the typing attempts: five frames, then once a second (never again before 2026-11-10, when the
  retry was added). `TrapWatch`, `TrialDoorWatch` and the barrier watch read `StateMachine.States` and `IsBlocked` up
  to a second old. Rule: the once-a-second retry is for the typing attempt (and the component map) only; between
  attempts an untyped entity's components rest like a typed one's, re-read every 100 ms (`EntityRefresh.OfResting`).
  A typed Useless entity (an opened chest) is still not read. Nothing that worked before is lost: monsters and
  players keep their path through the method unchanged.
- **Stacks of a buff held as separate entries (A6).** The host keeps one `StatusEffects` entry per buff name and
  merges further entries of the name into it, counting an incoming entry without a count as one but the entry already
  kept as its raw 0: three entries read as 2 stacks. Rule: `BuffStacks.Merged` counts both sides for what they hold,
  at least one each. A lone entry stays as read (the plugin's `PlayerCharges.Held` makes it one).
- **Learnt dodge times (A7).** A blow was learnt as the real milliseconds since its attack began and replayed as
  milliseconds into the clip at speed 1 (`AttackClock.MsToNominal`): learnt from a chilled or hasted monster it was
  late or early by that speed, for every monster of the kind, and saved. Rule: where the attack's clip was read while
  it ran (rares and uniques, every monster with the all-monsters switch, a player who parries, or while the live
  checks judge), the blow is learnt as its place in the clip: the last reading moved on at its rate for the real time
  since, a projectile's flight taken off (`AttackClock.NominalMsAt`, `Attack.BlowClock`). An attack whose clip was
  not read is still learnt in real time, which is the same number at speed 1; so a normal monster chilled while it
  teaches is still off by the chill, inside what `Believable` lets through. Saved entries (`config/dodge-timings.txt`
  and the shipped seed) stay usable as they are, with no migration: at speed 1 the two measures agree, and each new
  blow pushes an old one out of its five. Still from files, not live: a projectile's flight speed and a clip's reach.
- **Class resources from tables (A8).** Heat has no live maximum (the player stat reads 0), so the largest value the
  player's skills give at their gem level stood in for it and was said as fact: "Calor al máximo, 31", "Calor 12 de
  31". A support, a passive or an item that raises it is not in that row. Rule: a maximum is said only when a live
  player stat holds it. The tables' value (`ResourceTracker.Reading.Reach`) still tells a pool from a count, so the
  pool is not read at every shot, and marks the one count worth telling on the way up, said as a count ("Calor: 31");
  the vitals readout says "Calor: 12". Not done: claiming the maximum from the count having stopped rising, since the
  mod cannot see whether the skill is still adding. Also, any of the catalog's 79 `current_*` stats and 22 limits was
  spoken by its id's English words when held (`difficulty`, `shapeshift`, `active_block_maximum_distance_for_projectiles`):
  only the ones `Announcements.ResourceName` / `LimitName` name are spoken now; the rest are logged once (`Resource
  stat:<name> held (…) has no name in the mod: not said`). Logs for Heat: `Resource stat:number_of_crossbow_heat_stacks:
  no live maximum, the skills' tables give 31: told as a count there, never as full`, and `… above the 31 its skills'
  tables give` if it is ever seen higher.
- **Smaller ones (A9).**
  - `BuffIds.Base` cut a trailing "_" and hex-looking letters, and so 16 ids of `class-mechanics.json` (`shrine_*_2`,
    `rallying_cry_new_weapon_damage_added`): "Inspirado" was never found and `shrine_fire_2` was looked up as `shrine_fire`.
    Rule: only what the host appends is a suffix (upper-case hex), and a table is asked for the whole key first, the
    cut one only when it has that and not the whole (`BuffIds.In`; `ClassMechanicCatalog.BuffKey`, `HarmfulBuffs`,
    `StatusAilments`, `HazardGround`, `BossMechanicsWatch`).
  - `MonsterMods` kept modifier names per Mods.dat row address for the host's life: cleared when the game's process
    id changes (the rows of a game started again are elsewhere).
  - Host `AreaInstance` copied hash, level and terrain only when its address changed: another area loaded at the same
    address, or a hash read before the game wrote it, kept the old values and the old entities. It now reads the area
    again when the hash differs twice running (`ConfirmedChange`), logging `[AreaInstance] Area hash … where … was
    held` to the host's console; the plugin's instance reset (A4) follows from the new hash.
  - Host `Charges.PerUseCharge`, `Chest`'s kind and `Player.Name` were read once per address: a first read that failed
    stayed. Each is read again while it is not a value it can have (a cost under 1, a kind that did not read, an empty
    name).
  - Not done, with the reason: a sale said as "used" when the gold lags the inventory by more than 300 ms
    (`InventoryWatch.SettleMs`) and `QuestTrackerPage.lastSaid` kept between characters live in `Services/Inventory`
    and `Services/Screen`, which the other half of this audit owns (the page has no reset to call from outside). The
    cull cue stays silent when Culling Strike comes from a support, a passive or an item: the mod reads no live stat
    that says the player culls (`game-ids.txt` has only the two threshold stats), and none is guessed.
To confirm live (all of the above is code and unit tests; the game was not run):

| Fix | In the game | In `logs/accessibility.log` |
|---|---|---|
| A1 | Drink the life flask twice, more than 3 s apart and with nothing dying in between, then wait 3 s; drink a third time; L3 double tap with it full | 1st and 2nd: `Speech queued: <flask>, 67 cargas`; then `Flask cost: <flask> in slot 0, 8 charges a drink as seen (was unknown), 59 charges now`; 3rd: `<flask>, 6 usos`; full: "…, 9 usos" for a flask that consumes 8 of 75. Dev command `flasks`: `75 charges, base type 10 a drink, seen 8` |
| A2 | Hurt a boss, die to it, respawn at the checkpoint and ask the vitals away from it; walk back and ask again | `Boss life not said: <boss> (last polled 40%) not there to read, or reset by the game`, and no boss in that vitals line; back at it, the vitals line carries the percentage the game shows |
| A3 | With the pad, aim at a pack of one name with one of them hurt; then at the last one left | Debug: `Target -> <path> "<name>" (name shared: life and rarity not said)`, said once for the pack; then `Target -> <path> "<name>, 40 por ciento"` |
| A4 | Pass from one trial room to the next (same area name); elsewhere, die and respawn | `Instance: <hash> of <area> (<id>), another than the one entered` with no `Area:` line before it; none at the respawn. A unique met in the new room says its own name and life steps |
| A5 | Walk a trial's spike traps; open a barrier with a lever | The dodge cue comes before the spikes; `Barrier opened: <path>` within a moment of the game's own sound, not up to a second after |
| A6 | Hold a buff the game shows with a count (three or more stacks) that the mod said one short | `Resource buff:<id>: Count 3 of 0` with the count the icon shows |
| A7 | Take a blow from a rare or unique that is chilled or hasted, of an attack without contact events | `Dodge timing: <key> hit after N ms (learnt M ms), clip time at rate 0.70 (R ms real)`, N near R times the rate; from an unwarned normal monster `…, real time (no clock read)` |
| A8 | Mercenary: fire Rapid Shot until Heat stops rising; ask the vitals mid-way | `Resource stat:number_of_crossbow_heat_stacks: no live maximum, the skills' tables give 31: told as a count there, never as full`, then `…: Count 31 of 0` and `Speech queued: Calor: 31`; never "al máximo"; vitals "Calor: 12" |
| A9 | Use a warcry that gives "Inspirado"; take a shrine; restart the game under the running host and aim at a rare | `Resource buff:rallying_cry_new_weapon_damage_added: Begun 1 of 0` and the buff's name said; the rare's modifiers read as on screen after the restart |

### Interface, inventory, scanner, speech

- **A number under a resting cursor** (`FocusTracker.PollValue`, `ScreenNarrator.OnValueChanged`, pure `FocusNews`): a
  focused cell, slot or list row whose text changed only in its numbers says the changed part alone ("2" for "Orbe de
  aumento, 3" to "…, 2", "Nivel 8"); only a clock in the row ("11 minutos 46 segundos", "12:28:45") stays a tick, as
  for every other control. The repeat key and the L3 tap already read the element again.
- **The build a tree matched** (`BuildPlanFolder`, `UiSemantics.BuildGems`): kept per character name, and the plan, the
  gems and the Skills panel's names are read again when the asking player changes; a character whose name does not
  read has no build.
- **Moved and sold items** (`InventoryWatch.Reread`, pure `ItemReadings`): an item in reach is described again when the
  stack or the rarity the host shows differs from what its name was read with; the last named reading answers once it
  is gone, and "unknown item" is never kept. A gem without a tooltip is named from the game files (`GemIndex`). A rare
  or a unique moved without its tooltip was said by its base name and rarity; since the fifth pass it is named from the
  Words.dat keys its Mods component keeps (`ItemNameWords`, [item mods](item-mods.md#the-items-own-name-without-the-tooltip-2026-11-10)).
- **A ground item's tooltip** (`EntityCatalog.ItemOfTooltip`, pure `GroundTooltip`; `ScreenNarrator.OnHudChange`): the
  item is the one the game targets, else the one in view under that title; two stacks said differently and no target
  give the title without a count. A tooltip that shows again while the HUD is up is said again (a second stack of one
  currency was silent). Other HUD texts stay once an area: a notice that repeats (a friend connecting twice) is still
  silent the second time, because the HUD also shows again what it showed before (the vitals' tooltips came 15 times
  in one session's log) and which element holds notices needs a live look.
- **The guide's target gone** (`Beacon.Refresh`, pure `BeaconMath.Gone`): within 600 units, only what goes when used (a
  chest, an item, a portal, a quest's object) counts as reached; a creature or anything else is said once as "… is no
  longer in sight", the guide goes on to where it was, and reaching that place says "Arrived where … was last seen".
- **The scanner's choice** (`ScanBrowser.Reset`/`InArea`, `ModCommands.OnAreaChanged`): forgotten with its area
  instance (address and hash, checked before every scanner gesture; the area handler should also call
  `ModCommands.OnAreaChanged`). The guide releases its lock the same way (`Beacon.Tick`).
- **A reading that waited** (`SpeechFloor.Release`, `IAccessibilityOutputService.SpeakReading`): life, mana, a boss's
  life, charges, combo and the guide's turn prompts are readings; one that waited more than 2 s behind a kept line is
  not said (log `Speech not said (a reading that waited too long)`). What happened (a pick-up, a state, an arrival) is
  said however late.
- **A boss room from the area's tiles** (`EntityCatalog.Entries`, pure `MinimapMarks.RoomDefeated`): said "Boss room,
  defeated: …" without the table's reward note once the game's own boss-room mark is spent (the same boss by its
  label, or the area's one mark for its one room; the mark is remembered with the area) or the boss's defeat was told
  to `EntityCatalog.BossDefeated(name)`, which the boss watch should call. A reward note on a place that is no boss
  room ("Una's Lute (+2 passive points)") stays: nothing read today says the reward was taken.
- **Memos by element address** (`UiSemantics.ForgetFocusMemos`, `FocusTracker`): the map's own marker, the depth toggle,
  a grid's tabs and "no Atlas node" are forgotten when the focus changes layer or nothing has had it for half a
  second; a grid found without tabs is searched again after 500 ms; highlight markers are checked to still be markers;
  a texture's key is read again after 2 s.
- **Rows that read alike** (`FocusNews.RowChanged`): another row of a list the game focuses as one element is a move
  even with the same text; the list growing under the cursor is not. A move inside a grid or list resets what counts as
  said of the tooltip. A closed barrier whose entity is gone from within 120 cells of the player is forgotten
  (`BarrierMemory.IsGone`). Left as they are: equal tooltip lines are still read once (why the first import did so is
  not recorded, and no dump shows a tooltip with two equal lines); the explored share is of the ground reachable now,
  as [controls](../controls.md) says.
- **Gems from the game files** (`GemNames` over `GemIndex`): `gem-names.json` and its loader are gone. Over the 101 gems
  the user's seven build files name, both catalogs gave the same name in all ten languages; over all 1,211 keys they
  differ only where the hand-made table lacked or broke a name: 47 uncut gems (no gems of SkillGems; never asked for),
  about 44 gems the files mark `[DNT-UNUSED]` (still said by their id's words), three pattern names ("Spectre: {0}",
  said "Spectre"; `BuildNotes.Plain` cut them to "Spectre: {0") and 27 gems the table did not have. A support is told
  by the files' kind: `SupportOlrothsConviction`, which the build names, and `SupportEnervation` carry no "SupportGem".
  Spirit gems: the files hold the Meta kind and the Persistent tag, neither says which uncut gem engraves a gem, so
  nothing is said of it.
- **A merchant's first tab** (`FocusDetails.PageBeside`): a tabbed page that opens with the cursor on the inventory is
  named with its tab before the inventory's part ("Armas. Inventario. Orbe de caos…", "Alijo, pestaña 1. Inventario. …").
- **The quest tracker after a full-screen panel**: not a repeat inside one run. Of 113 quest pages said in the two play
  logs none repeats an unchanged page without a host start in between; the two seen that day (10:09:32, 11:11:01)
  followed host starts at 10:04:56 and 11:10:42 under an open full-screen panel, so the page was that run's first
  sight of the tracker. Left as it is: a mod started late must still say the objective.

## Fourth pass, 2026-11-10: the whole code in four parts

Four audits the same day read the whole code in four parts (host and tools; interface and speech; world, combat and
audio; player, items, passives, layout and core), against the code of that afternoon, with the play logs of 2026-11-08
to 11-10 where a claim could be checked. Code and unit tests only: none of the fixes ran in a fight or in a live
session. Each entry: what the player would meet, the rule now in force, where. Under each part, `Left open` says why a
finding was not fixed and `Live checks` is what only the game can settle. Findings that were fixed afterwards are under
"Closed after the pass".

### Host and tools

- **A key column taken from the zero half of the key before it (`3d9f47a`).** After a patch widens a key, the extractor
  could take +8 (the zero half of the previous 16-byte key, which "fits" as ClientStrings row 0) and report done: every
  stat of the endgame table read the same string. Rule: a key into another table is the 16 bytes the game writes, row
  then zero (a null key: null twice); a key into the same table stays 8 bytes. `DatColumnSearch`.
- **Class passive overrides read with the class column in the wrong place (`506f18a`).** The Druid and the Huntress could
  hear the Witch's nodes while the log said only "known row absent". Rule: the known row is found by node alone and the
  step fails unless that row is the Witch's and shows `spell_and_minion_damage1`. `PassiveOverrideColumns`.
- **A step given up on put its old file over the shipped one, at every start (`da87fbe`).** A corrected catalog shipped
  by an update was overwritten by the wrong file of the failed version. Rule: the GameData file goes into the plugin only
  when its step really ran at that version. `GameDataStamp.Made`, `GameDataSetup.Regenerate`.
- **An update's download that went quiet held the start for ever, in silence (`239856d`).** Rule: the copy ends after 60 s
  without data (counted again from each piece); the box says the download stopped, the mod stays on its version and the
  game starts. `PackageInstall.Download`, `Updater.Install`.
- **A failed update could leave no launcher, or half of each version (`239856d`).** Rule: the undo puts the launcher back
  first and every file by itself, logging the one it cannot; the new launcher is written beside the old and they change
  names in two renames. `PackageInstall`.
- **Three starts with an index that would not open gave up every optional catalog (`da87fbe`).** Rule: only a step's own
  failure counts toward giving it up; the bundle index failing counts for none. `GameDataStamp.After`, `GameDataSetup.Finish`.
- **Reading the install's files could end the mod with the game already started (`da87fbe`).** A patch replacing the
  index or the exe at that moment raised an error box. Rule: `DescribeInstall` runs inside the guard; a failure keeps the
  data and the mod starts. `GameDataSetup.EnsureCurrent`.
- **The re-read of a map out of id order had the big cap, on the frame thread (`f6db90f`).** A torn loop could cost
  seconds in a fight. Rule: it stops at the map's size plus one (100,000 nodes before, at most 601 in the test).
  `StdMapWalk`.
- **A number near the top of the range threw inside every text search (`b8c0d6f`).** The step, and the audit of that
  table, failed at every start. Rule: offsets are compared with the section's length, never added to its start.
  `DatTable` (`IsStringStart`, `ListItems`).
- **After a move the search had followed, the audit failed the endgame step (`c34a9a6`).** Rule: the two look-ups of the
  known rows are raw reads, not column reads the audit judges. `EndgameExtractor`.
- **The UI scripts' temp folder was named by process id and never emptied (`d1f526c`).** A killed run could merge another
  patch's `.ui` files into the catalog. Rule: the folder is emptied before it is filled. `UiScripts`.

Tests: 12 in `HostAndToolsAuditTests.cs`; two fixtures of `DatColumnSearchTests` and `ExtractorColumnTests` wrote keys as
8 bytes, which the game never does, and now use `WideKey`. Still untested: `GameDataSetup.Regenerate`, `Updater.Offer`,
`GameProcess` coroutines.

Left open, settled in the fifth pass (2026-11-10, evening). Each entry ends as **fixed** (how it was verified), **not a
fault** (why) or **blocked** (by what, and what unblocks it):
- **A key column with no known row can still be a neighbouring key that fits** (finding 12). **Fixed** (unit tests
  `KnownRowTests`, each failing with its known row taken away): every key column of `EndgameColumns` (split out of
  `EndgameExtractor`) and `ProjectileColumns` has a row read from the 0.5.5f files whose key names a row that fits it
  (the Merchant content shows the Trader icon, the Delirium mirror's node stat its own name, the Penitentiary's flavour
  text its map, Kingsmarch's node the Kingsmarch pin, Cull the Weak's stat set the cull stat, the Urchin's sling
  variation the `SlingUrchinProjectile`, …; the list is in the code). Tables whose rows have no Id are found through
  another column: a node stat by its stat, a map by its flavour text, an Atlas node by its passive or pin, a tower by
  its own node. On the game's files each known row accepts exactly one of the key-shaped offsets of its row (scratch
  probe: the Atlas objective among +0x8F, +0xA7, +0xC3; the map pin among +0x3C, +0x5C, +0x6C, +0x84), and
  `endgame`, `cull-skills`, `hit-times` and `game-ids` write the shipped catalogs byte for byte.
- **The launcher says nothing while a step hangs** (13). **Fixed** (`SilenceReminder`, unit tests
  `AStepWithoutProgressIsRemindedFirstAfterThirtySecondsThenEverySixty`, `AMovingCountRestartsTheSilence`): after 30 s
  with no count moving it says "Sigo trabajando en <paso>, unos N min", then every 60 s. The owner's to overrule: the
  two times are constants of `SilenceReminder`. Not heard: on that evening's patch the 28 steps ran in 30 s and none stalled.
- **The game window not up in 240 s ends the mod** (14). **Fixed** (`GameWait`, unit tests
  `TheWaitGoesOnWhileSomethingRunsAndRemindsCalmly`, `TheWaitGivesUpOnlyWhenNothingRanForThePatience`): the wait goes on
  while Steam or a game process runs and ends only after 240 s with neither; "Sigo esperando la ventana del juego" at
  30 s, then every 60 s. On the Kakao route only the game process counts (its patcher's name is not known). Heard on
  the patch of that evening, while Steam verified the install for seven minutes: `Spoken (0): Sigo esperando la ventana
  del juego` at 21:18:20 and then each minute, five times, until the launcher was stopped for a rebuild (`Poe2Access.log`).
- **`ui-catalog` reports done while it loses files** (15). **Fixed** (`UiLoss`, unit test
  `AFewLostUiScriptsAreToleratedAShareIsNot`): the step fails when more than 2 script files in 100 are lost, its output
  is absent and the last catalog stays. The share is a choice, not a measurement (`UiLoss.MaxLostPerHundred`).
- **`gems` and `passive-tree` leave a language out and report done** (16). **Fixed** (`GameDataCounts.Problem`, unit
  tests `ALanguageGoneFromAKeyedSectionIsAProblem`, `ALanguageTheClientNeverHadIsNoProblem`,
  `TheShippedCatalogsPassTheirOwnRule`): a language the last file had and the new one lacks, or sections that disagree
  on their languages, refuse the catalog; a client that never had the language passes.
- **A batch that timed out keeps running beside the frame loop.** **Fixed**, code only (`Extractor.Abandon`): steps
  still queued do not start and an abandoned step deletes its part file; the worker ends within the step it was in.
- **`Player.Name` is read once per component object.** **Not a fault**, seen live: yarkey, then malgdhler in one
  session; the inventory's build summary was malgdhler's own ("Build: 3 de 7 piezas con su base, 3 de 13
  modificadores, leídos en 6 de 7 piezas. Más lejos de la build: Guantes"), and the active build is found by the
  character's name; back on yarkey it was his again ("3 de 6 piezas … Mano principal").
- **`AreaInstanceLayout.Read` falls back on the compiled offsets silently.** **Fixed**, code only: logged once per spell
  of failures.
- **The parallel part of the map walk keeps the cap of 100,000.** **Fixed** (unit test
  `TheParallelPartOfABigMapIsBoundBySizeToo`): bound by the map's size plus one, counted across the seeds.
- **The Korean client's GGPK stays open.** **Fixed**, code only (`GameContent.CloseAll` runs whenever a sequence ends,
  and a file that does not open as a GGPK is disposed); **blocked** for the proof: needs a Kakao client.
- **A release that installs and does not work has no way back.** **Blocked** by the owner's choice between: (a) keep the
  last version's files beside the new ones and a `Poe2Access.exe rollback` command; (b) as now, reinstall the older zip
  by hand. Recommended: (a), because a blind player cannot easily find and unpack an older release.
- **A plugin that is missing or does not load is logged and nothing else.** **Fixed**, code only (`HostRun`): "El plugin
  de accesibilidad falta o no se cargó, así que el juego no se leerá".
- **17 log lines "Failed To Read the Memory" each time the game closes.** **Fixed** (`ReadFailureLog`, unit test
  `ABurstOfFailedReadsIsOneLine`): the first failure of a burst and a count of the rest.
- **`InventoryItemStruct` log bursts on area load** (`Inventory.UpdateData`). **Fixed**, code only: a cell that cannot
  be read is skipped with no line; none was found in the logs at hand (the host log is rewritten at each start), so
  that they had stopped by themselves is not proven.
- **`GameDataSetup` regenerates catalogs only when the launcher starts** (from the player part). **Fixed** as a log
  line (`GameUpdateWatch`, unit test `APatchedGameIsNoticedOnceOnAttach`): a new game process whose install differs from
  the one the catalogs were made for is logged once, and the plugin says "El juego se actualizó. Reinicia el mod para
  actualizar sus datos" when a game process's exe is another than the last one's under the same mod
  (`GameBuildChange`, unit test `AGameRestartedPatchedIsNoticed`). Not heard: on that evening's patch the mod was
  started again with the game, the path that regenerates ("Actualizando los datos del juego tras el parche; tarda unos
  minutos", 28 steps in 30 s, "Datos del juego listos"; then `Code fields: 52 of 52 read; moved: none`).

Live checks, as they stand:
- A fight with 128+ entities (`Frames:`, the id order); boss phases and stacked buffs; an entity whose id changes at one
  address. **Blocked:** need a fight, or leaving town, which the unattended session may not do.
- Log in with one character, out, in with another: done (yarkey, malgdhler, yarkey), each time the build summary of the
  character in play; see the `Player.Name` entry.
- The updater on a real release, and with the network cut. **Blocked:** needs a published release, the owner's to make.
- A start with `oo2core.dll` renamed. **Blocked:** it renames a file of the game's own install; needs the owner's leave
  (or a copy of the install to point the mod at).
- Korean client: the ten languages and the GGPK handle. **Blocked:** needs a Kakao client.

### Interface and speech

- **A boss's defeat, a turn or a partner down could go unsaid as "a reading that waited too long" (`61c774c`).** With a
  speech level changed from the default, news of a reading kind waited behind a kept line and was dropped when older than
  2 s. Rule: urgent news of a reading kind is not a reading; the waiting-life path has a non-reading twin `MentionNews`.
  `SpeechPriority`, `IAccessibilityOutputService`, `PrismOutputService`.
- **A percentage that cut in behind a kept line took the waiting pick-up with it (`94f0bbb`).** Rule: an interrupt
  replaces what waits unless it is a reading. `SpeechFloor.Replaces`.
- **"Vida 50 por ciento" said twice around a kept line (`610a3fe`).** Rule: a warning joined to an urgent line is
  withdrawn from the wait. `SpeechFloor.Withdraw`, `PrismOutputService`.
- **The compatibility warning said internal names in English (`f7fffe3`).** Rule: a part with no wording is said as
  "parte de los datos del juego…" (already in 8 languages); its name stays in the log. `Announcements.Compatibility`.
- **The passive tree never led back to where the character left off on the first opening of a session, and overwrote the
  saved place (`8ea02d1`).** `lastPassiveAt` started at `long.MinValue` and overflowed. Rule: it starts at
  `long.MinValue / 2`. `UiSemantics.PassiveFinder`. No unit test.
- **The whole chat history could be said again (`0773a28`).** One poll that read the chat with no line made the next
  poll's whole list news. Rule: after a reading with no line, more than 5 lines back is history; said lines are forgotten
  past 256. `ChatDiff.News`, `ChatWatcher`. Gives up: more than five lines in one poll into a truly empty chat.
- **"No class selector here" was kept per layer for the process, and a kept selector was read unchecked (`faad19c`).**
  Rule: "none" is kept 500 ms, a kept selector is re-checked by name, the table is bounded at 64 layers.
  `UiSemantics.Selectors` (`SelectorOf`). No unit test.
- **A passive node unnamed in the game files was said with no name (`a83c219`).** Rule: the row's own name stands in.
  `UiSemantics.PassiveTree`.
- **A class start that is not found was searched on every reading of the hovered node (`6fb6d03`).** Rule: once a second
  while the character reads. `UiSemantics.PassiveTree`.
- **A snapshot that ran out of its 6,000-element budget dropped the later layers in silence (`c8dc6c2`).** Rule: it is
  logged, once a minute (`Screen snapshot cut short at 6000 elements`). Raising the budget needs the live cost.
  `ScreenWatcher`.

Findings of this report that other parts fixed: the navigable flag bit moved by hovered tree nodes (O1; see the flag bit
entry of the player part), "Vendido" and other move lines not kept because `SeatOutput.Keep` lacked a parameter (O2; see
the world part; `SeatOutput` now also forwards `MentionNews`), and the passive-tree catalog older than the game said over
the live rows (O3; see the player part).

Left open, settled in the fifth pass (2026-11-10, evening):
- **A control under the free cursor with no tooltip is never said** (N1). **Blocked:** needs one live sweep of the
  free-cursor panels (shop, world map, Atlas) listing the hovered interactive elements that never get a tooltip, then
  the owner's ear for the chatter; the realm's maintenance and patch (21:01 to 21:25) left no time for the sweep.
- **A number that changes under the cursor does not reach the Skills panel's rows** (N2). **Blocked:** needs the list of
  `Panel` rows whose text ticks (a clock would speak every second), read live on the Skills panel. Meanwhile the test
  says what is true: `StaleValueTests` asserts that a `Panel` row is not covered and names this entry.
- **A cell is read whole again when little changed** (N3). **Not a fault:** the readout it replaces had not been spoken
  yet (`ScreenNarrator.OnValueChanged`, the `deferredFocus` branch): the player hears one line, with the value as it is
  now, instead of the old line and then a correction.
- **The skill bar's readout drops out with no log** (N5). **Fixed**, code only: logged once per session when the frames
  no longer match (`UiSemantics.SkillBar`, through `TreeFields.Log`).
- **The dropdown layout latch can be settled by one dropdown** (N6). **Fixed** (`ComboIdentity`, unit test
  `OneDropdownRebuiltIsStillOneOwner`): an owner is its name, its three nearest ancestors' names and its entry count,
  not its address; with every name empty the latch never settles and each dropdown reads with its own layout.
- **`ScreenWatcher.Reset` races the capture under way** (N7). **Fixed**, code only: the generation is taken before the
  reset flag and checked again after the pass is applied, so a pass begun before a reset is dropped.
- **`RouteSpeedTests.ALongRouteStaysWithinTheFrameBound` fails under load** (O4). **Fixed:** the best of several runs
  against the same bound, and the work counted (`RoutePlanner.LastExpansions`: at most 20,000 for the long routes,
  40,000 for the walled goal; measured 4,647 and 7,948). Not run under load.
- **Tests that do not test.** **Fixed:** the Skills row's role (above); the L3 press rule (`L3Gestures.GamesPress`,
  `OnlyATapOnAFocusTheGameMovedIsTheGames`); `FocusDetails.PageBeside`'s third condition
  (`ThePanelBesideIsNamedOnlyWhenItAddsSomething`); the long-text split (`LongTextParagraphs`,
  `ALongTextSplitsIntoItsLinesWithWords`, `AScrollLandsOnTheParagraphHoldingTheOffset`).
- **`SpeechPriority.IsReading` aged events under a reading kind.** **Fixed** (unit test
  `OnlyANumberIsAReadingAtEveryKindAndLevel`, kinds x levels x urgent x happened): a line is a reading only when it is
  a number, not urgent and not marked as something that happened; a flask drunk, refilled or arrived, a buff begun or
  ended and rage full or empty are marked. The level never makes a reading.
- **A worn weapon's first focus said twice** (found buying gear). **Fixed and heard:** an item its tooltip names waits
  up to 300 ms for a tooltip not up yet (`TooltipSettle.NameWaitMs`; a normal item never waits), and a rare or unique
  item is named from its own words at once. One line on each set: "Mano principal, Filo quimérico, raro, Build: 0 de 3
  modificadores. …" and "Segundo set de armas, mano principal, Lanza de guerra dentada, mágico, Build: 1 de 3 …".
- **The menu A opens beside another player had no word that it is a player.** **Fixed and heard**
  (`TheMenuOnAPlayerSaysItIsAPlayer`; said when a player of that name is in the area): "Jugador: Tejetormentas Niv. 27,
  Liga Forbidden Rites, Bleidz. Invitar al grupo, 1 de 5", and again by accident when a player stepped onto the stash
  ("Jugador: Monje Niv. 26, Liga Forbidden Rites, sanllee. Invitar al grupo, 1 de 5"); closed with B both times.
- **The status line named the last character's area at the login screen** (heard that evening: "Inicio de sesión.
  Área: La caravana de los ardura"). **Fixed** (`TheAreaIsSaidOnlyInAGame`).
- **A slider at its minimum fails its own check** (from the player part). **Fixed**, code only
  (`UiElementReader.ReadSliderState`): while no slider has proved the fields, one that reads at its minimum where they
  were measured is read there and nothing is searched; before, the search took the first place nearby with a value
  above a minimum and that number was read. **Blocked** for the live check: none of the owner's sliders sits at 0
  (volumes 78, 100, 69, 100, 100; HUD scale 100) and moving one changes his settings.

Live checks, as they stand:
- Boss fight with a lesson or the quest page on screen; the day's speed work in a fight. **Blocked:** need a fight.
- Restart with the tree left on a far node: heard on hairin, who follows no build (with a build active the build's next
  node leads instead, by design: yarkey and malgdhler). Her cursor left on "Armadura" (`config/passive-resume.txt`:
  `hairin	armour6`), the game quit and started again with the mod: "Zoom x1,0. … Evasión, asignado, Donde lo dejaste,
  Armadura: izquierda, 2. +16 a la evasión".
- Hours with NPC talk; two character creations in one session. **Blocked:** the first needs hours of play; the second
  needs the creation screen entered twice, which the patch left no time for.
- `Screen snapshot cut short`: none in the day's log (0 lines) with the tree at every zoom from x0,4 to x1,9, the shop
  earlier that day, the stash, a merchant and the salvage bench. The Atlas is **blocked:** no character there.
- The tree's first reading: `Slow tick: focus` 440 ms at the first opening of a session and 635 to 754 ms when the mod
  starts with the tree already open (three starts); once per session, the game itself is not held.
- `SettlingMemo`: the stash, the salvage bench and a merchant in turn, each title on its first line: "Alijo, pestaña 1.
  Inventario. Orbe de transmutación, 6, columna 1, fila 1, …"; "Mesa de reciclaje. Aquí puedes reciclar objetos de
  equipamiento con calidad o engarces … Orbe de transmutación, 6, columna 1, fila 1, …"; Zarka's "pestaña 1. Inventario.
  Pergamino de sabiduría, 8, columna 1, fila 2, 16 oro. …", then "4.776 oro".
- Free-cursor controls (N1) and the Skills row (N2). **Blocked:** see their entries.

### World, combat and audio

- **A boss's own life was not said when something untargetable carried its name (`40a026f`).** A twin (next form, or the
  NPC of the same name) made it "name shared" for the whole fight; sometimes nothing was said. Rule: carriers in play are
  picked among first; those set aside count only when none is in play. `TargetNarrator`, `TargetMatch.Pick`.
- **"Vendido: …", "Usado: …" and every move line of a seat were not kept (`2b16db3`).** The next focus line cut them (11
  of 13 move lines in the log had no `, kept`). Rule: `SeatOutput.Keep` takes all four parameters and `Moved` is
  forwarded; a test maps every interface call to the seat's own method. `SeatOutput`.
- **A boss that rises after "defeated" was never followed again (`651bed0`).** Rule: a boss at step 0 that reads alive
  starts over, logged `Boss risen: …`. `BossLifeSteps.Alive`, `BossWatch`.
- **The guide could let go without a word at every respawn (`f391e80`).** Rule: equal area hashes are the same instance;
  the address decides only while no hash is read. `BeaconMath.SameInstance`, `Beacon`. `ScanBrowser.InArea` left alone.
- **The guide called a Trial's room door "no longer in sight" (`c6b2561`).** The scanner never lists that door. Rule:
  only a target the scanner has listed since the lock can be gone. `Beacon.everListed`, `BeaconMath.Gone`.
- **A trap already striking was never warned of when the player walked into it (`d099e37`).** Rule: a strike is warned
  once, and only once the trap is within 400 units. `TrialTraps` (`TrapStrikes.Update`), `TrapWatch`.
- **Enemy sounds could judge sight on the previous area's walls (`101f053`).** Rule: the sight layer is keyed by vector
  address and the area instance's hash. `EnemyCues`.
- **One empty or damaged WAV stopped the mod at start or hung it (`55848fa`).** Rule: a negative chunk size, a short
  format chunk or zero frames is no sound. `GameSounds.ReadChannels`.
- **One value that is no number silenced every sound until restart (`e50dea9`).** A NaN pan or gain stayed in a bed for
  good. Rule: a pan, pitch or gain that is no number is not mixed. `AudioCueService` (`IsSound`, `Bed.Set`).
- **A road sign tapped like a character (`5b88693`).** Rule: no sound layer for an entity whose minimap icon is
  `StoryGlyph`. `EnemyCues.LayerOf`.

Events spoken under a "reading" kind that could be dropped (finding 17 of this report) were fixed by the interface
part's `61c774c`. Tests: `WorldCombatAuditTests.cs` (6); none for `101f053`, `5b88693`, `55848fa`, `e50dea9` (classes
the tests do not link). The sentence of this document saying `SeatOutput` does not forward `SpeakReading` was out of
date and is dropped.

Left open, settled in the fifth pass (2026-11-10, evening):
- **A boss with forms is said defeated at the end of its first form, and its room marked** (12). **Blocked:** needs one
  fight against a boss with two forms, watched in the log (`Boss risen:`, the catalog's row for the second form); the
  bosses are the owner's to fight (AGENTS rule 25).
- **Exit tins flap to "open" and back** (13). **Fixed**, unit tests only (`AnExitHeardIsNotLostToAFlap`,
  `RadarSoundsConfirmedChangesOnly`): an exit once confirmed stays an exit for `RadarSensor.ExitHoldMs`, 1.5 s (30
  polls), after the ray last saw it. The price: a true change from exit to wall or open is said 1.5 s later. The
  owner's to overrule by that one constant; to hear near any area exit.
- **"Se abrió el paso" may be said for a barrier still closed** (14). **Blocked:** needs a closed barrier walked away
  from until it sleeps, and back (two screens), outside a town.
- **A boss's growl stops while it cannot be targeted** (15). **Blocked** by the owner's choice between: (a) silence, as
  now: no growl means nothing to hit; (b) the growl goes on, quieter, so the boss is still placed while it cannot be
  hit. Recommended: (b) only if he finds bosses hard to place again after a phase.
- **The loss of the output device is neither seen nor logged** (16). **Fixed** as a log line, code only
  (`AudioCueService`: once when `waveOutWrite` fails, once when it works again); whether the cues come back after a
  replug is **blocked:** needs a person to unplug and plug the headset with the game open.
- **`ObjectSounds` returned when no game sound was extracted.** **Fixed**, code only: the Diablo IV loops and the quest
  ring play without the game's own sounds.
- **`MapPlaces.Assign` named no checkpoint past 50,000 pairings.** **Fixed** (`MapMarkersFindTheirCheckpoints`, 16
  shuffled checkpoints): past the budget each marker takes the place in the nearest direction, each place once, within
  the exact fit's 23 degrees (`MapPlaces.Greedy`).
- **`RoutePlanner.workspace` kept about 14 MB per pool thread.** **Fixed**
  (`SearchWorkspacesKeptBetweenSearchesAreBounded`): searches rent from one pool that keeps two workspaces at most.
- **Game names with no check when a patch renames them** (trap states, the room door, enemy-state buff ids and
  animations, `ActiveBlockChannel`, `Tail`). **Blocked:** needs each name looked up in the game's files after a patch
  (`catalog/game-keys.txt` holds table keys, not these names); a check that names them all needs a list from the
  extractor, which is not built.
- **Rules in host-bound classes with no test** (`Beacon.CollectSearch`, `BossWatch.WatchLife`, `TargetNarrator.Tick`).
  **Blocked:** each reads the host's entities directly; a test needs the rule lifted out of the class, not done in this
  pass. The Beacon's new phrase choice is tested (`WayCuts.Of`).
- **The guide on a closed barrier that opens says "ya no está a la vista" beside "Se abrió el paso".** **Not a fault:**
  both are true (the barrier as a target is gone because it opened); two short lines, in that order.
- **The half second before "derrotado" equals the host's 30-frame dormant latch.** **Blocked** by the owner's choice:
  `DefeatConfirmMs` 500 (now) or 1000 (covers the latch, the line comes half a second later); needs his ear in a fight.
- **`StaleValueTests` pins "another instance object with the same hash forgets the scanner's choice".** **Not a
  fault:** the scanner is left out of the respawn rule on purpose (`ScanBrowser.InArea` was left alone in `f391e80`);
  the test says what the code does.
- **The Beacon's "way stops short" when the search only ran out of budget** (the Open table). **Fixed**, unit tests
  only (`ASearchThatRanOutOfBudgetSaysSo`, `TheWayIsSaidClosedOnlyWhenItIs`): see the table.

Live checks, as they stand. **Blocked**, all of them, by what the unattended session may not do: a boss fought through
its forms; a death and respawn with the guide locked; a sale ("Vendido: …" kept); a Trial; a door opened and the far
side scanned; a blow from a chilled or hasted rare; `Frames:` in a boss fight; a barrier left and come back to; a
replugged headset; a road sign. The first seven need a fight or a sale, the owner's; the barrier and the sign need a
walk outside a town.

### Player, items, passives, layout and core

- **The navigable flag bit was moved on a healthy game (`fb1a6cc`).** A session begun in the passive tree sampled
  hovered nodes as focus, and from then until restart panel notes, control grouping and slot readers used bit 0x12 (in
  the log twice in two days). Rule: only an element the manager's focus slot holds that is neither hovered nor a passive
  node is sampled; another bit is taken only when a second round of twelve other elements names the same. `FlagBits`,
  `MovedBit`, `Poe2AccessibilityCore.InFocusSlot`.
- **A buff that runs out or holds a count was silenced for the session after three losses (`cc9275b`).** Tailwind, a
  warcry. Rule: only a buff with no duration and a single stack can be a held button's effect; one that runs out
  (`SteadyBuffs.RunsOut`: positive and finite) or holds more is told every time; a buff first seen in the baseline is
  not held back; the quiet list is forgotten at character selection; a settled count equal to the last one told is not
  said. `SteadyBuffs`, `ResourceWatch`, `ResourceTracker`.
  Heard in the first fight (owner on a Druid, 2026-11-10) and kept on purpose: a held Volcano says its stage as it
  grows ("Volcán: 2", "Volcán: 3", "Volcán se acabó"), and he uses it to know when to let go; "Altar de ritual,
  completado, A" and a ground item's name each time he walks over them, and the energy shield's thresholds, do not
  bother him. None of the three is noise to remove.
- **An accepted trade said nothing, or half (`4ff3695`).** One for one was taken as "the inventory's own item in a new
  place"; two for one swallowed a departure. Rule: in a trade an arrival is the same item only under the same name or
  when its departure never read; otherwise "Obtenido" and "Entregado". `ItemMoves`, `InventoryWatch`.
- **A trade's other side was read only while the cursor was in the player's own trade grid (`2bc136e`).** Rule: the
  window is watched from the grid it was last read beside while the cursor is elsewhere and the grid shows; an offer read
  empty from afar waits for the cursor's return; a grid no longer shown closes the trade. `TradeOfferWatch`.
- **One failing part could silence the whole mod or every menu (`2feff8c`).** The host drops the rest of the frame when
  `DrawUI` throws; a failure in one of the thirty resets lost the area's name. Rule: every part of the frame runs in
  `Isolated`. `Poe2AccessibilityCore` (`DrawUI`, `NarrateScreenChanges`, `OnAreaChanged`, `TickPlayerNews`).
- **A passive tree catalog older than the game was said over the live rows (`a67828e`).** Rule: the first 64 nodes decide
  as before; once most agree every node's row is compared and a node whose row differs is read from the row; while most
  differ the files stand. Costs two more reads per node when the map is built. `UiSemantics.PassiveTree`,
  `PassiveTreeFields`, `CatalogCrossCheck`.
- **Rules that wait for "a second area" keyed the area by the instance's address alone (`24edb3f`).** When the game
  reuses the `AreaInstance`, poise, area level, the absent in-game layer, cooldown entries and the sweep never saw a
  second area. Rule: an area is the instance's address and hash; an instance with no hash read is not swept and is no
  witness. The stack-count invariant now judges the count alone, because its maximum came from one Scroll of Wisdom.
  `LossWitnesses.AreaKey`, `WorldLayoutWatch`, `SkillCooldownWatch`, `OffsetHealer`.
- **Plugin offsets survived into the next game process (`f71081a`).** After a patch and restart under the same host the
  new build's reading was refused. Rule: every plugin offset of `LayoutFields` goes back to its compiled value at each
  new game process; an override naming a plugin offset that does not exist is logged, not dropped in silence.
  `Poe2AccessibilityCore.Compatibility`.
- **The gold counter was searched for at every poll (`88c82e0`).** Up to 4,000 elements, four times a second, while not
  found. Rule: a failed search waits 1 s; an area change searches at once. `GoldWatch`.
- **"Game states" lost was never taken back (`4bc8f0c`).** A state read afterwards records the part as found.
  `GameStateService`.
- **A buff list whose first id has a capital failed its shape (`0a3834d`).** Rule: the first id is judged as `IsBuffId`
  reads one, three characters at least. `StructShapes.IsBuffList`.

Tests: `PlayerItemNewsTests.cs` (10: buff rule, settled count, trade moves, moved bit, node rows, area key, buff list).
None for the trade window watch, the gold backoff, the frame isolation and the offset restore (they read through the
host).

Left open, settled in the fifth pass (2026-11-10, evening):
- **A trade item swapped for one that reads the same is not noticed**; **`TradeOfferWatch.Open` is true only after the
  cursor has been in the trade grid once.** **Blocked:** both need a trade window with a second player (whether the
  other side's item entities are stable; the window recognised without the focus).
- **A flask's cost learnt in a fight** (`FlaskDrinks`). **Blocked:** needs two drinks in a fight with kills between
  (`Flask cost:` against the tooltip's "Consume N").
- **A quiet button buff stays quiet for the character's session**; **after a respawn every buff, charge and ailment
  held at death is said as ended.** **Blocked** by the owner's choice: (a) as now; (b) nothing held at death is said as
  ended at the respawn (he knows he died), and a quiet buff is told again after an area change. Recommended: (b) for
  the respawn, (a) for the quiet buff.
- **The dev command file is read whatever `ResearchMode` says.** **Not a fault:** only someone who can already write
  into the mod's own folder can use it, the commands read and speak and never act on the game, and the unattended
  tests depend on it.
- **`ChargeWatch` has no grace after an area's first reading.** **Fixed** (`BaselineGrace`,
  `TheWindowStartsAtTheFirstReadingAndEnds`): for 1.5 s after an area's first reading, charges and Rage are the
  baseline, not news.
- **An unnamed item is described again at every poll** (`InventoryWatch`, `ItemReadings.IsStale`). **Not a fault:** it
  is a few reads per unnamed item four times a second, and it is how the item gets its name once the game fills it;
  a rare or unique item's name from its words is kept per Mods component (`InventoryReader.wordNames`).
- **`SkillStatSets.keyOffset` is found once per host.** **Fixed**, code only: when the known offset no longer names the
  table it is searched for again and replaced.
- **A real loss judged only on things that come and go can stay unsaid** (`SecondLook`); **nothing is gated while a
  part is suspected or lost.** **Not a fault**, by design: a part is said lost only on evidence that cannot be a thing
  leaving the screen, and a suspected part keeps reading because silence is worse than a doubtful line; each lost part
  is said once (`Announcements.Compatibility`).
- **A slider at its minimum fails its own check.** **Fixed**, code only; see the interface part.
- **`GameDataSetup` regenerates catalogs only when the launcher starts.** **Fixed** as a log line; see the host part.
- **`AnUnreachableGoalStaysWithinTheFrameBound` failed once under load.** **Fixed** with the other wall-clock test
  (the best of several runs, and the expansions counted).
- **Tests that do not exist** (`WorldLayoutWatch` inline counters, `LayoutProbe`'s two looks, `InGameFields`' absent
  rule, `PlayerSkills.JudgeNames`, `GameStateService.StatesOverdue`, `FlagBits` rounds beyond `MovedBit`).
  **Blocked:** not written in this pass; each class reads through the host, so each needs its rule lifted into a pure
  class first (`StatesOverdue` is already static: it needs only a `Compile` line and a test).
- **A rare or unique item named by its base where no tooltip was read** (ground labels, moves; from the stale-values
  pass). **Fixed and seen live:** the item's own words name it (`ItemNameWords`, `GameLayout.ItemNameWords`: the
  Words.dat keys at Mods +0x18; unit tests `ItemNameWordsTests`). With no tooltip read since the mod started, dev
  `inventories` named all eleven worn pieces ("Filo quimérico, raro", "Égida de glifo, raro", "Estallido repentino,
  único", "Trenza de medianoche, único", …); then each focused slot's title agreed (dev `layout`: "item names from
  words ok (56 titles agree, 0 differ)"). A rare's two words are put in the order their own space gives, so a language
  written without spaces keeps the base name; a magic item keeps no words and is still named by its base until its
  tooltip is read. Ground labels use the same call and were not seen (no item on the ground in a town).

Live checks, as they stand:
- `TotalTime` of a buff with no duration, the parry buff; the flask cost in a fight; gold piles in two areas; a reused
  `AreaInstance` address; item-granted skills' names; the lines after a death. **Blocked:** need a fight or areas
  outside a town.
- A player trade end to end. **Blocked:** needs a second player.
- A new game process begun in the tree: no `navigable flag moved` after the fix (`fb1a6cc`) in that day's log, with
  three mod starts under an open tree that evening; a fresh game process begun there was not tried.
- The game restarted under the running host: on that evening's patch the mod was started again with the game (`Code
  fields: 52 of 52 read in 522 ms; moved: none`, `Semantic fields: 80 of 80`); a host left running through a game
  restart was not tried, and now says so if the exe changed (see the host part).
- Options with a slider at 0 focused first. **Blocked:** see the slider entry of the interface part.

### Closed after the pass

Fixed afterwards by the director, from the "for others" and left lists of the reports.
- **A trial's trap read once a second.** An untyped entity that lives only under the monsters' path was read once a
  second, so the dodge cue for spikes could come late. Rule: it rests like other untyped entities.
  `EntityRefresh.Rests`, `Entity.cs`.
- **The launcher said "Game data ready" when steps had failed.** Rule: it says how many were not updated.
  `StartupPhrases.Partial`, `GameDataNarrator.StepFailed`. (The silence while one step hangs is still open.)
- **`TimedGuide` kept an entity id across instances.** Rule: `TimedGuide.Reset` is called from `ResetAreaState`.
- **`PlayerAlerts.Tick` ran its watches with no isolation.** Rule: each runs whatever the one before did, and the first
  fault is thrown on after all ran.
- **`ResearchMode` defaulted to true.** A deleted or unparsable settings file meant a UI dump per screen and a growing
  research log. Rule: off unless the settings file turns it on. `AccessibilitySettings`.
- **The `docs/patch-day.md` override example named an offset no override can move.** It now names `layout:SkillLevelKey`.
- **"1 puntos" and "1 párrafos".** Rule: `Announcements.LongText` and `Announcements.BuildProgress` have singular forms.
