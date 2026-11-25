# Atlas navigator: orientation, finder and node state (plan, 2026-11-25)

Plan only; nothing here was measured live at the Atlas yet. Builds on [endgame.md](endgame.md) (the endgame's data,
offline) and [endgame-ui.md](endgame-ui.md) §3 (GameHelper's atlas-node offsets). Confidence words as in endgame.md:
**PROVEN** = read from the code or the game's files; **LIKELY**; **UNSURE** = to settle in Phase 0.

## 1. The owner's word (2026-11-25)

- The sole named pain on the Atlas: **losing one's place** — not knowing where the cursor is on the graph or what is
  around it. Not named: silent hover, nodes being hard to land on, missing node state, over-verbose readout.
- Movement: **the stick hops node-to-node**, as on the passive tree (not a free cursor, not stick-pans-map).
- Priority: **the finder/navigator first**. No map-device pain was named, so device work is deferred (§7), not
  declared fine.

## 2. What the code does today (PROVEN, the code)

- **Hover-only, passive readout.** `UiSemantics.Endgame.DescribeEndgameNode` speaks a node only when the cursor is
  already on it and the element resolves through the main tree's record chain (offset searched ±0x200 around it,
  learnt per element class, trusted only when the PassiveSkills id is a catalogued endgame node): the popup's title
  (else the catalog's one map / biomes / "Atlas node"), "Holds: …" from content-icon art, the popup's rest, the
  catalog's objective / hint / blocked text, link directions (`AtlasLinks`, clockwise from up, from drawn centres
  else graph places), tower reach. Role `Panel`; `TooltipPending` while the popup is unbuilt.
- **No orientation.** Nothing says where the node sits in the 610-node graph, where the player is ("you are here"),
  how far anything is, or how to get anywhere. No node state from memory (accessible / completed / failed): only
  what the popup text happens to say. No resume of the last node (the passive tree has `PassiveResume`), no hop
  breadcrumb, no "what changed" after a completed map.
- **No finder.** The passive tree's D-pad finder and spoken stick route (`UiSemantics.PassiveFinder`,
  `PassiveStick.Route`, `WayTo`) have no Atlas equivalent. `EndgameCatalog` already holds every node's links, pin
  kind, maps, biomes, objective, contents and tower reach — the graph a finder needs is on disk, unused for travel.
- **Map device: no dedicated code** (no match for the receptacle, its warnings or `ConsoleActivateMapDevice` in the
  plugin). Generic focus and item-tooltips only: endgame.md §4.4 "LIKELY works", never swept live.
- **Host gap.** This repo's `host/GameReader` holds only the Atlas panel address (`{22,0,6}`,
  `ImportantUiElements.cs:43`, likely stale in controller mode — endgame-ui.md §5 found the panel by its name
  `WorldScreen`). Upstream GameHelper's node reader was never ported (§3).

## 3. Two models of an Atlas node — Phase 0 picks one

| | Model A: skill-graph node (the shipped code) | Model B: GameHelper's `AtlasMapNode` (endgame-ui.md §3) |
|---|---|---|
| Element | A passive-tree-like node element whose record points at a PassiveSkills row | Child of the Atlas panel, flags fingerprint `0x542EF3` (mist `0x442EF3`, you-are-here `0x502EF3`) |
| Identity | The row's id → `EndgameCatalog` node (graph `endgamemaplayoutgraph`) | Map data row `+0x290`, content name `+0x278`, grid position `+0x310` |
| State | Not read | Biome byte `+0x2BE`, **status byte `+0x2BF` (bit0 accessible, bit1 completed)**, connections vector `+0x590`, content tokens `+0x350`, badges `+0x368/+0x370` |
| Risk | Written offline, UNSURE live; the 0.5 Atlas may not draw nodes this way at all | Measured upstream, possibly before the 0.5 Atlas redesign; every offset must be re-validated here (AGENTS 7–9) |

They are not exclusive in outcome: the finder's graph comes from the catalog either way. What differs is where
identity, drawn position and **state** are read from, so no finder code is written against A until Phase 0 shows A
firing live.

## 4. Design rules for every phase

- The guide **speaks directions; it never moves the cursor or presses anything** (AGENTS 4, 14) — exactly the
  passive finder's contract.
- Speech is event-driven and snapshot-diffed (AGENTS 11–13): on Atlas open, on a hop settling, on a state change.
  No per-frame polling speech; node states are read when the Atlas opens or the hovered node changes.
- Distance is **links** (stick hops), never screen pixels; direction is the drawn side, via `AtlasLinks`.
- Memory layout stays in the host / `GameLayout`; accessibility logic in `Poe2Accessibility/Services/` (AGENTS 5).
  Pure graph logic (BFS distance, routes, list building, diffs) goes in a testable class beside `PassiveMap` /
  `PassiveStick`, unit-tested in `tests/Poe2Accessibility.Tests/EndgameTests.cs`.
- A node's spoken name may not exist before its popup (its map is drawn at random). Finder entries then fall back
  to pin kind / biomes / contents, as `EndgameCatalog.NodeName` already does for the readout.

## 5. Phases

### Phase 0 — live evidence sweep (checklist in §6)

Settles: A vs B; the status byte against nodes of known state; where "you are here" lives; whether the D-pad is
free on the Atlas; how pan/zoom move drawn centres (`EndgameCentres` cache validity); one Atlas-tree node; the map
device window's tree (capture only). Output: this file's §6 results filled in, raw dumps/log kept (AGENTS 16–17).

### Phase 1 — node state and the anchor

Port the validated fields (provenance + validation method documented per AGENTS 7). The readout gains state first —
name, then completed / accessible / locked with the catalog's blocked text — and the you-are-here node is
identified as the orientation anchor. Fallback if B's fields moved: the pin's art or a `MiscAnimated` key
(`EndgameMapPins` Available / Active / Failed / Complete), found with `datkeys` in Phase 0.
**Proof:** spoken state matches the popup on every sampled node kind.

### Phase 2 — orientation (the owner's pain; ships before any list)

- **On Atlas open, once:** "You are at X" plus the reachable frontier in links ("Accessible, not completed: A,
  2 links up-right; B, 3 links left"). Needs Phase 1; without state it degrades to the you-are-here name and its
  named neighbours.
- **Per hop, brief:** name + state + one anchor line — nearest of {you-are-here, a tower, a citadel, a hub, the
  start} in links and side. Full contents/objective stay for when the cursor settles (the `PassiveGuideBrief`
  pattern; setting beside it).
- **Breadcrumb:** "came from Z, down-left" from the hop history. No offsets needed.
- **Resume:** last hovered node per character (a `PassiveResume` sibling file); on reopen it is named, with its
  link distance and side from the cursor.

### Phase 3 — finder and guide

D-pad lists (Phase 0 must confirm the D-pad is free; `UiSemantics.AtlasFinder`, routing as in
`Poe2AccessibilityCore.Input` / `ModCommands` for the passive finder), each ordered by link distance from the
cursor's node: **Position** (Phase 2's summary, the home list) · **Accessible, not completed** · **By content**
(the content-icon catalog) · **Towers, citadels, hubs, uniques** (pin kinds) · **Objective nodes** (corrupted
nexus and kin). Left/right steps entries, up/down steps lists, as the passive finder does.

Selecting an entry sets a guide target: the shortest link route is computed over the catalog graph; every hop then
says the next stick direction and the links left; leaving the route re-routes silently; arrival speaks the full
node readout. The target survives opening the node's device window, so the device inherits its name even before
any device work.
**Proof:** endgame.md §6.3 over a whole route — push the stick the way each said link lies; the cursor must walk
the spoken route to the target, at two zoom levels.

### Phase 4 — reopen diff

Per-character snapshot of node states, taken when the Atlas closes. On open, before anything else: "Since last
time: X completed. Now accessible: Y, Z." **Proof:** complete one map, reopen, the diff matches.

### Phase 5 — deferred (no pain named; captured in Phase 0 only)

Map-device task summary (selected node, waystone tier vs the node's minimum, Activate readiness, `MapDeviceWarning*`
surfacing, post-activation confirmation), the tower tablet window's slotted effects, the Atlas passive tree's
allocation state and finder. Each re-enters when the owner names the pain or Phase 0's dump shows the generic
reader failing there.

## 6. Phase 0 capture checklist

Roles: the owner stands at the Atlas and hops; the agent writes `Plugins/Poe2Accessibility/debug/command.txt`
(it is polled every 500 ms, one lowercase command per line, deleted on read — `DevCommandFile`) and reads the log.
No key presses, so the game stays in controller input. Everything below that cannot be captured in one visit is
written down as still open, not guessed.

**Setup**

1. Settings: `ResearchMode` on (the command file answers), `VerboseLogging` on (`Hover without tooltip` lists
   atlas/device controls that say nothing — STATUS, audit N1).
2. Open the Atlas. Do nothing for a few seconds: record what, if anything, is spoken on open today.

**The panel and one node of each kind**

3. `dump visible` → `debug/ui-tree-InGameState-<timestamp>.txt`. From it: the Atlas panel's real path/name in
   controller mode, its child count, whether node children carry the `0x542EF3` / `0x502EF3` / `0x442EF3`
   fingerprints (model B) or sit as tree-canvas groups (model A).
4. Hop to each kind, wait for the popup, let the readout finish, and note the popup's own words about state before
   running, in order, `atlasnode`, `datkeys`, then `hex 0x<the @address atlasnode logged> 0x800`:
   - the **you-are-here** node;
   - a **completed** node;
   - an **accessible, not completed** node (the frontier);
   - a **locked** node beyond the frontier;
   - a **tower** node; a **citadel / unique / hub** node if one is within a few hops (skip otherwise, note it).
   Per node the log must hold: `Atlas node: @… class … record +0x…: <id> graph … pin …` (A proven, and at which
   offset) or `no endgame node` (A dead — B becomes the only road); the `Dat key:` lines (a pin/state row key?);
   the hex block to test B's bytes: `+0x2BE` biome, `+0x2BF` status bit0/bit1, against the state the owner knows
   that node to be in. **The byte is trusted only if it agrees on every sampled kind.**
5. One link check: from any node, push the stick the way its readout says a link lies; record whether the cursor
   lands on the named neighbour (endgame.md §6.3). Repeat after zooming once (which control zooms? owner's word).

**Inputs and edges**

6. On the Atlas, press each D-pad direction once; the owner says what the game did (nothing / pan / focus move /
   quick-travel). This decides the finder's controls in Phase 3.
7. Atlas passive tree: hover one node, `atlasnode` once (settles whether its nodes take the same chain and graph).
8. Map device (capture only): open it empty, `dump visible`; put a waystone in, `dump visible` again; hover the
   waystone and Activate, note what is spoken today. Do not activate for the test.

**Results** _(fill in during the sweep; raw `ui-tree-*.txt` and the log stay in `debug/` + `logs/`, the decisive
excerpts are copied here)_

- Model A live? Record offset learnt:
- Panel path/name and child fingerprints:
- Status byte `+0x2BF` vs known states (per sampled node):
- You-are-here: element / fingerprint / other sign:
- D-pad on the Atlas:
- Zoom control; centres hold under pan/zoom?
- Atlas tree node chain:
- Map device: slots/Activate spoken as today:

## 7. Risks and fallbacks

- **B's offsets predate the 0.5 redesign.** If no byte validates, Phases 2–3 ship catalog-only (anchors, routes,
  pin/content lists, resume, breadcrumb) and state-dependent lists wait for a pin-art or `datkeys` lead.
  Orientation — the owner's pain — does not depend on state.
- **A is dead and B's identity is a map row, not a PassiveSkills id.** Then drawn nodes must be matched to catalog
  nodes by grid position (`+0x310`) or drawn centre ↔ graph place. Phase 0's dump + hex must contain enough to
  test the match on the sampled nodes before any finder work.
- **D-pad taken.** The finder moves to whatever Phase 0 finds free (the passive finder's routing is input-agnostic
  below `ModCommands`).
- **TTS spam while hopping fast.** Phase 2's brief line only on settle, current readout cut by the next hop —
  the same discipline as the passive guide; judged by ear live, constants beside `PassiveGuideBrief`.
