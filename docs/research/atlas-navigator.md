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

### Phase 1 — node state and the anchor (state speech BUILT 2026-11-25; anchor still open)

Built and deployed to the play copy, awaiting the owner's ear: `AtlasNarrator` watches the selected node
(`map_control +0x2E0` via `UiSemantics.SelectedAtlasNode`); when it settles (~700 ms, popup built or 2.5 s
given up) the landing is spoken as name, state from `+0x32F` (`AtlasState.Of`, bits only), contents and links
(`UiSemantics.AtlasNodeLine` over the shipped endgame readout; setting `AnnounceAtlasNodes`, on by default).
Unit tests `AtlasStateReadsBitsNotEquality` / `AtlasLandingSaysStateAfterName`; whole suite green under Wine.
First ear-test (owner, ~13:20): states correct on every landing, but random-map nodes read "Atlas node" with
contents missing and a ~2.5 s wait — the map's popup is no part of the node's tooltip, so the name never
arrived (pending gave up), and canvas icons yield no texture key. Fixed the same day: the name comes from the
element's rolled area key (`EndgameNodeLabel`, neighbours via the centres scan's element map), contents from the
icon elements' own name text (`EndgameCatalog.IsContentName`), and a named node no longer waits for its popup.
In the play copy; hearing it waits on the next relaunch.
Ear-test rounds after hot reloads (same day) closed the rest: biome and contents read from the popup's own
subtree only (a whole-screen walk read the quest panel's "The Grand Expedition" as a content on every landing),
fog neighbours say "unrevealed" (revealed = drawn in the scan, not the visible flag, which also drops on drawn
nodes), and state words matched across two walks. **Phase 1's live proof is done.**
One population stays partly mute: nodes with no EndgameMapAtlas key (Rockpools, Oasis, Slash — the census gap,
1,035 canvas elements vs 610 catalog graph nodes). Grid, status, rolled area and popup words all read off the
element, so they land named and stated, but no identity or connection list is on the element (record search and
a full `datkeys` scan find no PassiveSkills key): their hops are real (Rockpools (13,18) ⇄ Slash (17,17) in one
hop, owner-walked), so their edges live in an external graph keyed by grid or the `+0x330` index. Phase 2 must
either find that graph or speak geometric nearest neighbours for them; until then they land link-less.
Not in this build: the you-are-here anchor (unresolved, §6) — it moves to Phase 2, whose per-hop brief is what
this landing line becomes.

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

Finder lists on a mod gesture (Phase 0 settled it: the game hops nodes with the D-pad itself, §6; the chord is the owner's pick. `UiSemantics.AtlasFinder`, routing as in
`Poe2AccessibilityCore.Input` / `ModCommands` for the passive finder), each ordered by link distance from the
cursor's node: **Position** (Phase 2's summary, the home list) · **Accessible, not completed** · **By content**
(the content-icon catalog) · **Towers, citadels, hubs, uniques** (pin kinds) · **Objective nodes** (corrupted
nexus and kin). One gesture pair steps lists, another steps entries (exact chords the owner's pick), as the passive finder's D-pad does on the tree.

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

**Results** _(sweep started live 2026-11-25 with the owner on the Atlas; raw evidence:
[evidence/ui-tree-InGameState-20261125-114220.txt](evidence/ui-tree-InGameState-20261125-114220.txt),
[evidence/atlas-live-20261125.log](evidence/atlas-live-20261125.log))_

- **Model A live?** The record chain works but is never invoked. `describe 0x…` on sampled node elements produced
  the endgame readout (`Panel "Ruins of Kingsmarch" … "Links: up left, Atlas node"`), so the record offset search
  resolves catalog nodes on the real elements. But on the Atlas neither the focus slot nor the UI manager's
  hovered slot ever points at a node (`atlasnode` twice: `no element @0`; `datkeys`: `scanning @0`), so
  `DescribeEndgameNode` is dead in play: a full day's log holds zero "Holds:" / "Links:" speeches. What the owner
  hears per hop is only the generic screen-text pass over the popup (`TextsAppeared layer 17: Pit | They scrabble
  and dig…`). **This is the "isn't all that useful", measured.**
- **Panel and nodes (Model B's anatomy, PROVEN):** `WorldScreen` `[0,1,17,2,3,0]` → `map_control` child 0 → canvas
  child 6 (`flags=0x502EF3`, 1,063 children) → node elements `flags=0x542EF3` (176 visible in a visible-only dump;
  the rest hidden), 27×27 for ordinary maps, larger for special pins, allocated 0x420 apart, each holding a
  `content_stack_layout` child whose icon elements carry the content's own words (`str@0x2E8="Powerful Map Boss"`).
  The popup is `WorldScreen` child 17. Both models are true at once: the B elements also carry the A record.
- **The selected node (PROVEN):** `map_control +0x2E0` and canvas `+0x408` both point at the node element the
  cursor rests on — validated by the popup: the pointed node's `+0x300` WorldAreas row is `MapPit` while the
  popup read "Pit". Node fields so far (each `datkeys`-proven on four nodes): `+0x2F0` EndgameMaps row, `+0x300`
  WorldAreas row (the map this node rolled: `MapCaldera`, `MapOrnateChambers`, `MapPit`, …), `+0x310`
  EndgameMapAtlas row (null on one sampled node), `+0x320` two i32 that read as grid coordinates
  ((3,−28), (12,−3), (8,−26), (14,4) — LIKELY, to confirm against the graph), canvas's parent chain holds the
  Acts row `Endgame` at `+0x2D0`. Upstream's `+0x290`/`+0x310` are stale by +0x60/+0x10 — trust nothing upstream
  without re-validation.
- **Identity without the record search (PROVEN):** node `+0x310` → EndgameMapAtlas row, row `+0x0` → PassiveSkills
  id, row `+0x68` → EndgameMapPins (the extractor's column, validated live). Pit = `AtlasOutsideFortressPath5`,
  the Caldera sample = `AtlasOutsideFortressPath71_`, the big 108×108 sample = `AtlasKingsmarch` (its `+0x300`
  area `MapUberBoss_FallenStar` matches the catalog node's maps, its one catalog link matches the readout's one
  link), the owner's current Bloodwood = `AtlasOutsideFortressPath36_` (catalog link `AtlasWhiteGate` is the
  readout's named neighbour "Ancient Gateway"). The `+0x320` i32 pair tracks the catalog place scaled ≈1/173 with
  y flipped (x exact on 3 of 4 with `round(x/173)+16`, y off by one once) — a grid coordinate to read directly,
  not a formula to trust.
- **Canvas census (full dump, [evidence/ui-tree-canvas-20261125-114731.txt](evidence/ui-tree-canvas-20261125-114731.txt)):**
  1,035 node-flagged children: 168 visible (`0x542EF3`) and 867 hidden (`0x5426F3`, the visible flag bit cleared) —
  fog/unrevealed is the element's own visible flag. The catalog graph holds 610 nodes, so elements outnumber graph
  nodes (connection/region elements share the flags, or nodes come in pairs — unsettled, and it does not matter
  until a scan walks them).
- **Node state (PROVEN): byte `+0x32F`, bit0 accessible, bit1 completed** — the semantics upstream documented at
  its stale `+0x2BF` (endgame-ui.md §3), at the live offset in `GameLayout.AtlasNode`. Four certified points over
  two sessions: locked **0** (The Burning Monolith, `AtlasYellowGate` — A on it did nothing); accessible **1**
  (the Ancient Gateway before completion, and Steaming Springs `AtlasNormalMapsWithDelirium7___` grid −3,11 —
  predicted 1 before sampling, A opened its Traverse); completed **3** (the same Gateway after the owner
  completed it, "Map Completed | Ancient Gateway" 12:34 — bit1 flipped on the completion event and nothing else
  nearby moved). The replicating accessible sample was the last Phase 0 unknown; it read 1 as predicted
  ([evidence/atlas-completed-gateway-20261125.log](evidence/atlas-completed-gateway-20261125.log),
  [evidence/ui-tree-atlas-after-gateway-20261125-123742.txt](evidence/ui-tree-atlas-after-gateway-20261125-123742.txt)).
  Higher bits occur on morning samples (4, 16 — attempted/sealed/special candidates), so Phase 1 reads bits 0/1
  only, never equality. Dead ends kept honest: `+0x340` is 2 on both new-build samples, `+0x33E` is per-node
  noise, there is no completion overlay element, and nodes draw no texture key the reader can see (`textures []`).
  Re-validation after a patch = this protocol (owner-certified hop + `atlasnode`), never a blind shift.
- You-are-here: **unresolved.** The canvas's last child (1062, 54×54 at (60,722)) is a button (its kid carries
  click sounds), not the marker. Next lead: whatever element shares the selected node's centre when the Atlas
  opens on the player's position, or a node whose content/art differs in a full-canvas scan.
- **Inputs (SETTLED live, the owner's test 11:50–11:59): the D-pad hops node-to-node on the Atlas** — each press
  (0x1/0x2/0x4/0x8 in the pad log) moved the selection to another node and its popup followed (Gateway, Steppe,
  Bloodwood, Rockpools, Sanctuary, Ravine). Unlike the passive tree, the game does not leave the D-pad free here,
  so Phase 3's finder cannot take it; it needs a mod gesture (the `ModCommands` chord system) or another input
  the game ignores on this screen — the owner's pick at Phase 3. **Zoom is RT** (the game's hint bar, layer 61:
  "Select [A] | Open Side Panel [X] | Action Menu [hold X] | Bookmarks [Y] | Add to Bookmarks [hold Y] | Edit
  Search [left stick click] | Zoom [RT]"). The side panel (Bookmarks, Edge Display) is ordinary focus and
  already reads. Node rects in dumps are canvas-space (far past 1920×1080), so drawn centres need the canvas
  transform, or the guide works from grid/graph places alone. One more sample meanwhile: the Ancient Gateway the
  owner rests on is `AtlasWhiteGate`, grid (8,24), sampled at 11:58; its catalog words are "Activate the
  Precursor Relay | Complete a Tower Map to access", its quest popup "Travel to the Ancient Gateway". **The
  owner certifies it not completed** ("still in my quest journal", 2026-11-25), and A on it opened the device:
  **accessible, not completed** — the first fully certified sample.
- **Map device (CAPTURED live, [evidence/ui-tree-map-device-20261125-120343.txt](evidence/ui-tree-map-device-20261125-120343.txt)):**
  A on an accessible node opens the device window over the Atlas with the inventory; the dump was taken with a
  waystone slotted (the device grid's row 1). What the mod says today, from the session's speech: "Moved to Map
  Device: Waystone (Tier 1) of Evasion, magic", the waystone's own lines in the slot ("Revives Available: 5.
  Pack Size: +6%…"), "Traverse, button", "Monster level: 65", the game's tablet tutorial ("Using Tablets"),
  and "Taken from Map Device" on removal. So the generic inventory reading carries the device better than §2
  feared; the one wrinkle on record is its focus context reading "Quests". An empty-device dump is still
  uncaptured but no longer needed to judge readability.
- **Atlas Skill Tree (REACHED and captured live, 12:56):** on the Atlas, **hold X (Action Menu radial), point
  the stick down to "Atlas Skill Tree", release** — the owner's route, "like the radial thingy"
  ([evidence/atlas-skill-tree-20261125.log](evidence/atlas-skill-tree-20261125.log),
  [evidence/ui-tree-atlas-skill-tree-20261125-125739.txt](evidence/ui-tree-atlas-skill-tree-20261125-125739.txt)).
  The tree is WorldScreen child 4 (canvas of 277 drawn groups); unlike the Atlas map, its nodes *are* the hovered
  element, so the shipped endgame reader fires as designed ("Essence Chance, 40% increased chance of Essences.
  Unallocated. Links: up, Imprisoned Monstrosities; down, The Ziggurat Refuge"). Node record at the main tree's
  offset `+0x2C8`, graph `atlasskillgraph` (`AtlasGenericSmall78_`). The tree is viewable mid-quest-line; the
  screen shows allocated counters only (0/336 overall, 0/36, 0/32… per cluster) — the unspent point count was
  not found in its texts (an allocate attempt or R3 Inspect Mode may speak it; not tried, points are the owner's
  to spend).
- The mouse-side door, for the record: an unnamed 32×32 edge button at (14,111) under the search bar (the
  depth-toggle widget class, probed fields holding `AtlasSkillTree`/`AtlasKeystones`) is focus item "1 of 3" in
  the Bookmarks side panel, but no D-pad direction from either neighbour lands on it (live, 12:47–12:55, "It's
  refusing to go there"); the agent cannot click it either (fullscreen Proton unreachable by the desktop tools,
  virtual pad would seize player 1). The radial above is the controller's door.

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
