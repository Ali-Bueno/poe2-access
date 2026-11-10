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

| Where | What can go wrong | Smallest fix |
|---|---|---|
| `UiSemantics.PassiveTree` (the stick step) | The multiple-choice redirect (`row+0x8B`, `elem+0x648`) is not modelled: the first push after an attribute popup lands one node off | Needs the redirect read from the game first; such pushes are not judged meanwhile |
| `Beacon` | A search that spent its budget ends as a partial way and is said as "the way stops short" | `RoutePlanner.FindRoute` would have to say the budget was spent; and a phrase for it |
| `EntityCatalog.Classify` (`ObjectNames.FromPath`) | An object no table names is said by its file's words, in English ("Chair"). It is already the last resort and logged | Marking it as unnamed outside English needs a new phrase in eight languages: the owner's call |

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
