# Passive skill tree: nodes, names, allocation, edges (PoE2 0.5.5e, 2026-10-23)

Read live, read-only (`tools/computer_use/peek.py` `Process` plus scratch scripts; input only through
`desktop.py`: radial menu to open, left stick to move, Circle to close, nothing allocated), Spanish client,
controller mode, Mercenary "yarkey" level 2 with 0 unspent points, PID 30604, InGameState S = `0x46678E53F10`.
UI dumps `debug/ui-tree-InGameState-20261023-202041.txt` and `-203026.txt` (tree open, visible-only).
Confidence: **MEASURED** = read live and checked against game output; **INFERRED** = consistent, not proven
on every case; **OPEN** = not known.

## Elements (G = `S+0x318`, M = `S+0x310`)

- MEASURED: **`G+0x3C0` = passive panel P** (title `+0x2E8` = "Pasivas"; child 2 of `panels_parent`, which is
  child 2 of the shared panels layer `G+0x378`). The pointer, the panel and everything below it **persist while
  the tree is closed**; closed = P's visible bit (0x0B of `+0x168`) clear, `M+0x760` = 0 (checked after Circle).
  GameHelper's `PassiveSkillTreeNodesChildPath {24, 2}` and `ImportantUiElementsOffsets.PassiveSkillTreePanel`
  (`G+0x730`) are stale in 0.5.5e (G child 24 has one unrelated child; `G+0x730` is another element).
- MEASURED: **canvas = P child 0** (14030×14039, pans with the view). Its **1372 children are groups** (first 4
  empty), each group's children are the node elements: **4477 elements of one class** (one vtable — take it at
  runtime), every one with a node record at `+0x2C8` (below); 3 other-class children carry none. Other P
  children: 1 `edge_fade_overlay`, 7 zoom legend ("Zoom", "x1,0"), 8 `expanded_frame` (search box, points,
  attributes), **10 = the cursor reticle** (83×83, screen centre).
- MEASURED: on-screen rect = the element's ordinary UI rect (GameHelper `UiElementBase` position/size, as the
  dumper prints it). Sizes by kind: small 39×39 (2940), notable or jewel socket 57×58 (925+12), keystone 82×84
  (33), icon-only "Maestría …" 113×113 (347). 220 elements are hidden (visible bit clear): 197 ascendancy nodes
  (`row+0x167` Ascendancy FK set) and 23 others (special jewel sockets among them).
- MEASURED: **hovered node = `M+0x760`** (already used by the plugin). The view pans so the hovered node sits
  under the reticle: in both dumps hovered rect centre = reticle centre = (1279.5, 781.5). The game's left-stick
  movement is geometric, not along edges: the stick went Armadura y evasión (59779) → Velocidad de ataque (97)
  → Armadura y evasión 12% → Regeneración de vida, and the third is not linked to the second.
- MEASURED: only hovered nodes have a `+0x120` tooltip (4 of 4477 after 4 hovers), so text must come from the
  dat rows. Tooltip texts: title, "Name\n(English name)", stat lines, "\nSin asignar" (unallocated only).

## Node → record → localized name (every hop checked by the FK's table name)

Foreign key = (row ptr, file ptr), file `+8` = std::wstring table name (same check as the skill bar).

- MEASURED: **node element `+0x2C8` → node record** (a PassiveSkillGraph node, 0xE0-byte objects). GameHelper's
  `SkillTreeNodeUiElement.SkillInfo` `+0x2A0` is null now (stale), and its `+0x08` row hop is `+0x00` here.
- MEASURED: **record `+0x00` = FK to "Data/Balance/PassiveSkills.dat"** (4477/4477). Record `+0x10` u32 = index
  in the PSG node table (below; 7633/7633), `+0x30` → PSG content object, **`+0x50/+0x58` = std::vector of edge
  pointers, out-edges only** (edge `from` == this record in 5385/5386; one self-loop).
- MEASURED: **PassiveSkills row** (unaligned, stride 0x1E7 from consecutive rows): `+0x00` Id
  ("armour_and_evasion2"), `+0x08` icon path, `+0x10` stats count + `+0x18` array of 16-byte FKs to
  "Data/Balance/Stats.dat" (Stats `+0x00` Id "base_physical_damage_reduction"), `+0x20..+0x2C` i32 stat values
  (10, 8 ↔ "+10 a la armadura / +8 a la evasión"; values past the count are junk), **`+0x30` u16
  PassiveSkillGraphId** (unique across 7632 rows; 59779), **`+0x32` Name = localized name** (equals the tooltip
  title on all 4 hovered nodes), `+0x3A` Characters list (count, ptr → FKs to "Data/Balance/Characters.dat"),
  **`+0x4A` IsKeystone** (33, = the 82×84 elements), **`+0x4B` IsNotable** (984), **`+0x54` icon-only** (368 of
  the 375 "Maestría …" rows, no stats; INFERRED decoration), **`+0x65` IsJewelSocket** (17/17, 0 others),
  `+0x12F` attribute node (293/293 "Atributo", 0 others), `+0xAC` FK PassiveSkillMasteryGroups, **`+0x167` FK
  "Data/Balance/Ascendancy.dat"** (ascendancy nodes), `+0x178` FK PassiveSkillTreeNodeFrameArt, `+0x1B3` FK
  PassiveSkillVariantTypes.
- MEASURED: stat lines are not cheap: the row holds Stats ids and raw values only; the text needs the stat
  description files (12 per minute is shown "0.2% de vida por segundo"). Use the hover tooltip for stats.

## Class overrides (2026-11-09)

- MEASURED (game files, 0.5.5f): **ClassPassiveSkillOverrides** (61 rows of 0x30: Characters key +0x00, the PassiveSkills
  row replaced +0x10, the row shown +0x20) swaps nodes for three classes only: Witch `Metadata/Characters/Int/IntFour`,
  Huntress `Dex/DexFourb`, Druid `StrInt/StrIntFourb`. The node record keeps the replaced row; the game shows the other
  one (its own name, stats and graph id). A Druid heard `spell_criticals7` "Daño crítico de hechizos" over the tooltip of
  `druid_class_start22` "Regeneración de vida", `criticals25_` "Destino cruel" over "Espíritu primitivo", `area_attacks4`
  "Área de ataques" over "Daño de hechizos y ataques" (2026-11-07 and 2026-11-09); the Mercenary has no overrides.
- The plugin reads the shown row for the player's class (catalog `passive-overrides.txt`, extractor `passive-overrides`;
  `UiSemantics.ShownRow`: the replaced row plus (shown − replaced) × the row size 0x1E7, trusted only when the Id there
  is the shown one). Name and kind come from it, the graph id, start and stick gate from the record's own row; a node
  counts as allocated under either graph id. **NOT MEASURED:** which of the two the allocated list keeps for an overridden
  node, and AscendancyPassiveSkillOverrides (13 rows, `AscendancyWitch3*` → `AscendancyAltWitch*`, its first key into a
  table not identified), not handled: the ascendancy view was never seen live.
- PassiveSkills row (0x1E7 bytes): Id +0x0, icon +0x8, stats key list +0x10, values from +0x20, graph id u16 +0x30, name
  +0x32 (unaligned: `dat-row-strings` steps by 4 and misses it).

## Allocation

- MEASURED: **tree state object T = canvas `+0x3D8`** (`+0x3E0` = T−0x10, a make_shared control block). Checks:
  `T+0x160` → file "Metadata/PassiveSkillGraph.psg", `T+0x170` → "Data/Balance/PassiveSkillTrees.dat",
  `T+0x178` ClassPassiveSkillOverrides.dat, `T+0x180` AscendancyPassiveSkillOverrides.dat, `T+0x190`
  PassiveSkillVariants.dat. It persists while the tree is closed.
- MEASURED (one allocated node; it is all this character has): **`T+0x1A8` = std::vector of 4-byte entries
  whose low u16 is the PassiveSkillGraphId of an allocated node**: one entry `83 E9 00 56` = 59779, the only
  node whose tooltip lacks "Sin asignar". INFERRED: entry = {u16 id, u8 (weapon set?), pad}; `T+0x1D8` is the
  [choices' list](#attribute-choices) (2026-11-14), `T+0x1C0` stays empty (OPEN: the weapon sets'). Not found in PlayerServerData (`AreaInstance+0x5B0`
  → `+0x48`[0]) within two hops.
- MEASURED: **node element `+0x64C` byte = 1 when allocated** (exactly 59779 of 4477) and `+0x648` = 0 on it, 4
  on every other node, including the three nodes linked to it. So the UI holds no "can allocate" state, at
  least with 0 points (OPEN: values with points to spend).
- MEASURED: **class start nodes are not UI elements** (the portrait): PSG records whose row has a non-empty
  Characters list: MERODEADOR 47175, BRUJA 54447, EXPLORADORA 50459, DUELISTA 50986 (Duelista, Mercenario),
  TEMPLARIO 61525, SEIS 44683. **Own start** = the one listing a Characters row whose `+0x00` Id equals the player
  entity's metadata path ("Metadata/Characters/StrDex/StrDexFourb"; Characters `+0x08` = "Mercenario"); the
  allocated node 59779 is linked to DUELISTA, which confirms it.
- INFERRED: state = allocated (in the list), **reachable** (not allocated, linked to an allocated node or the own
  start), else **locked**; "can allocate now" = reachable and points > 0. OPEN: jewels that allow unconnected
  allocation, weapon-set points, ascendancy.

## Edges

- MEASURED: **PSG content C = `[T+0x160]+0x28`** (= every record's `+0x30`). **`C+0x58/+0x60` = std::vector of
  6076 edge pointers**; edge = `+0x00` from record, `+0x08` to record, `+0x10` u32 index (= position, 6076/6076),
  `+0x14` i32 (signed arc/orbit value), `+0x18` u32 flags (0, 1, 1024, 1025). `C+0x20` = vector of 7633 0x18-byte
  entries (row, file, record) indexed by record `+0x10`.
- MEASURED: drawing every node–node edge between the dump rects onto a screenshot lands on the game's lines
  (straight chords for the curved ones). **Edges touching an icon-only node are not drawn** (two notables "linked"
  through a red mastery glow had no line between them), so drop them. Undirected adjacency = union of both
  directions; per-record vectors alone miss incoming edges.
- So the geometric fallback is not needed for neighbours; geometry is only for the direction words.

## Live readout (prototype, dump 203026 rects, screen y down)

- Armadura y evasión (small, **allocated**, 59779): DUELISTA start; abajo 219 px Velocidad de ataque
  (reachable); abajo 211 px Armadura y evasión (reachable); abajo-izquierda 324 px Atributo (reachable).
- Velocidad de ataque (small, reachable, 97): arriba 219 px Armadura y evasión (allocated); arriba 219 px Daño de
  proyectiles (reachable: linked to the start); abajo-derecha / abajo-izquierda 62 px Velocidad de ataque (locked).
  Two neighbours share "arriba" (a fork), so direction words need a tie-break.
- Regeneración de vida (small, locked, 14926, hovered): abajo 65 px Duro de matar; arriba 308 px Endurecido por la
  batalla (both locked).
- Points: **P child 8 → child 1 (`top_points_layout_gmp`) → child 0 → child 1 "Puntos de habilidad:", child 2
  "0"**; the attribute circles (DES 11, INT 7, FUE 11) are child 6 of the same layout. INFERRED: its 6 hidden
  siblings are the weapon-set and ascendancy counters.

## Reader sketch

1. Locate: P = `G+0x3C0`; canvas = P child 0; T = canvas `+0x3D8`; check T's `+0x160` and `+0x170` file names.
   Open = P visible. C = `[T+0x160]+0x28`.
2. Graph (static per session, cache it): walk canvas → group → elements; keep each element whose `+0x2C8`
   record has `+0x00` FK to PassiveSkills.dat; read Id, Name, GraphId, kind bytes. Drop hidden and icon-only
   (`+0x54`). Edges from `C+0x58`: add both directions, skip self-loops and icon-only endpoints, keep start
   records (name from row `+0x32`) as neighbours. Own start via Characters Id = player entity path.
3. Per hover change (`M+0x760` → element → record): allocated set = low u16 of each `T+0x1A8` entry
   (cross-check `+0x64C`); points from the label above; state per node as above.
4. Directions: angle from the hovered element's rect centre to each neighbour's (8 sectors, screen y down;
   start nodes: the portrait's position, OPEN); sort clockwise from up; same sector → order by angle and say
   "arriba, arriba" or use finer words. Announce "Velocidad de ataque, menor, alcanzable. Arriba: Armadura y
   evasión, asignado; …"; stats keep coming from the tooltip.
   MEASURED: the game builds a node's tooltip ~150 ms after the hover (log 2026-10-24 10:49:15.635 → .787).
   Name and state are said at once; stats, then links, follow with the tooltip. Links said before it made the
   late stat sound like the last neighbour's ("…, Projectile Damage. 10% increased Attack Damage", user
   report 2026-10-29).
5. Fault checks: every FK checked by table name; an allocated id with no element, or a `T+0x1A8` entry count
   that disagrees with the `+0x64C` flags, logs once and falls back to the tooltip's "Sin asignar".

OPEN: `+0x648` values and `T+0x1A8` entry layout with several or weapon-set allocations; the ascendancy view
(hidden nodes); start-node screen position; whether the stick can stop between nodes (`M+0x760` = 0).

## Stick movement (2026-10-31, kehgan, Spanish client)

- MEASURED: a push of the left stick (an octant) lands on the node scoring lowest within **45 degrees** of the push:
  **distance × (1 + 1.5 × tan(angle off the push))**, in screen pixels between element rect centres. Fitted to 127 moves
  recorded with the dev command `passivenear` (hovered node, the nodes around it with angle, distance and link, then
  the push and where it landed) at zoom x1,0, x1,9 and x0,6: 120 right (35/37, 30/30, 28/30, 27/30); the misses are
  near-ties or nodes 20-40 px apart at the farthest zoom. Links play no part: a node 800 px away is reached in one push
  when nothing lies nearer within the cone. Other forms tried and worse: distance × (1 + k × angle/45) (116),
  distance + c × angle (scale-dependent), links first (104). `PassiveStick` (pure, unit-tested).
- So a link is named by the push that reaches it (the nearest octant among those whose pick it is), or "not in one push"
  when none does; checked live on six links in a row (all landed as said).
- Zoom: LT/RT; the factor is the text "x1,4" beside the "Zoom" label (P child 7); the screen differ took its change for
  a counter tick until 2026-10-31.
- MEASURED (2026-11-08, yarkey, `stick-test`/`stick-walk` with the virtual pad, Y centring on armour_and_evasion2): the
  game lands on the same nodes at 1280x1024, 2560x1080 and 2560x1440 (eight pushes from one node, identical), so it picks
  in the tree's own coordinates. The model's input was one frame's screen pixels cached when the tree opened, and that
  frame gave other picks at 5:4 and 21:9 (2 of 8 right at 21:9). `PlacesOf` now takes laid-out places under the canvas
  (`UiSemantics.LayoutRect`: node and node holder are scale index 2 at local scale 0.42, the holder's RelativePosition is
  the tree place), the same at every window, zoom and pan: picks became identical at 5:4 and 21:9. Accuracy on random
  walks: 100/120 at 21:9 (seed 7, many pushes in the dense start area), 107/120 at 16:9 (seed 11). Refits on the 21:9 walk
  (angle penalty 0.75-1.0, linked nodes ×0.85-0.9) scored 108-110 there but 102-105 on the 16:9 walk, worse than the
  current rule's 108: kept as fitted. Misses: a linked node chosen over a nearer one straight ahead (down from
  armour_and_evasion2 lands on attack_speed2_, not attack_speed1), and pushes that do not move although a node lies
  ~800 px (21 node sizes) within the cone, while one 959 px at 15 degrees was reached.
- READ FROM THE GAME'S CODE (2026-11-08, static disassembly of the exe, report and tools in
  D:\code\decompilers\projects\poe2-research\re\stick-rule.md, `validate.py -v`): the step is the function at RVA
  0x9B9BA0 on the tree canvas (`canvas+0x1368` = current node record = the hovered node's `+0x2C8`), given the stick's own
  direction (y flipped, not snapped to octants). (1) Linked nodes first: a link within 19.95 degrees (`dot >= 0.94`) is
  taken; the code computes `(1.5 - 8.333 * (dot - 0.94)) * distance` but stores the dot instead, so likely the first such
  link in the node's edge list wins; this stage is skipped when the node's row has a non-zero byte at `+0x66` (ascendancy?).
  (2) Else the lowest `(5 - 4 * dot) * distance` among nodes with `dot > 0.7071` (the constant at 0x9CA139 is 0x3F350481 =
  0.7071f, acos 45.0005 degrees, so a node at exactly 45 degrees is in the cone: 4 recorded pushes landed on one). (3) In
  both, only nodes whose centre is inside the game window, visible and selectable; no other distance limit (the pushes that
  did not move had their nodes off the window's top or bottom; the cursor sits at the panel's centre). (4) None: no move.
  Applied to the 240 recorded pushes: 240/240 (the fitted model 208; without the on-screen test 229, without the linked
  stage 234). Holding the stick past half deflection for 0.16 s pans the view and snaps on release to the on-screen node
  nearest the centre (0xA194C0, 0xA19920, 0xA1A410).
- IMPLEMENTED (2026-11-08): `PassiveStick.Pick(offsets, octant, linked, window)` is the game's rule, stages 1 and 2 with the
  game's float constants and comparisons (the linked stage keeps the cosine as its best score, as the code does, so the first
  link in edge order wins). Inputs from `UiSemantics.PassiveTree`: offsets are the laid-out places under the canvas;
  `linked` is the hovered record's own edge vectors (`+0x38` in-edges, other end at edge `+0`; `+0x50` out-edges, other end
  at `+8`; `GameLayout.PassiveTree.RecordInEdgesOffset/RecordOutEdgesOffset`), trusted only when every edge names the record
  and a node the file links it to, else the same order rebuilt from the graph file's edge list (in-edges in file order, then
  out-edges; `PassiveGraph.StickLinks`); the `Window` is the hovered node's screen centre (GameHelper's absolute rect),
  `Core.Process.WindowArea`'s client size and the scale = the node's screen width / its canvas width. The row's 8 bytes at
  `+0x66` (`RowNoLinkedStepOffset`, column unknown; the code also shifts such nodes by the sub-canvas, likely ascendancy)
  are a key (16 bytes in the file: null on `blind17`, row 4 on `AscendancyWarrior2Small9`; `dat-kinds`, 2026-11-10), set on
  ascendancy rows. Applied since 2026-11-10: a node that holds it gets no linked stage (`PassivePlaces.Gated`,
  `StickLinksOf`), and a push from it is not judged by the self-check, as the sub-canvas shift is not modelled; when over a
  third of the shown nodes read it set, the bytes are another column's and are ignored. Not confirmed live yet (dev
  command `passivenear` on an ascendancy node: `no linked step True`). The multiple-choice redirect
  (`row+0x8B`, `elem+0x648`) is not implemented. When the window or the rects are unreadable, the fitted model stands in
  (`PassiveStick.PickFitted`, 208/240). `Route` (finder and Build Planner ways) uses the same rule, the first step with the
  hovered record's live edge order and every other with the file order. Tests: `PassiveStickTests` replays the 240 pushes
  from `tests/Poe2Accessibility.Tests/Fixtures/stick-walks.txt` (cone candidates only, screen pixels, the cursor at the
  panel's centre: 1279.75, 585.9 at 2560x1080; 1279.75, 781.2 at 2560x1440) at 240/240; the recordings hold no edge order,
  so the three pushes with two links in the cone (down from armour_and_evasion2) feed the links by the game's linked score.

### Live confirmation protocol (run 2026-11-08, yarkey, Mercenary level 22, 2560x1440)

On the passive tree with the virtual pad, ResearchMode on so `passivenear` logs (`debug/command.txt`), screen reader on:

1. Y to centre on the allocated nodes; `passivenear` for the hovered node. The log must show `window WxH cursor X,Y px per
   unit S` with W, H the client size and X, Y about the panel's centre (not `none (fitted model)`), and `edge order record:
   … ; file: … ; no linked step False`. The record's order must list the same ids as the file's; note whether in the same
   order (if `unreadable`, the record vectors moved: the file order is in use).
2. Hover armour_and_evasion2 (Duelist/Mercenary start area, the node where the walks began) and `passivenear`: the record's
   edge order must have attack_speed2_ before armour_and_evasion1 (the game landed on attack_speed2_ with down, three
   times); if the opposite, the first-wins reading is wrong and 0x9B9F1A-0x9B9F26 must be re-read.
3. From that node push each of the 8 directions once (0.2 s), returning with the opposite push or Y: every landing must be the
   id the `picks` line named before the push, including the `-` ones (no move). Then at zoom x0.6 and x1.9 (LT/RT) the
   same 8 pushes: the picks line must change where nodes leave or enter the window, and the landings must match it.
4. Each hovered node's links line ("arriba: …, abajo izquierda: …") must name the push that lands on it: follow six links in
   a row by the push said; six of six must land on the node named.
5. Finder: pick a keystone with the D-pad and follow its "N pushes" way; the count must fall by one per push with no
   correction (a push landing elsewhere shows as a count that does not fall).
6. Any miss: `passivenear` on the node before the push and paste the `picks`, `window` and `edge order` lines with the
   landing into docs/research/passive-tree.md.

Results (0.5.5f, virtual Xbox pad): (1) after Y `window 2560x1440 cursor 1279,8,781,2 px per unit 0,3780`, `edge order record:
duelist597 strength34 attack_speed2_ armour_and_evasion1; file:` the same ids in the same order, `no linked step False`.
(2) attack_speed2_ before armour_and_evasion1: the first-wins reading holds. (3) The 8 pushes from armour_and_evasion2 landed
on the picks' ids 8/8 at x1.0 (`up -`: no move), 8/8 at x0.5 (up then picks spells18, which entered the window, and landed
there) and 8/8 at x1.9. (4) Six links followed by the push said (down, down-right, down-right, down, down, down-right): 6/6.
(5) Oasis from the end of that walk: one miss. On dexterity66 (Attribute) the way said "down-right, 3 pushes" and the push
landed on lightning11 (as the picks said), which said 7; reached again from lightning11 the same node said 8. Cause: the
readout comes a frame after the push while the view still pans: `passivenear` at once gave `hovered dexterity66 at 1309,695`,
`cursor 1307,3,699,5`, settled 1.5 s later `1280,781`. A window placed on the mid-pan node lets far nodes in, so the route and
even the links line ("right, not in one push, Lightning Damage" then, a frame later, "down-right, Lightning Damage") changed
with the side the node was reached from. The window now sits where the node rests, the centre of the canvas's frame (the
panel element `0,122 2560x1318`, `PassiveStick.SettledCursor`): dexterity66 says 8 from both sides and a walk from
armour_and_evasion2 counted 15, 14 … 1, "Arrived at Oasis" with no correction.

## Finder and what the pad does in the tree (0.5.5f, 2026-11-07, malgdhler)

- MEASURED, the virtual pad on a node (hint bar: "Editar filtro [L3]", "Modo inspección [R3]", "Zoom [RT]", "Centrar [Y]",
  "Seleccionar [A]"): L3 opens the filter, R3 inspect mode, Y centres on the allocated nodes, LB goes to the Character panel and
  RB back, X did nothing with no change pending. The **right stick moves the cursor too**, so it is not free. The stick rule's self-check judges only a left push that does not start where the right stick
  left the cursor: the user's evening of 2026-11-09 lost the rule twice ("7 hit, 3 missed") right after right-stick moves.
  MEASURED (2026-11-09 17:22, the user's Druid, zoom x0,4, trace `Tree right stick` of v2026.11.09-5): the right stick
  **pans the view**: the canvas slides (deflection 0.55: 5 px in the first 100 ms) while the cursor stays at the frame's
  centre (1280,781 in 2560x1440) and hovers whatever node passes under it ("no node" in between); **on release the view
  eases until the hovered node sits at the centre**, about 1 s (placeholder2 drawn at 1311 → 1280; spell_criticals7 1269 →
  1280). Once settled the cursor is back on the step rule's footing; a left push during that ease starts from an
  unsettled view, which is why the push after the right stick is not judged. **The D-pad does
  nothing in any direction**: it is the finder's (`Poe2AccessibilityCore.FindOnTree`, `ModCommands.OnTreeDirection`).
- MEASURED: "No cumples los requisitos" ends the tooltip of every node not linked yet, small ones and keystones alike, and
  not one reachable ("Atributo … alcanzable"). No node of the tree was seen with a level condition.
- MEASURED (dev command `passivemap`: links from each class start, drawn nodes only, other starts not walked through):

  | Start | Nearest keystones | Nearest notables | Keystones within 20 / 30 / 40 points |
  |---|---|---|---|
  | Ranger, Huntress | 11, 12, 12 | 5, 5, 7 | 13 / 28 / 33 |
  | Marauder, Warrior | 12, 12, 13 | 5, 5, 7 | 18 / 29 / 33 |
  | Duelist, Mercenary | 11, 12, 12 | 5, 5, 7 | 13 / 29 / 33 |
  | Druid | 9, 11, 13 | 5, 5, 7 | 19 / 31 / 33 |
  | Witch, Sorceress | 11, 13, 13 | 5, 5, 7 | 19 / 32 / 33 |
  | Shadow, Monk | 12, 13, 13 | 5, 5, 7 | 17 / 30 / 33 |

- 3910 drawn nodes: rings by thirds of the tree's half-size hold 336 / 1476 / 2098 (`PassiveMap.Zone`, centre of the bounding box).
- Cost is `PassiveMap.Steps`: breadth-first along the links from the allocated ids and the own start. The way is
  `PassiveStick.Route` (cursor pushes, not links: the cursor needs no link to look at a node), recomputed on every hover, so a
  push that lands elsewhere corrects itself. Followed live from the far side of the tree: 31 pushes, the count falling, to
  "Llegaste a Fe cicatrizada"; beyond 30 pushes (60 since 2026-11-10) it says "lejos" and the direction.
- Where you left off (2026-11-10, the user: "el juego al cerrarlo y abrir regresa el árbol al inicio"): `PassiveResume` keeps
  each character's last hovered node id (`config/passive-resume.txt`), but for the node the tree opens on; a passive node read
  after 5 s without one is the tree opening (`TreeLeftMs`; a popup over the tree is shorter and keeps the node), and with no
  build's next node the finder gets `Return(node)`: "Donde lo dejaste, X: dir, N" to "Estás donde lo dejaste"; a node
  allocated elsewhere drops it. Not tried live.
- A target chosen on a node must not change that node's tooltip: the narrator says a line replaced in place, which cut off the
  finder's own answer 10 ms after it began (`UiSemantics.FinderLine` keeps the line while the cursor stays and the allocated
  count is the same: a node allocated under the cursor moves the trail on, said as a replaced line).
- Trail (2026-11-09, asked by the user: "una mejor forma de guiar al jugador" to keystones, jewel sockets and a build's
  notables): the cursor way only showed where a node is, and "8 points away" left the 8 nodes to find. After the cursor has
  been on the target (the game's tooltip says what it does: the mod has no stat text of its own), `PassiveFinder.Taking` turns
  the guide to `PassiveMap.Trail`: Dijkstra from the taken nodes and the own start over node costs
  (`PassiveMap.TrailCost`: a point = 1024, 2 off on the active build's marked nodes, 1 off on a notable, so the fewest
  points win and ties go to the build, then to notables; toward a marked node an unmarked one costs 64 points, so the trail
  keeps to the build's own line). The line names the trail's first node (always reachable) and the cursor way to it. The D-pad's
  Build list holds the marked nodes to take now, small ones and the hovered one too, in the `.build` file's order when it
  is here ([as many as the points pay for](#the-ascendancys-tree); the keystones, notables and sockets not taken until
  2026-11-14: the owner heard "0 de 3 asignados" over two entries).
- Brief guide (2026-11-10, the user's play log: one node read as name, stats, "Seleccionar, A. Mantén presionado A para
  seleccionar el tipo de puntos", "No cumples los requisitos", the build step, the author's note twice (the `.build`
  file's and the game's own note tooltip, differing by one full stop), every link, then the build's way, 5-10 s a push):
  `UiSemantics.GuideAt` works out the guide once per node and allocation (finder first, else the build's next node) as a
  `GuideSay` (line, here, on the line, next octant); with `PassiveGuideBrief` a node on the way is `Text` = its name,
  `Tooltip` = the way, said without waiting for the game's tooltip, which follows it (the node's stats, not the build's step and note;
  first the way alone, then on the user's wish the stats after it: "que la guía tenga más prioridad", "los tooltips del árbol"), and the goal is said only when its key changes
  (`GoalAndWay`); every line stays in `AccessibleUiNode.Details` for L3 held. `LinesOf(…, asides)` sets the button hints
  (a glyph image or inline glyph) and red lines without digits apart. A directional sound for a node of the line
  (`CueSynth.GuideStep`, three candidates) was tried and dropped the same day: the user found the brief line enough
  ("la guía la hiciste tan bien que puedo guiarme muy fácil"); it never played anyway, the tree's settings being applied
  before `UiSemantics` existed (`ApplyPassiveTree` now runs again once it does).
- MEASURED from the user's log (2026-11-10, Mercenary, 71-step build): after the attribute node's popup ("+5 a la fuerza,
  1 de 3", held A) the next push landed elsewhere than predicted three times in four ("Atributo: abajo a la derecha, 1"
  landed on "Defensas y vida de compañeros", then on `block20` instead of `jewel_slot1970`); with 3 misses in 10 the rule
  was lost and every way said "lejos" for the rest of the session. Why the popup shifts the push is not measured. Now the
  first landing after any other focus is not judged (`PassiveFocusLeft`), `StickCheck` loses the rule at 5 misses in 10 and
  gives it back at 2 or fewer (pushes are still judged while lost), and an untrusted way says the direction alone.
- Row names may hold keyword markup ("Engarce de [Jewel|joya]"); `ReadPassiveRow` shows the display text.

## Build Planner (patch 0.5, 2026-10-31)

- MEASURED: **node element `+0x70C` byte = 1** while the node is in the active build (the same five elements read with a
  build chosen and with "None": nothing else of them changed). Weapon sets are drawn as ring colours (red set I, green
  set II); the author's note (`additional_text`) is drawn beside the hovered node **outside the UI tree** (absent from a
  full dump; its text lives in a text-layout object and in the parsed JSON, both found by a memory scan, neither on a
  path from the tree).
- MEASURED (2026-11-07, a test build): a node with a note owns **two tooltips** (vector +0x120, 0x18 bytes each): first the
  note, then the node's stats. Reading the first alone said the note and never what the node does (a tester: "it simply just
  tells you that they are in the build"). `TooltipEntries` reads them all: stats first, then the note, so a website build's notes
  are read too. The string at +0x620 ("Fuerza\nStrength\n\n+8 a la fuerza") is no fallback: all nodes had it in a Spanish session
  that had used the tree's search, none in an English one.
- The files: `Documents\My Games\Path of Exile 2\BuildPlanner\*.build`, JSON (GGG developer docs, "Build Planner" v1):
  `passives` = ids (PassiveSkills `Id`) or objects {id, level_interval, weapon_set 0-2, additional_text}; the GGG example
  writes attribute advice as notes ("<m>{<red>{Strength +5 is recommended}}"); Mobalytics exports carry no passive notes.
  The mod reads the file whose passives cover at least half of the marked nodes ([which file](#which-file-the-marks-are)): step = place among
  the marked nodes in the file's order ([the two counts](#the-builds-two-counts)), weapon set, note without markup, and for an attribute node (`strength17`, `attributes65`…) whose note shows the
  attribute only by colour, that attribute. A website-only build has marks but no file: no notes.
- The mark byte has no second signature on the element. `BuildPlan.MarksPlausible`: when over a quarter of the nodes read
  marked (no build takes that many; a byte moved by a patch would) the marks are dropped, "passive build marks" is reported
  missing, and the build known from the game's settings stays; a file whose passives cover the marks reports them found.
- The build's gems and equipment: [ui layers](ui-layers.md#build-planner-gems-055f-2026-11-10).

### The build's two counts

MEASURED (2026-11-10, yarkey, "Campaign Progression - [0.5.5] Twister G.build"): the file has **85** `passives` entries
and the tree marks **60** nodes. The entries hold 64 distinct ids: 21 are a node named again — 5 under a weapon set
(`dexterity82_`, `attack_speed2_`, `attributes29` in set 1; `dexterity66`, `dexterity20` in set 2) and 16 attribute
nodes listed once more at the end of the file (entries 69-84, no extra field) — and 4 are `AscendancyMercenary3…` nodes
(entries 65-68), which the tree of a character that has not ascended does not mark. Live: all 60 marked ids are in the
file and exactly 4 of the file's ids are not marked (the count, heard as "4 más de la build no están en este árbol"; the
four ids themselves were not logged). `BuildPlan.StepsOver` numbers the steps over the marked nodes, each at its first
place in the file, so the Build list's "16 de 60 asignados" and a node's "Build: paso 1 de 60" count the same nodes; the
file's nodes the tree does not show are said once, with the list. An ascendancy's start is no step and is not counted
(2026-11-14): ascending gives it.
- UI: in the tree, L3 opens "Filters/Build Planner"; D-pad right twice reaches the planner button (`collapse_button`,
  A expands it), D-pad down the list (`BuildPlannerComboBox`, "Active Build"). The gem engraving window has the same
  list (`build_planner_combobox`) and a first category of the build's gems.

### The ascendancy's tree

MEASURED (0.5.5g, 2026-11-14, yarkidius, a Warrior just ascended to Smith of Kitava with 0 skill points and 2
ascendancy points; `dump visible` and dev `passivenear`):

- Ascending gives the class's canvas one more child (1372 → 1373 groups): **a canvas of its own among the groups**
  (1134×1134 px at the view's centre at zoom 1, a tree state of its own at `+0x3D8`, another object than the class's,
  with an allocated list of as many entries, 30). Its children are holders of one node element each (24), plus the
  card's `flavour_text_label`. The class's canvas keeps its 220 hidden elements, the ascendancies' among them.
- The stick steps from one to the other (melee17, by the Warrior's start, to "Resistencia al fuego" in one push), and
  the game marks the build's nodes on it with the same byte (`+0x70C`: the start and three more of the owner's file).
- The points left are the tree's own labels, in `top_points_layout_gmp` beside the canvas: the first row's last child
  is the skill points' count ("0"); the ascendancy's row is the one whose label's tagged text holds the keyword
  `AscendancyPoints` ("[AscendancyPoints|Puntos de ascendencia]:"), its last child the count ("2"); the weapon sets'
  rows keep their counts a level deeper.
- Mod: one graph for both (`PassiveGraphOf`: the inner canvas's nodes are read last, over the hidden elements of the
  same records; `TreeCanvasOf` gives the class's canvas for a node of either), so places, the stick rule, the build's
  steps and the finder cover both. The ascendancy's start is the character's own (`PassiveGraph.AscendancyStart`,
  `IsOwnStart`); allocated ids are both states' lists (`AllocatedIn`); the build's next node is one there are points
  for (`PointsLeft`, pure `BuildPlan.PointsOrder`). The graph is read again when the canvas's group count changes.
- MEASURED (the owner took the build's three ascendancy nodes): both canvases got new state objects and **both
  allocated lists hold the ascendancy's nodes** (30 → 33 entries each; `passivenear`: "taken of the ascendancy:
  AscendancyWarrior3Notable6_0 …6_8 …6_9"); the nodes read "asignado" and the guide went back to the class's tree.
- The D-pad's Build list holds what the points left pay for, in the build's order: as many nodes of each tree as it
  has points, the next one alone with none (`BuildPlan.TakeNow`; the owner: "los que puedo asignar en este momento, no
  los posibles"), and its header says so ("2 por asignar ahora").
- **NOT MEASURED:** the stick's step from an ascendancy node, which the self-check does not judge (the game shifts it
  by the inner canvas); the ways counted across the two trees were walked by the owner without a miss heard.

### Attribute choices

MEASURED (0.5.5g, 2026-11-14, yarkidius, a Warrior of level 29 who had just changed three attribute nodes from
Strength to Dexterity; a read-only read of the state while the tree was closed, and the game's files):

- **`T+0x1D8` = std::vector of 4-byte entries {u16 an allocated node's graph id, u16 the graph id of the PassiveSkills
  row chosen for it}**, one per node with a choice. Eight entries, the character's eight attribute nodes (`strength15`,
  `19`, `21`, `58`, `61_`, `67`, `68`, `attributes32`): four read 26297 and four 14927.
- The game's files: **PassiveSkillVariants** (249 rows of 0x20: key to PassiveSkillVariantTypes `+0x00`, key to
  PassiveSkills `+0x10`) lists what a node with a choice can be made. Type `GenericAttribute` has three rows,
  `generic_attribute_strength` (graph id 26297, "Fuerza"), `generic_attribute_dexterity` (14927, "Destreza") and
  `generic_attribute_intelligence_` (57022). Four nodes of Dexterity agrees with the sheet: Dexterity 12 with one such
  node before the change, three more after it (the gold fell by three times 447). The other types are the
  Pathfinder's alternate attribute and the Atlas trees' choices.
- The allocated list beside it (`T+0x1A8`, 35 entries) keeps every node's third byte 0; the fourth reads 0 on 28 and
  FF, DD, FC, 02, FD, DF, FD on seven nodes of no common kind (not read, not understood). `T+0x1C0`, `+0x1F0` and
  `+0x208` are empty vectors. **NOT MEASURED: where a node taken with a weapon set's points is kept** (this character
  has none); dev `passivenear` prints the three vectors' sizes, which shows it once one is taken.
- Mod: `passive-tree.json` carries `variants` (the variant row's graph id → "Id|type", its name with the nodes';
  `PassiveTreeExtractor.Variants`, left out when the table is not two keys a row), `PassiveTreeFields.Choices` reads
  the list (pure `PassiveChoices.Read`: every entry must name a node of the tree and a variant row, else the list is
  another one; tried as far on as the allocated list moved, said lost as `passive attribute choices` on its second
  bad reading). The D-pad's **Attributes** list holds the allocated nodes with a choice, grouped by it, each said
  with it first ("Destreza. Atributo, abajo a la izquierda…"), under a header that counts them ("Fuerza 4, Destreza
  4"). Where the build's note recommends an attribute (`BuildNotes.Recommended`: named, or shown by colour) and the
  node was given another, the node's build lines and the list say so ("la build recomienda Fuerza, tienes
  Destreza") and those nodes come first. A build file with no notes says nothing of attributes: the format has no
  field for the choice (GGG developer docs, "BuildPassive": id, level_interval, weapon_set, additional_text).

### The tree's window

MEASURED (0.5.5g, 2026-11-14, a read-only walk of the window's elements while it was closed; the texts of the play
log's 39 openings of that day, one of them the Hooded One's "Reintegrar pasivas"):

- The canvas's parent holds, besides the canvas: the Build Planner and filter box with `top_points_layout_gmp` (below),
  the zoom, and **a bar of unconfirmed changes**: a part with one child of three, a label ("1 cambio de habilidad
  pasiva sin confirmar", "2 cambios de habilidades pasivas sin confirmar"; "0 cambio…" while hidden), a row with an
  icon and the cost ("4.273 > 3.826", tagged `4.273 > <red>{3.826}`: the gold now and the gold left; the row is
  hidden when the changes cost nothing) and a label with the button's glyph (`<<xbox_button_x;0>>` "Aplicar
  cambios"). The part is hidden while nothing is pending. No element of it has a name.
- `top_points_layout_gmp`, rows in order: skill points (icon, label, count); **one row per weapon set** (icon, "Set de
  armas:", then a pair of labels "4" and " (4)"; two shown, a third hidden); the ascendancy's (label with the
  `AscendancyPoints` keyword, count); **the gold** ("Tu oro:", "3.562": the live amount even while hidden), shown
  only on the refund opening of the 39; the attributes; a hidden "Tributo" row.
- INFERRED: a weapon set's first count is its points left and the rows stand in the sets' order (I, II). The owner had
  "4 (4)" on both with none of the build's eight weapon-set nodes taken, four of them named for each set; what the
  count in brackets is, and which row is which set, was not told apart (both read the same).
- The game's own reading of the bar was said queued and survived on a plain allocation (five times that day) but not
  on the refund: the attribute's popup closes on the node, which is read again and cuts it (three changes unsaid).
- **The chooser of points**: the window's last part, hidden, with no name: one child, hidden too, holding one label
  that read "Asignar\n(Principal)" on the closed tree. The game's hint on a node is "Mantén presionado A para
  seleccionar el tipo de puntos" (2026-11-10), so this is taken for what held A shows. **NOT SEEN OPEN:** its text was
  never among the screen's texts in the play logs, and what it reads for a weapon set is not known.
- Mod (`UiSemantics.PassivePoints`): `PointsLeft` gives the tree's, the ascendancy's and each weapon set's points
  (`TreePoints`); `Refunding` is the gold's row shown; `TreeNews` adds to the node under the cursor, once per change
  of the bar, the count and "Oro: de 4.273 a 3.826", and once the bar is gone with the gold at the amount it
  announced, "Cambios aplicados, te quedan 2.932 de oro". A line the screen's reading already said is not said again
  (`AccessibleUiNode.TooltipNews`, `ScreenNarrator.OnTooltipChanged`). `PointsChoice` adds the chooser's label as one
  more line of the node while both its parts are shown, so a change of it is said as a line replaced.
- LIVE (2026-11-14 09:55, the deployed build, the owner's tree open): `passive attribute choices where measured`;
  `passivenear`: "points 0, ascendancy 0, weapon sets 4/4/0; refunding False; choices strength21=
  generic_attribute_dexterity strength67=generic_attribute_strength …" (four of each), the three lists beside the
  allocated one 0 bytes; heard "Armadura, Siguiente de la build: Daño de ataques, con puntos del set de armas 1:
  izquierda, 11" and, on the node, "…siguiente de la build, … Build: paso 33 de 43. con puntos del set de armas 1".
  The owner came back to the node four times in sixteen seconds and left the tree with nothing taken: the line did
  not say how, the game's hint being left for the details. It now ends "mantén presionado A para elegir el tipo de
  puntos" (`BuildSetPointsHow`). **NOT HEARD LIVE:** the bar's lines, the refund's words and lists, the chooser.

### Refunding

MEASURED (the play log of 2026-11-14 09:25-09:27, the Hooded One's "Reintegrar pasivas"): the tree opens titled
"Reintegrar pasivas" with the gold's row and a "Refund All [hold Y]" hint; an allocated node's tooltip gains its price
("447 oro", an ascendancy's "2.235 oro"); the hint on an allocated node is "Reintegrar [A]" or "Seleccionar [A]", and
on an attribute node A opens the three choices, a change of attribute costing as a refund does.

- Which nodes the game refunds: on the owner's 35 allocated nodes the rule "a node no other allocated node hangs
  from" (`PassiveMap.Held` = 0: the allocated nodes no line of allocated nodes would join to the class's start or the
  ascendancy's were it gone) gives 9 nodes; the four the game offered "Reintegrar" on are among them (Metal fluido,
  Símbolo fundido, Estímulo sangriento, and no change of hint between the first two) and the six it offered
  "Seleccionar" on hold 1 to 22 nodes each. Not seen: the hint on an attribute node that holds none.
- Mod: while refunding, an allocated node says "asignado, se puede reintegrar" or "asignado, sostiene 3 nodos"
  (`RefundState`), the D-pad has a **Refundable now** list (those that hold none, nearest first; empty, so passed
  over, on any other opening), and the build's guide does not lead (its next node is none to go to: with no target
  chosen a node is its name, state and stats, as brief as on a guide's way). The finder's lists still lead.
- The build's weapon sets: a node the file names with `weapon_set` (also when it names it twice, once without) is
  paid from that set's points: `TreePoints.PoolOf`, `BuildPlan.PointsOrder` and `TakeNow` count the tree's, the
  ascendancy's and each set's points apart, so with no skill point and four of each set the build's next node is
  its first weapon-set node, said "Siguiente de la build: Daño de ataques, con puntos del set de armas 1" (on the
  node itself, with the way: "…: mantén presionado A para elegir el tipo de puntos") and the Build list holds the eight. A set whose label does not read is paid from the tree's
  own points. Before this the owner heard "sin puntos por asignar" over eight points.

### Which file the marks are

A guide comes as one file per stage, and a later stage holds the earlier one's tree and more. MEASURED (2026-11-13,
kehgan, the "[0.5] WHIRLING Trinity Martial Artist" files): "Lvl 1-23" has 29 entries over 27 nodes; "23+ Unarmed" holds
all 27 among its 54, in 33 of its 60 entries. Counting entries, "23+ Unarmed" scored 33 against 29 and its gems (Killing Palm,
Falling Thunder) were said for a build of Twister. `BuildPlan.Fit` counts each node once and `BuildPlanFolder.Matching`
picks the most marked nodes held, then the fewest nodes besides (0 against 27 here), then, for files of one tree, the one
the planner's list named last, else the one the game's settings name for the character (`active_builds`).

- Checked over the owner's 37 files (2026-11-13, each taken as the marks, with and without its ascendancy nodes): every
  one is picked, and one pair has one tree and other gems ("Act 2" and "Act 3 - Raxx's 0.5.5 Plant Oracle Druid").
- The planner's list (`BuildPlannerComboBox` in the tree, `build_planner_combobox` in the engraving window) is the game's
  own word: its committed entry, read whenever the list is focused, is the character's build at once
  (`BuildPlanFolder.Shown`, by file name or the file's `name`), so a build changed with the tree closed does not wait
  for the settings. NOT MEASURED: when the game writes `active_builds` (the file was last written at 11:14:42, before
  the game started at 11:14:56, and not in the eleven minutes after). A text of no file changes nothing.
- The entry's text, MEASURED from the play log of 2026-11-13 (05:41-05:57, the owner choosing in the tree's list): the
  build's `name` as the file writes it ("ACT 1 - [0.5.5] Grenades Gemling League", "Ninguno" for none; 33 entries with
  32 files). Over the 37 files of that evening every `name` is at most 40 characters and equals the file's own name but
  for a trailing space (10 files) or a "/" the file name drops (4: "ACT 3/4 - …" is `ACT 34 - ….build`), so a name is
  matched by the file name first and by the one file whose `name` it is otherwise (unit-tested with such a name). While
  the list is open its value follows the highlighted entry ("Eligiendo: …"); the value is read again when it changes,
  so the entry left chosen is the last one noted.
- The log says whose build, which file and why, once per change: `Build plan: kehgan follows Lvl 1-23 - ….build (the
  tree's marks: 27 of 27 marked, 0 besides)`, `(the planner's list)`, `(the game's settings)`.
- The mod remembers a character's file, not its contents: a file written again is read again within five seconds.
