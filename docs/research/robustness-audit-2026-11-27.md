# Robustness audit (2026-11-27)

A read of the whole plugin after the 500 commits since the audit of [2026-11-10](robustness-audit-2026-11-10.md)
(league mechanics, boss warnings, the Atlas): four readers by subject, one of the layout constants added since, a
mechanical search for repeated code, and the play log of 2026-11-27 beside them. What was fixed is first; what was left,
and why, is last. The owner's report that started it: "tiré 2 items uno seguido tras otro; uno dice que lo he usado o
equipado en vez de tirar, el otro sí se narra bien, y así con otras acciones".

## Item moves

The log showed the wrong lines came with the kind of item or deed, not with speed:

| Heard (log 2026-11-27) | Cause | Now |
|---|---|---|
| "Usado: Guantes sombríos de la lamprea, mágico" on a Y drop (9 of the day's 16 "Usado") | The name "by base" kept for a titled item was read with the tooltip title the reader remembers, so the ground item (a new entity, read by its base) never matched | `InventoryReader.DescribeUntitled`: the item as its new entity reads |
| "Equipado: Traje de hatungo, mágico" for the Traje de hatungo del poeta del pingüino | The same: the arrival could not take its departure's title | the same |
| "Desequipado: Corona del Rey pálido. Equipado: Corona del Rey pálido, Casco" on an orb used on the worn helmet | The stack the orb came from is a new entity in the bag, and took the helmet's departure for an unequip before the helmet's own arrival was looked at | `ItemMoves.Read` pairs an item made anew where it is worn before anything else (also a flask drunk as an item is picked up) |
| "Movido a Selecciona una recompensa: Colmillos helados" | A quest item turned in as its reward window opens | No panel keeps a quest item: "Entregado" (`ItemMoveKind.HandedOver`) |

Made for quick hands, each with a unit test in `PlayerItemNewsTests`:

- A batch (the moves seen until the inventories are still for 300 ms) can hold two deeds. An arrival in the hand or a
  slot used to silence every departure of the batch; it now takes the departure of its own name (else the one never read,
  else the only one) and the others are said: thrown and picked up, sold and equipped. When several are left and none
  carries its name, none is said, as before.
- A Y drop passes through the hand: an arrival there whose entity is gone before the batch settles is no move, so the
  batch is the drop alone (it was silent whenever a poll caught the hand).
- The panel beside is the one open when the move showed, not when the batch is said 300 to 500 ms later (a stash closed
  at once made its moves "Usado"); a trade or the favours open then count too.
- The hand's item is "from the panel beside" when nothing but the hand's own item left (a swap inside the stash).
- One log line per batch said or seen beside a panel: `Item moves: in [Cursor1 …], out [MainInventory1 …], gold +19,
  beside "Alijo": Sold …`. A wrong line can be explained from the log without playing it again.
- A stack split by hand is a new entity in the bag and another in the hand for one departure: the departure is the
  stack's (the hand's part would have left the rest "Obtenido").
- An item made anew in the inventory it was in (an orb used on it) is read by its old title for that batch only: its
  tooltip names it again.

Live on kehgan, 2026-11-27 (El refugio del zigurat; a town refuses drops, so the title was proved by the hand and a
slot): the identified magic amulet picked up and set down logged `in [Cursor1 Amuleto de piedra de sangre reforzado del
caleidoscopio, mágico]` and said nothing; X on it said "Desequipado: Collar de venganza, raro. Equipado: Amuleto de piedra
de sangre reforzado del caleidoscopio, mágico, Amuleto" (and back); one Regal Orb split off its stack and put back logged
`in [MainInventory1 Orbe regio; Cursor1 Orbe regio], out [MainInventory1 Orbe regio, 2]` and said nothing. Not heard
live: "Tirado" on an identified magic item (it needs an area that takes drops).

Not changed: how a sale's gold is measured (two polls before the batch against the moment it is said); a number that
changes after A is still said at most every 1.5 s (`NumberChanges`: a new press could let a running counter chatter);
the shop's balance is said once it rests (`ShopBalanceWatch`).

## One fault no longer silences the rest

- The speech worker's loop (a throw outside `Execute` ended the host), each sub-watch of a boss fight's poll (its lines
  were lost and `others` grew), the audio mixer (one voice or bed that threw stopped every sound), a layer of the screen
  snapshot (the whole screen went silent; its last capture stands in), the focus tracker's subscribers, one entity of the
  scanner's listing, and in the core: the partner's block, the pad's finders before the gesture machine, each step of a
  game state change, the layout sweep before the compatibility line, the narrator's tick and each screen change.

## State that outlived its area, fight or press

- A place build or a shape build finished after the area changed and was published into the new one (`PlaceNarrator`,
  `EntityCatalog.Clear`); the beacon kept the last turn and route flags across a new lock; the lost hint and the hint
  trails compared the first poll with the last area's place; a boss effect after five quiet seconds was taken for the
  fight's first poll and never placed (`BossEffectWatch.baselined`); the throw watch read a boss gone with its area; the
  Chaos trial kept its plates across rooms; two rares of one base on the ground took the first one's name; an area with
  more than 64 stat pairs read as none (512, with the refused count logged).

## A game update that moves a field

Everything added since 2026-11-12 fails quiet except what follows, which would have been believed:

| Field | Guard now |
|---|---|
| Atlas node status byte and grid (`AtlasNode`) | `AtlasState.Plausible` judges each scan (cells are small numbers, some node is accessible, a completed one is accessible too once four are completed) and reports the part "Atlas nodes" after two scans that read wrong; the scan's census is logged (live, kehgan: `542 nodes, 368 catalogued, status bits 0:362 1:4 2:0 3:2, 368 cells in range`) |
| `MinimapIconRow.PointerDistanceOffset` | In `DatColumns`: judged by its table's row size |

Open, each quiet when wrong and nothing says so: the MinimapIcon component's hidden byte, the league rulesets' inner
fields, the fog reader when it never comes to be trusted, the Atlas node's dat keys (not through `DatKeySite`), the UI
names of the dialogue speaker, the Ritual buttons and the cinematic skip. Boss and league pieces typed in code now log
once when a fought boss or a matched piece lacks what its rules read (`BossThrowWatch`, `BossMazeWatch`, `LeagueWatch`).

## Repeated code

A search for runs of the same code lines across the 623 source files found two of seven lines or more: there is little
copied code. Folded: the walk of `panels_parent`'s shown panels (`UiSemantics.ShownPanels`: shop page, side panel, world
map), the Atlas's "nearest first" ranking written three times (`AtlasNearestFirst`), a row button's walk
(`QuickButtonNodes`), the poise judge's actors, the passive tree's two vector reads, `StateOf` and the "log each distinct
line once" helper (`StateReading`, `DistinctLog`), the map overlay's search (`FindNamed`), the "look up at most every
250 ms" caches of the core (`ThrottledLookup`), `PlayerAlerts`' six try blocks, `InspectLayout`'s element names. Split as
pure moves: `EntityCatalog.Classify` (880 lines) into `.Containers`, `.Targeting`, `.Predicates`; the passive finder's
research report. `InventoryWatch` keeps what it knows of an item in one record (`Seen`) instead of three dictionaries.

## Cost

No per-frame work was found that mattered; removed anyway: `File.Exists` on every tick for the command file, a list per
tick in the gesture machine, `PanelOverGame` walked several times a frame, the deployed watch's sets and path checks, the
league watch's rule lookup and log line per entity per poll, a regex per HUD text in a Trial, a log line built for a
layout part already known.

## Reviewed before it shipped

A second reader went over the whole change against the code before it. Two regressions were found and removed: the
split stack above, and a warning mentioned and not yet heard that was dropped whenever anything stopped the speech (a
panel closing, a cinematic), not only the player. Two savings were taken back as not worth a doubt: the options screen's
disabled row read at the value poll's pace (a row left behind could be said), and the flood watch keeping its first lava
object (a second one would never be followed).

## Left as it is

- The poll throttle (`now - last < Ms`) is written in some thirty watchers: an idiom, one line each; a helper would touch
  them all for nothing heard.
- One shared pass over the awake entities for the forty places that walk them: each has its own pace and its own filter,
  and the frame's cost does not ask for it.
- The screen differ's two-snapshot confirmation and the narrator's 1.5 s repeat guard drop a value that comes and goes
  between two snapshots: deliberate, and every screen line passes through them.
- Phase news of two bosses fought at once shares one counter and one 4 s merge window, and danger words one 800 ms gap
  (`BossMechanicsWatch`): changing either changes what is heard in every fight, and needs a fight to be judged.
- A tooltip's settle clock per item (so alternating between two cells keeps each one's clock) was tried and taken back:
  a tooltip that fills again on return could be read half filled.
- A fork narrower than a walk between two polls cannot be stepped over at walking speed (the entry window is the node's
  radius plus two cells on each side against some nine cells a poll).
- `OnEnable`'s 480 lines of wiring: its order is the dependency order; not split without a live session on it.
